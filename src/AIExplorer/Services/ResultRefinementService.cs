using AIExplorer.Models;

namespace AIExplorer.Services;

public static class ResultRefinementService
{
    public static ResultRefinementResult Refine(
        string query,
        IReadOnlyList<SearchResult> source,
        DateTime? utcNow = null) =>
        Refine(
            query,
            source,
            contentFacts: null,
            utcNow);

    public static ResultRefinementResult Refine(
        string query,
        IReadOnlyList<SearchResult> source,
        IReadOnlyDictionary<string, SearchResultTextFacts>? contentFacts,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ResultRefinementResult(
                source.ToArray(),
                false,
                source.Count,
                source.Count,
                0,
                string.Empty,
                string.Empty);
        }

        var intent = SearchQueryInterpreter.Interpret(query);
        return Refine(
            intent,
            source,
            contentFacts,
            utcNow);
    }

    public static ResultRefinementResult Refine(
        SearchIntent intent,
        IReadOnlyList<SearchResult> source,
        IReadOnlyDictionary<string, SearchResultTextFacts>? contentFacts = null,
        DateTime? utcNow = null)
    {
        if (!intent.HasCriteria)
        {
            return new ResultRefinementResult(
                source.ToArray(),
                false,
                source.Count,
                source.Count,
                0,
                "구체적인 단어, 파일 형식, 층수 또는 정렬 조건을 입력해 주세요.",
                string.Empty);
        }

        var terms = intent.MetadataTerms;
        var ranked = new List<RefinedResult>(source.Count);
        var unknownResults = 0;
        for (var index = 0; index < source.Count; index++)
        {
            var result = source[index];
            var candidateText =
                $"{result.Name} {result.DirectoryPath} {result.TypeDisplay}";
            if (!SearchTextAnalyzer.ContainsAllFloorReferences(
                    intent.FloorReferences,
                    candidateText))
            {
                continue;
            }

            if (intent.DirectoryOnly && !result.IsDirectory)
            {
                continue;
            }
            if (intent.FilesOnly && result.IsDirectory)
            {
                continue;
            }

            SearchResultTextFacts? resultContentFacts = null;
            if (contentFacts is not null)
            {
                contentFacts.TryGetValue(
                    result.FullPath,
                    out resultContentFacts);
            }
            var attributeMatches = 0;
            var hasUnknownAttribute = false;
            var rejectedByAttribute = false;
            foreach (var predicate in intent.AttributePredicates)
            {
                var attributeMatch = SearchTextAttributeAnalyzer.Evaluate(
                    predicate,
                    result.Name,
                    result.DirectoryPath,
                    resultContentFacts,
                    result.IsDirectory);
                if (attributeMatch == SearchAttributeMatch.NoMatch)
                {
                    rejectedByAttribute = true;
                    break;
                }
                if (attributeMatch == SearchAttributeMatch.Unknown)
                {
                    hasUnknownAttribute = true;
                }
                else
                {
                    attributeMatches++;
                }
            }
            if (rejectedByAttribute)
            {
                continue;
            }
            if (hasUnknownAttribute)
            {
                unknownResults++;
                continue;
            }

            var extension = result.IsDirectory
                ? string.Empty
                : Path.GetExtension(result.FullPath);
            var category = result.IsDirectory
                ? FileCategory.Other
                : FileTypeCatalog.GetCategory(extension);
            var nameMatches = CountMatches(result.Name, terms);
            var pathMatches = CountMatches(result.DirectoryPath, terms);
            var typeMatches = CountMatches(
                $"{result.TypeDisplay} {extension} " +
                string.Join(
                    ' ',
                    FileMetadataDescriptor.GetSearchTerms(extension)),
                terms);
            var matchedTerms = terms
                .Where(term =>
                    ContainsTerm(result.Name, term) ||
                    ContainsTerm(result.DirectoryPath, term) ||
                    ContainsTerm(result.TypeDisplay, term) ||
                    ContainsTerm(extension, term))
                .Select(term => term.Original)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var requiredMatches = terms.Count == 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(terms.Count * 0.5d));
            if (matchedTerms < requiredMatches)
            {
                continue;
            }

            var categoryMatch = intent.Categories.Count == 0 ||
                                (!result.IsDirectory &&
                                 intent.Categories.Contains(category));
            var extensionMatch =
                intent.RequestedExtensions.Count == 0 ||
                (!result.IsDirectory &&
                 intent.RequestedExtensions.Contains(
                     extension,
                     StringComparer.OrdinalIgnoreCase));
            var hasDirectDirectoryEvidence =
                result.IsDirectory &&
                (nameMatches > 0 || pathMatches > 0 ||
                 intent.FloorReferences.Count > 0);
            if ((!categoryMatch || !extensionMatch) &&
                !hasDirectDirectoryEvidence)
            {
                continue;
            }

            var modifiedUtc = ResolveModifiedUtc(result);
            if (intent.ModifiedFromUtc is not null &&
                modifiedUtc < intent.ModifiedFromUtc.Value)
            {
                continue;
            }
            if (intent.ModifiedToUtc is not null &&
                modifiedUtc >= intent.ModifiedToUtc.Value)
            {
                continue;
            }

            var termCount = Math.Max(1, terms.Count);
            var nameCoverage = Math.Clamp(
                nameMatches / (double)termCount,
                0d,
                1d);
            var pathCoverage = Math.Clamp(
                pathMatches / (double)termCount,
                0d,
                1d);
            var typeCoverage = Math.Clamp(
                typeMatches / (double)termCount,
                0d,
                1d);
            var preferenceScore =
                SearchRankingPreferenceService.CalculatePreferenceScore(
                    intent.RankingProfile,
                    new SearchRankingSignals(
                        result.CreatedUtc,
                        modifiedUtc,
                        result.SizeBytes,
                        nameCoverage,
                        pathCoverage,
                        typeCoverage,
                        result.EvidenceKind is
                            SearchEvidenceKind.Content or
                            SearchEvidenceKind.Combined
                                ? result.MatchPercent / 100d
                                : 0d,
                        result.WasAiAnalyzed
                            ? result.MatchPercent / 100d
                            : 0d),
                    utcNow);
            var matchScore =
                matchedTerms * 100d +
                nameMatches * 55d +
                pathMatches * 22d +
                typeMatches * 18d +
                intent.FloorReferences.Count * 80d +
                attributeMatches * 120d;
            ranked.Add(new RefinedResult(
                result,
                index,
                matchScore,
                preferenceScore));
        }

        var ordered = ranked
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.PreferenceScore)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.Result)
            .ToArray();
        var summary = intent.RankingProfile.HasPreferences
            ? $"{source.Count:N0}개 중 {ordered.Length:N0}개 · " +
              intent.RankingProfile.Summary
            : $"{source.Count:N0}개 중 {ordered.Length:N0}개";
        if (unknownResults > 0)
        {
            summary += $" · 내용 미확인 {unknownResults:N0}개";
        }
        return new ResultRefinementResult(
            ordered,
            true,
            source.Count,
            ordered.Length,
            unknownResults,
            summary,
            intent.Summary);
    }

    private static int CountMatches(
        string text,
        IReadOnlyList<SearchTerm> terms) =>
        terms.Count(term => ContainsTerm(text, term));

    private static bool ContainsTerm(string text, SearchTerm term)
    {
        var normalized = SearchTextAnalyzer.NormalizeForMatching(text);
        var compact = SearchTextAnalyzer.NormalizeForMatching(
            text,
            compact: true);
        return term.Alternatives.Any(alternative =>
        {
            var normalizedAlternative =
                SearchTextAnalyzer.NormalizeForMatching(alternative);
            if (normalizedAlternative.Length == 0)
            {
                return false;
            }

            if (normalizedAlternative.Any(char.IsWhiteSpace))
            {
                return normalized.Contains(
                    normalizedAlternative,
                    StringComparison.OrdinalIgnoreCase);
            }

            var compactAlternative =
                SearchTextAnalyzer.NormalizeForMatching(
                    normalizedAlternative,
                    compact: true);
            if (compactAlternative.Any(character =>
                    character is >= '\uAC00' and <= '\uD7A3' or
                        >= '\u3400' and <= '\u9FFF'))
            {
                return compact.Contains(
                    compactAlternative,
                    StringComparison.OrdinalIgnoreCase);
            }

            return SearchQueryInterpreter
                .TokenizeText(normalized)
                .Any(word =>
                    word.Equals(
                        compactAlternative,
                        StringComparison.OrdinalIgnoreCase) ||
                    compactAlternative.Length >= 3 &&
                    word.StartsWith(
                        compactAlternative,
                        StringComparison.OrdinalIgnoreCase));
        });
    }

    private static DateTime ResolveModifiedUtc(SearchResult result)
    {
        if (result.ModifiedUtc != default)
        {
            return result.ModifiedUtc.ToUniversalTime();
        }

        return DateTime.TryParse(
            result.ModifiedDisplay,
            out var localTime)
                ? localTime.ToUniversalTime()
                : DateTime.MinValue;
    }

    private sealed record RefinedResult(
        SearchResult Result,
        int OriginalIndex,
        double MatchScore,
        double PreferenceScore);
}

public sealed record ResultRefinementResult(
    IReadOnlyList<SearchResult> Results,
    bool IsApplied,
    int SourceCount,
    int ResultCount,
    int UnknownCount,
    string Summary,
    string Interpretation);
