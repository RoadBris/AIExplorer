$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$SemanticIndex = Join-Path $Root "src\AIExplorer\Services\SemanticIndexService.cs"
$NetworkService = Join-Path $Root "src\AIExplorer\Services\NetworkPathService.cs"
$NetworkDialog = Join-Path $Root "src\AIExplorer\Dialogs\NetworkLocationDialog.xaml"
$NetworkDialogCode = Join-Path $Root "src\AIExplorer\Dialogs\NetworkLocationDialog.xaml.cs"
$MainWindowCode = Join-Path $Root "src\AIExplorer\MainWindow.xaml.cs"
$MainWindowXaml = Join-Path $Root "src\AIExplorer\MainWindow.xaml"
$AppXaml = Join-Path $Root "src\AIExplorer\App.xaml"
$AppCode = Join-Path $Root "src\AIExplorer\App.xaml.cs"
$FavoriteService = Join-Path $Root "src\AIExplorer\Services\FavoritePathService.cs"
$BackgroundIndexPlanner = Join-Path $Root "src\AIExplorer\Services\BackgroundIndexRootPlanner.cs"
$BackgroundIndexWorkPolicy = Join-Path $Root "src\AIExplorer\Services\BackgroundIndexWorkPolicy.cs"
$TrayIconService = Join-Path $Root "src\AIExplorer\Services\TrayIconService.cs"
$VisibilityPolicy = Join-Path $Root "src\AIExplorer\Services\SearchVisibilityPolicy.cs"
$TitleSearchService = Join-Path $Root "src\AIExplorer\Services\TitleSearchService.cs"
$RankingService = Join-Path $Root "src\AIExplorer\Services\SearchRankingService.cs"
$TextPromptDialog = Join-Path $Root "src\AIExplorer\Dialogs\TextPromptDialog.xaml"
$AppSettings = Join-Path $Root "src\AIExplorer\Models\AppSettings.cs"
$SmokeTests = Join-Path $Root "tests\AIExplorer.SmokeTests\Program.cs"
$AppProject = Join-Path $Root "src\AIExplorer\AIExplorer.csproj"
$DocumentExtractor = Join-Path $Root "src\AIExplorer\Services\DocumentTextExtractor.cs"
$ContentIndex = Join-Path $Root "src\AIExplorer\Services\ContentIndexService.cs"
$ContentSearch = Join-Path $Root "src\AIExplorer\Services\ContentSearchService.cs"
$QueryInterpreter = Join-Path $Root "src\AIExplorer\Services\SearchQueryInterpreter.cs"
$SearchPlan = Join-Path $Root "src\AIExplorer\Services\SearchPlan.cs"
$NaturalLanguageSearch = Join-Path $Root "src\AIExplorer\Services\NaturalLanguageSearchService.cs"
$TextAttributes = Join-Path $Root "src\AIExplorer\Services\SearchTextAttributes.cs"
$MetadataDescriptor = Join-Path $Root "src\AIExplorer\Services\FileMetadataDescriptor.cs"
$MetadataSearch = Join-Path $Root "src\AIExplorer\Services\MetadataSearchService.cs"
$InstantTitleSearch = Join-Path $Root "src\AIExplorer\Services\InstantTitleSearchService.cs"
$VisualIndex = Join-Path $Root "src\AIExplorer\Services\VisualIndexService.cs"
$VisualPrompt = Join-Path $Root "src\AIExplorer\Services\VisualQueryPromptBuilder.cs"
$InstantTitleMemoryIndex = Join-Path $Root "src\AIExplorer\Services\InstantTitleMemoryIndex.cs"
$BulkObservableCollection = Join-Path $Root "src\AIExplorer\Models\BulkObservableCollection.cs"
$SearchResultSort = Join-Path $Root "src\AIExplorer\Services\SearchResultSortService.cs"

