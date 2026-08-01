using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public static partial class SearchQueryInterpreter
{
    private static readonly HashSet<string> StopWords = new(
        [
            "관련", "관련된", "자료", "파일", "폴더", "찾기", "찾아", "찾아줘",
            "찾아줘요", "찾아주라", "찾아달라", "찾아주세요",
            "보여줘", "보여줘요", "보여달라", "보여주세요",
            "대한", "있는", "내", "좀", "해줘", "검색", "검색해줘",
            "검색해줘요", "검색해달라", "검색해주세요",
            "담긴", "담겨", "들어간", "들어있는", "포함된", "포함", "내용",
            "쓰는", "쓰이는", "사용하는", "사용되는", "사용하", "사용되",
            "사용한", "필요한", "있는지", "어디", "어딘가", "혹시",
            "오늘", "어제", "지난달", "저번달", "이번달", "지난주", "이번주",
            "작년", "올해", "최근", "최신", "새로", "수정", "수정한",
            "찾고", "만든", "만들어진", "생성", "생성한", "생성된", "작성",
            "작성한", "작성된", "업데이트", "업데이트한", "변경", "편집",
            "더", "조금", "많이", "강하게", "약하게", "가장", "무조건",
            "우선", "우선해", "우선하고", "먼저", "위", "위로", "아래",
            "아래로", "오게", "오도록", "높게", "낮게", "중요", "중요하게",
            "올려", "올려줘", "올리고", "내려", "내려줘", "내리고",
            "가중치", "비중", "정렬", "순", "순서", "기준", "일수록"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MeaningfulShortTokens = new(
        [
            "키", "값", "표", "글", "앱", "웹", "책", "차", "집", "돈"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RankingControlWords = new(
        [
            "파일명", "제목", "정확", "정확한", "정확도", "일치", "일치도",
            "경로", "폴더명", "위치", "확장자", "형식", "종류", "본문",
            "의미", "유사", "유사도", "크기", "큰", "작은",
            "대용량", "소용량", "생성일", "작성일", "수정일",
            "최신순", "최근순"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] KoreanSuffixes =
    [
        "일수록", "에서는", "으로는", "에게서", "에서", "으로", "에게",
        "까지", "부터", "처럼", "보다", "하고", "이며",
        "과", "와", "을", "를", "이", "가", "은", "는", "의", "에", "로"
    ];

    private static readonly string[][] SemanticGroups =
    [
        ["와이파이", "wifi", "wi-fi", "무선", "wlan"],
        ["네트워크", "network", "랜", "lan"],
        ["오류", "에러", "error", "장애", "고장", "문제", "끊김", "실패", "fail", "crash"],
        ["백업", "backup", "복사본"],
        ["보고서", "리포트", "report"],
        ["회의", "미팅", "meeting", "회의록"],
        ["계약", "계약서", "contract"],
        ["영수증", "receipt"],
        ["청구서", "invoice"],
        [
            "로그인", "로그온", "login", "logon", "signin", "sign-in",
            "인증", "접속", "계정", "계정정보", "account", "credential",
            "credentials", "자격정보", "username", "사용자명", "아이디",
            "key", "privatekey", "ppk", "pem"
        ],
        ["코드", "code", "인증코드", "인증번호", "접속코드", "accesscode"],
        ["비밀번호", "password", "패스워드", "암호"],
        [
            "키", "ssh", "ssh키", "sshkey", "ssh-key", "ssh key",
            "개인키", "비밀키", "공개키", "키파일", "키 파일",
            "키페어", "키 페어", "키쌍",
            "privatekey", "private-key", "private key",
            "publickey", "public-key", "public key", "keypair", "key pair",
            "key", "putty", "rsa", "ed25519",
            "ppk", "pem", "pub", "id_rsa", "id_ed25519",
            "credential", "credentials", "자격증명", "접속자격",
            "계정", "인증"
        ],
        ["vpn", "가상사설망"]
    ];

    private static readonly string[][] ContentEvidenceGroups =
    [
        ["와이파이", "wifi", "wi-fi", "무선", "wlan"],
        ["네트워크", "network", "랜", "lan"],
        ["오류", "에러", "error", "장애", "고장", "문제", "끊김", "실패", "fail", "crash"],
        ["백업", "backup", "복사본"],
        ["보고서", "리포트", "report"],
        ["회의", "미팅", "meeting", "회의록"],
        ["계약", "계약서", "contract"],
        ["영수증", "receipt"],
        ["청구서", "invoice"],
        [
            "ai", "인공지능", "stable diffusion", "stablediffusion",
            "comfyui", "txt2img", "img2img", "생성형ai",
            "negative prompt", "cfg scale", "sampler", "seed", "lora",
            "model hash", "workflow"
        ],
        [
            "태그", "tag", "tags", "prompt", "negative prompt",
            "cfg scale", "sampler", "seed", "lora", "model hash",
            "workflow", "score_9", "1girl", "1boy"
        ],
        ["로그인", "로그온", "login", "logon", "signin", "sign-in"],
        [
            "계정", "계정정보", "account", "credential", "credentials",
            "자격정보", "username", "사용자명", "아이디"
        ],
        ["인증", "authentication", "authenticate"],
        ["접속", "connection", "connect"],
        ["코드", "code", "인증코드", "인증번호", "접속코드", "accesscode"],
        ["비밀번호", "password", "패스워드", "암호"],
        ["vpn", "가상사설망"]
    ];

    public static SearchIntent Interpret(string query, DateTime? localNow = null)
    {
        var now = localNow ?? DateTime.Now;
        var attributeParse = SearchAttributeQueryParser.Parse(query);
        var floorReferences =
            SearchTextAnalyzer.ExtractFloorReferences(query);
        var rawTokens = TokenizeText(
            SearchTextAnalyzer.RemoveFloorReferences(
                attributeParse.RemainingQuery));
        var allRawTokens = TokenizeText(
            attributeParse.RemainingQuery);
        var fileTypeTokens = allRawTokens
            .Where(token =>
                !token.Equals(
                    "ai",
                    StringComparison.OrdinalIgnoreCase) ||
                ShouldTreatAiAsFileFormat(query))
            .ToArray();
        var rankingProfile = BuildRankingProfile(query);
        var explicitWeightToken = rankingProfile.HasPreferences
            ? Regex.Match(
                    query,
                    @"(?<percent>\d{1,3})\s*%",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Groups["percent"]
                .Value
            : string.Empty;
        var categories = FileTypeCatalog.DetectCategories(
            query,
            fileTypeTokens);
        var extensions = fileTypeTokens
            .Select(token =>
                FileTypeCatalog.TryResolveExtensionToken(token, out var extension)
                    ? extension
                    : null)
            .Where(extension => extension is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        extensions.UnionWith(ExtractExplicitExtensions(query));
        if (attributeParse.HwpFormatRequested)
        {
            extensions.Add(".hwp");
            extensions.Add(".hwpx");
        }

        foreach (var extension in extensions)
        {
            categories = categories
                .Append(FileTypeCatalog.GetCategory(extension))
                .ToHashSet();
        }

        var categoryAliases = categories
            .SelectMany(FileTypeCatalog.GetAliases)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var searchableTokens = rawTokens
            .Select(NormalizeToken)
            .Where(IsSearchableToken)
            .Where(token => !StopWords.Contains(token))
            .Where(token => !extensions.Contains($".{token}"))
            .Where(token =>
                !rankingProfile.HasPreferences ||
                !RankingControlWords.Contains(token) &&
                !token.Equals(
                    explicitWeightToken,
                    StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var terms = searchableTokens
            .Where(token => !categoryAliases.Contains(token))
            .Select(token => new SearchTerm(
                token,
                ExpandTerm(token),
                ExpandContentEvidenceTerm(token)))
            .ToArray();
        var literalTerms = searchableTokens
            .Where(categoryAliases.Contains)
            .Select(token => new SearchTerm(
                token,
                [token],
                [token]))
            .ToArray();

        var (modifiedFromUtc, modifiedToUtc, dateDescription) =
            ResolveDateRange(query, now);
        var directoryOnly = DirectoryRequestRegex().IsMatch(query);

        var summaryParts = new List<string>();
        if (extensions.Count > 0)
        {
            summaryParts.Add(string.Join(
                "·",
                extensions
                    .OrderBy(item => item)
                    .Select(item => item.TrimStart('.').ToUpperInvariant())));
        }
        else if (categories.Count > 0)
        {
            summaryParts.Add(string.Join(
                "·",
                categories
                    .OrderBy(category => category)
                    .Select(FileTypeCatalog.GetCategoryLabel)));
        }

        if (terms.Length > 0)
        {
            summaryParts.Add("이름·경로 의미 단서");
        }

        if (literalTerms.Length > 0)
        {
            summaryParts.Add("종류 표현의 실제 이름·경로 일치");
        }

        if (floorReferences.Count > 0)
        {
            summaryParts.Add(string.Join(
                "·",
                floorReferences.Select(reference => reference.Display)));
        }

        if (attributeParse.HasPredicates)
        {
            summaryParts.Add(attributeParse.Summary);
        }

        if (attributeParse.FilesOnly)
        {
            summaryParts.Add("파일만");
        }

        if (!string.IsNullOrWhiteSpace(dateDescription))
        {
            summaryParts.Add(dateDescription);
        }

        if (rankingProfile.HasPreferences)
        {
            summaryParts.Add(rankingProfile.Summary);
        }

        return new SearchIntent(
            query.Trim(),
            terms,
            literalTerms,
            floorReferences,
            attributeParse.Predicates,
            categories,
            extensions,
            modifiedFromUtc,
            modifiedToUtc,
            directoryOnly,
            attributeParse.FilesOnly,
            rankingProfile,
            summaryParts.Count > 0
                ? string.Join(" + ", summaryParts)
                : "이름·경로 단어 분석");
    }

    public static string[] TokenizeText(string text)
    {
        return WordRegex()
            .Matches(text.ToLowerInvariant())
            .Cast<Match>()
            .Select(match => match.Value)
            .Select(NormalizeToken)
            .Where(token => token.Length > 0)
            .ToArray();
    }

    public static bool IsSearchableToken(string token) =>
        !string.IsNullOrWhiteSpace(token) &&
        (token.Length >= 2 ||
         MeaningfulShortTokens.Contains(token));

    private static IEnumerable<string> ExtractExplicitExtensions(string query)
    {
        foreach (Match match in ExplicitExtensionRegex().Matches(query))
        {
            var value = match.Groups["extension"].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return $".{value.ToLowerInvariant()}";
            }
        }
    }

    private static bool ShouldTreatAiAsFileFormat(string query) =>
        ExplicitExtensionRegex().IsMatch(query) ||
        Regex.IsMatch(
            query,
            @"일러스트레이터|adobe\s*illustrator",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IReadOnlyList<string> ExpandTerm(string token)
    {
        foreach (var group in SemanticGroups)
        {
            if (group.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return [token];
    }

    private static IReadOnlyList<string> ExpandContentEvidenceTerm(
        string token)
    {
        foreach (var group in ContentEvidenceGroups)
        {
            if (group.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return [token];
    }

    private static string NormalizeToken(string token)
    {
        var normalized = token.Trim().TrimStart('.').ToLowerInvariant();
        foreach (var suffix in KoreanSuffixes)
        {
            var stemLength = normalized.Length - suffix.Length;
            if (stemLength >= 1 &&
                normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var stem = normalized[..^suffix.Length];
                if (stem.Length >= 2 ||
                    MeaningfulShortTokens.Contains(stem))
                {
                    return stem;
                }
            }
        }

        return normalized;
    }

    private static (DateTime? FromUtc, DateTime? ToUtc, string Description) ResolveDateRange(
        string query,
        DateTime now)
    {
        var today = now.Date;
        DateTime? from = null;
        DateTime? to = null;
        var description = string.Empty;

        if (query.Contains("오늘", StringComparison.OrdinalIgnoreCase))
        {
            from = today;
            to = today.AddDays(1);
            description = "오늘 수정";
        }
        else if (query.Contains("어제", StringComparison.OrdinalIgnoreCase))
        {
            from = today.AddDays(-1);
            to = today;
            description = "어제 수정";
        }
        else if (query.Contains("지난달", StringComparison.OrdinalIgnoreCase) ||
                 query.Contains("저번달", StringComparison.OrdinalIgnoreCase))
        {
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            from = thisMonth.AddMonths(-1);
            to = thisMonth;
            description = "지난달 수정";
        }
        else if (query.Contains("이번달", StringComparison.OrdinalIgnoreCase) ||
                 query.Contains("이번 달", StringComparison.OrdinalIgnoreCase))
        {
            from = new DateTime(today.Year, today.Month, 1);
            to = from.Value.AddMonths(1);
            description = "이번 달 수정";
        }
        else if (query.Contains("지난주", StringComparison.OrdinalIgnoreCase) ||
                 query.Contains("지난 주", StringComparison.OrdinalIgnoreCase))
        {
            var thisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            from = thisWeek.AddDays(-7);
            to = thisWeek;
            description = "지난주 수정";
        }
        else if (query.Contains("이번주", StringComparison.OrdinalIgnoreCase) ||
                 query.Contains("이번 주", StringComparison.OrdinalIgnoreCase))
        {
            from = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            to = from.Value.AddDays(7);
            description = "이번 주 수정";
        }
        else if (query.Contains("작년", StringComparison.OrdinalIgnoreCase))
        {
            from = new DateTime(today.Year - 1, 1, 1);
            to = new DateTime(today.Year, 1, 1);
            description = "작년 수정";
        }
        else if (query.Contains("올해", StringComparison.OrdinalIgnoreCase))
        {
            from = new DateTime(today.Year, 1, 1);
            to = from.Value.AddYears(1);
            description = "올해 수정";
        }

        return (
            from?.ToUniversalTime(),
            to?.ToUniversalTime(),
            description);
    }

    private static SearchRankingProfile BuildRankingProfile(string query)
    {
        var directives = new List<SearchRankingDirective>();
        var hasRankingCue = Regex.IsMatch(
            query,
            @"우선|먼저|위로|아래로|정렬|순(?:으로)?|중요|가중치|비중|높게|낮게|일수록",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var explicitWeight = ResolveExplicitWeight(query);
        var strength = ResolveStrength(query, explicitWeight);
        var weight = explicitWeight ?? strength switch
        {
            SearchRankingStrength.Slight => 0.15d,
            SearchRankingStrength.Normal => 0.28d,
            SearchRankingStrength.Strong => 0.45d,
            SearchRankingStrength.Dominant => 0.65d,
            _ => 0.28d
        };
        var strengthLabel = strength switch
        {
            SearchRankingStrength.Slight => "약하게",
            SearchRankingStrength.Normal => "보통",
            SearchRankingStrength.Strong => "강하게",
            SearchRankingStrength.Dominant => "최우선",
            _ => "보통"
        };

        var mentionsCreation = Regex.IsMatch(
            query,
            @"생성|만들|작성",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var mentionsModification = Regex.IsMatch(
            query,
            @"수정|업데이트|변경|편집",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var mentionsRecency = Regex.IsMatch(
            query,
            @"최근|최신|새로|오래된|생성일|작성일|수정일",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (mentionsRecency &&
            (hasRankingCue ||
             query.Contains("최근", StringComparison.OrdinalIgnoreCase) ||
             query.Contains("최신", StringComparison.OrdinalIgnoreCase)))
        {
            var feature = mentionsCreation && !mentionsModification
                ? SearchRankingFeature.CreatedRecency
                : SearchRankingFeature.ModifiedRecency;
            var olderFirst = Regex.IsMatch(
                query,
                @"오래된.*(?:우선|먼저|위|순)|(?:오래된|과거).*파일.*위",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var primary = Regex.IsMatch(
                query,
                @"무조건.*(?:최근|최신|새로|오래)|(?:최근|최신|생성일|작성일|수정일|오래된)\s*순|최우선",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var timeLabel = feature == SearchRankingFeature.CreatedRecency
                ? "생성일"
                : "수정일";
            directives.Add(new SearchRankingDirective(
                feature,
                olderFirst
                    ? SearchRankingDirection.LowerFirst
                    : SearchRankingDirection.HigherFirst,
                primary ? SearchRankingStrength.Dominant : strength,
                primary ? Math.Max(weight, 0.65d) : weight,
                primary,
                $"{timeLabel} {(olderFirst ? "오래된 순" : "최신 순")} " +
                $"{(primary ? "최우선" : strengthLabel)}",
                ResolveRecencyHalfLifeDays(query)));
        }

        if (hasRankingCue &&
            Regex.IsMatch(
                query,
                @"파일명|제목|이름.*정확|정확한.*이름",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            directives.Add(CreateMatchDirective(
                SearchRankingFeature.NameMatch,
                "파일명 일치",
                query,
                strength,
                weight,
                strengthLabel));
        }

        if (hasRankingCue &&
            Regex.IsMatch(
                query,
                @"경로|폴더명|위치",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            directives.Add(CreateMatchDirective(
                SearchRankingFeature.PathMatch,
                "경로 일치",
                query,
                strength,
                weight,
                strengthLabel));
        }

        if (hasRankingCue &&
            Regex.IsMatch(
                query,
                @"확장자|파일\s*형식|파일\s*종류",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            directives.Add(CreateMatchDirective(
                SearchRankingFeature.TypeMatch,
                "파일 형식 일치",
                query,
                strength,
                weight,
                strengthLabel));
        }

        if (hasRankingCue &&
            Regex.IsMatch(
                query,
                @"본문|내용",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            directives.Add(CreateMatchDirective(
                SearchRankingFeature.ContentMatch,
                "본문 일치",
                query,
                strength,
                weight,
                strengthLabel));
        }

        if (hasRankingCue &&
            Regex.IsMatch(
                query,
                @"의미|유사|(?<![a-z0-9])ai(?![a-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            directives.Add(CreateMatchDirective(
                SearchRankingFeature.SemanticMatch,
                "의미 유사도",
                query,
                strength,
                weight,
                strengthLabel));
        }

        var mentionsLargeFiles = Regex.IsMatch(
            query,
            @"큰\s*파일|대용량|크기.*큰",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var mentionsSmallFiles = Regex.IsMatch(
            query,
            @"작은\s*파일|소용량|크기.*작",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (hasRankingCue && (mentionsLargeFiles || mentionsSmallFiles))
        {
            directives.Add(new SearchRankingDirective(
                SearchRankingFeature.FileSize,
                mentionsSmallFiles
                    ? SearchRankingDirection.LowerFirst
                    : SearchRankingDirection.HigherFirst,
                strength,
                weight,
                IsPrimaryDirective(query),
                $"파일 크기 {(mentionsSmallFiles ? "작은 순" : "큰 순")} {strengthLabel}"));
        }

        return directives.Count == 0
            ? SearchRankingProfile.Default
            : new SearchRankingProfile(
                directives
                    .DistinctBy(directive => directive.Feature)
                    .ToArray());
    }

    private static SearchRankingDirective CreateMatchDirective(
        SearchRankingFeature feature,
        string label,
        string query,
        SearchRankingStrength strength,
        double weight,
        string strengthLabel)
    {
        var lowerFirst = Regex.IsMatch(
            query,
            @"덜\s*중요|아래|낮게|감점",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return new SearchRankingDirective(
            feature,
            lowerFirst
                ? SearchRankingDirection.LowerFirst
                : SearchRankingDirection.HigherFirst,
            strength,
            weight,
            IsPrimaryDirective(query),
            $"{label} {(lowerFirst ? "낮게" : "높게")} {strengthLabel}");
    }

    private static bool IsPrimaryDirective(string query) =>
        Regex.IsMatch(
            query,
            @"무조건|최우선|가장\s*중요|1\s*순위",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static double? ResolveExplicitWeight(string query)
    {
        var match = Regex.Match(
            query,
            @"(?<percent>\d{1,3})\s*%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success &&
               double.TryParse(match.Groups["percent"].Value, out var percent)
            ? Math.Clamp(percent / 100d, 0.05d, 1d)
            : null;
    }

    private static SearchRankingStrength ResolveStrength(
        string query,
        double? explicitWeight)
    {
        if (explicitWeight is not null)
        {
            return explicitWeight.Value switch
            {
                >= 0.6d => SearchRankingStrength.Dominant,
                >= 0.4d => SearchRankingStrength.Strong,
                < 0.2d => SearchRankingStrength.Slight,
                _ => SearchRankingStrength.Normal
            };
        }

        if (Regex.IsMatch(
                query,
                @"무조건|최우선|가장\s*중요",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return SearchRankingStrength.Dominant;
        }
        if (Regex.IsMatch(
                query,
                @"강하게|많이|더\s*중요|더\s*위",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return SearchRankingStrength.Strong;
        }
        return Regex.IsMatch(
            query,
            @"조금|약하게|살짝",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? SearchRankingStrength.Slight
            : SearchRankingStrength.Normal;
    }

    private static double ResolveRecencyHalfLifeDays(string query)
    {
        var match = Regex.Match(
            query,
            @"(?<amount>\d{1,4})\s*(?<unit>일|주|개월|달|년)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success ||
            !double.TryParse(match.Groups["amount"].Value, out var amount))
        {
            return 90d;
        }

        return match.Groups["unit"].Value switch
        {
            "주" => amount * 7d,
            "개월" or "달" => amount * 30d,
            "년" => amount * 365d,
            _ => amount
        };
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:\*)?\.(?<extension>[a-z0-9][a-z0-9_+-]{0,31})(?![\p{L}\p{N}._+-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitExtensionRegex();

    [GeneratedRegex(@"폴더\s*(을|를)?\s*(찾|보여|검색)", RegexOptions.IgnoreCase)]
    private static partial Regex DirectoryRequestRegex();
}

public sealed record SearchIntent(
    string OriginalQuery,
    IReadOnlyList<SearchTerm> Terms,
    IReadOnlyList<SearchTerm> LiteralTerms,
    IReadOnlyList<SearchFloorReference> FloorReferences,
    IReadOnlyList<SearchTextAttributePredicate> AttributePredicates,
    IReadOnlyCollection<FileCategory> Categories,
    IReadOnlyCollection<string> RequestedExtensions,
    DateTime? ModifiedFromUtc,
    DateTime? ModifiedToUtc,
    bool DirectoryOnly,
    bool FilesOnly,
    SearchRankingProfile RankingProfile,
    string Summary)
{
    public IReadOnlyList<SearchTerm> MetadataTerms =>
        Terms
            .Concat(LiteralTerms)
            .DistinctBy(
                term => term.Original,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public int MetadataTermCount =>
        Terms
            .Select(term => term.Original)
            .Concat(LiteralTerms.Select(term => term.Original))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    public bool RequiresContentAttributes =>
        AttributePredicates.Any(predicate =>
            predicate.Scope is
                SearchTextAttributeScope.Content or
                SearchTextAttributeScope.NameOrContent);

    public bool IsSingleTermNameLookup =>
        Terms.Count == 1 &&
        LiteralTerms.Count == 0 &&
        FloorReferences.Count == 0 &&
        AttributePredicates.Count == 0 &&
        Categories.Count == 0 &&
        RequestedExtensions.Count == 0 &&
        ModifiedFromUtc is null &&
        ModifiedToUtc is null &&
        !RankingProfile.HasPreferences;

    public SearchIntentClassification Classification =>
        SearchIntentClassifier.Classify(this);

    public bool IsExplicitNameLookup =>
        Classification.Mode == SearchIntentMode.ExactName;

    public bool PreferRecent =>
        RankingProfile.Directives.Any(directive =>
            (directive.Feature is
                 SearchRankingFeature.CreatedRecency or
                 SearchRankingFeature.ModifiedRecency) &&
            directive.Direction == SearchRankingDirection.HigherFirst);

    public bool HasCriteria =>
        Terms.Count > 0 ||
        LiteralTerms.Count > 0 ||
        FloorReferences.Count > 0 ||
        AttributePredicates.Count > 0 ||
        Categories.Count > 0 ||
        RequestedExtensions.Count > 0 ||
        ModifiedFromUtc is not null ||
        DirectoryOnly ||
        FilesOnly ||
        RankingProfile.HasPreferences;
}

public sealed record SearchTerm(
    string Original,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> ContentEvidenceAlternatives);
