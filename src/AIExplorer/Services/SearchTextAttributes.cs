using System.Text;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public enum SearchTextScript
{
    Hangul,
    Latin,
    Digit
}

public enum SearchTextAttributeScope
{
    Name,
    Path,
    Content,
    NameOrContent
}

public enum SearchTextAttributeMode
{
    Contains,
    Excludes,
    Only,
    Predominantly
}

public enum SearchAttributeMatch
{
    NoMatch,
    Match,
    Unknown
}

public sealed record SearchTextAttributePredicate(
    SearchTextScript Script,
    SearchTextAttributeScope Scope,
    SearchTextAttributeMode Mode,
    string Description);

public sealed record SearchAttributeParseResult(
    string RemainingQuery,
    IReadOnlyList<SearchTextAttributePredicate> Predicates,
    bool FilesOnly,
    bool HwpFormatRequested)
{
    public bool HasPredicates => Predicates.Count > 0;

    public string Summary =>
        string.Join(
            " · ",
            Predicates.Select(predicate => predicate.Description));
}

public readonly record struct SearchCharacterProfile(
    int HangulCharacters,
    int LatinCharacters,
    int DigitCharacters,
    int LetterCharacters)
{
    public int MeaningfulCharacters =>
        LetterCharacters + DigitCharacters;
}

public sealed record SearchResultTextFacts(
    bool ContentKnown,
    SearchCharacterProfile ContentProfile,
    DocumentContentSource? Source = null);

public static class SearchTextAttributeAnalyzer
{
    public static SearchCharacterProfile Analyze(string text)
    {
        var hangul = 0;
        var latin = 0;
        var digits = 0;
        var letters = 0;
        foreach (var character in text.Normalize(NormalizationForm.FormKC))
        {
            if (IsHangul(character))
            {
                hangul++;
                letters++;
            }
            else if (character is >= 'a' and <= 'z' or
                     >= 'A' and <= 'Z')
            {
                latin++;
                letters++;
            }
            else if (char.IsDigit(character))
            {
                digits++;
            }
            else if (char.IsLetter(character))
            {
                letters++;
            }
        }

        return new SearchCharacterProfile(
            hangul,
            latin,
            digits,
            letters);
    }

    public static bool IsMatch(
        SearchCharacterProfile profile,
        SearchTextScript script,
        SearchTextAttributeMode mode)
    {
        var count = script switch
        {
            SearchTextScript.Hangul => profile.HangulCharacters,
            SearchTextScript.Latin => profile.LatinCharacters,
            SearchTextScript.Digit => profile.DigitCharacters,
            _ => 0
        };
        if (mode == SearchTextAttributeMode.Excludes)
        {
            return count == 0;
        }
        if (mode == SearchTextAttributeMode.Contains)
        {
            return count > 0;
        }
        if (mode == SearchTextAttributeMode.Only)
        {
            return count > 0 &&
                   count == profile.MeaningfulCharacters;
        }

        var denominator = Math.Max(1, profile.MeaningfulCharacters);
        return count >= 5 &&
               count / (double)denominator >= 0.6d;
    }

    public static SearchAttributeMatch Evaluate(
        SearchTextAttributePredicate predicate,
        string name,
        string path,
        SearchResultTextFacts? contentFacts,
        bool isDirectory = false)
    {
        var searchableName = isDirectory
            ? name
            : Path.GetFileNameWithoutExtension(name);
        var nameMatch = IsMatch(
            Analyze(searchableName),
            predicate.Script,
            predicate.Mode);
        var pathMatch = IsMatch(
            Analyze(path),
            predicate.Script,
            predicate.Mode);
        var contentMatch = contentFacts is { ContentKnown: true }
            ? IsMatch(
                contentFacts.ContentProfile,
                predicate.Script,
                predicate.Mode)
            : (bool?)null;

        return predicate.Scope switch
        {
            SearchTextAttributeScope.Name =>
                nameMatch
                    ? SearchAttributeMatch.Match
                    : SearchAttributeMatch.NoMatch,
            SearchTextAttributeScope.Path =>
                pathMatch
                    ? SearchAttributeMatch.Match
                    : SearchAttributeMatch.NoMatch,
            SearchTextAttributeScope.Content =>
                contentMatch is null
                    ? SearchAttributeMatch.Unknown
                    : contentMatch.Value
                        ? SearchAttributeMatch.Match
                        : SearchAttributeMatch.NoMatch,
            _ => EvaluateNameOrContent(
                predicate.Mode,
                nameMatch,
                contentMatch)
        };
    }

