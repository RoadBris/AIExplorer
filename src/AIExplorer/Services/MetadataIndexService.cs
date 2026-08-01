using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class MetadataIndexService
{
    private const int CurrentFormatVersion = 5;
    private static readonly TimeSpan MaximumIndexAge = TimeSpan.FromMinutes(30);
    private static readonly EnumerationOptions IndexingOptions =
        SearchVisibilityPolicy.CreateEnumerationOptions();

    private readonly string _indexDirectory;
    private readonly ConcurrentDictionary<string, MetadataIndexSnapshot> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rootLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MetadataIndexService(string indexDirectory)
    {
        _indexDirectory = indexDirectory;
    }

    public async Task<MetadataIndexProbe> ProbeAsync(
        string root,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootWriteUtc = GetRootWriteTimeUtc(normalizedRoot);
        if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot))
        {
            var usable = IsUsable(
                memorySnapshot,
                normalizedRoot,
                rootWriteUtc,
                maximumItems);
            return MetadataIndexProbe.FromSnapshot(memorySnapshot, usable);
        }

        var indexPath = GetIndexPath(normalizedRoot);
        var diskSnapshot = await TryLoadAsync(indexPath, cancellationToken);
        if (diskSnapshot is null)
        {
            return MetadataIndexProbe.Missing;
        }

        var isUsable = IsUsable(
            diskSnapshot,
            normalizedRoot,
            rootWriteUtc,
            maximumItems);
        if (isUsable)
        {
            _memoryCache[normalizedRoot] = diskSnapshot;
        }

        return MetadataIndexProbe.FromSnapshot(diskSnapshot, isUsable);
    }

    public async Task<IndexAccessResult?> TryGetUsableAsync(
        string root,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootWriteUtc = GetRootWriteTimeUtc(normalizedRoot);
        if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot) &&
            IsUsable(memorySnapshot, normalizedRoot, rootWriteUtc, maximumItems))
        {
            return new IndexAccessResult(memorySnapshot, UsedCache: true);
        }

        var indexPath = GetIndexPath(normalizedRoot);
        var diskSnapshot = await TryLoadAsync(indexPath, cancellationToken);
        if (diskSnapshot is null ||
            !IsUsable(diskSnapshot, normalizedRoot, rootWriteUtc, maximumItems))
        {
            return null;
        }

        _memoryCache[normalizedRoot] = diskSnapshot;
        return new IndexAccessResult(diskSnapshot, UsedCache: true);
    }

    public async Task<IndexAccessResult?> TryGetAvailableAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot) &&
            IsAvailable(memorySnapshot, normalizedRoot))
        {
            return new IndexAccessResult(memorySnapshot, UsedCache: true);
        }

        var indexPath = GetIndexPath(normalizedRoot);
        var diskSnapshot = await TryLoadAsync(indexPath, cancellationToken);
        if (diskSnapshot is null ||
            !IsAvailable(diskSnapshot, normalizedRoot))
        {
            return null;
        }

        _memoryCache[normalizedRoot] = diskSnapshot;
        return new IndexAccessResult(diskSnapshot, UsedCache: true);
    }

    public async Task<IndexAccessResult> GetOrBuildAsync(
        string root,
        int maximumItems,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootLock = _rootLocks.GetOrAdd(normalizedRoot, _ => new SemaphoreSlim(1, 1));
        await rootLock.WaitAsync(cancellationToken);
        try
        {
            var rootWriteUtc = GetRootWriteTimeUtc(normalizedRoot);
            if (!forceRefresh &&
                _memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot) &&
                IsUsable(memorySnapshot, normalizedRoot, rootWriteUtc, maximumItems))
            {
                return new IndexAccessResult(memorySnapshot, UsedCache: true);
            }

            var indexPath = GetIndexPath(normalizedRoot);
            var diskSnapshot = await TryLoadAsync(indexPath, cancellationToken);
            if (!forceRefresh &&
                diskSnapshot is not null &&
                IsUsable(diskSnapshot, normalizedRoot, rootWriteUtc, maximumItems))
            {
                _memoryCache[normalizedRoot] = diskSnapshot;
                return new IndexAccessResult(diskSnapshot, UsedCache: true);
            }

            var builtSnapshot = await Task.Run(
                () => BuildSnapshot(
                    normalizedRoot,
                    maximumItems,
                    progress,
                    cancellationToken),
                cancellationToken);
            _memoryCache[normalizedRoot] = builtSnapshot;
            await TrySaveAsync(indexPath, builtSnapshot, cancellationToken);
            return new IndexAccessResult(builtSnapshot, UsedCache: false);
        }
        finally
        {
            rootLock.Release();
        }
    }

    private MetadataIndexSnapshot BuildSnapshot(
        string root,
        int maximumItems,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var items = new List<IndexedFileRecord>(Math.Min(maximumItems, 16_384));
        var pendingDirectories = new Queue<string>(
            SearchPathPriority.GetTraversalRoots(root));
        var visitedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var isComplete = true;
        var lastReportedItems = -1;
        var lastReportTick = Environment.TickCount64;
        var highestEstimatedPercent = 0d;

        void ReportIndexProgress(
            string currentPath,
            bool currentDirectoryComplete = false)
        {
            if (progress is null)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (items.Count - lastReportedItems < 250 &&
                now - lastReportTick < 180)
            {
                return;
            }

            var remainingDirectoryEstimate = pendingDirectories.Count +
                                             (currentDirectoryComplete ? 0 : 1);
            var estimatedTotalDirectories = visitedDirectories.Count +
                                            remainingDirectoryEstimate;
            var estimatedPercent = estimatedTotalDirectories <= 0
                ? 0d
                : Math.Min(
                    99d,
                    (double)visitedDirectories.Count /
                    estimatedTotalDirectories * 100d);
            highestEstimatedPercent = Math.Max(
                highestEstimatedPercent,
                estimatedPercent);
            lastReportedItems = items.Count;
            lastReportTick = now;
            progress.Report(new SearchProgress(
                items.Count,
                0,
                currentPath,
                SearchPhase.Indexing,
                PercentComplete: highestEstimatedPercent));
        }

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pendingDirectories.Dequeue();
            if (!visitedDirectories.Add(currentDirectory))
            {
                continue;
            }

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(currentDirectory)
                    .EnumerateFileSystemInfos("*", IndexingOptions);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            try
            {
                foreach (var info in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (items.Count >= maximumItems)
                    {
                        isComplete = false;
                        break;
                    }

                    try
                    {
                        if (!SearchVisibilityPolicy.TryGetVisibleAttributes(
                                info,
                                out var attributes))
                        {
                            continue;
                        }

                        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                        if (isDirectory && !attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            pendingDirectories.Enqueue(info.FullName);
                        }

                        items.Add(new IndexedFileRecord
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            DirectoryPath = Path.GetDirectoryName(info.FullName) ?? root,
                            Extension = isDirectory
                                ? string.Empty
                                : info.Extension.ToLowerInvariant(),
                            IsDirectory = isDirectory,
                            SizeBytes = info is FileInfo file ? file.Length : null,
                            CreatedUtc = info.CreationTimeUtc,
                            ModifiedUtc = info.LastWriteTimeUtc
                        });
                    }
                    catch (IOException)
                    {
                        // Files can disappear or disconnect while the index is built.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Protected items are omitted without aborting the index.
                    }

                    ReportIndexProgress(currentDirectory);
                }
            }
            catch (IOException)
            {
                // A network folder can disconnect during enumeration.
            }
            catch (UnauthorizedAccessException)
            {
                // An inaccessible child folder is skipped.
            }

            ReportIndexProgress(
                currentDirectory,
                currentDirectoryComplete: true);

            if (!isComplete)
            {
                break;
            }
        }

        progress?.Report(new SearchProgress(
            items.Count,
            0,
            string.Empty,
            SearchPhase.Indexing,
            PercentComplete: 100d));

        return new MetadataIndexSnapshot
        {
            FormatVersion = CurrentFormatVersion,
            Root = root,
            BuiltUtc = DateTime.UtcNow,
            RootWriteUtc = GetRootWriteTimeUtc(root),
            MaximumItems = maximumItems,
            IsComplete = isComplete,
            Items = items
        };
    }

    private static bool IsAvailable(
        MetadataIndexSnapshot snapshot,
        string root) =>
        snapshot.FormatVersion == CurrentFormatVersion &&
        string.Equals(
            snapshot.Root,
            root,
            StringComparison.OrdinalIgnoreCase);

    private bool IsUsable(
        MetadataIndexSnapshot snapshot,
        string root,
        DateTime rootWriteUtc,
        int maximumItems)
    {
        if (snapshot.FormatVersion != CurrentFormatVersion ||
            !string.Equals(snapshot.Root, root, StringComparison.OrdinalIgnoreCase) ||
            DateTime.UtcNow - snapshot.BuiltUtc > MaximumIndexAge ||
            snapshot.RootWriteUtc != rootWriteUtc)
        {
            return false;
        }

        return snapshot.IsComplete || snapshot.MaximumItems >= maximumItems;
    }

    private async Task<MetadataIndexSnapshot?> TryLoadAsync(
        string indexPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(indexPath))
            {
                return null;
            }

            await using var stream = new FileStream(
                indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65_536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<MetadataIndexSnapshot>(
                stream,
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private async Task TrySaveAsync(
        string indexPath,
        MetadataIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var temporaryPath = indexPath + ".tmp";
        try
        {
            Directory.CreateDirectory(_indexDirectory);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             65_536,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    _jsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, indexPath, true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private string GetIndexPath(string root)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(root.ToUpperInvariant()));
        var key = Convert.ToHexString(hash)[..24];
        return Path.Combine(_indexDirectory, $"metadata-{key}.json");
    }

    private static DateTime GetRootWriteTimeUtc(string root)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(root);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A stale temporary file can be replaced on the next successful save.
        }
    }
}