$RequiredFiles = @(
    $SemanticIndex,
    $NetworkService,
    $NetworkDialog,
    $NetworkDialogCode,
    $MainWindowCode,
    $MainWindowXaml,
    $AppXaml,
    $AppCode,
    $FavoriteService,
    $BackgroundIndexPlanner,
    $BackgroundIndexWorkPolicy,
    $TrayIconService,
    $VisibilityPolicy,
    $TitleSearchService,
    $RankingService,
    $TextPromptDialog,
    $AppSettings,
    $SmokeTests,
    $AppProject,
    $DocumentExtractor,
    $ContentIndex,
    $ContentSearch,
    $QueryInterpreter,
    $SearchPlan,
    $NaturalLanguageSearch,
    $TextAttributes,
    $MetadataDescriptor,
    $MetadataSearch,
    $InstantTitleSearch,
    $VisualIndex,
    $VisualPrompt,
    $InstantTitleMemoryIndex,
    $BulkObservableCollection
    $SearchResultSort
)
foreach ($Path in $RequiredFiles) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required source file was not found: $Path"
    }
}

$SemanticText = [System.IO.File]::ReadAllText($SemanticIndex)
$NetworkText = [System.IO.File]::ReadAllText($NetworkService)
$DialogText = [System.IO.File]::ReadAllText($NetworkDialog)
$DialogCodeText = [System.IO.File]::ReadAllText($NetworkDialogCode)
$MainWindowText = [System.IO.File]::ReadAllText($MainWindowCode)
$MainWindowXamlText = [System.IO.File]::ReadAllText($MainWindowXaml)
$AppXamlText = [System.IO.File]::ReadAllText($AppXaml)
$AppCodeText = [System.IO.File]::ReadAllText($AppCode)
$FavoriteText = [System.IO.File]::ReadAllText($FavoriteService)
$BackgroundIndexPlannerText = [System.IO.File]::ReadAllText($BackgroundIndexPlanner)
$BackgroundIndexWorkPolicyText = [System.IO.File]::ReadAllText($BackgroundIndexWorkPolicy)
$TrayIconText = [System.IO.File]::ReadAllText($TrayIconService)
$VisibilityText = [System.IO.File]::ReadAllText($VisibilityPolicy)
$TitleSearchText = [System.IO.File]::ReadAllText($TitleSearchService)
$RankingText = [System.IO.File]::ReadAllText($RankingService)
$TextPromptText = [System.IO.File]::ReadAllText($TextPromptDialog)
$AppSettingsText = [System.IO.File]::ReadAllText($AppSettings)
$SmokeText = [System.IO.File]::ReadAllText($SmokeTests)
$AppProjectText = [System.IO.File]::ReadAllText($AppProject)
$DocumentExtractorText = [System.IO.File]::ReadAllText($DocumentExtractor)
$ContentIndexText = [System.IO.File]::ReadAllText($ContentIndex)
$ContentSearchText = [System.IO.File]::ReadAllText($ContentSearch)
$QueryInterpreterText = [System.IO.File]::ReadAllText($QueryInterpreter)
$TextAttributesText = [System.IO.File]::ReadAllText($TextAttributes)
$MetadataDescriptorText = [System.IO.File]::ReadAllText($MetadataDescriptor)
$MetadataSearchText = [System.IO.File]::ReadAllText($MetadataSearch)
$InstantTitleSearchText = [System.IO.File]::ReadAllText($InstantTitleSearch)
$VisualIndexText = [System.IO.File]::ReadAllText($VisualIndex)
$VisualPromptText = [System.IO.File]::ReadAllText($VisualPrompt)
$InstantTitleMemoryIndexText = [System.IO.File]::ReadAllText($InstantTitleMemoryIndex)
$BulkObservableCollectionText = [System.IO.File]::ReadAllText($BulkObservableCollection)
$SearchResultSortText = [System.IO.File]::ReadAllText($SearchResultSort)

$AccountResponseDeclarations = [regex]::Matches(
    $SmokeText,
    '\bvar\s+accountResponse\s*='
).Count
if ($AccountResponseDeclarations -ne 1) {
    throw "C# CS0128 regression: accountResponse must be declared exactly once; found $AccountResponseDeclarations declarations."
}
if (-not $SmokeText.Contains('var mullvadAccountResponse = await search.SearchAsync(')) {
    throw "Mullvad account regression must use its scenario-specific response variable."
}
if (-not $MainWindowText.Contains('_ = Dispatcher.BeginInvoke(')) {
    throw "Background preview dispatcher operation must be explicitly discarded to prevent CS4014."
}

