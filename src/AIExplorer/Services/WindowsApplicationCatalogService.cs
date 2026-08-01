using Microsoft.Win32;

namespace AIExplorer.Services;

public sealed record WindowsApplicationCatalogEntry(
    string Name,
    string FullPath,
    string SourceLabel,
    bool IsDirectory,
    DateTime ModifiedUtc);

public sealed record WindowsApplicationCatalogMatch(
    WindowsApplicationCatalogEntry Entry,
    double Score,
    double MatchPercent,
    bool IsExactName,
    string Reason);

public sealed class WindowsApplicationCatalogService
{
    private static readonly TimeSpan CacheLifetime =
        TimeSpan.FromMinutes(2);
    private static readonly HashSet<string> ShortcutExtensions = new(
        [".lnk", ".url", ".exe", ".appref-ms"],
        StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly IReadOnlyList<WindowsApplicationCatalogEntry>?
        _fixedEntries;
    private IReadOnlyList<WindowsApplicationCatalogEntry>? _cachedEntries;
    private DateTime _cacheBuiltUtc;

    public WindowsApplicationCatalogService()
    {
    }

    public WindowsApplicationCatalogService(
        IReadOnlyList<WindowsApplicationCatalogEntry> fixedEntries)
    {
        _fixedEntries = fixedEntries;
        _cachedEntries = fixedEntries;
        _cacheBuiltUtc = DateTime.MaxValue;
    }

    public async Task<IReadOnlyList<WindowsApplicationCatalogMatch>>
        SearchAsync(
            SearchIntent intent,
            int maximumResults,
            CancellationToken cancellationToken)
    {
        if (!intent.Classification.SearchApplicationCatalog ||
            intent.MetadataTerms.Count == 0)
        {
            return [];
        }

        var entries = await GetEntriesAsync(cancellationToken)
            .ConfigureAwait(false);
        return await Task.Run(
                () => SearchEntries(
                    entries,
                    intent,
                    maximumResults,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public void Invalidate()
    {
        if (_fixedEntries is not null)
        {
            return;
        }

        _cachedEntries = null;
        _cacheBuiltUtc = default;
    }

    private async Task<IReadOnlyList<WindowsApplicationCatalogEntry>>
        GetEntriesAsync(CancellationToken cancellationToken)
    {
        if (_cachedEntries is not null &&
            DateTime.UtcNow - _cacheBuiltUtc < CacheLifetime)
        {
            return _cachedEntries;
        }

        await _loadLock.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (_cachedEntries is not null &&
                DateTime.UtcNow - _cacheBuiltUtc < CacheLifetime)
            {
                return _cachedEntries;
            }

            _cachedEntries = await Task.Run(
                    () => BuildCatalog(cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            _cacheBuiltUtc = DateTime.UtcNow;
            return _cachedEntries;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static IReadOnlyList<WindowsApplicationCatalogMatch>
        SearchEntries(
            IReadOnlyList<WindowsApplicationCatalogEntry> entries,
            SearchIntent intent,
            int maximumResults,
            CancellationToken cancellationToken)
    {
        var terms = intent.MetadataTerms.ToArray();
        var matches = new List<WindowsApplicationCatalogMatch>();
        for (var index = 0; index < entries.Count; index++)
        {
            if ((index & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = entries[index];
            var normalizedName = Normalize(entry.Name);
            var compactName = normalizedName.Replace(" ", string.Empty);
            var matchedTerms = 0;
            var originalMatches = 0;
            foreach (var term in terms)
            {
                var original = Normalize(term.Original);
                if (ContainsTerm(normalizedName, compactName, original))
                {
                    matchedTerms++;
                    originalMatches++;
                    continue;
                }

                if (term.Alternatives.Any(alternative =>
                        ContainsTerm(
                            normalizedName,
                            compactName,
                            Normalize(alternative))))
                {
                    matchedTerms++;
                }
            }

            var requiredMatches = terms.Length <= 2
                ? terms.Length
                : Math.Max(2, (int)Math.Ceiling(terms.Length * 0.6d));
            if (matchedTerms < requiredMatches)
            {
                continue;
            }

            var originalPhrase = Normalize(string.Join(
                " ",
                terms.Select(term => term.Original)));
            var exactPhrase = originalPhrase.Length > 0 &&
                              normalizedName.Contains(
                                  originalPhrase,
                                  StringComparison.OrdinalIgnoreCase);
            var exactName = exactPhrase &&
                            string.Equals(
                                normalizedName,
                                originalPhrase,
                                StringComparison.OrdinalIgnoreCase);
            var sourceBonus = entry.SourceLabel switch
            {
                "공용 바탕 화면" or "사용자 바탕 화면" => 70d,
                "공용 시작 메뉴" or "사용자 시작 메뉴" => 55d,
                _ => 35d
            };
            var score = 780d +
                        matchedTerms * 85d +
                        originalMatches * 55d +
                        (exactPhrase ? 110d : 0d) +
                        (exactName ? 80d : 0d) +
                        sourceBonus;
            var reason =
                $"{entry.SourceLabel}에서 앱·바로가기 이름이 " +
                $"검색어와 직접 일치합니다.";
            matches.Add(new WindowsApplicationCatalogMatch(
                entry,
                score,
                exactName ? 100d : exactPhrase ? 98d : 92d,
                exactName,
                reason));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(
                match => match.Entry.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Max(1, maximumResults))
            .ToArray();
    }

    private static IReadOnlyList<WindowsApplicationCatalogEntry>
        BuildCatalog(CancellationToken cancellationToken)
    {
        var entries = new Dictionary<
            string,
            WindowsApplicationCatalogEntry>(
            StringComparer.OrdinalIgnoreCase);

        AddShellRoot(
            Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory),
            "사용자 바탕 화면",
            recursive: false,
            entries,
            cancellationToken);
        AddShellRoot(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonDesktopDirectory),
            "공용 바탕 화면",
            recursive: false,
            entries,
            cancellationToken);
        AddShellRoot(
            Environment.GetFolderPath(
                Environment.SpecialFolder.StartMenu),
            "사용자 시작 메뉴",
            recursive: true,
            entries,
            cancellationToken);
        AddShellRoot(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartMenu),
            "공용 시작 메뉴",
            recursive: true,
            entries,
            cancellationToken);

        if (OperatingSystem.IsWindows())
        {
            AddRegistryApplications(entries, cancellationToken);
        }

        return entries.Values.ToArray();
    }

    private static void AddShellRoot(
        string root,
        string sourceLabel,
        bool recursive,
        IDictionary<string, WindowsApplicationCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(root) ||
            !Directory.Exists(root))
        {
            return;
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip =
                    FileAttributes.Hidden |
                    FileAttributes.System |
                    FileAttributes.ReparsePoint,
                MaxRecursionDepth = recursive ? 12 : 0
            };
            var inspected = 0;
            foreach (var info in new DirectoryInfo(root)
                         .EnumerateFileSystemInfos("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++inspected > 50_000)
                {
                    break;
                }

                var isDirectory =
                    info.Attributes.HasFlag(FileAttributes.Directory);
                if (!isDirectory &&
                    !ShortcutExtensions.Contains(info.Extension))
                {
                    continue;
                }

                var displayName = isDirectory
                    ? info.Name
                    : Path.GetFileNameWithoutExtension(info.Name);
                if (string.IsNullOrWhiteSpace(displayName) ||
                    SearchVisibilityPolicy.IsExcludedName(displayName))
                {
                    continue;
                }

                entries.TryAdd(
                    info.FullName,
                    new WindowsApplicationCatalogEntry(
                        displayName,
                        info.FullName,
                        sourceLabel,
                        isDirectory,
                        SafeGetLastWriteTimeUtc(info)));
            }
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException)
        {
            // Other application sources remain searchable.
        }
    }

    private static void AddRegistryApplications(
        IDictionary<string, WindowsApplicationCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var hive in new[]
                 {
                     RegistryHive.CurrentUser,
                     RegistryHive.LocalMachine
                 })
        {
            foreach (var view in new[]
                     {
                         RegistryView.Registry64,
                         RegistryView.Registry32
                     })
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(
                        hive,
                        view);
                    AddUninstallEntries(
                        baseKey,
                        entries,
                        cancellationToken);
                    AddAppPathEntries(
                        baseKey,
                        entries,
                        cancellationToken);
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException or
                        IOException)
                {
                    // Registry view is optional.
                }
            }
        }
    }

    private static void AddUninstallEntries(
        RegistryKey baseKey,
        IDictionary<string, WindowsApplicationCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        using var uninstall = baseKey.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstall is null)
        {
            return;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var item = uninstall.OpenSubKey(subKeyName);
            var displayName = item?.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            var path = ResolveRegistryApplicationPath(item);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            AddRegistryEntry(displayName, path, entries);
        }
    }

    private static void AddAppPathEntries(
        RegistryKey baseKey,
        IDictionary<string, WindowsApplicationCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        using var appPaths = baseKey.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\App Paths");
        if (appPaths is null)
        {
            return;
        }

        foreach (var subKeyName in appPaths.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var item = appPaths.OpenSubKey(subKeyName);
            var path = NormalizeRegistryPath(
                item?.GetValue(null) as string);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            AddRegistryEntry(
                Path.GetFileNameWithoutExtension(subKeyName),
                path,
                entries);
        }
    }

    private static string ResolveRegistryApplicationPath(
        RegistryKey? item)
    {
        var displayIcon = NormalizeRegistryPath(
            item?.GetValue("DisplayIcon") as string);
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            return displayIcon;
        }

        var installLocation = NormalizeRegistryPath(
            item?.GetValue("InstallLocation") as string);
        return installLocation;
    }

    private static string NormalizeRegistryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(
            value.Trim());
        if (expanded.StartsWith('"'))
        {
            var closingQuote = expanded.IndexOf('"', 1);
            expanded = closingQuote > 1
                ? expanded[1..closingQuote]
                : expanded.Trim('"');
        }
        else
        {
            var comma = expanded.LastIndexOf(',');
            if (comma > 2 &&
                int.TryParse(expanded[(comma + 1)..], out _))
            {
                expanded = expanded[..comma];
            }
        }

        expanded = expanded.Trim().Trim('"');
        return File.Exists(expanded) || Directory.Exists(expanded)
            ? expanded
            : string.Empty;
    }

