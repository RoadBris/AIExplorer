using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class FileSystemService
{
    private readonly ShellIconService _shellIconService;

    private static readonly EnumerationOptions ListingOptions =
        SearchVisibilityPolicy.CreateEnumerationOptions();

    public FileSystemService()
        : this(new ShellIconService())
    {
    }

    public FileSystemService(ShellIconService shellIconService)
    {
        _shellIconService = shellIconService;
    }

    public Task<IReadOnlyList<FileSystemEntry>> GetEntriesAsync(
        string path,
        FileSortMode sortMode,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<FileSystemEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = new DirectoryInfo(path);
            var results = new List<FileSystemEntry>();

            foreach (var info in directory.EnumerateFileSystemInfos("*", ListingOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = CreateEntry(info);
                if (entry is not null)
                {
                    results.Add(entry);
                }
            }

            return Sort(results, sortMode);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NavigationNode>> GetChildDirectoriesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<NavigationNode>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = new List<NavigationNode>();

            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories("*", ListingOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    results.Add(new NavigationNode(
                        directory.Name,
                        directory.FullName,
                        "\uE8B7",
                        NavigationNodeKind.Folder,
                        canExpand: true,
                        iconImage: _shellIconService.GetFileSystemIcon(
                            directory.FullName,
                            isDirectory: true)));
                }
                catch (IOException)
                {
                    // A disappearing network folder is normal during enumeration.
                }
                catch (UnauthorizedAccessException)
                {
                    // Inaccessible folders are omitted from the navigation tree.
                }
            }

            return results
                .OrderBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }, cancellationToken);
    }

    public FileSystemEntry? CreateEntry(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            return CreateEntry(info);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? GetParentPath(string path)
    {
        try
        {
            return Directory.GetParent(path)?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private FileSystemEntry? CreateEntry(FileSystemInfo info)
    {
        try
        {
            if (!SearchVisibilityPolicy.TryGetVisibleAttributes(
                    info,
                    out _))
            {
                return null;
            }

            var isDirectory = info is DirectoryInfo;
            long? size = info is FileInfo file ? file.Length : null;
            return new FileSystemEntry
            {
                Name = info.Name,
                FullPath = info.FullName,
                IsDirectory = isDirectory,
                SizeBytes = size,
                SizeDisplay = isDirectory ? string.Empty : FormatSize(size ?? 0),
                ModifiedAt = info.LastWriteTime,
                ModifiedDisplay = info.LastWriteTime.ToString("yyyy-MM-dd  HH:mm"),
                TypeDisplay = isDirectory
                    ? "파일 폴더"
                    : FileTypeCatalog.GetTypeDisplay(info.Extension),
                IconGlyph = GetIconGlyph(isDirectory, info.Extension),
                IconImage = _shellIconService.GetFileSystemIcon(
                    info.FullName,
                    isDirectory)
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<FileSystemEntry> Sort(
        IEnumerable<FileSystemEntry> entries,
        FileSortMode mode)
    {
        var directories = entries.Where(entry => entry.IsDirectory);
        var files = entries.Where(entry => !entry.IsDirectory);

        IOrderedEnumerable<FileSystemEntry> SortGroup(IEnumerable<FileSystemEntry> group) =>
            mode switch
            {
                FileSortMode.Modified => group
                    .OrderByDescending(entry => entry.ModifiedAt)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                FileSortMode.Type => group
                    .OrderBy(entry => entry.TypeDisplay, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                FileSortMode.Size => group
                    .OrderByDescending(entry => entry.SizeBytes ?? 0)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => group.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            };

        return SortGroup(directories)
            .Concat(SortGroup(files))
            .ToArray();
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.#} {units[unitIndex]}";
    }

    private static string GetIconGlyph(bool isDirectory, string extension)
    {
        if (isDirectory)
        {
            return "\uE8B7";
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "\uEA90",
            ".docx" or ".hwp" or ".hwpx" or ".txt" or ".md" => "\uE8A5",
            ".xlsx" or ".csv" => "\uEA42",
            ".pptx" => "\uE8A5",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "\uEB9F",
            ".mp4" or ".mkv" or ".avi" or ".mov" => "\uE714",
            ".mp3" or ".wav" or ".flac" or ".m4a" => "\uE8D6",
            ".zip" or ".7z" or ".rar" => "\uF012",
            ".exe" => "\uE7C5",
            _ => "\uE8A5"
        };
    }
}
