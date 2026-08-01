using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public enum SearchPlanTarget
{
    Any,
    File,
    Folder
}

public enum SearchPlanSort
{
    Relevance,
    CreatedNewest,
    CreatedOldest,
    ModifiedNewest,
    ModifiedOldest,
    NameMatch,
    PathMatch,
    LargeFirst,
    SmallFirst
}

public sealed record SearchPlanTerm(
    string Term,
    IReadOnlyList<string> Alternatives);

public sealed record SearchPlan(
    string OriginalQuery,
    IReadOnlyList<SearchPlanTerm> TermGroups,
    IReadOnlyList<string> RequestedExtensions,
    SearchPlanTarget Target,
    SearchPlanSort Sort,
    bool UsePreviousResults,
    double Confidence,
    string Interpretation,
    bool UsedLanguageModel)
{
    public static SearchPlan FromDeterministic(
        SearchIntent intent,
        SearchConversationContext? context = null)
    {
        var referencesPreviousResults =
            context is { ResultCount: > 0 } &&
            SearchPlanCompiler.ReferencesPreviousResults(
                intent.OriginalQuery);
        var target = intent.DirectoryOnly
            ? SearchPlanTarget.Folder
            : intent.FilesOnly
                ? SearchPlanTarget.File
                : SearchPlanTarget.Any;
        return new SearchPlan(
            intent.OriginalQuery,
            intent.MetadataTerms
                .Select(term => new SearchPlanTerm(
                    term.Original,
                    term.Alternatives))
                .ToArray(),
            intent.RequestedExtensions.ToArray(),
            target,
            SearchPlanCompiler.ResolveSort(intent.RankingProfile),
            referencesPreviousResults,
            1d,
            intent.Summary,
            false);
    }
}

public sealed record SearchConversationContext(
    string PreviousQuery,
    int ResultCount);

public sealed record NaturalLanguageSearchInterpretation(
    SearchIntent Intent,
    SearchPlan Plan,
    bool LanguageModelAvailable,
    string DisplaySummary)
{
    public bool ShouldRefinePreviousResults =>
        Plan.UsePreviousResults;
}

public static partial class SearchPlanCompiler
{
    public static NaturalLanguageSearchInterpretation Compile(
        SearchIntent deterministicIntent,
        SearchPlan plan,
        bool languageModelAvailable)
    {
        // The deterministic parser is the authority for executable search
        // filters. A generative plan may explain the query, but it must never
        // invent a term (for example "기타"), an extension, a file-only
        // constraint, or a ranking rule that the user did not request.
        var deterministicPlan = SearchPlan.FromDeterministic(
            deterministicIntent);
        var safePlan = deterministicPlan with
        {
            UsePreviousResults =
                deterministicPlan.UsePreviousResults &&
                plan.UsePreviousResults,
            Confidence = Math.Clamp(plan.Confidence, 0d, 1d),
            Interpretation = deterministicIntent.Summary,
            UsedLanguageModel =
                languageModelAvailable && plan.UsedLanguageModel
        };
        var displaySummary = BuildDisplaySummary(
            deterministicIntent,
            safePlan);
        return new NaturalLanguageSearchInterpretation(
            deterministicIntent,
            safePlan,
            languageModelAvailable,
            displaySummary);
    }

    public static bool ReferencesPreviousResults(string query) =>
        PreviousResultReferenceRegex().IsMatch(query);

    public static SearchPlanSort ResolveSort(
        SearchRankingProfile profile)
    {
        var directive = profile.Directives
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.Weight)
            .FirstOrDefault();
        if (directive is null)
        {
            return SearchPlanSort.Relevance;
        }