if ($MainWindowText.Contains('is string sourcePath') -and
    $MainWindowText.Contains('foreach (var sourcePath in paths)')) {
    throw "C# CS0136 regression: favorite reorder and file-drop loop both declare sourcePath."
}
if (-not $MainWindowText.Contains('is string reorderSourcePath') -or
    $MainWindowText -notmatch 'FavoritePathService\.MoveFavorite\(\s*_settings\.Favorites,\s*reorderSourcePath,\s*targetPath') {
    throw "Favorite reorder payload must use reorderSourcePath."
}

if ($MainWindowText.Contains('ResolveAllAvailableRoots()')) {
    throw "C# CS0103 regression: removed ResolveAllAvailableRoots is still called."
}
if (-not $MainWindowText.Contains('ResolveAllAvailableRootsWithoutProbe()')) {
    throw "Current all-roots resolver call is missing."
}

if (-not $VisibilityText.Contains('FileAttributes.Hidden | FileAttributes.System') -or
    -not $VisibilityText.Contains('name.StartsWith("~", StringComparison.Ordinal)')) {
    throw "Hidden/system and tilde-prefixed entries must remain excluded from search."
}
if (-not $MainWindowText.Contains('Search local cache files before touching a potentially sleeping') -or
    $MainWindowText.Contains('MaximumInteractiveAnalysisPasses = 4') -or
    -not $MainWindowText.Contains('남은 파일은 유휴 시간에 분석합니다')) {
    throw "The cache-first read-only foreground search contract is missing."
}
$TitleProgressStart = $MainWindowText.IndexOf(
    'var titleProgress = new Progress<TitleSearchProgress>'
)
if ($TitleProgressStart -lt 0) {
    throw "Independent title progress block was not found."
}
$TitleProgressEnd = $MainWindowText.IndexOf(
    'var titleSearchTask = _instantTitleSearchService',
    $TitleProgressStart
)
if ($TitleProgressEnd -le $TitleProgressStart) {
    throw "Independent title progress block was not found."
}
$TitleProgressBlock = $MainWindowText.Substring(
    $TitleProgressStart,
    $TitleProgressEnd - $TitleProgressStart
)
if ($TitleProgressBlock.Contains('MergeProgressiveSearchResults')) {
    throw "Title-only hits must not be copied into integrated search results."
}
if (-not $TitleSearchText.Contains('intent.RequestedExtensions.Count == 0') -or
    -not $TitleSearchText.Contains('intent.Categories.Count == 0') -or
    -not $TitleSearchText.Contains('!intent.DirectoryOnly') -or
    -not $SmokeText.Contains('제목 키워드가 없는 이미지 종류 요청도 즉시 표시') -or
    -not $SmokeText.Contains('제목 키워드가 없는 폴더 요청도 즉시 표시')) {
    throw "Structured type-only title search contract is missing."
}
if (-not $QueryInterpreterText.Contains('ExtractExplicitExtensions(query)') -or
    -not $QueryInterpreterText.Contains('ExplicitExtensionRegex()') -or
    -not $QueryInterpreterText.Contains('"찾아달라"') -or
    -not $QueryInterpreterText.Contains('"ssh키"') -or
    -not $QueryInterpreterText.Contains('MeaningfulShortTokens') -or
    -not $QueryInterpreterText.Contains('IsSearchableToken(') -or
    -not $TitleSearchText.Contains('var normalizedTitle = Normalize(name)') -or
    -not $TitleSearchText.Contains('ContainsAlternativeTerm(') -or
    -not $RankingText.Contains('TokenizeText(record.Name)') -or
    -not $SmokeText.Contains('등록되지 않은 점 표기 확장자도 정확한 제목 검색 조건으로 사용') -or
    -not $SmokeText.Contains('카탈로그에 없는 명시적 확장자를 통합 검색에서 발견') -or
    -not $SmokeText.Contains('카탈로그에 없는 확장자를 파일명 메타데이터로 통합 검색') -or
    -not $SmokeText.Contains('AWS SSH 키 자연어 검색에서 영문 key 제목을 즉시 발견') -or
    -not $SmokeText.Contains('SSH key 의미가 맞는 PPK 파일을 AWS 단어만 맞는 문서보다 우선') -or
    -not $SmokeText.Contains('AWS 키 자연어 변형 검색에서 정답 파일 Recall@3 보장')) {
    throw "Arbitrary file-extension discovery contract is missing."
}
if (-not $RankingText.Contains('Preserve low-coverage candidates') -or
    -not $RankingText.Contains('FileMetadataDescriptor.GetSearchTerms') -or
    -not $RankingText.Contains('AllowsCompactLanguagePartialMatch') -or
    -not $SmokeText.Contains('본문에 두 단어가 있는 잡음보다 IT팀 계정관리 파일명을 우선') -or
    -not $SmokeText.Contains('var firstAccountNoiseRank = FindResultRank(') -or
    -not $SmokeText.Contains('accountManagementRank < firstAccountNoiseRank') -or
    -not $SmokeText.Contains('static int FindResultRank<T>(') -or
    -not $MainWindowXamlText.Contains('VirtualizingPanel.ScrollUnit="Pixel"') -or
    -not $MainWindowXamlText.Contains('CanReservePreviewSpace') -or
    -not $TextPromptText.Contains('SizeToContent="Height"') -or
    -not $TextPromptText.Contains('ResizeMode="CanResizeWithGrip"')) {
    throw "Name-first ranking, smooth result scrolling, or prompt layout contract is missing."
}
if (-not $MetadataDescriptorText.Contains('DescriptorVersion = 2') -or
    -not $MetadataDescriptorText.Contains('PuTTY SSH 개인키') -or
    -not $MetadataDescriptorText.Contains('BuildSemanticText(') -or
    -not $SemanticText.Contains('CurrentFormatVersion = 6') -or
    -not $SemanticText.Contains('record.TextHash') -or
    -not $SemanticText.Contains('핵심 검색 의도:') -or
    -not $MetadataSearchText.Contains('AddRankFusionEvidence(') -or
    -not $MetadataSearchText.Contains('GetHybridRankScore(') -or
    -not $TitleSearchText.Contains('BuildParentContext(fullPath)') -or
    -not $SmokeText.Contains('Other 특수 파일도 형식 의미와 상위 경로를 메타데이터 의미 문서로 구성') -or
    -not $SmokeText.Contains('빠른 검색에서 파일명 AWS와 계정관리문서 경로 단서를 결합') -or
    -not $SmokeText.Contains('약한 다중 검색어 후보를 보존하되 문맥이 완전한 파일보다 낮게 배치')) {
    throw "Metadata-wide semantic retrieval contract is missing."
}
if (-not $SmokeText.Contains('엑셀 시트명과 내부 셀 값 직접 추출') -or
    -not $SmokeText.Contains('평범한 엑셀 제목과 내부 장비명을 결합해 최상위 검색') -or
    -not $MainWindowText.Contains('남은 파일은 유휴 시간에 분석합니다') -or
    -not $AppProjectText.Contains('ExcelDataReader" Version="3.9.0"') -or
    -not $DocumentExtractorText.Contains('ExcelReaderFactory.CreateReader') -or
    -not $ContentIndexText.Contains('preferredFiles') -or
    -not $ContentSearchText.Contains('DocumentContentSource.Spreadsheet')) {
    throw "Spreadsheet cell search or read-only foreground indexing contract is missing."
}
if (-not $QueryInterpreterText.Contains('ContentEvidenceGroups') -or
    -not $QueryInterpreterText.Contains('ContentEvidenceAlternatives') -or
    -not $ContentSearchText.Contains('term.ContentEvidenceAlternatives') -or
    -not $ContentSearchText.Contains('실제 일치') -or
    -not $SmokeText.Contains('접속 점검표를 계정 내용 일치로 오인하지 않음') -or
    -not $SmokeText.Contains('번역어 본문 근거에 실제 일치 단어 표시')) {
    throw "Direct content evidence must not treat login, authentication, or connection as literal account content."
}

