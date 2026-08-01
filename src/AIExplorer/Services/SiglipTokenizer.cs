using Microsoft.ML.Tokenizers;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

/// <summary>
/// SigLIP 2의 Gemma/SentencePiece 토크나이저 설정을 재현합니다.
/// 입력은 소문자로 정규화하고 EOS를 포함해 64토큰으로 맞춥니다.
/// </summary>
public sealed partial class SiglipTokenizer
{
    public const int ContextLength = 64;
    public const long PaddingTokenId = 0;
    public const long EndTokenId = 1;

    private readonly SentencePieceTokenizer _tokenizer;

    public SiglipTokenizer(string tokenizerPath)
    {
        using var stream = new FileStream(
            tokenizerPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.SequentialScan);
        _tokenizer = SentencePieceTokenizer.Create(
            stream,
            false,
            true);
    }

    public TokenizedText Encode(string text)
    {
        var normalized = WhitespaceRegex()
            .Replace(text.ToLowerInvariant(), " ")
            .Trim();
        var encoded = _tokenizer.EncodeToIds(
            normalized,
            false,
            true);
        var tokenIds = encoded
            .Select(value => (long)value)
            .ToList();

        if (tokenIds.Count == 0 || tokenIds[^1] != EndTokenId)
        {
            tokenIds.Add(EndTokenId);
        }

        if (tokenIds.Count > ContextLength)
        {
            tokenIds = tokenIds
                .Take(ContextLength - 1)
                .Append(EndTokenId)
                .ToList();
        }

        var inputIds = Enumerable.Repeat(
                PaddingTokenId,
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

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
