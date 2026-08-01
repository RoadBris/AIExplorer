using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class SemanticIndexService
{
    private const int CurrentFormatVersion = 6;
    private const int EmbeddingBatchSize = 4;
    private const int MaximumNewDocumentsPerSearch = 384;
    private const double MinimumSimilarity = 0.76d;
    private const double MaximumDistanceFromBest = 0.05d;
    private const int MaximumSemanticCandidates = 240;
    private const int MinimumExpandedCandidateCount = 120;

    private readonly string _indexDirectory;
    private readonly ITextEmbeddingService _embeddingService;
    private readonly ConcurrentDictionary<string, SemanticIndexSnapshot> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rootLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SemanticIndexService(
        string indexDirectory,
        ITextEmbeddingService embeddingService)
    {
        _indexDirectory = indexDirectory;
        _embeddingService = embeddingService;
    }

    public async Task<SemanticIndexProbe> ProbeAsync(
        string root,
        IReadOnlyList<ContentDocumentRecord> documents,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable || documents.Count == 0)
        {
            return new SemanticIndexProbe(
                Exists: false,
                IsComplete: documents.Count == 0,
                IndexedDocuments: 0,
                TotalDocuments: documents.Count);
        }

        var normalizedRoot = Path.GetFullPath(root);
        SemanticIndexSnapshot? snapshot = null;
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
            return new SemanticIndexProbe(
                Exists: false,
                IsComplete: false,
                IndexedDocuments: 0,
                TotalDocuments: documents.Count);
        }

        var documentsByPath = documents
            .GroupBy(
                document => document.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        var indexed = snapshot.Documents.Count(record =>
            documentsByPath.TryGetValue(record.FullPath, out var document) &&
            IsCurrent(record, document));
        return new SemanticIndexProbe(
            Exists: true,
            IsComplete: indexed >= documentsByPath.Count,
            IndexedDocuments: indexed,
            TotalDocuments: documentsByPath.Count);
    }

    public async Task<SemanticWarmupResult> WarmUpAsync(
        string root,
        IReadOnlyList<ContentDocumentRecord> documents,
        int maximumNewDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable || documents.Count == 0)
        {
            return SemanticWarmupResult.Empty;
        }

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
            var documentsByPath = documents
                .GroupBy(
                    document => document.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
            snapshot.Documents ??= [];
            var recordsByPath = snapshot.Documents
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.FullPath) &&
                    documentsByPath.ContainsKey(record.FullPath))
                .GroupBy(
                    record => record.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);
            var pendingDocuments = documentsByPath.Values
                .Where(document =>
                    !recordsByPath.TryGetValue(document.FullPath, out var record) ||
                    !IsCurrent(record, document))
                .OrderByDescending(document =>
                    SearchPathPriority.GetPathPriority(document.DirectoryPath))
                .ThenByDescending(document => document.Text.Length > 0)
                .ThenByDescending(document => document.ModifiedUtc)
                .Take(Math.Clamp(
                    maximumNewDocuments,
                    0,
                    MaximumNewDocumentsPerSearch))
                .ToArray();
            var newlyIndexedDocuments = 0;

            try
            {
                for (var offset = 0;
                     offset < pendingDocuments.Length;
                     offset += EmbeddingBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = pendingDocuments
                        .Skip(offset)
                        .Take(EmbeddingBatchSize)
                        .ToArray();
                    var vectors = await _embeddingService.EmbedAsync(
                        batch
                            .Select(document =>
                                BuildEmbeddingPassage(
                                    normalizedRoot,
                                    document))
                            .ToArray(),
                        EmbeddingPurpose.Passage,
                        cancellationToken);
                    if (vectors.Count != batch.Length)
                    {
                        throw new InvalidDataException(
                            "자동 AI 색인 결과 개수가 문서 개수와 일치하지 않습니다.");
                    }

                    for (var index = 0; index < batch.Length; index++)
                    {
                        var document = batch[index];
                        var vector = vectors[index];
                        if (snapshot.Dimensions == 0)
                        {
                            snapshot.Dimensions = vector.Length;
                        }
                        else if (snapshot.Dimensions != vector.Length)
                        {
                            throw new InvalidDataException(
                                "자동 AI 색인 벡터 차원이 변경되었습니다.");
                        }

                        recordsByPath[document.FullPath] =
                            CreateRecord(document, vector);
                        newlyIndexedDocuments++;
                    }

                    snapshot.Documents = recordsByPath.Values.ToList();
                    progress?.Report(new SearchProgress(
                        Math.Min(
                            offset + batch.Length,
                            pendingDocuments.Length),
                        snapshot.Documents.Count,
                        batch[^1].DirectoryPath,
                        SearchPhase.SemanticIndexing));
                }
            }
            catch (OperationCanceledException)
            {
                snapshot.Documents = recordsByPath.Values.ToList();
                snapshot.UpdatedUtc = DateTime.UtcNow;
                _memoryCache[normalizedRoot] = snapshot;
                await TrySaveAsync(
                    indexPath,
                    snapshot,
                    CancellationToken.None);
                throw;
            }

            snapshot.Documents = recordsByPath.Values.ToList();
            snapshot.UpdatedUtc = DateTime.UtcNow;
            _memoryCache[normalizedRoot] = snapshot;
            await TrySaveAsync(indexPath, snapshot, cancellationToken);
            return new SemanticWarmupResult(
                snapshot.Documents.Count,
                documentsByPath.Count,
                newlyIndexedDocuments);
        }
        finally
        {
            rootLock.Release();
        }
    }

    public async Task<SemanticSearchAccessResult> FindCandidatesAsync(
        string root,
        SearchIntent intent,
        IReadOnlyList<ContentDocumentRecord> documents,
        int maximumResults,
        int maximumNewDocuments,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable ||
            intent.DirectoryOnly ||
            intent.Terms.Count == 0 ||
            documents.Count == 0)
        {
            return SemanticSearchAccessResult.Empty;
        }

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
            var documentsByPath = documents
                .GroupBy(
                    document => document.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
            snapshot.Documents ??= [];
            var recordsByPath = snapshot.Documents
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.FullPath) &&
                    documentsByPath.ContainsKey(record.FullPath))
                .GroupBy(
                    record => record.FullPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);
            var pendingDocuments = documents
                .Where(document =>
                    !recordsByPath.TryGetValue(document.FullPath, out var record) ||
                    !IsCurrent(record, document))
                .OrderByDescending(document =>
                    CalculateQueryPriority(intent, document))
                .ThenByDescending(document =>
                    SearchPathPriority.GetPathPriority(document.DirectoryPath))
                .ThenByDescending(document => document.ModifiedUtc)
                .Take(Math.Clamp(
                    maximumNewDocuments,
                    0,
                    MaximumNewDocumentsPerSearch))
                .ToArray();
            var newlyIndexedDocuments = 0;

            try
            {
                for (var offset = 0;
                     offset < pendingDocuments.Length;
                     offset += EmbeddingBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = pendingDocuments
                        .Skip(offset)
                        .Take(EmbeddingBatchSize)
                        .ToArray();
                    var passages = batch
                        .Select(document =>
                            BuildEmbeddingPassage(normalizedRoot, document))
                        .ToArray();
                    var vectors = await _embeddingService.EmbedAsync(
                        passages,
                        EmbeddingPurpose.Passage,
                        cancellationToken);
                    if (vectors.Count != batch.Length)
                    {
                        throw new InvalidDataException(
                            "AI 임베딩 결과 개수가 문서 개수와 일치하지 않습니다.");
                    }

                    for (var index = 0; index < batch.Length; index++)
                    {
                        var document = batch[index];
                        var vector = vectors[index];
                        if (snapshot.Dimensions == 0)
                        {
                            snapshot.Dimensions = vector.Length;
                        }
                        else if (snapshot.Dimensions != vector.Length)
                        {
                            throw new InvalidDataException(
                                "AI 임베딩 벡터 차원이 변경되었습니다.");
                        }

                        var record = CreateRecord(document, vector);
                        recordsByPath[document.FullPath] = record;
                        newlyIndexedDocuments++;
                    }

                    snapshot.Documents = recordsByPath.Values.ToList();
                    progress?.Report(new SearchProgress(
                        Math.Min(offset + batch.Length, pendingDocuments.Length),
                        snapshot.Documents.Count,
                        batch[^1].DirectoryPath,
                        SearchPhase.SemanticIndexing));
                }
            }
            catch (OperationCanceledException)
            {
                snapshot.Documents = recordsByPath.Values.ToList();
                await TrySaveAsync(
                    indexPath,
                    snapshot,
                    CancellationToken.None);
                throw;
            }

            snapshot.Documents = recordsByPath.Values.ToList();
            snapshot.UpdatedUtc = DateTime.UtcNow;
            _memoryCache[normalizedRoot] = snapshot;
            await TrySaveAsync(indexPath, snapshot, cancellationToken);

            if (snapshot.Documents.Count == 0)
            {
                return SemanticSearchAccessResult.Empty;
            }

            var queryVectors = await _embeddingService.EmbedAsync(
                [BuildEmbeddingQuery(intent)],
                EmbeddingPurpose.Query,
                cancellationToken);
            var queryVector = queryVectors.Single();
            var scored = new List<SemanticSearchCandidate>();
            var similaritiesByPath = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);
            var searchedDocuments = 0;

            foreach (var record in snapshot.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!documentsByPath.TryGetValue(
                        record.FullPath,
                        out var document) ||
                    !MatchesHardFilters(intent, document) ||
                    record.Dimensions != queryVector.Length ||
                    record.Vector is null ||
                    record.Vector.Length != queryVector.Length)
                {
                    continue;
                }

                searchedDocuments++;
                var similarity = CalculateCosineSimilarity(
                    queryVector,
                    record.Vector);
                similaritiesByPath[document.FullPath] = similarity;
                if (similarity >= MinimumSimilarity)
                {
                    scored.Add(new SemanticSearchCandidate(
                        document,
                        120d + Math.Max(0d, similarity - 0.55d) * 500d,
                        similarity,
                        "로컬 AI Multilingual E5가 파일명·경로·추출 내용을 " +
                        "검색 문장과 비교해 관련 가능성을 추정했습니다. " +
                        "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다."));
                }

                if (searchedDocuments % 1_000 == 0)
                {
                    progress?.Report(new SearchProgress(
                        searchedDocuments,
                        scored.Count,
                        document.DirectoryPath,
                        SearchPhase.SemanticSearching));
                }
            }

            progress?.Report(new SearchProgress(
                searchedDocuments,
                scored.Count,
                string.Empty,
                SearchPhase.SemanticSearching));

            var totalDocuments = documentsByPath.Count;
            var bestSimilarity = scored.Count == 0
                ? 0d
                : scored.Max(candidate => candidate.Similarity);
            var relativeCutoff = Math.Max(
                MinimumSimilarity,
                bestSimilarity - MaximumDistanceFromBest);
            var orderedCandidates = scored
                .OrderByDescending(candidate => candidate.Similarity)
                .ThenByDescending(candidate => candidate.Document.ModifiedUtc)
                .ToArray();
            var strongCandidates = orderedCandidates
                .Where(candidate => candidate.Similarity >= relativeCutoff)
                .ToArray();
            var expandedCount = Math.Min(
                Math.Min(Math.Max(1, maximumResults), MaximumSemanticCandidates),
                Math.Max(MinimumExpandedCandidateCount, strongCandidates.Length));
            var selectedCandidates = strongCandidates.Length >= expandedCount
                ? strongCandidates
                : orderedCandidates;
            return new SemanticSearchAccessResult(
                selectedCandidates
                    .Take(expandedCount)
                    .ToArray(),
                similaritiesByPath,
                snapshot.Documents.Count,
                totalDocuments,
                newlyIndexedDocuments,
                snapshot.Documents.Count >= totalDocuments);
        }
        finally
        {
            rootLock.Release();
        }
    }

    private async Task<SemanticIndexSnapshot> GetSnapshotAsync(
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

        return new SemanticIndexSnapshot
        {
            FormatVersion = CurrentFormatVersion,
            ModelId = _embeddingService.ModelId,
            Root = root,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private bool IsCompatible(SemanticIndexSnapshot snapshot, string root) =>
        snapshot.FormatVersion == CurrentFormatVersion &&
        string.Equals(
            snapshot.ModelId,
            _embeddingService.ModelId,
            StringComparison.Ordinal) &&
        string.Equals(snapshot.Root, root, StringComparison.OrdinalIgnoreCase);

    private static bool IsCurrent(
        SemanticVectorRecord record,
        ContentDocumentRecord document) =>
        record.ModifiedUtcTicks == document.ModifiedUtc.Ticks &&
        record.SizeBytes == document.SizeBytes &&
        record.TextLength == document.Text.Length &&
        string.Equals(
            record.TextHash,
            Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(document.Text))),
            StringComparison.Ordinal);

    private static double CalculateQueryPriority(
        SearchIntent intent,
        ContentDocumentRecord document)
    {
        var metadataCandidate = SearchRankingService.ScoreCandidate(
            intent,
            new IndexedFileRecord
            {
                Name = document.Name,
                FullPath = document.FullPath,
                DirectoryPath = document.DirectoryPath,
                Extension = document.Extension,
                IsDirectory = false,
                SizeBytes = document.SizeBytes,
                CreatedUtc = document.CreatedUtc,
                ModifiedUtc = document.ModifiedUtc
            });
        if (metadataCandidate is null)
        {
            return 0d;
        }

        return
            metadataCandidate.NameMatchCount * 10_000d +
            metadataCandidate.TypeMatchCount * 5_000d +
            metadataCandidate.PathMatchCount * 1_000d +
            metadataCandidate.Score;
    }

    private static SemanticVectorRecord CreateRecord(
        ContentDocumentRecord document,
        IReadOnlyList<float> vector) =>
        new()
        {
            FullPath = document.FullPath,
            ModifiedUtcTicks = document.ModifiedUtc.Ticks,
            SizeBytes = document.SizeBytes,
            TextLength = document.Text.Length,
            TextHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(document.Text))),
            Dimensions = vector.Count,
            Vector = Quantize(vector)
        };

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
        IReadOnlyList<byte> quantizedPassage)
    {
        var dotProduct = 0d;
        var passageMagnitudeSquared = 0d;
        for (var index = 0; index < query.Count; index++)
        {
            var passageValue =
                unchecked((sbyte)quantizedPassage[index]) / 127d;
            dotProduct += query[index] * passageValue;
            passageMagnitudeSquared += passageValue * passageValue;
        }

        return passageMagnitudeSquared <= double.Epsilon
            ? 0d
            : dotProduct / Math.Sqrt(passageMagnitudeSquared);
    }

    private static string BuildEmbeddingPassage(
        string root,
        ContentDocumentRecord document)
    {
        string relativeDirectory;
        try
        {
            relativeDirectory = Path.GetRelativePath(
                root,
                document.DirectoryPath);
        }
        catch
        {
            relativeDirectory = document.DirectoryPath;
        }

        return
            $"파일명: {document.Name}\n" +
            $"폴더: {relativeDirectory}\n" +
            $"형식: {FileMetadataDescriptor.GetFormatDescription(document.Extension)}\n" +
            $"내용: {SampleText(document.Text, 420)}";
    }

    private static string BuildEmbeddingQuery(SearchIntent intent)
    {
        var semanticTerms = intent.Terms
            .Select(term => term.Original)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        if (semanticTerms.Length == 0)
        {
            return intent.OriginalQuery;
        }

        return
            $"{intent.OriginalQuery}\n" +
            $"핵심 검색 의도: {string.Join(", ", semanticTerms)}";
    }

    private static string SampleText(string text, int maximumCharacters)
    {
        if (text.Length <= maximumCharacters)
        {
            return text;
        }

        var firstLength = maximumCharacters / 2;
        var middleLength = maximumCharacters / 4;
        var lastLength = maximumCharacters - firstLength - middleLength;
        var middleStart = Math.Max(
            firstLength,
            text.Length / 2 - middleLength / 2);
        return
            text[..firstLength] +
            "\n…\n" +
            text.Substring(middleStart, middleLength) +
            "\n…\n" +
            text[^lastLength..];
    }

    private static bool MatchesHardFilters(
        SearchIntent intent,
        ContentDocumentRecord document)
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

    private async Task<SemanticIndexSnapshot?> TryLoadAsync(
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
            return await JsonSerializer.DeserializeAsync<SemanticIndexSnapshot>(
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
        SemanticIndexSnapshot snapshot,
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
        return Path.Combine(_indexDirectory, $"semantic-{key}.json.gz");
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
            // A stale temporary file can be replaced by a later index save.
        }
    }
}

