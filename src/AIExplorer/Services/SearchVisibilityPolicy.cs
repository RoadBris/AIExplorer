using System.IO;

namespace AIExplorer.Services;

/// <summary>
/// Defines which file-system entries are useful to normal users and search.
/// Hidden/system entries and temporary names beginning with '~' are excluded
/// consistently from navigation, title search, lexical search and AI indexes.
/// </summary>
public static class SearchVisibilityPolicy
{
    public const FileAttributes AttributesToSkip =
        FileAttributes.Hidden | FileAttributes.System;

    public static EnumerationOptions CreateEnumerationOptions() => new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = AttributesToSkip
    };

    public static bool IsExcludedName(string? name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.StartsWith("~", StringComparison.Ordinal);

    public static bool TryGetVisibleAttributes(
        FileSystemInfo info,
        out FileAttributes attributes)
    {
        attributes = default;
        if (IsExcludedName(info.Name))
        {
            return false;
        }

        try
        {
            attributes = info.Attributes;
            return (attributes & AttributesToSkip) == 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsVisiblePathByName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .All(segment => !IsExcludedName(segment));
    }
}
