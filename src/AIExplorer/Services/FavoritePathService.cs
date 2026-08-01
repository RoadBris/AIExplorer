using System.Runtime.InteropServices;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed record FavoriteDropTarget(string Name, string Path);

public static class FavoritePathService
{
    public static bool IsSupportedDropSource(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        if (Directory.Exists(sourcePath))
        {
            return true;
        }

        if (!File.Exists(sourcePath))
        {
            return false;
        }

        var extension = Path.GetExtension(sourcePath);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryResolve(
        string sourcePath,
        out FavoriteDropTarget? target,
        out string error)
    {
        target = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            error = "빈 경로는 즐겨찾기에 추가할 수 없습니다.";
            return false;
        }

        var displayName = string.Empty;
        string? resolvedPath;
        if (Directory.Exists(sourcePath))
        {
            resolvedPath = sourcePath;
            displayName = GetFolderDisplayName(sourcePath);
        }
        else if (File.Exists(sourcePath) &&
                 Path.GetExtension(sourcePath).Equals(
                     ".lnk",
                     StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = TryResolveWindowsShortcut(sourcePath);
            displayName = Path.GetFileNameWithoutExtension(sourcePath);
        }
        else if (File.Exists(sourcePath) &&
                 Path.GetExtension(sourcePath).Equals(
                     ".url",
                     StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = TryResolveInternetShortcut(sourcePath);
            displayName = Path.GetFileNameWithoutExtension(sourcePath);
        }
        else
        {
            error = "폴더 또는 폴더를 가리키는 Windows 바로가기만 추가할 수 있습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            error = "바로가기의 대상 폴더를 확인하지 못했습니다.";
            return false;
        }

        string normalized;
        try
        {
            normalized = NormalizeTargetPath(resolvedPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            error = $"바로가기 대상 경로가 올바르지 않습니다: {exception.Message}";
            return false;
        }

        if (!Directory.Exists(normalized) &&
            !NetworkPathService.IsPotentialNetworkPath(normalized))
        {
            error = "바로가기 대상이 폴더 또는 네트워크 공유가 아닙니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = GetFolderDisplayName(normalized);
        }

        target = new FavoriteDropTarget(displayName, normalized);
        return true;
    }


    public static bool TryCreateFolderTarget(
        string folderPath,
        string? preferredName,
        out FavoriteDropTarget? target,
        out string error)
    {
        target = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            error = "빈 경로는 즐겨찾기에 추가할 수 없습니다.";
            return false;
        }

        string normalized;
        try
        {
            normalized = NormalizeTargetPath(folderPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            error = $"즐겨찾기 경로가 올바르지 않습니다: {exception.Message}";
            return false;
        }

        if (!Directory.Exists(normalized) &&
            !NetworkPathService.IsPotentialNetworkPath(normalized))
        {
            error = "폴더 또는 네트워크 공유만 즐겨찾기에 추가할 수 있습니다.";
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(preferredName)
            ? GetFolderDisplayName(normalized)
            : preferredName.Trim();
        target = new FavoriteDropTarget(displayName, normalized);
        return true;
    }

    public static bool MoveFavorite(
        IList<FavoriteLocation> favorites,
        string sourcePath,
        string? targetPath,
        bool insertAfter)
    {
        if (favorites.Count < 2 || string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        var sourceIdentity = GetIdentity(sourcePath);
        var sourceIndex = -1;
        for (var index = 0; index < favorites.Count; index++)
        {
            if (string.Equals(
                    GetIdentity(favorites[index].Path),
                    sourceIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                sourceIndex = index;
                break;
            }
        }

        if (sourceIndex < 0)
        {
            return false;
        }

        var source = favorites[sourceIndex];
        favorites.RemoveAt(sourceIndex);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            favorites.Add(source);
            return sourceIndex != favorites.Count - 1;
        }

        var targetIdentity = GetIdentity(targetPath);
        var targetIndex = -1;
        for (var index = 0; index < favorites.Count; index++)
        {
            if (string.Equals(
                    GetIdentity(favorites[index].Path),
                    targetIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
        {
            favorites.Insert(Math.Min(sourceIndex, favorites.Count), source);
            return false;
        }

        var insertIndex = targetIndex + (insertAfter ? 1 : 0);
        insertIndex = Math.Clamp(insertIndex, 0, favorites.Count);
        favorites.Insert(insertIndex, source);
        return insertIndex != sourceIndex;
    }

    public static string GetIdentity(string path)
    {
        try
        {
            var normalized = NormalizeTargetPath(path);
            var root = Path.GetPathRoot(normalized);
            return string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string NormalizeTargetPath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        if (NetworkPathService.IsPotentialNetworkPath(trimmed))
        {
            return NetworkPathService.NormalizeNetworkLocationPath(trimmed);
        }

        var fullPath = Path.GetFullPath(trimmed);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static string GetFolderDisplayName(string path)
    {
        var trimmed = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return trimmed.TrimStart(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static string? TryResolveInternetShortcut(string shortcutPath)
    {
        try
        {
            var urlLine = File.ReadLines(shortcutPath)
                .FirstOrDefault(line =>
                    line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
            var value = urlLine?[4..].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            return value;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveWindowsShortcut(string shortcutPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

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
}
