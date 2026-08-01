using AIExplorer.Models;
using AIExplorer.Services;

var temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"AIExplorer-Smoke-{Guid.NewGuid():N}");

try
{
    Directory.CreateDirectory(temporaryRoot);
    var contentRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "content")).FullName;
    var sourceFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "14층 무선 점검"));
    var accountDocumentFolder = Directory.CreateDirectory(
        Path.Combine(sourceFolder.FullName, "계정관리문서"));
    var generalDocumentFolder = Directory.CreateDirectory(
        Path.Combine(sourceFolder.FullName, "일반문서"));
    var drawingFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "3. 도면"));
    var basementThirdFloorFolder = Directory.CreateDirectory(
        Path.Combine(drawingFolder.FullName, "지하 3층"));
    var basementThirteenthFloorFolder = Directory.CreateDirectory(
        Path.Combine(drawingFolder.FullName, "지하 13층"));
    var basementDrawingFile = Path.Combine(
        basementThirdFloorFolder.FullName,
        "건물 배관 도면.dwg");
    await File.WriteAllTextAsync(
        basementDrawingFile,
        "basement drawing fixture");
    var basementThirteenthDrawingFile = Path.Combine(
        basementThirteenthFloorFolder.FullName,
        "건물 배관 도면.dwg");
    await File.WriteAllTextAsync(
        basementThirteenthDrawingFile,
        "different floor fixture");
    var destinationFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "복사 대상"));
    var sourceFile = Path.Combine(sourceFolder.FullName, "와이파이 끊김 원인.txt");
    await File.WriteAllTextAsync(sourceFile, "AP 재부팅 및 채널 간섭 점검");
    var modelFile = Path.Combine(sourceFolder.FullName, "무작위_자산_001.stl");
    await File.WriteAllTextAsync(modelFile, "solid test");
    var semanticModelFile = Path.Combine(
        sourceFolder.FullName,
        "WireGuard_privacy_relay_shape.stl");
    await File.WriteAllTextAsync(semanticModelFile, "solid metadata-only");
    var falsePositiveFile = Path.Combine(
        sourceFolder.FullName,
        "0000000002030000834A3DE0.bin");
    await File.WriteAllBytesAsync(falsePositiveFile, [0, 1, 2, 3]);
    var privateKeyFile = Path.Combine(
        sourceFolder.FullName,
        "production-access.ppk");
    await File.WriteAllTextAsync(privateKeyFile, "private key fixture");
    var awsPrivateKeyFile = Path.Combine(
        accountDocumentFolder.FullName,
        "aws_viewplasticsurgery_key.ppk");
    await File.WriteAllTextAsync(awsPrivateKeyFile, "aws key fixture");
    var awsMonkeyNoiseFile = Path.Combine(
        generalDocumentFolder.FullName,
        "aws_viewplasticsurgery_monkey.txt");
    await File.WriteAllTextAsync(awsMonkeyNoiseFile, "unrelated animal fixture");
    var certificateFile = Path.Combine(
        sourceFolder.FullName,
        "edge-certificate.pem");
    await File.WriteAllTextAsync(certificateFile, "certificate fixture");
    var customFormatFile = Path.Combine(
        sourceFolder.FullName,
        "vendor-payload.artifact42");
    await File.WriteAllBytesAsync(customFormatFile, [4, 2]);
    var hiddenContentFile = Path.Combine(
        sourceFolder.FullName,
        "개인 메모.txt");
    await File.WriteAllTextAsync(
        hiddenContentFile,
        "Mullvad VPN 로그인 코드는 849201이며 사용 후 폐기해야 합니다.");
    var englishNamedHangulContentFile = Path.Combine(
        sourceFolder.FullName,
        "content-language-fixture.txt");
    await File.WriteAllTextAsync(
        englishNamedHangulContentFile,
        "이 파일의 본문은 한글 문자 조건을 검증하기 위한 자료입니다.");
    var aiContextCodeFile = Path.Combine(
        sourceFolder.FullName,
        "AIContextCode.txt");
    await File.WriteAllTextAsync(
        aiContextCodeFile,
        "Negative prompt: low quality\nCFG scale: 7\nSampler: DPM++ 2M\nSeed: 12345\nLoRA: office_style");
    var mortFolder = Directory.CreateDirectory(
        Path.Combine(sourceFolder.FullName, "MORT 1.281v - 20240722"));
    var mortTranslationFolder = Directory.CreateDirectory(
        Path.Combine(sourceFolder.FullName, "MORT_GOOGLE_TRANS"));
    var mortExecutable = Path.Combine(
        mortFolder.FullName,
        "MORT.exe");
    await File.WriteAllBytesAsync(mortExecutable, [0x4d, 0x5a]);
    var mortUnrelatedChild = Path.Combine(
        mortFolder.FullName,
        "unrelated.dll");
    await File.WriteAllBytesAsync(mortUnrelatedChild, [0x00]);
    var semanticOnlyFile = Path.Combine(
        sourceFolder.FullName,
        "정리되지 않은 기록.txt");
    await File.WriteAllTextAsync(
        semanticOnlyFile,
        "WireGuard tunnel account token and privacy relay configuration.");
    var withholdingFile = Path.Combine(
        sourceFolder.FullName,
        "2026_원천징수_신고서.md");
    await File.WriteAllTextAsync(
        withholdingFile,
        "직원 급여 원천징수 신고 및 납부 자료");
    var tildeTemporaryFile = Path.Combine(
        sourceFolder.FullName,
        "~원천징수_임시복구본.md");
    await File.WriteAllTextAsync(
        tildeTemporaryFile,
        "사용자 검색에 표시하면 안 되는 임시 파일");
    var hiddenSearchFile = Path.Combine(
        sourceFolder.FullName,
        "숨김_원천징수_자료.md");
    await File.WriteAllTextAsync(
        hiddenSearchFile,
        "사용자 검색에 표시하면 안 되는 숨김 파일");
    if (OperatingSystem.IsWindows())
    {
        File.SetAttributes(hiddenSearchFile, FileAttributes.Hidden);
    }
    var utf16ContentFile = Path.Combine(
        sourceFolder.FullName,
        "장비 기록.txt");
    await File.WriteAllTextAsync(
        utf16ContentFile,
        "레거시 UTF16 장비 점검 암호는 bluebird입니다.",
        Encoding.Unicode);
    var ocrImageFile = Path.Combine(
        sourceFolder.FullName,
        "무작위_스캔_001.png");
    await File.WriteAllBytesAsync(
        ocrImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var characterImageFile = Path.Combine(
        sourceFolder.FullName,
        "unlabeled_character_art.png");
    await File.WriteAllBytesAsync(
        characterImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var namedCharacterImageFile = Path.Combine(
        sourceFolder.FullName,
        "rapi_generic_character.png");
    await File.WriteAllBytesAsync(
        namedCharacterImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var unrelatedCharacterImageFile = Path.Combine(
        sourceFolder.FullName,
        "unrelated_generic_character.png");
    await File.WriteAllBytesAsync(
        unrelatedCharacterImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var userInterfaceImageFile = Path.Combine(
        sourceFolder.FullName,
        "unrelated_ui_screenshot.png");
    await File.WriteAllBytesAsync(
        userInterfaceImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var numericCharacterImageFile = Path.Combine(
        sourceFolder.FullName,
        "1238475.png");
    await File.WriteAllBytesAsync(
        numericCharacterImageFile,
        [0x89, 0x50, 0x4E, 0x47]);
    var rankingFixtureFolder = Directory.CreateDirectory(
        Path.Combine(sourceFolder.FullName, "ranking-fixtures"));
    var oldRankingFixture = Path.Combine(
        rankingFixtureFolder.FullName,
        "priorityfixture_old.txt");
    var newRankingFixture = Path.Combine(
        rankingFixtureFolder.FullName,
        "priorityfixture_new.txt");
    await File.WriteAllTextAsync(
        oldRankingFixture,
        "same ranking fixture content");
    await File.WriteAllTextAsync(
        newRankingFixture,
        "same ranking fixture content");
    var rankingFixtureNow = DateTime.UtcNow;
    File.SetCreationTimeUtc(
        oldRankingFixture,
        rankingFixtureNow.AddYears(-3));
    File.SetCreationTimeUtc(
        newRankingFixture,
        rankingFixtureNow.AddDays(-1));
    File.SetLastWriteTimeUtc(oldRankingFixture, rankingFixtureNow);
    File.SetLastWriteTimeUtc(
        newRankingFixture,
        rankingFixtureNow.AddMonths(-1));

    var backgroundIndexRoots =
        BackgroundIndexRootPlanner.OrderRoots(
            [sourceFolder.FullName],
            [
                drawingFolder.FullName,
                accountDocumentFolder.FullName,
                sourceFolder.FullName.ToUpperInvariant()
            ],
            [
                contentRoot,
                drawingFolder.FullName.ToUpperInvariant(),
                generalDocumentFolder.FullName
            ]);
    Assert(
        backgroundIndexRoots.SequenceEqual(
            [
                sourceFolder.FullName,
                drawingFolder.FullName,
                accountDocumentFolder.FullName,
                contentRoot,
                generalDocumentFolder.FullName
            ],
            StringComparer.OrdinalIgnoreCase),
        "백그라운드 색인은 현재 검색 위치 다음에 즐겨찾기를 우선하고 중복 제거");

    var foregroundIndexBudget =
        BackgroundIndexWorkPolicy.GetBudget(
            isHiddenToTray: false,
            scheduledDelay: TimeSpan.FromSeconds(2));
    var idleIndexBudget = BackgroundIndexWorkPolicy.GetBudget(
        isHiddenToTray: false,
        scheduledDelay: TimeSpan.FromMinutes(15));
    var trayIndexBudget = BackgroundIndexWorkPolicy.GetBudget(
        isHiddenToTray: true,
        scheduledDelay: TimeSpan.FromSeconds(1));
    Assert(
        !foregroundIndexBudget.AllowHeavyAiIndexing &&
        foregroundIndexBudget.MaximumContentDocumentsPerRoot == 0 &&
        foregroundIndexBudget.MaximumNewSemanticDocumentsPerRoot == 0 &&
        foregroundIndexBudget.MaximumNewVisualDocumentsPerRoot == 0 &&
        idleIndexBudget.AllowHeavyAiIndexing &&
        trayIndexBudget.AllowHeavyAiIndexing &&
        BackgroundIndexWorkPolicy.GetNextDelay(
            isHiddenToTray: false,
            newAiDocumentsWereIndexed: true) ==
        TimeSpan.FromMinutes(15) &&
        BackgroundIndexWorkPolicy.GetNextDelay(
            isHiddenToTray: true,
            newAiDocumentsWereIndexed: true) ==
        TimeSpan.FromSeconds(20),
        "전면 실행 중에는 제목 색인만 수행하고 무거운 AI 색인은 긴 유휴·트레이에서만 배치 실행");

    var sortNow = DateTime.UtcNow;
    var driveDResult = CreateSortResult(
        "다 문서.txt",
        @"D:\Zulu\다 문서.txt",
        sortNow,
        score: 980d);
    var driveCBetaResult = CreateSortResult(
        "가 문서.txt",
        @"C:\Beta\가 문서.txt",
        sortNow.AddDays(-2),
        score: 900d);
    var driveCAlphaResult = CreateSortResult(
        "나 문서.txt",
        @"C:\Alpha\나 문서.txt",
        sortNow.AddDays(-1),
        score: 850d);
    var relevanceOrdered = new[]
    {
        driveDResult,
        driveCBetaResult,
        driveCAlphaResult
    };
    Assert(
        SearchResultSortService.Sort(
                relevanceOrdered,
                SearchResultSortMode.Relevance)
            .SequenceEqual(relevanceOrdered) &&
        SearchResultSortService.Sort(
                relevanceOrdered,
                SearchResultSortMode.TopLevelPath)
            .Select(result => result.FullPath)
            .SequenceEqual(
                [
                    driveCAlphaResult.FullPath,
                    driveCBetaResult.FullPath,
                    driveDResult.FullPath
                ],
                StringComparer.OrdinalIgnoreCase) &&
        SearchResultSortService.Sort(
                relevanceOrdered,
                SearchResultSortMode.Name)
            .Select(result => result.Name)
            .SequenceEqual(
                ["가 문서.txt", "나 문서.txt", "다 문서.txt"],
                StringComparer.CurrentCultureIgnoreCase) &&
        SearchResultSortService.Sort(
                relevanceOrdered,
                SearchResultSortMode.ModifiedNewest)
            .Select(result => result.FullPath)
            .SequenceEqual(
                [
                    driveDResult.FullPath,
                    driveCAlphaResult.FullPath,
                    driveCBetaResult.FullPath
                ],
                StringComparer.OrdinalIgnoreCase),
        "검색 결과를 일치도·드라이브 최상위 경로·가나다·최신 수정일로 정렬");

    var weightedTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "priorityfixture 파일을 찾고 최근에 만들어진 파일일수록 더 위로 오게 해줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                weightedTitleEvents),
            cancellationToken: CancellationToken.None);
    var weightedTitleHits = weightedTitleEvents
        .SelectMany(item => item.NewHits)
        .Where(hit =>
            hit.FullPath == oldRankingFixture ||
            hit.FullPath == newRankingFixture)
        .ToArray();
    Assert(
        weightedTitleHits.Length == 2 &&
        weightedTitleHits.First(hit =>
                hit.FullPath == newRankingFixture)
            .Score >
        weightedTitleHits.First(hit =>
                hit.FullPath == oldRankingFixture)
            .Score &&
        weightedTitleHits.All(hit => hit.CreatedLocal is not null),
        "빠른 이름·경로 검색에도 생성일 자연어 가중치를 반영");

    var titleProgressEvents = new List<TitleSearchProgress>();
    var titleSearchSummary = await new TitleSearchService(
            new NetworkPathService())
        .SearchAsync(
            "원천징수 파일 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                titleProgressEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        titleSearchSummary.MatchedItems >= 1 &&
        titleProgressEvents
            .SelectMany(item => item.NewHits)
            .Any(item => string.Equals(
                item.FullPath,
                withholdingFile,
                StringComparison.OrdinalIgnoreCase)),
        "독립 제목 검색은 파일명 키워드를 놓치지 않음");
    var titlePaths = titleProgressEvents
        .SelectMany(item => item.NewHits)
        .Select(item => item.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        !titlePaths.Contains(tildeTemporaryFile),
        "물결표로 시작하는 임시 파일은 제목 검색에서 제외");
    if (OperatingSystem.IsWindows())
    {
        Assert(
            !titlePaths.Contains(hiddenSearchFile),
            "숨김 속성 파일은 제목 검색에서 제외");
    }
    Assert(
        titleProgressEvents.Any(item =>
            !item.IsCompleted &&
            item.NewHits.Any(hit => string.Equals(
                hit.FullPath,
                withholdingFile,
                StringComparison.OrdinalIgnoreCase))),
        "첫 제목 일치는 전체 탐색 완료 전에 전달");
    var typeOnlyTitleEvents = new List<TitleSearchProgress>();
    var typeOnlyTitleSummary = await new TitleSearchService(
            new NetworkPathService())
        .SearchAsync(
            "stl 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                typeOnlyTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        typeOnlyTitleSummary.MatchedItems >= 1 &&
        typeOnlyTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                modelFile,
                StringComparison.OrdinalIgnoreCase)),
        "파일명 키워드가 없어도 확장자 요청은 제목 검색에 즉시 표시");
    Assert(
        SearchQueryInterpreter.Interpret(
                "stl 파일을 찾아줘")
            .RequestedExtensions.Contains(
                ".stl",
                StringComparer.OrdinalIgnoreCase),
        "STL 형식 토큰을 확장자 조건으로 해석");

    var uncommonTypeTitleEvents = new List<TitleSearchProgress>();
    var uncommonTypeTitleSummary = await new TitleSearchService(
            new NetworkPathService())
        .SearchAsync(
            ".ppk 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                uncommonTypeTitleEvents),
            cancellationToken: CancellationToken.None);
    var uncommonTypeTitlePaths = uncommonTypeTitleEvents
        .SelectMany(item => item.NewHits)
        .Select(item => item.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        uncommonTypeTitleSummary.MatchedItems >= 1 &&
        uncommonTypeTitlePaths.Contains(privateKeyFile) &&
        !uncommonTypeTitlePaths.Contains(certificateFile),
        "등록되지 않은 점 표기 확장자도 정확한 제목 검색 조건으로 사용");
    Assert(
        SearchQueryInterpreter.Interpret(
                ".artifact42 파일을 찾아줘")
            .RequestedExtensions.Contains(
                ".artifact42",
                StringComparer.OrdinalIgnoreCase),
        "임의의 명시적 확장자를 카탈로그 없이 검색 조건으로 해석");

    var bareExtensionTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "pem 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                bareExtensionTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        bareExtensionTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                certificateFile,
                StringComparison.OrdinalIgnoreCase)),
        "등록되지 않은 확장자도 파일명 메타데이터의 일부로 검색");

    var sshKeyIntent = SearchQueryInterpreter.Interpret(
        "aws ssh키를 찾아달라");
    Assert(
        sshKeyIntent.Terms.Count == 2 &&
        sshKeyIntent.Terms.Any(term =>
            term.Original.Equals(
                "ssh키",
                StringComparison.OrdinalIgnoreCase) &&
            term.Alternatives.Contains(
                "key",
                StringComparer.OrdinalIgnoreCase) &&
            term.Alternatives.Contains(
                "ppk",
                StringComparer.OrdinalIgnoreCase)),
        "명령형 표현을 제거하고 SSH 키를 영문 파일명·확장자 의미로 확장");
    var createdRecencyIntent = SearchQueryInterpreter.Interpret(
        "aws ssh키를 찾고 최근에 만들어진 파일일수록 더 위로 오게 해줘");
    var createdRecencyDirective =
        createdRecencyIntent.RankingProfile.Directives.Single();
    Assert(
        createdRecencyIntent.Terms.Count == 2 &&
        createdRecencyIntent.Terms.Any(term =>
            term.Original.Equals("aws", StringComparison.OrdinalIgnoreCase)) &&
        createdRecencyIntent.Terms.Any(term =>
            term.Original.Equals("ssh키", StringComparison.OrdinalIgnoreCase)) &&
        createdRecencyDirective.Feature ==
            SearchRankingFeature.CreatedRecency &&
        createdRecencyDirective.Direction ==
            SearchRankingDirection.HigherFirst &&
        createdRecencyDirective.Strength ==
            SearchRankingStrength.Strong,
        "자연어 문장에서 검색 대상과 생성일 최신 가중치를 분리");
    var modifiedRecencyIntent = SearchQueryInterpreter.Interpret(
        "aws ssh키를 찾고 최근 수정한 파일을 조금 우선해줘");
    Assert(
        modifiedRecencyIntent.RankingProfile.Directives.Single().Feature ==
            SearchRankingFeature.ModifiedRecency &&
        modifiedRecencyIntent.RankingProfile.Directives.Single().Strength ==
            SearchRankingStrength.Slight,
        "생성일과 수정일 표현을 구분하고 강도 표현을 해석");
    var explicitWeightIntent = SearchQueryInterpreter.Interpret(
        "aws ssh키를 찾고 생성일 최신 가중치를 40%로 해줘");
    Assert(
        Math.Abs(
            explicitWeightIntent.RankingProfile.Directives.Single().Weight -
            0.4d) < 0.001d &&
        explicitWeightIntent.Terms.All(term =>
            !term.Original.Equals("40", StringComparison.Ordinal)),
        "자연어 검색의 명시적 퍼센트 가중치를 해석");
    var namePriorityIntent = SearchQueryInterpreter.Interpret(
        "계정 파일을 찾고 파일명 일치를 가장 중요하게 해줘");
    Assert(
        namePriorityIntent.Terms.Count == 1 &&
        namePriorityIntent.Terms[0].Original.Equals(
            "계정",
            StringComparison.OrdinalIgnoreCase) &&
        namePriorityIntent.RankingProfile.Directives.Single().Feature ==
            SearchRankingFeature.NameMatch &&
        namePriorityIntent.RankingProfile.Directives.Single().IsPrimary,
        "파일명 일치 우선 문장을 검색 대상과 최우선 랭킹 지시로 분리");
    var sizePriorityIntent = SearchQueryInterpreter.Interpret(
        "로그 파일을 찾고 큰 파일을 조금 위로 올려줘");
    Assert(
        sizePriorityIntent.RankingProfile.Directives.Single().Feature ==
            SearchRankingFeature.FileSize &&
        sizePriorityIntent.RankingProfile.Directives.Single().Direction ==
            SearchRankingDirection.HigherFirst &&
        sizePriorityIntent.RankingProfile.Directives.Single().Strength ==
            SearchRankingStrength.Slight,
        "파일 크기와 강도에 대한 자연어 가중치를 해석");

    var languagePlanJson =
        """
        {
          "term_groups": [
            {
              "term": "aws",
              "alternatives": ["amazon web services"]
            },
            {
              "term": "ssh키",
              "alternatives": ["ssh key", "private key", "ppk", "pem", "putty"]
            },
            {
              "term": "기타",
              "alternatives": ["misc", "other"]
            }
          ],
          "requested_extensions": [],
          "target": "file",
          "sort": "created_newest",
          "use_previous_results": true,
          "confidence": 0.93,
          "interpretation": "이전 결과에서 AWS SSH 키 파일을 찾아 최근 생성 순으로 정렬"
        }
        """;
    var languagePlan = NaturalLanguageSearchService.ParsePlanJson(
        languagePlanJson,
        "그중 aws ssh키를 최근에 만든 순서로 보여줘",
        new SearchConversationContext(
            "계정 관련 파일을 찾아줘",
            24));
    Assert(
        languagePlan.UsedLanguageModel &&
        languagePlan.UsePreviousResults &&
        languagePlan.Target == SearchPlanTarget.File &&
        languagePlan.Sort == SearchPlanSort.CreatedNewest &&
        languagePlan.TermGroups.Any(group =>
            group.Term.Equals(
                "ssh키",
                StringComparison.OrdinalIgnoreCase) &&
            group.Alternatives.Contains(
                "ppk",
                StringComparer.OrdinalIgnoreCase)),
        "로컬 LLM JSON을 이전 결과 문맥과 SSH 키 검색 계획으로 검증");
    var deterministicLanguageIntent = SearchQueryInterpreter.Interpret(
        "그중 aws ssh키를 최근에 만든 순서로 보여줘");
    var compiledLanguageIntent = SearchPlanCompiler.Compile(
        deterministicLanguageIntent,
        languagePlan,
        languageModelAvailable: true);
    Assert(
        ReferenceEquals(
            compiledLanguageIntent.Intent,
            deterministicLanguageIntent) &&
        !compiledLanguageIntent.ShouldRefinePreviousResults &&
        !compiledLanguageIntent.Intent.FilesOnly &&
        compiledLanguageIntent.Intent.Terms.All(term =>
            !term.Original.Equals(
                "기타",
                StringComparison.OrdinalIgnoreCase)),
        "LLM 계획이 사용자가 입력하지 않은 단어·파일 제한·결과내 재검색을 추가하지 못함",
        $"refine={compiledLanguageIntent.ShouldRefinePreviousResults}; " +
        $"files={compiledLanguageIntent.Intent.FilesOnly}; " +
        $"terms={string.Join(',', compiledLanguageIntent.Intent.Terms.Select(term => $"{term.Original}:{string.Join('/', term.Alternatives)}"))}; " +
        $"ranking={compiledLanguageIntent.Intent.RankingProfile.Summary}");
    var explicitExtensionPlan =
        NaturalLanguageSearchService.ParsePlanJson(
            languagePlanJson.Replace(
                "\"requested_extensions\": []",
                "\"requested_extensions\": [\".pem\"]",
                StringComparison.Ordinal),
            ".ppk 파일 중 aws 키를 찾아줘");
    var explicitExtensionIntent = SearchPlanCompiler.Compile(
        SearchQueryInterpreter.Interpret(
            ".ppk 파일 중 aws 키를 찾아줘"),
        explicitExtensionPlan,
        languageModelAvailable: true).Intent;
    Assert(
        explicitExtensionIntent.RequestedExtensions.Contains(
            ".ppk",
            StringComparer.OrdinalIgnoreCase) &&
        !explicitExtensionIntent.RequestedExtensions.Contains(
            ".pem",
            StringComparer.OrdinalIgnoreCase),
        "사용자의 명시적 확장자는 보존하고 LLM이 추가한 확장자는 거부");

    var rankingNow = DateTime.UtcNow;
    var oldCreatedKey = new IndexedFileRecord
    {
        Name = "aws_archive_key.ppk",
        FullPath = Path.Combine(
            accountDocumentFolder.FullName,
            "aws_archive_key.ppk"),
        DirectoryPath = accountDocumentFolder.FullName,
        Extension = ".ppk",
        IsDirectory = false,
        SizeBytes = 1_024,
        CreatedUtc = rankingNow.AddYears(-2),
        ModifiedUtc = rankingNow
    };
    var newlyCreatedKey = new IndexedFileRecord
    {
        Name = "aws_current_key.ppk",
        FullPath = Path.Combine(
            accountDocumentFolder.FullName,
            "aws_current_key.ppk"),
        DirectoryPath = accountDocumentFolder.FullName,
        Extension = ".ppk",
        IsDirectory = false,
        SizeBytes = 1_024,
        CreatedUtc = rankingNow.AddDays(-1),
        ModifiedUtc = rankingNow.AddMonths(-1)
    };
    var creationRankedCandidates = SearchRankingService.FindCandidates(
        createdRecencyIntent,
        [oldCreatedKey, newlyCreatedKey],
        maximumResults: 10,
        progress: null,
        CancellationToken.None);
    Assert(
        creationRankedCandidates.Count == 2 &&
        creationRankedCandidates[0].Record.FullPath ==
            newlyCreatedKey.FullPath,
        "최근 수정된 오래된 파일보다 최근 생성된 파일을 생성일 기준으로 우선");

    var recentUnrelatedFile = new IndexedFileRecord
    {
        Name = "aws_monkey_notes.txt",
        FullPath = Path.Combine(
            generalDocumentFolder.FullName,
            "aws_monkey_notes.txt"),
        DirectoryPath = generalDocumentFolder.FullName,
        Extension = ".txt",
        IsDirectory = false,
        SizeBytes = 1_024,
        CreatedUtc = rankingNow,
        ModifiedUtc = rankingNow
    };
    var guardedRanking = SearchRankingService.FindCandidates(
        createdRecencyIntent,
        [oldCreatedKey, recentUnrelatedFile],
        maximumResults: 10,
        progress: null,
        CancellationToken.None);
    Assert(
        guardedRanking.Count == 2 &&
        guardedRanking[0].Record.FullPath == oldCreatedKey.FullPath,
        "최신 가중치가 검색 의도가 약한 최신 파일을 관련 키 파일 위로 오승격하지 않음");
    var naturalKeyTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "aws ssh키를 찾아달라",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                naturalKeyTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        naturalKeyTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                awsPrivateKeyFile,
                StringComparison.OrdinalIgnoreCase)),
        "AWS SSH 키 자연어 검색에서 영문 key 제목을 즉시 발견");
    var naturalKeyTitleHits = naturalKeyTitleEvents
        .SelectMany(item => item.NewHits)
        .ToArray();
    Assert(
        naturalKeyTitleHits.First(hit => string.Equals(
                hit.FullPath,
                awsPrivateKeyFile,
                StringComparison.OrdinalIgnoreCase))
            .Score >
        naturalKeyTitleHits.First(hit => string.Equals(
                hit.FullPath,
                awsMonkeyNoiseFile,
                StringComparison.OrdinalIgnoreCase))
            .Score,
        "SSH key 의미 일치가 AWS 단어만 맞는 monkey 파일보다 높은 제목 점수");

    var pathContextTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "aws 관리 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                pathContextTitleEvents),
            cancellationToken: CancellationToken.None);
    var pathContextKeyHit = pathContextTitleEvents
        .SelectMany(item => item.NewHits)
        .FirstOrDefault(hit => string.Equals(
            hit.FullPath,
            awsPrivateKeyFile,
            StringComparison.OrdinalIgnoreCase));
    Assert(
        pathContextKeyHit is not null &&
        pathContextKeyHit.Reason.Contains(
            "상위 폴더",
            StringComparison.Ordinal),
        "빠른 검색에서 파일명 AWS와 계정관리문서 경로 단서를 결합");

    var keyMetadataDescription =
        FileMetadataDescriptor.BuildSemanticText(
            contentRoot,
            new IndexedFileRecord
            {
                Name = Path.GetFileName(awsPrivateKeyFile),
                FullPath = awsPrivateKeyFile,
                DirectoryPath = accountDocumentFolder.FullName,
                Extension = ".ppk",
                IsDirectory = false,
                SizeBytes = new FileInfo(awsPrivateKeyFile).Length,
                ModifiedUtc = DateTime.UtcNow
            });
    Assert(
        keyMetadataDescription.Contains(
            "PuTTY SSH 개인키",
            StringComparison.Ordinal) &&
        keyMetadataDescription.Contains(
            "계정관리문서",
            StringComparison.Ordinal),
        "Other 특수 파일도 형식 의미와 상위 경로를 메타데이터 의미 문서로 구성");

    var imageOnlyTitleEvents = new List<TitleSearchProgress>();
    var imageOnlyTitleSummary = await new TitleSearchService(
            new NetworkPathService())
        .SearchAsync(
            "이미지 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                imageOnlyTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        imageOnlyTitleSummary.MatchedItems >= 1 &&
        imageOnlyTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                ocrImageFile,
                StringComparison.OrdinalIgnoreCase)),
        "제목 키워드가 없는 이미지 종류 요청도 즉시 표시");

    var folderOnlyTitleEvents = new List<TitleSearchProgress>();
    var folderOnlyTitleSummary = await new TitleSearchService(
            new NetworkPathService())
        .SearchAsync(
            "폴더를 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                folderOnlyTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        folderOnlyTitleSummary.MatchedItems >= 1 &&
        folderOnlyTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                sourceFolder.FullName,
                StringComparison.OrdinalIgnoreCase)),
        "제목 키워드가 없는 폴더 요청도 즉시 표시");

    var mortTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "MORT를 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                mortTitleEvents),
            cancellationToken: CancellationToken.None);
    var mortTitlePaths = mortTitleEvents
        .SelectMany(item => item.NewHits)
        .Select(hit => hit.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        mortTitlePaths.Contains(mortFolder.FullName) &&
        mortTitlePaths.Contains(mortTranslationFolder.FullName) &&
        mortTitlePaths.Contains(mortExecutable) &&
        !mortTitlePaths.Contains(mortUnrelatedChild),
        "단일 이름 검색은 파일·폴더 이름 자체가 맞는 항목만 반환");

    var vpnRelatedIntent = SearchQueryInterpreter.Interpret(
        "vpn 관련 파일을 찾아줘");
    Assert(
        vpnRelatedIntent.Classification.Mode ==
            SearchIntentMode.TopicRelated &&
        !vpnRelatedIntent.IsExplicitNameLookup &&
        vpnRelatedIntent.Classification.SearchApplicationCatalog,
        "관련 자료 요청을 제목 전용 검색으로 조기 종료하지 않고 앱 카탈로그까지 검색");
    var explicitVpnNameIntent = SearchQueryInterpreter.Interpret(
        "파일명이 vpn인 항목을 찾아줘");
    Assert(
        explicitVpnNameIntent.Classification.Mode ==
            SearchIntentMode.ExactName &&
        explicitVpnNameIntent.IsExplicitNameLookup,
        "사용자가 파일명을 명시한 경우에만 제목 전용 검색으로 분류");

    var vpnShortcutPath = Path.Combine(
        temporaryRoot,
        "Public Desktop",
        "Mullvad VPN.lnk");
    var openVpnShortcutPath = Path.Combine(
        temporaryRoot,
        "Common Start Menu",
        "OpenVPN GUI.lnk");
    var applicationCatalog = new WindowsApplicationCatalogService(
        [
            new WindowsApplicationCatalogEntry(
                "Mullvad VPN",
                vpnShortcutPath,
                "공용 바탕 화면",
                IsDirectory: false,
                DateTime.UtcNow),
            new WindowsApplicationCatalogEntry(
                "OpenVPN GUI",
                openVpnShortcutPath,
                "공용 시작 메뉴",
                IsDirectory: false,
                DateTime.UtcNow),
            new WindowsApplicationCatalogEntry(
                "Unrelated Editor",
                Path.Combine(temporaryRoot, "Editor.exe"),
                "설치된 프로그램",
                IsDirectory: false,
                DateTime.UtcNow)
        ]);
    var vpnApplicationMatches = await applicationCatalog.SearchAsync(
        vpnRelatedIntent,
        maximumResults: 20,
        CancellationToken.None);
    Assert(
        vpnApplicationMatches.Count == 2 &&
        vpnApplicationMatches.Any(match =>
            match.Entry.FullPath == vpnShortcutPath) &&
        vpnApplicationMatches.Any(match =>
            match.Entry.FullPath == openVpnShortcutPath) &&
        vpnApplicationMatches.All(match =>
            !match.Entry.Name.Contains(
                "Unrelated",
                StringComparison.OrdinalIgnoreCase)),
        "공용 바탕화면과 공용 시작 메뉴의 VPN 바로가기를 전역 앱 카탈로그에서 발견");
    if (OperatingSystem.IsWindows())
    {
        var commonDesktopVpn = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonDesktopDirectory),
            "Mullvad VPN.lnk");
        var commonStartMenuVpn = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartMenu),
            "Programs",
            "Mullvad VPN.lnk");
        if (File.Exists(commonDesktopVpn) &&
            File.Exists(commonStartMenuVpn))
        {
            var liveApplicationMatches =
                await new WindowsApplicationCatalogService().SearchAsync(
                    vpnRelatedIntent,
                    maximumResults: 50,
                    CancellationToken.None);
            Assert(
                liveApplicationMatches.Any(match =>
                    string.Equals(
                        match.Entry.FullPath,
                        commonDesktopVpn,
                        StringComparison.OrdinalIgnoreCase)) &&
                liveApplicationMatches.Any(match =>
                    string.Equals(
                        match.Entry.FullPath,
                        commonStartMenuVpn,
                        StringComparison.OrdinalIgnoreCase)),
                "실제 Windows 공용 바탕화면과 시작 메뉴의 VPN 바로가기를 발견");
        }
    }

    var aiTagIntent = SearchQueryInterpreter.Interpret(
        "AI 태그가 담긴 파일을 찾아줘");
    var aiTagCandidates = ContentSearchService.FindCandidates(
        aiTagIntent,
        [new ContentDocumentRecord
        {
            Name = Path.GetFileName(aiContextCodeFile),
            FullPath = aiContextCodeFile,
            DirectoryPath = sourceFolder.FullName,
            Extension = ".txt",
            ModifiedUtc = DateTime.UtcNow,
            Text = await File.ReadAllTextAsync(aiContextCodeFile),
            Source = DocumentContentSource.PlainText
        }],
        maximumResults: 20,
        progress: null,
        CancellationToken.None);
    Assert(
        aiTagIntent.Terms.Any(term =>
            term.Original.Equals(
                "ai",
                StringComparison.OrdinalIgnoreCase)) &&
        aiTagIntent.Terms.Any(term =>
            term.Original.Equals(
                "태그",
                StringComparison.OrdinalIgnoreCase)) &&
        aiTagCandidates.Any(candidate =>
            candidate.Document.FullPath == aiContextCodeFile),
        "AI 생성 태그 질의가 Negative prompt·CFG·Sampler·Seed 본문을 발견",
        $"terms={string.Join(';', aiTagIntent.Terms.Select(term => $"{term.Original}=[{string.Join(',', term.ContentEvidenceAlternatives)}]"))}; candidates={aiTagCandidates.Count}");

    Assert(
        new AppSettings().UseSystemTrayBackground,
        "시스템 트레이 백그라운드 실행 옵션 기본값 유지");

    var drawingIntent = SearchQueryInterpreter.Interpret(
        "건물 도면 파일을 찾아줘");
    Assert(
        drawingIntent.Categories.Contains(FileCategory.CadDrawing) &&
        drawingIntent.Terms.Any(term => term.Original == "건물") &&
        drawingIntent.LiteralTerms.Any(term => term.Original == "도면"),
        "도면을 파일 종류와 실제 이름 단서로 동시에 보존");

    var compactFloor = SearchTextAnalyzer.ExtractFloorReferences(
        "지하3층 도면");
    var spacedFloor = SearchTextAnalyzer.ExtractFloorReferences(
        "지하 3 층 도면");
    var englishFloor = SearchTextAnalyzer.ExtractFloorReferences(
        "B3F drawing");
    Assert(
        compactFloor.SequenceEqual(spacedFloor) &&
        compactFloor.SequenceEqual(englishFloor) &&
        compactFloor.Single() == new SearchFloorReference(true, 3),
        "지하3층·지하 3층·B3F를 같은 구조화 위치로 해석");
    Assert(
        !SearchTextAnalyzer.ContainsAllFloorReferences(
            compactFloor,
            "지하 13층 도면") &&
        !SearchTextAnalyzer.ContainsAllFloorReferences(
            compactFloor,
            "지상 3층 도면"),
        "층수 부분 일치와 지하·지상 혼동 방지");

    var drawingTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "건물 도면 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 200,
            progress: new CollectingProgress<TitleSearchProgress>(
                drawingTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        drawingTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                drawingFolder.FullName,
                StringComparison.OrdinalIgnoreCase)),
        "빠른 이름·경로 검색에서 실제 도면 폴더 복구");

    var compactFloorTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "지하3층 도면",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 200,
            progress: new CollectingProgress<TitleSearchProgress>(
                compactFloorTitleEvents),
            cancellationToken: CancellationToken.None);
    var spacedFloorTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "지하 3층 도면",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 200,
            progress: new CollectingProgress<TitleSearchProgress>(
                spacedFloorTitleEvents),
            cancellationToken: CancellationToken.None);
    var compactFloorPaths = compactFloorTitleEvents
        .SelectMany(item => item.NewHits)
        .Select(hit => hit.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var spacedFloorPaths = spacedFloorTitleEvents
        .SelectMany(item => item.NewHits)
        .Select(hit => hit.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        compactFloorPaths.SetEquals(spacedFloorPaths) &&
        compactFloorPaths.Contains(basementDrawingFile) &&
        !compactFloorPaths.Contains(basementThirteenthDrawingFile),
        "공백 유무와 관계없는 층수 빠른 검색 결과");

    var refinementNow = new DateTime(
        2026,
        7,
        31,
        0,
        0,
        0,
        DateTimeKind.Utc);
    var refinementSource = new[]
    {
        new SearchResult
        {
            Name = "건물 배관 도면.dwg",
            FullPath = basementDrawingFile,
            DirectoryPath = basementThirdFloorFolder.FullName,
            TypeDisplay = "DWG CAD 도면",
            ModifiedDisplay = "2026-07-30 10:00",
            CreatedUtc = refinementNow.AddDays(-1),
            ModifiedUtc = refinementNow.AddHours(-2),
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 900d,
            MatchPercent = 95d
        },
        new SearchResult
        {
            Name = "건물 배관 도면.dwg",
            FullPath = basementThirteenthDrawingFile,
            DirectoryPath = basementThirteenthFloorFolder.FullName,
            TypeDisplay = "DWG CAD 도면",
            ModifiedDisplay = "2025-01-01 10:00",
            CreatedUtc = refinementNow.AddDays(-300),
            ModifiedUtc = refinementNow.AddDays(-200),
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 880d,
            MatchPercent = 92d
        }
    };
    var compactRefinement = ResultRefinementService.Refine(
        "지하3층 도면",
        refinementSource,
        refinementNow);
    var spacedRefinement = ResultRefinementService.Refine(
        "지하 3층 도면",
        refinementSource,
        refinementNow);
    Assert(
        compactRefinement.Results
            .Select(result => result.FullPath)
            .SequenceEqual(spacedRefinement.Results.Select(
                result => result.FullPath)) &&
        compactRefinement.Results.Count == 1 &&
        compactRefinement.Results[0].FullPath == basementDrawingFile,
        "현재 결과 내 재검색도 층수 공백을 동일하게 처리");
    var clearedRefinement = ResultRefinementService.Refine(
        string.Empty,
        refinementSource,
        refinementNow);
    Assert(
        !clearedRefinement.IsApplied &&
        clearedRefinement.Results.Count == refinementSource.Length,
        "결과 내 검색 조건을 지우면 원래 후보 복원");
    var recentRefinement = ResultRefinementService.Refine(
        "도면 파일을 찾고 최근에 만들어진 파일일수록 더 위로",
        refinementSource,
        refinementNow);
    Assert(
        recentRefinement.Results.First().FullPath == basementDrawingFile,
        "결과 내 재검색에서 자연어 생성일 우선순위 적용");

    var hangulAttributeIntent = SearchQueryInterpreter.Interpret(
        "한글이 들어간 파일만 찾아줘");
    Assert(
        hangulAttributeIntent.Terms.Count == 0 &&
        hangulAttributeIntent.FilesOnly &&
        hangulAttributeIntent.AttributePredicates.Count == 1 &&
        hangulAttributeIntent.AttributePredicates[0] is
        {
            Script: SearchTextScript.Hangul,
            Scope: SearchTextAttributeScope.NameOrContent,
            Mode: SearchTextAttributeMode.Contains
        },
        "한글 포함 문장을 일반 키워드가 아닌 문자 조건으로 해석");
    var contentOnlyHangulIntent = SearchQueryInterpreter.Interpret(
        "내용에 한글이 포함된 파일만 찾아줘");
    Assert(
        contentOnlyHangulIntent.AttributePredicates.Single().Scope ==
        SearchTextAttributeScope.Content,
        "본문 한글 조건의 검사 범위 해석");
    var hangulOnlyNameIntent = SearchQueryInterpreter.Interpret(
        "한글로만 된 파일을 찾아줘");
    Assert(
        hangulOnlyNameIntent.Terms.Count == 0 &&
        hangulOnlyNameIntent.FilesOnly &&
        hangulOnlyNameIntent.AttributePredicates.Single() is
        {
            Script: SearchTextScript.Hangul,
            Scope: SearchTextAttributeScope.Name,
            Mode: SearchTextAttributeMode.Only
        },
        "한글로만 된 파일을 확장자 제외 파일명 전용 조건으로 해석");
    foreach (var naturalVariant in new[]
             {
                 "파일명이 전부 한글로 된 파일",
                 "파일 이름 중에 한글로만 된 파일",
                 "파일 이름 중에 한글로만 된걸 찾아줘",
                 "한글만 쓴 이름의 파일"
             })
    {
        var variantIntent =
            SearchQueryInterpreter.Interpret(naturalVariant);
        Assert(
            variantIntent.FilesOnly &&
            variantIntent.Terms.Count == 0 &&
            variantIntent.AttributePredicates.Single() is
            {
                Script: SearchTextScript.Hangul,
                Scope: SearchTextAttributeScope.Name,
                Mode: SearchTextAttributeMode.Only
            },
            $"한글 전용 파일명 자연어 변형 해석: {naturalVariant}");
    }
    Assert(
        SearchTextAttributeAnalyzer.IsMatch(
            SearchTextAttributeAnalyzer.Analyze("회의록_최종(서명)"),
            SearchTextScript.Hangul,
            SearchTextAttributeMode.Only) &&
        !SearchTextAttributeAnalyzer.IsMatch(
            SearchTextAttributeAnalyzer.Analyze("회의록3층"),
            SearchTextScript.Hangul,
            SearchTextAttributeMode.Only) &&
        !SearchTextAttributeAnalyzer.IsMatch(
            SearchTextAttributeAnalyzer.Analyze("회의록_final"),
            SearchTextScript.Hangul,
            SearchTextAttributeMode.Only),
        "한글 전용 파일명은 공백·구분자를 허용하고 숫자·영문을 제외");
    var hangulOnlyTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "한글로만 된 파일을 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                hangulOnlyTitleEvents),
            cancellationToken: CancellationToken.None);
    var hangulOnlyTitleHits = hangulOnlyTitleEvents
        .SelectMany(item => item.NewHits)
        .DistinctBy(
            item => item.FullPath,
            StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Assert(
        hangulOnlyTitleHits.Any(hit =>
            hit.FullPath == basementDrawingFile) &&
        hangulOnlyTitleHits.All(hit =>
            SearchTextAttributeAnalyzer.IsMatch(
                SearchTextAttributeAnalyzer.Analyze(
                    Path.GetFileNameWithoutExtension(hit.Name)),
                SearchTextScript.Hangul,
                SearchTextAttributeMode.Only)),
        "빠른 이름 검색도 확장자를 제외한 한글 전용 파일명만 발견");
    var hwpIntent = SearchQueryInterpreter.Interpret(
        "한글 파일을 찾아줘");
    Assert(
        hwpIntent.AttributePredicates.Count == 0 &&
        hwpIntent.RequestedExtensions.Contains(".hwp") &&
        hwpIntent.RequestedExtensions.Contains(".hwpx"),
        "한글 파일은 HWP·HWPX 형식 요청으로 구분");

    var attributeRefinementSource = new[]
    {
        new SearchResult
        {
            Name = Path.GetFileName(privateKeyFile),
            FullPath = privateKeyFile,
            DirectoryPath = sourceFolder.FullName,
            TypeDisplay = "PPK 파일",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 500d,
            MatchPercent = 80d
        },
        new SearchResult
        {
            Name = Path.GetFileName(certificateFile),
            FullPath = certificateFile,
            DirectoryPath = sourceFolder.FullName,
            TypeDisplay = "PEM 파일",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 480d,
            MatchPercent = 78d
        }
    };
    var attributeFacts =
        new Dictionary<string, SearchResultTextFacts>(
            StringComparer.OrdinalIgnoreCase)
        {
            [privateKeyFile] = new(
                ContentKnown: true,
                SearchTextAttributeAnalyzer.Analyze(
                    "이 문서에는 실제 한글 내용이 있습니다.")),
            [certificateFile] = new(
                ContentKnown: true,
                SearchTextAttributeAnalyzer.Analyze(
                    "certificate content only"))
        };
    var hangulContentRefinement =
        ResultRefinementService.Refine(
            "한글이 들어간 파일만 찾아줘",
            attributeRefinementSource,
            attributeFacts);
    Assert(
        hangulContentRefinement.Results.Count == 1 &&
        hangulContentRefinement.Results[0].FullPath ==
        privateKeyFile &&
        hangulContentRefinement.UnknownCount == 0,
        "현재 결과의 실제 본문 한글 속성으로 재검색");
    var hangulOnlyRefinementSource = new[]
    {
        new SearchResult
        {
            Name = "도면.pdf",
            FullPath = Path.Combine(sourceFolder.FullName, "도면.pdf"),
            DirectoryPath = sourceFolder.FullName,
            TypeDisplay = "PDF 문서",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 510d,
            MatchPercent = 81d
        },
        new SearchResult
        {
            Name = "건물 배관 도면.dwg",
            FullPath = basementDrawingFile,
            DirectoryPath = basementThirdFloorFolder.FullName,
            TypeDisplay = "DWG CAD 도면",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 500d,
            MatchPercent = 80d
        },
        new SearchResult
        {
            Name = "도면3층.pdf",
            FullPath = Path.Combine(sourceFolder.FullName, "도면3층.pdf"),
            DirectoryPath = sourceFolder.FullName,
            TypeDisplay = "PDF 문서",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 490d,
            MatchPercent = 79d
        },
        new SearchResult
        {
            Name = "도면_final.pdf",
            FullPath = Path.Combine(
                sourceFolder.FullName,
                "도면_final.pdf"),
            DirectoryPath = sourceFolder.FullName,
            TypeDisplay = "PDF 문서",
            ModifiedDisplay = "2026-07-30 10:00",
            Reason = "기존 검색 결과",
            IconGlyph = string.Empty,
            Score = 480d,
            MatchPercent = 78d
        }
    };
    var hangulOnlyRefinement = ResultRefinementService.Refine(
        "한글로만 된 파일을 찾아줘",
        hangulOnlyRefinementSource);
    Assert(
        hangulOnlyRefinement.Results
            .Select(result => result.Name)
            .SequenceEqual(["도면.pdf", "건물 배관 도면.dwg"]) &&
        hangulOnlyRefinement.Interpretation.Contains(
            "파일명 한글 만",
            StringComparison.Ordinal),
        "결과 내 검색에서 확장자를 제외한 한글 전용 파일명만 유지");
    var unknownHangulRefinement =
        ResultRefinementService.Refine(
            "내용에 한글이 포함된 파일만 찾아줘",
            attributeRefinementSource);
    Assert(
        unknownHangulRefinement.Results.Count == 0 &&
        unknownHangulRefinement.UnknownCount ==
        attributeRefinementSource.Length,
        "내용 미확인 결과를 불일치와 구분");

    var history = new NavigationHistory();
    history.Record(contentRoot);
    history.Record(sourceFolder.FullName);
    Assert(history.CanGoBack, "뒤로 이동 기록");
    Assert(history.Back() == contentRoot, "뒤로 이동 경로");
    Assert(history.Forward() == sourceFolder.FullName, "앞으로 이동 경로");

    var computerNode = new NavigationNode(
        "내 PC",
        null,
        string.Empty,
        NavigationNodeKind.Computer);
    Assert(computerNode.IsSelectable, "내 PC 탐색 노드 선택 가능");

    var favoriteFolder = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "favorite-folder"));
    Assert(
        FavoritePathService.TryResolve(
            favoriteFolder.FullName,
            out var favoriteDropTarget,
            out _) &&
        favoriteDropTarget is not null &&
        string.Equals(
            FavoritePathService.GetIdentity(favoriteDropTarget.Path),
            FavoritePathService.GetIdentity(favoriteFolder.FullName),
            StringComparison.OrdinalIgnoreCase),
        "폴더 드래그 즐겨찾기 등록");
    var favoriteNode = new NavigationNode(
        "즐겨찾는 폴더",
        favoriteFolder.FullName,
        string.Empty,
        NavigationNodeKind.Favorite);
    Assert(favoriteNode.IsSelectable, "즐겨찾기 탐색 노드 선택 가능");
    var favoriteUrl = Path.Combine(temporaryRoot, "favorite-folder.url");
    await File.WriteAllTextAsync(
        favoriteUrl,
        $"[InternetShortcut]{Environment.NewLine}URL={new Uri(favoriteFolder.FullName).AbsoluteUri}");
    Assert(
        FavoritePathService.TryResolve(
            favoriteUrl,
            out var favoriteUrlTarget,
            out _) &&
        favoriteUrlTarget is not null &&
        string.Equals(
            FavoritePathService.GetIdentity(favoriteUrlTarget.Path),
            FavoritePathService.GetIdentity(favoriteFolder.FullName),
            StringComparison.OrdinalIgnoreCase),
        "URL 바로가기 드래그 즐겨찾기 등록");

    Assert(
        FavoritePathService.TryCreateFolderTarget(
            favoriteFolder.FullName,
            "우클릭 폴더",
            out var favoriteContextTarget,
            out _) &&
        favoriteContextTarget is not null &&
        favoriteContextTarget.Name == "우클릭 폴더",
        "폴더 우클릭 즐겨찾기 등록");

    var favoriteOrder = new List<FavoriteLocation>
    {
        new() { Name = "첫 번째", Path = Path.Combine(temporaryRoot, "fav-1") },
        new() { Name = "두 번째", Path = Path.Combine(temporaryRoot, "fav-2") },
        new() { Name = "세 번째", Path = Path.Combine(temporaryRoot, "fav-3") }
    };
    Assert(
        FavoritePathService.MoveFavorite(
            favoriteOrder,
            favoriteOrder[0].Path,
            favoriteOrder[2].Path,
            insertAfter: true) &&
        favoriteOrder.Select(item => item.Name).SequenceEqual(
            ["두 번째", "세 번째", "첫 번째"]),
        "즐겨찾기 드래그 순서 변경");

    var favoritesSectionNode = new NavigationNode(
        "즐겨찾기",
        null,
        string.Empty,
        NavigationNodeKind.FavoritesSection);
    Assert(!favoritesSectionNode.IsSelectable, "즐겨찾기 섹션은 탐색 대상이 아님");

    Assert(
        new SearchRequest("test", [contentRoot]).MaximumResults >= 500,
        "기본 검색 결과 500개 표시");
    var progressiveFastRequest = new SearchRequest(
        "test",
        [contentRoot],
        IndexingMode: SearchIndexingMode.ExistingIndexOnly,
        AllowTargetedScan: false,
        IncludeAiCandidates: false);
    Assert(
        !progressiveFastRequest.AllowTargetedScan &&
        !progressiveFastRequest.IncludeAiCandidates,
        "점진 검색 첫 단계는 직접 재탐색과 AI 실행을 생략");

    var resultVolumeRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "result-volume"));
    for (var index = 0; index < 60; index++)
    {
        await File.WriteAllTextAsync(
            Path.Combine(
                resultVolumeRoot.FullName,
                $"원천징수_자료_{index:000}.txt"),
            "원천징수 자료");
    }
    var resultVolume = await new TargetedFileSearchService().FindAsync(
        resultVolumeRoot.FullName,
        SearchQueryInterpreter.Interpret("원천징수 자료 파일을 찾아줘"),
        maximumResults: 500,
        includeVisualTypeFallback: false,
        progress: null,
        CancellationToken.None);
    Assert(
        resultVolume.Records.Count >= 60,
        "첫 완전 일치 이후에도 관련 파일 계속 수집");
    Assert(
        resultVolume.Records.All(record =>
            !Path.GetFileName(record.FullPath).StartsWith(
                "~",
                StringComparison.Ordinal)),
        "물결표로 시작하는 임시 파일은 직접 검색에서 제외");

    var liveBatchCount = 0;
    var liveBatchItems = 0;
    _ = await new TargetedFileSearchService().FindAsync(
        resultVolumeRoot.FullName,
        SearchQueryInterpreter.Interpret("원천징수 자료 파일을 찾아줘"),
        maximumResults: 500,
        includeVisualTypeFallback: false,
        progress: null,
        liveBatch: (batch, _, _) =>
        {
            liveBatchCount++;
            liveBatchItems += batch.Count;
        },
        cancellationToken: CancellationToken.None);
    Assert(
        liveBatchCount >= 2 && liveBatchItems >= 60,
        "직접 탐색 결과를 작은 묶음으로 점진 전달",
        $"배치 {liveBatchCount:N0}회 · 항목 {liveBatchItems:N0}개");

    var weakMultiTermCandidate = SearchRankingService.ScoreCandidate(
        SearchQueryInterpreter.Interpret(
            "개인정보 보호망 접속 자격 문서를 찾아줘"),
        new IndexedFileRecord
        {
            Name = "개인정보_한단어만_있는_무관자료.txt",
            FullPath = Path.Combine(contentRoot, "개인정보_한단어만_있는_무관자료.txt"),
            DirectoryPath = contentRoot,
            Extension = ".txt",
            IsDirectory = false,
            SizeBytes = 10,
            ModifiedUtc = DateTime.UtcNow
        });
    var coherentMultiTermCandidate = SearchRankingService.ScoreCandidate(
        SearchQueryInterpreter.Interpret(
            "개인정보 보호망 접속 자격 문서를 찾아줘"),
        new IndexedFileRecord
        {
            Name = "개인정보_보호망_접속_자격.txt",
            FullPath = Path.Combine(
                contentRoot,
                "개인정보_보호망_접속_자격.txt"),
            DirectoryPath = contentRoot,
            Extension = ".txt",
            IsDirectory = false,
            SizeBytes = 10,
            ModifiedUtc = DateTime.UtcNow
        });
    Assert(
        weakMultiTermCandidate is not null &&
        coherentMultiTermCandidate is not null &&
        coherentMultiTermCandidate.Score > weakMultiTermCandidate.Score,
        "약한 다중 검색어 후보를 보존하되 문맥이 완전한 파일보다 낮게 배치");

    var contextIntent = SearchQueryInterpreter.Interpret(
        "개인정보 보호망 접속 자격 문서를 찾아줘");
    var coherentContext = new ContentDocumentRecord
    {
        Name = "coherent.txt",
        FullPath = Path.Combine(contentRoot, "coherent.txt"),
        DirectoryPath = contentRoot,
        Extension = ".txt",
        ModifiedUtc = DateTime.UtcNow,
        Text = "개인정보 보호망 접속 자격을 발급하는 절차와 담당자 안내",
        Source = DocumentContentSource.PlainText
    };
    var scatteredContext = new ContentDocumentRecord
    {
        Name = "scattered.txt",
        FullPath = Path.Combine(contentRoot, "scattered.txt"),
        DirectoryPath = contentRoot,
        Extension = ".txt",
        ModifiedUtc = DateTime.UtcNow,
        Text = "개인정보" + new string('가', 1800) +
               "보호망" + new string('나', 1800) +
               "접속" + new string('다', 1800) + "자격",
        Source = DocumentContentSource.PlainText
    };
    var contextCandidates = ContentSearchService.FindCandidates(
        contextIntent,
        [scatteredContext, coherentContext],
        maximumResults: 20,
        progress: null,
        CancellationToken.None);
    Assert(
        contextCandidates.FirstOrDefault()?.Document.FullPath ==
        coherentContext.FullPath,
        "본문 단어 개수보다 같은 문맥의 결합을 우선");

    var connectedShare = new ConnectedNetworkShareInfo(
        @"\\server\자료실",
        "자료실",
        "Windows SMB 연결");
    Assert(
        connectedShare.Name == "자료실" &&
        connectedShare.ServerRoot == @"\\server",
        "연결된 UNC 공유 폴더 표시 정보");

    if (OperatingSystem.IsWindows())
    {
        Assert(
            string.Equals(
                NetworkPathService.NormalizeDirectoryPath(@"Z:"),
                @"Z:\",
                StringComparison.OrdinalIgnoreCase),
            "매핑 드라이브 루트의 역슬래시 보존");
        Assert(
            string.Equals(
                NetworkPathService
                    .NormalizeDirectoryPath(@"\\server\share\")
                    .TrimEnd(Path.DirectorySeparatorChar),
                @"\\server\share",
                StringComparison.OrdinalIgnoreCase),
            "UNC 공유 루트 정규화");
        Assert(
            NetworkPathService.IsUncPath(@"\\server\share\folder"),
            "UNC 네트워크 경로 판별");
        Assert(
            string.Equals(
                NetworkPathService.NormalizeNetworkLocationPath("192.168.0.10"),
                @"\\192.168.0.10",
                StringComparison.OrdinalIgnoreCase),
            "IP 주소만 입력해 UNC 서버 루트로 정규화");
        Assert(
            NetworkPathService.IsUncServerRoot(@"\\192.168.0.10"),
            "UNC 서버 최상위 위치 판별");
        Assert(
            string.Equals(
                NetworkPathService.GetUncServerRoot(@"\\server\share\folder"),
                @"\\server",
                StringComparison.OrdinalIgnoreCase),
            "UNC 서버 루트 추출");
        Assert(
            string.Equals(
                NetworkPathService.GetUncShareRoot(@"\\server\share\folder"),
                @"\\server\share",
                StringComparison.OrdinalIgnoreCase),
            "UNC 공유 루트 추출");
        Assert(
            string.Equals(
                NetworkPathService.GetNetworkParentPath(@"\\server\share"),
                @"\\server",
                StringComparison.OrdinalIgnoreCase),
            "공유 폴더에서 서버 최상위로 이동");
    }

    var fileSystem = new FileSystemService();
    var entries = await fileSystem.GetEntriesAsync(
        contentRoot,
        FileSortMode.Name,
        CancellationToken.None);
    Assert(
        entries.Count == 3 &&
        entries.Any(entry => entry.FullPath == drawingFolder.FullName),
        "폴더 목록");
    Assert(entries.All(entry => entry.IsDirectory), "폴더 형식 판별");

    var accountManagementFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "계정관리문서"));
    var accountManagementFile = Path.Combine(
        accountManagementFolder.FullName,
        "IT팀_계정관리.xlsx");
    await CreateSpreadsheetFixtureAsync(
        accountManagementFile,
        "계정관리",
        [["담당 부서별 계정 목록"]]);
    var genericAccountFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "장비현황"));
    var genericAccountWorkbook = Path.Combine(
        genericAccountFolder.FullName,
        "계정 목록.xlsx");
    const string internalEquipmentName = "NEPTUNE-FW-8842";
    await CreateSpreadsheetFixtureAsync(
        genericAccountWorkbook,
        "장비 계정",
        [
            ["관리 대상", "운영 구분"],
            [internalEquipmentName, "관리자 계정"]
        ]);
    var accountFolderOnlyFile = Path.Combine(
        accountManagementFolder.FullName,
        "담당자_배포목록.txt");
    await File.WriteAllTextAsync(
        accountFolderOnlyFile,
        "담당자와 배포 시기");
    var accountNoiseFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "일반_참고자료"));
    for (var index = 0; index < 8; index++)
    {
        await File.WriteAllTextAsync(
            Path.Combine(
                accountNoiseFolder.FullName,
                $"연결_참고_{index:00}.txt"),
            "네트워크 연결 과정에서 사용하는 계정 참고 문구");
    }

    var accountIntent = SearchQueryInterpreter.Interpret(
        "네트워크 계정 관련 문서를 찾아줘");
    Assert(
        accountIntent.Categories.Contains(FileCategory.Document) &&
        accountIntent.Categories.Contains(FileCategory.Spreadsheet) &&
        accountIntent.Categories.Contains(FileCategory.Presentation),
        "일반 문서 요청은 스프레드시트와 프레젠테이션도 포함");
    var accountTerm = accountIntent.Terms.First(term =>
        term.Original.Equals(
            "계정",
            StringComparison.OrdinalIgnoreCase));
    Assert(
        accountTerm.Alternatives.Contains(
            "로그인",
            StringComparer.OrdinalIgnoreCase) &&
        accountTerm.ContentEvidenceAlternatives.Contains(
            "account",
            StringComparer.OrdinalIgnoreCase) &&
        !accountTerm.ContentEvidenceAlternatives.Contains(
            "로그인",
            StringComparer.OrdinalIgnoreCase) &&
        !accountTerm.ContentEvidenceAlternatives.Contains(
            "인증",
            StringComparer.OrdinalIgnoreCase) &&
        !accountTerm.ContentEvidenceAlternatives.Contains(
            "접속",
            StringComparer.OrdinalIgnoreCase),
        "계정 의미 확장과 직접 본문 근거를 분리");
    var inspectionSheet = new ContentDocumentRecord
    {
        Name = "부성형외과_네트워크_점검표.xlsx",
        FullPath = Path.Combine(
            contentRoot,
            "부성형외과_네트워크_점검표.xlsx"),
        DirectoryPath = contentRoot,
        Extension = ".xlsx",
        SizeBytes = 1024,
        ModifiedUtc = DateTime.UtcNow,
        Text = "네트워크 접속 상태 장비 점검 결과 정상",
        WasTruncated = false,
        Source = DocumentContentSource.Spreadsheet,
        AnalyzedPages = 0
    };
    var inspectionFalsePositives = ContentSearchService.FindCandidates(
        accountIntent,
        [inspectionSheet],
        maximumResults: 20,
        progress: null,
        CancellationToken.None);
    Assert(
        inspectionFalsePositives.Count == 0,
        "접속 점검표를 계정 내용 일치로 오인하지 않음");
    var translatedAccountIntent = SearchQueryInterpreter.Interpret(
        "계정 문서를 찾아줘");
    var translatedAccountSheet = new ContentDocumentRecord
    {
        Name = "사용자목록.xlsx",
        FullPath = Path.Combine(contentRoot, "사용자목록.xlsx"),
        DirectoryPath = contentRoot,
        Extension = ".xlsx",
        SizeBytes = 1024,
        ModifiedUtc = DateTime.UtcNow,
        Text = "network account owner list",
        WasTruncated = false,
        Source = DocumentContentSource.Spreadsheet,
        AnalyzedPages = 0
    };
    var translatedAccountCandidate = ContentSearchService.FindCandidates(
        translatedAccountIntent,
        [translatedAccountSheet],
        maximumResults: 20,
        progress: null,
        CancellationToken.None).Single();
    Assert(
        translatedAccountCandidate.Reason.Contains(
            "실제 일치 ‘account’",
            StringComparison.Ordinal),
        "번역어 본문 근거에 실제 일치 단어 표시");
    var accountNameCandidate = SearchRankingService.ScoreCandidate(
        accountIntent,
        new IndexedFileRecord
        {
            Name = Path.GetFileName(accountManagementFile),
            FullPath = accountManagementFile,
            DirectoryPath = accountManagementFolder.FullName,
            Extension = ".xlsx",
            IsDirectory = false,
            SizeBytes = new FileInfo(accountManagementFile).Length,
            ModifiedUtc = DateTime.UtcNow
        });
    Assert(
        accountNameCandidate is { NameMatchCount: 1 },
        "두 단어 질의에서도 직접 파일명 계정 단서를 후보로 유지");

    var accountTitleEvents = new List<TitleSearchProgress>();
    _ = await new TitleSearchService(new NetworkPathService())
        .SearchAsync(
            "네트워크 계정 관련 문서를 찾아줘",
            [contentRoot],
            maximumScannedItems: 10_000,
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                accountTitleEvents),
            cancellationToken: CancellationToken.None);
    Assert(
        accountTitleEvents
            .SelectMany(item => item.NewHits)
            .Any(hit => string.Equals(
                hit.FullPath,
                accountManagementFile,
                StringComparison.OrdinalIgnoreCase)),
        "계정 일부 일치 파일을 독립 제목 검색에서 발견");

    var search = new MetadataSearchService(
        fileSystem,
        Path.Combine(temporaryRoot, "_test-index"));
    var hangulOnlyNameResponse = await search.SearchAsync(
        new SearchRequest(
            "한글로만 된 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        hangulOnlyNameResponse.Results.Any(result =>
            result.FullPath == basementDrawingFile) &&
        hangulOnlyNameResponse.Results.All(result =>
            SearchTextAttributeAnalyzer.IsMatch(
                SearchTextAttributeAnalyzer.Analyze(
                    Path.GetFileNameWithoutExtension(result.Name)),
                SearchTextScript.Hangul,
                SearchTextAttributeMode.Only)),
        "통합 검색도 확장자를 제외한 한글 전용 파일명만 발견");
    var hangulAttributeResponse = await search.SearchAsync(
        new SearchRequest(
            "한글이 들어간 파일만 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        hangulAttributeResponse.Results.Any(result =>
            result.FullPath == englishNamedHangulContentFile &&
            result.EvidenceKind is
                SearchEvidenceKind.Content or
                SearchEvidenceKind.Combined),
        "영문 파일명의 실제 한글 본문을 통합 조건 검색으로 발견");
    var storedHangulFacts = await search.GetResultTextFactsAsync(
        [contentRoot],
        [englishNamedHangulContentFile],
        maximumOnDemandDocuments: 0,
        progress: null,
        CancellationToken.None);
    Assert(
        storedHangulFacts[englishNamedHangulContentFile]
            .ContentKnown &&
        storedHangulFacts[englishNamedHangulContentFile]
            .ContentProfile.HangulCharacters > 0,
        "현재 결과 재검색이 저장된 본문 문자 속성을 재사용");

    var drawingResponse = await search.SearchAsync(
        new SearchRequest(
            "건물 도면 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        drawingResponse.Results.Any(result =>
            string.Equals(
                result.FullPath,
                drawingFolder.FullName,
                StringComparison.OrdinalIgnoreCase)),
        "통합 검색에서도 실제 도면 폴더를 이름·경로 후보로 유지");
    var compactFloorResponse = await search.SearchAsync(
        new SearchRequest(
            "지하3층 도면",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    var spacedFloorResponse = await search.SearchAsync(
        new SearchRequest(
            "지하 3층 도면",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    var compactIntegratedPaths = compactFloorResponse.Results
        .Select(result => result.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var spacedIntegratedPaths = spacedFloorResponse.Results
        .Select(result => result.FullPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        compactIntegratedPaths.SetEquals(spacedIntegratedPaths) &&
        compactIntegratedPaths.Contains(basementDrawingFile) &&
        !compactIntegratedPaths.Contains(
            basementThirteenthDrawingFile),
        "통합 검색의 지하3층·지하 3층 결과 동일성");

    var createdPreferenceResponse = await search.SearchAsync(
        new SearchRequest(
            "priorityfixture 파일을 찾고 최근에 만들어진 파일일수록 더 위로 오게 해줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    var oldRankingFixtureRank = FindResultRank(
        createdPreferenceResponse.Results,
        result => result.FullPath == oldRankingFixture);
    var newRankingFixtureRank = FindResultRank(
        createdPreferenceResponse.Results,
        result => result.FullPath == newRankingFixture);
    Assert(
        newRankingFixtureRank < oldRankingFixtureRank &&
        createdPreferenceResponse.Results
            .First(result => result.FullPath == newRankingFixture)
            .Reason.Contains(
                "생성일 최신 순",
                StringComparison.Ordinal) &&
        createdPreferenceResponse.Diagnostics.IntentSummary.Contains(
            "생성일 최신 순",
            StringComparison.Ordinal),
        "통합 검색 최종 순위와 결과 근거에 생성일 자연어 가중치를 반영",
        $"신규 파일 순위: {FormatResultRank(newRankingFixtureRank)}, " +
        $"기존 파일 순위: {FormatResultRank(oldRankingFixtureRank)}");
    var accountResponse = await search.SearchAsync(
        new SearchRequest(
            "네트워크 계정 관련 문서를 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    var accountManagementRank = FindResultRank(
        accountResponse.Results,
        result => string.Equals(
            result.FullPath,
            accountManagementFile,
            StringComparison.OrdinalIgnoreCase));
    var accountNoisePrefix =
        accountNoiseFolder.FullName + Path.DirectorySeparatorChar;
    var firstAccountNoiseRank = FindResultRank(
        accountResponse.Results,
        result => result.FullPath.StartsWith(
            accountNoisePrefix,
            StringComparison.OrdinalIgnoreCase));
    Assert(
        accountManagementRank < firstAccountNoiseRank,
        "본문에 두 단어가 있는 잡음보다 IT팀 계정관리 파일명을 우선",
        $"IT팀 계정관리 순위: {FormatResultRank(accountManagementRank)}, " +
        $"첫 본문 잡음 순위: {FormatResultRank(firstAccountNoiseRank)}" +
        Environment.NewLine +
        string.Join(
            Environment.NewLine,
            accountResponse.Results
                .Take(12)
                .Select((result, index) =>
                    $"{index + 1}. {result.Name} | " +
                    $"{result.MatchDisplay} | {result.Reason}")));
    Assert(
        accountResponse.Results.Any(result =>
            result.FullPath == accountFolderOnlyFile),
        "계정관리문서 상위 폴더 단서로 내부 문서 발견");

    var spreadsheetExtractor = new DocumentTextExtractor();
    Assert(
        spreadsheetExtractor.CanExtract(".xls") &&
        spreadsheetExtractor.CanExtract(".xlsx") &&
        spreadsheetExtractor.CanExtract(".xlsm") &&
        spreadsheetExtractor.CanExtract(".xlsb"),
        "구형·현재·바이너리 엑셀 형식 지원");
    var extractedWorkbook = await spreadsheetExtractor.ExtractAsync(
        genericAccountWorkbook,
        CancellationToken.None);
    Assert(
        extractedWorkbook is
        {
            Source: DocumentContentSource.Spreadsheet
        } &&
        extractedWorkbook.Text.Contains(
            "장비 계정",
            StringComparison.OrdinalIgnoreCase) &&
        extractedWorkbook.Text.Contains(
            internalEquipmentName,
            StringComparison.OrdinalIgnoreCase),
        "엑셀 시트명과 내부 셀 값 직접 추출");
    var equipmentAccountResponse = await search.SearchAsync(
        new SearchRequest(
            $"{internalEquipmentName} 계정 문서를 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    var equipmentAccountResult =
        equipmentAccountResponse.Results.FirstOrDefault();
    Assert(
        equipmentAccountResult?.FullPath == genericAccountWorkbook,
        "평범한 엑셀 제목과 내부 장비명을 결합해 최상위 검색",
        string.Join(
            Environment.NewLine,
            equipmentAccountResponse.Results
                .Take(5)
                .Select(result =>
                    $"{result.Name} | {result.MatchDisplay} | {result.Reason}")));
    Assert(
        equipmentAccountResult is
        {
            EvidenceKind: SearchEvidenceKind.Combined
        } &&
        equipmentAccountResult.Reason.Contains(
            "엑셀",
            StringComparison.Ordinal),
        "엑셀 파일명·셀 내용 결합 근거 표시");

    var textResponse = await search.SearchAsync(
        new SearchRequest("14층 와이파이 끊김과 관련된 자료를 찾아줘", [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        textResponse.Results.Any(result => result.FullPath == sourceFile),
        "이름·경로 의미 단서 검색");
    Assert(
        textResponse.Results.All(result =>
            result.MatchPercent is >= 1d and <= 100d &&
            (result.WasAdvancedAnalyzed ||
             result.EvidenceKind is
                 SearchEvidenceKind.SemanticCandidate or
                 SearchEvidenceKind.VisualCandidate
                ? result.MatchDisplay.Contains('%') &&
                  result.RelevanceDisplay.StartsWith(
                      "연관성",
                      StringComparison.Ordinal)
                : !result.MatchDisplay.Contains('%') &&
                  (result.RelevanceDisplay.Contains(
                       "근거",
                       StringComparison.Ordinal) ||
                   result.RelevanceDisplay.Contains(
                       "일치",
                       StringComparison.Ordinal)))),
        "AI 확률과 결정적 파일 근거를 구분해 표시");

    var secondSearchRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "second-drive"));
    var secondRootTarget = Path.Combine(
        secondSearchRoot.FullName,
        "all_locations_mullvad_account.txt");
    await File.WriteAllTextAsync(
        secondRootTarget,
        "account recovery material");
    var allLocationsResponse = await search.SearchAsync(
        new SearchRequest(
            "mullvad account 파일",
            [contentRoot, secondSearchRoot.FullName]),
        progress: null,
        CancellationToken.None);
    Assert(
        allLocationsResponse.Results.Any(result =>
            result.FullPath == secondRootTarget),
        "여러 위치 일괄 검색에서 두 번째 루트 결과 발견");

    var uncommonExtensionResponse = await search.SearchAsync(
        new SearchRequest(
            ".artifact42 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        uncommonExtensionResponse.Results.Any(result =>
            result.FullPath == customFormatFile),
        "카탈로그에 없는 명시적 확장자를 통합 검색에서 발견");

    var bareExtensionResponse = await search.SearchAsync(
        new SearchRequest(
            "pem 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        bareExtensionResponse.Results.Any(result =>
            result.FullPath == certificateFile),
        "카탈로그에 없는 확장자를 파일명 메타데이터로 통합 검색");

    var naturalKeyResponse = await search.SearchAsync(
        new SearchRequest(
            "aws ssh키를 찾아달라",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        naturalKeyResponse.Results.Any(result =>
            result.FullPath == awsPrivateKeyFile),
        "AWS SSH 키 자연어 검색에서 PPK 파일을 통합 검색 후보로 발견");
    Assert(
        naturalKeyResponse.Results
            .Select(result => result.FullPath)
            .ToList()
            .IndexOf(awsPrivateKeyFile) <
        naturalKeyResponse.Results
            .Select(result => result.FullPath)
            .ToList()
            .IndexOf(awsMonkeyNoiseFile),
        "SSH key 의미가 맞는 PPK 파일을 AWS 단어만 맞는 문서보다 우선");

    var contextualKeyResponse = await search.SearchAsync(
        new SearchRequest(
            "aws 서버 접속 자격 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        contextualKeyResponse.Results.Any(result =>
            result.FullPath == awsPrivateKeyFile) &&
        contextualKeyResponse.Results
            .Select(result => result.FullPath)
            .ToList()
            .IndexOf(awsPrivateKeyFile) <
        contextualKeyResponse.Results
            .Select(result => result.FullPath)
            .ToList()
            .IndexOf(awsMonkeyNoiseFile),
        "AWS·서버·접속·자격 단서를 이름·PPK 형식·계정관리 경로로 결합");

    foreach (var naturalLanguageQuery in new[]
             {
                 "aws 키를 찾아줘",
                 "putty에서 쓰는 aws 개인키를 찾아줘",
                 "계정관리 aws 인증 키가 어디 있는지 찾아줘",
                 "aws 서버 로그인에 사용하는 자격 파일"
             })
    {
        var benchmarkResponse = await search.SearchAsync(
            new SearchRequest(
                naturalLanguageQuery,
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        var expectedRank = benchmarkResponse.Results
            .Select(result => result.FullPath)
            .ToList()
            .IndexOf(awsPrivateKeyFile);
        Assert(
            expectedRank is >= 0 and < 3,
            "AWS 키 자연어 변형 검색에서 정답 파일 Recall@3 보장",
            $"{naturalLanguageQuery} · 실제 순위 {expectedRank + 1:N0}");
    }

    var modelResponse = await search.SearchAsync(
        new SearchRequest("3d 모델링 파일을 찾아줘", [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        modelResponse.Results.Any(result => result.FullPath == modelFile),
        "3D 모델 유형 인식");
    Assert(
        modelResponse.Results.All(result => result.FullPath != falsePositiveFile),
        "긴 임의 파일명의 3D 오탐 방지");
    Assert(modelResponse.Diagnostics.UsedCachedIndex, "메타데이터 색인 재사용");
    Assert(
        FileTypeCatalog.GetTypeDisplay(".stl").Contains("3D 모델"),
        "3D 모델 표시 형식");

    var contentResponse = await search.SearchAsync(
        new SearchRequest(
            "mullvad 로그인 코드가 담긴 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(contentResponse.Diagnostics.UsedContentSearch, "파일 본문 검색 실행");
    Assert(
        contentResponse.Diagnostics.ContentIndexedDocuments > 0,
        "본문 문서 색인");
    Assert(
        contentResponse.Results.Any(
            result => result.FullPath == hiddenContentFile),
        "파일명과 무관한 Mullvad 로그인 코드 본문 검색");
    Assert(
        contentResponse.Results
            .First(result => result.FullPath == hiddenContentFile)
            .Reason
            .Contains("본문", StringComparison.Ordinal),
        "본문 일치 근거 표시");

    var utf16Response = await search.SearchAsync(
        new SearchRequest(
            "bluebird 장비 암호가 담긴 파일을 찾아줘",
            [contentRoot]),
        progress: null,
        CancellationToken.None);
    Assert(
        utf16Response.Results.Any(
            result => result.FullPath == utf16ContentFile),
        "UTF-16 BOM 텍스트 본문 검색");

    var fakeOcrExtractor = new DocumentTextExtractor(
        new FakeOcrTextExtractor());
    var extractedOcr = await fakeOcrExtractor.ExtractAsync(
        ocrImageFile,
        CancellationToken.None);
    Assert(
        extractedOcr is
        {
            Source: DocumentContentSource.ImageOcr,
            Text: not null
        } &&
        extractedOcr.Text.Contains(
            "849201",
            StringComparison.Ordinal),
        "이미지 OCR 추출 경로");
    var confirmedOcr = extractedOcr ??
                       throw new InvalidOperationException(
                           "OCR smoke fixture was not extracted.");
    var ocrContentCandidate = ContentSearchService.FindCandidates(
            SearchQueryInterpreter.Interpret(
                "849201 영수증을 찾아줘"),
            [
                new ContentDocumentRecord
                {
                    Name = Path.GetFileName(ocrImageFile),
                    FullPath = ocrImageFile,
                    DirectoryPath = sourceFolder.FullName,
                    Extension = ".png",
                    SizeBytes = 4,
                    ModifiedUtc = DateTime.UtcNow,
                    Text = confirmedOcr.Text,
                    Source = confirmedOcr.Source,
                    AnalyzedPages = confirmedOcr.AnalyzedPages
                }
            ],
            maximumResults: 10,
            progress: null,
            CancellationToken.None)
        .Single();
    Assert(
        ocrContentCandidate.Reason.Contains(
            "이미지 OCR",
            StringComparison.Ordinal),
        "OCR 일치 근거 표시");

    var weakCoverageCandidates = ContentSearchService.FindCandidates(
        SearchQueryInterpreter.Interpret(
            "개인정보 보호망 접속 자격 자료를 찾아줘"),
        [
            new ContentDocumentRecord
            {
                Name = Path.GetFileName(semanticOnlyFile),
                FullPath = semanticOnlyFile,
                DirectoryPath = sourceFolder.FullName,
                Extension = ".txt",
                SizeBytes = new FileInfo(semanticOnlyFile).Length,
                ModifiedUtc = DateTime.UtcNow,
                Text = "WireGuard tunnel account token and privacy relay configuration.",
                Source = DocumentContentSource.PlainText
            }
        ],
        maximumResults: 10,
        progress: null,
        CancellationToken.None);
    Assert(
        weakCoverageCandidates.Count == 0,
        "여러 검색어 중 한 개만 맞는 본문 오탐 제거");

    var visualFallback = await new TargetedFileSearchService().FindAsync(
        contentRoot,
        SearchQueryInterpreter.Interpret("강아지를 찾아줘"),
        maximumResults: 100,
        includeVisualTypeFallback: true,
        progress: null,
        CancellationToken.None);
    Assert(
        visualFallback.Records.Any(record =>
            record.FullPath == ocrImageFile),
        "잘린 메타데이터 밖 이미지도 시각 AI 분석 대상으로 수집");

    using (var fakeVisual = new FakeVisualEmbeddingService())
    using (var fakeTextForVisual = new FakeEmbeddingService())
    using (var fakeImageTagger = new FakeImageTaggingService())
    {
        var visualSearch = new MetadataSearchService(
            fileSystem,
            Path.Combine(temporaryRoot, "_visual-metadata-index"),
            Path.Combine(temporaryRoot, "_visual-content-index"),
            Path.Combine(temporaryRoot, "_visual-semantic-index"),
            fakeTextForVisual,
            Path.Combine(temporaryRoot, "_visual-vector-index"),
            fakeVisual,
            fakeImageTagger);
        var visualWarmup = await visualSearch.WarmUpAsync(
            [contentRoot],
            progress: null,
            CancellationToken.None,
            maximumMetadataItemsPerRoot: 100,
            maximumContentDocumentsPerRoot: 100,
            maximumNewSemanticDocumentsPerRoot: 100,
            maximumNewVisualDocumentsPerRoot: 100);
        Assert(
            visualWarmup.VisualDocuments > 0,
            "검색 전에 시각 색인 준비");
        var visualFileCallsAfterWarmup = fakeVisual.FileEmbeddingCalls;
        var visualResponse = await visualSearch.SearchAsync(
            new SearchRequest(
                "노을 사진을 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            visualResponse.Diagnostics.UsedVisualSearch,
            "SigLIP 2 시각 의미 검색 실행");
        Assert(
            fakeVisual.FileEmbeddingCalls == visualFileCallsAfterWarmup,
            "검색 중 새 이미지 색인 금지");
        Assert(
            visualResponse.Results.Any(
                result => result.FullPath == ocrImageFile),
            "파일명과 무관한 이미지 픽셀 의미 검색");
        var visualResult = visualResponse.Results.First(
            result => result.FullPath == ocrImageFile);
        Assert(
            visualResult.WasVisualAnalyzed &&
            visualResult.MatchDisplay.StartsWith(
                "시각 후보",
                StringComparison.Ordinal),
            "시각 후보 배지 표시");
        Assert(
            visualResult.Reason.Contains(
                "시각 AI",
                StringComparison.Ordinal),
            "이미지 픽셀 분석 근거 표시");

        var characterProfile = VisualQueryPromptBuilder.Analyze(
            SearchQueryInterpreter.Interpret(
                "라피 캐릭터 이미지를 찾아줘"));
        Assert(
            characterProfile.Kind == VisualQueryKind.Character &&
            characterProfile.SuppressUserInterface &&
            characterProfile.IsNamedSubject,
            "캐릭터 고유명 이미지 질의의 엄격 모드 판별");
        Assert(
            VisualQueryPromptBuilder.Build(
                    "라피 캐릭터 이미지를 찾아줘",
                    characterProfile)
                .Contains(
                    "rapi",
                    StringComparison.OrdinalIgnoreCase),
            "한글 캐릭터 고유명 영문 음역을 시각 문구에 추가");
        var characterPrompts =
            VisualQueryPromptBuilder.BuildVariants(
                "라피 캐릭터 이미지를 찾아줘",
                characterProfile);
        Assert(
            characterPrompts.Count >= 4 &&
            characterPrompts.All(prompt =>
                prompt.Contains(
                    "rapi",
                    StringComparison.OrdinalIgnoreCase)),
            "캐릭터 고유명을 여러 시각 문구로 비교");
        var asunaIntent = SearchQueryInterpreter.Interpret(
            "아스나 이미지를 찾아줘");
        Assert(
            VisualQueryPromptBuilder.Build(
                    asunaIntent.OriginalQuery,
                    VisualQueryPromptBuilder.Analyze(asunaIntent))
                .Contains(
                    "asuna",
                    StringComparison.OrdinalIgnoreCase),
            "외래어 캐릭터 이름의 간소화 음역 추가");
        var userInterfaceProfile = VisualQueryPromptBuilder.Analyze(
            SearchQueryInterpreter.Interpret(
                "프로그램 UI 스크린샷을 찾아줘"));
        Assert(
            userInterfaceProfile.Kind ==
            VisualQueryKind.UserInterface &&
            !userInterfaceProfile.SuppressUserInterface,
            "명시적인 UI 이미지 질의는 UI 감점 제외");
        Assert(
            VisualQueryPromptBuilder.Analyze(
                SearchQueryInterpreter.Interpret(
                    "Rui character image"))
                .Kind == VisualQueryKind.Character,
            "고유명 내부 ui 철자를 UI 요청으로 오인하지 않음");
        var officeMaterialProfile = VisualQueryPromptBuilder.Analyze(
            SearchQueryInterpreter.Interpret(
                "표와 차트가 있는 사무 자료 이미지를 찾아줘"));
        Assert(
            officeMaterialProfile.Kind == VisualQueryKind.OfficeMaterial &&
            VisualQueryPromptBuilder.BuildVariants(
                    "표와 차트가 있는 사무 자료 이미지를 찾아줘",
                    officeMaterialProfile)
                .Any(prompt => prompt.Contains(
                    "office",
                    StringComparison.OrdinalIgnoreCase)),
            "사무 자료 이미지를 전용 다중 시각 문구로 분석");
        Assert(
            !VisualQueryPromptBuilder.Analyze(
                    SearchQueryInterpreter.Interpret(
                        "누드 이미지를 찾아줘"))
                .IsNamedSubject,
            "성인 시각 개념을 캐릭터 고유명으로 오인하지 않음");

        var characterResponse = await visualSearch.SearchAsync(
            new SearchRequest(
                "라피 캐릭터 이미지를 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            characterResponse.Results.Any(result =>
                result.FullPath == namedCharacterImageFile) &&
            characterResponse.Results.All(result =>
                result.FullPath != characterImageFile),
            "캐릭터 이름이 확인된 이미지만 시각 검색에 포함");
        var numericCharacterResponse = await visualSearch.SearchAsync(
            new SearchRequest(
                "란마 이미지를 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            numericCharacterResponse.Results.Any(result =>
                result.FullPath == numericCharacterImageFile &&
                result.Reason.Contains(
                    "캐릭터 태거",
                    StringComparison.Ordinal)),
            "숫자 파일명의 캐릭터를 픽셀 태그로 검색");
        Assert(
            characterResponse.Results.FirstOrDefault()?.FullPath ==
            namedCharacterImageFile,
            "고유명 파일명 단서와 시각 분석이 함께 있는 이미지를 최우선");
        Assert(
            characterResponse.Results
                .First(result =>
                    result.FullPath == namedCharacterImageFile)
                .Reason.Contains(
                    "파일명·폴더명",
                    StringComparison.Ordinal),
            "캐릭터 고유명과 이미지 분석의 결합 근거 표시");
        Assert(
            characterResponse.Results.All(result =>
                result.FullPath != userInterfaceImageFile),
            "캐릭터 검색에서 UI 스크린샷 감점·제외");
        Assert(
            characterResponse.Results.All(result =>
                result.FullPath != unrelatedCharacterImageFile),
            "고유명 근거가 없는 일반 캐릭터 이미지 제외");
    }

    var weakVisualRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "weak-visual-confidence"));
    var weakVisualFile = Path.Combine(
        weakVisualRoot.FullName,
        "weak_visual_frame.png");
    await File.WriteAllBytesAsync(weakVisualFile, [0x01, 0x02, 0x03]);
    using (var weakFakeVisual = new FakeVisualEmbeddingService())
    using (var weakFakeText = new FakeEmbeddingService())
    {
        var weakVisualSearch = new MetadataSearchService(
            fileSystem,
            Path.Combine(temporaryRoot, "_weak-visual-metadata"),
            Path.Combine(temporaryRoot, "_weak-visual-content"),
            Path.Combine(temporaryRoot, "_weak-visual-semantic"),
            weakFakeText,
            Path.Combine(temporaryRoot, "_weak-visual-vector"),
            weakFakeVisual);
        _ = await weakVisualSearch.WarmUpAsync(
            [weakVisualRoot.FullName],
            progress: null,
            CancellationToken.None,
            maximumMetadataItemsPerRoot: 100,
            maximumContentDocumentsPerRoot: 100,
            maximumNewSemanticDocumentsPerRoot: 100,
            maximumNewVisualDocumentsPerRoot: 100);
        var weakVisualResponse = await weakVisualSearch.SearchAsync(
            new SearchRequest(
                "노을 사진을 찾아줘",
                [weakVisualRoot.FullName]),
            progress: null,
            CancellationToken.None);
        var weakVisualResult = weakVisualResponse.Results.First(result =>
            result.FullPath == weakVisualFile);
        Assert(
            weakVisualResult.EvidenceKind ==
            SearchEvidenceKind.VisualCandidate &&
            weakVisualResult.MatchPercent <= 55d,
            "낮은 절대 시각 유사도를 1위라는 이유로 높은 퍼센트로 표시하지 않음");
    }

    var broadVisualRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "broad-visual-parent"));
    var broadDistractorFolder = Directory.CreateDirectory(
        Path.Combine(broadVisualRoot.FullName, "many-other-images"));
    var broadTargetFolder = Directory.CreateDirectory(
        Path.Combine(broadVisualRoot.FullName, "older-images"));
    var broadVisualRecords = Enumerable.Range(0, 140)
        .Select(index => new IndexedFileRecord
        {
            Name = $"generic_character_{index:000}.png",
            FullPath = Path.Combine(
                broadDistractorFolder.FullName,
                $"generic_character_{index:000}.png"),
            DirectoryPath = broadDistractorFolder.FullName,
            Extension = ".png",
            IsDirectory = false,
            SizeBytes = 4,
            ModifiedUtc = DateTime.UtcNow.AddMinutes(-index)
        })
        .ToList();
    var broadVisualTarget = new IndexedFileRecord
    {
        Name = "hidden_subject_frame.png",
        FullPath = Path.Combine(
            broadTargetFolder.FullName,
            "hidden_subject_frame.png"),
        DirectoryPath = broadTargetFolder.FullName,
        Extension = ".png",
        IsDirectory = false,
        SizeBytes = 4,
        ModifiedUtc = DateTime.UtcNow.AddYears(-2)
    };
    broadVisualRecords.Add(broadVisualTarget);
    using (var broadFakeVisual = new FakeVisualEmbeddingService())
    {
        var broadVisualIndex = new VisualIndexService(
            Path.Combine(temporaryRoot, "_broad-visual-index"),
            broadFakeVisual);
        var broadVisualResult =
            await broadVisualIndex.FindCandidatesAsync(
                broadVisualRoot.FullName,
                SearchQueryInterpreter.Interpret(
                    "라피 캐릭터 이미지를 찾아줘"),
                broadVisualRecords,
                maximumResults: 250,
                maximumNewDocuments: 96,
                progress: null,
                CancellationToken.None);
        Assert(
            broadVisualResult.Candidates.All(candidate =>
                !string.Equals(
                    candidate.Document.FullPath,
                    broadVisualTarget.FullPath,
                    StringComparison.OrdinalIgnoreCase)),
            "캐릭터 신원 태그·파일명·폴더명 근거가 없는 시각 유사 후보 제외");
    }

    var volumeVisualRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "visual-result-volume"));
    var volumeVisualRecords = Enumerable.Range(0, 180)
        .Select(index => new IndexedFileRecord
        {
            Name = $"scene_{index:000}.png",
            FullPath = Path.Combine(
                volumeVisualRoot.FullName,
                $"scene_{index:000}.png"),
            DirectoryPath = volumeVisualRoot.FullName,
            Extension = ".png",
            IsDirectory = false,
            SizeBytes = 4,
            ModifiedUtc = DateTime.UtcNow.AddMinutes(-index)
        })
        .ToArray();
    using (var volumeFakeVisual = new FakeVisualEmbeddingService())
    {
        var volumeVisualIndex = new VisualIndexService(
            Path.Combine(temporaryRoot, "_volume-visual-index"),
            volumeFakeVisual);
        _ = await volumeVisualIndex.WarmUpAsync(
            volumeVisualRoot.FullName,
            volumeVisualRecords,
            maximumNewDocuments: 96,
            progress: null,
            CancellationToken.None);
        _ = await volumeVisualIndex.WarmUpAsync(
            volumeVisualRoot.FullName,
            volumeVisualRecords,
            maximumNewDocuments: 96,
            progress: null,
            CancellationToken.None);
        var volumeVisualResult =
            await volumeVisualIndex.FindCandidatesAsync(
                volumeVisualRoot.FullName,
                SearchQueryInterpreter.Interpret(
                    "노을 사진을 찾아줘"),
                volumeVisualRecords,
                maximumResults: 250,
                maximumNewDocuments: 96,
                progress: null,
                CancellationToken.None);
        Assert(
            volumeVisualResult.Candidates.Count > 100,
            "시각 검색 결과 100개 초과 반환");
    }

    var mullvadRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "mullvad-ranking"));
    var simulatedDesktop = Directory.CreateDirectory(
        Path.Combine(
            mullvadRoot.FullName,
            "Users",
            "Commander",
            "Desktop"));
    var desiredAccountFile = Path.Combine(
        simulatedDesktop.FullName,
        "mullvad_account_recovery.dat");
    await File.WriteAllBytesAsync(
        desiredAccountFile,
        [0x31, 0x37, 0x34, 0x39, 0x32, 0x30]);
    var noisyMullvadResources = Directory.CreateDirectory(
        Path.Combine(
            mullvadRoot.FullName,
            "Program Files",
            "Mullvad VPN",
            "resources"));
    for (var index = 0; index < 320; index++)
    {
        await File.WriteAllTextAsync(
            Path.Combine(
                noisyMullvadResources.FullName,
                $"CHANGELOG_{index:000}.md"),
            "Mullvad VPN login documentation and release notes.");
    }

    var loginIntent = SearchQueryInterpreter.Interpret(
        "mullvad 로그인 관련 파일을 찾아줘");
    Assert(
        loginIntent.Terms
            .First(term =>
                term.Original.Equals(
                    "로그인",
                    StringComparison.OrdinalIgnoreCase))
            .Alternatives
            .Contains("account", StringComparer.OrdinalIgnoreCase),
        "로그인과 account 의미 확장");

    var mullvadAccountResponse = await search.SearchAsync(
        new SearchRequest(
            "mullvad 로그인 관련 파일을 찾아줘",
            [mullvadRoot.FullName]),
        progress: null,
        CancellationToken.None);
    Assert(
        mullvadAccountResponse.Results.FirstOrDefault()?.FullPath ==
        desiredAccountFile,
        "본문 일치 결과보다 mullvad_account 파일명 우선");
    Assert(
        mullvadAccountResponse.Results
            .First(result => result.FullPath == desiredAccountFile)
            .Reason
            .Contains("파일명", StringComparison.Ordinal),
        "mullvad_account 파일명 일치 근거 표시");

    var truncatedAccountSearch = new MetadataSearchService(
        fileSystem,
        Path.Combine(temporaryRoot, "_truncated-account-metadata"),
        Path.Combine(temporaryRoot, "_truncated-account-content"));
    var recoveredAccountResponse = await truncatedAccountSearch.SearchAsync(
        new SearchRequest(
            "mullvad 로그인 관련 파일을 찾아줘",
            [mullvadRoot.FullName],
            MaximumResults: 100,
            MaximumScannedItems: 1),
        progress: null,
        CancellationToken.None);
    Assert(
        recoveredAccountResponse.Diagnostics.UsedTargetedScan,
        "일반 문장 검색의 잘린 메타데이터 색인 보완");
    Assert(
        recoveredAccountResponse.Results.FirstOrDefault()?.FullPath ==
        desiredAccountFile,
        "색인 상한 밖 mullvad_account 파일 정밀 재탐색");

    var searchNowRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "search-now-unindexed"));
    var searchNowTarget = Path.Combine(
        searchNowRoot.FullName,
        "원천징수_즉시검색.txt");
    await File.WriteAllTextAsync(searchNowTarget, "원천징수 자료");
    var searchNowService = new MetadataSearchService(
        fileSystem,
        Path.Combine(temporaryRoot, "_search-now-metadata"),
        Path.Combine(temporaryRoot, "_search-now-content"));
    var readiness = await searchNowService.GetIndexReadinessAsync(
        "원천징수 파일을 찾아줘",
        [searchNowRoot.FullName],
        maximumScannedItems: 100,
        maximumContentDocuments: 100,
        CancellationToken.None);
    Assert(readiness.RequiresIndexing, "미색인 위치 사전 감지");
    var searchNowResponse = await searchNowService.SearchAsync(
        new SearchRequest(
            "원천징수 파일을 찾아줘",
            [searchNowRoot.FullName],
            MaximumScannedItems: 100,
            IndexingMode: SearchIndexingMode.ExistingIndexOnly),
        progress: null,
        CancellationToken.None);
    Assert(
        searchNowResponse.Diagnostics.UsedExistingIndexOnly &&
        searchNowResponse.Diagnostics.UsedTargetedScan &&
        searchNowResponse.Results.FirstOrDefault()?.FullPath ==
        searchNowTarget,
        "일단 검색은 색인 없이 파일명·경로 직접 확인");

    var progressiveEmptyPass = await searchNowService.SearchAsync(
        new SearchRequest(
            "원천징수 파일을 찾아줘",
            [searchNowRoot.FullName],
            MaximumScannedItems: 100,
            IndexingMode: SearchIndexingMode.ExistingIndexOnly,
            AllowTargetedScan: false,
            IncludeAiCandidates: false),
        progress: null,
        CancellationToken.None);
    Assert(
        !progressiveEmptyPass.Diagnostics.UsedTargetedScan &&
        progressiveEmptyPass.Results.Count == 0,
        "점진 검색은 준비된 결과만 즉시 반환");

    _ = await searchNowService.WarmUpAsync(
        [searchNowRoot.FullName],
        progress: null,
        CancellationToken.None,
        maximumMetadataItemsPerRoot: 100,
        maximumContentDocumentsPerRoot: 100,
        maximumNewSemanticDocumentsPerRoot: 0,
        maximumNewVisualDocumentsPerRoot: 0);
    var progressiveIndexedPass = await searchNowService.SearchAsync(
        new SearchRequest(
            "원천징수 파일을 찾아줘",
            [searchNowRoot.FullName],
            MaximumScannedItems: 100,
            IndexingMode: SearchIndexingMode.ExistingIndexOnly,
            AllowTargetedScan: false,
            IncludeAiCandidates: false),
        progress: null,
        CancellationToken.None);
    Assert(
        !progressiveIndexedPass.Diagnostics.UsedTargetedScan &&
        progressiveIndexedPass.Results.FirstOrDefault()?.FullPath ==
        searchNowTarget,
        "점진 색인 완료 단계에서 결과 자동 보강");

    using (var fakeEmbedding = new FakeEmbeddingService())
    {
        var semanticSearch = new MetadataSearchService(
            fileSystem,
            Path.Combine(temporaryRoot, "_semantic-metadata-index"),
            Path.Combine(temporaryRoot, "_semantic-content-index"),
            Path.Combine(temporaryRoot, "_semantic-vector-index"),
            fakeEmbedding);
        var warmup = await semanticSearch.WarmUpAsync(
            [contentRoot],
            progress: null,
            CancellationToken.None,
            maximumMetadataItemsPerRoot: 100,
            maximumContentDocumentsPerRoot: 100,
            maximumNewSemanticDocumentsPerRoot: 100);
        Assert(warmup.Roots == 1, "시작 자동 색인 위치");
        Assert(warmup.IndexedItems > 0, "시작 메타데이터 자동 색인");
        Assert(warmup.ContentDocuments > 0, "시작 본문 자동 색인");
        Assert(warmup.SemanticDocuments > 0, "시작 AI 의미 자동 색인");

        var semanticResponse = await semanticSearch.SearchAsync(
            new SearchRequest(
                "개인정보 보호망 접속 자격 자료를 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            semanticResponse.Diagnostics.UsedSemanticSearch,
            "로컬 AI 의미 검색 실행");
        Assert(
            semanticResponse.Diagnostics.AiModelReady,
            "로컬 AI 모델 준비 상태");
        Assert(
            semanticResponse.Results.Any(
                result => result.FullPath == semanticOnlyFile),
            "표면 단어가 다른 문서의 의미 검색");
        var semanticOnlyResult = semanticResponse.Results
            .First(result => result.FullPath == semanticOnlyFile);
        Assert(
            semanticOnlyResult.Reason.Contains(
                "로컬 AI",
                StringComparison.Ordinal),
            "AI 의미 검색 근거 표시",
            $"실제 근거: {semanticOnlyResult.Reason}");
        Assert(
            semanticOnlyResult.WasAiAnalyzed,
            "AI 분석 결과 표시");
        Assert(
            semanticOnlyResult.EvidenceKind ==
            SearchEvidenceKind.SemanticCandidate &&
            !HasDirectContentEvidenceReason(
                semanticOnlyResult.Reason),
            "낮은 본문 단어 커버리지는 AI 후보를 덮어쓰지 않음",
            $"실제 근거 종류: {semanticOnlyResult.EvidenceKind}" +
            Environment.NewLine +
            $"실제 근거: {semanticOnlyResult.Reason}");
        Assert(
            semanticOnlyResult.MatchDisplay.StartsWith(
                "AI 후보",
                StringComparison.Ordinal),
            "AI 후보 배지 표시",
            $"실제 배지: {semanticOnlyResult.MatchDisplay}");

        var semanticAccountResponse = await semanticSearch.SearchAsync(
            new SearchRequest(
                "mullvad 로그인 관련 파일을 찾아줘",
                [mullvadRoot.FullName]),
            progress: null,
            CancellationToken.None);
        Assert(
            semanticAccountResponse.Results.FirstOrDefault()?.FullPath ==
            desiredAccountFile,
            "AI 혼합 검색에서도 mullvad_account 파일 우선");
        var semanticAccountResult = semanticAccountResponse.Results
            .First(result => result.FullPath == desiredAccountFile);
        Assert(
            semanticAccountResult.Reason.Contains(
                "파일명",
                StringComparison.Ordinal) &&
            semanticAccountResult.MatchDisplay.Equals(
                "정확 일치",
                StringComparison.Ordinal),
            "본문 추출 불가 파일은 파일명 정확 일치를 우선",
            $"실제 근거: {semanticAccountResult.Reason} · " +
            $"배지: {semanticAccountResult.MatchDisplay}");

        var withholdingResponse = await semanticSearch.SearchAsync(
            new SearchRequest(
                "원천징수 관련 파일을 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            withholdingResponse.Results.FirstOrDefault()?.FullPath ==
            withholdingFile,
            "단일 업무 용어는 파일명 정확 일치를 최우선");
        Assert(
            withholdingResponse.Diagnostics.UsedSemanticSearch,
            "단일 업무 용어도 E5 의미 검색을 보조 근거로 사용");
        Assert(
            withholdingResponse.Results.All(result =>
                result.FullPath != semanticOnlyFile),
            "정확 일치가 있으면 단일 용어의 독립 E5 오탐을 억제");

        var singleTermSemanticResponse = await semanticSearch.SearchAsync(
            new SearchRequest(
                "보호망 파일을 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            singleTermSemanticResponse.Diagnostics.UsedSemanticSearch &&
            singleTermSemanticResponse.Results.Any(result =>
                result.FullPath == semanticOnlyFile),
            "정확 단서가 없는 단일 용어는 E5 의미 후보로 복구");

        var metadataSemanticResponse = await semanticSearch.SearchAsync(
            new SearchRequest(
                "보호망 3d 모델을 찾아줘",
                [contentRoot]),
            progress: null,
            CancellationToken.None);
        Assert(
            metadataSemanticResponse.Results.Any(result =>
                result.FullPath == semanticModelFile &&
                result.WasAiAnalyzed),
            "본문이 없는 사용자 파일도 파일명·경로 E5 의미 색인으로 검색");

        var advancedAnalysis = new AdvancedAnalysisService(fakeEmbedding);
        var advancedResponse = await advancedAnalysis.AnalyzeAsync(
            "개인정보 보호망 접속 자격 자료",
            [
                new SearchResult
                {
                    Name = Path.GetFileName(hiddenContentFile),
                    FullPath = hiddenContentFile,
                    DirectoryPath =
                        Path.GetDirectoryName(hiddenContentFile)!,
                    TypeDisplay = "텍스트 문서",
                    ModifiedDisplay = "오늘",
                    Reason = "기본 검색 후보",
                    IconGlyph = string.Empty,
                    Score = 100d,
                    MatchPercent = 90d
                },
                new SearchResult
                {
                    Name = Path.GetFileName(semanticOnlyFile),
                    FullPath = semanticOnlyFile,
                    DirectoryPath =
                        Path.GetDirectoryName(semanticOnlyFile)!,
                    TypeDisplay = "텍스트 문서",
                    ModifiedDisplay = "오늘",
                    Reason = "기본 검색 후보",
                    IconGlyph = string.Empty,
                    Score = 40d,
                    MatchPercent = 55d
                }
            ],
            progress: null,
            CancellationToken.None);
        Assert(
            advancedResponse.Results.First().FullPath == semanticOnlyFile,
            "768차원 정밀 분석 결과 재정렬");
        Assert(
            advancedResponse.Results.First().WasAdvancedAnalyzed &&
            advancedResponse.Results.First().MatchDisplay.StartsWith(
                "정밀 AI",
                StringComparison.Ordinal),
            "정밀 AI 일치도 배지 표시");
        Assert(
            fakeEmbedding.LastResolution == EmbeddingResolution.Full,
            "정밀 재평가 전체 임베딩 차원 요청");
    }

    var truncatedRoot = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "truncated-content"));
    var modelFolderBeyondLimit = Directory.CreateDirectory(
        Path.Combine(truncatedRoot.FullName, "models-beyond-index-limit"));
    var modelBeyondLimit = Path.Combine(
        modelFolderBeyondLimit.FullName,
        "색인_상한_뒤의_모델.stl");
    await File.WriteAllTextAsync(modelBeyondLimit, "solid beyond-limit");
    var truncatedSearch = new MetadataSearchService(
        fileSystem,
        Path.Combine(temporaryRoot, "_truncated-test-index"));
    var recoveredModelResponse = await truncatedSearch.SearchAsync(
        new SearchRequest(
            "stl 파일을 찾아줘",
            [truncatedRoot.FullName],
            MaximumResults: 100,
            MaximumScannedItems: 1),
        progress: null,
        CancellationToken.None);
    Assert(
        recoveredModelResponse.Diagnostics.IndexWasTruncated,
        "색인 상한 감지");
    Assert(
        recoveredModelResponse.Diagnostics.UsedTargetedScan,
        "파일 형식 정밀 탐색 실행");
    Assert(
        recoveredModelResponse.Results.Any(
            result => result.FullPath == modelBeyondLimit),
        "색인 상한 이후 STL 파일 복구");

    var operations = new FileOperationService();
    await operations.CopyOrMoveAsync(
        [sourceFile],
        destinationFolder.FullName,
        move: false,
        progress: null,
        CancellationToken.None);
    var copiedFile = Path.Combine(destinationFolder.FullName, Path.GetFileName(sourceFile));
    Assert(File.Exists(copiedFile), "파일 복사");

    var renamedFile = operations.Rename(copiedFile, "무선 점검 결과.txt");
    Assert(File.Exists(renamedFile), "파일 이름 변경");
    Assert(File.Exists(sourceFile), "복사 후 원본 보존");

    var migrationSource = Directory.CreateDirectory(
        Path.Combine(temporaryRoot, "storage-source"));
    var migrationNested = Directory.CreateDirectory(
        Path.Combine(migrationSource.FullName, "models", "semantic"));
    var migrationModel = Path.Combine(
        migrationNested.FullName,
        "test-model.gguf");
    await File.WriteAllBytesAsync(
        migrationModel,
        Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 251))
            .ToArray());
    await File.WriteAllTextAsync(
        Path.Combine(migrationSource.FullName, "settings.json"),
        "{\"SearchPanelVisible\":true}");
    var migrationTarget = Path.Combine(
        temporaryRoot,
        "storage-target");
    var storageOverride = Path.Combine(
        temporaryRoot,
        "bootstrap",
        "storage-location.txt");
    var migrationSettings = new SettingsService(
        migrationSource.FullName,
        storageOverride);
    Assert(
        migrationSettings.GetDataDirectorySize() >= 4096,
        "현재 저장 위치 사용량 계산");
    var migrationResult =
        await migrationSettings.ChangeDataDirectoryAsync(
            migrationTarget,
            progress: null,
            CancellationToken.None);
    Assert(migrationResult.LocationChanged, "저장 위치 변경 기록");
    Assert(
        File.Exists(
            Path.Combine(
                migrationTarget,
                "models",
                "semantic",
                "test-model.gguf")),
        "모델과 색인 폴더 복사");
    Assert(
        File.Exists(
            Path.Combine(
                migrationSource.FullName,
                "models",
                "semantic",
                "test-model.gguf")),
        "저장 위치 변경 후 원본 보존");
    var relocatedSettings = new SettingsService(
        dataDirectory: null,
        storageOverridePath: storageOverride);
    Assert(
        string.Equals(
            Path.GetFullPath(relocatedSettings.DataDirectory),
            Path.GetFullPath(migrationTarget),
            StringComparison.OrdinalIgnoreCase),
        "다음 실행에서 지정 저장 위치 적용");

    AppLog.Initialize(temporaryRoot);
    AppLog.Info("Smoke test log entry.");
    AppLog.Shutdown();
    Assert(
        Directory.EnumerateFiles(Path.Combine(temporaryRoot, "logs"), "*.log").Any(),
        "실행 로그 생성");

    if (OperatingSystem.IsWindows())
    {
        var commandProcessor =
            Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var childStartInfo = new ProcessStartInfo
        {
            FileName = commandProcessor,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        childStartInfo.ArgumentList.Add("/d");
        childStartInfo.ArgumentList.Add("/c");
        childStartInfo.ArgumentList.Add(
            "ping -n 30 127.0.0.1 >nul");
        var childProcess = Process.Start(childStartInfo) ??
                           throw new InvalidOperationException(
                               "프로세스 정리 테스트를 시작하지 못했습니다.");
        var childProcessId = childProcess.Id;
        using (var launchedProcesses = new LaunchedProcessTracker())
        {
            launchedProcesses.Track(
                childProcess,
                Path.Combine(temporaryRoot, "viewer-test.png"));
            launchedProcesses.TerminateAll();
        }

        Assert(
            !IsProcessRunning(childProcessId),
            "앱이 연 외부 프로세스 종료");
    }

    var indexedSkyrimFolder = Directory.CreateDirectory(
        Path.Combine(contentRoot, "스카이림 모드"));
    var instantTitleSearch = new InstantTitleSearchService(
        new MetadataIndexService(
            Path.Combine(temporaryRoot, "instant-title-index")));
    var instantTitleIndexProgress =
        new List<InstantTitleIndexProgress>();
    await instantTitleSearch.WarmIndexesAsync(
        [contentRoot],
        progress: new CollectingProgress<InstantTitleIndexProgress>(
            instantTitleIndexProgress),
        CancellationToken.None);
    Assert(
        instantTitleIndexProgress.Count >= 2 &&
        instantTitleIndexProgress.Any(state =>
            !string.IsNullOrWhiteSpace(state.CurrentPath)) &&
        instantTitleIndexProgress[^1].IsCompleted &&
        instantTitleIndexProgress[^1].PercentComplete == 100d,
        "제목 색인의 현재 경로·항목 수·완료 진행률 표시");
    var instantDefaultOptions = new InstantTitleSearchOptions(
        MatchCase: false,
        MatchWholeWord: false,
        UseRegularExpression: false,
        ItemFilter: InstantTitleItemFilter.All,
        SortField: InstantTitleSortField.Name,
        SortAscending: true);
    var instantTitleResponse = await instantTitleSearch.SearchAsync(
        "배관",
        [contentRoot],
        instantDefaultOptions,
        maximumResults: 100,
        CancellationToken.None);
    Assert(
        instantTitleResponse.Results.Count(result =>
            result.Name.Contains("배관", StringComparison.Ordinal)) == 2,
        "제목 즉시 검색 부분 문자열 일치");
    var instantSingleCharacterResponse =
        await instantTitleSearch.SearchAsync(
            "배",
            [contentRoot],
            instantDefaultOptions,
            maximumResults: 100,
            CancellationToken.None);
    Assert(
        instantSingleCharacterResponse.Results.Any(result =>
            result.Name.Contains("배", StringComparison.Ordinal)),
        "제목 검색은 한 글자 입력부터 즉시 결과 반환");
    var indexedNaturalTitleEvents = new List<TitleSearchProgress>();
    var indexedNaturalTitleSummary =
        await instantTitleSearch.SearchNaturalLanguageAsync(
            "스카이림 모드 폴더를 찾아줘",
            [contentRoot],
            maximumResults: 100,
            progress: new CollectingProgress<TitleSearchProgress>(
                indexedNaturalTitleEvents),
            CancellationToken.None);
    Assert(
        indexedNaturalTitleSummary.MatchedItems >= 1 &&
        indexedNaturalTitleEvents
            .SelectMany(state => state.NewHits)
            .Any(hit => hit.FullPath == indexedSkyrimFolder.FullName),
        "간단한 폴더 자연어 검색을 완료된 제목 색인에서 즉시 처리");
    var instantFolderResponse = await instantTitleSearch.SearchAsync(
        "지하 3층",
        [contentRoot],
        instantDefaultOptions with
        {
            ItemFilter = InstantTitleItemFilter.Folders
        },
        maximumResults: 100,
        CancellationToken.None);
    Assert(
        instantFolderResponse.Results.Count == 1 &&
        instantFolderResponse.Results[0].IsDirectory,
        "제목 즉시 검색 폴더 필터");
    var instantCaseSensitiveResponse = await instantTitleSearch.SearchAsync(
        "wireguard",
        [contentRoot],
        instantDefaultOptions with { MatchCase = true },
        maximumResults: 100,
        CancellationToken.None);
    Assert(
        instantCaseSensitiveResponse.Results.Count == 0,
        "제목 즉시 검색 대소문자 구분");
    var instantRegexResponse = await instantTitleSearch.SearchAsync(
        "^2026_원천",
        [contentRoot],
        instantDefaultOptions with { UseRegularExpression = true },
        maximumResults: 100,
        CancellationToken.None);
    Assert(
        instantRegexResponse.Results.Count == 1 &&
        string.Equals(
            instantRegexResponse.Results[0].FullPath,
            withholdingFile,
            StringComparison.OrdinalIgnoreCase),
        "제목 즉시 검색 정규식");
    var invalidRegexResponse = await instantTitleSearch.SearchAsync(
        "[",
        [contentRoot],
        instantDefaultOptions with { UseRegularExpression = true },
        maximumResults: 100,
        CancellationToken.None);
    Assert(
        !string.IsNullOrWhiteSpace(invalidRegexResponse.ValidationError),
        "제목 즉시 검색 잘못된 정규식 안내");

    var syntheticTitleRecordCount = int.TryParse(
        Environment.GetEnvironmentVariable(
            "AIEXPLORER_TITLE_BENCHMARK_COUNT"),
        out var requestedSyntheticTitleRecordCount)
        ? Math.Max(50_000, requestedSyntheticTitleRecordCount)
        : 50_000;
    var syntheticTitleRecords = Enumerable.Range(
            0,
            syntheticTitleRecordCount)
        .Select(index => new IndexedFileRecord
        {
            Name = $"document_{index:00000}.txt",
            FullPath = Path.Combine(
                @"C:\synthetic\ordinary",
                $"document_{index:00000}.txt"),
            DirectoryPath = @"C:\synthetic\ordinary",
            Extension = ".txt",
            IsDirectory = false,
            SizeBytes = index,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow
        })
        .Append(new IndexedFileRecord
        {
            Name = "값_대상.txt",
            FullPath = @"C:\synthetic\스카이림 모드\값_대상.txt",
            DirectoryPath = @"C:\synthetic\스카이림 모드",
            Extension = ".txt",
            IsDirectory = false,
            SizeBytes = 1,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow
        })
        .ToArray();
    var syntheticTitleBuildStopwatch = Stopwatch.StartNew();
    var syntheticTitleIndex = InstantTitleMemoryIndex.Create(
        syntheticTitleRecords);
    syntheticTitleBuildStopwatch.Stop();
    var syntheticTitleLookupStopwatch = Stopwatch.StartNew();
    var koreanCharacterPosting =
        syntheticTitleIndex.FindNameCandidates("값");
    var commonCharacterPosting =
        syntheticTitleIndex.FindNameCandidates("d");
    syntheticTitleLookupStopwatch.Stop();
    Assert(
        koreanCharacterPosting.Count == 1 &&
        syntheticTitleIndex[koreanCharacterPosting[0]].Name ==
        "값_대상.txt" &&
        commonCharacterPosting.Count == syntheticTitleRecordCount &&
        ReferenceEquals(
            commonCharacterPosting,
            syntheticTitleIndex.FindNameCandidates("d")),
        "완료 색인의 한 글자 검색은 전체 순회 없이 문자 포스팅을 즉시 재사용");
    if (syntheticTitleRecordCount > 50_000)
    {
        Console.WriteLine(
            $"Instant title benchmark: {syntheticTitleRecordCount:N0} items · " +
            $"build {syntheticTitleBuildStopwatch.Elapsed.TotalSeconds:0.000}s · " +
            $"one-character lookup {syntheticTitleLookupStopwatch.Elapsed.TotalMilliseconds:0.000}ms");
    }
    var pathContextCandidates =
        syntheticTitleIndex.FindContextCandidates(["스카이림"]);
    Assert(
        pathContextCandidates.Count == 1 &&
        syntheticTitleIndex[pathContextCandidates[0]].FullPath.Contains(
            "스카이림 모드",
            StringComparison.Ordinal),
        "지능 검색의 빠른 이름·경로 결과도 메모리 색인 사용");

    var packagedDataRoot = Environment.GetEnvironmentVariable(
        "AIEXPLORER_TEST_LANGUAGE_DATA");
    if (!string.IsNullOrWhiteSpace(packagedDataRoot))
    {
        using var packagedModelManager = new AiModelManager(
            Path.Combine(
                packagedDataRoot,
                "models",
                "semantic"));
        using var packagedLanguageService =
            new NaturalLanguageSearchService(
                packagedModelManager);
        Assert(
            packagedLanguageService.IsAvailable,
            "릴리스 Qwen3 자연어 모델과 llama.cpp 실행기 발견");
        var liveLanguageInterpretation =
            await packagedLanguageService.InterpretAsync(
                "AWS SSH 키를 찾고 최근에 만들어진 파일을 위로 올려줘",
                context: null,
                CancellationToken.None);
        Assert(
            liveLanguageInterpretation.Plan.UsedLanguageModel &&
            liveLanguageInterpretation.Intent.FilesOnly &&
            liveLanguageInterpretation.Intent.Terms.Any(term =>
                term.Alternatives.Contains(
                    "ppk",
                    StringComparer.OrdinalIgnoreCase)) &&
            liveLanguageInterpretation.Intent.RankingProfile.Directives.Any(
                directive =>
                    directive.Feature ==
                    SearchRankingFeature.CreatedRecency),
            "실제 로컬 Qwen3가 AWS SSH 키와 생성일 우선 검색 계획 생성",
            liveLanguageInterpretation.DisplaySummary);
    }

    Console.WriteLine("AIExplorer smoke tests passed.");
    return 0;
}
finally
{
    if (Directory.Exists(temporaryRoot))
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
}

static void Assert(
    bool condition,
    string scenario,
    string? details = null)
{
    if (!condition)
    {
        var suffix = string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : Environment.NewLine + details;
        throw new InvalidOperationException(
            $"Smoke test failed: {scenario}{suffix}");
    }
}

static SearchResult CreateSortResult(
    string name,
    string fullPath,
    DateTime modifiedUtc,
    double score) =>
    new()
    {
        Name = name,
        FullPath = fullPath,
        DirectoryPath = Path.GetDirectoryName(fullPath) ?? fullPath,
        TypeDisplay = "텍스트 문서",
        ModifiedDisplay = modifiedUtc.ToLocalTime().ToString(
            "yyyy-MM-dd HH:mm"),
        ModifiedUtc = modifiedUtc,
        Reason = "정렬 회귀 테스트",
        IconGlyph = string.Empty,
        Score = score,
        MatchPercent = 90d,
        EvidenceKind = SearchEvidenceKind.NameCandidate,
        IsDirectory = false
    };

static int FindResultRank<T>(
    IReadOnlyList<T> results,
    Func<T, bool> predicate)
{
    for (var index = 0; index < results.Count; index++)
    {
        if (predicate(results[index]))
        {
            return index;
        }
    }

    return int.MaxValue;
}

static string FormatResultRank(int zeroBasedRank)
{
    return zeroBasedRank == int.MaxValue
        ? "결과 없음"
        : $"{zeroBasedRank + 1}위";
}

static bool HasDirectContentEvidenceReason(string reason)
{
    return reason.Contains(
               "파일 본문에서",
               StringComparison.Ordinal) ||
           reason.Contains(
               "본문 앞부분에서",
               StringComparison.Ordinal) ||
           reason.Contains(
               "엑셀 시트·셀에서",
               StringComparison.Ordinal) ||
           reason.Contains(
               "이미지 OCR에서",
               StringComparison.Ordinal) ||
           reason.Contains(
               "PDF 표본",
               StringComparison.Ordinal);
}

static bool IsProcessRunning(int processId)
{
    try
    {
        using var process = Process.GetProcessById(processId);
        return !process.HasExited;
    }
    catch (ArgumentException)
    {
        return false;
    }
}

static async Task CreateSpreadsheetFixtureAsync(
    string path,
    string sheetName,
    IReadOnlyList<IReadOnlyList<string>> rows)
{
    await using var stream = new FileStream(
        path,
        FileMode.Create,
        FileAccess.ReadWrite,
        FileShare.None);
    using var archive = new ZipArchive(
        stream,
        ZipArchiveMode.Create,
        leaveOpen: true);

    await AddArchiveTextAsync(
        archive,
        "[Content_Types].xml",
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """);
    await AddArchiveTextAsync(
        archive,
        "_rels/.rels",
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """);
    await AddArchiveTextAsync(
        archive,
        "xl/_rels/workbook.xml.rels",
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """);
    await AddArchiveTextAsync(
        archive,
        "xl/workbook.xml",
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="{EscapeXml(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """);

    var worksheet = new StringBuilder(
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
        """);
    for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
    {
        worksheet.Append($"<row r=\"{rowIndex + 1}\">");
        for (var columnIndex = 0;
             columnIndex < rows[rowIndex].Count;
             columnIndex++)
        {
            var reference =
                $"{GetSpreadsheetColumnName(columnIndex)}{rowIndex + 1}";
            worksheet.Append(
                $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>" +
                EscapeXml(rows[rowIndex][columnIndex]) +
                "</t></is></c>");
        }

        worksheet.Append("</row>");
    }

    worksheet.Append("</sheetData></worksheet>");
    await AddArchiveTextAsync(
        archive,
        "xl/worksheets/sheet1.xml",
        worksheet.ToString());
}

static async Task AddArchiveTextAsync(
    ZipArchive archive,
    string entryName,
    string content)
{
    var entry = archive.CreateEntry(
        entryName,
        CompressionLevel.Fastest);
    await using var entryStream = entry.Open();
    await using var writer = new StreamWriter(
        entryStream,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    await writer.WriteAsync(content);
}

static string EscapeXml(string value) =>
    System.Security.SecurityElement.Escape(value) ?? string.Empty;

static string GetSpreadsheetColumnName(int zeroBasedColumn)
{
    var value = zeroBasedColumn + 1;
    var builder = new StringBuilder();
    while (value > 0)
    {
        value--;
        builder.Insert(0, (char)('A' + value % 26));
        value /= 26;
    }

    return builder.ToString();
}

sealed class FakeEmbeddingService : ITextEmbeddingService
{
    public bool IsAvailable => true;

    public string ModelId => "smoke-test-embedding-v1";

    public EmbeddingResolution LastResolution { get; private set; }

    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken,
        EmbeddingResolution resolution = EmbeddingResolution.Compact)
    {
        LastResolution = resolution;
        IReadOnlyList<float[]> vectors = texts
            .Select(text =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (purpose == EmbeddingPurpose.Query ||
                    text.Contains("WireGuard", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains(
                        "mullvad_account",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { 1f, 0f, 0f };
                }

                return new[] { 0f, 1f, 0f };
            })
            .ToArray();
        return Task.FromResult(vectors);
    }

    public void Dispose()
    {
    }
}

sealed class FakeOcrTextExtractor : IOcrTextExtractor
{
    public bool IsAvailable => true;

    public bool CanExtract(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    public Task<OcrTextExtraction?> ExtractAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<OcrTextExtraction?>(
            new OcrTextExtraction(
                "영수증 계정 번호 849201",
                DocumentContentSource.ImageOcr,
                PagesAnalyzed: 1,
                WasTruncated: false));
    }
}

sealed class FakeVisualEmbeddingService : IVisualEmbeddingService
{
    public bool IsAvailable => true;

    public int FileEmbeddingCalls { get; private set; }

    public string ModelId => "smoke-test-visual-v1";

    public bool CanAnalyze(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<float[]> EmbedQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EmbedPromptAsync(
            VisualQueryPromptBuilder.Build(query),
            cancellationToken);
    }

    public Task<float[]> EmbedPromptAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(
                prompt,
                VisualQueryPromptBuilder.GenericCharacterPrompt,
                StringComparison.Ordinal))
        {
            return Task.FromResult(new[] { 0.8f, 0f, 0.6f });
        }
        if (string.Equals(
                prompt,
                VisualIndexService.AnimeDomainPrompt,
                StringComparison.Ordinal))
        {
            return Task.FromResult(new[] { 0.8f, 0f, 0.6f });
        }
        if (string.Equals(
                prompt,
                VisualIndexService.OfficeDomainPrompt,
                StringComparison.Ordinal))
        {
            return Task.FromResult(new[] { 1f, 0f, 0f });
        }

        return Task.FromResult(
            prompt.StartsWith(
                "software user interface",
                StringComparison.OrdinalIgnoreCase)
                ? new[] { 0f, 1f, 0f }
                : new[] { 1f, 0f, 0f });
    }

    public Task<float[]?> EmbedFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileEmbeddingCalls++;
        return Task.FromResult<float[]?>(
            Path.GetFileName(path).Contains(
                "ui_",
                StringComparison.OrdinalIgnoreCase)
                ? new[] { 0f, 1f, 0f }
                : Path.GetFileName(path).Contains(
                    "weak_visual",
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { 0.08f, 0.9967949f, 0f }
                : Path.GetFileName(path).Contains(
                    "generic_character",
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { 0.8f, 0f, 0.6f }
                : Path.GetFileName(path).Equals(
                    "1238475.png",
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { 0.8f, 0f, 0.6f }
                : new[] { 1f, 0f, 0f });
    }

    public void Dispose()
    {
    }
}

sealed class FakeImageTaggingService : IImageTaggingService
{
    public bool IsAvailable => true;

    public string ModelId => "smoke-test-image-tagger-v1";

    public bool CanAnalyze(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    public Task<ImageTagEvidence?> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ImageTagPrediction> predictions =
            Path.GetFileName(path).Equals(
                "1238475.png",
                StringComparison.OrdinalIgnoreCase)
                ?
                [
                    new ImageTagPrediction(
                        "saotome_ranma",
                        ImageTagCategory.Character,
                        0.93d)
                ]
                : [];
        return Task.FromResult<ImageTagEvidence?>(
            new ImageTagEvidence(predictions));
    }

    public void Dispose()
    {
    }
}

sealed class CollectingProgress<T> : IProgress<T>
{
    private readonly ICollection<T> _items;

    public CollectingProgress(ICollection<T> items)
    {
        _items = items;
    }

    public void Report(T value) => _items.Add(value);
}