public sealed record IndexAccessResult(
    MetadataIndexSnapshot Snapshot,
    bool UsedCache);

public sealed class MetadataIndexSnapshot
{
    public int FormatVersion { get; set; }

    public required string Root { get; set; }

    public DateTime BuiltUtc { get; set; }

    public DateTime RootWriteUtc { get; set; }

    public int MaximumItems { get; set; }

    public bool IsComplete { get; set; }

    public List<IndexedFileRecord> Items { get; set; } = [];
}

public sealed class IndexedFileRecord
{
    public required string Name { get; set; }

    public required string FullPath { get; set; }

    public required string DirectoryPath { get; set; }

    public required string Extension { get; set; }

    public bool IsDirectory { get; set; }

    public long? SizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}


public sealed record MetadataIndexProbe(
    bool Exists,
    bool IsUsable,
    bool IsComplete,
    int IndexedItems,
    IReadOnlyList<IndexedFileRecord> Items)
{
    public static MetadataIndexProbe Missing { get; } =
        new(false, false, false, 0, []);

    public static MetadataIndexProbe FromSnapshot(
        MetadataIndexSnapshot snapshot,
        bool isUsable) =>
        new(
            true,
            isUsable,
            snapshot.IsComplete,
            snapshot.Items.Count,
            snapshot.Items);
}
