using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class VisualIndexService
{
    public const string AnimeDomainPrompt =
        "anime illustration, manga or video game character artwork";
    public const string OfficeDomainPrompt =
        "office document, spreadsheet, presentation slide, scanned report " +
        "or software screenshot";

    private const int CurrentFormatVersion = 6;
    private const int MaximumNewDocumentsPerSearch = 256;
    private const int MaximumVisualCandidates = 500;
    private static readonly TimeSpan FailedAttemptRetryDelay =
        TimeSpan.FromHours(24);

    private readonly string _indexDirectory;
    private readonly IVisualEmbeddingService _embeddingService;
    private readonly IImageTaggingService? _imageTagger;
    private readonly ConcurrentDictionary<string, VisualIndexSnapshot>
        _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rootLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _domainPromptLock = new(1, 1);
    private float[]? _animeDomainVector;
    private float[]? _officeDomainVector;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public VisualIndexService(
        string indexDirectory,
        IVisualEmbeddingService embeddingService,
        IImageTaggingService? imageTagger = null)
    {
        _indexDirectory = indexDirectory;
        _embeddingService = embeddingService;
        _imageTagger = imageTagger;
    }

    public async Task<VisualIndexProbe> ProbeAsync(
        string root,
        IReadOnlyList<IndexedFileRecord> indexedItems,
        CancellationToken cancellationToken)
    {
        var documents = SelectVisualDocuments(root, indexedItems);
        if (!_embeddingService.IsAvailable || documents.Count == 0)
        {
            return new VisualIndexProbe(
                Exists: false,
                IsComplete: documents.Count == 0,
                IndexedDocuments: 0,
                TotalDocuments: documents.Count);
        }

        var normalizedRoot = Path.GetFullPath(root);
        VisualIndexSnapshot? snapshot = null;
        if (_memoryCache.TryGetValue(normalizedRoot, out var cached) &&
            IsCompatible(cached, normalizedRoot))
        {
            snapshot = cached;
        }
        else
        {
            var loaded = await TryLoadAsync(
                GetIndexPath(normalizedRoot),
                cancellationToken);
            if (loaded is not null && IsCompatible(loaded, normalizedRoot))
            {
                snapshot = loaded;
                _memoryCache[normalizedRoot] = loaded;
            }
        }

        if (snapshot is null)
        {
            return new VisualIndexProbe(
                Exists: false,
                IsComplete: false,
                IndexedDocuments: 0,
                TotalDocuments: documents.Count);
        }

        var documentsByPath = documents.ToDictionary(
            document => document.FullPath,
            StringComparer.OrdinalIgnoreCase);
        var currentAttempts = CountCurrentAttempts(snapshot, documentsByPath);
        var indexed = snapshot.Documents.Count(record =>
            documentsByPath.TryGetValue(record.FullPath, out var document) &&
            IsCurrent(record, document));
        return new VisualIndexProbe(
            Exists: true,
            IsComplete: currentAttempts >= documentsByPath.Count,
            IndexedDocuments: indexed,
            TotalDocuments: documentsByPath.Count);
    }

    public async Task<VisualWarmupResult> WarmUpAsync(
        string root,
        IReadOnlyList<IndexedFileRecord> indexedItems,
        int maximumNewDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable)
        {
            return VisualWarmupResult.Empty;
        }

        var documents = SelectVisualDocuments(root, indexedItems);
        if (documents.Count == 0)
        {
            return VisualWarmupResult.Empty;
        }

        var update = await UpdateIndexAsync(
            root,
            documents,
            intent: null,
            maximumNewDocuments,
            progress,
            cancellationToken);
        return new VisualWarmupResult(
            update.IndexedDocuments,
            documents.Count,
            update.NewlyIndexedDocuments);
    }

    public async Task<VisualSearchAccessResult> FindCandidatesAsync(
        string root,
        SearchIntent intent,
        IReadOnlyList<IndexedFileRecord> indexedItems,
        int maximumResults,
        int maximumNewDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable ||
            intent.DirectoryOnly ||
            intent.Terms.Count == 0)
        {
            return VisualSearchAccessResult.Empty;
        }

        var documents = SelectVisualDocuments(root, indexedItems);
        if (documents.Count == 0)
        {
            return VisualSearchAccessResult.Empty;
        }

        var update = await UpdateIndexAsync(
            root,
            documents,
            intent,
            maximumNewDocuments,
            progress,
            cancellationToken);
        var snapshot = update.Snapshot;
        if (snapshot.Documents.Count == 0)
        {
            return VisualSearchAccessResult.Empty;
        }

        var queryProfile = VisualQueryPromptBuilder.Analyze(intent);
        var queryVectors = new List<float[]>();
        foreach (var prompt in VisualQueryPromptBuilder.BuildVariants(
                     intent.OriginalQuery,
                     queryProfile))
        {
            queryVectors.Add(
                await _embeddingService.EmbedPromptAsync(
                    prompt,
                    cancellationToken));
        }
        var queryVector = queryVectors[0];
        var userInterfaceVector =
            queryProfile.SuppressUserInterface
                ? await _embeddingService.EmbedPromptAsync(
                    VisualQueryPromptBuilder.UserInterfaceNegativePrompt,
                    cancellationToken)
                : null;
        var genericCharacterVector =
            queryProfile.IsNamedSubject
                ? await _embeddingService.EmbedPromptAsync(
                    VisualQueryPromptBuilder.GenericCharacterPrompt,
                    cancellationToken)
                : null;
        var documentsByPath = documents.ToDictionary(
            document => document.FullPath,
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> identityAliases = queryProfile.IsNamedSubject
            ? VisualQueryPromptBuilder.BuildIdentityAliases(intent)
            : Array.Empty<string>();
        var tagAliases = VisualQueryPromptBuilder.BuildTagAliases(intent);
        var scored = new List<VisualSearchCandidate>();
        var namedSubjectCandidates = new List<VisualSearchCandidate>();
        var similaritiesByPath = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase);
        var searchedDocuments = 0;

        foreach (var record in snapshot.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!documentsByPath.TryGetValue(
                    record.FullPath,
                    out var document) ||
                !IsCurrent(record, document) ||
                !MatchesHardFilters(intent, document) ||
                record.Dimensions != queryVector.Length ||
                record.Vectors.Count == 0 ||
                record.Vectors.Any(vector =>
                    vector is null || vector.Length != queryVector.Length))
            {
                continue;
            }

            searchedDocuments++;
            var promptSimilarities = queryVectors
                .Select(vector => record.Vectors.Max(regionVector =>
                    CalculateCosineSimilarity(vector, regionVector)))
                .ToArray();
            var similarity = CombinePromptSimilarities(
                promptSimilarities,
                queryProfile.IsNamedSubject);
            var userInterfaceSimilarity =
                userInterfaceVector is null
                    ? double.NegativeInfinity
                    : record.Vectors.Max(regionVector =>
                        CalculateCosineSimilarity(
                            userInterfaceVector,
                            regionVector));
            var userInterfaceMargin =
                similarity - userInterfaceSimilarity;
            var genericCharacterSimilarity =
                genericCharacterVector is null
                    ? double.NegativeInfinity
                    : record.Vectors.Max(regionVector =>
                        CalculateCosineSimilarity(
                            genericCharacterVector,
                            regionVector));
            var namedSubjectLift =
                similarity - genericCharacterSimilarity;
            var identityMetadata = GetIdentityMetadataEvidence(
                document,
                identityAliases);
            var matchedCharacterTag = FindBestMatchingCharacterTag(
                record.Tags,
                identityAliases);
            var matchedGeneralTag = FindBestMatchingGeneralTag(
                record.Tags,
                tagAliases);
            var identityCorroborated =
                identityMetadata.NameMatched ||
                identityMetadata.PathMatched ||
                matchedCharacterTag is not null;
            var identityNameMatched = identityMetadata.NameMatched;
            var passesUserInterfaceFilter =
                matchedCharacterTag is not null ||
                !queryProfile.SuppressUserInterface ||
                userInterfaceMargin >=
                queryProfile.MinimumUserInterfaceMargin;
            var passesNamedSubjectFilter =
                !queryProfile.IsNamedSubject ||
                identityCorroborated;
            var passesMinimumSimilarity =
                similarity >= queryProfile.MinimumSimilarity ||
                matchedCharacterTag is not null ||
                matchedGeneralTag is { Confidence: >= 0.5d };
            if (passesMinimumSimilarity &&
                passesUserInterfaceFilter &&
                passesNamedSubjectFilter)
            {
                similaritiesByPath[document.FullPath] = similarity;
                var kind = document.Extension.Equals(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase)
                    ? "PDF 화면"
                    : "이미지 픽셀";
                var reason = queryProfile.IsNamedSubject
                    ? identityCorroborated
                        ? matchedCharacterTag is not null
                            ? $"캐릭터 태거가 '{FormatTagName(matchedCharacterTag.Name)}'을 " +
                              $"{matchedCharacterTag.Confidence:P0} 신뢰도로 확인했고 SigLIP 2 {kind} 분석도 함께 반영했습니다."
                            : $"파일명·폴더명의 캐릭터 이름 단서와 SigLIP 2 {kind} 분석이 함께 확인된 후보입니다."
                        : passesNamedSubjectFilter
                        ? $"SigLIP 2가 {kind}을 고유명 음역과 캐릭터 외형 문구에 모두 가깝게 본 시각 후보입니다. 파일명·폴더명 단서가 있으면 신원 판단에 우선 반영합니다."
                        : $"SigLIP 2가 {kind}을 비슷한 캐릭터 외형으로 본 광범위 후보입니다. 이 결과만으로 캐릭터 신원이 확인된 것은 아닙니다."
                    : matchedGeneralTag is not null
                    ? $"이미지 태거가 '{FormatTagName(matchedGeneralTag.Name)}' 특징을 " +
                      $"{matchedGeneralTag.Confidence:P0} 신뢰도로 확인했고 SigLIP 2 {kind} 의미를 함께 반영했습니다."
                    : queryProfile.SuppressUserInterface
                    ? $"SigLIP 2 다국어 시각 AI가 {kind}을 캐릭터 전용 문구와 비교하고 UI 화면 유사도를 감점했습니다."
                    : $"SigLIP 2 다국어 시각 AI가 {kind}과 검색 문장의 의미가 관련된 것으로 판단했습니다.";
                var candidate = new VisualSearchCandidate(
                    document,
                    175d +
                    similarity * 420d +
                    (identityCorroborated
                        ? identityNameMatched
                            ? 240d
                            : matchedCharacterTag is not null
                                ? 220d + matchedCharacterTag.Confidence * 120d
                                : 110d
                        : 0d) +
                    (queryProfile.IsNamedSubject
                        ? Math.Max(0d, namedSubjectLift) * 160d
                        : 0d) +
                    (matchedGeneralTag?.Confidence ?? 0d) * 90d,
                    similarity,
                    reason,
                    identityCorroborated);
                scored.Add(candidate);
                if (!queryProfile.IsNamedSubject ||
                    passesNamedSubjectFilter)
                {
                    namedSubjectCandidates.Add(candidate);
                }
            }

            if (searchedDocuments % 250 == 0)
            {
                progress?.Report(new SearchProgress(
                    searchedDocuments,
                    scored.Count,
                    document.DirectoryPath,
                    SearchPhase.VisualSearching));
            }
        }

        progress?.Report(new SearchProgress(
            searchedDocuments,
            scored.Count,
            string.Empty,
            SearchPhase.VisualSearching));

        var candidatePool =
            queryProfile.IsNamedSubject
                ? namedSubjectCandidates
                : scored;
        var bestSimilarity = candidatePool.Count == 0
            ? 0d
            : candidatePool.Max(candidate => candidate.Similarity);
        var relativeCutoff = Math.Max(
            queryProfile.MinimumSimilarity,
            bestSimilarity - queryProfile.MaximumDistanceFromBest);
        var orderedCandidates = candidatePool
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Similarity)
            .ThenByDescending(candidate => candidate.Document.ModifiedUtc)
            .ToArray();
        var strongCandidates = orderedCandidates
            .Where(candidate =>
                candidate.IdentityCorroborated ||
                candidate.Similarity >= relativeCutoff)
            .ToArray();
        var resultCount = Math.Min(
            Math.Min(Math.Max(1, maximumResults), MaximumVisualCandidates),
            strongCandidates.Length);
        return new VisualSearchAccessResult(
            strongCandidates
                .Take(resultCount)
                .ToArray(),
            similaritiesByPath,
            snapshot.Documents.Count,
            documents.Count,
            update.NewlyIndexedDocuments,
            CountCurrentAttempts(snapshot, documentsByPath) >=
            documents.Count);
    }

    private static double CombinePromptSimilarities(
        IReadOnlyList<double> similarities,
        bool requirePromptAgreement)
    {
        if (similarities.Count == 0)
        {
            return 0d;
        }

        var ordered = similarities
            .OrderByDescending(value => value)
            .ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        return requirePromptAgreement
            ? ordered[0] * 0.65d + ordered[1] * 0.35d
            : ordered[0] * 0.82d + ordered[1] * 0.18d;
    }

    private static VisualTagScoreRecord? FindBestMatchingCharacterTag(
        IReadOnlyList<VisualTagScoreRecord> tags,
        IReadOnlyList<string> aliases)
    {
        if (tags.Count == 0 || aliases.Count == 0)
        {
            return null;
        }

        var normalizedAliases = aliases
            .SelectMany(GetNormalizedTagTerms)
            .Where(alias => alias.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedAliases.Length == 0)
        {
            return null;
        }

        return tags
            .Where(tag => tag.Category == ImageTagCategory.Character)
            .Where(tag =>
            {
                var tagTerms = GetNormalizedTagTerms(tag.Name);
                return normalizedAliases.Any(alias =>
                    tagTerms.Contains(
                        alias,
                        StringComparer.OrdinalIgnoreCase));
            })
            .OrderByDescending(tag => tag.Confidence)
            .FirstOrDefault();
    }

    private static VisualTagScoreRecord? FindBestMatchingGeneralTag(
        IReadOnlyList<VisualTagScoreRecord> tags,
        IReadOnlyList<string> aliases)
    {
        if (tags.Count == 0 || aliases.Count == 0)
        {
            return null;
        }

        var normalizedAliases = aliases
            .SelectMany(GetNormalizedTagTerms)
            .Where(alias => alias.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return tags
            .Where(tag => tag.Category != ImageTagCategory.Character)
            .Where(tag =>
            {
                var tagTerms = GetNormalizedTagTerms(tag.Name);
                return normalizedAliases.Any(alias =>
                    tagTerms.Contains(
                        alias,
                        StringComparer.OrdinalIgnoreCase));
            })
            .OrderByDescending(tag => tag.Confidence)
            .FirstOrDefault();
    }

    private static string NormalizeTagText(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static IReadOnlyList<string> GetNormalizedTagTerms(string value)
    {
        var terms = value
            .Split(
                value.Where(character => !char.IsLetterOrDigit(character))
                    .Distinct()
                    .ToArray(),
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(NormalizeTagText)
            .Prepend(NormalizeTagText(value))
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return terms;
    }

    private static string FormatTagName(string value) =>
        value.Replace('_', ' ').Replace('-', ' ');

    private static IdentityMetadataEvidence GetIdentityMetadataEvidence(
        IndexedFileRecord document,
        IReadOnlyList<string> aliases)
    {
        if (aliases.Count == 0)
        {
            return default;
        }

        var nameWords = SearchQueryInterpreter.TokenizeText(
            Path.GetFileNameWithoutExtension(document.Name));
        var pathWords = SearchQueryInterpreter.TokenizeText(
            document.DirectoryPath);
        return new IdentityMetadataEvidence(
            aliases.Any(alias =>
                nameWords.Contains(
                    alias,
                    StringComparer.OrdinalIgnoreCase)),
            aliases.Any(alias =>
                pathWords.Contains(
                    alias,
                    StringComparer.OrdinalIgnoreCase)));
    }

    private async Task<VisualIndexUpdate> UpdateIndexAsync(
        string root,
        IReadOnlyList<IndexedFileRecord> documents,
        SearchIntent? intent,
        int maximumNewDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var rootLock = _rootLocks.GetOrAdd(
            normalizedRoot,
            _ => new SemaphoreSlim(1, 1));
        await rootLock.WaitAsync(cancellationToken);
        try
        {
            var indexPath = GetIndexPath(normalizedRoot);
            var snapshot = await GetSnapshotAsync(
                normalizedRoot,
                indexPath,
                cancellationToken);
            var documentsByPath = documents.ToDictionary(
                document => document.FullPath,
                StringComparer.OrdinalIgnoreCase);
            snapshot.Documents ??= [];
            snapshot.FailedAttempts ??= [];
            var recordsByPath = snapshot.Documents
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.FullPath) &&
                    documentsByPath.TryGetValue(
                        record.FullPath,
                        out var document) &&
                    IsCurrent(record, document))
                .GroupBy(
                    record => record.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);
            var failedAttemptsByPath = snapshot.FailedAttempts
                .Where(attempt =>
                    !string.IsNullOrWhiteSpace(attempt.FullPath) &&
                    documentsByPath.TryGetValue(
                        attempt.FullPath,
                        out var document) &&
                    IsCurrent(attempt, document) &&
                    DateTime.UtcNow - attempt.AttemptedUtc <
                    FailedAttemptRetryDelay)
                .GroupBy(
                    attempt => attempt.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);
            var forceCharacterTagging = intent is not null &&
                                         VisualQueryPromptBuilder
                                             .Analyze(intent)
                                             .IsNamedSubject;
            var pending = SelectPendingDocuments(
                documents,
                recordsByPath,
                failedAttemptsByPath,
                intent,
                maximumNewDocuments,
                forceCharacterTagging);
            var newlyIndexedDocuments = 0;

            try
            {
                for (var index = 0; index < pending.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var document = pending[index];
                    IReadOnlyList<float[]> vectors;
                    try
                    {
                        vectors = await _embeddingService.EmbedFileRegionsAsync(
                            document.FullPath,
                            cancellationToken);
                    }
                    catch (Exception exception) when (
                        exception is IOException or
                        UnauthorizedAccessException or
                        InvalidDataException or
                        NotSupportedException or
                        ArgumentException or
                        System.Runtime.InteropServices.COMException)
                    {
                        AppLog.Warning(
                            $"시각 색인에서 파일을 건너뜁니다: " +
                            $"{document.FullPath} · {exception.Message}");
                        failedAttemptsByPath[document.FullPath] =
                            CreateFailedAttempt(document);
                        continue;
                    }

                    if (vectors.Count == 0 ||
                        vectors.Any(vector => vector.Length == 0))
                    {
                        failedAttemptsByPath[document.FullPath] =
                            CreateFailedAttempt(document);
                        continue;
                    }

                    if (snapshot.Dimensions == 0)
                    {
                        snapshot.Dimensions = vectors[0].Length;
                    }
                    else if (vectors.Any(vector =>
                                 snapshot.Dimensions != vector.Length))
                    {
                        throw new InvalidDataException(
                            "시각 AI 색인 벡터 차원이 변경되었습니다.");
                    }

                    ImageTagEvidence? tagEvidence = null;
                    var taggerAnalyzed = false;
                    if (_imageTagger is { IsAvailable: true } &&
                        _imageTagger.CanAnalyze(document.Extension) &&
                        (forceCharacterTagging ||
                         await ShouldRunImageTaggerAsync(
                             vectors,
                             cancellationToken)))
                    {
                        try
                        {
                            tagEvidence = await _imageTagger.AnalyzeAsync(
                                document.FullPath,
                                cancellationToken);
                            taggerAnalyzed = true;
                        }
                        catch (Exception exception) when (
                            exception is IOException or
                            UnauthorizedAccessException or
                            InvalidDataException or
                            NotSupportedException or
                            ArgumentException or
                            System.Runtime.InteropServices.COMException or
                            Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
                        {
                            AppLog.Warning(
                                "이미지 태그 분석을 건너뜁니다: " +
                                $"{document.FullPath} · {exception.Message}");
                        }
                    }

                    recordsByPath[document.FullPath] =
                        CreateRecord(
                            document,
                            vectors,
                            tagEvidence,
                            taggerAnalyzed);
                    failedAttemptsByPath.Remove(document.FullPath);
                    newlyIndexedDocuments++;
                    snapshot.Documents = recordsByPath.Values.ToList();
                    progress?.Report(new SearchProgress(
                        index + 1,
                        snapshot.Documents.Count,
                        document.DirectoryPath,
                        SearchPhase.VisualIndexing));
                    if (intent is null)
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(35),
                            cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                snapshot.Documents = recordsByPath.Values.ToList();
                snapshot.FailedAttempts =
                    failedAttemptsByPath.Values.ToList();
                snapshot.UpdatedUtc = DateTime.UtcNow;
                _memoryCache[normalizedRoot] = snapshot;
                await TrySaveAsync(
                    indexPath,
                    snapshot,
                    CancellationToken.None);
                throw;
            }

            snapshot.Documents = recordsByPath.Values.ToList();
            snapshot.FailedAttempts =
                failedAttemptsByPath.Values.ToList();
            snapshot.UpdatedUtc = DateTime.UtcNow;
            _memoryCache[normalizedRoot] = snapshot;
            await TrySaveAsync(indexPath, snapshot, cancellationToken);
            return new VisualIndexUpdate(
                snapshot,
                snapshot.Documents.Count,
                newlyIndexedDocuments);
        }
        finally
        {
            rootLock.Release();
        }
    }

    private IReadOnlyList<IndexedFileRecord> SelectVisualDocuments(
        string root,
        IReadOnlyList<IndexedFileRecord> indexedItems) =>
        indexedItems
            .Where(item =>
                !item.IsDirectory &&
                SearchPathPriority.IsInsideRoot(item.FullPath, root) &&
                _embeddingService.CanAnalyze(item.Extension))
            .GroupBy(
                item => item.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    private async Task<VisualIndexSnapshot> GetSnapshotAsync(
        string root,
        string indexPath,
        CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(root, out var cached) &&
            IsCompatible(cached, root))
        {
            return cached;
        }

        var loaded = await TryLoadAsync(indexPath, cancellationToken);
        if (loaded is not null && IsCompatible(loaded, root))
        {
            _memoryCache[root] = loaded;
            return loaded;
        }

        return new VisualIndexSnapshot
        {
            FormatVersion = CurrentFormatVersion,
            ModelId = _embeddingService.ModelId,
            TaggerModelId = _imageTagger is { IsAvailable: true }
                ? _imageTagger.ModelId
                : string.Empty,
            Root = root,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private bool IsCompatible(VisualIndexSnapshot snapshot, string root) =>
        snapshot.FormatVersion == CurrentFormatVersion &&
        string.Equals(
            snapshot.ModelId,
            _embeddingService.ModelId,
            StringComparison.Ordinal) &&
        string.Equals(
            snapshot.TaggerModelId,
            _imageTagger is { IsAvailable: true }
                ? _imageTagger.ModelId
                : string.Empty,
            StringComparison.Ordinal) &&
        string.Equals(snapshot.Root, root, StringComparison.OrdinalIgnoreCase);

    private static bool IsCurrent(
        VisualVectorRecord record,
        IndexedFileRecord document) =>
        record.ModifiedUtcTicks == document.ModifiedUtc.Ticks &&
        record.SizeBytes == document.SizeBytes;

    private static bool IsCurrent(
        VisualFailedAttemptRecord attempt,
        IndexedFileRecord document) =>
        attempt.ModifiedUtcTicks == document.ModifiedUtc.Ticks &&
        attempt.SizeBytes == document.SizeBytes;

    private static double CalculateQueryPriority(
        SearchIntent intent,
        IndexedFileRecord document)
    {
        var metadataCandidate = SearchRankingService.ScoreCandidate(
            intent,
            document);
        return metadataCandidate is null
            ? SearchPathPriority.GetPathPriority(document.DirectoryPath)
            : metadataCandidate.NameMatchCount * 10_000d +
              metadataCandidate.TypeMatchCount * 5_000d +
              metadataCandidate.PathMatchCount * 1_000d +
              metadataCandidate.Score;
    }

    private IndexedFileRecord[] SelectPendingDocuments(
        IReadOnlyList<IndexedFileRecord> documents,
        IReadOnlyDictionary<string, VisualVectorRecord> recordsByPath,
        IReadOnlyDictionary<string, VisualFailedAttemptRecord>
            failedAttemptsByPath,
        SearchIntent? intent,
        int maximumNewDocuments,
        bool forceCharacterTagging)
    {
        var capacity = Math.Clamp(
            maximumNewDocuments,
            0,
            MaximumNewDocumentsPerSearch);
        if (capacity == 0)
        {
            return [];
        }
        var pending = documents
            .Where(document =>
                !failedAttemptsByPath.ContainsKey(document.FullPath) &&
                (!recordsByPath.TryGetValue(
                     document.FullPath,
                     out var existingRecord) ||
                 forceCharacterTagging &&
                 !existingRecord.TaggerAnalyzed &&
                 _imageTagger is { IsAvailable: true } &&
                 _imageTagger.CanAnalyze(document.Extension)))
            .Select(document => new PendingVisualDocument(
                document,
                intent is null
                    ? SearchPathPriority.GetPathPriority(
                        document.DirectoryPath)
                    : CalculateQueryPriority(intent, document)))
            .ToArray();
        if (pending.Length <= capacity)
        {
            return pending
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.Document.ModifiedUtc)
                .Select(item => item.Document)
                .ToArray();
        }

        var selected = new List<IndexedFileRecord>(capacity);
        var selectedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (intent is not null)
        {
            foreach (var item in pending
                         .Where(item => item.Priority >= 1_000d)
                         .OrderByDescending(item => item.Priority)
                         .ThenByDescending(item =>
                             item.Document.ModifiedUtc)
                         .Take(Math.Max(1, capacity / 2)))
            {
                selected.Add(item.Document);
                selectedPaths.Add(item.Document.FullPath);
            }
        }

        var directoryQueues = pending
            .Where(item => !selectedPaths.Contains(item.Document.FullPath))
            .GroupBy(
                item => item.Document.DirectoryPath,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new Queue<PendingVisualDocument>(
                group
                    .OrderByDescending(item => item.Priority)
                    .ThenByDescending(item =>
                        item.Document.ModifiedUtc)))
            .OrderByDescending(queue => queue.Peek().Priority)
            .ToList();

        while (selected.Count < capacity &&
               directoryQueues.Count > 0)
        {
            for (var index = 0;
                 index < directoryQueues.Count &&
                 selected.Count < capacity;)
            {
                var queue = directoryQueues[index];
                var item = queue.Dequeue();
                selected.Add(item.Document);
                selectedPaths.Add(item.Document.FullPath);
                if (queue.Count == 0)
                {
                    directoryQueues.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        return selected.ToArray();
    }

    private static bool MatchesHardFilters(
        SearchIntent intent,
        IndexedFileRecord document)
    {
        if (intent.ModifiedFromUtc is not null &&
            document.ModifiedUtc < intent.ModifiedFromUtc.Value)
        {
            return false;
        }

        if (intent.ModifiedToUtc is not null &&
            document.ModifiedUtc >= intent.ModifiedToUtc.Value)
        {
            return false;
        }

        if (intent.RequestedExtensions.Count > 0 &&
            !intent.RequestedExtensions.Contains(
                document.Extension,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return intent.Categories.Count == 0 ||
               intent.Categories.Contains(
                   FileTypeCatalog.GetCategory(document.Extension));
    }

    private static VisualVectorRecord CreateRecord(
        IndexedFileRecord document,
        IReadOnlyList<float[]> vectors,
        ImageTagEvidence? tagEvidence,
        bool taggerAnalyzed) =>
        new()
        {
            FullPath = document.FullPath,
            ModifiedUtcTicks = document.ModifiedUtc.Ticks,
            SizeBytes = document.SizeBytes,
            Dimensions = vectors[0].Length,
            TaggerAnalyzed = taggerAnalyzed,
            Vectors = vectors
                .Select(vector => Quantize(vector))
                .ToList(),
            Tags = tagEvidence?.Predictions
                .Select(tag => new VisualTagScoreRecord
                {
                    Name = tag.Name,
                    Category = tag.Category,
                    Confidence = tag.Confidence
                })
                .ToList() ?? []
        };

    private static VisualFailedAttemptRecord CreateFailedAttempt(
        IndexedFileRecord document) =>
        new()
        {
            FullPath = document.FullPath,
            ModifiedUtcTicks = document.ModifiedUtc.Ticks,
            SizeBytes = document.SizeBytes,
            AttemptedUtc = DateTime.UtcNow
        };

    private static int CountCurrentAttempts(
        VisualIndexSnapshot snapshot,
        IReadOnlyDictionary<string, IndexedFileRecord> documentsByPath)
    {
        var attemptedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var record in snapshot.Documents)
        {
            if (documentsByPath.TryGetValue(
                    record.FullPath,
                    out var document) &&
                IsCurrent(record, document))
            {
                attemptedPaths.Add(record.FullPath);
            }
        }

        foreach (var attempt in snapshot.FailedAttempts)
        {
            if (documentsByPath.TryGetValue(
                    attempt.FullPath,
                    out var document) &&
                IsCurrent(attempt, document) &&
                DateTime.UtcNow - attempt.AttemptedUtc <
                FailedAttemptRetryDelay)
            {
                attemptedPaths.Add(attempt.FullPath);
            }
        }

        return attemptedPaths.Count;
    }

    private async Task<bool> ShouldRunImageTaggerAsync(
        IReadOnlyList<float[]> imageVectors,
        CancellationToken cancellationToken)
    {
        if (_imageTagger is not { IsAvailable: true } ||
            imageVectors.Count == 0)
        {
            return false;
        }

        await EnsureDomainPromptVectorsAsync(cancellationToken);
        var animeSimilarity = imageVectors.Max(vector =>
            CalculateCosineSimilarity(_animeDomainVector!, vector));
        var officeSimilarity = imageVectors.Max(vector =>
            CalculateCosineSimilarity(_officeDomainVector!, vector));
        return animeSimilarity >= 0.06d &&
               animeSimilarity - officeSimilarity >= 0.015d;
    }

    private async Task EnsureDomainPromptVectorsAsync(
        CancellationToken cancellationToken)
    {
        if (_animeDomainVector is not null && _officeDomainVector is not null)
        {
            return;
        }

        await _domainPromptLock.WaitAsync(cancellationToken);
        try
        {
            if (_animeDomainVector is not null &&
                _officeDomainVector is not null)
            {
                return;
            }

            _animeDomainVector = await _embeddingService.EmbedPromptAsync(
                AnimeDomainPrompt,
                cancellationToken);
            _officeDomainVector = await _embeddingService.EmbedPromptAsync(
                OfficeDomainPrompt,
                cancellationToken);
        }
        finally
        {
            _domainPromptLock.Release();
        }
    }

    private static double CalculateCosineSimilarity(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
    {
        if (left.Count != right.Count || left.Count == 0)
        {
            return 0d;
        }

        var dotProduct = 0d;
        var leftMagnitudeSquared = 0d;
        var rightMagnitudeSquared = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            dotProduct += left[index] * right[index];
            leftMagnitudeSquared += left[index] * left[index];
            rightMagnitudeSquared += right[index] * right[index];
        }

        return leftMagnitudeSquared <= double.Epsilon ||
               rightMagnitudeSquared <= double.Epsilon
            ? 0d
            : dotProduct /
              Math.Sqrt(leftMagnitudeSquared * rightMagnitudeSquared);
    }

    private static byte[] Quantize(IReadOnlyList<float> vector)
    {
        var quantized = new byte[vector.Count];
        for (var index = 0; index < vector.Count; index++)
        {
            var value = (int)Math.Round(
                Math.Clamp(vector[index], -1f, 1f) * 127f);
            quantized[index] = unchecked((byte)(sbyte)Math.Clamp(
                value,
                -127,
                127));
        }

        return quantized;
    }

    private static double CalculateCosineSimilarity(
        IReadOnlyList<float> query,
        IReadOnlyList<byte> quantizedImage)
    {
        var dotProduct = 0d;
        var imageMagnitudeSquared = 0d;
        for (var index = 0; index < query.Count; index++)
        {
            var imageValue =
                unchecked((sbyte)quantizedImage[index]) / 127d;
            dotProduct += query[index] * imageValue;
            imageMagnitudeSquared += imageValue * imageValue;
        }

        return imageMagnitudeSquared <= double.Epsilon
            ? 0d
            : dotProduct / Math.Sqrt(imageMagnitudeSquared);
    }

    private async Task<VisualIndexSnapshot?> TryLoadAsync(
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
            return await JsonSerializer.DeserializeAsync<VisualIndexSnapshot>(
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
        VisualIndexSnapshot snapshot,
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
        return Path.Combine(_indexDirectory, $"visual-{key}.json.gz");
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
            // A later visual index save can replace the stale file.
        }
    }

    private sealed record VisualIndexUpdate(
        VisualIndexSnapshot Snapshot,
        int IndexedDocuments,
        int NewlyIndexedDocuments);

    private sealed record PendingVisualDocument(
        IndexedFileRecord Document,
        double Priority);

    private readonly record struct IdentityMetadataEvidence(
        bool NameMatched,
        bool PathMatched);
}

public sealed record VisualSearchAccessResult(
    IReadOnlyList<VisualSearchCandidate> Candidates,
    IReadOnlyDictionary<string, double> SimilaritiesByPath,
    int IndexedDocuments,
    int TotalDocuments,
    int NewlyIndexedDocuments,
    bool IsComplete)
{
    public static VisualSearchAccessResult Empty { get; } =
        new(
            [],
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase),
            0,
            0,
            0,
            true);
}

public sealed record VisualSearchCandidate(
    IndexedFileRecord Document,
    double Score,
    double Similarity,
    string Reason,
    bool IdentityCorroborated = false);

public sealed record VisualWarmupResult(
    int IndexedDocuments,
    int TotalDocuments,
    int NewlyIndexedDocuments)
{
    public static VisualWarmupResult Empty { get; } = new(0, 0, 0);
}

public sealed class VisualIndexSnapshot
{
    public int FormatVersion { get; set; }

    public required string ModelId { get; set; }

    public string TaggerModelId { get; set; } = string.Empty;

    public required string Root { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public int Dimensions { get; set; }

    public List<VisualVectorRecord> Documents { get; set; } = [];

    public List<VisualFailedAttemptRecord> FailedAttempts { get; set; } = [];
}

public sealed class VisualVectorRecord
{
    public required string FullPath { get; set; }

    public long ModifiedUtcTicks { get; set; }

    public long? SizeBytes { get; set; }

    public int Dimensions { get; set; }

    public bool TaggerAnalyzed { get; set; }

    public List<byte[]> Vectors { get; set; } = [];

    public List<VisualTagScoreRecord> Tags { get; set; } = [];
}

public sealed class VisualTagScoreRecord
{
    public required string Name { get; set; }

    public ImageTagCategory Category { get; set; }

    public double Confidence { get; set; }
}

public sealed class VisualFailedAttemptRecord
{
    public required string FullPath { get; set; }

    public long ModifiedUtcTicks { get; set; }

    public long? SizeBytes { get; set; }

    public DateTime AttemptedUtc { get; set; }
}


public sealed record VisualIndexProbe(
    bool Exists,
    bool IsComplete,
    int IndexedDocuments,
    int TotalDocuments);
