using AIExplorer.Models;

namespace AIExplorer.Services;

public static class ContentSearchService
{
    public static IReadOnlyList<ContentSearchCandidate> FindCandidates(
        SearchIntent intent,
        IReadOnlyList<ContentDocumentRecord> documents,
        int maximumResults,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (intent.DirectoryOnly ||
            documents.Count == 0 ||
            intent.Terms.Count == 0 &&
            !intent.RequiresContentAttributes)
        {
            return [];
        }

        var documentFrequencies = CalculateDocumentFrequencies(
            intent.Terms,
            documents,
            cancellationToken);
        var results = new List<ContentSearchCandidate>();
        var matchedDocuments = 0;
        var averageLength = Math.Max(
            1d,
            documents.Average(document => Math.Max(1, document.Text.Length)));

        for (var index = 0; index < documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = documents[index];
            if (!MatchesHardFilters(intent, document))
            {
                continue;
            }

            var contentFacts = new SearchResultTextFacts(
                ContentKnown: true,
                SearchTextAttributeAnalyzer.Analyze(document.Text),
                document.Source);
            var attributeMatches = intent.AttributePredicates
                .Select(predicate => new
                {
                    Predicate = predicate,
                    Match = SearchTextAttributeAnalyzer.Evaluate(
                        predicate,
                        document.Name,
                        document.DirectoryPath,
                        contentFacts)
                })
                .ToArray();
            if (attributeMatches.Any(item =>
                    item.Match != SearchAttributeMatch.Match))
            {
                continue;
            }
            if (intent.Terms.Count == 0)
            {
                matchedDocuments++;
                results.Add(new ContentSearchCandidate(
                    document,
                    380d + attributeMatches.Length * 80d,
                    BuildAttributeReason(
                        attributeMatches.Select(item =>
                            item.Predicate),
                        document),
                    MatchedTermCount: 1,
                    TotalTermCount: 1,
                    MetadataMatchedTermCount: 0));
                continue;
            }

            var matchedTerms = new List<string>();
            var contentMatchedTerms = new List<ContentTermMatch>();
            var metadataMatchedTerms = new List<string>();
            var matchedPositions = new List<int>();
            var lexicalScore = 0d;
            for (var termIndex = 0; termIndex < intent.Terms.Count; termIndex++)
            {
                var term = intent.Terms[termIndex];
                var termFrequency = 0;
                var firstPosition = int.MaxValue;
                string? matchedContentText = null;
                foreach (var alternative in
                         term.ContentEvidenceAlternatives)
                {
                    var frequency = CountOccurrences(
                        document.Text,
                        alternative,
                        maximum: 8);
                    if (frequency <= 0)
                    {
                        continue;
                    }

                    var position = document.Text.IndexOf(
                        alternative,
                        StringComparison.OrdinalIgnoreCase);
                    if (frequency > termFrequency ||
                        (frequency == termFrequency && position < firstPosition))
                    {
                        termFrequency = frequency;
                        firstPosition = Math.Max(0, position);
                        matchedContentText = alternative;
                    }
                }

                var matchedName = ContainsAlternative(
                    document.Name,
                    term.Alternatives);
                var matchedPath = !matchedName &&
                    ContainsAlternative(
                        document.DirectoryPath,
                        term.Alternatives);
                if (termFrequency == 0)
                {
                    if (matchedName || matchedPath)
                    {
                        matchedTerms.Add(term.Original);
                        metadataMatchedTerms.Add(term.Original);
                        lexicalScore += matchedName ? 1.4d : 0.65d;
                    }

                    continue;
                }

                matchedTerms.Add(term.Original);
                contentMatchedTerms.Add(new ContentTermMatch(
                    term.Original,
                    matchedContentText ?? term.Original));
                if (matchedName || matchedPath)
                {
                    metadataMatchedTerms.Add(term.Original);
                }
                matchedPositions.Add(firstPosition);
                var documentFrequency = documentFrequencies[termIndex];
                var inverseDocumentFrequency = Math.Log(
                    1d +
                    (documents.Count - documentFrequency + 0.5d) /
                    (documentFrequency + 0.5d));
                var lengthRatio = document.Text.Length / averageLength;
                const double k1 = 1.2d;
                const double b = 0.72d;
                var normalizedFrequency =
                    termFrequency * (k1 + 1d) /
                    (termFrequency + k1 * (1d - b + b * lengthRatio));
                lexicalScore += inverseDocumentFrequency * normalizedFrequency;
            }

            if (!HasSufficientCoverage(intent.Terms.Count, matchedTerms.Count))
            {
                continue;
            }
            if (contentMatchedTerms.Count == 0)
            {
                // Title-only evidence belongs in the independent title and
                // metadata searches. The integrated content lane must add
                // evidence that came from inside the file.
                continue;
            }

            var contextCoherence = CalculateContextCoherence(matchedPositions);
            if (intent.Terms.Count > 1 &&
                matchedTerms.Count < intent.Terms.Count &&
                contextCoherence < 0.35d)
            {
                continue;
            }

            matchedDocuments++;
            var coverage = (double)matchedTerms.Count / intent.Terms.Count;
            var score =
                120d +
                lexicalScore * 22d +
                coverage * 100d +
                contextCoherence * 85d;
            score += contentMatchedTerms.Count * 32d;
            if (matchedTerms.Count == intent.Terms.Count)
            {
                score += 45d;
            }

            results.Add(new ContentSearchCandidate(
                document,
                score,
                BuildReason(
                    contentMatchedTerms,
                    metadataMatchedTerms,
                    document,
                    contextCoherence),
                matchedTerms.Count,
                intent.Terms.Count,
                metadataMatchedTerms.Count));

            if ((index + 1) % 1_000 == 0)
            {
                progress?.Report(new SearchProgress(
                    index + 1,
                    matchedDocuments,
                    document.DirectoryPath,
                    SearchPhase.ContentSearching));
            }
        }

        progress?.Report(new SearchProgress(
            documents.Count,
            matchedDocuments,
            string.Empty,
            SearchPhase.ContentSearching));

        var candidateLimit = (int)Math.Min(
            2_000L,
            Math.Max(maximumResults, (long)maximumResults * 6L));
        return results
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Document.ModifiedUtc)
            .ThenBy(
                candidate => candidate.Document.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .Take(candidateLimit)
            .ToArray();
    }