$ExpectedReasonFragment = "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다."
$ForbiddenReasonFragment = "추출된 본문을 포함한 문서 정보의 다국어 의미가"
$RegressionScenario = "낮은 본문 단어 커버리지는 AI 후보를 덮어쓰지 않음"

if (-not $SemanticText.Contains($ExpectedReasonFragment)) {
    throw "AI semantic candidate reason contract is missing."
}
if ($SemanticText.Contains($ForbiddenReasonFragment)) {
    throw "AI semantic candidate reason can be mistaken for content evidence."
}
if (-not $SmokeText.Contains($RegressionScenario)) {
    throw "AI semantic evidence regression test is missing."
}
if ($SmokeText -match 'semanticOnlyResult\.Reason\.Contains\(\s*"본문"') {
    throw "Semantic evidence regression must not reject a reason merely because a disclaimer contains the word body."
}
if (-not $SmokeText.Contains('semanticOnlyResult.EvidenceKind ==') -or
    -not $SmokeText.Contains('SearchEvidenceKind.SemanticCandidate') -or
    -not $SmokeText.Contains('HasDirectContentEvidenceReason(')) {
    throw "Semantic evidence regression must inspect evidence kind and direct-content reason markers."
}

$NetworkContracts = @(
    "WNetAddConnection3",
    "WNetOpenEnum",
    "WNetEnumResource",
    "NetShareEnum",
    "NetUseEnum",
    "GetConnectedSharedFolders()",
    "ConnectedNetworkShareInfo",
    "NormalizeNetworkLocationPath",
    "IsUncServerRoot",
    "EnumerateServerSharesAsync",
    "expanded.Length == 2",
    "WaitAsync(TimeSpan.FromSeconds(12)"
)
foreach ($Contract in $NetworkContracts) {
    if (-not $NetworkText.Contains($Contract)) {
        throw "Network path contract is missing: $Contract"
    }
}

