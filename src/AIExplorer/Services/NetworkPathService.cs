using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace AIExplorer.Services;

public sealed class NetworkPathService
{
    private const int ResourceConnected = 1;
    private const int ResourceGlobalNet = 2;
    private const int ResourceRemembered = 3;
    private const int ResourceTypeDisk = 1;
    private const int ResourceDisplayTypeServer = 2;
    private const int ResourceUsageContainer = 2;
    private const int ConnectInteractive = 0x00000008;
    private const int ConnectPrompt = 0x00000010;
    private const int NoError = 0;
    private const int ErrorMoreData = 234;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorAlreadyAssigned = 85;
    private const int ErrorCancelled = 1223;
    private const int ErrorSessionCredentialConflict = 1219;
    private const int MaxPreferredLength = -1;
    private const int UseStatusOk = 0;
    private const int ShareTypeDiskTree = 0;
    private const int ShareTypeMask = 0xFFFF;
    private const int ShareTypeSpecial = unchecked((int)0x80000000);

    private static readonly EnumerationOptions ProbeOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = 0
    };

    private static readonly Regex HostNamePattern = new(
        @"^[A-Za-z0-9](?:[A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeDirectoryPath(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            throw new ArgumentException("경로가 비어 있습니다.", nameof(requestedPath));
        }

        var expanded = Environment
            .ExpandEnvironmentVariables(requestedPath.Trim().Trim('"'))
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        if (IsUncPath(expanded))
        {
            var components = SplitUncPath(expanded);
            if (components.Length == 0)
            {
                throw new ArgumentException("UNC 서버 이름이 비어 있습니다.", nameof(requestedPath));
            }

            return @"\\" + string.Join(Path.DirectorySeparatorChar, components);
        }

        // "Z:" means the current directory on drive Z, not the drive root.
        // Users entering a mapped drive normally mean "Z:\".
        if (expanded.Length == 2 &&
            char.IsLetter(expanded[0]) &&
            expanded[1] == Path.VolumeSeparatorChar)
        {
            expanded += Path.DirectorySeparatorChar;
        }

        var fullPath = Path.GetFullPath(expanded);
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar);
    }

    public static string NormalizeNetworkLocationPath(string requestedPath)
    {
        var trimmed = requestedPath?.Trim().Trim('"') ?? string.Empty;
        if (LooksLikeBareNetworkHost(trimmed))
        {
            trimmed = @"\\" + trimmed;
        }

        return NormalizeDirectoryPath(trimmed);
    }

    public static bool LooksLikeBareNetworkHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar) ||
            value.Contains(Path.VolumeSeparatorChar) ||
            value.Contains(' '))
        {
            return false;
        }

        return IPAddress.TryParse(value, out _) || HostNamePattern.IsMatch(value);
    }

    public static bool IsUncPath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith(@"\\", StringComparison.Ordinal);

    public static bool IsUncServerRoot(string path)
    {
        if (!IsUncPath(path))
        {
            return false;
        }

        return SplitUncPath(path).Length == 1;
    }

    public static string? GetUncServerRoot(string path)
    {
        if (!IsUncPath(path))
        {
            if (!TryResolveToUnc(path, out var uncPath))
            {
                return null;
            }

            path = uncPath;
        }

        var parts = SplitUncPath(path);
        return parts.Length >= 1 ? $@"\\{parts[0]}" : null;
    }

    public static string? GetUncShareRoot(string path)
    {
        if (!IsUncPath(path))
        {
            if (!TryResolveToUnc(path, out var uncPath))
            {
                return null;
            }

            path = uncPath;
        }

        var parts = SplitUncPath(path);
        return parts.Length >= 2
            ? $@"\\{parts[0]}\{parts[1]}"
            : null;
    }

    public static string? GetNetworkParentPath(string path)
    {
        if (!IsUncPath(path))
        {
            return null;
        }

        var parts = SplitUncPath(path);
        if (parts.Length <= 1)
        {
            return null;
        }

        if (parts.Length == 2)
        {
            return $@"\\{parts[0]}";
        }

        return @"\\" + string.Join(Path.DirectorySeparatorChar, parts[..^1]);
    }

    public static bool IsSupportedNetworkLocation(string path)
    {
        if (IsUncPath(path))
        {
            return GetUncServerRoot(path) is not null;
        }

        return IsPotentialNetworkPath(path);
    }

    public static IReadOnlyList<ConnectedNetworkShareInfo> GetConnectedSharedFolders()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var shares = new Dictionary<string, ConnectedNetworkShareInfo>(
            StringComparer.OrdinalIgnoreCase);

        void Add(string? remotePath, string source)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return;
            }

            string? shareRoot;
            try
            {
                shareRoot = GetUncShareRoot(remotePath);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(shareRoot))
            {
                return;
            }

            var shareName = SplitUncPath(shareRoot).LastOrDefault();
            if (string.IsNullOrWhiteSpace(shareName) ||
                shareName.EndsWith('$'))
            {
                return;
            }

            shares[shareRoot] = new ConnectedNetworkShareInfo(
                shareRoot,
                shareName,
                source);
        }

        // NetUseEnum returns the current Windows SMB sessions, including
        // deviceless UNC connections that do not have a drive letter.
        var resumeHandle = 0;
        do
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                var result = NetUseEnum(
                    null,
                    2,
                    out buffer,
                    MaxPreferredLength,
                    out var entriesRead,
                    out _,
                    ref resumeHandle);
                if (result is not NoError and not ErrorMoreData)
                {
                    break;
                }

                var itemSize = Marshal.SizeOf<UseInfo2>();
                var current = buffer;
                for (var index = 0; index < entriesRead; index++)
                {
                    var item = Marshal.PtrToStructure<UseInfo2>(current);
                    current = IntPtr.Add(current, itemSize);
                    if (item.Status == UseStatusOk)
                    {
                        Add(item.Remote, "Windows SMB 연결");
                    }
                }

                if (result == NoError)
                {
                    break;
                }
            }
            catch
            {
                break;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    _ = NetApiBufferFree(buffer);
                }
            }
        }
        while (resumeHandle != 0);

        // WNet is retained as a fallback because some NAS providers do not
        // expose deviceless sessions through NetUseEnum consistently.
        foreach (var resource in EnumerateWNetResources(ResourceConnected))
        {
            Add(resource.RemoteName, "현재 UNC 연결");
        }

        // An active mapped drive is represented only by its remote share.
        // The drive letter itself is intentionally not added to navigation.
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var localPath = $"{letter}:{Path.DirectorySeparatorChar}";
            if (TryResolveActiveMappedDrive(localPath, out var remotePath))
            {
                Add(remotePath, "현재 공유 연결");
            }
        }

        return shares.Values
            .OrderBy(share => share.ServerRoot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(share => share.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MappedNetworkDriveInfo> GetMappedNetworkDrives()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var mappings = new Dictionary<string, MappedNetworkDriveInfo>(
            StringComparer.OrdinalIgnoreCase);

        // WNetGetConnection finds mappings active in the current Windows
        // logon session without touching the remote server.
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var localPath = $"{letter}:{Path.DirectorySeparatorChar}";
            if (!TryResolveActiveMappedDrive(localPath, out var remotePath) ||
                !IsUncPath(remotePath))
            {
                continue;
            }

            mappings[localPath] = new MappedNetworkDriveInfo(
                localPath,
                NormalizeDirectoryPath(remotePath),
                IsConnected: true);
        }

        // Persistent mappings can remain in HKCU\Network even when the NAS
        // is temporarily offline. Keep them visible so the user can reconnect
        // through the existing Windows credential flow.
        try
        {
            using var networkKey = Registry.CurrentUser.OpenSubKey("Network");
            foreach (var subKeyName in networkKey?.GetSubKeyNames() ?? [])
            {
                if (subKeyName.Length != 1 || !char.IsLetter(subKeyName[0]))
                {
                    continue;
                }

                using var driveKey = networkKey!.OpenSubKey(subKeyName);
                var remotePath = driveKey?.GetValue("RemotePath") as string;
                if (!IsUncPath(remotePath ?? string.Empty))
                {
                    continue;
                }

                var localPath =
                    $"{char.ToUpperInvariant(subKeyName[0])}:{Path.DirectorySeparatorChar}";
                var normalizedRemote = NormalizeDirectoryPath(remotePath!);
                if (mappings.TryGetValue(localPath, out var activeMapping))
                {
                    mappings[localPath] = activeMapping with
                    {
                        RemotePath = normalizedRemote
                    };
                }
                else
                {
                    mappings[localPath] = new MappedNetworkDriveInfo(
                        localPath,
                        normalizedRemote,
                        IsConnected: false);
                }
            }
        }
        catch
        {
            // Registry access can be restricted by policy. Active mappings
            // discovered above are still usable in that case.
        }

        return mappings.Values
            .OrderBy(item => item.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<KnownNetworkLocationInfo> GetKnownNetworkLocations()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var locations = new Dictionary<string, KnownNetworkLocationInfo>(
            StringComparer.OrdinalIgnoreCase);

        void Add(
            string remotePath,
            string? localPath,
            bool connected,
            string source)
        {
            if (!IsUncPath(remotePath))
            {
                return;
            }

            string normalized;
            try
            {
                normalized = NormalizeDirectoryPath(remotePath);
            }
            catch
            {
                return;
            }

            var serverRoot = GetUncServerRoot(normalized);
            if (serverRoot is null)
            {
                return;
            }

            var identity = normalized.TrimEnd(Path.DirectorySeparatorChar);
            if (locations.TryGetValue(identity, out var existing))
            {
                locations[identity] = existing with
                {
                    LocalPath = existing.LocalPath ?? localPath,
                    IsConnected = existing.IsConnected || connected,
                    Source = existing.Source.Contains(source, StringComparison.OrdinalIgnoreCase)
                        ? existing.Source
                        : existing.Source + ", " + source
                };
                return;
            }

            locations[identity] = new KnownNetworkLocationInfo(
                normalized,
                localPath,
                connected,
                source);
        }

        foreach (var mapping in GetMappedNetworkDrives())
        {
            Add(
                mapping.RemotePath,
                mapping.LocalPath,
                mapping.IsConnected,
                "매핑 드라이브");
        }

        foreach (var resource in EnumerateWNetResources(ResourceConnected))
        {
            if (!string.IsNullOrWhiteSpace(resource.RemoteName))
            {
                Add(
                    resource.RemoteName,
                    NormalizeLocalDrivePath(resource.LocalName),
                    connected: true,
                    "현재 연결");
            }
        }

        foreach (var resource in EnumerateWNetResources(ResourceRemembered))
        {
            if (!string.IsNullOrWhiteSpace(resource.RemoteName))
            {
                Add(
                    resource.RemoteName,
                    NormalizeLocalDrivePath(resource.LocalName),
                    connected: false,
                    "저장된 연결");
            }
        }

        foreach (var shortcut in EnumerateNetworkShortcutTargets())
        {
            Add(shortcut, null, connected: false, "네트워크 위치");
        }

        foreach (var mountedPath in EnumerateExplorerMountedUncPaths())
        {
            Add(mountedPath, null, connected: false, "탐색기 연결 기록");
        }

        return locations.Values
            .OrderBy(location => location.ServerRoot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static bool TryResolveToUnc(string path, out string uncPath)
    {
        uncPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (IsUncPath(path))
        {
            uncPath = NormalizeDirectoryPath(path);
            return true;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root) ||
            !TryResolveMappedDrive(root, out var remoteRoot))
        {
            return false;
        }

        var relative = path[root.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        uncPath = NormalizeDirectoryPath(
            string.IsNullOrWhiteSpace(relative)
                ? remoteRoot
                : Path.Combine(remoteRoot, relative));
        return true;
    }

    public static bool IsPotentialNetworkPath(string path)
    {
        if (IsUncPath(path))
        {
            return true;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root) || root.Length < 2)
        {
            return false;
        }

        if (OperatingSystem.IsWindows() &&
            TryResolveMappedDrive(root, out _))
        {
            return true;
        }

        try
        {
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    public async Task<NetworkAccessResult> EnsureAccessibleAsync(
        Window owner,
        string requestedPath,
        bool promptForConnection,
        CancellationToken cancellationToken)
    {
        var path = NormalizeDirectoryPath(requestedPath);
        var firstProbe = await ProbeAsync(path, cancellationToken);
        if (firstProbe.Success ||
            !promptForConnection ||
            !IsPotentialNetworkPath(path) ||
            !OperatingSystem.IsWindows())
        {
            return firstProbe;
        }

        var connectResult = PromptForConnection(owner, path);
        if (!connectResult.Success)
        {
            return connectResult;
        }

        return await ProbeAsync(path, cancellationToken);
    }

    public async Task<NetworkAccessResult> ProbeAsync(
        string requestedPath,
        CancellationToken cancellationToken)
    {
        var path = NormalizeDirectoryPath(requestedPath);
        if (IsUncServerRoot(path))
        {
            var shares = await EnumerateServerSharesAsync(path, cancellationToken);
            return shares.Success
                ? NetworkAccessResult.Accessible(path)
                : NetworkAccessResult.Failed(path, shares.Message, shares.Exception);
        }

        try
        {
            await Task.Run(
                    () =>
                    {
                        var directory = new DirectoryInfo(path);
                        using var enumerator = directory
                            .EnumerateFileSystemInfos("*", ProbeOptions)
                            .GetEnumerator();
                        _ = enumerator.MoveNext();
                    },
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);

            return NetworkAccessResult.Accessible(path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return NetworkAccessResult.Failed(
                path,
                "네트워크 위치가 12초 안에 응답하지 않았습니다.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return NetworkAccessResult.Failed(
                path,
                "네트워크 위치에 접근할 권한이 없습니다.",
                exception);
        }
        catch (IOException exception)
        {
            return NetworkAccessResult.Failed(
                path,
                "네트워크 위치가 연결되지 않았거나 응답하지 않습니다.",
                exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return NetworkAccessResult.Failed(
                path,
                "올바른 폴더 경로가 아닙니다.",
                exception);
        }
    }

    public async Task<ServerShareEnumerationResult> EnumerateServerSharesAsync(
        string requestedServerPath,
        CancellationToken cancellationToken)
    {
        var serverRoot = GetUncServerRoot(
            NormalizeDirectoryPath(requestedServerPath));
        if (serverRoot is null || !IsUncServerRoot(serverRoot))
        {
            return ServerShareEnumerationResult.Failed(
                requestedServerPath,
                @"\\서버 또는 192.168.0.10 형식의 서버 위치가 아닙니다.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return ServerShareEnumerationResult.Failed(
                serverRoot,
                "공유 폴더 열거는 Windows에서만 지원합니다.");
        }

        try
        {
            var shares = await Task.Run(
                    () => EnumerateServerSharesCore(serverRoot),
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);
            return ServerShareEnumerationResult.Succeeded(serverRoot, shares);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return ServerShareEnumerationResult.Failed(
                serverRoot,
                "서버가 12초 안에 공유 폴더 목록을 응답하지 않았습니다.",
                exception);
        }
        catch (Win32Exception exception)
        {
            return ServerShareEnumerationResult.Failed(
                serverRoot,
                $"공유 폴더 목록을 읽지 못했습니다. {exception.Message}",
                exception);
        }
        catch (Exception exception)
        {
            return ServerShareEnumerationResult.Failed(
                serverRoot,
                "공유 폴더 목록을 읽지 못했습니다.",
                exception);
        }
    }

    private static IReadOnlyList<ServerShareInfo> EnumerateServerSharesCore(
        string serverRoot)
    {
        try
        {
            return EnumerateServerSharesViaNetApi(serverRoot);
        }
        catch (Win32Exception netApiException)
        {
            // Some NAS devices or Windows policies reject NetShareEnum even
            // though Explorer can browse the same server through the network
            // provider. Use the WNet provider as a compatibility fallback.
            var providerShares = EnumerateServerSharesViaWNet(serverRoot);
            if (providerShares.Count > 0)
            {
                return providerShares;
            }

            throw new Win32Exception(
                netApiException.NativeErrorCode,
                netApiException.Message);
        }
    }

    private static IReadOnlyList<ServerShareInfo> EnumerateServerSharesViaNetApi(
        string serverRoot)
    {
        var shares = new Dictionary<string, ServerShareInfo>(
            StringComparer.OrdinalIgnoreCase);
        var resumeHandle = 0;

        do
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                var result = NetShareEnum(
                    serverRoot,
                    1,
                    out buffer,
                    MaxPreferredLength,
                    out var entriesRead,
                    out _,
                    ref resumeHandle);
                if (result is not NoError and not ErrorMoreData)
                {
                    throw new Win32Exception(result);
                }

                var itemSize = Marshal.SizeOf<ShareInfo1>();
                var current = buffer;
                for (var index = 0; index < entriesRead; index++)
                {
                    var item = Marshal.PtrToStructure<ShareInfo1>(current);
                    current = IntPtr.Add(current, itemSize);

                    if (string.IsNullOrWhiteSpace(item.NetName) ||
                        (item.Type & ShareTypeMask) != ShareTypeDiskTree ||
                        (item.Type & ShareTypeSpecial) != 0)
                    {
                        continue;
                    }

                    var path = $@"{serverRoot}\{item.NetName}";
                    shares[path] = new ServerShareInfo(
                        item.NetName,
                        path,
                        item.Remark ?? string.Empty);
                }

                if (result == NoError)
                {
                    break;
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    _ = NetApiBufferFree(buffer);
                }
            }
        }
        while (resumeHandle != 0);

        return shares.Values
            .OrderBy(share => share.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ServerShareInfo> EnumerateServerSharesViaWNet(
        string serverRoot)
    {
        var serverResource = new NetResource
        {
            Scope = ResourceGlobalNet,
            Type = ResourceTypeDisk,
            DisplayType = ResourceDisplayTypeServer,
            Usage = ResourceUsageContainer,
            LocalName = null,
            RemoteName = serverRoot,
            Comment = null,
            Provider = null
        };

        var resourcePointer = Marshal.AllocHGlobal(Marshal.SizeOf<NetResource>());
        var resourceInitialized = false;
        IntPtr enumHandle = IntPtr.Zero;
        try
        {
            Marshal.StructureToPtr(serverResource, resourcePointer, fDeleteOld: false);
            resourceInitialized = true;
            var openResult = WNetOpenEnum(
                ResourceGlobalNet,
                ResourceTypeDisk,
                0,
                resourcePointer,
                out enumHandle);
            if (openResult != NoError)
            {
                return [];
            }

            var shares = new Dictionary<string, ServerShareInfo>(
                StringComparer.OrdinalIgnoreCase);
            var bufferSize = 64 * 1024;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                while (true)
                {
                    var count = -1;
                    var size = bufferSize;
                    var result = WNetEnumResource(
                        enumHandle,
                        ref count,
                        buffer,
                        ref size);
                    if (result == ErrorNoMoreItems)
                    {
                        break;
                    }

                    if (result == ErrorMoreData)
                    {
                        var newBufferSize = Math.Max(size, bufferSize * 2);
                        var newBuffer = Marshal.AllocHGlobal(newBufferSize);
                        Marshal.FreeHGlobal(buffer);
                        buffer = newBuffer;
                        bufferSize = newBufferSize;
                        continue;
                    }

                    if (result != NoError)
                    {
                        break;
                    }

                    var itemSize = Marshal.SizeOf<NetResource>();
                    var current = buffer;
                    for (var index = 0; index < count; index++)
                    {
                        var resource = Marshal.PtrToStructure<NetResource>(current);
                        current = IntPtr.Add(current, itemSize);
                        if (string.IsNullOrWhiteSpace(resource.RemoteName))
                        {
                            continue;
                        }

                        var shareRoot = GetUncShareRoot(resource.RemoteName);
                        if (shareRoot is null ||
                            !string.Equals(
                                GetUncServerRoot(shareRoot),
                                serverRoot,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var name = SplitUncPath(shareRoot).LastOrDefault();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        shares[shareRoot] = new ServerShareInfo(
                            name,
                            shareRoot,
                            resource.Comment ?? string.Empty);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return shares.Values
                .OrderBy(share => share.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        finally
        {
            if (enumHandle != IntPtr.Zero)
            {
                _ = WNetCloseEnum(enumHandle);
            }

            if (resourceInitialized)
            {
                Marshal.DestroyStructure<NetResource>(resourcePointer);
            }

            Marshal.FreeHGlobal(resourcePointer);
        }
    }

    private static NetworkAccessResult PromptForConnection(
        Window owner,
        string path)
    {
        var ownerHandle = new WindowInteropHelper(owner).Handle;
        string? localName = null;
        string? remoteRoot;

        if (IsUncPath(path))
        {
            remoteRoot = IsUncServerRoot(path)
                ? GetUncServerRoot(path) + @"\IPC$"
                : GetUncShareRoot(path);
        }
        else
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root) ||
                root.Length < 2 ||
                root[1] != Path.VolumeSeparatorChar)
            {
                return NetworkAccessResult.Failed(
                    path,
                    "올바른 매핑 드라이브 경로가 아닙니다.");
            }

            if (TryResolveActiveMappedDrive(root, out var activeRemoteName))
            {
                // The drive letter already exists. Create or refresh the server
                // session without trying to assign the same letter again.
                remoteRoot = GetUncShareRoot(activeRemoteName);
            }
            else if (TryResolveRememberedDrive(root[..2], out var rememberedRemoteName))
            {
                // A disconnected persistent mapping needs its local drive letter.
                localName = root[..2];
                remoteRoot = GetUncShareRoot(rememberedRemoteName);
            }
            else
            {
                return NetworkAccessResult.Failed(
                    path,
                    "Windows에 저장된 매핑 드라이브 연결 정보를 찾지 못했습니다.");
            }
        }

        if (string.IsNullOrWhiteSpace(remoteRoot))
        {
            return NetworkAccessResult.Failed(
                path,
                @"\\서버 또는 \\서버\공유폴더 형식의 경로를 입력해 주세요.");
        }

        var resource = new NetResource
        {
            Scope = 0,
            Type = ResourceTypeDisk,
            DisplayType = 0,
            Usage = 0,
            LocalName = localName,
            RemoteName = remoteRoot,
            Comment = null,
            Provider = null
        };
        var result = WNetAddConnection3(
            ownerHandle,
            ref resource,
            password: null,
            userName: null,
            flags: ConnectInteractive | ConnectPrompt);

        if (result is NoError or ErrorAlreadyAssigned)
        {
            return NetworkAccessResult.Accessible(path);
        }

        if (result == ErrorCancelled)
        {
            return NetworkAccessResult.Failed(
                path,
                "Windows 네트워크 연결이 취소되었습니다.",
                nativeErrorCode: result);
        }

        if (result == ErrorSessionCredentialConflict)
        {
            return NetworkAccessResult.Failed(
                path,
                "같은 서버에 다른 계정으로 연결된 세션이 있습니다. " +
                "Windows 자격 증명 또는 기존 네트워크 연결을 정리한 뒤 다시 시도해 주세요.",
                nativeErrorCode: result);
        }

        var exception = new Win32Exception(result);
        return NetworkAccessResult.Failed(
            path,
            $"Windows 네트워크 연결에 실패했습니다. {exception.Message}",
            exception,
            result);
    }

    private static string[] SplitUncPath(string path) =>
        path.Trim()
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? NormalizeLocalDrivePath(string? localName)
    {
        if (string.IsNullOrWhiteSpace(localName) ||
            localName.Length < 2 ||
            localName[1] != Path.VolumeSeparatorChar)
        {
            return null;
        }

        return $"{char.ToUpperInvariant(localName[0])}:{Path.DirectorySeparatorChar}";
    }

    private static IEnumerable<NetworkResourceInfo> EnumerateWNetResources(int scope)
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var openResult = WNetOpenEnum(
            scope,
            ResourceTypeDisk,
            0,
            IntPtr.Zero,
            out var enumHandle);
        if (openResult != NoError)
        {
            yield break;
        }

        try
        {
            var bufferSize = 64 * 1024;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                while (true)
                {
                    var count = -1;
                    var size = bufferSize;
                    var result = WNetEnumResource(
                        enumHandle,
                        ref count,
                        buffer,
                        ref size);
                    if (result == ErrorNoMoreItems)
                    {
                        break;
                    }

                    if (result == ErrorMoreData)
                    {
                        var newBufferSize = Math.Max(size, bufferSize * 2);
                        var newBuffer = Marshal.AllocHGlobal(newBufferSize);
                        Marshal.FreeHGlobal(buffer);
                        buffer = newBuffer;
                        bufferSize = newBufferSize;
                        continue;
                    }

                    if (result != NoError)
                    {
                        break;
                    }

                    var itemSize = Marshal.SizeOf<NetResource>();
                    var current = buffer;
                    for (var index = 0; index < count; index++)
                    {
                        var resource = Marshal.PtrToStructure<NetResource>(current);
                        current = IntPtr.Add(current, itemSize);
                        if (!string.IsNullOrWhiteSpace(resource.RemoteName))
                        {
                            yield return new NetworkResourceInfo(
                                resource.LocalName,
                                resource.RemoteName,
                                resource.DisplayType,
                                resource.Usage);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            _ = WNetCloseEnum(enumHandle);
        }
    }

    private static IEnumerable<string> EnumerateNetworkShortcutTargets()
    {
        var shortcutsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Network Shortcuts");
        if (!Directory.Exists(shortcutsFolder))
        {
            yield break;
        }

        IEnumerable<string> shortcutFiles;
        try
        {
            shortcutFiles = Directory.EnumerateFiles(
                shortcutsFolder,
                "*.lnk",
                SearchOption.AllDirectories).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var shortcutFile in shortcutFiles)
        {
            var target = TryResolveWindowsShortcut(shortcutFile);
            if (IsUncPath(target ?? string.Empty))
            {
                yield return target!;
            }
        }

        IEnumerable<string> urlFiles;
        try
        {
            urlFiles = Directory.EnumerateFiles(
                shortcutsFolder,
                "*.url",
                SearchOption.AllDirectories).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var urlFile in urlFiles)
        {
            string? target = null;
            try
            {
                var urlLine = File.ReadLines(urlFile)
                    .FirstOrDefault(line =>
                        line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                target = urlLine is null ? null : urlLine[4..].Trim();
                if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    target = uri.LocalPath;
                }
            }
            catch
            {
                // Ignore malformed Explorer shortcut files.
            }

            if (IsUncPath(target ?? string.Empty))
            {
                yield return target!;
            }
        }
    }

    private static string? TryResolveWindowsShortcut(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            dynamic dynamicShell = shell;
            shortcut = dynamicShell.CreateShortcut(shortcutPath);
            dynamic dynamicShortcut = shortcut;
            return (string?)dynamicShortcut.TargetPath;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                _ = Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                _ = Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static IEnumerable<string> EnumerateExplorerMountedUncPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        string[] subKeyNames;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2");
            subKeyNames = key?.GetSubKeyNames() ?? [];
        }
        catch
        {
            yield break;
        }

        foreach (var subKeyName in subKeyNames)
        {
            if (!subKeyName.StartsWith("##", StringComparison.Ordinal) ||
                subKeyName.Length <= 2)
            {
                continue;
            }

            var encoded = subKeyName[2..];
            var components = encoded.Split(
                '#',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length >= 1)
            {
                yield return @"\\" + string.Join(Path.DirectorySeparatorChar, components);
            }
        }
    }

    private static bool TryResolveMappedDrive(
        string root,
        out string remoteName)
    {
        if (TryResolveActiveMappedDrive(root, out remoteName))
        {
            return true;
        }

        var localName = !string.IsNullOrWhiteSpace(root) && root.Length >= 2
            ? root[..2]
            : root;
        return TryResolveRememberedDrive(localName, out remoteName);
    }

    private static bool TryResolveActiveMappedDrive(
        string root,
        out string remoteName)
    {
        remoteName = string.Empty;
        if (!OperatingSystem.IsWindows() ||
            string.IsNullOrWhiteSpace(root) ||
            root.Length < 2 ||
            root[1] != Path.VolumeSeparatorChar)
        {
            return false;
        }

        var localName = root[..2];
        var capacity = 512;
        var buffer = new StringBuilder(capacity);
        var result = WNetGetConnection(localName, buffer, ref capacity);
        if (result == ErrorMoreData)
        {
            buffer = new StringBuilder(capacity);
            result = WNetGetConnection(localName, buffer, ref capacity);
        }

        if (result != NoError || buffer.Length == 0)
        {
            return false;
        }

        remoteName = buffer.ToString();
        return true;
    }

    private static bool TryResolveRememberedDrive(
        string localName,
        out string remoteName)
    {
        remoteName = string.Empty;
        if (!OperatingSystem.IsWindows() ||
            localName.Length < 1 ||
            !char.IsLetter(localName[0]))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"Network\{char.ToUpperInvariant(localName[0])}");
            remoteName = key?.GetValue("RemotePath") as string ?? string.Empty;
            return IsUncPath(remoteName);
        }
        catch
        {
            remoteName = string.Empty;
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public int Scope;
        public int Type;
        public int DisplayType;
        public int Usage;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? LocalName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? RemoteName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Provider;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UseInfo2
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Local;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Remote;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Password;

        public int Status;
        public int AssignmentType;
        public int ReferenceCount;
        public int UseCount;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DomainName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShareInfo1
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? NetName;

        public int Type;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Remark;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetAddConnection3(
        IntPtr hwndOwner,
        ref NetResource netResource,
        string? password,
        string? userName,
        int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetGetConnection(
        string localName,
        StringBuilder remoteName,
        ref int length);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetOpenEnum(
        int scope,
        int type,
        int usage,
        IntPtr netResource,
        out IntPtr enumHandle);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetEnumResource(
        IntPtr enumHandle,
        ref int count,
        IntPtr buffer,
        ref int bufferSize);

    [DllImport("mpr.dll", SetLastError = true)]
    private static extern int WNetCloseEnum(IntPtr enumHandle);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetUseEnum(
        string? uncServerName,
        int level,
        out IntPtr buffer,
        int preferredMaximumLength,
        out int entriesRead,
        out int totalEntries,
        ref int resumeHandle);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetShareEnum(
        string serverName,
        int level,
        out IntPtr buffer,
        int preferredMaximumLength,
        out int entriesRead,
        out int totalEntries,
        ref int resumeHandle);

    [DllImport("Netapi32.dll", SetLastError = true)]
    private static extern int NetApiBufferFree(IntPtr buffer);
}

public sealed record ConnectedNetworkShareInfo(
    string Path,
    string Name,
    string Source)
{
    public string ServerRoot =>
        NetworkPathService.GetUncServerRoot(Path) ?? Path;
}

public sealed record MappedNetworkDriveInfo(
    string LocalPath,
    string RemotePath,
    bool IsConnected)
{
    public string DisplayName
    {
        get
        {
            var shareName = NetworkPathService.GetUncShareRoot(RemotePath)?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();
            var driveName = LocalPath.TrimEnd(Path.DirectorySeparatorChar);
            var name = string.IsNullOrWhiteSpace(shareName)
                ? "네트워크 드라이브"
                : shareName;

            return IsConnected
                ? $"{name} ({driveName})"
                : $"{name} ({driveName}, 연결 필요)";
        }
    }
}

public sealed record KnownNetworkLocationInfo(
    string RemotePath,
    string? LocalPath,
    bool IsConnected,
    string Source)
{
    public string ServerRoot =>
        NetworkPathService.GetUncServerRoot(RemotePath) ?? RemotePath;

    public string? ShareRoot =>
        NetworkPathService.GetUncShareRoot(RemotePath);

    public string DisplayName
    {
        get
        {
            var path = ShareRoot ?? ServerRoot;
            var name = path
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? path;
            var local = string.IsNullOrWhiteSpace(LocalPath)
                ? string.Empty
                : $" ({LocalPath.TrimEnd(Path.DirectorySeparatorChar)})";
            return IsConnected
                ? name + local
                : name + local + " · 연결 필요";
        }
    }
}

public sealed record NetworkResourceInfo(
    string? LocalName,
    string RemoteName,
    int DisplayType,
    int Usage);

public sealed record ServerShareInfo(
    string Name,
    string Path,
    string Remark);

public sealed record ServerShareEnumerationResult(
    bool Success,
    string ServerRoot,
    IReadOnlyList<ServerShareInfo> Shares,
    string Message,
    Exception? Exception = null)
{
    public static ServerShareEnumerationResult Succeeded(
        string serverRoot,
        IReadOnlyList<ServerShareInfo> shares) =>
        new(
            true,
            serverRoot,
            shares,
            shares.Count == 0
                ? "표시 가능한 공유 폴더가 없습니다."
                : $"공유 폴더 {shares.Count:N0}개를 찾았습니다.");

    public static ServerShareEnumerationResult Failed(
        string serverRoot,
        string message,
        Exception? exception = null) =>
        new(false, serverRoot, [], message, exception);
}

public sealed record NetworkAccessResult(
    bool Success,
    string Path,
    string Message,
    Exception? Exception = null,
    int? NativeErrorCode = null)
{
    public static NetworkAccessResult Accessible(string path) =>
        new(true, path, "네트워크 위치에 연결되었습니다.");

    public static NetworkAccessResult Failed(
        string path,
        string message,
        Exception? exception = null,
        int? nativeErrorCode = null) =>
        new(false, path, message, exception, nativeErrorCode);
}
