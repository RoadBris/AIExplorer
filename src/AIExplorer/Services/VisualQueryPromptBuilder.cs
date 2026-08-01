using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public static partial class VisualQueryPromptBuilder
{
    public const string UserInterfaceNegativePrompt =
        "software user interface screenshot, application window, " +
        "dashboard, menu, dialog box, document page with interface text";

    public const string GenericCharacterPrompt =
        "a high quality character portrait or character illustration, " +
        "centered fictional character";

    private static readonly string[] CharacterIntentTerms =
    [
        "캐릭터", "인물 일러스트", "팬아트", "팬 아트", "애니", "만화",
        "character", "fanart", "fan art", "anime", "cartoon"
    ];

    private static readonly string[] UserInterfaceIntentTerms =
    [
        "스크린샷", "화면 캡처", "화면캡처", "user interface",
        "인터페이스", "앱 화면", "프로그램 화면", "대시보드", "dialog",
        "screenshot"
    ];

    private static readonly string[] DocumentIntentTerms =
    [
        "문서", "영수증", "청구서", "계약서", "신분증", "명함", "pdf",
        "document", "receipt", "invoice", "scan"
    ];

    private static readonly string[] OfficeMaterialIntentTerms =
    [
        "사무 자료", "사무자료", "업무 자료", "업무자료", "회의 자료",
        "회의자료", "보고서 이미지", "서류", "양식", "스프레드시트",
        "엑셀 화면", "프레젠테이션", "슬라이드", "표가 있는", "그래프",
        "차트", "도표", "화이트보드", "office document",
        "business document", "spreadsheet", "presentation", "slide",
        "form", "chart", "whiteboard"
    ];

    private static readonly HashSet<string> GenericRequestTerms = new(
        [
            "사진", "이미지", "그림", "파일", "자료", "photo", "image",
            "picture", "file", "캐릭터", "character", "팬아트", "fanart",
            "anime"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] EnglishConcreteVisualTerms =
    [
        "person", "woman", "man", "family", "baby", "child", "face",
        "dog", "cat", "bird", "animal", "car", "vehicle", "motorcycle",
        "bicycle", "airplane", "train", "boat", "building", "house",
        "room", "office", "city", "street", "mountain", "ocean", "beach",
        "river", "lake", "forest", "tree", "flower", "sky", "cloud",
        "snow", "rain", "sunset", "sunrise", "food", "coffee", "cake",
        "wedding", "meeting", "product", "computer", "phone", "logo",
        "icon", "map", "chart", "poster", "red", "blue", "green", "yellow",
        "purple", "orange", "black", "white", "adult", "nude", "naked",
        "erotic", "explicit", "office document", "spreadsheet", "slide",
        "presentation", "form", "whiteboard", "diagram", "flowchart"
    ];

    private static readonly string[] HangulInitials =
    [
        "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s",
        "ss", "", "j", "jj", "ch", "k", "t", "p", "h"
    ];

    private static readonly string[] HangulVowels =
    [
        "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o",
        "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu",
        "eu", "ui", "i"
    ];

    private static readonly string[] HangulFinals =
    [
        "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m",
        "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng",
        "t", "t", "k", "t", "p", "h"
    ];

    private static readonly IReadOnlyList<KeyValuePair<string, string>>
        KoreanVisualTerms =
        [
            new("게임 캐릭터", "video game character"),
            new("애니메이션 캐릭터", "anime character"),
            new("인물 일러스트", "character portrait illustration"),
            new("캐릭터", "fictional character"),
            new("팬 아트", "fan art"),
            new("팬아트", "fan art"),
            new("애니메이션", "anime"),
            new("애니", "anime"),
            new("만화", "cartoon illustration"),
            new("일러스트", "illustration"),
            new("초상화", "portrait"),
            new("전신", "full body"),
            new("반신", "upper body"),
            new("스크린샷", "screenshot"),
            new("화면 캡처", "screenshot"),
            new("화면캡처", "screenshot"),
            new("사무 자료", "office business material"),
            new("사무자료", "office business material"),
            new("업무 자료", "business work material"),
            new("업무자료", "business work material"),
            new("회의 자료", "meeting document or presentation"),
            new("회의자료", "meeting document or presentation"),
            new("스프레드시트", "spreadsheet"),
            new("엑셀 화면", "spreadsheet screen"),
            new("프레젠테이션", "presentation slide"),
            new("슬라이드", "presentation slide"),
            new("화이트보드", "office whiteboard notes"),
            new("순서도", "flowchart"),
            new("도표", "business diagram or chart"),
            new("양식", "business form"),
            new("서류", "office document"),
            new("계좌", "bank account"),
            new("영수증", "receipt"),
            new("청구서", "invoice"),
            new("계약서", "contract document"),
            new("신분증", "identity card"),
            new("명함", "business card"),
            new("메뉴판", "menu"),
            new("바코드", "barcode"),
            new("큐알코드", "QR code"),
            new("QR코드", "QR code"),
            new("손글씨", "handwriting"),
            new("서명", "signature"),
            new("포스터", "poster"),
            new("표지", "cover"),
            new("문서", "document"),
            new("표가 있는", "table"),
            new("그래프", "chart graph"),
            new("차트", "chart"),
            new("지도", "map"),
            new("도면", "technical drawing"),
            new("설계", "design drawing"),
            new("로고", "logo"),
            new("아이콘", "icon"),
            new("증명사진", "portrait photo"),
            new("인물", "person portrait"),
            new("사람", "person"),
            new("가족", "family"),
            new("아기", "baby"),
            new("어린이", "child"),
            new("남자", "man"),
            new("여자", "woman"),
            new("성인물", "adult content"),
            new("성인 이미지", "adult image"),
            new("성인 사진", "adult photo"),
            new("누드", "nude"),
            new("나체", "nude person"),
            new("에로틱", "erotic"),
            new("노출", "revealing adult image"),
            new("얼굴", "face portrait"),
            new("강아지", "dog"),
            new("고양이", "cat"),
            new("새 사진", "bird photo"),
            new("조류", "bird"),
            new("동물", "animal"),
            new("자동차", "car"),
            new("차량", "vehicle"),
            new("오토바이", "motorcycle"),
            new("자전거", "bicycle"),
            new("비행기", "airplane"),
            new("기차", "train"),
            new("선박", "boat ship"),
            new("보트", "boat"),
            new("건물", "building"),
            new("주택", "house"),
            new("가옥", "house"),
            new("집 사진", "house photo"),
            new("방 사진", "room photo"),
            new("실내", "indoor room"),
            new("야외", "outdoor"),
            new("사무실", "office"),
            new("도시", "city"),
            new("거리", "street"),
            new("산 풍경", "mountain landscape"),
            new("산 사진", "mountain photo"),
            new("산악", "mountain"),
            new("바다", "ocean sea"),
            new("해변", "beach"),
            new("강물", "river"),
            new("강 사진", "river photo"),
            new("호수", "lake"),
            new("숲", "forest"),
            new("나무", "tree"),
            new("꽃", "flower"),
            new("하늘", "sky"),
            new("구름", "cloud"),
            new("설경", "snow landscape"),
            new("눈 내린", "snow"),
            new("비 오는", "rain"),
            new("우천", "rain"),
            new("노을", "sunset"),
            new("일출", "sunrise"),
            new("야경", "night city"),
            new("봄 풍경", "spring landscape"),
            new("여름", "summer"),
            new("가을", "autumn"),
            new("겨울", "winter"),
            new("음식", "food"),
            new("식사", "meal"),
            new("커피", "coffee"),
            new("케이크", "cake"),
            new("여행", "travel"),
            new("휴가", "vacation"),
            new("결혼식", "wedding"),
            new("생일", "birthday"),
            new("회의", "meeting"),
            new("제품", "product"),
            new("기계", "machine"),
            new("컴퓨터", "computer"),
            new("휴대폰", "smartphone"),
            new("모니터", "monitor"),
            new("사진", "photo"),
            new("이미지", "image"),
            new("그림", "illustration"),
            new("그려진", "illustration"),
            new("풍경", "landscape"),
            new("흑백", "black and white"),
            new("검은색", "black"),
            new("흰색", "white"),
            new("빨간", "red"),
            new("붉은", "red"),
            new("빨간색", "red"),
            new("파란", "blue"),
            new("푸른", "blue"),
            new("파란색", "blue"),
            new("초록", "green"),
            new("녹색", "green"),
            new("초록색", "green"),
            new("노란", "yellow"),
            new("노란색", "yellow"),
            new("보라", "purple"),
            new("보라색", "purple"),
            new("주황", "orange"),
            new("주황색", "orange"),
            new("밝은", "bright"),
            new("어두운", "dark")
        ];

    public static VisualQueryProfile Analyze(SearchIntent intent)
    {
        var query = intent.OriginalQuery.ToLowerInvariant();
        var tokens = SearchQueryInterpreter.TokenizeText(query);
        if (ContainsAny(query, UserInterfaceIntentTerms) ||
            tokens.Contains("ui", StringComparer.OrdinalIgnoreCase))
        {
            return new VisualQueryProfile(
                VisualQueryKind.UserInterface,
                SuppressUserInterface: false,
                IsNamedSubject: false,
                MinimumSimilarity: 0.05d,
                MaximumDistanceFromBest: 0.10d,
                MinimumUserInterfaceMargin: double.NegativeInfinity,
                MinimumNamedSubjectLift: double.NegativeInfinity);
        }

        var explicitlyCharacter =
            ContainsAny(query, CharacterIntentTerms);
        var namedSubject = IsLikelyNamedImageSubject(intent, query);
        if (explicitlyCharacter || namedSubject)
        {
            return new VisualQueryProfile(
                VisualQueryKind.Character,
                SuppressUserInterface: true,
                IsNamedSubject: namedSubject,
                MinimumSimilarity: namedSubject ? 0.075d : 0.07d,
                MaximumDistanceFromBest: 0.09d,
                MinimumUserInterfaceMargin: 0.015d,
                MinimumNamedSubjectLift:
                    namedSubject ? 0.02d : double.NegativeInfinity);
        }

        if (ContainsAny(query, OfficeMaterialIntentTerms))
        {
            return new VisualQueryProfile(
                VisualQueryKind.OfficeMaterial,
                SuppressUserInterface: false,
                IsNamedSubject: false,
                MinimumSimilarity: 0.05d,
                MaximumDistanceFromBest: 0.11d,
                MinimumUserInterfaceMargin: double.NegativeInfinity,
                MinimumNamedSubjectLift: double.NegativeInfinity);
        }

        if (ContainsAny(query, DocumentIntentTerms))
        {
            return new VisualQueryProfile(
                VisualQueryKind.Document,
                SuppressUserInterface: false,
                IsNamedSubject: false,
                MinimumSimilarity: 0.05d,
                MaximumDistanceFromBest: 0.10d,
                MinimumUserInterfaceMargin: double.NegativeInfinity,
                MinimumNamedSubjectLift: double.NegativeInfinity);
        }

        return new VisualQueryProfile(
            VisualQueryKind.General,
            SuppressUserInterface: false,
            IsNamedSubject: false,
            MinimumSimilarity: 0.055d,
            MaximumDistanceFromBest: 0.10d,
            MinimumUserInterfaceMargin: double.NegativeInfinity,
            MinimumNamedSubjectLift: double.NegativeInfinity);
    }

    public static string Build(string query) =>
        Build(
            query,
            Analyze(SearchQueryInterpreter.Interpret(query)));

    public static string Build(
        string query,
        VisualQueryProfile profile)
    {
        var subject = BuildSubject(query, profile);
        return profile.Kind switch
        {
            VisualQueryKind.Character =>
                $"{subject}, 캐릭터 일러스트, character illustration, " +
                "single fictional character",
            VisualQueryKind.UserInterface =>
                $"{subject}, 프로그램 화면, software user interface screenshot",
            VisualQueryKind.OfficeMaterial =>
                $"{subject}, 사무 문서 또는 업무 자료, office document, " +
                "business material, spreadsheet, presentation or diagram",
            VisualQueryKind.Document =>
                $"{subject}, 문서 페이지, document page or scanned document",
            _ => $"{subject}, 사진 또는 이미지, photo or image"
        };
    }

    public static IReadOnlyList<string> BuildVariants(
        string query,
        VisualQueryProfile profile)
    {
        var normalized = NormalizeQuery(query);
        var subject = BuildSubject(query, profile);
        var prompts = profile.Kind == VisualQueryKind.Character
            ? new[]
            {
                profile.IsNamedSubject ? subject : normalized,
                Build(query, profile),
                $"{subject}, 공식 캐릭터 그림, official character artwork",
                $"{subject}, 애니메이션 또는 게임 캐릭터, anime or game character"
            }
            : profile.Kind == VisualQueryKind.OfficeMaterial
                ? new[]
                {
                    normalized,
                    Build(query, profile),
                    $"{subject}, 표 차트 문서 슬라이드, table chart document slide",
                    $"{subject}, 스캔 서류 또는 사무실 자료, scanned office material"
                }
            : profile.Kind == VisualQueryKind.General
                ? new[]
                {
                    normalized,
                    Build(query, profile),
                    $"{subject}, 관련 이미지, image depicting the subject"
                }
                : new[]
                {
                    normalized,
                    Build(query, profile)
                };
        return prompts
            .Where(prompt => !string.IsNullOrWhiteSpace(prompt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> BuildIdentityAliases(
        SearchIntent intent)
    {
        var subjectTerms = intent.Terms
            .Select(term => term.Original)
            .Where(term => !GenericRequestTerms.Contains(term))
            .Where(term => !CharacterIntentTerms.Contains(
                term,
                StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (subjectTerms.Length == 0)
        {
            return [];
        }

        var aliases = new HashSet<string>(
            subjectTerms,
            StringComparer.OrdinalIgnoreCase);
        var romanized = BuildRomanizedAliases(
            string.Join(" ", subjectTerms));
        foreach (var alias in romanized.Split(
                     [' ', '/'],
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (alias.Length >= 2)
            {
                aliases.Add(alias);
            }
        }

        return aliases.ToArray();
    }

    public static IReadOnlyList<string> BuildTagAliases(SearchIntent intent)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in intent.Terms.Concat(intent.LiteralTerms))
        {
            aliases.Add(term.Original);
            foreach (var alternative in term.Alternatives)
            {
                aliases.Add(alternative);
            }
        }

        foreach (var pair in KoreanVisualTerms)
        {
            if (intent.OriginalQuery.Contains(
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(pair.Value);
            }
        }

        foreach (var alias in BuildIdentityAliases(intent))
        {
            aliases.Add(alias);
        }

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
    }

    private static string BuildSubject(
        string query,
        VisualQueryProfile profile)
    {
        var normalized = NormalizeQuery(query);
        var translated = normalized;
        var translatedAny = false;
        foreach (var pair in KoreanVisualTerms
                     .OrderByDescending(item => item.Key.Length))
        {
            if (!translated.Contains(
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            translated = translated.Replace(
                pair.Key,
                " " + pair.Value + " ",
                StringComparison.OrdinalIgnoreCase);
            translatedAny = true;
        }

        translated = KoreanParticleRegex().Replace(translated, " ");
        translated = WhitespaceRegex().Replace(translated, " ").Trim();
        var aliases = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            aliases.Add(normalized);
        }

        if (translatedAny &&
            !string.IsNullOrWhiteSpace(translated) &&
            !string.Equals(
                normalized,
                translated,
                StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add(translated);
        }

        if (profile.IsNamedSubject)
        {
            var romanizedAliases = BuildRomanizedAliases(normalized);
            if (!string.IsNullOrWhiteSpace(romanizedAliases))
            {
                aliases.Add(romanizedAliases);
            }
        }

        return aliases.Count == 0
            ? "요청한 대상, requested subject"
            : string.Join(", ", aliases.Distinct(
                StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeQuery(string query) =>
        WhitespaceRegex()
            .Replace(
                QueryNoiseRegex()
                    .Replace(query.ToLowerInvariant(), " "),
                " ")
            .Trim();

    public static bool HasKnownVisualConcept(string query) =>
        KoreanVisualTerms.Any(pair =>
            query.Contains(
                pair.Key,
                StringComparison.OrdinalIgnoreCase)) ||
        query.Contains("scan", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("photo", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("picture", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("image", StringComparison.OrdinalIgnoreCase);

    private static bool IsLikelyNamedImageSubject(
        SearchIntent intent,
        string normalizedQuery)
    {
        var imageRequested =
            intent.Categories.Contains(FileCategory.Image) ||
            intent.RequestedExtensions.Any(extension =>
                FileTypeCatalog.GetCategory(extension) ==
                FileCategory.Image);
        if (!imageRequested ||
            ContainsConcreteVisualConcept(normalizedQuery))
        {
            return false;
        }

        var subjectTerms = intent.Terms
            .Select(term => term.Original)
            .Where(term => !GenericRequestTerms.Contains(term))
            .Where(term => !CharacterIntentTerms.Contains(
                term,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return subjectTerms.Length is >= 1 and <= 4;
    }

    private static bool ContainsConcreteVisualConcept(string query)
    {
        foreach (var pair in KoreanVisualTerms)
        {
            if (GenericRequestTerms.Contains(pair.Key) ||
                CharacterIntentTerms.Contains(
                    pair.Key,
                    StringComparer.OrdinalIgnoreCase) ||
                UserInterfaceIntentTerms.Contains(
                    pair.Key,
                    StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (query.Contains(
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var tokens = SearchQueryInterpreter.TokenizeText(query);
        return tokens.Any(token =>
            EnglishConcreteVisualTerms.Contains(
                token,
                StringComparer.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(
        string query,
        IEnumerable<string> terms) =>
        terms.Any(term =>
            query.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string BuildRomanizedAliases(string normalizedQuery)
    {
        if (!normalizedQuery.Any(IsModernHangulSyllable))
        {
            return string.Empty;
        }

        var subject = normalizedQuery;
        foreach (var pair in KoreanVisualTerms
                     .OrderByDescending(item => item.Key.Length))
        {
            subject = subject.Replace(
                pair.Key,
                " ",
                StringComparison.OrdinalIgnoreCase);
        }

        subject = KoreanParticleRegex().Replace(subject, " ");
        subject = WhitespaceRegex().Replace(subject, " ").Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        var revised = RomanizeHangul(subject);
        if (string.IsNullOrWhiteSpace(revised))
        {
            return string.Empty;
        }

        var loanwordFriendly = revised
            .Replace("eu", "u", StringComparison.Ordinal)
            .Replace("eo", "o", StringComparison.Ordinal);
        return string.Equals(
                revised,
                loanwordFriendly,
                StringComparison.Ordinal)
            ? revised
            : revised + " / " + loanwordFriendly;
    }

    private static string RomanizeHangul(string text)
    {
        var result = new System.Text.StringBuilder(text.Length * 2);
        foreach (var character in text)
        {
            if (!IsModernHangulSyllable(character))
            {
                result.Append(character);
                continue;
            }

            var syllable = character - 0xAC00;
            var initial = syllable / (21 * 28);
            var vowel = syllable % (21 * 28) / 28;
            var final = syllable % 28;
            result.Append(HangulInitials[initial]);
            result.Append(HangulVowels[vowel]);
            result.Append(HangulFinals[final]);
        }

        return WhitespaceRegex()
            .Replace(result.ToString(), " ")
            .Trim();
    }

    private static bool IsModernHangulSyllable(char character) =>
        character is >= '\uAC00' and <= '\uD7A3';

    [GeneratedRegex(
        @"(찾아\s*줘|찾아줘|찾아\s*주세요|검색해\s*줘|검색해줘|관련된?|파일들?|자료들?|보여\s*줘|보여줘)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QueryNoiseRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"(^|\s)(을|를|이|가|은|는|의|에|로|와|과)($|\s)",
        RegexOptions.CultureInvariant)]
    private static partial Regex KoreanParticleRegex();
}

public enum VisualQueryKind
{
    General,
    Character,
    OfficeMaterial,
    Document,
    UserInterface
}

public sealed record VisualQueryProfile(
    VisualQueryKind Kind,
    bool SuppressUserInterface,
    bool IsNamedSubject,
    double MinimumSimilarity,
    double MaximumDistanceFromBest,
    double MinimumUserInterfaceMargin,
    double MinimumNamedSubjectLift);