    private static SearchAttributeMatch EvaluateNameOrContent(
        SearchTextAttributeMode mode,
        bool nameMatch,
        bool? contentMatch)
    {
        if (mode == SearchTextAttributeMode.Excludes)
        {
            if (!nameMatch)
            {
                return SearchAttributeMatch.NoMatch;
            }

            return contentMatch is null
                ? SearchAttributeMatch.Unknown
                : contentMatch.Value
                    ? SearchAttributeMatch.Match
                    : SearchAttributeMatch.NoMatch;
        }

        if (nameMatch || contentMatch == true)
        {
            return SearchAttributeMatch.Match;
        }

        return contentMatch is null
            ? SearchAttributeMatch.Unknown
            : SearchAttributeMatch.NoMatch;
    }

    private static bool IsHangul(char character) =>
        character is >= '\uAC00' and <= '\uD7A3' or
            >= '\u1100' and <= '\u11FF' or
            >= '\u3130' and <= '\u318F' or
            >= '\uA960' and <= '\uA97F' or
            >= '\uD7B0' and <= '\uD7FF';
}

public static class SearchAttributeQueryParser
{
    private static readonly Regex AttributeRegex = new(
        @"(?:(?<scope>파일\s*명|파일\s*이름|이름|제목|본문|내용|문서\s*(?:안|속)|경로|폴더\s*명)\s*(?:에|에는|에서|이|가|은|는|중에|중에서)?\s*)?(?:(?<exclusive>전부|오직)\s*)?(?<script>한글|한국어|영문|영어|알파벳|숫자)(?:이|가|를|로)?\s*(?<scriptOnly>만(?:으로)?\s*)?(?<relation>되어\s*있는|들어간|들어있는|포함된|포함한|이루어진|구성된|사용된|사용한|작성된|적힌|쓰인|쓴|포함|있는|없는|없음|제외한|제외|된)",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly Regex ReverseAttributeRegex = new(
        @"(?:(?<exclusive>전부|오직)\s*)?(?<script>한글|한국어|영문|영어|알파벳|숫자)(?:이|가|를|로)?\s*(?<scriptOnly>만(?:으로)?\s*)?(?<relation>되어\s*있는|포함된|이루어진|구성된|사용된|사용한|작성된|적힌|쓰인|쓴|있는|없는|된)\s*(?<scope>파일\s*명|파일\s*이름|이름|제목|본문|내용|경로|폴더\s*명)",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly Regex FilesOnlyRegex = new(
        @"파일(?:들)?\s*만",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly Regex FileNameScopeRegex = new(
        @"파일\s*(?:명|이름)",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly Regex FilesTargetRegex = new(
        @"(?<![\p{L}\p{N}])파일(?!\s*(?:명|이름))(?:들)?(?:만|을|를|이|가|은|는)?(?![\p{L}\p{N}])",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private static readonly Regex HwpFormatRegex = new(
        @"(?<![\p{L}\p{N}])한글\s*(?:파일|문서)(?:만|을|를|이|가|은|는)?(?![\p{L}\p{N}])",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    public static SearchAttributeParseResult Parse(string query)
    {
        var predicates = new List<SearchTextAttributePredicate>();
        var remaining = query;
        foreach (var regex in new[] { ReverseAttributeRegex, AttributeRegex })
        {
            var matches = regex.Matches(remaining)
                .Cast<Match>()
                .Where(match => match.Success)
                .ToArray();
            foreach (var match in matches)
            {
                predicates.Add(CreatePredicate(match));
            }

            remaining = regex.Replace(remaining, " ");
        }

        var filesOnly =
            FilesOnlyRegex.IsMatch(query) ||
            predicates.Count > 0 &&
            (FilesTargetRegex.IsMatch(query) ||
             FileNameScopeRegex.IsMatch(query));
        if (predicates.Count > 0)
        {
            remaining = FilesOnlyRegex.Replace(remaining, " ");
            remaining = FilesTargetRegex.Replace(remaining, " ");
        }

        var hwpFormatRequested =
            predicates.Count == 0 &&
            HwpFormatRegex.IsMatch(query);
        if (hwpFormatRequested)
        {
            remaining = HwpFormatRegex.Replace(remaining, " ");
        }

        return new SearchAttributeParseResult(
            remaining,
            predicates
                .Distinct()
                .ToArray(),
            filesOnly,
            hwpFormatRequested);
    }

    private static SearchTextAttributePredicate CreatePredicate(
        Match match)
    {
        var scriptText = match.Groups["script"].Value;
        var script = scriptText switch
        {
            "영문" or "영어" or "알파벳" => SearchTextScript.Latin,
            "숫자" => SearchTextScript.Digit,
            _ => SearchTextScript.Hangul
        };
        var relation = match.Groups["relation"].Value;
        var mode =
            relation.Contains("없", StringComparison.Ordinal) ||
            relation.Contains("제외", StringComparison.Ordinal)
                ? SearchTextAttributeMode.Excludes
                : match.Groups["scriptOnly"].Success ||
                  match.Groups["exclusive"].Success
                    ? SearchTextAttributeMode.Only
                : relation is "작성된" or "쓰인"
                    ? SearchTextAttributeMode.Predominantly
                    : SearchTextAttributeMode.Contains;
        var scope = ResolveScope(match.Groups["scope"].Value);
        if (scope == SearchTextAttributeScope.NameOrContent &&
            mode == SearchTextAttributeMode.Only)
        {
            scope = SearchTextAttributeScope.Name;
        }
        if (scope == SearchTextAttributeScope.NameOrContent &&
            relation is "작성된" or "쓰인")
        {
            scope = SearchTextAttributeScope.Content;
        }
        var scriptLabel = script switch
        {
            SearchTextScript.Latin => "영문",
            SearchTextScript.Digit => "숫자",
            _ => mode == SearchTextAttributeMode.Predominantly
                ? "한국어"
                : "한글"
        };
        var scopeLabel = scope switch
        {
            SearchTextAttributeScope.Name => "파일명",
            SearchTextAttributeScope.Path => "경로",
            SearchTextAttributeScope.Content => "내용",
            _ => "파일명+내용"
        };
        var modeLabel = mode switch
        {
            SearchTextAttributeMode.Excludes => "없음",
            SearchTextAttributeMode.Only => "만",
            SearchTextAttributeMode.Predominantly => "중심",
            _ => "포함"
        };
        return new SearchTextAttributePredicate(
            script,
            scope,
            mode,
            $"{scopeLabel} {scriptLabel} {modeLabel}");
    }

    private static SearchTextAttributeScope ResolveScope(string value)
    {
        var compact = Regex.Replace(value, @"\s+", string.Empty);
        if (compact.Contains("파일명", StringComparison.Ordinal) ||
            compact is "이름" or "제목")
        {
            return SearchTextAttributeScope.Name;
        }
        if (compact.Contains("경로", StringComparison.Ordinal) ||
            compact.Contains("폴더명", StringComparison.Ordinal))
        {
            return SearchTextAttributeScope.Path;
        }
        if (compact.Contains("본문", StringComparison.Ordinal) ||
            compact.Contains("내용", StringComparison.Ordinal) ||
            compact.Contains("문서안", StringComparison.Ordinal) ||
            compact.Contains("문서속", StringComparison.Ordinal))
        {
            return SearchTextAttributeScope.Content;
        }

        return SearchTextAttributeScope.NameOrContent;
    }
}
