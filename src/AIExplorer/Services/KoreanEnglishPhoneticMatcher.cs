using System.Text;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public static partial class KoreanEnglishPhoneticMatcher
{
    private static readonly string[] Initials =
    [
        "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s",
        "ss", "", "j", "jj", "ch", "k", "t", "p", "h"
    ];

    private static readonly string[] Vowels =
    [
        "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o",
        "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu",
        "eu", "ui", "i"
    ];

    private static readonly string[] Finals =
    [
        "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m",
        "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng",
        "t", "t", "k", "t", "p", "h"
    ];

    public static IReadOnlyList<string> BuildAliases(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !text.Any(IsHangulSyllable))
        {
            return [];
        }

        var revised = Romanize(text);
        var loanword = revised
            .Replace("eu", "u", StringComparison.Ordinal)
            .Replace("eo", "o", StringComparison.Ordinal)
            .Replace("ae", "e", StringComparison.Ordinal)
            .Replace("ui", "i", StringComparison.Ordinal);
        var foreignConsonants = loanword
            .Replace("p", "f", StringComparison.Ordinal)
            .Replace("j", "z", StringComparison.Ordinal)
            .Replace("r", "l", StringComparison.Ordinal)
            .Replace("b", "v", StringComparison.Ordinal);
        var compactForeign = foreignConsonants
            .Replace("ai", "i", StringComparison.Ordinal)
            .Replace("oi", "i", StringComparison.Ordinal);

        return new[] { revised, loanword, foreignConsonants, compactForeign }
            .Select(NormalizeLatin)
            .Where(alias => alias.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsMatch(string candidateText, string hangulTerm) =>
        CalculateBestSimilarity(candidateText, hangulTerm) >= 0.76d;

    public static double CalculateBestSimilarity(
        string candidateText,
        string hangulTerm)
    {
        var aliases = BuildAliases(hangulTerm);
        if (aliases.Count == 0)
        {
            return 0d;
        }

        var candidateWords = LatinWordRegex()
            .Matches(candidateText.ToLowerInvariant())
            .Select(match => NormalizeLatin(match.Value))
            .Where(word => word.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var best = 0d;
        foreach (var word in candidateWords)
        {
            foreach (var alias in aliases)
            {
                foreach (var fragment in EnumerateCandidateFragments(
                             word,
                             alias.Length))
                {
                    var maximumLength = Math.Max(
                        fragment.Length,
                        alias.Length);
                    if (maximumLength == 0 ||
                        Math.Abs(fragment.Length - alias.Length) >
                        Math.Max(3, maximumLength / 2))
                    {
                        continue;
                    }

                    var similarity = 1d -
                                     CalculateEditDistance(
                                         fragment,
                                         alias) /
                                     (double)maximumLength;
                    best = Math.Max(best, similarity);

                    var wordSkeleton = BuildPhoneticSkeleton(fragment);
                    var aliasSkeleton = BuildPhoneticSkeleton(alias);
                    var skeletonLength = Math.Max(
                        wordSkeleton.Length,
                        aliasSkeleton.Length);
                    if (Math.Min(
                            wordSkeleton.Length,
                            aliasSkeleton.Length) >= 3)
                    {
                        var skeletonSimilarity = 1d -
                            CalculateEditDistance(
                                wordSkeleton,
                                aliasSkeleton) /
                            (double)skeletonLength;
                        best = Math.Max(best, skeletonSimilarity);
                    }
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> EnumerateCandidateFragments(
        string word,
        int targetLength)
    {
        yield return word;
        if (word.Length <= targetLength + 2)
        {
            yield break;
        }

        var minimumLength = Math.Max(4, targetLength - 2);
        var maximumLength = Math.Min(word.Length - 1, targetLength + 2);
        for (var length = minimumLength;
             length <= maximumLength;
             length++)
        {
            for (var start = 0; start <= word.Length - length; start++)
            {
                yield return word.Substring(start, length);
            }
        }
    }

    private static string BuildPhoneticSkeleton(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previous = '\0';
        foreach (var character in value)
        {
            var code = character switch
            {
                'a' or 'e' or 'i' or 'o' or 'u' or 'y' => '\0',
                'b' or 'p' or 'f' or 'v' => 'F',
                'c' or 'g' or 'k' or 'q' => 'K',
                'd' or 't' => 'T',
                'j' or 'z' => 'Z',
                'l' or 'r' => 'L',
                's' or 'x' => 'S',
                'm' => 'M',
                'n' => 'N',
                'h' => 'H',
                'w' => 'W',
                _ => '\0'
            };
            if (code == '\0' || code == previous)
            {
                continue;
            }

            builder.Append(code);
            previous = code;
        }

        return builder.ToString();
    }

    private static string Romanize(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        foreach (var character in text)
        {
            if (!IsHangulSyllable(character))
            {
                builder.Append(character);
                continue;
            }

            var syllable = character - 0xAC00;
            var initial = syllable / (21 * 28);
            var vowel = syllable % (21 * 28) / 28;
            var final = syllable % 28;
            builder.Append(Initials[initial]);
            builder.Append(Vowels[vowel]);
            builder.Append(Finals[final]);
        }

        return builder.ToString();
    }

    private static string NormalizeLatin(string value) =>
        new(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static int CalculateEditDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1;
                 rightIndex <= right.Length;
                 rightIndex++)
            {
                var substitution = previous[rightIndex - 1] +
                                   (left[leftIndex - 1] ==
                                    right[rightIndex - 1]
                                       ? 0
                                       : 1);
                current[rightIndex] = Math.Min(
                    Math.Min(
                        previous[rightIndex] + 1,
                        current[rightIndex - 1] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static bool IsHangulSyllable(char character) =>
        character is >= '\uAC00' and <= '\uD7A3';

    [GeneratedRegex("[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex LatinWordRegex();
}