$DialogContracts = @(
    'SizeToContent="Height"',
    'ResizeMode="CanResizeWithGrip"',
    'HorizontalContentAlignment="Stretch"',
    'x:Name="PathTextBox"',
    'Content="연결 확인"'
)
foreach ($Contract in $DialogContracts) {
    if (-not $DialogText.Contains($Contract)) {
        throw "Network dialog layout contract is missing: $Contract"
    }
}
if ($DialogText.Contains('Height="330"') -or
    $DialogText.Contains('ResizeMode="NoResize"')) {
    throw "Network dialog reverted to the clipped fixed-size layout."
}
if ($DialogCodeText.Contains('.TrimEnd(Path.DirectorySeparatorChar)')) {
    throw "Mapped drive root can be truncated from Z:\ to Z:."
}
if (-not $MainWindowText.Contains('new NetworkLocationDialog(this, _networkPathService)') -or
    -not $MainWindowText.Contains('NavigationNodeKind.Computer') -or
    -not $MainWindowText.Contains('ShowComputerViewAsync') -or
    -not $MainWindowText.Contains('CollectNetworkTreeLocations') -or
    -not $MainWindowText.Contains('NetworkPathService.GetConnectedSharedFolders()')) {
    throw "Main window network discovery or My PC view contract is missing."
}
if (-not $MainWindowText.Contains('EnsureSearchRootsAccessibleAsync') -or
    -not $MainWindowText.Contains('.Where(IsSyntacticallyValidSearchRoot)') -or
    -not $MainWindowText.Contains('_networkPathService.EnsureAccessibleAsync(')) {
    throw "Search roots must remain syntactically valid candidates until asynchronous access and reconnect checks run."
}
if ($MainWindowText.Contains('NetworkPathService.GetKnownNetworkLocations()')) {
    throw "Remembered or mapped network drives must not be auto-added to the UI."
}
if (-not $MainWindowText.Contains('drive.IsReady && drive.DriveType != DriveType.Network')) {
    throw "Automatic mapped-drive navigation is still enabled."
}
if (-not $MainWindowText.Contains('MaximumResults: 500')) {
    throw "Search result maximum was not expanded to 500."
}
if (-not $SmokeText.Contains('첫 완전 일치 이후에도 관련 파일 계속 수집')) {
    throw "Search result volume regression test is missing."
}
foreach ($Scenario in @(
    "내 PC 탐색 노드 선택 가능",
    "매핑 드라이브 루트의 역슬래시 보존",
    "UNC 공유 루트 정규화",
    "UNC 네트워크 경로 판별",
    "IP 주소만 입력해 UNC 서버 루트로 정규화",
    "UNC 서버 최상위 위치 판별",
    "공유 폴더에서 서버 최상위로 이동"
)) {
    if (-not $SmokeText.Contains($Scenario)) {
        throw "Network smoke regression test is missing: $Scenario"
    }
}


