using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class ContentIndexService
{
    private const int CurrentFormatVersion = 7;
    private static readonly TimeSpan MaximumIndexAge = TimeSpan.FromMinutes(30);
    private static readonly EnumerationOptions IndexingOptions =
        SearchVisibilityPolicy.CreateEnumerationOptions();

    private readonly string _indexDirectory;
    private readonly DocumentTextExtractor _textExtractor;
    private readonly ConcurrentDictionary<string, ContentIndexSnapshot> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rootLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ContentIndexService(
        string indexDirectory,
        DocumentTextExtractor? textExtractor = null)
    {
        _indexDirectory = indexDirectory;
        _textExtractor = textExtractor ?? new DocumentTextExtractor();
    }

    public async Task<ContentIndexProbe> ProbeAsync(
        string root,
        int maximumDocuments,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootWriteUtc = GetRootWriteTimeUtc(normalizedRoot);
        if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot))
        {
            return ContentIndexProbe.FromSnapshot(
                memorySnapshot,
                IsUsable(
                    memorySnapshot,
                    normalizedRoot,
                    rootWriteUtc,
                    maximumDocuments));
        }

        var snapshot = await TryLoadAsync(
            GetIndexPath(normalizedRoot),
            cancellationToken);
        if (snapshot is null)
        {
            return ContentIndexProbe.Missing;
        }

        var isUsable = IsUsable(
            snapshot,
            normalizedRoot,
            rootWriteUtc,
            maximumDocuments);
        if (isUsable)
        {
            _memoryCache[normalizedRoot] = snapshot;
        }

        return ContentIndexProbe.FromSnapshot(snapshot, isUsable);
    }

    public async Task<ContentIndexAccessResult?> TryGetUsableAsync(
        string root,
        int maximumDocuments,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeAsync(
            root,
            maximumDocuments,
            cancellationToken);
        if (!probe.IsUsable || probe.Snapshot is null)
        {
            return null;
        }

        return new ContentIndexAccessResult(
            probe.Snapshot,
            UsedCache: true);
    }

    public async Task<ContentIndexAccessResult?> TryGetAvailableAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot) &&
            IsAvailable(memorySnapshot, normalizedRoot))
        {
            return new ContentIndexAccessResult(
                memorySnapshot,
                UsedCache: true);
        }

        var snapshot = await TryLoadAsync(
            GetIndexPath(normalizedRoot),
            cancellationToken);
        if (snapshot is null ||
            !IsAvailable(snapshot, normalizedRoot))
        {
            return null;
        }

        _memoryCache[normalizedRoot] = snapshot;
        return new ContentIndexAccessResult(
            snapshot,
            UsedCache: true);
    }

    public async Task<ContentIndexAccessResult> GetOrBuildAsync(
        string root,
        int maximumDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken,
        bool forceRefresh = false,
        IReadOnlyList<IndexedFileRecord>? preferredFiles = null)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootLock = _rootLocks.GetOrAdd(
            normalizedRoot,
            _ => new SemaphoreSlim(1, 1));
        await rootLock.WaitAsync(cancellationToken);
        try
        {
            var rootWriteUtc = GetRootWriteTimeUtc(normalizedRoot);
            ContentIndexSnapshot? reusableSnapshot = null;
            if (_memoryCache.TryGetValue(normalizedRoot, out var memorySnapshot))
            {
                if (!forceRefresh &&
                    IsUsable(
                        memorySnapshot,
                        normalizedRoot,
                        rootWriteUtc,
                        maximumDocuments) &&
                    HasPreferredFiles(
                        memorySnapshot,
                        preferredFiles))
                {
                    return new ContentIndexAccessResult(
                        memorySnapshot,
                        UsedCache: true);
                }

                if (IsCompatibleSeed(memorySnapshot, normalizedRoot))
                {
                    reusableSnapshot = memorySnapshot;
                }
            }

            var indexPath = GetIndexPath(normalizedRoot);
            var diskSnapshot = await TryLoadAsync(indexPath, cancellationToken);
            if (diskSnapshot is not null)
            {
                if (!forceRefresh &&
                    IsUsable(
                        diskSnapshot,
                        normalizedRoot,
                        rootWriteUtc,
                        maximumDocuments) &&
                    HasPreferredFiles(
                        diskSnapshot,
                        preferredFiles))
                {
                    _memoryCache[normalizedRoot] = diskSnapshot;
                    return new ContentIndexAccessResult(
                        diskSnapshot,
                        UsedCache: true);
                }

                if (IsCompatibleSeed(diskSnapshot, normalizedRoot))
                {
                    reusableSnapshot = diskSnapshot;
                }
            }

            var builtSnapshot = await BuildSnapshotAsync(
                normalizedRoot,
                Math.Max(1, maximumDocuments),
                reusableSnapshot,
                preferredFiles,
                progress,
                cancellationToken);
            _memoryCache[normalizedRoot] = builtSnapshot;
            await TrySaveAsync(indexPath, builtSnapshot, cancellationToken);
            return new ContentIndexAccessResult(
                builtSnapshot,
                UsedCache: false);
        }
        finally
        {
            rootLock.Release();
        }
    }

    private async Task<ContentIndexSnapshot> BuildSnapshotAsync(
        string root,
        int maximumDocuments,
        ContentIndexSnapshot? reusableSnapshot,
        IReadOnlyList<IndexedFileRecord>? preferredFiles,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var documents = new List<ContentDocumentRecord>(
            Math.Min(maximumDocuments, 2_048));
        var pendingDirectories = new Queue<string>(
            SearchPathPriority.GetTraversalRoots(root));
        var visitedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var scannedItems = 0;
        var maximumOcrAttempts = Math.Clamp(
            maximumDocuments / 8,
            96,
            512);
        var ocrAttempts = 0;
        var deferredOcrDocuments = 0;
        var isComplete = true;
        var hitDocumentLimit = false;
        var reusableByPath = reusableSnapshot?.Documents
            .GroupBy(
                document => document.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ContentDocumentRecord>(
                StringComparer.OrdinalIgnoreCase);
        var reusableOcrAttempts = reusableSnapshot?.OcrAttempts
            .GroupBy(
                attempt => attempt.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ContentOcrAttemptRecord>(
                StringComparer.OrdinalIgnoreCase);
        var currentOcrAttempts =
            new Dictionary<string, ContentOcrAttemptRecord>(
                StringComparer.OrdinalIgnoreCase);
        var indexedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var preferredLimit = Math.Min(
            maximumDocuments,
            Math.Max(64, maximumDocuments / 2));
        foreach (var preferred in (preferredFiles ?? [])
                     .Where(item =>
                         !item.IsDirectory &&
                         IsSpreadsheet(item.Extension) &&
                         SearchPathPriority.IsInsideRoot(item.FullPath, root))
                     .DistinctBy(
                         item => item.FullPath,
                         StringComparer.OrdinalIgnoreCase)
                     .Take(preferredLimit))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(preferred.FullPath);
                if (!info.Exists ||
                    !SearchVisibilityPolicy.TryGetVisibleAttributes(
                        info,
                        out _) ||
                    !_textExtractor.CanExtract(info.Extension))
                {
                    continue;
                }

                var sizeBytes = info.Length;
                if (reusableByPath.TryGetValue(
                        info.FullName,
                        out var reusable) &&
                    reusable.ModifiedUtc == info.LastWriteTimeUtc &&
                    reusable.SizeBytes == sizeBytes)
                {
                    documents.Add(reusable);
                    indexedPaths.Add(info.FullName);
                    continue;
                }

                var extracted = await _textExtractor.ExtractAsync(
                    info.FullName,
                    cancellationToken);
                if (extracted is null)
                {
                    continue;
                }

                documents.Add(CreateDocumentRecord(
                    info,
                    root,
                    extracted));
                indexedPaths.Add(info.FullName);
                progress?.Report(new SearchProgress(
                    scannedItems,
                    documents.Count,
                    info.FullName,
                    SearchPhase.ContentIndexing));
            }
            catch (IOException)
            {
                // A workbook can be replaced while it is being indexed.
            }
            catch (UnauthorizedAccessException)
            {
                // Protected workbooks are omitted.
            }
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
                    if (scannedItems < int.MaxValue)
                    {
                        scannedItems++;
                    }

                    try
                    {
                        if (!SearchVisibilityPolicy.TryGetVisibleAttributes(
                                info,
                                out var attributes))
                        {
                            continue;
                        }

                        if (attributes.HasFlag(FileAttributes.Directory))
                        {
                            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                pendingDirectories.Enqueue(info.FullName);
                            }

                            continue;
                        }

                        if (!_textExtractor.CanExtract(info.Extension))
                        {
                            continue;
                        }
                        if (indexedPaths.Contains(info.FullName))
                        {
                            continue;
                        }

                        if (documents.Count >= maximumDocuments)
                        {
                            isComplete = false;
                            hitDocumentLimit = true;
                            break;
                        }

                        var sizeBytes =
                            info is FileInfo existingFile
                                ? existingFile.Length
                                : (long?)null;
                        if (reusableByPath.TryGetValue(
                                info.FullName,
                                out var reusable) &&
                            reusable.ModifiedUtc ==
                            info.LastWriteTimeUtc &&
                            reusable.SizeBytes == sizeBytes)
                        {
                            documents.Add(reusable);
                            indexedPaths.Add(info.FullName);
                            continue;
                        }

                        var usesOcr = _textExtractor.UsesOcr(info.Extension);
                        if (usesOcr &&
                            reusableOcrAttempts.TryGetValue(
                                info.FullName,
                                out var reusableAttempt) &&
                            reusableAttempt.ModifiedUtc ==
                            info.LastWriteTimeUtc &&
                            reusableAttempt.SizeBytes == sizeBytes)
                        {
                            currentOcrAttempts[info.FullName] =
                                reusableAttempt;
                            continue;
                        }

                        if (usesOcr &&
                            ocrAttempts >= maximumOcrAttempts)
                        {
                            deferredOcrDocuments++;
                            isComplete = false;
                            continue;
                        }

                        if (usesOcr)
                        {
                            ocrAttempts++;
                        }

                        var extracted = await _textExtractor.ExtractAsync(
                            info.FullName,
                            cancellationToken);
                        if (usesOcr)
                        {
                            currentOcrAttempts[info.FullName] =
                                new ContentOcrAttemptRecord
                                {
                                    FullPath = info.FullName,
                                    SizeBytes = sizeBytes,
                                    ModifiedUtc = info.LastWriteTimeUtc
                                };
                        }

                        if (extracted is null)
                        {
                            continue;
                        }

                        documents.Add(CreateDocumentRecord(
                            info,
                            root,
                            extracted));
                        indexedPaths.Add(info.FullName);
                        if (extracted.Source is
                            DocumentContentSource.ImageOcr or
                            DocumentContentSource.PdfOcr)
                        {
                            progress?.Report(new SearchProgress(
                                scannedItems,
                                documents.Count,
                                info.FullName,
                                SearchPhase.OcrIndexing));
                        }
                    }
                    catch (IOException)
                    {
                        // Files can be replaced while their contents are indexed.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Protected documents are omitted.
                    }

                    if (scannedItems > 0 && scannedItems % 250 == 0)
                    {
                        progress?.Report(new SearchProgress(
                            scannedItems,
                            documents.Count,
                            currentDirectory,
                            SearchPhase.ContentIndexing));
                    }
                }
            }
            catch (IOException)
            {
                // Removable and network drives can disconnect during indexing.
            }
            catch (UnauthorizedAccessException)
            {
                // An inaccessible child folder is skipped.
            }

            if (hitDocumentLimit)
            {
                break;
            }
        }

        progress?.Report(new SearchProgress(
            scannedItems,
            documents.Count,
            string.Empty,
            SearchPhase.ContentIndexing));

        if (hitDocumentLimit)
        {
            foreach (var pair in reusableOcrAttempts)
            {
                currentOcrAttempts.TryAdd(pair.Key, pair.Value);
            }
        }

        return new ContentIndexSnapshot
        {
            FormatVersion = CurrentFormatVersion,
            Root = root,
            BuiltUtc = DateTime.UtcNow,
            RootWriteUtc = GetRootWriteTimeUtc(root),
            MaximumDocuments = maximumDocuments,
            IsComplete = isComplete,
            ScannedItems = scannedItems,
            Documents = documents,
            OcrAttempts = currentOcrAttempts.Values.ToList(),
            DeferredOcrDocuments = deferredOcrDocuments
        };
    }

    private static ContentDocumentRecord CreateDocumentRecord(
        FileSystemInfo info,
        string root,
        ExtractedDocument extracted) =>
        new()
        {
            Name = info.Name,
            FullPath = info.FullName,
            DirectoryPath =
                Path.GetDirectoryName(info.FullName) ?? root,
            Extension = info.Extension.ToLowerInvariant(),
            SizeBytes =
                info is FileInfo file
                    ? file.Length
                    : null,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
            Text = extracted.Text,
            WasTruncated = extracted.WasTruncated,
            Source = extracted.Source,
            AnalyzedPages = extracted.AnalyzedPages
        };

    private static bool IsSpreadsheet(string extension) =>
        extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsb", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xltx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xltm", StringComparison.OrdinalIgnoreCase);

    private static bool HasPreferredFiles(
        ContentIndexSnapshot snapshot,
        IReadOnlyList<IndexedFileRecord>? preferredFiles)
    {
        if (preferredFiles is null || preferredFiles.Count == 0)
        {
            return true;
        }

        var indexed = snapshot.Documents
            .Select(document => document.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return preferredFiles
            .Where(item =>
                !item.IsDirectory &&
                IsSpreadsheet(item.Extension))
            .Take(32)
            .All(item => indexed.Contains(item.FullPath));
    }

    private static bool IsUsable(
        ContentIndexSnapshot snapshot,
        string root,
        DateTime rootWriteUtc,
        int maximumDocuments)
    {
        if (snapshot.FormatVersion != CurrentFormatVersion ||
            !string.Equals(snapshot.Root, root, StringComparison.OrdinalIgnoreCase) ||
            DateTime.UtcNow - snapshot.BuiltUtc > MaximumIndexAge ||
            snapshot.RootWriteUtc != rootWriteUtc)
        {
            return false;
        }

        if (snapshot.DeferredOcrDocuments > 0)
        {
            return false;
        }

        return snapshot.IsComplete ||
               snapshot.MaximumDocuments >= maximumDocuments;
    }

    private static bool IsAvailable(
        ContentIndexSnapshot snapshot,
        string root) =>
        IsCompatibleSeed(snapshot, root);

    private static bool IsCompatibleSeed(
        ContentIndexSnapshot snapshot,
        string root) =>
        snapshot.FormatVersion == CurrentFormatVersion &&
        string.Equals(
            snapshot.Root,
            root,
            StringComparison.OrdinalIgnoreCase);

    private async Task<ContentIndexSnapshot?> TryLoadAsync(
        string indexPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(indexPath))
            {
                return null;
            }

            await using var fileStream = new FileStream(
                indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                65_536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var gzip = new GZipStream(
                fileStream,
                CompressionMode.Decompress);
            return await JsonSerializer.DeserializeAsync<ContentIndexSnapshot>(
                gzip,
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException)
        {
            return null;
        }
    }

    private async Task TrySaveAsync(
        string indexPath,
        ContentIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var temporaryPath = indexPath + ".tmp";
        try
        {
            Directory.CreateDirectory(_indexDirectory);
            await using (var fileStream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             65_536,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan))
            await using (var gzip = new GZipStream(
                             fileStream,
                             CompressionLevel.Fastest,
                             leaveOpen: false))
            {
                await JsonSerializer.SerializeAsync(
                    gzip,
                    snapshot,
                    _jsonOptions,
                    cancellationToken);
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
        return Path.Combine(_indexDirectory, $"content-{key}.json.gz");
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
            // A stale temporary file can be replaced later.
        }
    }
}