        return (directive.Feature, directive.Direction) switch
        {
            (SearchRankingFeature.CreatedRecency,
                SearchRankingDirection.HigherFirst) =>
                SearchPlanSort.CreatedNewest,
            (SearchRankingFeature.CreatedRecency,
                SearchRankingDirection.LowerFirst) =>
                SearchPlanSort.CreatedOldest,
            (SearchRankingFeature.ModifiedRecency,
                SearchRankingDirection.HigherFirst) =>
                SearchPlanSort.ModifiedNewest,
            (SearchRankingFeature.ModifiedRecency,
                SearchRankingDirection.LowerFirst) =>
                SearchPlanSort.ModifiedOldest,
            (SearchRankingFeature.NameMatch, _) =>
                SearchPlanSort.NameMatch,
            (SearchRankingFeature.PathMatch, _) =>
                SearchPlanSort.PathMatch,
            (SearchRankingFeature.FileSize,
                SearchRankingDirection.HigherFirst) =>
                SearchPlanSort.LargeFirst,
            (SearchRankingFeature.FileSize,
                SearchRankingDirection.LowerFirst) =>
                SearchPlanSort.SmallFirst,
            _ => SearchPlanSort.Relevance
        };
    }

    private static SearchRankingProfile BuildRankingProfile(
        SearchPlanSort sort)
    {
        var directive = sort switch
        {
            SearchPlanSort.CreatedNewest => CreateDirective(
                SearchRankingFeature.CreatedRecency,
                SearchRankingDirection.HigherFirst,
                "최근 생성 우선"),
            SearchPlanSort.CreatedOldest => CreateDirective(
                SearchRankingFeature.CreatedRecency,
                SearchRankingDirection.LowerFirst,
                "오래된 생성일 우선"),
            SearchPlanSort.ModifiedNewest => CreateDirective(
                SearchRankingFeature.ModifiedRecency,
                SearchRankingDirection.HigherFirst,
                "최근 수정 우선"),
            SearchPlanSort.ModifiedOldest => CreateDirective(
                SearchRankingFeature.ModifiedRecency,
                SearchRankingDirection.LowerFirst,
                "오래된 수정일 우선"),
            SearchPlanSort.NameMatch => CreateDirective(
                SearchRankingFeature.NameMatch,
                SearchRankingDirection.HigherFirst,
                "파일명 일치 우선"),
            SearchPlanSort.PathMatch => CreateDirective(
                SearchRankingFeature.PathMatch,
                SearchRankingDirection.HigherFirst,
                "경로 일치 우선"),
            SearchPlanSort.LargeFirst => CreateDirective(
                SearchRankingFeature.FileSize,
                SearchRankingDirection.HigherFirst,
                "큰 파일 우선"),
            SearchPlanSort.SmallFirst => CreateDirective(
                SearchRankingFeature.FileSize,
                SearchRankingDirection.LowerFirst,
                "작은 파일 우선"),
            _ => null
        };
        return directive is null
            ? SearchRankingProfile.Default
            : new SearchRankingProfile([directive]);
    }

    private static SearchRankingDirective CreateDirective(
        SearchRankingFeature feature,
        SearchRankingDirection direction,
        string description) =>
        new(
            feature,
            direction,
            SearchRankingStrength.Strong,
            0.85d,
            true,
            description);

    private static string BuildDisplaySummary(
        SearchIntent intent,
        SearchPlan plan)
    {
        var parts = new List<string>
        {
            intent.Classification.DisplayLabel
        };
        if (intent.DirectoryOnly)
        {
            parts.Add("폴더");
        }
        else if (intent.FilesOnly)
        {
            parts.Add("파일");
        }

        var terms = intent.MetadataTerms
            .Select(term => term.Original)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (terms.Length > 0)
        {
            parts.Add(string.Join(" · ", terms));
        }

        if (intent.RequestedExtensions.Count > 0)
        {
            parts.Add(string.Join(
                "/",
                intent.RequestedExtensions
                    .Select(extension =>
                        extension.TrimStart('.').ToUpperInvariant())
                    .OrderBy(value => value)));
        }

        if (intent.RankingProfile.HasPreferences)
        {
            parts.Add(intent.RankingProfile.Summary);
        }

        if (plan.UsePreviousResults)
        {
            parts.Add("현재 결과 안에서");
        }

        return parts.Count > 0
            ? string.Join("  |  ", parts)
            : intent.Summary;
    }

    private static string NormalizePlanTerm(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static string? NormalizeExtension(string value)
    {
        var extension = value.Trim().ToLowerInvariant();
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        return ExtensionRegex().IsMatch(extension)
            ? extension
            : null;
    }

    [GeneratedRegex(
        @"^\.[a-z0-9][a-z0-9_+-]{0,31}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionRegex();

    [GeneratedRegex(
        @"(?:그\s*중|그\s*결과|검색\s*결과|결과\s*(?:안|내|중|에서)|" +
        @"방금\s*(?:찾은|검색한)|여기서|거기서)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PreviousResultReferenceRegex();
}
