using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public static class SearchTextAnalyzer
{
    private static readonly Regex FloorRegex = new(
        @"(?:(?<underground>지하)[\s._-]*0*(?<basement>\d{1,3})[\s._-]*층|(?<![a-z0-9])b[\s._-]*0*(?<englishBasement>\d{1,3})[\s._-]*(?:f|층)?(?![a-z0-9])|(?<![\p{L}\p{N}])0*(?<ground>\d{1,3})[\s._-]*층)",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<SearchFloorReference> ExtractFloorReferences(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = NormalizeUnicode(text);
        return FloorRegex
            .Matches(normalized)
            .Cast<Match>()
            .Select(ToFloorReference)
            .Where(reference => reference is not null)
            .Cast<SearchFloorReference>()
            .Distinct()
            .ToArray();
    }

    public static string RemoveFloorReferences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return FloorRegex.Replace(NormalizeUnicode(text), " ");
    }

    public static bool ContainsAllFloorReferences(
        IReadOnlyCollection<SearchFloorReference> requested,
        string candidateText)
    {
        if (requested.Count == 0)
        {
            return true;
        }

        var candidateReferences = ExtractFloorReferences(candidateText)
            .ToHashSet();
        return requested.All(candidateReferences.Contains);
    }

    public static string NormalizeForMatching(
        string text,
        bool compact = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = NormalizeUnicode(text).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!compact && !previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return compact
            ? builder.ToString()
            : builder.ToString().Trim();
    }

    private static SearchFloorReference? ToFloorReference(Match match)
    {
        var isUnderground =
            match.Groups["basement"].Success ||
            match.Groups["englishBasement"].Success;
        var numberText = match.Groups["basement"].Success
            ? match.Groups["basement"].Value
            : match.Groups["englishBasement"].Success
                ? match.Groups["englishBasement"].Value
                : match.Groups["ground"].Value;
        return int.TryParse(
            numberText,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number) &&
            number is > 0 and <= 999
                ? new SearchFloorReference(isUnderground, number)
                : null;
    }

    private static string NormalizeUnicode(string text) =>
        text.Normalize(NormalizationForm.FormKC);
}

public sealed record SearchFloorReference(
    bool IsUnderground,
    int Number)
{
    public string Display =>
        IsUnderground
            ? $"지하 {Number}층"
            : $"{Number}층";
}
