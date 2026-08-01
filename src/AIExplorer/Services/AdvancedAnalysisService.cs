using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class AdvancedAnalysisService
{
    private const int MaximumAnalyzedResults = 50;
    private const int EmbeddingBatchSize = 4;

    private readonly ITextEmbeddingService _embeddingService;
    private readonly DocumentTextExtractor _textExtractor;

    public AdvancedAnalysisService(
        ITextEmbeddingService embeddingService,
        DocumentTextExtractor? textExtractor = null)
    {
        _embeddingService = embeddingService;
        _textExtractor = textExtractor ?? new DocumentTextExtractor();
    }

    public async Task<AdvancedAnalysisResult> AnalyzeAsync(
        string query,
        IReadOnlyList<SearchResult> searchResults,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_embeddingService.IsAvailable)
        {
            throw new InvalidOperationException(
                "정밀 재평가에 필요한 로컬 AI 모델이 준비되지 않았습니다.");
        }

        var candidates = searchResults
            .Take(MaximumAnalyzedResults)
            .ToArray();
        if (candidates.Length == 0)
        {
            return new AdvancedAnalysisResult(searchResults, 0, 0);
        }

        var queryVectors = await _embeddingService.EmbedAsync(
            [query],
            EmbeddingPurpose.Query,
            cancellationToken,
            EmbeddingResolution.Full);
        var queryVector = queryVectors.Single();
        var analyzed = new List<AnalyzedCandidate>(candidates.Length);

        for (var offset = 0;
             offset < candidates.Length;
             offset += EmbeddingBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = candidates
                .Skip(offset)
                .Take(EmbeddingBatchSize)
                .ToArray();
            var passages = new string[batch.Length];
            for (var index = 0; index < batch.Length; index++)
            {
                passages[index] = await BuildPassageAsync(
                    batch[index],
                    cancellationToken);
            }

            var vectors = await _embeddingService.EmbedAsync(
                passages,
                EmbeddingPurpose.Passage,
                cancellationToken,
                EmbeddingResolution.Full);
            if (vectors.Count != batch.Length)
            {
                throw new InvalidDataException(
                    "정밀 재평가 결과 개수가 검색 결과 개수와 일치하지 않습니다.");
            }

            for (var index = 0; index < batch.Length; index++)
            {
                var similarity = CalculateCosineSimilarity(
                    queryVector,
                    vectors[index]);
                analyzed.Add(new AnalyzedCandidate(
                    batch[index],
                    similarity,
                    offset + index));
            }

            progress?.Report(new SearchProgress(
                Math.Min(offset + batch.Length, candidates.Length),
                analyzed.Count,
                batch[^1].DirectoryPath,
                SearchPhase.AdvancedAnalyzing));
        }

        var maximumBaseScore = candidates.Max(result => result.Score);
        var reranked = analyzed
            .OrderByDescending(candidate =>
                CalculateCombinedScore(candidate, maximumBaseScore))
            .ThenBy(candidate => candidate.OriginalRank)
            .Select(candidate =>
                CreateAdvancedResult(
                    candidate,
                    maximumBaseScore))
            .Concat(searchResults.Skip(candidates.Length))
            .ToArray();

        return new AdvancedAnalysisResult(
            reranked,
            analyzed.Count,
            queryVector.Length);
    }

    private async Task<string> BuildPassageAsync(
        SearchResult result,
        CancellationToken cancellationToken)
    {
        string content = string.Empty;
        if (!result.IsDirectory &&
            _textExtractor.CanExtract(Path.GetExtension(result.FullPath)))
        {
            var extracted = await _textExtractor.ExtractAsync(
                result.FullPath,
                cancellationToken);
            content = extracted?.Text ?? string.Empty;
        }

        return
            $"파일명: {result.Name}\n" +
            $"경로: {result.DirectoryPath}\n" +
            $"파일 형식: {result.TypeDisplay}\n" +
            $"기존 검색 근거: {result.Reason}\n" +
            $"본문: {SampleText(content, 1_800)}";
    }

    private static SearchResult CreateAdvancedResult(
        AnalyzedCandidate candidate,
        double maximumBaseScore)
    {
        var original = candidate.Result;
        var baseRatio = maximumBaseScore <= double.Epsilon
            ? 0d
            : Math.Clamp(original.Score / maximumBaseScore, 0d, 1d);
        var semanticPercent = Math.Clamp(
            (candidate.Similarity - 0.68d) / 0.24d * 100d,
            0d,
            100d);
        var matchPercent = Math.Round(
            Math.Clamp(
                semanticPercent * 0.72d +
                Math.Sqrt(baseRatio) * 28d,
                1d,
                99d),
            MidpointRounding.AwayFromZero);

        return new SearchResult
        {
            Name = original.Name,
            FullPath = original.FullPath,
            DirectoryPath = original.DirectoryPath,
            TypeDisplay = original.TypeDisplay,
            ModifiedDisplay = original.ModifiedDisplay,
            CreatedUtc = original.CreatedUtc,
            ModifiedUtc = original.ModifiedUtc,
            SizeBytes = original.SizeBytes,
            Reason =
                "로컬 AI Multilingual E5 정밀 재평가에서 " +
                "파일명·경로·본문을 " +
                "전체 768차원으로 다시 비교했습니다. " +
                original.Reason,
            IconGlyph = original.IconGlyph,
            IconImage = original.IconImage,
            PreviewImage = original.PreviewImage,
            Score = CalculateCombinedScore(candidate, maximumBaseScore),
            MatchPercent = matchPercent,
            WasAiAnalyzed = true,
            WasVisualAnalyzed = original.WasVisualAnalyzed,
            WasAdvancedAnalyzed = true,
            EvidenceKind = original.EvidenceKind,
            IsDirectory = original.IsDirectory
        };
    }

    private static double CalculateCombinedScore(
        AnalyzedCandidate candidate,
        double maximumBaseScore)
    {
        var baseRatio = maximumBaseScore <= double.Epsilon
            ? 0d
            : Math.Clamp(
                candidate.Result.Score / maximumBaseScore,
                0d,
                1d);
        return candidate.Similarity * 0.76d +
               Math.Sqrt(baseRatio) * 0.24d;
    }

    private static double CalculateCosineSimilarity(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
        {
            throw new InvalidDataException(
                "정밀 재평가 임베딩 벡터 차원이 올바르지 않습니다.");
        }

        var dotProduct = 0d;
        var leftMagnitude = 0d;
        var rightMagnitude = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            dotProduct += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        var magnitude = Math.Sqrt(leftMagnitude * rightMagnitude);
        return magnitude <= double.Epsilon
            ? 0d
            : dotProduct / magnitude;
    }

    private static string SampleText(string text, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.Length <= maximumCharacters)
        {
            return text;
        }

        var firstLength = maximumCharacters * 2 / 3;
        var lastLength = maximumCharacters - firstLength;
        return text[..firstLength] + "\n…\n" + text[^lastLength..];
    }

    private sealed record AnalyzedCandidate(
        SearchResult Result,
        double Similarity,
        int OriginalRank);
}
