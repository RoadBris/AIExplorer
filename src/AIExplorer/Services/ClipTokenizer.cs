using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public sealed partial class ClipTokenizer
{
    public const int ContextLength = 77;
    public const long StartTokenId = 49_406;
    public const long EndTokenId = 49_407;

    private readonly Dictionary<string, long> _vocabulary;
    private readonly Dictionary<string, int> _mergeRanks;
    private readonly Dictionary<byte, char> _byteEncoder;
    private readonly Dictionary<string, IReadOnlyList<string>> _cache =
        new(StringComparer.Ordinal);
    private readonly object _cacheLock = new();

    public ClipTokenizer(string tokenizerPath)
    {
        using var stream = new FileStream(
            tokenizerPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.SequentialScan);
        using var document = JsonDocument.Parse(stream);
        var model = document.RootElement.GetProperty("model");
        _vocabulary = model.GetProperty("vocab")
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetInt64(),
                StringComparer.Ordinal);
        _mergeRanks = new Dictionary<string, int>(
            StringComparer.Ordinal);
        var rank = 0;
        foreach (var merge in model.GetProperty("merges").EnumerateArray())
        {
            var values = merge.EnumerateArray().ToArray();
            if (values.Length != 2)
            {
                continue;
            }

            _mergeRanks[
                PairKey(
                    values[0].GetString() ?? string.Empty,
                    values[1].GetString() ?? string.Empty)] = rank++;
        }

        _byteEncoder = BuildByteEncoder();
    }

    public TokenizedText Encode(string text)
    {
        var normalized = Normalize(text);
        var tokenIds = new List<long>(ContextLength)
        {
            StartTokenId
        };
        foreach (Match match in TokenRegex().Matches(normalized))
        {
            var bytes = Encoding.UTF8.GetBytes(match.Value);
            var encoded = new string(
                bytes.Select(value => _byteEncoder[value]).ToArray());
            foreach (var token in ApplyBpe(encoded))
            {
                tokenIds.Add(
                    _vocabulary.TryGetValue(token, out var tokenId)
                        ? tokenId
                        : EndTokenId);
                if (tokenIds.Count >= ContextLength - 1)
                {
                    break;
                }
            }

            if (tokenIds.Count >= ContextLength - 1)
            {
                break;
            }
        }

        tokenIds.Add(EndTokenId);
        var inputIds = Enumerable.Repeat(
                EndTokenId,
                ContextLength)
            .ToArray();
        var attentionMask = new long[ContextLength];
        for (var index = 0; index < tokenIds.Count; index++)
        {
            inputIds[index] = tokenIds[index];
            attentionMask[index] = 1;
        }

        return new TokenizedText(inputIds, attentionMask);
    }

    private IReadOnlyList<string> ApplyBpe(string token)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(token, out var cached))
            {
                return cached;
            }
        }

        if (token.Length == 0)
        {
            return [];
        }

        var symbols = token
            .Select(character => character.ToString())
            .ToList();
        symbols[^1] += "</w>";

        while (symbols.Count > 1)
        {
            var bestRank = int.MaxValue;
            string? bestLeft = null;
            string? bestRight = null;
            for (var index = 0; index < symbols.Count - 1; index++)
            {
                if (!_mergeRanks.TryGetValue(
                        PairKey(symbols[index], symbols[index + 1]),
                        out var pairRank) ||
                    pairRank >= bestRank)
                {
                    continue;
                }

                bestRank = pairRank;
                bestLeft = symbols[index];
                bestRight = symbols[index + 1];
            }

            if (bestLeft is null || bestRight is null)
            {
                break;
            }

            var merged = new List<string>(symbols.Count);
            for (var index = 0; index < symbols.Count;)
            {
                if (index < symbols.Count - 1 &&
                    symbols[index] == bestLeft &&
                    symbols[index + 1] == bestRight)
                {
                    merged.Add(bestLeft + bestRight);
                    index += 2;
                }
                else
                {
                    merged.Add(symbols[index]);
                    index++;
                }
            }

            symbols = merged;
        }

        lock (_cacheLock)
        {
            if (_cache.Count >= 4_096)
            {
                _cache.Clear();
            }

            _cache[token] = symbols;
        }

        return symbols;
    }

    private static Dictionary<byte, char> BuildByteEncoder()
    {
        var visibleBytes = Enumerable.Range('!', '~' - '!' + 1)
            .Concat(Enumerable.Range('¡', '¬' - '¡' + 1))
            .Concat(Enumerable.Range('®', 'ÿ' - '®' + 1))
            .ToList();
        var unicodePoints = visibleBytes.ToList();
        var extraIndex = 0;
        for (var value = 0; value < 256; value++)
        {
            if (visibleBytes.Contains(value))
            {
                continue;
            }

            visibleBytes.Add(value);
            unicodePoints.Add(256 + extraIndex++);
        }

        return visibleBytes
            .Select(
                (value, index) =>
                    new KeyValuePair<byte, char>(
                        (byte)value,
                        (char)unicodePoints[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static string Normalize(string text)
    {
        var normalized = text
            .Normalize(NormalizationForm.FormC)
            .ToLower(CultureInfo.InvariantCulture);
        return WhitespaceRegex().Replace(normalized, " ").Trim();
    }

    private static string PairKey(string left, string right) =>
        left + "\u001F" + right;

    [GeneratedRegex(
        @"<\|startoftext\|>|<\|endoftext\|>|'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed record TokenizedText(
    long[] InputIds,
    long[] AttentionMask);
