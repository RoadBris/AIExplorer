using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class InstantTitleSearchService
{
    private const int MaximumIndexedItemsPerRoot = 1_000_000;
    private readonly MetadataIndexService _indexService;
    private readonly ConditionalWeakTable<
        MetadataIndexSnapshot,
        PreparedIndexState> _preparedIndexes = new();

    public InstantTitleSearchService(MetadataIndexService indexService)
    {
        _indexService = indexService;
    }

    public async Task WarmIndexesAsync(
        IReadOnlyList<string> roots,
        IProgress<InstantTitleIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedRoots = NormalizeRoots(roots);
        var indexedItems = 0;
        for (var index = 0; index < normalizedRoots.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = normalizedRoots[index];
            progress?.Report(new InstantTitleIndexProgress(
                index,
                normalizedRoots.Length,
                indexedItems,
                root,
                root,
                CalculateOverallPercent(
                    index,
                    normalizedRoots.Length,
                    0d)));

            try
            {
                var rootProgress = new ForwardingProgress<SearchProgress>(
                    state =>
                    {
                        var rootPercent = Math.Clamp(
                            state.PercentComplete ?? 0d,
                            0d,
                            99d);
                        progress?.Report(new InstantTitleIndexProgress(
                            index,
                            normalizedRoots.Length,
                            SaturatingAdd(
                                indexedItems,
                                state.ScannedItems),
                            root,
                            string.IsNullOrWhiteSpace(state.CurrentPath)
                                ? root
                                : state.CurrentPath,
                            CalculateOverallPercent(
                                index,
                                normalizedRoots.Length,
                                rootPercent)));
                    });
                var result = await _indexService.GetOrBuildAsync(
                        root,
                        MaximumIndexedItemsPerRoot,
                        rootProgress,
                        cancellationToken)
                    .ConfigureAwait(false);
                progress?.Report(new InstantTitleIndexProgress(
                    index,
                    normalizedRoots.Length,
                    SaturatingAdd(
                        indexedItems,
                        result.Snapshot.Items.Count),
                    root,
                    $"{root} · 빠른 검색 구조 준비 중",
                    CalculateOverallPercent(
                        index,
                        normalizedRoots.Length,
                        99d)));
                _ = await GetPreparedIndexAsync(
                        result.Snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
                indexedItems = SaturatingAdd(
                    indexedItems,
                    result.Snapshot.Items.Count);
            }
            catch (Exception exception) when (
                exception is IOException or
                    UnauthorizedAccessException or
                    NotSupportedException or
                    ArgumentException)
            {
                // Unavailable and protected roots are skipped. Other roots remain usable.
            }

            progress?.Report(new InstantTitleIndexProgress(
                index + 1,
                normalizedRoots.Length,
                indexedItems,
                root,
                root,
                CalculateOverallPercent(
                    index,
                    normalizedRoots.Length,
                    100d)));
        }

        progress?.Report(new InstantTitleIndexProgress(
            normalizedRoots.Length,
            normalizedRoots.Length,
            indexedItems,
            string.Empty,
            string.Empty,
            100d,
            IsCompleted: true));
    }

    private static double CalculateOverallPercent(
        int completedRoots,
        int totalRoots,
        double currentRootPercent)
    {
        if (totalRoots <= 0)
        {
            return 100d;
        }

        return Math.Clamp(
            (completedRoots + currentRootPercent / 100d) /
            totalRoots * 100d,
            0d,
            100d);
    }

    private static int SaturatingAdd(int left, int right) =>
        (int)Math.Min(int.MaxValue, (long)left + right);

    public async Task<InstantTitleSearchResponse> SearchAsync(
        string query,
        IReadOnlyList<string> roots,
        InstantTitleSearchOptions options,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new InstantTitleSearchResponse(
                [],
                0,
                0,
                0,
                0,
                stopwatch.Elapsed);
        }

        var normalizedRoots = NormalizeRoots(roots);
        var preparedSnapshots = new List<PreparedSnapshot>(
            normalizedRoots.Length);
        var missingRoots = 0;
        foreach (var root in normalizedRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var available = await _indexService.TryGetAvailableAsync(
                        root,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (available is null)
                {
                    missingRoots++;
                    continue;
                }

                var preparedIndex = await GetPreparedIndexAsync(
                        available.Snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
                preparedSnapshots.Add(new PreparedSnapshot(
                    available.Snapshot,
                    preparedIndex));
            }
            catch (Exception exception) when (
                exception is IOException or
                    UnauthorizedAccessException or
                    NotSupportedException or
                    ArgumentException)
            {
                missingRoots++;
            }
        }

        return await Task.Run(
                () => SearchPreparedSnapshots(
                    query.Trim(),
                    preparedSnapshots,
                    missingRoots,
                    options,
                    Math.Max(1, maximumResults),
                    stopwatch,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static InstantTitleSearchResponse SearchPreparedSnapshots(
        string query,
        IReadOnlyList<PreparedSnapshot> preparedSnapshots,
        int missingRoots,
        InstantTitleSearchOptions options,
        int maximumResults,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        if (!InstantTitleMatcher.TryCreate(
                query,
                options,
                out var matcher,
                out var validationError))
        {
            return new InstantTitleSearchResponse(
                [],
                0,
                preparedSnapshots.Sum(item => item.Index.Count),
                preparedSnapshots.Count,
                missingRoots,
                stopwatch.Elapsed,
                validationError);
        }

        var matched = new List<IndexedFileRecord>(
            preparedSnapshots.Count * maximumResults);
        var totalMatches = 0;
        var indexedItems = 0;
        foreach (var prepared in preparedSnapshots)
        {
            indexedItems = SaturatingAdd(
                indexedItems,
                prepared.Index.Count);
            var batch = SearchPreparedIndex(
                prepared.Index,
                query,
                matcher,
                options,
                maximumResults,
                cancellationToken);
            totalMatches = SaturatingAdd(
                totalMatches,
                batch.TotalMatches);
            matched.AddRange(batch.TopMatches);
        }

        IOrderedEnumerable<IndexedFileRecord> ordered = options.SortField switch
        {
            InstantTitleSortField.Path => options.SortAscending
                ? matched.OrderBy(
                    item => item.DirectoryPath,
                    StringComparer.CurrentCultureIgnoreCase)
                : matched.OrderByDescending(
                    item => item.DirectoryPath,
                    StringComparer.CurrentCultureIgnoreCase),
            InstantTitleSortField.Size => options.SortAscending
                ? matched.OrderBy(item => item.SizeBytes ?? -1L)
                : matched.OrderByDescending(item => item.SizeBytes ?? -1L),
            InstantTitleSortField.Modified => options.SortAscending
                ? matched.OrderBy(item => item.ModifiedUtc)
                : matched.OrderByDescending(item => item.ModifiedUtc),
            _ => options.SortAscending
                ? matched.OrderBy(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                : matched.OrderByDescending(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
        };

        var seenPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var results = ordered
            .ThenBy(item => item.FullPath, StringComparer.CurrentCultureIgnoreCase)
            .Where(item => seenPaths.Add(item.FullPath))
            .Take(maximumResults)
            .Select(item => new InstantTitleSearchItem(
                item.Name,
                item.FullPath,
                item.DirectoryPath,
                item.Extension,
                item.IsDirectory,
                item.SizeBytes,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToArray();
        stopwatch.Stop();
        return new InstantTitleSearchResponse(
            results,
            totalMatches,
            indexedItems,
            preparedSnapshots.Count,
            missingRoots,
            stopwatch.Elapsed);
    }

    public async Task<TitleSearchSummary> SearchNaturalLanguageAsync(
        string query,
        IReadOnlyList<string> roots,
        int maximumResults,
        IProgress<TitleSearchProgress>? progress,
        CancellationToken cancellationToken,
        SearchIntent? providedIntent = null)
    {
        var normalizedRoots = NormalizeRoots(roots);
        var preparedSnapshots = new List<PreparedSnapshot>(
            normalizedRoots.Length);
        var skippedRoots = 0;
        foreach (var root in normalizedRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var available = await _indexService.TryGetAvailableAsync(
                        root,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (available is null)
                {
                    skippedRoots++;
                    continue;
                }

                preparedSnapshots.Add(new PreparedSnapshot(
                    available.Snapshot,
                    await GetPreparedIndexAsync(
                            available.Snapshot,
                            cancellationToken)
                        .ConfigureAwait(false)));
            }
            catch (Exception exception) when (
                exception is IOException or
                    UnauthorizedAccessException or
                    NotSupportedException or
                    ArgumentException)
            {
                skippedRoots++;
            }
        }

        var intent = providedIntent ??
                     SearchQueryInterpreter.Interpret(query);
        var candidateTerms = BuildNaturalCandidateTerms(intent);
        return await Task.Run(
                () => SearchNaturalLanguagePreparedIndexes(
                    query,
                    intent,
                    candidateTerms,
                    preparedSnapshots,
                    Math.Max(1, maximumResults),
                    skippedRoots,
                    progress,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static PreparedSearchBatch SearchPreparedIndex(
        InstantTitleMemoryIndex index,
        string query,
        InstantTitleMatcher matcher,
        InstantTitleSearchOptions options,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int>? candidateIds = matcher.CanUseNameIndex
            ? index.FindNameCandidates(query)
            : null;
        if (matcher.PostingGuaranteesMatch &&
            candidateIds is not null &&
            options.SortField == InstantTitleSortField.Name)
        {
            var directMatches = new List<IndexedFileRecord>(
                Math.Min(maximumResults, candidateIds.Count));
            var directCount = Math.Min(
                maximumResults,
                candidateIds.Count);
            for (var offset = 0; offset < directCount; offset++)
            {
                var orderedOffset = options.SortAscending
                    ? offset
                    : candidateIds.Count - 1 - offset;
                directMatches.Add(index[candidateIds[orderedOffset]]);
            }

            return new PreparedSearchBatch(
                directMatches,
                candidateIds.Count);
        }

        var topMatches = new List<IndexedFileRecord>(maximumResults);
        List<IndexedFileRecord>? allMatches =
            options.SortField == InstantTitleSortField.Name
                ? null
                : [];
        var totalMatches = 0;
        var candidateCount = candidateIds?.Count ?? index.Count;
        for (var offset = 0; offset < candidateCount; offset++)
        {
            if ((offset & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var orderedOffset =
                options.SortField == InstantTitleSortField.Name &&
                !options.SortAscending
                    ? candidateCount - 1 - offset
                    : offset;
            var itemId = candidateIds is null
                ? orderedOffset
                : candidateIds[orderedOffset];
            var item = index[itemId];
            if (!matcher.IsMatch(item))
            {
                continue;
            }

            totalMatches++;
            if (allMatches is not null)
            {
                allMatches.Add(item);
            }
            else if (topMatches.Count < maximumResults)
            {
                topMatches.Add(item);
            }
        }

        if (allMatches is not null)
        {
            topMatches.AddRange(OrderMatches(allMatches, options)
                .Take(maximumResults));
        }

        return new PreparedSearchBatch(topMatches, totalMatches);
    }

    private static TitleSearchSummary SearchNaturalLanguagePreparedIndexes(
        string query,
        SearchIntent intent,
        IReadOnlyList<string> candidateTerms,
        IReadOnlyList<PreparedSnapshot> preparedSnapshots,
        int maximumResults,
        int skippedRoots,
        IProgress<TitleSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var matcher = TitleSearchService.TitleMatcher.Create(
            query,
            intent);
        var hits = new List<TitleSearchHit>(maximumResults);
        var seenPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var scannedItems = 0;
        foreach (var prepared in preparedSnapshots)
        {
            IReadOnlyList<int>? candidateIds = candidateTerms.Count > 0
                ? prepared.Index.FindContextCandidates(candidateTerms)
                : null;
            var candidateCount = candidateIds?.Count ?? prepared.Index.Count;
            for (var offset = 0;
                 offset < candidateCount && hits.Count < maximumResults;
                 offset++)
            {
                if ((offset & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                scannedItems++;
                var itemId = candidateIds is null
                    ? offset
                    : candidateIds[offset];
                var item = prepared.Index[itemId];
                if (!seenPaths.Add(item.FullPath) ||
                    !matcher.TryMatch(
                        item.Name,
                        item.FullPath,
                        item.IsDirectory,
                        out var score,
                        out var matchPercent,
                        out var isExact,
                        out var reason))
                {
                    continue;
                }

                if (matcher.RequiresFileTimestamps)
                {
                    matcher.ApplyPreferences(
                        ref score,
                        ref reason,
                        item.CreatedUtc,
                        item.ModifiedUtc,
                        item.SizeBytes);
                }

                hits.Add(new TitleSearchHit(
                    item.Name,
                    item.FullPath,
                    item.IsDirectory,
                    item.ModifiedUtc == default
                        ? null
                        : item.ModifiedUtc.ToLocalTime(),
                    score,
                    matchPercent,
                    isExact,
                    reason,
                    item.CreatedUtc == default
                        ? null
                        : item.CreatedUtc.ToLocalTime()));
            }

            if (hits.Count >= maximumResults)
            {
                break;
            }
        }

        var orderedHits = hits
            .OrderByDescending(hit => hit.Score)
            .ThenByDescending(hit => hit.IsExactMatch)
            .ThenBy(hit => hit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        progress?.Report(new TitleSearchProgress(
            scannedItems,
            orderedHits.Length,
            string.Empty,
            orderedHits,
            IsCompleted: true));
        return new TitleSearchSummary(
            scannedItems,
            orderedHits.Length,
            skippedRoots);
    }

    private static IReadOnlyList<string> BuildNaturalCandidateTerms(
        SearchIntent intent) =>
        intent.Terms
            .Concat(intent.LiteralTerms)
            .SelectMany(term => term.Alternatives.Prepend(term.Original))
            .Concat(intent.RequestedExtensions.Select(extension =>
                extension.TrimStart('.')))
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IOrderedEnumerable<IndexedFileRecord> OrderMatches(
        IEnumerable<IndexedFileRecord> matched,
        InstantTitleSearchOptions options) =>
        options.SortField switch
        {
            InstantTitleSortField.Path => options.SortAscending
                ? matched.OrderBy(
                    item => item.DirectoryPath,
                    StringComparer.CurrentCultureIgnoreCase)
                : matched.OrderByDescending(
                    item => item.DirectoryPath,
                    StringComparer.CurrentCultureIgnoreCase),
            InstantTitleSortField.Size => options.SortAscending
                ? matched.OrderBy(item => item.SizeBytes ?? -1L)
                : matched.OrderByDescending(item => item.SizeBytes ?? -1L),
            InstantTitleSortField.Modified => options.SortAscending
                ? matched.OrderBy(item => item.ModifiedUtc)
                : matched.OrderByDescending(item => item.ModifiedUtc),
            _ => options.SortAscending
                ? matched.OrderBy(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                : matched.OrderByDescending(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
        };

    private Task<InstantTitleMemoryIndex> GetPreparedIndexAsync(
        MetadataIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var state = _preparedIndexes.GetValue(
            snapshot,
            static item => new PreparedIndexState(item));
        return state.IndexTask.WaitAsync(cancellationToken);
    }

    private static string[] NormalizeRoots(IEnumerable<string> roots) =>
        roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root =>
            {
                try
                {
                    return Path.GetFullPath(root);
                }
                catch
                {
                    return string.Empty;
                }
            })
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record PreparedSnapshot(
        MetadataIndexSnapshot Snapshot,
        InstantTitleMemoryIndex Index);

    private sealed record PreparedSearchBatch(
        IReadOnlyList<IndexedFileRecord> TopMatches,
        int TotalMatches);

    private sealed class PreparedIndexState
    {
        public PreparedIndexState(MetadataIndexSnapshot snapshot)
        {
            IndexTask = Task.Run(() =>
                InstantTitleMemoryIndex.Create(snapshot.Items));
        }

        public Task<InstantTitleMemoryIndex> IndexTask { get; }
    }

    private sealed class ForwardingProgress<T>(Action<T> report)
        : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class InstantTitleMatcher
    {
        private readonly string _query;
        private readonly InstantTitleSearchOptions _options;
        private readonly StringComparison _comparison;
        private readonly Regex? _regularExpression;
        private readonly bool _searchFullPath;

        public bool CanUseNameIndex =>
            !_searchFullPath && _regularExpression is null;

        public bool PostingGuaranteesMatch =>
            CanUseNameIndex &&
            _query.Length == 1 &&
            !_options.MatchCase &&
            !_options.MatchWholeWord &&
            _options.ItemFilter == InstantTitleItemFilter.All;

        private InstantTitleMatcher(
            string query,
            InstantTitleSearchOptions options,
            Regex? regularExpression)
        {
            _query = query;
            _options = options;
            _regularExpression = regularExpression;
            _comparison = options.MatchCase
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            _searchFullPath = query.Contains(Path.DirectorySeparatorChar) ||
                              query.Contains(Path.AltDirectorySeparatorChar) ||
                              query.Contains(':');
        }

        public static bool TryCreate(
            string query,
            InstantTitleSearchOptions options,
            out InstantTitleMatcher matcher,
            out string? validationError)
        {
            Regex? regularExpression = null;
            if (options.UseRegularExpression)
            {
                try
                {
                    var regexOptions = RegexOptions.CultureInvariant;
                    if (!options.MatchCase)
                    {
                        regexOptions |= RegexOptions.IgnoreCase;
                    }

                    regularExpression = new Regex(
                        query,
                        regexOptions,
                        TimeSpan.FromMilliseconds(75));
                }
                catch (ArgumentException)
                {
                    matcher = null!;
                    validationError = "정규식 문법을 확인해 주세요.";
                    return false;
                }
            }

            matcher = new InstantTitleMatcher(
                query,
                options,
                regularExpression);
            validationError = null;
            return true;
        }

        public bool IsMatch(IndexedFileRecord item)
        {
            if (_options.ItemFilter == InstantTitleItemFilter.Files &&
                item.IsDirectory)
            {
                return false;
            }
            if (_options.ItemFilter == InstantTitleItemFilter.Folders &&
                !item.IsDirectory)
            {
                return false;
            }

            var candidate = _searchFullPath ? item.FullPath : item.Name;
            if (_regularExpression is not null)
            {
                try
                {
                    return _regularExpression.IsMatch(candidate);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }

            if (!_options.MatchWholeWord)
            {
                return candidate.Contains(_query, _comparison);
            }

            var searchFrom = 0;
            while (searchFrom <= candidate.Length - _query.Length)
            {
                var index = candidate.IndexOf(
                    _query,
                    searchFrom,
                    _comparison);
                if (index < 0)
                {
                    return false;
                }

                var end = index + _query.Length;
                if ((index == 0 || !char.IsLetterOrDigit(candidate[index - 1])) &&
                    (end == candidate.Length || !char.IsLetterOrDigit(candidate[end])))
                {
                    return true;
                }

                searchFrom = index + 1;
            }

            return false;
        }
    }
}
