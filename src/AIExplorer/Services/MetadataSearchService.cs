using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class MetadataSearchService
{
    private const int MaximumMetadataSemanticDocumentsPerRoot = 20_000;

    private readonly FileSystemService _fileSystemService;
    private readonly MetadataIndexService _indexService;
    private readonly ContentIndexService _contentIndexService;
    private readonly ITextEmbeddingService? _embeddingService;
    private readonly SemanticIndexService? _semanticIndexService;
    private readonly IVisualEmbeddingService? _visualEmbeddingService;
    private readonly VisualIndexService? _visualIndexService;
    private readonly TargetedFileSearchService _targetedSearchService = new();
    private readonly DocumentTextExtractor _resultTextExtractor = new();

    internal MetadataIndexService MetadataIndexService => _indexService;

    public MetadataSearchService(FileSystemService fileSystemService)
        : this(
            fileSystemService,
            Path.Combine(new SettingsService().DataDirectory, "index"))
    {
    }

    public MetadataSearchService(
        FileSystemService fileSystemService,
        string indexDirectory)
        : this(
            fileSystemService,
            indexDirectory,
            Path.Combine(
                Path.GetDirectoryName(indexDirectory) ?? indexDirectory,
                "content-index"))
    {
    }

    public MetadataSearchService(
        FileSystemService fileSystemService,
        string indexDirectory,
        string contentIndexDirectory)
    {
        _fileSystemService = fileSystemService;
        _indexService = new MetadataIndexService(indexDirectory);
        _contentIndexService = new ContentIndexService(contentIndexDirectory);
    }

    public MetadataSearchService(
        FileSystemService fileSystemService,
        string indexDirectory,
        string contentIndexDirectory,
        string semanticIndexDirectory,
        ITextEmbeddingService embeddingService)
        : this(fileSystemService, indexDirectory, contentIndexDirectory)
    {
        _embeddingService = embeddingService;
        _semanticIndexService = new SemanticIndexService(
            semanticIndexDirectory,
            embeddingService);
    }

    public MetadataSearchService(
        FileSystemService fileSystemService,
        string indexDirectory,
        string contentIndexDirectory,
        string semanticIndexDirectory,
        ITextEmbeddingService embeddingService,
        string visualIndexDirectory,
        IVisualEmbeddingService visualEmbeddingService,
        IImageTaggingService? imageTagger = null)
        : this(
            fileSystemService,
            indexDirectory,
            contentIndexDirectory,
            semanticIndexDirectory,
            embeddingService)
    {
        _visualEmbeddingService = visualEmbeddingService;
        _visualIndexService = new VisualIndexService(
            visualIndexDirectory,
            visualEmbeddingService,
            imageTagger);
    }

    public async Task<IReadOnlyDictionary<string, SearchResultTextFacts>>
        GetResultTextFactsAsync(
            IReadOnlyList<string> requestedRoots,
            IReadOnlyCollection<string> candidatePaths,
            int maximumOnDemandDocuments,
            IProgress<ResultTextFactsProgress>? progress,
            CancellationToken cancellationToken)
    {
        var candidateSet = candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var facts = new Dictionary<string, SearchResultTextFacts>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var root in requestedRoots
                     .Where(root => !string.IsNullOrWhiteSpace(root))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var available = await _contentIndexService
                    .TryGetAvailableAsync(root, cancellationToken);
                if (available is null)
                {
                    continue;
                }

                foreach (var document in available.Snapshot.Documents)
                {
                    if (!candidateSet.Contains(document.FullPath))
                    {
                        continue;
                    }

                    facts[document.FullPath] = new SearchResultTextFacts(
                        ContentKnown: true,
                        SearchTextAttributeAnalyzer.Analyze(document.Text),
                        document.Source);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    "현재 결과의 저장된 내용 속성을 일부 읽지 못했습니다. " +
                    exception.Message);
            }
        }

        var missingPaths = candidateSet
            .Where(path => !facts.ContainsKey(path))
            .Where(File.Exists)
            .Where(path =>
                _resultTextExtractor.CanExtract(
                    Path.GetExtension(path)))
            .Take(Math.Max(0, maximumOnDemandDocuments))
            .ToArray();
        for (var index = 0; index < missingPaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = missingPaths[index];
            var extracted = await _resultTextExtractor.ExtractAsync(
                path,
                cancellationToken);
            facts[path] = extracted is null
                ? new SearchResultTextFacts(
                    ContentKnown: false,
                    default)
                : new SearchResultTextFacts(
                    ContentKnown: true,
                    SearchTextAttributeAnalyzer.Analyze(extracted.Text),
                    extracted.Source);
            progress?.Report(new ResultTextFactsProgress(
                index + 1,
                missingPaths.Length,
                path));
        }

        foreach (var path in candidateSet)
        {
            facts.TryAdd(
                path,
                new SearchResultTextFacts(
                    ContentKnown: false,
                    default));
        }

        return facts;
    }

    public async Task<SearchIndexReadiness> GetIndexReadinessAsync(
        string query,
        IReadOnlyList<string> requestedRoots,
        int maximumScannedItems,
        int maximumContentDocuments,
        CancellationToken cancellationToken,
        SearchIntent? providedIntent = null)
    {
        var intent =
            providedIntent ??
            SearchQueryInterpreter.Interpret(query);
        var roots = requestedRoots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roots.Length == 0)
        {
            return SearchIndexReadiness.Ready(0);
        }

        var perRootItems = Math.Max(
            1,
            Math.Max(1, maximumScannedItems) / roots.Length);
        var perRootDocuments = Math.Max(
            1,
            Math.Max(1, maximumContentDocuments) / roots.Length);
        var missingMetadata = 0;
        var staleMetadata = 0;
        var incompleteContent = 0;
        var incompleteSemantic = 0;
        var incompleteVisual = 0;
        var indexedItems = 0;
        var contentDocuments = 0;
        var semanticDocuments = 0;
        var visualDocuments = 0;
        var visualFiles = 0;
        var visualRequested = ShouldSearchVisually(intent);
        var semanticRequested =
            intent.Terms.Count > 0 &&
            ShouldSearchContent(intent) &&
            !visualRequested;

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = await _indexService.ProbeAsync(
                root,
                perRootItems,
                cancellationToken);
            indexedItems += metadata.IndexedItems;
            if (!metadata.Exists)
            {
                missingMetadata++;
                continue;
            }

            if (!metadata.IsUsable)
            {
                staleMetadata++;
                continue;
            }

            if (!metadata.IsComplete)
            {
                staleMetadata++;
            }

            if (!ShouldSearchContent(intent))
            {
                continue;
            }

            var content = await _contentIndexService.ProbeAsync(
                root,
                perRootDocuments,
                cancellationToken);
            contentDocuments += content.Documents;
            var preferredContentItems = OrderPreferredContentItems(
                metadata.Items,
                intent);
            if (!content.IsUsable ||
                !content.IsComplete ||
                !HasPreferredContentItems(
                    content.Snapshot?.Documents ?? [],
                    preferredContentItems))
            {
                incompleteContent++;
            }

            var semanticSource = BuildSemanticDocuments(
                root,
                metadata.Items,
                content.Snapshot?.Documents ?? [],
                intent);
            if (semanticRequested &&
                _semanticIndexService is not null &&
                _embeddingService?.IsAvailable == true &&
                semanticSource.Count > 0)
            {
                var semantic = await _semanticIndexService.ProbeAsync(
                    root,
                    semanticSource,
                    cancellationToken);
                semanticDocuments += semantic.IndexedDocuments;
                if (!semantic.IsComplete)
                {
                    incompleteSemantic++;
                }
            }

            if (visualRequested &&
                _visualIndexService is not null &&
                _visualEmbeddingService?.IsAvailable == true)
            {
                var visual = await _visualIndexService.ProbeAsync(
                    root,
                    metadata.Items,
                    cancellationToken,
                    requireCharacterTagging:
                        VisualQueryPromptBuilder
                            .Analyze(intent)
                            .IsNamedSubject);
                visualDocuments += visual.IndexedDocuments;
                visualFiles += visual.TotalDocuments;
                if (!visual.IsComplete)
                {
                    incompleteVisual++;
                }
            }
        }

        var requiresIndexing =
            missingMetadata > 0 ||
            staleMetadata > 0 ||
            incompleteContent > 0 ||
            incompleteSemantic > 0 ||
            incompleteVisual > 0;
        var details = new List<string>();
        if (missingMetadata > 0)
        {
            details.Add($"기본 색인 없음 {missingMetadata}곳");
        }
        if (staleMetadata > 0)
        {
            details.Add($"기본 색인 갱신 필요 {staleMetadata}곳");
        }
        if (incompleteContent > 0)
        {
            details.Add($"본문·OCR 미완료 {incompleteContent}곳");
        }
        if (incompleteSemantic > 0)
        {
            details.Add($"문서 AI 미완료 {incompleteSemantic}곳");
        }
        if (incompleteVisual > 0)
        {
            details.Add($"이미지 AI 미완료 {incompleteVisual}곳");
        }

        return new SearchIndexReadiness(
            requiresIndexing,
            visualRequested,
            semanticRequested,
            roots.Length,
            missingMetadata,
            staleMetadata,
            incompleteContent,
            incompleteSemantic,
            incompleteVisual,
            indexedItems,
            contentDocuments,
            semanticDocuments,
            visualDocuments,
            visualFiles,
            requiresIndexing
                ? string.Join(" · ", details)
                : "검색 색인이 준비되어 있습니다.");
    }

    public async Task<IndexWarmupResult> PrepareIndexesAsync(
        IReadOnlyList<string> requestedRoots,
        bool includeSemanticIndex,
        bool includeVisualIndex,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        IndexWarmupResult latest = new(0, 0, 0, 0, 0, 0, 0);
        for (var pass = 0; pass < 96; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            latest = await WarmUpAsync(
                requestedRoots,
                progress,
                cancellationToken,
                maximumMetadataItemsPerRoot: 240_000,
                maximumContentDocumentsPerRoot: 20_000,
                maximumNewSemanticDocumentsPerRoot:
                    includeSemanticIndex ? 384 : 0,
                maximumNewVisualDocumentsPerRoot:
                    includeVisualIndex ? 256 : 0);
            if ((!includeSemanticIndex ||
                 latest.NewlyIndexedSemanticDocuments == 0) &&
                (!includeVisualIndex ||
                 latest.NewlyIndexedVisualDocuments == 0))
            {
                break;
            }
        }

        return latest;
    }

    public async Task<IndexWarmupResult> WarmUpAsync(
        IReadOnlyList<string> requestedRoots,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken,
        int maximumMetadataItemsPerRoot = 40_000,
        int maximumContentDocumentsPerRoot = 5_000,
        int maximumNewSemanticDocumentsPerRoot = 192,
        int maximumNewVisualDocumentsPerRoot = 48,
        bool forceMetadataRefresh = false,
        bool forceContentRefresh = false,
        string? priorityQuery = null)
    {
        var roots = requestedRoots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var indexedItems = 0;
        var contentDocuments = 0;
        var semanticDocuments = 0;
        var newlyIndexedSemanticDocuments = 0;
        var visualDocuments = 0;
        var newlyIndexedVisualDocuments = 0;
        var priorityIntent = string.IsNullOrWhiteSpace(priorityQuery)
            ? null
            : SearchQueryInterpreter.Interpret(priorityQuery);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = await _indexService.GetOrBuildAsync(
                    root,
                    Math.Max(1, maximumMetadataItemsPerRoot),
                    progress,
                    cancellationToken,
                    forceRefresh: forceMetadataRefresh && IsNetworkRoot(root));
                indexedItems = SaturatingAdd(
                    indexedItems,
                    metadata.Snapshot.Items.Count);

                ContentIndexAccessResult? content = null;
                if (maximumContentDocumentsPerRoot > 0)
                {
                    content = await _contentIndexService.GetOrBuildAsync(
                        root,
                        maximumContentDocumentsPerRoot,
                        progress,
                        cancellationToken,
                        forceRefresh: forceContentRefresh && IsNetworkRoot(root),
                        preferredFiles: OrderPreferredContentItems(
                            metadata.Snapshot.Items,
                            priorityIntent));
                    contentDocuments = SaturatingAdd(
                        contentDocuments,
                        content.Snapshot.Documents.Count);
                }

                if (content is not null &&
                    maximumNewSemanticDocumentsPerRoot > 0 &&
                    _semanticIndexService is not null &&
                    _embeddingService?.IsAvailable == true)
                {
                    var semanticSource = BuildSemanticDocuments(
                        root,
                        metadata.Snapshot.Items,
                        content.Snapshot.Documents,
                        priorityIntent);
                    var semantic = await _semanticIndexService.WarmUpAsync(
                        root,
                        semanticSource,
                        maximumNewSemanticDocumentsPerRoot,
                        progress,
                        cancellationToken);
                    semanticDocuments = SaturatingAdd(
                        semanticDocuments,
                        semantic.IndexedDocuments);
                    newlyIndexedSemanticDocuments = SaturatingAdd(
                        newlyIndexedSemanticDocuments,
                        semantic.NewlyIndexedDocuments);
                }

                if (maximumNewVisualDocumentsPerRoot > 0 &&
                    _visualIndexService is not null &&
                    _visualEmbeddingService?.IsAvailable == true)
                {
                    var visual = await _visualIndexService.WarmUpAsync(
                        root,
                        metadata.Snapshot.Items,
                        maximumNewVisualDocumentsPerRoot,
                        progress,
                        cancellationToken);
                    visualDocuments = SaturatingAdd(
                        visualDocuments,
                        visual.IndexedDocuments);
                    newlyIndexedVisualDocuments = SaturatingAdd(
                        newlyIndexedVisualDocuments,
                        visual.NewlyIndexedDocuments);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    $"자동 색인에서 위치를 건너뜁니다: {root} · " +
                    exception.Message);
            }
        }

        return new IndexWarmupResult(
            roots.Length,
            indexedItems,
            contentDocuments,
            semanticDocuments,
            newlyIndexedSemanticDocuments,
            visualDocuments,
            newlyIndexedVisualDocuments);
    }

    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var intent =
            request.Intent ??
            SearchQueryInterpreter.Interpret(request.Query);
        if (!intent.HasCriteria)
        {
            return EmptyResponse(intent.Summary, request.IndexingMode);
        }

        var existingOnly =
            request.IndexingMode == SearchIndexingMode.ExistingIndexOnly;
        var roots = (existingOnly
                ? request.Roots
                    .Select(NormalizeRootWithoutProbe)
                    .Where(root => !string.IsNullOrWhiteSpace(root))
                : request.Roots
                    .Where(Directory.Exists)
                    .Select(Path.GetFullPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var maximumResults = Math.Max(1, request.MaximumResults);
        var maximumIndexedItems = Math.Max(1, request.MaximumScannedItems);
        var perRootMaximumItems = Math.Max(
            1,
            maximumIndexedItems / Math.Max(1, roots.Length));
        var maximumContentDocuments = Math.Max(
            1,
            request.MaximumContentDocuments);
        var perRootMaximumDocuments = Math.Max(
            1,
            maximumContentDocuments / Math.Max(1, roots.Length));
        var shouldSearchVisually = ShouldSearchVisually(intent);
        var precisionFirst = IsPrecisionFirst(intent, shouldSearchVisually);

        var indexedItems = new List<IndexedFileRecord>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var truncatedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedCachedIndex = true;
        var indexWasTruncated = false;

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.AllowTargetedScan && IsNetworkRoot(root))
            {
                scanRoots.Add(root);
            }

            IndexAccessResult? index = existingOnly
                ? await _indexService.TryGetAvailableAsync(
                    root,
                    cancellationToken)
                : await _indexService.GetOrBuildAsync(
                    root,
                    perRootMaximumItems,
                    progress,
                    cancellationToken);
            if (index is null)
            {
                if (request.AllowTargetedScan)
                {
                    scanRoots.Add(root);
                }

                usedCachedIndex = false;
                continue;
            }

            usedCachedIndex &= index.UsedCache;
            var truncated = !index.Snapshot.IsComplete;
            indexWasTruncated |= truncated;
            if (truncated)
            {
                truncatedRoots.Add(root);
            }

            foreach (var item in index.Snapshot.Items.Take(perRootMaximumItems))
            {
                if (!SearchVisibilityPolicy.IsVisiblePathByName(item.FullPath))
                {
                    continue;
                }

                if (indexedItems.Count >= maximumIndexedItems)
                {
                    indexWasTruncated = true;
                    truncatedRoots.Add(root);
                    break;
                }

                if (seenPaths.Add(item.FullPath))
                {
                    indexedItems.Add(item);
                }
            }
        }

        var candidates = await Task.Run(
            () => SearchRankingService.FindCandidates(
                intent,
                indexedItems,
                maximumResults,
                progress,
                cancellationToken),
            cancellationToken);
        var usedTargetedScan = false;
        var targetedScannedItems = 0;

        foreach (var root in truncatedRoots)
        {
            if (request.AllowTargetedScan &&
                SupportsTargetedScan(intent) &&
                (shouldSearchVisually ||
                 NeedsTargetedScan(intent, candidates, maximumResults)))
            {
                scanRoots.Add(root);
            }
        }

        foreach (var root in scanRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            usedTargetedScan = true;
            var targeted = await _targetedSearchService.FindAsync(
                root,
                intent,
                maximumResults,
                includeVisualTypeFallback: false,
                progress,
                (batch, scannedItems, matchedItems) =>
                {
                    var partialResults = batch
                        .Select(candidate => CreateFastSearchResult(
                            candidate,
                            intent.MetadataTermCount,
                            intent.RankingProfile))
                        .ToArray();
                    if (partialResults.Length == 0)
                    {
                        return;
                    }

                    progress?.Report(new SearchProgress(
                        scannedItems,
                        matchedItems,
                        root,
                        SearchPhase.TargetedScanning,
                        partialResults));
                },
                request.MaximumTargetedScanItems,
                cancellationToken);
            targetedScannedItems = SaturatingAdd(
                targetedScannedItems,
                targeted.ScannedItems);
            foreach (var record in targeted.Records)
            {
                if (seenPaths.Add(record.FullPath))
                {
                    indexedItems.Add(record);
                }
            }
        }

        if (scanRoots.Count > 0)
        {
            candidates = await Task.Run(
                () => SearchRankingService.FindCandidates(
                    intent,
                    indexedItems,
                    maximumResults,
                    progress: null,
                    cancellationToken),
                cancellationToken);
        }

        var contentDocuments = new List<ContentDocumentRecord>();
        var contentRoots = new List<ContentRootScope>();
        var contentIndexedDocuments = 0;
        var usedContentSearch = ShouldSearchContent(intent);
        if (usedContentSearch)
        {
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ContentIndexAccessResult? content = existingOnly
                    ? await _contentIndexService.TryGetAvailableAsync(
                        root,
                        cancellationToken)
                    : await _contentIndexService.GetOrBuildAsync(
                        root,
                        perRootMaximumDocuments,
                        progress,
                        cancellationToken,
                        preferredFiles: OrderPreferredContentItems(
                            indexedItems.Where(item =>
                                SearchPathPriority.IsInsideRoot(
                                    item.FullPath,
                                    root)),
                            intent));
                if (content is null)
                {
                    contentRoots.Add(new ContentRootScope(root, []));
                    continue;
                }

                contentIndexedDocuments = SaturatingAdd(
                    contentIndexedDocuments,
                    content.Snapshot.Documents.Count);
                indexWasTruncated |= !content.Snapshot.IsComplete;
                var rootDocuments = content.Snapshot.Documents
                    .Where(document =>
                        SearchVisibilityPolicy.IsVisiblePathByName(
                            document.FullPath))
                    .Take(perRootMaximumDocuments)
                    .ToArray();
                contentDocuments.AddRange(rootDocuments);
                contentRoots.Add(new ContentRootScope(root, rootDocuments));
            }
        }

        var contentCandidates = usedContentSearch
            ? await Task.Run(
                () => ContentSearchService.FindCandidates(
                    intent,
                    contentDocuments,
                    maximumResults,
                    progress,
                    cancellationToken),
                cancellationToken)
            : [];

        var semanticCandidates = new List<SemanticSearchCandidate>();
        var semanticIndexedDocuments = 0;
        var semanticTotalDocuments = 0;
        var usedSemanticSearch = false;
        var aiModelReady = _embeddingService?.IsAvailable == true;
        if (request.IncludeAiCandidates &&
            aiModelReady &&
            _semanticIndexService is not null &&
            intent.Terms.Count > 0 &&
            !intent.DirectoryOnly &&
            !shouldSearchVisually)
        {
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var rootContent = contentRoots
                        .FirstOrDefault(scope => string.Equals(
                            scope.Root,
                            root,
                            StringComparison.OrdinalIgnoreCase))
                        ?.Documents ?? [];
                    var semanticSource = BuildSemanticDocuments(
                        root,
                        indexedItems,
                        rootContent,
                        intent);
                    var semantic = await _semanticIndexService.FindCandidatesAsync(
                        root,
                        intent,
                        semanticSource,
                        maximumResults,
                        maximumNewDocuments: 0,
                        progress,
                        cancellationToken);
                    usedSemanticSearch |= semantic.IndexedDocuments > 0;
                    semanticIndexedDocuments = SaturatingAdd(
                        semanticIndexedDocuments,
                        semantic.IndexedDocuments);
                    semanticTotalDocuments = SaturatingAdd(
                        semanticTotalDocuments,
                        semantic.TotalDocuments);
                    semanticCandidates.AddRange(semantic.Candidates);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AppLog.Warning(
                        "로컬 AI 의미 검색을 건너뛰고 정확 검색으로 계속합니다. " +
                        exception.Message);
                }
            }
        }

        var visualCandidates = new List<VisualSearchCandidate>();
        var visualIndexedDocuments = 0;
        var visualTotalDocuments = 0;
        var usedVisualSearch = false;
        var visualModelReady = _visualEmbeddingService?.IsAvailable == true;
        if (request.IncludeAiCandidates &&
            visualModelReady &&
            _visualIndexService is not null &&
            intent.Terms.Count > 0 &&
            !intent.DirectoryOnly &&
            shouldSearchVisually)
        {
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var visual = await _visualIndexService.FindCandidatesAsync(
                        root,
                        intent,
                        indexedItems,
                        maximumResults,
                        maximumNewDocuments:
                            request.MaximumNewVisualDocumentsPerRoot,
                        progress,
                        cancellationToken);
                    usedVisualSearch |= visual.IndexedDocuments > 0;
                    visualIndexedDocuments = SaturatingAdd(
                        visualIndexedDocuments,
                        visual.IndexedDocuments);
                    visualTotalDocuments = SaturatingAdd(
                        visualTotalDocuments,
                        visual.TotalDocuments);
                    visualCandidates.AddRange(visual.Candidates);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AppLog.Warning(
                        "시각 AI 검색을 건너뛰고 파일명·OCR 검색으로 계속합니다. " +
                        exception.Message);
                }
            }
        }

        var merged = new Dictionary<string, MergedSearchCandidate>(
            StringComparer.OrdinalIgnoreCase);
        var hasStrongLexicalEvidence =
            candidates.Any(candidate =>
                candidate.NameMatchCount >=
                Math.Max(1, intent.MetadataTermCount)) ||
            contentCandidates.Any(candidate =>
                candidate.Coverage >= 0.99d);
        for (var rank = 0; rank < candidates.Count; rank++)
        {
            var candidate = candidates[rank];
            var evidence = candidate.NameMatchCount >=
                           Math.Max(1, intent.MetadataTermCount)
                ? SearchEvidenceKind.ExactName
                : candidate.NameMatchCount > 0
                    ? SearchEvidenceKind.NameCandidate
                    : candidate.TypeMatchCount > 0
                        ? SearchEvidenceKind.Metadata
                    : SearchEvidenceKind.Path;
            MergeCandidate(
                merged,
                candidate.Record.FullPath,
                candidate.Record.DirectoryPath,
                candidate.Record.CreatedUtc,
                candidate.Record.ModifiedUtc,
                candidate.Record.SizeBytes,
                candidate.Score,
                candidate.Reason,
                evidence,
                candidate.NameMatchCount,
                candidate.PathMatchCount,
                candidate.TypeMatchCount,
                intent.MetadataTermCount,
                contentCoverage: null,
                aiSimilarity: null,
                visualSimilarity: null);
            AddRankFusionEvidence(
                merged[candidate.Record.FullPath],
                rank);
        }

        for (var rank = 0; rank < contentCandidates.Count; rank++)
        {
            var candidate = contentCandidates[rank];
            if (!MatchesStructuredLocation(
                    intent,
                    candidate.Document.FullPath,
                    candidate.Document.DirectoryPath))
            {
                continue;
            }

            MergeCandidate(
                merged,
                candidate.Document.FullPath,
                candidate.Document.DirectoryPath,
                candidate.Document.CreatedUtc,
                candidate.Document.ModifiedUtc,
                candidate.Document.SizeBytes,
                candidate.Score + 340d,
                candidate.Reason,
                candidate.MetadataMatchedTermCount > 0
                    ? SearchEvidenceKind.Combined
                    : SearchEvidenceKind.Content,
                candidate.MetadataMatchedTermCount,
                0,
                0,
                intent.Terms.Count,
                contentCoverage: candidate.Coverage,
                aiSimilarity: null,
                visualSimilarity: null);
            AddRankFusionEvidence(
                merged[candidate.Document.FullPath],
                rank);
        }

        var rankedSemanticCandidates = semanticCandidates
            .OrderByDescending(candidate => candidate.Similarity)
            .ThenByDescending(candidate => candidate.Document.ModifiedUtc)
            .ToArray();
        for (var rank = 0; rank < rankedSemanticCandidates.Length; rank++)
        {
            var candidate = rankedSemanticCandidates[rank];
            if (!MatchesStructuredLocation(
                    intent,
                    candidate.Document.FullPath,
                    candidate.Document.DirectoryPath))
            {
                continue;
            }

            if (precisionFirst &&
                hasStrongLexicalEvidence &&
                !merged.ContainsKey(candidate.Document.FullPath))
            {
                continue;
            }

            MergeCandidate(
                merged,
                candidate.Document.FullPath,
                candidate.Document.DirectoryPath,
                candidate.Document.CreatedUtc,
                candidate.Document.ModifiedUtc,
                candidate.Document.SizeBytes,
                precisionFirst
                    ? 18d + Math.Max(0d, candidate.Similarity - 0.76d) * 80d
                    : 90d + Math.Max(0d, candidate.Similarity - 0.76d) * 260d,
                candidate.Reason,
                SearchEvidenceKind.SemanticCandidate,
                0,
                0,
                0,
                intent.Terms.Count,
                contentCoverage: null,
                aiSimilarity: candidate.Similarity,
                visualSimilarity: null);
            AddRankFusionEvidence(
                merged[candidate.Document.FullPath],
                rank);
        }

        var rankedVisualCandidates = visualCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Similarity)
            .ThenByDescending(candidate => candidate.Document.ModifiedUtc)
            .ToArray();
        for (var rank = 0; rank < rankedVisualCandidates.Length; rank++)
        {
            var candidate = rankedVisualCandidates[rank];
            if (!MatchesStructuredLocation(
                    intent,
                    candidate.Document.FullPath,
                    candidate.Document.DirectoryPath))
            {
                continue;
            }

            MergeCandidate(
                merged,
                candidate.Document.FullPath,
                candidate.Document.DirectoryPath,
                candidate.Document.CreatedUtc,
                candidate.Document.ModifiedUtc,
                candidate.Document.SizeBytes,
                candidate.Score,
                candidate.Reason,
                SearchEvidenceKind.VisualCandidate,
                0,
                0,
                0,
                intent.Terms.Count,
                contentCoverage: null,
                aiSimilarity: null,
                visualSimilarity: candidate.Similarity);
            AddRankFusionEvidence(
                merged[candidate.Document.FullPath],
                rank);
        }

        var ordered = merged.Values
            .OrderByDescending(candidate =>
                GetEvidenceGuardrail(
                    candidate,
                    shouldSearchVisually))
            .ThenByDescending(candidate =>
                GetPrimaryPreferenceScore(
                    candidate,
                    intent.RankingProfile))
            .ThenByDescending(candidate =>
                GetHybridRankScore(
                    candidate,
                    shouldSearchVisually,
                    intent.RankingProfile))
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.ModifiedUtc)
            .ThenBy(
                candidate => Path.GetFileName(candidate.FullPath),
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var bestSemantic = ordered
            .Where(candidate => candidate.AiSimilarity is not null)
            .Select(candidate => candidate.AiSimilarity!.Value)
            .DefaultIfEmpty(0d)
            .Max();
        var bestVisual = ordered
            .Where(candidate => candidate.VisualSimilarity is not null)
            .Select(candidate => candidate.VisualSimilarity!.Value)
            .DefaultIfEmpty(0d)
            .Max();
        var results = new List<SearchResult>(maximumResults);
        foreach (var candidate in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = _fileSystemService.CreateEntry(candidate.FullPath);
            if (entry is null)
            {
                continue;
            }

            var evidence = ResolvePrimaryEvidence(
                candidate,
                shouldSearchVisually);
            results.Add(new SearchResult
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                DirectoryPath = Path.GetDirectoryName(entry.FullPath) ??
                                candidate.DirectoryPath,
                TypeDisplay = entry.TypeDisplay,
                ModifiedDisplay = entry.ModifiedDisplay,
                CreatedUtc = candidate.CreatedUtc,
                ModifiedUtc = candidate.ModifiedUtc,
                SizeBytes = candidate.SizeBytes,
                Reason = BuildDisplayReason(candidate, intent.RankingProfile),
                IconGlyph = entry.IconGlyph,
                IconImage = entry.IconImage,
                Score = GetResultRankScore(
                    candidate,
                    shouldSearchVisually,
                    intent.RankingProfile),
                MatchPercent = CalculateMatchPercent(
                    candidate,
                    evidence,
                    bestSemantic,
                    bestVisual),
                EvidenceKind = evidence,
                WasAiAnalyzed = candidate.AiSimilarity is not null ||
                                candidate.VisualSimilarity is not null,
                WasVisualAnalyzed = candidate.VisualSimilarity is not null,
                IsDirectory = entry.IsDirectory
            });
            if (results.Count >= maximumResults)
            {
                break;
            }
        }

        return new SearchResponse(
            results,
            new SearchDiagnostics(
                IndexedItems: indexedItems.Count,
                TargetedScannedItems: targetedScannedItems,
                ContentIndexedDocuments: contentIndexedDocuments,
                OcrIndexedDocuments: contentDocuments.Count(document =>
                    document.Source is
                        DocumentContentSource.ImageOcr or
                        DocumentContentSource.PdfOcr),
                SemanticIndexedDocuments: semanticIndexedDocuments,
                SemanticTotalDocuments: semanticTotalDocuments,
                VisualIndexedDocuments: visualIndexedDocuments,
                VisualTotalDocuments: visualTotalDocuments,
                UsedCachedIndex: usedCachedIndex,
                UsedTargetedScan: usedTargetedScan,
                UsedContentSearch: usedContentSearch && contentDocuments.Count > 0,
                UsedSemanticSearch: usedSemanticSearch,
                UsedVisualSearch: usedVisualSearch,
                AiModelReady: aiModelReady,
                VisualModelReady: visualModelReady,
                IndexWasTruncated: indexWasTruncated,
                UsedExistingIndexOnly: existingOnly,
                IntentSummary: precisionFirst
                    ? intent.Summary + " · 정확 일치 우선"
                    : intent.Summary));
    }

    private static SearchResult CreateFastSearchResult(
        SearchCandidate candidate,
        int totalTerms,
        SearchRankingProfile rankingProfile)
    {
        var evidence = candidate.NameMatchCount >= Math.Max(1, totalTerms)
            ? SearchEvidenceKind.ExactName
            : candidate.NameMatchCount > 0
                ? SearchEvidenceKind.NameCandidate
                : candidate.TypeMatchCount > 0
                    ? SearchEvidenceKind.Metadata
                : SearchEvidenceKind.Path;
        var matchPercent = evidence switch
        {
            SearchEvidenceKind.ExactName => 99d,
            SearchEvidenceKind.NameCandidate => Math.Round(
                62d +
                Math.Clamp(
                    totalTerms <= 0
                        ? 0.5d
                        : (double)candidate.NameMatchCount / totalTerms,
                    0d,
                    1d) * 24d,
                MidpointRounding.AwayFromZero),
            SearchEvidenceKind.Metadata => Math.Round(
                58d +
                Math.Clamp(
                    totalTerms <= 0
                        ? 0.5d
                        : (double)candidate.MatchedTermCount / totalTerms,
                    0d,
                    1d) * 26d,
                MidpointRounding.AwayFromZero),
            _ => 45d
        };

        return new SearchResult
        {
            Name = candidate.Record.Name,
            FullPath = candidate.Record.FullPath,
            DirectoryPath = candidate.Record.DirectoryPath,
            TypeDisplay = candidate.Record.IsDirectory
                ? "파일 폴더"
                : FileTypeCatalog.GetTypeDisplay(candidate.Record.Extension),
            ModifiedDisplay = candidate.Record.ModifiedUtc
                .ToLocalTime()
                .ToString("yyyy-MM-dd  HH:mm"),
            CreatedUtc = candidate.Record.CreatedUtc,
            ModifiedUtc = candidate.Record.ModifiedUtc,
            SizeBytes = candidate.Record.SizeBytes,
            Reason = AppendPreferenceReason(
                candidate.Reason,
                SearchRankingPreferenceService.BuildAppliedReason(
                    rankingProfile)),
            IconGlyph = GetFastIconGlyph(
                candidate.Record.IsDirectory,
                candidate.Record.Extension),
            Score = candidate.RankingScore,
            MatchPercent = matchPercent,
            EvidenceKind = evidence,
            IsDirectory = candidate.Record.IsDirectory
        };
    }

    private static string AppendPreferenceReason(
        string reason,
        string preferenceReason) =>
        string.IsNullOrWhiteSpace(preferenceReason)
            ? reason
            : $"{reason} {preferenceReason}".Trim();

    private static string GetFastIconGlyph(
        bool isDirectory,
        string extension)
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


    private static string NormalizeRootWithoutProbe(string root)
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

    private static bool IsNetworkRoot(string root) =>
        root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase);

    private static SearchResponse EmptyResponse(
        string summary,
        SearchIndexingMode indexingMode) =>
        new(
            [],
            new SearchDiagnostics(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                indexingMode == SearchIndexingMode.ExistingIndexOnly,
                summary));

    private static bool IsPrecisionFirst(
        SearchIntent intent,
        bool visualSearchRequested) =>
        !visualSearchRequested &&
        intent.MetadataTermCount == 1 &&
        intent.MetadataTerms[0].Original.Length >= 2;

    private static bool SupportsTargetedScan(SearchIntent intent) =>
        intent.RequestedExtensions.Count > 0 ||
        intent.Categories.Count > 0 ||
        intent.MetadataTermCount > 0 ||
        intent.FloorReferences.Count > 0 ||
        intent.AttributePredicates.Count > 0;

    private static bool NeedsTargetedScan(
        SearchIntent intent,
        IReadOnlyList<SearchCandidate> candidates,
        int maximumResults)
    {
        if (candidates.Count < maximumResults &&
            (intent.RequestedExtensions.Count > 0 ||
             intent.Categories.Count > 0))
        {
            return true;
        }

        if (intent.MetadataTermCount == 0)
        {
            return intent.AttributePredicates.Count > 0 &&
                   candidates.Count < maximumResults;
        }

        if (candidates.Count == 0)
        {
            return true;
        }

        return intent.MetadataTermCount >= 2 &&
               !candidates.Any(candidate =>
                   candidate.MatchedTermCount == intent.MetadataTermCount);
    }

    private static bool ShouldSearchContent(SearchIntent intent) =>
        !intent.DirectoryOnly &&
        (intent.Terms.Count > 0 ||
         intent.RequiresContentAttributes);

    private static bool MatchesStructuredLocation(
        SearchIntent intent,
        string fullPath,
        string directoryPath) =>
        SearchTextAnalyzer.ContainsAllFloorReferences(
            intent.FloorReferences,
            $"{Path.GetFileName(fullPath)} {directoryPath}");

    private static bool ShouldSearchVisually(SearchIntent intent) =>
        intent.Categories.Contains(FileCategory.Image) ||
        intent.RequestedExtensions.Any(extension =>
            FileTypeCatalog.GetCategory(extension) == FileCategory.Image ||
            extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) ||
        VisualQueryPromptBuilder.HasKnownVisualConcept(intent.OriginalQuery);

    private static IReadOnlyList<IndexedFileRecord> OrderPreferredContentItems(
        IEnumerable<IndexedFileRecord> items,
        SearchIntent? priorityIntent)
    {
        return items
            .Where(item =>
                !item.IsDirectory &&
                IsSpreadsheet(item.Extension))
            .Select(item => new
            {
                Item = item,
                Candidate = priorityIntent is null
                    ? null
                    : SearchRankingService.ScoreCandidate(
                        priorityIntent,
                        item)
            })
            .OrderByDescending(value => value.Candidate is not null)
            .ThenByDescending(value => value.Candidate?.Score ?? 0d)
            .ThenByDescending(value =>
                SearchPathPriority.GetPathPriority(
                    value.Item.DirectoryPath))
            .ThenByDescending(value => value.Item.ModifiedUtc)
            .Select(value => value.Item)
            .ToArray();
    }

    private static bool IsSpreadsheet(string extension) =>
        extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsb", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xltx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xltm", StringComparison.OrdinalIgnoreCase);

    private static bool HasPreferredContentItems(
        IReadOnlyList<ContentDocumentRecord> documents,
        IReadOnlyList<IndexedFileRecord> preferredItems)
    {
        if (preferredItems.Count == 0)
        {
            return true;
        }

        var indexed = documents
            .Select(document => document.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return preferredItems
            .Take(32)
            .All(item => indexed.Contains(item.FullPath));
    }

    private static IReadOnlyList<ContentDocumentRecord> BuildSemanticDocuments(
        string root,
        IReadOnlyList<IndexedFileRecord> indexedItems,
        IReadOnlyList<ContentDocumentRecord> contentDocuments,
        SearchIntent? priorityIntent)
    {
        var documents = contentDocuments
            .Where(document =>
                !string.IsNullOrWhiteSpace(document.Text) &&
                SearchPathPriority.IsInsideRoot(document.FullPath, root))
            .GroupBy(
                document => document.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var metadataCapacity = Math.Max(
            0,
            MaximumMetadataSemanticDocumentsPerRoot - documents.Count);
        foreach (var value in indexedItems
                     .Where(item =>
                         !item.IsDirectory &&
                         !documents.ContainsKey(item.FullPath) &&
                         SearchPathPriority.IsInsideRoot(
                             item.FullPath,
                             root))
                     .Select(item => new
                     {
                         Item = item,
                         Candidate = priorityIntent is null
                             ? null
                             : SearchRankingService.ScoreCandidate(
                                 priorityIntent,
                                 item)
                     })
                     .OrderByDescending(value =>
                         value.Candidate is not null)
                     .ThenByDescending(value =>
                         value.Candidate?.MatchedTermCount ?? 0)
                     .ThenByDescending(value =>
                         value.Candidate?.Score ?? 0d)
                     .ThenByDescending(value =>
                         SearchPathPriority.GetPathPriority(
                             value.Item.DirectoryPath))
                     .ThenByDescending(value => value.Item.ModifiedUtc)
                     .Take(metadataCapacity))
        {
            var item = value.Item;
            documents[item.FullPath] = new ContentDocumentRecord
            {
                Name = item.Name,
                FullPath = item.FullPath,
                DirectoryPath = item.DirectoryPath,
                Extension = item.Extension,
                SizeBytes = item.SizeBytes,
                CreatedUtc = item.CreatedUtc,
                ModifiedUtc = item.ModifiedUtc,
                Text = FileMetadataDescriptor.BuildSemanticText(
                    root,
                    item),
                WasTruncated = false,
                Source = DocumentContentSource.PlainText,
                AnalyzedPages = 0
            };
        }

        return documents.Values.ToArray();
    }

    private static void MergeCandidate(
        IDictionary<string, MergedSearchCandidate> candidates,
        string fullPath,
        string directoryPath,
        DateTime createdUtc,
        DateTime modifiedUtc,
        long? sizeBytes,
        double score,
        string reason,
        SearchEvidenceKind evidence,
        int nameMatchCount,
        int pathMatchCount,
        int typeMatchCount,
        int totalTerms,
        double? contentCoverage,
        double? aiSimilarity,
        double? visualSimilarity)
    {
        if (!candidates.TryGetValue(fullPath, out var candidate))
        {
            candidate = new MergedSearchCandidate(
                fullPath,
                directoryPath,
                createdUtc,
                modifiedUtc,
                sizeBytes,
                score,
                totalTerms);
            candidates[fullPath] = candidate;
        }
        else
        {
            candidate.Score += score * 0.55d;
            if (modifiedUtc > candidate.ModifiedUtc)
            {
                candidate.ModifiedUtc = modifiedUtc;
            }
            if (candidate.CreatedUtc == default ||
                createdUtc != default && createdUtc < candidate.CreatedUtc)
            {
                candidate.CreatedUtc = createdUtc;
            }
            candidate.SizeBytes ??= sizeBytes;
        }

        candidate.Evidence.Add(evidence);
        candidate.NameMatchCount = Math.Max(
            candidate.NameMatchCount,
            nameMatchCount);
        candidate.PathMatchCount = Math.Max(
            candidate.PathMatchCount,
            pathMatchCount);
        candidate.TypeMatchCount = Math.Max(
            candidate.TypeMatchCount,
            typeMatchCount);
        candidate.ContentCoverage = MaxNullable(
            candidate.ContentCoverage,
            contentCoverage);
        candidate.AiSimilarity = MaxNullable(
            candidate.AiSimilarity,
            aiSimilarity);
        candidate.VisualSimilarity = MaxNullable(
            candidate.VisualSimilarity,
            visualSimilarity);
        candidate.Reasons.Add(reason);
    }

    private static double? MaxNullable(double? left, double? right)
    {
        if (left is null)
        {
            return right;
        }
        if (right is null)
        {
            return left;
        }
        return Math.Max(left.Value, right.Value);
    }

    private static void AddRankFusionEvidence(
        MergedSearchCandidate candidate,
        int zeroBasedRank,
        double weight = 1d)
    {
        const double rankConstant = 60d;
        candidate.RankFusionScore +=
            weight / (rankConstant + zeroBasedRank + 1d);
    }

    private static double GetHybridRankScore(
        MergedSearchCandidate candidate,
        bool preferVisual,
        SearchRankingProfile rankingProfile) =>
        GetEvidencePriority(candidate, preferVisual) +
        candidate.RankFusionScore * 30_000d +
        SearchRankingPreferenceService.CalculateAdjustment(
            rankingProfile,
            GetRankingSignals(candidate));

    private static double GetPrimaryPreferenceScore(
        MergedSearchCandidate candidate,
        SearchRankingProfile rankingProfile) =>
        rankingProfile.HasPrimaryPreference
            ? SearchRankingPreferenceService.CalculatePreferenceScore(
                rankingProfile,
                GetRankingSignals(candidate),
                primaryOnly: true)
            : 0d;

    private static double GetResultRankScore(
        MergedSearchCandidate candidate,
        bool preferVisual,
        SearchRankingProfile rankingProfile) =>
        GetEvidenceGuardrail(candidate, preferVisual) * 1_000_000d +
        GetPrimaryPreferenceScore(candidate, rankingProfile) * 100_000d +
        GetHybridRankScore(candidate, preferVisual, rankingProfile);

    private static SearchRankingSignals GetRankingSignals(
        MergedSearchCandidate candidate)
    {
        var termCount = Math.Max(1, candidate.TotalTerms);
        return new SearchRankingSignals(
            candidate.CreatedUtc,
            candidate.ModifiedUtc,
            candidate.SizeBytes,
            Math.Clamp(
                candidate.NameMatchCount / (double)termCount,
                0d,
                1d),
            Math.Clamp(
                candidate.PathMatchCount / (double)termCount,
                0d,
                1d),
            Math.Clamp(
                candidate.TypeMatchCount / (double)termCount,
                0d,
                1d),
            Math.Clamp(candidate.ContentCoverage ?? 0d, 0d, 1d),
            NormalizeSemanticSimilarity(candidate.AiSimilarity));
    }

    private static double NormalizeSemanticSimilarity(double? similarity) =>
        similarity is null
            ? 0d
            : Math.Clamp((similarity.Value - 0.68d) / 0.24d, 0d, 1d);

    private static int GetEvidenceGuardrail(
        MergedSearchCandidate candidate,
        bool preferVisual)
    {
        var evidence = ResolvePrimaryEvidence(candidate, preferVisual);
        return evidence switch
        {
            SearchEvidenceKind.ExactName => 3,
            SearchEvidenceKind.Combined => 2,
            SearchEvidenceKind.VisualCandidate when preferVisual => 2,
            _ => 1
        };
    }

    private static int GetEvidencePriority(
        MergedSearchCandidate candidate,
        bool preferVisual)
    {
        var evidence = ResolvePrimaryEvidence(candidate, preferVisual);
        return evidence switch
        {
            SearchEvidenceKind.ExactName => 800,
            SearchEvidenceKind.Combined => 760,
            SearchEvidenceKind.VisualCandidate when preferVisual => 740,
            SearchEvidenceKind.NameCandidate => 700,
            SearchEvidenceKind.Content => 620,
            SearchEvidenceKind.Path => 540,
            SearchEvidenceKind.SemanticCandidate => 450,
            SearchEvidenceKind.VisualCandidate => 320,
            _ => 100
        } + candidate.Evidence.Count;
    }

    private static SearchEvidenceKind ResolvePrimaryEvidence(
        MergedSearchCandidate candidate,
        bool preferVisual = false)
    {
        if (candidate.Evidence.Contains(SearchEvidenceKind.ExactName))
        {
            return SearchEvidenceKind.ExactName;
        }
        if (preferVisual &&
            candidate.Evidence.Contains(
                SearchEvidenceKind.VisualCandidate))
        {
            return SearchEvidenceKind.VisualCandidate;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.Combined))
        {
            return SearchEvidenceKind.Combined;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.Content) &&
            candidate.Evidence.Contains(SearchEvidenceKind.NameCandidate) &&
            candidate.ContentCoverage >= 0.99d)
        {
            return SearchEvidenceKind.Combined;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.NameCandidate))
        {
            return SearchEvidenceKind.NameCandidate;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.Content))
        {
            return SearchEvidenceKind.Content;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.Path))
        {
            return SearchEvidenceKind.Path;
        }
        if (candidate.Evidence.Contains(
                SearchEvidenceKind.SemanticCandidate))
        {
            return SearchEvidenceKind.SemanticCandidate;
        }
        if (candidate.Evidence.Contains(SearchEvidenceKind.VisualCandidate))
        {
            return SearchEvidenceKind.VisualCandidate;
        }
        return SearchEvidenceKind.Metadata;
    }

    private static string BuildDisplayReason(
        MergedSearchCandidate candidate,
        SearchRankingProfile rankingProfile)
    {
        static int Priority(string reason)
        {
            if (reason.Contains("파일명", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }
            if (reason.Contains("본문", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("OCR", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("엑셀", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }
            if (reason.Contains("경로", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }
            if (reason.Contains("시각", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("SigLIP", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }
            if (reason.Contains("Multilingual E5", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 1;
        }

        var baseReason = string.Join(
            " ",
            candidate.Reasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(Priority)
                .Take(3));
        var preferenceReason =
            SearchRankingPreferenceService.BuildAppliedReason(
                rankingProfile);
        return string.IsNullOrWhiteSpace(preferenceReason)
            ? baseReason
            : $"{baseReason} {preferenceReason}".Trim();
    }

    private static double CalculateMatchPercent(
        MergedSearchCandidate candidate,
        SearchEvidenceKind evidence,
        double bestSemantic,
        double bestVisual)
    {
        return evidence switch
        {
            SearchEvidenceKind.ExactName =>
                candidate.TotalTerms > 0 &&
                candidate.NameMatchCount >= candidate.TotalTerms
                    ? 99d
                    : 92d,
            SearchEvidenceKind.Combined => Math.Round(
                92d +
                Math.Clamp(
                    candidate.ContentCoverage ?? 0.5d,
                    0.5d,
                    1d) * 7d,
                MidpointRounding.AwayFromZero),
            SearchEvidenceKind.NameCandidate => Math.Round(
                62d +
                Math.Clamp(
                    candidate.TotalTerms <= 0
                        ? 0.5d
                        : (double)candidate.NameMatchCount / candidate.TotalTerms,
                    0d,
                    1d) * 24d,
                MidpointRounding.AwayFromZero),
            SearchEvidenceKind.Content =>
                candidate.Evidence.Contains(SearchEvidenceKind.ExactName)
                    ? 99d
                    : Math.Round(
                        72d +
                        Math.Clamp(
                            candidate.ContentCoverage ?? 0.5d,
                            0.5d,
                            1d) * 24d,
                        MidpointRounding.AwayFromZero),
            SearchEvidenceKind.Path => 45d,
            SearchEvidenceKind.VisualCandidate => CalculateVisualMatchPercent(
                candidate.VisualSimilarity,
                bestVisual),
            SearchEvidenceKind.SemanticCandidate => Math.Round(
                45d + RelativeConfidence(
                    candidate.AiSimilarity,
                    bestSemantic) * 27d,
                MidpointRounding.AwayFromZero),
            _ => 70d
        };
    }

    private static double RelativeConfidence(
        double? similarity,
        double bestSimilarity)
    {
        if (similarity is null || bestSimilarity <= double.Epsilon)
        {
            return 0d;
        }

        var distance = Math.Max(0d, bestSimilarity - similarity.Value);
        return Math.Clamp(1d - distance / 0.08d, 0d, 1d);
    }

    private static double CalculateVisualMatchPercent(
        double? similarity,
        double bestSimilarity)
    {
        if (similarity is null)
        {
            return 35d;
        }

        // Cross-modal cosine scores are much lower than same-modality scores.
        // Absolute confidence carries most of the displayed percentage so the
        // best item in a weak result set can no longer appear highly reliable.
        var absoluteConfidence = Math.Clamp(
            (similarity.Value - 0.04d) / 0.26d,
            0d,
            1d);
        var relativeConfidence = RelativeConfidence(
            similarity,
            bestSimilarity);
        return Math.Round(
            Math.Clamp(
                35d + absoluteConfidence * 55d +
                relativeConfidence * 5d,
                35d,
                95d),
            MidpointRounding.AwayFromZero);
    }

    private static int SaturatingAdd(int left, int right) =>
        (int)Math.Min(int.MaxValue, (long)left + right);

    private sealed class MergedSearchCandidate
    {
        public MergedSearchCandidate(
            string fullPath,
            string directoryPath,
            DateTime createdUtc,
            DateTime modifiedUtc,
            long? sizeBytes,
            double score,
            int totalTerms)
        {
            FullPath = fullPath;
            DirectoryPath = directoryPath;
            CreatedUtc = createdUtc;
            ModifiedUtc = modifiedUtc;
            SizeBytes = sizeBytes;
            Score = score;
            TotalTerms = totalTerms;
        }

        public string FullPath { get; }
        public string DirectoryPath { get; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public long? SizeBytes { get; set; }
        public double Score { get; set; }
        public int TotalTerms { get; }
        public int NameMatchCount { get; set; }
        public int PathMatchCount { get; set; }
        public int TypeMatchCount { get; set; }
        public double? ContentCoverage { get; set; }
        public double? AiSimilarity { get; set; }
        public double? VisualSimilarity { get; set; }

        public double RankFusionScore { get; set; }
        public HashSet<SearchEvidenceKind> Evidence { get; } = [];
        public List<string> Reasons { get; } = [];
    }

    private sealed record ContentRootScope(
        string Root,
        IReadOnlyList<ContentDocumentRecord> Documents);
}

public sealed record ResultTextFactsProgress(
    int AnalyzedDocuments,
    int TotalDocuments,
    string CurrentPath);
