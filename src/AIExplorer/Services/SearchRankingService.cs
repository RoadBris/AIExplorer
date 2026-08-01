using AIExplorer.Models;

namespace AIExplorer.Services;

public static class SearchRankingService
{
    public static IReadOnlyList<SearchCandidate> FindCandidates(
        SearchIntent intent,
        IReadOnlyList<IndexedFileRecord> records,
        int maximumResults,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchCandidate>();
        var matched = 0;

        for (var index = 0; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = records[index];
            var candidate = ScoreCandidate(intent, record);
            if (candidate is not null)
            {
                results.Add(candidate);
                matched++;
            }

            if ((index + 1) % 2_000 == 0)
            {
                progress?.Report(new SearchProgress(
                    index + 1,
                    matched,
                    record.DirectoryPath,
                    SearchPhase.Searching));
            }
        }

        progress?.Report(new SearchProgress(
            records.Count,
            matched,
            string.Empty,
            SearchPhase.Searching));

        var candidateLimit = (int)Math.Min(
            5_000L,
            Math.Max(maximumResults, (long)maximumResults * 12L));
        return results
            .OrderByDescending(candidate => candidate.RankingScore)
            .ThenByDescending(candidate => candidate.Record.ModifiedUtc)
            .ThenBy(candidate => candidate.Record.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(candidateLimit)
            .ToArray();
    }

    public static SearchCandidate? ScoreCandidate(
        SearchIntent intent,
        IndexedFileRecord record)
    {
        if (intent.DirectoryOnly && !record.IsDirectory)
        {
            return null;
        }
        if (intent.FilesOnly && record.IsDirectory)
        {
            return null;
        }

        if (intent.ModifiedFromUtc is not null &&
            record.ModifiedUtc < intent.ModifiedFromUtc.Value)
        {
            return null;
        }

        if (intent.ModifiedToUtc is not null &&
            record.ModifiedUtc >= intent.ModifiedToUtc.Value)
        {
            return null;
        }

        var category = record.IsDirectory
            ? FileCategory.Other
            : FileTypeCatalog.GetCategory(record.Extension);
        var categoryMatch = record.IsDirectory ||
                            intent.Categories.Count == 0 ||
                            intent.Categories.Contains(category);
        if (!categoryMatch)
        {
            return null;
        }

        var extensionMatch = record.IsDirectory ||
                             intent.RequestedExtensions.Count == 0 ||
                             intent.RequestedExtensions.Contains(
                                 record.Extension,
                                 StringComparer.OrdinalIgnoreCase);
        if (!extensionMatch)
        {
            return null;
        }

        // Extensions are part of a file's searchable metadata. This keeps
        // uncommon and future formats discoverable even when they are not in
        // FileTypeCatalog (for example PPK, PEM, or vendor-specific formats).
        var nameWords = SearchQueryInterpreter.TokenizeText(record.Name);
        var pathWords = SearchQueryInterpreter.TokenizeText(record.DirectoryPath);
        var typeWords = FileMetadataDescriptor.GetSearchTerms(
            record.Extension);
        var matchedAttributePredicates = new List<string>();
        foreach (var predicate in intent.AttributePredicates)
        {
            var attributeMatch = SearchTextAttributeAnalyzer.Evaluate(
                predicate,
                record.Name,
                record.DirectoryPath,
                contentFacts: null,
                record.IsDirectory);
            if (attributeMatch == SearchAttributeMatch.NoMatch)
            {
                return null;
            }
            if (attributeMatch == SearchAttributeMatch.Match)
            {
                matchedAttributePredicates.Add(
                    predicate.Description);
            }
        }
        var metadataTerms = intent.Terms
            .Concat(intent.LiteralTerms)
            .DistinctBy(
                term => term.Original,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var floorMatch =
            SearchTextAnalyzer.ContainsAllFloorReferences(
                intent.FloorReferences,
                $"{record.Name} {record.DirectoryPath}");
        if (!floorMatch)
        {
            return null;
        }
        var nameMatches = new List<string>();
        var pathMatches = new List<string>();
        var typeMatches = new List<string>();
        var matchedTermSet = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var originalMatchedTermSet = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var originalNameMatches = 0;
        var originalPathMatches = 0;
        var originalTypeMatches = 0;
        var score = 0d;
        score += matchedAttributePredicates.Count * 240d;

        foreach (var term in metadataTerms)
        {
            var specificity = 1d + Math.Min(term.Original.Length, 8) * 0.08d;
            var nameMatch = GetBestMatch(nameWords, term);
            if (nameMatch.Strength > 0d)
            {
                nameMatches.Add(term.Original);
                matchedTermSet.Add(term.Original);
                if (nameMatch.UsedOriginalTerm)
                {
                    originalNameMatches++;
                    originalMatchedTermSet.Add(term.Original);
                }

                score +=
                    (nameMatch.UsedOriginalTerm ? 185d : 128d) *
                    specificity *
                    nameMatch.Strength;
            }

            var pathMatch = GetBestMatch(pathWords, term);
            if (pathMatch.Strength > 0d)
            {
                pathMatches.Add(term.Original);
                matchedTermSet.Add(term.Original);
                if (pathMatch.UsedOriginalTerm)
                {
                    originalPathMatches++;
                    originalMatchedTermSet.Add(term.Original);
                }

                score +=
                    (pathMatch.UsedOriginalTerm ? 58d : 39d) *
                    specificity *
                    pathMatch.Strength *
                    (nameMatch.Strength > 0d ? 0.65d : 1d);
            }

            var typeMatch = GetBestMatch(typeWords, term);
            if (typeMatch.Strength > 0d)
            {
                typeMatches.Add(term.Original);
                matchedTermSet.Add(term.Original);
                if (typeMatch.UsedOriginalTerm)
                {
                    originalTypeMatches++;
                    originalMatchedTermSet.Add(term.Original);
                }

                score +=
                    (typeMatch.UsedOriginalTerm ? 115d : 82d) *
                    specificity *
                    typeMatch.Strength *
                    (nameMatch.Strength > 0d ? 0.72d : 1d);
            }
        }

        var matchedTerms = matchedTermSet.Count;
        var coreTermNames = intent.Terms
            .Select(term => term.Original)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var literalTermNames = intent.LiteralTerms
            .Select(term => term.Original)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var coreMatchedTerms = matchedTermSet.Count(coreTermNames.Contains);
        var literalDirectMatches = nameMatches
            .Concat(pathMatches)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(literalTermNames.Contains);
        if (intent.IsSingleTermNameLookup && nameMatches.Count == 0)
        {
            return null;
        }
        if (intent.Terms.Count > 0 &&
            coreMatchedTerms == 0 &&
            literalDirectMatches == 0)
        {
            return null;
        }
        if (intent.Terms.Count == 0 &&
            metadataTerms.Length > 0 &&
            matchedTerms == 0 &&
            intent.Categories.Count == 0 &&
            intent.RequestedExtensions.Count == 0 &&
            intent.FloorReferences.Count == 0 &&
            !intent.DirectoryOnly)
        {
            return null;
        }

        if (metadataTerms.Length > 1)
        {
            var coverage = (double)matchedTerms / metadataTerms.Length;
            if (coverage < 0.5d)
            {
                // Preserve low-coverage candidates for later path, type, and
                // semantic corroboration instead of discarding them early.
                score *= 0.72d;
            }

            score += originalMatchedTermSet.Count * 28d;
            score -= Math.Max(0, metadataTerms.Length - matchedTerms) * 18d;
        }

        if (metadataTerms.Length > 0)
        {
            var coverage = (double)matchedTerms / metadataTerms.Length;
            score += 170d * coverage * coverage;
            score += nameMatches.Count * 26d;
            score += originalNameMatches * 34d;
            score += originalPathMatches * 8d;
            score += typeMatches.Count * 18d;
            score += originalTypeMatches * 12d;

            if (matchedTerms == metadataTerms.Length)
            {
                score += 190d;
            }

            if (nameMatches.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                metadataTerms.Length)
            {
                score += 230d;
            }

            var corroboratedTerms = nameMatches
                .Intersect(
                    pathMatches.Concat(typeMatches),
                    StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            score += corroboratedTerms * 42d;

            var normalizedName = string.Join(" ", nameWords);
            var originalPhrase = string.Join(
                " ",
                metadataTerms.Select(term => term.Original));
            if (originalPhrase.Length >= 3 &&
                normalizedName.Contains(
                    originalPhrase,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 260d;
            }
        }

        if (intent.RequestedExtensions.Count > 0)
        {
            score += 180d;
        }
        else if (intent.Categories.Count > 0)
        {
            score += 120d;
        }

        if (intent.ModifiedFromUtc is not null || intent.ModifiedToUtc is not null)
        {
            score += 70d;
        }

        if (intent.DirectoryOnly)
        {
            score += 80d;
        }

        if (intent.FloorReferences.Count > 0)
        {
            score += 210d;
        }

        if (record.IsDirectory &&
            (intent.Categories.Count > 0 ||
             intent.RequestedExtensions.Count > 0) &&
            nameMatches.Count == 0 &&
            pathMatches.Count == 0 &&
            intent.FloorReferences.Count == 0)
        {
            return null;
        }

        if (score <= 0)
        {
            return null;
        }

        var termCount = Math.Max(1, metadataTerms.Length);
        var preferenceScore =
            SearchRankingPreferenceService.CalculatePreferenceScore(
                intent.RankingProfile,
                new SearchRankingSignals(
                    record.CreatedUtc,
                    record.ModifiedUtc,
                    record.SizeBytes,
                    Math.Clamp(nameMatches.Count / (double)termCount, 0d, 1d),
                    Math.Clamp(pathMatches.Count / (double)termCount, 0d, 1d),
                    Math.Clamp(typeMatches.Count / (double)termCount, 0d, 1d),
                    0d,
                    0d));
        var relevanceTier = metadataTerms.Length > 0 &&
                            nameMatches.Count >= metadataTerms.Length
            ? 3
            : metadataTerms.Length > 0 &&
              matchedTerms >= metadataTerms.Length
                ? 2
                : 1;
        return new SearchCandidate(
            record,
            score,
            BuildReason(
                intent,
                record,
                category,
                nameMatches,
                pathMatches,
                typeMatches,
                matchedAttributePredicates),
            nameMatches.Count,
            pathMatches.Count,
            typeMatches.Count,
            matchedTerms,
            preferenceScore,
            relevanceTier);
    }

    private static TermMatch GetBestMatch(
        IReadOnlyCollection<string> candidateWords,
        SearchTerm term)
    {
        var best = new TermMatch(0d, false);
        var allowCompactPartialMatch =
            AllowsCompactLanguagePartialMatch(term.Original);
        foreach (var word in candidateWords)
        {
            if (word.Equals(term.Original, StringComparison.OrdinalIgnoreCase))
            {
                return new TermMatch(1d, true);
            }

            if ((term.Original.Length >= 3 ||
                 allowCompactPartialMatch) &&
                word.StartsWith(term.Original, StringComparison.OrdinalIgnoreCase))
            {
                best = Better(best, new TermMatch(0.92d, true));
            }
            else if ((term.Original.Length >= 4 ||
                      allowCompactPartialMatch) &&
                     word.Contains(
                         term.Original,
                         StringComparison.OrdinalIgnoreCase))
            {
                best = Better(best, new TermMatch(0.80d, true));
            }
        }

        foreach (var alternative in term.Alternatives)
        {
            if (alternative.Equals(
                    term.Original,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var allowAlternativeCompactMatch =
                AllowsCompactLanguagePartialMatch(alternative);
            foreach (var word in candidateWords)
            {
                if (word.Equals(alternative, StringComparison.OrdinalIgnoreCase))
                {
                    best = Better(best, new TermMatch(0.78d, false));
                }
                else if ((alternative.Length >= 3 ||
                          allowAlternativeCompactMatch) &&
                         word.StartsWith(alternative, StringComparison.OrdinalIgnoreCase))
                {
                    best = Better(best, new TermMatch(0.68d, false));
                }
                else if ((alternative.Length >= 4 ||
                          allowAlternativeCompactMatch) &&
                         word.Contains(
                             alternative,
                             StringComparison.OrdinalIgnoreCase))
                {
                    best = Better(best, new TermMatch(0.56d, false));
                }
            }
        }

        if (term.Original.Any(character =>
                character is >= '\uAC00' and <= '\uD7A3'))
        {
            foreach (var word in candidateWords)
            {
                var phoneticSimilarity =
                    KoreanEnglishPhoneticMatcher.CalculateBestSimilarity(
                        word,
                        term.Original);
                if (phoneticSimilarity >= 0.76d)
                {
                    best = Better(
                        best,
                        new TermMatch(
                            0.62d +
                            Math.Min(0.16d, phoneticSimilarity - 0.76d),
                            false));
                }
            }
        }

        return best;
    }

    private static bool AllowsCompactLanguagePartialMatch(string term) =>
        term.Length >= 2 &&
        term.Any(character =>
            character is >= '\uAC00' and <= '\uD7A3' or
                >= '\u3400' and <= '\u9FFF');

    private static TermMatch Better(TermMatch left, TermMatch right) =>
        right.Strength > left.Strength ? right : left;

    private readonly record struct TermMatch(
        double Strength,
        bool UsedOriginalTerm);

    private static string BuildReason(
        SearchIntent intent,
        IndexedFileRecord record,
        FileCategory category,
        IReadOnlyCollection<string> nameMatches,
        IReadOnlyCollection<string> pathMatches,
        IReadOnlyCollection<string> typeMatches,
        IReadOnlyCollection<string> attributeMatches)
    {
        var reasons = new List<string>();
        if (intent.RequestedExtensions.Count > 0)
        {
            reasons.Add($"{record.Extension.ToUpperInvariant()} 형식");
        }
        else if (intent.Categories.Count > 0 && !record.IsDirectory)
        {
            reasons.Add($"{FileTypeCatalog.GetCategoryLabel(category)} 형식");
        }

        if (nameMatches.Count > 0)
        {
            reasons.Add(
                $"파일명: {string.Join(", ", nameMatches.Select(term => $"‘{term}’"))}");
        }
        if (pathMatches.Count > 0)
        {
            reasons.Add(
                $"경로: {string.Join(", ", pathMatches.Select(term => $"‘{term}’"))}");
        }

        if (typeMatches.Count > 0)
        {
            reasons.Add(
                $"형식 의미: {string.Join(", ", typeMatches.Select(term => $"‘{term}’"))}");
        }

        if (intent.ModifiedFromUtc is not null || intent.ModifiedToUtc is not null)
        {
            reasons.Add("요청한 수정 시기");
        }

        if (intent.DirectoryOnly)
        {
            reasons.Add("폴더");
        }

        if (attributeMatches.Count > 0)
        {
            reasons.Add(string.Join(
                "·",
                attributeMatches));
        }

        if (intent.FloorReferences.Count > 0)
        {
            reasons.Add(string.Join(
                "·",
                intent.FloorReferences.Select(reference =>
                    $"{reference.Display} 위치")));
        }

        return reasons.Count > 0
            ? string.Join(" · ", reasons) + "과 일치합니다."
            : "검색 조건과 일치합니다.";
    }
}

public sealed record SearchCandidate(
    IndexedFileRecord Record,
    double Score,
    string Reason,
    int NameMatchCount,
    int PathMatchCount,
    int TypeMatchCount = 0,
    int UniqueMatchedTermCount = -1,
    double PreferenceScore = 0d,
    int RelevanceTier = 1)
{
    public double RankingScore =>
        RelevanceTier * 1_000_000d +
        Score +
        PreferenceScore * 1_000d;

    public int MatchedTermCount =>
        UniqueMatchedTermCount >= 0
            ? UniqueMatchedTermCount
            : NameMatchCount + PathMatchCount + TypeMatchCount;
}