$FavoriteContracts = @(
    'Text="끌어서 추가"',
    'AllowDrop="True"',
    'PreviewDragOver="NavigationTree_PreviewDragOver"',
    'Drop="NavigationTree_Drop"',
    'Handler="NavigationTreeItem_PreviewMouseMove"',
    'Click="AddNavigationFolderToFavoritesMenuItem_Click"',
    'Click="AddSelectedFolderToFavoritesMenuItem_Click"',
    'x:Name="AddCurrentPathToFavoritesButton"',
    'Click="AddCurrentPathToFavoritesButton_Click"',
    'SelectedIndex="0"',
    'Click="RenameFavoriteMenuItem_Click"',
    'Click="RemoveFavoriteMenuItem_Click"'
)
foreach ($Contract in $FavoriteContracts) {
    if (-not $MainWindowXamlText.Contains($Contract) -and
        -not $MainWindowText.Contains($Contract)) {
        throw "Favorite navigation contract is missing: $Contract"
    }
}
if (-not $MainWindowText.Contains('UpdateCurrentPathFavoriteButtonState') -or
    -not $MainWindowText.Contains('GetFavoriteDisplayName(_currentPath)')) {
    throw "Current-path favorite button implementation is missing."
}
if (-not $MainWindowXamlText.Contains('x:Name="InstantTitleIndexProgressPanel"') -or
    -not $MainWindowXamlText.Contains('x:Name="InstantTitleIndexDetailText"') -or
    -not $MainWindowXamlText.Contains('MinHeight="42"') -or
    -not $MainWindowText.Contains('IsScrollBarInteraction(e.OriginalSource)') -or
    -not $MainWindowText.Contains('ScheduleInstantTitleSearch(immediate: true)') -or
    -not $InstantTitleSearchText.Contains('state.PercentComplete') -or
    -not $SmokeText.Contains('제목 색인의 현재 경로·항목 수·완료 진행률 표시') -or
    -not $SmokeText.Contains('제목 검색은 한 글자 입력부터 즉시 결과 반환')) {
    throw "Instant title input, per-keystroke feedback, scrollbar drag, or index progress contract is missing."
}
if ($MainWindowText.Contains('interpretation.ShouldRefinePreviousResults') -or
    -not $MainWindowText.Contains('SearchPlan.FromDeterministic(deterministicIntent)') -or
    -not $MainWindowText.Contains('CanUseDeterministicFastPath') -or
    -not $MainWindowText.Contains('.SearchNaturalLanguageAsync(') -or
    -not $MainWindowText.Contains('InstantTitleSearchResults.ReplaceAll') -or
    -not $InstantTitleSearchText.Contains('PostingGuaranteesMatch') -or
    -not $InstantTitleMemoryIndexText.Contains('FindNameCandidates') -or
    -not $BulkObservableCollectionText.Contains('NotifyCollectionChangedAction.Reset') -or
    -not $SmokeText.Contains('완료 색인의 한 글자 검색은 전체 순회 없이 문자 포스팅을 즉시 재사용') -or
    -not $SmokeText.Contains('간단한 폴더 자연어 검색을 완료된 제목 색인에서 즉시 처리')) {
    throw "Fast indexed title lookup or explicit-only result refinement contract is missing."
}
if ($VisualIndexText.Contains('MinimumExpandedCandidateCount') -or
    -not $VisualIndexText.Contains('strongCandidates.Length') -or
    -not $VisualIndexText.Contains('candidate.IdentityCorroborated') -or
    -not $VisualIndexText.Contains('identityCorroborated;') -or
    -not $VisualPromptText.Contains('namedSubject ? 0.02d') -or
    -not $MetadataSearchText.Contains('CalculateVisualMatchPercent(') -or
    -not $SmokeText.Contains('낮은 절대 시각 유사도를 1위라는 이유로 높은 퍼센트로 표시하지 않음')) {
    throw "Visual relevance filtering or absolute confidence calibration contract is missing."
}
if ($MainWindowText.Contains('Directory.Exists(path) || NetworkPathService.IsPotentialNetworkPath(path)')) {
    throw "UNC favorite eligibility must not block on Directory.Exists before syntax validation."
}
if ($MainWindowXamlText.Contains('Click="AddNetworkLocationButton_Click"')) {
    throw "The inactive network plus/add control is still exposed in the navigation UI."
}
if ($MainWindowText.Contains('var network = new NavigationNode(') -or
    $MainWindowText.Contains('NavigationRoots.Add(network)')) {
    throw "The standalone network navigation tree must remain removed."
}
foreach ($Contract in @(
    'FavoritePathService.TryResolve',
    'FavoritePathService.MoveFavorite',
    'FavoritePathService.TryCreateFolderTarget',
    'FavoriteReorderDataFormat',
    'NavigationNodeKind.Favorite',
    'NavigationNodeKind.FavoritesSection',
    '_settings.Favorites',
    'new FavoriteLocation'
)) {
    if (-not $MainWindowText.Contains($Contract)) {
        throw "Favorite implementation contract is missing: $Contract"
    }
}
foreach ($Contract in @(
    'TryResolveWindowsShortcut',
    'TryResolveInternetShortcut',
    'TryCreateFolderTarget',
    'MoveFavorite',
    '.lnk',
    '.url'
)) {
    if (-not $FavoriteText.Contains($Contract)) {
        throw "Favorite service contract is missing: $Contract"
    }
}
if (-not $AppSettingsText.Contains('List<FavoriteLocation> Favorites')) {
    throw "Favorite persistence contract is missing from AppSettings."
}
if (-not $MainWindowXamlText.Contains('PreviewMouseDown="Window_PreviewMouseDown"') -or
    -not $MainWindowText.Contains('MouseButton.XButton1') -or
    -not $MainWindowText.Contains('MouseButton.XButton2') -or
    -not $MainWindowText.Contains('Key.BrowserBack') -or
    -not $MainWindowText.Contains('key == Key.Back') -or
    -not $MainWindowText.Contains('FocusSearchInput()') -or
    -not $MainWindowXamlText.Contains('x:Name="SearchResultsHostGrid"') -or
    -not $MainWindowXamlText.Contains('x:Name="SearchPanelColumn"') -or
    -not $MainWindowXamlText.Contains('x:Name="InstantTitlePanelColumn"') -or
    -not $MainWindowXamlText.Contains('ResizeDirection="Columns"') -or
    -not $MainWindowXamlText.Contains('x:Name="InstantTitleSearchPanel"') -or
    -not $MainWindowXamlText.Contains('x:Name="SearchResultSortComboBox"') -or
    -not $MainWindowText.Contains('SearchResultSortComboBox_SelectionChanged') -or
    -not $AppSettingsText.Contains('SearchResultSortMode SearchResultSortMode') -or
    -not $SearchResultSortText.Contains('SearchResultSortMode.TopLevelPath') -or
    -not $SearchResultSortText.Contains('SearchResultSortMode.ModifiedNewest') -or
    -not $MainWindowXamlText.Contains('x:Name="NaturalLanguageInterpretationBar"') -or
    -not $MainWindowText.Contains('NaturalLanguageSearchService') -or
    -not $TextAttributesText.Contains('SearchTextAttributeMode.Only') -or
    -not $SmokeText.Contains('한글로만 된 파일을 확장자 제외 파일명 전용 조건으로 해석') -or
    -not $SmokeText.Contains('빠른 이름 검색도 확장자를 제외한 한글 전용 파일명만 발견') -or
    -not $SmokeText.Contains('통합 검색도 확장자를 제외한 한글 전용 파일명만 발견') -or
    -not $SmokeText.Contains('검색 결과를 일치도·드라이브 최상위 경로·가나다·최신 수정일로 정렬')) {
    throw "Explorer input, Hangul-only filename, or responsive result layout contract is missing."
}
if (-not $AppProjectText.Contains('<UseWindowsForms>true</UseWindowsForms>') -or
    -not $AppProjectText.Contains('WFO0003') -or
    -not $AppXamlText.Contains('ShutdownMode="OnExplicitShutdown"') -or
    -not $AppCodeText.Contains('IsSessionEnding = true') -or
    -not $MainWindowText.Contains('HideToSystemTray()') -or
    -not $MainWindowText.Contains('_settings.UseSystemTrayBackground') -or
    -not $MainWindowText.Contains('RequestApplicationExit()') -or
    -not $MainWindowText.Contains('BackgroundIndexRootPlanner.OrderRoots(') -or
    -not $MainWindowText.Contains('ResolveFavoriteIndexRootsWithoutProbe()') -or
    -not $BackgroundIndexWorkPolicyText.Contains('TimeSpan.FromMinutes(5)') -or
    -not $BackgroundIndexWorkPolicyText.Contains('MaximumNewVisualDocumentsPerRoot: 0') -or
    -not $BackgroundIndexPlannerText.Contains('StringComparer.OrdinalIgnoreCase') -or
    -not $TrayIconText.Contains('AI 탐색기 열기') -or
    -not $TrayIconText.Contains('백그라운드 색인 일시 중지') -or
    -not $TrayIconText.Contains('완전히 종료') -or
    -not $TrayIconText.Contains('public void SetVisible(bool visible)') -or
    -not $SmokeText.Contains('백그라운드 색인은 현재 검색 위치 다음에 즐겨찾기를 우선하고 중복 제거') -or
    -not $SmokeText.Contains('전면 실행 중에는 제목 색인만 수행하고 무거운 AI 색인은 긴 유휴·트레이에서만 배치 실행')) {
    throw "Favorite-first background indexing or system-tray lifecycle contract is missing."
}
foreach ($Scenario in @(
    '폴더 드래그 즐겨찾기 등록',
    '즐겨찾기 탐색 노드 선택 가능',
    'URL 바로가기 드래그 즐겨찾기 등록',
    '폴더 우클릭 즐겨찾기 등록',
    '즐겨찾기 드래그 순서 변경',
    '즐겨찾기 섹션은 탐색 대상이 아님'
)) {
    if (-not $SmokeText.Contains($Scenario)) {
        throw "Favorite smoke regression test is missing: $Scenario"
    }
}