    private static void AddRegistryEntry(
        string displayName,
        string path,
        IDictionary<string, WindowsApplicationCatalogEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var isDirectory = Directory.Exists(path);
        entries.TryAdd(
            path,
            new WindowsApplicationCatalogEntry(
                displayName.Trim(),
                path,
                "설치된 프로그램",
                isDirectory,
                SafeGetLastWriteTimeUtc(path, isDirectory)));
    }

    private static DateTime SafeGetLastWriteTimeUtc(
        FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTimeUtc;
        }
        catch
        {
            return default;
        }
    }

    private static DateTime SafeGetLastWriteTimeUtc(
        string path,
        bool isDirectory)
    {
        try
        {
            return isDirectory
                ? Directory.GetLastWriteTimeUtc(path)
                : File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return default;
        }
    }

    private static string Normalize(string value)
    {
        var characters = value
            .Trim()
            .ToLowerInvariant()
            .Select(character =>
                char.IsLetterOrDigit(character)
                    ? character
                    : ' ')
            .ToArray();
        return string.Join(
            ' ',
            new string(characters)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
    }

    private static bool ContainsTerm(
        string normalizedName,
        string compactName,
        string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        return normalizedName.Contains(
                   term,
                   StringComparison.OrdinalIgnoreCase) ||
               compactName.Contains(
                   term.Replace(" ", string.Empty),
                   StringComparison.OrdinalIgnoreCase);
    }
}