public sealed record ContentIndexAccessResult(
    ContentIndexSnapshot Snapshot,
    bool UsedCache);

public sealed class ContentIndexSnapshot
{
    public int FormatVersion { get; set; }

    public required string Root { get; set; }

    public DateTime BuiltUtc { get; set; }

    public DateTime RootWriteUtc { get; set; }

    public int MaximumDocuments { get; set; }

    public bool IsComplete { get; set; }

    public int ScannedItems { get; set; }

    public List<ContentDocumentRecord> Documents { get; set; } = [];

    public List<ContentOcrAttemptRecord> OcrAttempts { get; set; } = [];

    public int DeferredOcrDocuments { get; set; }
}

public sealed class ContentDocumentRecord
{
    public required string Name { get; set; }

    public required string FullPath { get; set; }

    public required string DirectoryPath { get; set; }

    public required string Extension { get; set; }

    public long? SizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public required string Text { get; set; }

    public bool WasTruncated { get; set; }

    public DocumentContentSource Source { get; set; }

    public int AnalyzedPages { get; set; }
}

public sealed class ContentOcrAttemptRecord
{
    public required string FullPath { get; set; }

    public long? SizeBytes { get; set; }

    public DateTime ModifiedUtc { get; set; }
}


public sealed record ContentIndexProbe(
    bool Exists,
    bool IsUsable,
    bool IsComplete,
    int Documents,
    ContentIndexSnapshot? Snapshot)
{
    public static ContentIndexProbe Missing { get; } =
        new(false, false, false, 0, null);

    public static ContentIndexProbe FromSnapshot(
        ContentIndexSnapshot snapshot,
        bool isUsable) =>
        new(
            true,
            isUsable,
            snapshot.IsComplete,
            snapshot.Documents.Count,
            snapshot);
}
