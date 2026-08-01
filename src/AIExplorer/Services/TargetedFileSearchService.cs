using System.Diagnostics;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class TargetedFileSearchService
{
    private static readonly EnumerationOptions ScanningOptions =
        SearchVisibilityPolicy.CreateEnumerationOptions();

    public Task<TargetedSearchResult> FindAsync(
        string root,
        SearchIntent intent,
        int maximumResults,
        bool includeVisualTypeFallback,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken) =>
        FindAsync(
            root,
            intent,
            maximumResults,
            includeVisualTypeFallback,
            progress,
            liveBatch: null,
            maximumScannedItems: int.MaxValue,
            cancellationToken: cancellationToken);

    public Task<TargetedSearchResult> FindAsync(
        string root,
        SearchIntent intent,
        int maximumResults,
        bool includeVisualTypeFallback,
        IProgress<SearchProgress>? progress,
        Action<IReadOnlyList<SearchCandidate>, int, int>? liveBatch,
        CancellationToken cancellationToken) =>
        FindAsync(
            root,
            intent,
            maximumResults,
            includeVisualTypeFallback,
            progress,
            liveBatch,
            maximumScannedItems: int.MaxValue,
            cancellationToken: cancellationToken);

    public Task<TargetedSearchResult> FindAsync(
        string root,
        SearchIntent intent,
        int maximumResults,
        bool includeVisualTypeFallback,
        IProgress<SearchProgress>? progress,
        Action<IReadOnlyList<SearchCandidate>, int, int>? liveBatch,
        int maximumScannedItems,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => Find(
                Path.GetFullPath(root),
                intent,
                Math.Max(1, maximumResults),
                includeVisualTypeFallback,
                progress,
                liveBatch,
                Math.Max(1, maximumScannedItems),
                cancellationToken),
            cancellationToken);
    }

    private static TargetedSearchResult Find(
        string root,
        SearchIntent intent,
        int maximumResults,
        bool includeVisualTypeFallback,
        IProgress<SearchProgress>? progress,
        Action<IReadOnlyList<SearchCandidate>, int, int>? liveBatch,
        int maximumScannedItems,
        CancellationToken cancellationToken)
    {
        var simpleTypeRequest =
            intent.Terms.Count == 0 &&
            !intent.RankingProfile.HasPreferences;
        var candidateCapacity = includeVisualTypeFallback
            ? (int)Math.Min(
                20_000L,
                Math.Max(4_000L, (long)maximumResults * 40L))
            : simpleTypeRequest
            ? maximumResults
            : (int)Math.Min(
                5_000L,
                Math.Max(maximumResults, (long)maximumResults * 12L));
        var candidates =
            new PriorityQueue<SearchCandidate, (double Score, long ModifiedTicks)>();
        var pendingDirectories = new Queue<string>(
            SearchPathPriority.GetTraversalRoots(root));
        var visitedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var scannedItems = 0;
        var matchedItems = 0;
        var liveCandidates = new List<SearchCandidate>();
        var isNetworkRoot = root.StartsWith(
            @"\\",
            StringComparison.OrdinalIgnoreCase);
        var liveBatchSize = isNetworkRoot ? 8 : 24;
        var progressInterval = isNetworkRoot ? 100 : 750;
        var liveFlushClock = Stopwatch.StartNew();

        void FlushLiveCandidates()
        {
            if (liveBatch is null || liveCandidates.Count == 0)
            {
                return;
            }

            var snapshot = liveCandidates.ToArray();
            liveCandidates.Clear();
            liveFlushClock.Restart();
            liveBatch(snapshot, scannedItems, matchedItems);
        }

        while (pendingDirectories.Count > 0 &&
               scannedItems < maximumScannedItems)
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
                    .EnumerateFileSystemInfos("*", ScanningOptions);
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
                    if (scannedItems >= maximumScannedItems)
                    {
                        break;
                    }
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

                        var extension = info.Extension.ToLowerInvariant();
                        if (!MatchesRequestedType(intent, extension))
                        {
                            continue;
                        }

                        var record = new IndexedFileRecord
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            DirectoryPath =
                                Path.GetDirectoryName(info.FullName) ?? root,
                            Extension = extension,
                            IsDirectory = false,
                            SizeBytes = info is FileInfo file ? file.Length : null,
                            CreatedUtc = info.CreationTimeUtc,
                            ModifiedUtc = info.LastWriteTimeUtc
                        };
                        var candidate =
                            SearchRankingService.ScoreCandidate(intent, record);
                        if (candidate is null &&
                            includeVisualTypeFallback &&
                            IsVisualSearchDocument(extension))
                        {
                            candidate = new SearchCandidate(
                                record,
                                SearchPathPriority.GetPathPriority(
                                    record.DirectoryPath),
                                "시각 AI 분석 대상 이미지·PDF입니다.",
                                0,
                                0);
                        }

                        if (candidate is null)
                        {
                            continue;
                        }

                        matchedItems++;
                        RetainBestCandidate(
                            candidates,
                            candidate,
                            candidateCapacity);
                        if (liveBatch is not null)
                        {
                            liveCandidates.Add(candidate);
                            if (liveCandidates.Count >= liveBatchSize ||
                                liveFlushClock.Elapsed >= TimeSpan.FromMilliseconds(300))
                            {
                                FlushLiveCandidates();
                            }
                        }

                        if (simpleTypeRequest &&
                            !includeVisualTypeFallback &&
                            candidates.Count >= candidateCapacity)
                        {
                            FlushLiveCandidates();
                            return Complete(
                                candidates,
                                scannedItems,
                                matchedItems,
                                progress);
                        }
                    }
                    catch (IOException)
                    {
                        // A file can disappear while a drive is being searched.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Protected files are omitted without aborting the search.
                    }

                    if (liveCandidates.Count > 0 &&
                        liveFlushClock.Elapsed >= TimeSpan.FromMilliseconds(300))
                    {
                        FlushLiveCandidates();
                    }

                    if (scannedItems > 0 && scannedItems % progressInterval == 0)
                    {
                        progress?.Report(new SearchProgress(
                            scannedItems,
                            matchedItems,
                            currentDirectory,
                            SearchPhase.TargetedScanning));
                    }
                }
            }
            catch (IOException)
            {
                // Removable and network drives can disconnect during enumeration.
            }
            catch (UnauthorizedAccessException)
            {
                // An inaccessible child folder is skipped.
            }
        }

        FlushLiveCandidates();
        return Complete(candidates, scannedItems, matchedItems, progress);
    }

    private static bool MatchesRequestedType(
        SearchIntent intent,
        string extension)
    {
        if (intent.RequestedExtensions.Count > 0 &&
            !intent.RequestedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return intent.Categories.Count == 0 ||
               intent.Categories.Contains(FileTypeCatalog.GetCategory(extension));
    }

    private static bool IsVisualSearchDocument(string extension) =>
        FileTypeCatalog.GetCategory(extension) == FileCategory.Image ||
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static void RetainBestCandidate(
        PriorityQueue<SearchCandidate, (double Score, long ModifiedTicks)> candidates,
        SearchCandidate candidate,
        int capacity)
    {
        var priority = (
            candidate.RankingScore,
            candidate.Record.ModifiedUtc.Ticks);
        if (candidates.Count < capacity)
        {
            candidates.Enqueue(candidate, priority);
            return;
        }

        candidates.TryPeek(out _, out var lowestPriority);
        if (Comparer<(double Score, long ModifiedTicks)>.Default.Compare(
                priority,
                lowestPriority) <= 0)
        {
            return;
        }

        candidates.Dequeue();
        candidates.Enqueue(candidate, priority);
    }

    private static TargetedSearchResult Complete(
        PriorityQueue<SearchCandidate, (double Score, long ModifiedTicks)> candidates,
        int scannedItems,
        int matchedItems,
        IProgress<SearchProgress>? progress)
    {
        progress?.Report(new SearchProgress(
            scannedItems,
            matchedItems,
            string.Empty,
            SearchPhase.TargetedScanning));

        return new TargetedSearchResult(
            candidates.UnorderedItems
                .Select(item => item.Element.Record)
                .ToArray(),
            scannedItems);
    }
}

public sealed record TargetedSearchResult(
    IReadOnlyList<IndexedFileRecord> Records,
    int ScannedItems);
