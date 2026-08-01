using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class TitleSearchService
{
    private const int BatchSize = 8;
    private static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(220);
    private static readonly EnumerationOptions VisibleEnumerationOptions =
        SearchVisibilityPolicy.CreateEnumerationOptions();

    private readonly NetworkPathService _networkPathService;

    public TitleSearchService(NetworkPathService networkPathService)
    {
        _networkPathService = networkPathService;
    }

    public Task<TitleSearchSummary> SearchAsync(
        string query,
        IReadOnlyList<string> roots,
        int maximumScannedItems,
        int maximumResults,
        IProgress<TitleSearchProgress>? progress,
        CancellationToken cancellationToken,
        SearchIntent? providedIntent = null) =>
        Task.Factory.StartNew(
                () => SearchCoreAsync(
                    query,
                    roots,
                    maximumScannedItems,
                    maximumResults,
                    progress,
                    cancellationToken,
                    providedIntent),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();

    private async Task<TitleSearchSummary> SearchCoreAsync(
        string query,
        IReadOnlyList<string> roots,
        int maximumScannedItems,
        int maximumResults,
        IProgress<TitleSearchProgress>? progress,
        CancellationToken cancellationToken,
        SearchIntent? providedIntent)
    {
        var matcher = TitleMatcher.Create(
            query,
            providedIntent);
        var normalizedRoots = roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(NormalizeRootSafely)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reportedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingHits = new List<TitleSearchHit>(BatchSize);
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        var scannedItems = 0;
        var matchedItems = 0;
        var skippedRoots = 0;
        var currentPath = normalizedRoots.FirstOrDefault() ?? string.Empty;

        progress?.Report(new TitleSearchProgress(
            ScannedItems: 0,
            MatchedItems: 0,
            CurrentPath: currentPath,
            NewHits: Array.Empty<TitleSearchHit>(),
            IsCompleted: false));

        void ProcessEntry(string entryPath, bool isDirectory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scannedItems >= maximumScannedItems ||
                matchedItems >= maximumResults)
            {
                return;
            }

            scannedItems++;
            var name = Path.GetFileName(entryPath);
            if (SearchVisibilityPolicy.IsExcludedName(name) ||
                !matcher.TryMatch(
                    name,
                    entryPath,
                    isDirectory,
                    out var score,
                    out var matchPercent,
                    out var isExact,
                    out var reason))
            {
                ReportIfDue(
                    force: false,
                    completed: false,
                    currentPath,
                    scannedItems,
                    matchedItems,
                    pendingHits,
                    stopwatch.Elapsed,
                    ref lastReport,
                    progress);
                return;
            }

            if (!reportedPaths.Add(entryPath))
            {
                return;
            }

            DateTime? modifiedLocal = null;
            DateTime? createdLocal = null;
            if (matcher.RequiresFileTimestamps)
            {
                try
                {
                    FileSystemInfo info = isDirectory
                        ? new DirectoryInfo(entryPath)
                        : new FileInfo(entryPath);
                    var modifiedUtc = info.LastWriteTimeUtc;
                    var createdUtc = info.CreationTimeUtc;
                    modifiedLocal = modifiedUtc.ToLocalTime();
                    createdLocal = createdUtc.ToLocalTime();
                    matcher.ApplyPreferences(
                        ref score,
                        ref reason,
                        createdUtc,
                        modifiedUtc,
                        info is FileInfo file ? file.Length : null);
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException or
                    IOException or
                    NotSupportedException)
                {
                    // The lexical title match can still be shown immediately.
                }
            }

            pendingHits.Add(new TitleSearchHit(
                name,
                entryPath,
                isDirectory,
                modifiedLocal,
                score,
                matchPercent,
                isExact,
                reason,
                createdLocal));
            matchedItems++;

            ReportIfDue(
                force: matchedItems == 1 ||
                       isExact ||
                       pendingHits.Count >= BatchSize,
                completed: false,
                currentPath,
                scannedItems,
                matchedItems,
                pendingHits,
                stopwatch.Elapsed,
                ref lastReport,
                progress);
        }

        foreach (var configuredRoot in normalizedRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expandedRoots = await ExpandServerRootAsync(
                configuredRoot,
                cancellationToken);
            if (expandedRoots.Count == 0)
            {
                skippedRoots++;
                continue;
            }

            foreach (var root in expandedRoots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scannedItems >= maximumScannedItems ||
                    matchedItems >= maximumResults)
                {
                    break;
                }

                var queue = new Queue<string>();
                queue.Enqueue(root);

                while (queue.Count > 0 &&
                       scannedItems < maximumScannedItems &&
                       matchedItems < maximumResults)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentPath = queue.Dequeue();
                    if (!visitedDirectories.Add(currentPath))
                    {
                        continue;
                    }

                    // Enumerate files separately so their names can be used
                    // directly. EnumerateFileSystemEntries followed by
                    // File.GetAttributes caused one extra SMB request per file
                    // and delayed the first visible match on large shares.
                    try
                    {
                        foreach (var filePath in Directory.EnumerateFiles(
                                     currentPath,
                                     "*",
                                     VisibleEnumerationOptions))
                        {
                            if (scannedItems >= maximumScannedItems ||
                                matchedItems >= maximumResults)
                            {
                                break;
                            }

                            ProcessEntry(filePath, isDirectory: false);
                        }
                    }
                    catch (Exception exception) when (
                        exception is UnauthorizedAccessException or
                        IOException or
                        NotSupportedException)
                    {
                        // A share may disappear while it is being enumerated.
                    }

                    if (scannedItems >= maximumScannedItems ||
                        matchedItems >= maximumResults)
                    {
                        break;
                    }

                    try
                    {
                        foreach (var directoryPath in
                                 Directory.EnumerateDirectories(
                                     currentPath,
                                     "*",
                                     VisibleEnumerationOptions))
                        {
                            if (scannedItems >= maximumScannedItems ||
                                matchedItems >= maximumResults)
                            {
                                break;
                            }

                            if (SearchVisibilityPolicy.IsExcludedName(
                                    Path.GetFileName(directoryPath)))
                            {
                                continue;
                            }

                            ProcessEntry(directoryPath, isDirectory: true);

                            try
                            {
                                var attributes = File.GetAttributes(directoryPath);
                                if (!attributes.HasFlag(
                                        FileAttributes.ReparsePoint))
                                {
                                    queue.Enqueue(directoryPath);
                                }
                            }
                            catch (Exception exception) when (
                                exception is UnauthorizedAccessException or
                                IOException or
                                NotSupportedException)
                            {
                                // The directory title can still be displayed,
                                // but an inaccessible directory is not traversed.
                            }
                        }
                    }
                    catch (Exception exception) when (
                        exception is UnauthorizedAccessException or
                        IOException or
                        NotSupportedException)
                    {
                        // Continue with other queued folders or roots.
                    }

                    ReportIfDue(
                        force: false,
                        completed: false,
                        currentPath,
                        scannedItems,
                        matchedItems,
                        pendingHits,
                        stopwatch.Elapsed,
                        ref lastReport,
                        progress);
                }
            }
        }

        ReportIfDue(
            force: true,
            completed: true,
            currentPath,
            scannedItems,
            matchedItems,
            pendingHits,
            stopwatch.Elapsed,
            ref lastReport,
            progress);
        return new TitleSearchSummary(
            scannedItems,
            matchedItems,
            skippedRoots);
    }

    private async Task<IReadOnlyList<string>> ExpandServerRootAsync(
        string root,
        CancellationToken cancellationToken)
    {
        if (!NetworkPathService.IsUncServerRoot(root))
        {
            return [root];
        }

        var shares = await _networkPathService.EnumerateServerSharesAsync(
            root,
            cancellationToken);
        return shares.Success
            ? shares.Shares.Select(share => share.Path).ToArray()
            : [];
    }

    private static string NormalizeRootSafely(string root)
    {
        try
        {
            return NetworkPathService.IsPotentialNetworkPath(root)
                ? NetworkPathService.NormalizeNetworkLocationPath(root)
                : Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void ReportIfDue(
        bool force,
        bool completed,
        string currentPath,
        int scannedItems,
        int matchedItems,
        List<TitleSearchHit> pendingHits,
        TimeSpan elapsed,
        ref TimeSpan lastReport,
        IProgress<TitleSearchProgress>? progress)
    {
        if (progress is null)
        {
            pendingHits.Clear();
            lastReport = elapsed;
            return;
        }

        if (!force &&
            pendingHits.Count < BatchSize &&
            elapsed - lastReport < BatchInterval)
        {
            return;
        }

        var batch = pendingHits.Count == 0
            ? Array.Empty<TitleSearchHit>()
            : pendingHits.ToArray();
        pendingHits.Clear();
        lastReport = elapsed;
        progress.Report(new TitleSearchProgress(
            scannedItems,
            matchedItems,
            currentPath,
            batch,
            completed));
    }

    internal sealed class TitleMatcher
    {
        private readonly string _normalizedQuery;
        private readonly TitleTerm[] _terms;
        private readonly TitleTerm[] _literalTerms;
        private readonly IReadOnlyCollection<string> _requestedExtensions;
        private readonly IReadOnlyCollection<FileCategory> _requestedCategories;
        private readonly IReadOnlyList<SearchFloorReference> _floorReferences;
        private readonly IReadOnlyList<SearchTextAttributePredicate>
            _attributePredicates;
        private readonly bool _directoryOnly;
        private readonly bool _filesOnly;
        private readonly bool _strictNameLookup;
        private readonly SearchRankingProfile _rankingProfile;

        private TitleMatcher(
            string normalizedQuery,
            TitleTerm[] terms,
            TitleTerm[] literalTerms,
            IReadOnlyCollection<string> requestedExtensions,
            IReadOnlyCollection<FileCategory> requestedCategories,
            IReadOnlyList<SearchFloorReference> floorReferences,
            IReadOnlyList<SearchTextAttributePredicate> attributePredicates,
            bool directoryOnly,
            bool filesOnly,
            bool strictNameLookup,
            SearchRankingProfile rankingProfile)
        {
            _normalizedQuery = normalizedQuery;
            _terms = terms;
            _literalTerms = literalTerms;
            _requestedExtensions = requestedExtensions;
            _requestedCategories = requestedCategories;
            _floorReferences = floorReferences;
            _attributePredicates = attributePredicates;
            _directoryOnly = directoryOnly;
            _filesOnly = filesOnly;
            _strictNameLookup = strictNameLookup;
            _rankingProfile = rankingProfile;
        }

        public bool RequiresFileTimestamps =>
            _rankingProfile.RequiresFileTimestamps;

        public static TitleMatcher Create(
            string query,
            SearchIntent? providedIntent = null)
        {
            var intent =
                providedIntent ??
                SearchQueryInterpreter.Interpret(query);
            var terms = CreateTitleTerms(intent.Terms);
            var literalTerms = CreateTitleTerms(intent.LiteralTerms);
            if (terms.Length == 0 &&
                literalTerms.Length == 0 &&
                intent.RequestedExtensions.Count == 0 &&
                intent.Categories.Count == 0 &&
                intent.FloorReferences.Count == 0 &&
                intent.AttributePredicates.Count == 0 &&
                !intent.DirectoryOnly &&
                !intent.FilesOnly)
            {
                terms = SearchQueryInterpreter.TokenizeText(query)
                .Select(term => new TitleTerm(
                    Normalize(term),
                    [Normalize(term)]))
                    .Where(term =>
                        SearchQueryInterpreter.IsSearchableToken(
                            term.Original))
                    .DistinctBy(
                        term => term.Original,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return new TitleMatcher(
                Normalize(query),
                terms,
                literalTerms,
                intent.RequestedExtensions,
                intent.Categories,
                intent.FloorReferences,
                intent.AttributePredicates,
                intent.DirectoryOnly,
                intent.FilesOnly,
                intent.IsSingleTermNameLookup,
                intent.RankingProfile);
        }

        private static TitleTerm[] CreateTitleTerms(
            IEnumerable<SearchTerm> source) =>
            source
                .Select(term => new TitleTerm(
                    Normalize(term.Original),
                    term.Alternatives
                        .Select(Normalize)
                        .Where(
                            SearchQueryInterpreter.IsSearchableToken)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()))
                .Where(term =>
                    SearchQueryInterpreter.IsSearchableToken(
                        term.Original))
                .DistinctBy(
                    term => term.Original,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        public void ApplyPreferences(
            ref double score,
            ref string reason,
            DateTime createdUtc,
            DateTime modifiedUtc,
            long? sizeBytes)
        {
            var adjustment =
                SearchRankingPreferenceService.CalculateAdjustment(
                    _rankingProfile,
                    new SearchRankingSignals(
                        createdUtc,
                        modifiedUtc,
                        sizeBytes,
                        0d,
                        0d,
                        0d,
                        0d,
                        0d));
            score += adjustment;
            var preferenceReason =
                SearchRankingPreferenceService.BuildAppliedReason(
                    _rankingProfile);
            if (!string.IsNullOrWhiteSpace(preferenceReason))
            {
                reason = $"{reason} {preferenceReason}".Trim();
            }
        }

        public bool TryMatch(
            string name,
            string fullPath,
            bool isDirectory,
            out double score,
            out double matchPercent,
            out bool isExact,
            out string reason)
        {
            score = 0d;
            matchPercent = 0d;
            isExact = false;
            reason = string.Empty;

            if (_directoryOnly && !isDirectory)
            {
                return false;
            }
            if (_filesOnly && isDirectory)
            {
                return false;
            }

            // Keep the extension in the searchable title. Known or explicitly
            // dotted extensions are handled as exact type filters, while bare
            // queries such as "ppk 파일" can still match an unregistered
            // extension without requiring a catalog update.
            var normalizedTitle = Normalize(name);
            if (normalizedTitle.Length == 0)
            {
                return false;
            }
            var normalizedParentPath = Normalize(
                BuildParentContext(fullPath));
            var literalTitleMatches = _literalTerms.Count(term =>
                term.Alternatives.Any(alternative =>
                    ContainsAlternativeTerm(
                        normalizedTitle,
                        alternative)));
            var literalPathMatches = _literalTerms.Count(term =>
                term.Alternatives.Any(alternative =>
                    ContainsAlternativeTerm(
                        normalizedParentPath,
                        alternative)));
            var hasLiteralDirectMatch =
                literalTitleMatches + literalPathMatches > 0;
            var matchedAttributePredicates = new List<string>();
            foreach (var predicate in _attributePredicates)
            {
                var attributeMatch =
                    SearchTextAttributeAnalyzer.Evaluate(
                        predicate,
                        name,
                        BuildParentContext(fullPath),
                        contentFacts: null,
                        isDirectory);
                if (attributeMatch == SearchAttributeMatch.NoMatch)
                {
                    return false;
                }
                if (attributeMatch == SearchAttributeMatch.Match)
                {
                    matchedAttributePredicates.Add(
                        predicate.Description);
                }
            }
            if (!SearchTextAnalyzer.ContainsAllFloorReferences(
                    _floorReferences,
                    $"{name} {BuildParentContext(fullPath)}"))
            {
                return false;
            }

            var extension = isDirectory
                ? string.Empty
                : Path.GetExtension(name).ToLowerInvariant();
            if (_requestedExtensions.Count > 0 &&
                !_requestedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_requestedCategories.Count > 0 &&
                !isDirectory &&
                !_requestedCategories.Contains(
                    FileTypeCatalog.GetCategory(extension)))
            {
                return false;
            }

            if (_terms.Length == 0 &&
                !isDirectory &&
                (_requestedExtensions.Count > 0 ||
                 _requestedCategories.Count > 0))
            {
                score = _requestedExtensions.Count > 0 ? 880d : 760d;
                matchPercent = _requestedExtensions.Count > 0 ? 96d : 88d;
                reason = _requestedExtensions.Count > 0
                    ? "요청한 파일 확장자와 일치합니다."
                    : "요청한 파일 종류와 일치합니다.";
                return true;
            }

            if (_terms.Length == 0 &&
                isDirectory &&
                _directoryOnly)
            {
                score = 760d;
                matchPercent = 88d;
                reason = "요청한 폴더 항목입니다.";
                return true;
            }

            if (_terms.Length == 0 && _floorReferences.Count > 0)
            {
                score = 820d;
                matchPercent = 92d;
                reason = string.Join(
                    "·",
                    _floorReferences.Select(reference =>
                        $"{reference.Display} 위치와 일치합니다."));
                return true;
            }

            if (_terms.Length == 0 && hasLiteralDirectMatch)
            {
                score = 805d + literalTitleMatches * 25d;
                matchPercent = literalTitleMatches > 0 ? 93d : 86d;
                reason = literalTitleMatches > 0
                    ? "파일 종류 표현이 실제 파일명·폴더명에 들어 있습니다."
                    : "파일 종류 표현이 상위 폴더 경로에 들어 있습니다.";
                return true;
            }

            if (_terms.Length == 0 &&
                matchedAttributePredicates.Count > 0)
            {
                score = 835d +
                        matchedAttributePredicates.Count * 20d;
                matchPercent = 94d;
                reason = string.Join(
                    "·",
                    matchedAttributePredicates) +
                    " 조건이 파일명·경로와 일치합니다.";
                return true;
            }

            if (_normalizedQuery.Length > 0 &&
                string.Equals(
                    normalizedTitle,
                    _normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            {
                score = 1000d;
                matchPercent = 100d;
                isExact = true;
                reason = "파일 제목이 검색어와 정확히 같습니다.";
                return true;
            }

            if (_normalizedQuery.Length >= 2 &&
                normalizedTitle.Contains(
                    _normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            {
                score = 930d;
                matchPercent = 98d;
                isExact = true;
                reason = "파일 제목에 입력한 검색어 전체가 들어 있습니다.";
                return true;
            }

            if (_terms.Length == 0)
            {
                return false;
            }

            var originalMatchedTerms = _terms.Count(term =>
                ContainsOriginalTerm(normalizedTitle, term.Original));
            var titleMatchedTerms = _terms
                .Where(term =>
                    term.Alternatives.Any(alternative =>
                        ContainsAlternativeTerm(
                            normalizedTitle,
                            alternative)))
                .Select(term => term.Original)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pathMatchedTerms = _terms
                .Where(term =>
                    term.Alternatives.Any(alternative =>
                        ContainsAlternativeTerm(
                            normalizedParentPath,
                            alternative)))
                .Select(term => term.Original)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matchedTerms = titleMatchedTerms
                .Concat(pathMatchedTerms)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (_strictNameLookup && titleMatchedTerms.Count == 0)
            {
                return false;
            }
            if (matchedTerms == 0)
            {
                if (!hasLiteralDirectMatch)
                {
                    return false;
                }

                score = 790d + literalTitleMatches * 25d;
                matchPercent = literalTitleMatches > 0 ? 92d : 84d;
                reason = literalTitleMatches > 0
                    ? "파일 종류 표현이 실제 파일명·폴더명에 들어 있습니다."
                    : "파일 종류 표현이 상위 폴더 경로에 들어 있습니다.";
                return true;
            }

            var coverage = matchedTerms / (double)_terms.Length;
            var allTermsMatched = matchedTerms == _terms.Length;
            var allTitleTermsMatched =
                titleMatchedTerms.Count == _terms.Length;
            var allOriginalTermsMatched =
                originalMatchedTerms == _terms.Length;
            var compactCore = string.Concat(
                _terms.Select(term => term.Original));
            var compactTitle = normalizedTitle.Replace(" ", string.Empty);
            var corePhraseMatched = compactCore.Length >= 2 &&
                                    compactTitle.Contains(
                                        compactCore,
                                        StringComparison.OrdinalIgnoreCase);

            if (allTermsMatched && corePhraseMatched)
            {
                score = 900d + Math.Min(40d, compactCore.Length);
                matchPercent = 96d;
                reason = "핵심 검색어가 파일 제목에 같은 순서로 들어 있습니다.";
                return true;
            }

            if (allOriginalTermsMatched)
            {
                score = 850d + Math.Min(40d, _terms.Length * 5d);
                matchPercent = 92d;
                reason = "모든 핵심 검색어가 파일 제목에 들어 있습니다.";
                return true;
            }

            if (allTitleTermsMatched)
            {
                score = 825d + Math.Min(35d, _terms.Length * 5d);
                matchPercent = 90d;
                reason = "모든 핵심 검색어 또는 관련 표현이 파일 제목에 들어 있습니다.";
                return true;
            }

            if (allTermsMatched)
            {
                score = 760d +
                        titleMatchedTerms.Count * 18d +
                        pathMatchedTerms.Count * 10d;
                matchPercent = 88d;
                reason = "파일명과 상위 폴더의 단서를 합쳐 모든 검색 의도와 일치합니다.";
                return true;
            }

            score = 540d + coverage * 190d +
                    originalMatchedTerms * 10d +
                    titleMatchedTerms.Count * 9d +
                    pathMatchedTerms.Count * 4d;
            if (!isDirectory &&
                (_requestedExtensions.Count > 0 ||
                 _requestedCategories.Count > 0))
            {
                score += 80d;
            }
            matchPercent = Math.Clamp(60d + coverage * 28d, 61d, 88d);
            reason = titleMatchedTerms.Count == 0
                ? "검색어 또는 관련 표현이 상위 폴더 경로에 들어 있습니다."
                : _terms.Length == 1
                    ? originalMatchedTerms == 1
                    ? "검색 키워드가 파일 제목에 들어 있습니다."
                    : "검색 키워드의 관련 표현이 파일 제목에 들어 있습니다."
                    : $"핵심 검색어 {_terms.Length:N0}개 중 {matchedTerms:N0}개의 파일명·경로 표현이 일치합니다.";
            return true;
        }

        private sealed record TitleTerm(
            string Original,
            IReadOnlyList<string> Alternatives);

        private static bool ContainsOriginalTerm(
            string normalizedTitle,
            string term) =>
            normalizedTitle.Contains(
                term,
                StringComparison.OrdinalIgnoreCase);

        private static bool ContainsAlternativeTerm(
            string normalizedTitle,
            string alternative)
        {
            if (alternative.Any(char.IsWhiteSpace))
            {
                return normalizedTitle.Contains(
                    alternative,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (alternative.All(character =>
                    character is >= 'a' and <= 'z' or
                        >= '0' and <= '9'))
            {
                return ContainsWholeWord(
                    normalizedTitle,
                    alternative);
            }

            return normalizedTitle.Contains(
                alternative,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsWholeWord(
            string normalizedTitle,
            string word)
        {
            var searchFrom = 0;
            while (searchFrom <= normalizedTitle.Length - word.Length)
            {
                var index = normalizedTitle.IndexOf(
                    word,
                    searchFrom,
                    StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var end = index + word.Length;
                if ((index == 0 || normalizedTitle[index - 1] == ' ') &&
                    (end == normalizedTitle.Length ||
                     normalizedTitle[end] == ' '))
                {
                    return true;
                }

                searchFrom = index + 1;
            }

            return false;
        }

        private static string BuildParentContext(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            var segments = new Stack<string>(3);
            var current = directory;
            for (var depth = 0; depth < 3; depth++)
            {
                var name = Path.GetFileName(
                    Path.TrimEndingDirectorySeparator(current));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    segments.Push(name);
                }

                var parent = Path.GetDirectoryName(
                    Path.TrimEndingDirectorySeparator(current));
                if (string.IsNullOrWhiteSpace(parent) ||
                    string.Equals(
                        parent,
                        current,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }

            return string.Join(' ', segments);
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var previousWasSpace = false;
            foreach (var character in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