    private static bool HasSufficientCoverage(
        int totalTermCount,
        int matchedTermCount)
    {
        if (matchedTermCount <= 0 || totalTermCount <= 0)
        {
            return false;
        }

        if (totalTermCount == 1)
        {
            return true;
        }

        var minimumMatches = Math.Max(
            2,
            (int)Math.Ceiling(totalTermCount * 0.5d));
        return matchedTermCount >= minimumMatches;
    }

    private static int[] CalculateDocumentFrequencies(
        IReadOnlyList<SearchTerm> terms,
        IReadOnlyList<ContentDocumentRecord> documents,
        CancellationToken cancellationToken)
    {
        var frequencies = new int[terms.Count];
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < terms.Count; index++)
            {
                if (terms[index].ContentEvidenceAlternatives.Any(
                        alternative => document.Text.Contains(
                            alternative,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    frequencies[index]++;
                }
            }
        }

        return frequencies;
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

    private static double CalculateContextCoherence(
        IReadOnlyList<int> positions)
    {
        if (positions.Count <= 1)
        {
            return positions.Count == 1 ? 1d : 0d;
        }

        var ordered = positions.OrderBy(position => position).ToArray();
        var span = ordered[^1] - ordered[0];
        return span switch
        {
            <= 160 => 1d,
            <= 500 => 0.78d,
            <= 1_500 => 0.48d,
            <= 4_000 => 0.28d,
            _ => 0.12d
        };
    }

    private static int CountOccurrences(
        string text,
        string value,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var count = 0;
        var position = 0;
        while (position < text.Length && count < maximum)
        {
            var found = text.IndexOf(
                value,
                position,
                StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                break;
            }

            count++;
            position = found + Math.Max(1, value.Length);
        }

        return count;
    }

    private static bool ContainsAlternative(
        string text,
        IReadOnlyList<string> alternatives) =>
        alternatives.Any(alternative =>
            !string.IsNullOrWhiteSpace(alternative) &&
            text.Contains(
                alternative,
                StringComparison.OrdinalIgnoreCase));

    private static string BuildReason(
        IReadOnlyCollection<ContentTermMatch> contentMatchedTerms,
        IReadOnlyCollection<string> metadataMatchedTerms,
        ContentDocumentRecord document,
        double contextCoherence)
    {
        var terms = string.Join(
            ", ",
            contentMatchedTerms
                .DistinctBy(
                    match => match.QueryTerm,
                    StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .Select(FormatContentTermMatch));
        var combinedText = metadataMatchedTerms.Count > 0
            ? " 파일명·폴더 단서와 함께 일치합니다."
            : string.Empty;
        var contextText = contextCoherence >= 0.75d
            ? " 같은 문맥에서 함께 나타납니다."
            : " 관련 단서를 확인했습니다.";
        return document.Source switch
        {
            DocumentContentSource.Spreadsheet =>
                $"엑셀 시트·셀에서 {terms} 내용을 확인했습니다.{combinedText}",
            DocumentContentSource.ImageOcr =>
                $"이미지 OCR에서 {terms} 글자가{contextText}",
            DocumentContentSource.PdfOcr =>
                document.WasTruncated
                    ? $"PDF 표본 {Math.Max(1, document.AnalyzedPages)}쪽의 OCR 앞부분에서 {terms}가{contextText}"
                    : $"PDF 표본 {Math.Max(1, document.AnalyzedPages)}쪽의 OCR에서 {terms}가{contextText}",
            _ =>
                document.WasTruncated
                    ? $"본문 앞부분에서 {terms}가{contextText}{combinedText}"
                    : $"파일 본문에서 {terms}가{contextText}{combinedText}"
        };
    }

    private static string FormatContentTermMatch(ContentTermMatch match)
    {
        return match.QueryTerm.Equals(
            match.MatchedText,
            StringComparison.OrdinalIgnoreCase)
            ? $"‘{match.QueryTerm}’"
            : $"‘{match.QueryTerm}’(실제 일치 ‘{match.MatchedText}’)";
    }

    private static string BuildAttributeReason(
        IEnumerable<SearchTextAttributePredicate> predicates,
        ContentDocumentRecord document)
    {
        var condition = string.Join(
            "·",
            predicates.Select(predicate => predicate.Description));
        return document.Source switch
        {
            DocumentContentSource.Spreadsheet =>
                $"엑셀 시트·셀에서 {condition} 조건을 확인했습니다.",
            DocumentContentSource.ImageOcr =>
                $"이미지 OCR에서 {condition} 조건을 확인했습니다.",
            DocumentContentSource.PdfOcr =>
                $"PDF OCR에서 {condition} 조건을 확인했습니다.",
            _ => $"파일 본문에서 {condition} 조건을 확인했습니다."
        };
    }
}

public sealed record ContentTermMatch(
    string QueryTerm,
    string MatchedText);

public sealed record ContentSearchCandidate(
    ContentDocumentRecord Document,
    double Score,
    string Reason,
    int MatchedTermCount,
    int TotalTermCount,
    int MetadataMatchedTermCount)
{
    public double Coverage =>
        TotalTermCount <= 0
            ? 0d
            : Math.Clamp(
                (double)MatchedTermCount / TotalTermCount,
                0d,
                1d);
}
