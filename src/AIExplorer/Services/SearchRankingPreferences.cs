namespace AIExplorer.Services;

public enum SearchRankingFeature
{
    CreatedRecency,
    ModifiedRecency,
    NameMatch,
    PathMatch,
    TypeMatch,
    ContentMatch,
    SemanticMatch,
    FileSize
}

public enum SearchRankingDirection
{
    HigherFirst,
    LowerFirst
}

public enum SearchRankingStrength
{
    Slight,
    Normal,
    Strong,
    Dominant
}

public sealed record SearchRankingDirective(
    SearchRankingFeature Feature,
    SearchRankingDirection Direction,
    SearchRankingStrength Strength,
    double Weight,
    bool IsPrimary,
    string Description,
    double HalfLifeDays = 90d);

public sealed record SearchRankingProfile(
    IReadOnlyList<SearchRankingDirective> Directives)
{
    public static SearchRankingProfile Default { get; } = new([]);

    public bool HasPreferences => Directives.Count > 0;

    public bool HasPrimaryPreference =>
        Directives.Any(directive => directive.IsPrimary);

    public bool RequiresFileTimestamps =>
        Directives.Any(directive =>
            directive.Feature is
                SearchRankingFeature.CreatedRecency or
                SearchRankingFeature.ModifiedRecency or
                SearchRankingFeature.FileSize);

    public string Summary =>
        HasPreferences
            ? string.Join(
                " · ",
                Directives
                    .Select(directive => directive.Description)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
            : string.Empty;
}

public readonly record struct SearchRankingSignals(
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    long? SizeBytes,
    double NameMatch,
    double PathMatch,
    double TypeMatch,
    double ContentMatch,
    double SemanticMatch);

public static class SearchRankingPreferenceService
{
    private const double PreferenceScale = 1_000d;

    public static double CalculatePreferenceScore(
        SearchRankingProfile profile,
        SearchRankingSignals signals,
        DateTime? utcNow = null,
        bool primaryOnly = false)
    {
        if (!profile.HasPreferences)
        {
            return 0d;
        }

        var now = utcNow ?? DateTime.UtcNow;
        var weightedScore = 0d;
        var totalWeight = 0d;
        foreach (var directive in profile.Directives)
        {
            if (primaryOnly && !directive.IsPrimary)
            {
                continue;
            }

            var weight = Math.Clamp(directive.Weight, 0.01d, 1d);
            var featureScore = GetFeatureScore(directive, signals, now);
            weightedScore += featureScore * weight;
            totalWeight += weight;
        }

        return totalWeight <= double.Epsilon
            ? 0d
            : Math.Clamp(weightedScore / totalWeight, 0d, 1d);
    }

    public static double CalculateAdjustment(
        SearchRankingProfile profile,
        SearchRankingSignals signals,
        DateTime? utcNow = null) =>
        CalculatePreferenceScore(profile, signals, utcNow) *
        profile.Directives.Sum(directive =>
            Math.Clamp(directive.Weight, 0.01d, 1d)) *
        PreferenceScale;

    public static DateTime GetEffectiveCreatedUtc(
        DateTime createdUtc,
        DateTime modifiedUtc,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return createdUtc >= new DateTime(1970, 1, 1) &&
               createdUtc <= now.AddDays(1)
            ? createdUtc
            : modifiedUtc;
    }

    public static string BuildAppliedReason(SearchRankingProfile profile) =>
        profile.HasPreferences
            ? $"사용자 우선순위({profile.Summary})를 반영했습니다."
            : string.Empty;

    private static double GetFeatureScore(
        SearchRankingDirective directive,
        SearchRankingSignals signals,
        DateTime utcNow)
    {
        var score = directive.Feature switch
        {
            SearchRankingFeature.CreatedRecency => GetRecencyScore(
                GetEffectiveCreatedUtc(
                    signals.CreatedUtc,
                    signals.ModifiedUtc,
                    utcNow),
                directive.HalfLifeDays,
                utcNow),
            SearchRankingFeature.ModifiedRecency => GetRecencyScore(
                signals.ModifiedUtc,
                directive.HalfLifeDays,
                utcNow),
            SearchRankingFeature.NameMatch => signals.NameMatch,
            SearchRankingFeature.PathMatch => signals.PathMatch,
            SearchRankingFeature.TypeMatch => signals.TypeMatch,
            SearchRankingFeature.ContentMatch => signals.ContentMatch,
            SearchRankingFeature.SemanticMatch => signals.SemanticMatch,
            SearchRankingFeature.FileSize => GetSizeScore(signals.SizeBytes),
            _ => 0d
        };

        score = Math.Clamp(score, 0d, 1d);
        return directive.Direction == SearchRankingDirection.LowerFirst
            ? 1d - score
            : score;
    }

    private static double GetRecencyScore(
        DateTime timestampUtc,
        double halfLifeDays,
        DateTime utcNow)
    {
        if (timestampUtc == default)
        {
            return 0d;
        }

        var ageDays = Math.Max(
            0d,
            (utcNow - timestampUtc.ToUniversalTime()).TotalDays);
        var safeHalfLife = Math.Clamp(halfLifeDays, 1d, 3_650d);
        return Math.Pow(0.5d, ageDays / safeHalfLife);
    }

    private static double GetSizeScore(long? sizeBytes)
    {
        if (sizeBytes is null || sizeBytes <= 0)
        {
            return 0d;
        }

        // Normalize logarithmically from roughly 1 KB to 10 GB.
        return Math.Clamp(
            (Math.Log10(sizeBytes.Value) - 3d) / 7d,
            0d,
            1d);
    }
}