public sealed record SemanticSearchAccessResult(
    IReadOnlyList<SemanticSearchCandidate> Candidates,
    IReadOnlyDictionary<string, double> SimilaritiesByPath,
    int IndexedDocuments,
    int TotalDocuments,
    int NewlyIndexedDocuments,
    bool IsComplete)
{
    public static SemanticSearchAccessResult Empty { get; } =
        new(
            [],
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase),
            0,
            0,
            0,
            true);
}

public sealed record SemanticSearchCandidate(
    ContentDocumentRecord Document,
    double Score,
    double Similarity,
    string Reason);

public sealed record SemanticWarmupResult(
    int IndexedDocuments,
    int TotalDocuments,
    int NewlyIndexedDocuments)
{
    public static SemanticWarmupResult Empty { get; } = new(0, 0, 0);
}

public sealed class SemanticIndexSnapshot
{
    public int FormatVersion { get; set; }

    public required string ModelId { get; set; }

    public required string Root { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public int Dimensions { get; set; }

    public List<SemanticVectorRecord> Documents { get; set; } = [];
}

public sealed class SemanticVectorRecord
{
    public required string FullPath { get; set; }

    public long ModifiedUtcTicks { get; set; }

    public long? SizeBytes { get; set; }

    public int TextLength { get; set; }

    public required string TextHash { get; set; }

    public int Dimensions { get; set; }

    public required byte[] Vector { get; set; }
}


public sealed record SemanticIndexProbe(
    bool Exists,
    bool IsComplete,
    int IndexedDocuments,
    int TotalDocuments);