$bundleScriptPath = Join-Path $root "tools\prepare_ai_bundle.ps1"
$bundleScript = Get-Content -LiteralPath $bundleScriptPath -Raw
$bundleTokens = $null
$bundleParseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    $bundleScriptPath,
    [ref]$bundleTokens,
    [ref]$bundleParseErrors) | Out-Null
if ($bundleParseErrors.Count -gt 0) {
    $firstBundleParseError = $bundleParseErrors[0]
    throw (
        "prepare_ai_bundle.ps1 PowerShell syntax error: " +
        $firstBundleParseError.Message)
}
if ($bundleScript -notmatch [regex]::Escape(
        '^llama-b.+-bin-win-x64\.zip$')) {
    throw "Current llama.cpp Windows x64 CPU asset naming is unsupported."
}
if ($bundleScript -notmatch "Get-WindowsX64CpuRuntimeAsset") {
    throw "The llama.cpp runtime asset selector is missing."
}
if ($bundleScript -notmatch "AllowEmptyCollection") {
    throw "The llama.cpp runtime selector does not accept an empty asset list."
}
if ($bundleScript -notmatch "Get-ReleaseAssets" -or
    $bundleScript -notmatch "assets_url") {
    throw "The llama.cpp release assets_url fallback is missing."
}
if ($bundleScript -notmatch "Find-WindowsX64CpuRuntimeRelease" -or
    $bundleScript -notmatch "releases\?per_page=10") {
    throw "The llama.cpp recent-release fallback is missing."
}

Write-Host "Preflight passed: explorer input, responsive results, exact-script filename search, tray indexing, network access, and AI runtime contracts agree."
