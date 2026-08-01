using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIExplorer.Dialogs;
using AIExplorer.Models;
using AIExplorer.Services;

namespace AIExplorer;

public partial class MainWindow : Window
{
    private const string SearchInputPlaceholder =
        "이곳에 검색 문구를 입력하세요.";
    private const string ComputerVirtualPath = "aiexplorer://computer";
    private const string FavoriteReorderDataFormat = "AIExplorer.FavoriteReorder";
    private const int MaximumSearchPreviewResults = 80;

    private readonly ShellIconService _shellIconService;
    private readonly FileSystemService _fileSystemService;
    private readonly FileOperationService _fileOperationService = new();
    private readonly ShellService _shellService;
    private readonly NetworkPathService _networkPathService = new();
    private readonly InstantTitleSearchService _instantTitleSearchService;
    private readonly WindowsApplicationCatalogService
        _applicationCatalogService = new();
    private readonly SettingsService _settingsService = new();
    private readonly ImagePreviewService _imagePreviewService = new();
    private readonly NavigationHistory _history = new();
    private readonly AiModelManager _aiModelManager;
    private readonly NaturalLanguageSearchService
        _languageSearchService;
    private readonly LocalEmbeddingService _embeddingService;
    private readonly LocalVisualEmbeddingService _visualEmbeddingService;
    private readonly LocalImageTaggingService _imageTaggingService;
    private readonly MetadataSearchService _searchService;
    private readonly AdvancedAnalysisService _advancedAnalysisService;
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _searchInputIdleTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };
    private readonly DispatcherTimer _instantTitleSearchIdleTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(220)
    };

    private AppSettings _settings = new();
    private CancellationTokenSource? _navigationCancellation;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _searchPreviewCancellation;
    private CancellationTokenSource? _backgroundIndexCancellation;
    private CancellationTokenSource? _fileOperationCancellation;
    private CancellationTokenSource? _modelInstallCancellation;
    private CancellationTokenSource? _storageMigrationCancellation;
    private CancellationTokenSource? _resultRefinementCancellation;
    private CancellationTokenSource? _instantTitleSearchCancellation;
    private CancellationTokenSource? _instantTitleIndexCancellation;
    private readonly HashSet<string> _searchPreviewAttemptedPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SearchResult> _allIntegratedSearchResults = [];
    private readonly List<SearchResult> _allTitleSearchResults = [];
    private readonly Dictionary<string, SearchResultTextFacts>
        _resultTextFacts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SearchResult>
        _instantTitleResultCache = new(StringComparer.OrdinalIgnoreCase);
    private Point _dragStartPoint;
    private NavigationNode? _navigationDragNode;
    private List<string> _clipboardPaths = [];
    private bool _clipboardIsCut;
    private string? _currentPath;
    private bool _isComputerView;
    private double _lastSearchPanelWidth = 620;
    private double _lastInstantTitlePanelWidth = 460;
    private bool _isInitializing = true;
    private bool _isClosing;
    private bool _isSearchBusy;
    private bool _isBackgroundIndexing;
    private bool _isBackgroundIndexingPausedByUser;
    private bool _isHiddenToTray;
    private bool _isExplicitExitRequested;
    private bool _shutdownCleanupCompleted;
    private bool _resultViewChosenByUser;
    private SearchResultPane _selectedResultPane =
        SearchResultPane.Integrated;
    private bool _isSearchInputPlaceholderActive;
    private string? _lastSearchQuery;
    private SearchIntent? _activeSearchIntent;
    private SearchIntent? _activeResultRefinementIntent;
    private string _activeResultRefinementQuery = string.Empty;
    private IReadOnlyList<string> _lastSearchRoots = [];
    private bool _isResultRefinementAnalyzing;
    private IReadOnlyList<string> _priorityIndexRoots = [];
    private InstantTitleSortField _instantTitleSortField =
        InstantTitleSortField.Name;
    private bool _instantTitleSortAscending = true;
    private SearchResultSortMode _searchResultSortMode =
        SearchResultSortMode.Relevance;

    public MainWindow()
    {
        var launchedProcesses =
            (Application.Current as App)?.LaunchedProcesses ??
            throw new InvalidOperationException(
                "앱 프로세스 추적 서비스를 초기화하지 못했습니다.");
        _shellService = new ShellService(launchedProcesses);
        _shellIconService = new ShellIconService();
        _fileSystemService = new FileSystemService(_shellIconService);
        _aiModelManager = new AiModelManager(
            Path.Combine(
                _settingsService.DataDirectory,
                "models",
                "semantic"));
        _languageSearchService =
            new NaturalLanguageSearchService(_aiModelManager);
        _embeddingService = new LocalEmbeddingService(_aiModelManager);
        _visualEmbeddingService = new LocalVisualEmbeddingService(
            _aiModelManager.VisualModelPath,
            _aiModelManager.VisualTokenizerPath);
        _imageTaggingService = new LocalImageTaggingService(
            _aiModelManager.ImageTaggerModelPath,
            _aiModelManager.ImageTaggerLabelsPath);
        _searchService = new MetadataSearchService(
            _fileSystemService,
            Path.Combine(_settingsService.DataDirectory, "index"),
            Path.Combine(_settingsService.DataDirectory, "content-index"),
            Path.Combine(_settingsService.DataDirectory, "semantic-index"),
            _embeddingService,
            Path.Combine(_settingsService.DataDirectory, "visual-index"),
            _visualEmbeddingService,
            _imageTaggingService);
        _instantTitleSearchService = new InstantTitleSearchService(
            _searchService.MetadataIndexService);
        _advancedAnalysisService =
            new AdvancedAnalysisService(_embeddingService);
        InitializeComponent();
        _trayIconService = new TrayIconService();
        _trayIconService.RestoreRequested +=
            TrayIconService_RestoreRequested;
        _trayIconService.ToggleIndexingRequested +=
            TrayIconService_ToggleIndexingRequested;
        _trayIconService.ExitRequested +=
            TrayIconService_ExitRequested;
        ShowSearchInputPlaceholder();
        _searchInputIdleTimer.Tick += SearchInputIdleTimer_Tick;
        _instantTitleSearchIdleTimer.Tick +=
            InstantTitleSearchIdleTimer_Tick;
        CurrentFolderIcon.Source = _shellIconService.GetStockIcon(ShellStockIcon.Folder);
        DataContext = this;
    }

    public ObservableCollection<NavigationNode> NavigationRoots { get; } = [];

    public ObservableCollection<FileSystemEntry> FileItems { get; } = [];

    public ObservableCollection<SearchResult> SearchResults { get; } = [];

    public ObservableCollection<SearchResult> TitleSearchResults { get; } = [];

    public BulkObservableCollection<SearchResult> InstantTitleSearchResults { get; } = [];

    private sealed record NetworkTreeLocation(
        string Path,
        string Name,
        bool IsConnected);

    private enum SearchResultPane
    {
        Integrated,
        Title
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        _settings.NetworkLocations ??= [];
        _settings.Favorites ??= [];
        _trayIconService.SetVisible(
            _settings.UseSystemTrayBackground);
        _lastSearchPanelWidth = _settings.SearchPanelWidth < 470
            ? 620
            : Math.Clamp(_settings.SearchPanelWidth, 470, 900);
        _lastInstantTitlePanelWidth = _settings.InstantTitlePanelWidth < 360
            ? 460
            : Math.Clamp(_settings.InstantTitlePanelWidth, 360, 720);
        InstantTitlePanelColumn.Width =
            new GridLength(_lastInstantTitlePanelWidth);

        SortComboBox.SelectedIndex = (int)_settings.SortMode;
        _searchResultSortMode = Enum.IsDefined(
            _settings.SearchResultSortMode)
            ? _settings.SearchResultSortMode
            : SearchResultSortMode.Relevance;
        SearchResultSortComboBox.SelectedIndex =
            (int)_searchResultSortMode;
        BuildNavigationTree();
        // Older builds could persist a hidden search panel through the
        // removed result-collapse action. Restore it on startup so search
        // can never disappear after upgrading.
        _settings.SearchPanelVisible = true;
        SetSearchPanelVisible(true, persist: false);
        UpdateAiModelUi();

        var initialPath = ResolveInitialPath();
        await NavigateToAsync(initialPath);
        _isInitializing = false;
        StartInstantTitleIndexWarmup();
        if (_aiModelManager.IsInstalled)
        {
            ScheduleBackgroundIndexing();
        }
        else
        {
            UpdateAiModelUi();
            StatusText.Text = "이름·경로 검색 사용 가능 · AI 미설치";
        }

        UpdateSearchResultsLayout();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        var sessionEnding =
            (Application.Current as App)?.IsSessionEnding == true;
        if (!_isExplicitExitRequested &&
            !_isClosing &&
            !sessionEnding &&
            _settings.UseSystemTrayBackground)
        {
            e.Cancel = true;
            HideToSystemTray();
            return;
        }

        if (_shutdownCleanupCompleted)
        {
            return;
        }

        _shutdownCleanupCompleted = true;
        _isClosing = true;
        _searchInputIdleTimer.Stop();
        _instantTitleSearchIdleTimer.Stop();
        _navigationCancellation?.Cancel();
        _searchCancellation?.Cancel();
        _searchPreviewCancellation?.Cancel();
        _backgroundIndexCancellation?.Cancel();
        _fileOperationCancellation?.Cancel();
        _modelInstallCancellation?.Cancel();
        _storageMigrationCancellation?.Cancel();
        _resultRefinementCancellation?.Cancel();
        _instantTitleSearchCancellation?.Cancel();
        _instantTitleIndexCancellation?.Cancel();
        _trayIconService.Dispose();
        _shellService.TerminateLaunchedProcesses();
        _languageSearchService.Dispose();
        _embeddingService.Dispose();
        _visualEmbeddingService.Dispose();
        _imageTaggingService.Dispose();
        _aiModelManager.Dispose();
        CaptureSettings();
        _ = SaveSettingsSafelyAsync();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        // A hidden-to-tray close is cancelled in Window_Closing. Therefore a
        // real Closed event always means the process must end, including when
        // the user disabled background tray operation.
        if (_isExplicitExitRequested)
        {
            return;
        }

        _isExplicitExitRequested = true;
        Application.Current.Shutdown();
    }

    private void HideToSystemTray()
    {
        _searchCancellation?.Cancel();
        CancelSearchPreviewLoading();
        Keyboard.ClearFocus();
        CaptureSettings();
        _ = SaveSettingsSafelyAsync();
        ShowInTaskbar = false;
        Hide();
        _isHiddenToTray = true;
        _trayIconService.ShowBackgroundNotice();
        _trayIconService.SetStatus(
            _isBackgroundIndexingPausedByUser
                ? "자동 색인 일시 중지"
                : "즐겨찾기 색인 준비");
        if (!_isBackgroundIndexingPausedByUser)
        {
            ScheduleBackgroundIndexing(TimeSpan.FromSeconds(1));
        }
    }

    private void RestoreFromSystemTray()
    {
        if (_isClosing)
        {
            return;
        }

        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        _isHiddenToTray = false;
        _trayIconService.SetStatus(
            _isBackgroundIndexingPausedByUser
                ? "자동 색인 일시 중지"
                : "실행 중");
    }

    private void TrayIconService_RestoreRequested(
        object? sender,
        EventArgs e) =>
        Dispatcher.Invoke(RestoreFromSystemTray);

    private void TrayIconService_ToggleIndexingRequested(
        object? sender,
        EventArgs e) =>
        Dispatcher.Invoke(ToggleBackgroundIndexingFromTray);

    private void TrayIconService_ExitRequested(
        object? sender,
        EventArgs e) =>
        Dispatcher.Invoke(RequestApplicationExit);

    private void ToggleBackgroundIndexingFromTray()
    {
        _isBackgroundIndexingPausedByUser =
            !_isBackgroundIndexingPausedByUser;
        _trayIconService.SetIndexingPaused(
            _isBackgroundIndexingPausedByUser);
        if (_isBackgroundIndexingPausedByUser)
        {
            PauseBackgroundIndexing();
            IndexStatusText.Text = "사용자 요청 · 자동 색인 일시 중지";
            StatusText.Text = "백그라운드 색인을 일시 중지했습니다.";
        }
        else
        {
            StatusText.Text = "백그라운드 색인을 다시 시작했습니다.";
            ScheduleBackgroundIndexing(TimeSpan.FromSeconds(1));
        }
    }

    private void RequestApplicationExit()
    {
        if (_isClosing)
        {
            return;
        }

        _isExplicitExitRequested = true;
        Application.Current.Shutdown();
    }

    private string ResolveInitialPath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastPath) &&
            (Directory.Exists(_settings.LastPath) ||
             NetworkPathService.IsPotentialNetworkPath(_settings.LastPath)))
        {
            return _settings.LastPath;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (Directory.Exists(documents))
        {
            return documents;
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (Directory.Exists(desktop))
        {
            return desktop;
        }

        return GetReadyDrives().FirstOrDefault()?.RootDirectory.FullName
               ?? AppContext.BaseDirectory;
    }

    private void BuildNavigationTree()
    {
        NavigationRoots.Clear();

        var quickAccess = new NavigationNode(
            "빠른 접근",
            null,
            "\uE718",
            NavigationNodeKind.Section,
            iconImage: _shellIconService.GetStockIcon(ShellStockIcon.Folder))
        {
            IsExpanded = true,
            IsLoaded = true
        };
        AddSpecialFolder(
            quickAccess,
            "바탕 화면",
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "\uE8FC");
        AddSpecialFolder(
            quickAccess,
            "문서",
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "\uE8A5");
        AddSpecialFolder(
            quickAccess,
            "다운로드",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"),
            "\uE896");
        NavigationRoots.Add(quickAccess);

        var favorites = new NavigationNode(
            "즐겨찾기",
            null,
            "\uE734",
            NavigationNodeKind.FavoritesSection,
            iconImage: _shellIconService.GetStockIcon(ShellStockIcon.Folder))
        {
            IsExpanded = true,
            IsLoaded = true
        };

        foreach (var favorite in _settings.Favorites
                     .Where(item =>
                         !string.IsNullOrWhiteSpace(item.Path))
                     .GroupBy(
                         item => FavoritePathService.GetIdentity(item.Path),
                         StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var isNetwork = NetworkPathService.IsPotentialNetworkPath(favorite.Path);
            favorites.Children.Add(new NavigationNode(
                string.IsNullOrWhiteSpace(favorite.Name)
                    ? GetFavoriteDisplayName(favorite.Path)
                    : favorite.Name,
                favorite.Path,
                "\uE734",
                NavigationNodeKind.Favorite,
                canExpand: true,
                iconImage: isNetwork
                    ? _shellIconService.GetStockIcon(ShellStockIcon.NetworkDrive)
                    : _shellIconService.GetFileSystemIcon(
                        favorite.Path,
                        isDirectory: true,
                        preferSpecific: true)));
        }

        if (favorites.Children.Count == 0)
        {
            favorites.Children.Add(new NavigationNode(
                "여기로 끌어 추가",
                null,
                "\uE734",
                NavigationNodeKind.Placeholder));
        }

        NavigationRoots.Add(favorites);

        var computer = new NavigationNode(
            "내 PC",
            null,
            "\uE7F1",
            NavigationNodeKind.Computer,
            iconImage: _shellIconService.GetStockIcon(ShellStockIcon.DesktopPc))
        {
            IsExpanded = true,
            IsLoaded = true
        };

        foreach (var drive in GetNavigationDrives())
        {
            var label = GetDriveDisplayName(drive);
            computer.Children.Add(new NavigationNode(
                label,
                drive.RootDirectory.FullName,
                drive.DriveType == DriveType.Network ? "\uE968" : "\uEDA2",
                drive.DriveType == DriveType.Network
                    ? NavigationNodeKind.Network
                    : NavigationNodeKind.Drive,
                canExpand: true,
                iconImage: _shellIconService.GetStockIcon(
                    GetDriveStockIcon(drive.DriveType))));
        }

        NavigationRoots.Add(computer);
        UpdateCurrentPathFavoriteButtonState();
    }

    private IReadOnlyList<NetworkTreeLocation> CollectNetworkTreeLocations()
    {
        var locations = new Dictionary<string, NetworkTreeLocation>(
            StringComparer.OrdinalIgnoreCase);

        void Add(string path, string name, bool isConnected)
        {
            string normalized;
            try
            {
                normalized = NetworkPathService.NormalizeNetworkLocationPath(path);
                if (NetworkPathService.TryResolveToUnc(normalized, out var uncPath))
                {
                    normalized = uncPath;
                }
            }
            catch
            {
                return;
            }

            if (!NetworkPathService.IsUncPath(normalized))
            {
                return;
            }

            var identity = GetNetworkLocationIdentity(normalized);
            if (locations.TryGetValue(identity, out var existing))
            {
                locations[identity] = existing with
                {
                    Name = string.IsNullOrWhiteSpace(existing.Name) ? name : existing.Name,
                    IsConnected = existing.IsConnected || isConnected
                };
            }
            else
            {
                locations[identity] = new NetworkTreeLocation(
                    normalized,
                    name,
                    isConnected);
            }
        }

        foreach (var share in NetworkPathService.GetConnectedSharedFolders())
        {
            Add(share.Path, share.Name, isConnected: true);
        }

        foreach (var location in _settings.NetworkLocations)
        {
            Add(location.Path, location.Name, isConnected: false);
        }

        return locations.Values.ToArray();
    }

    private NavigationNode CreateNetworkShareNode(
        string name,
        string sharePath,
        bool isConnected)
    {
        var displayName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileName(sharePath)
            : name;
        if (!isConnected &&
            !displayName.Contains("연결 필요", StringComparison.Ordinal))
        {
            displayName += " · 연결 필요";
        }

        return new NavigationNode(
            displayName,
            sharePath,
            "\uE8CE",
            NavigationNodeKind.NetworkShare,
            canExpand: true,
            iconImage: _shellIconService.GetStockIcon(ShellStockIcon.NetworkDrive));
    }

    private static string GetNetworkLocationIdentity(string path)
    {
        try
        {
            return NetworkPathService
                .NormalizeDirectoryPath(path)
                .TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    private static IEnumerable<DriveInfo> GetReadyDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            bool ready;
            try
            {
                ready = drive.IsReady;
            }
            catch
            {
                ready = false;
            }

            if (ready)
            {
                yield return drive;
            }
        }
    }

    private static IEnumerable<DriveInfo> GetNavigationDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            bool include;
            try
            {
                include = drive.IsReady && drive.DriveType != DriveType.Network;
            }
            catch
            {
                include = false;
            }

            if (include)
            {
                yield return drive;
            }
        }
    }

    private static string GetDriveDisplayName(DriveInfo drive)
    {
        var driveName = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
        string? volumeLabel = null;
        try
        {
            if (drive.IsReady)
            {
                volumeLabel = drive.VolumeLabel;
            }
        }
        catch
        {
            // 볼륨 이름을 읽지 못한 로컬 드라이브는 기본 이름으로 표시합니다.
        }

        if (!string.IsNullOrWhiteSpace(volumeLabel))
        {
            return $"{volumeLabel} ({driveName})";
        }

        return drive.DriveType == DriveType.Network
            ? $"네트워크 드라이브 ({driveName})"
            : $"로컬 디스크 ({driveName})";
    }

    private static string GetFavoriteDisplayName(string path)
    {
        var trimmed = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name)
            ? trimmed.TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            : name;
    }

    private void AddSpecialFolder(
        NavigationNode parent,
        string name,
        string path,
        string icon)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            parent.Children.Add(new NavigationNode(
                name,
                path,
                icon,
                NavigationNodeKind.SpecialFolder,
                canExpand: true,
                iconImage: _shellIconService.GetFileSystemIcon(
                    path,
                    isDirectory: true,
                    preferSpecific: true)));
        }
    }

    private static ShellStockIcon GetDriveStockIcon(DriveType driveType) =>
        driveType switch
        {
            DriveType.Removable => ShellStockIcon.RemovableDrive,
            DriveType.Fixed => ShellStockIcon.FixedDrive,
            DriveType.Network => ShellStockIcon.NetworkDrive,
            DriveType.CDRom => ShellStockIcon.OpticalDrive,
            DriveType.Ram => ShellStockIcon.RamDrive,
            _ => ShellStockIcon.UnknownDrive
        };

    private async void NavigationTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not TreeViewItem item ||
            item.DataContext is not NavigationNode node ||
            node.IsLoaded ||
            node.IsPlaceholder ||
            string.IsNullOrWhiteSpace(node.Path))
        {
            return;
        }

        node.IsLoaded = true;
        node.Children.Clear();

        try
        {
            if (NetworkPathService.IsPotentialNetworkPath(node.Path))
            {
                var access = await _networkPathService.EnsureAccessibleAsync(
                    this,
                    node.Path,
                    promptForConnection: true,
                    cancellationToken: CancellationToken.None);
                if (!access.Success)
                {
                    node.IsLoaded = false;
                    node.Children.Add(new NavigationNode(
                        access.Message,
                        null,
                        "\uE783",
                        NavigationNodeKind.Placeholder));
                    return;
                }
            }

            if (node.Kind == NavigationNodeKind.NetworkServer ||
                NetworkPathService.IsUncServerRoot(node.Path))
            {
                var shareResult = await _networkPathService.EnumerateServerSharesAsync(
                    node.Path,
                    CancellationToken.None);
                if (!shareResult.Success)
                {
                    node.IsLoaded = false;
                    node.Children.Add(new NavigationNode(
                        shareResult.Message,
                        null,
                        "\uE783",
                        NavigationNodeKind.Placeholder));
                    return;
                }

                foreach (var share in shareResult.Shares)
                {
                    node.Children.Add(CreateNetworkShareNode(
                        share.Name,
                        share.Path,
                        isConnected: true));
                }

                if (node.Children.Count == 0)
                {
                    node.Children.Add(new NavigationNode(
                        "표시 가능한 공유 폴더가 없습니다.",
                        null,
                        "\uE783",
                        NavigationNodeKind.Placeholder));
                }

                return;
            }

            var children = await _fileSystemService.GetChildDirectoriesAsync(
                node.Path,
                CancellationToken.None);
            foreach (var child in children)
            {
                node.Children.Add(child);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            node.IsLoaded = false;
            node.Children.Add(new NavigationNode(
                "접근할 수 없음",
                null,
                "\uE783",
                NavigationNodeKind.Placeholder));
        }
    }

    private async void NavigationTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not NavigationNode { IsSelectable: true } node)
        {
            return;
        }

        if (node.Kind == NavigationNodeKind.Computer)
        {
            await NavigateToAsync(ComputerVirtualPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.Path))
        {
            await NavigateToAsync(node.Path);
        }
    }

    private void NavigationTree_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void NavigationTreeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var node = NavigationTree.SelectedItem as NavigationNode;
        var isFavorite = node?.Kind == NavigationNodeKind.Favorite;
        var canAdd = node is
        {
            Path: not null,
            Kind: not NavigationNodeKind.Favorite and
                  not NavigationNodeKind.Placeholder and
                  not NavigationNodeKind.Computer and
                  not NavigationNodeKind.Section and
                  not NavigationNodeKind.FavoritesSection
        } && IsFavoriteCandidatePath(node.Path);
        var alreadyAdded = canAdd && IsFavoritePath(node!.Path!);

        AddNavigationFolderToFavoritesMenuItem.Visibility = canAdd
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddNavigationFolderToFavoritesMenuItem.IsEnabled = canAdd && !alreadyAdded;
        AddNavigationFolderToFavoritesMenuItem.Header = alreadyAdded
            ? "이미 즐겨찾기에 있음"
            : "즐겨찾기에 추가";
        FavoriteAddSeparator.Visibility = canAdd
            ? Visibility.Visible
            : Visibility.Collapsed;

        RenameFavoriteMenuItem.Visibility = isFavorite
            ? Visibility.Visible
            : Visibility.Collapsed;
        RemoveFavoriteMenuItem.Visibility = isFavorite
            ? Visibility.Visible
            : Visibility.Collapsed;
        FavoriteEditSeparator.Visibility = isFavorite
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void AddNavigationFolderToFavoritesMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (NavigationTree.SelectedItem is not NavigationNode
            { Path: not null } node)
        {
            return;
        }

        await AddFavoritePathAsync(node.Path, node.Name);
    }

    private async Task NavigateToAsync(string requestedPath, bool recordHistory = true)
    {
        if (string.Equals(
                requestedPath,
                ComputerVirtualPath,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedPath.Trim(), "내 PC", StringComparison.OrdinalIgnoreCase))
        {
            await ShowComputerViewAsync(recordHistory);
            return;
        }

        string path;
        try
        {
            path = NetworkPathService.LooksLikeBareNetworkHost(requestedPath.Trim())
                ? NetworkPathService.NormalizeNetworkLocationPath(requestedPath)
                : NormalizeDirectoryPath(requestedPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            ShowError("경로를 열 수 없습니다.", exception);
            return;
        }

        _navigationCancellation?.Cancel();
        _navigationCancellation?.Dispose();
        _navigationCancellation = new CancellationTokenSource();
        var token = _navigationCancellation.Token;

        SetNavigationBusy(true);
        StatusText.Text = "폴더 연결을 확인하는 중...";

        try
        {
            if (NetworkPathService.IsPotentialNetworkPath(path))
            {
                var access = await _networkPathService.EnsureAccessibleAsync(
                    this,
                    path,
                    promptForConnection: true,
                    cancellationToken: token);
                if (!access.Success)
                {
                    MessageBox.Show(
                        this,
                        access.Message,
                        "네트워크 위치",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
            }
            else if (!Directory.Exists(path))
            {
                MessageBox.Show(
                    this,
                    "폴더를 찾을 수 없습니다.",
                    "AI 탐색기",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            StatusText.Text = NetworkPathService.IsUncServerRoot(path)
                ? "서버의 공유 폴더를 불러오는 중..."
                : "폴더 내용을 불러오는 중...";

            IReadOnlyList<FileSystemEntry> entries;
            if (NetworkPathService.IsUncServerRoot(path))
            {
                var shareResult = await _networkPathService.EnumerateServerSharesAsync(
                    path,
                    token);
                if (!shareResult.Success)
                {
                    MessageBox.Show(
                        this,
                        shareResult.Message,
                        "네트워크 공유 폴더",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                entries = shareResult.Shares
                    .Select(CreateServerShareEntry)
                    .ToArray();
            }
            else
            {
                entries = await _fileSystemService.GetEntriesAsync(
                    path,
                    _settings.SortMode,
                    token);
            }

            token.ThrowIfCancellationRequested();
            ApplyFolderEntries(entries);

            _isComputerView = false;
            _currentPath = path;
            PathTextBox.Text = path;
            CurrentFolderIcon.Source = NetworkPathService.IsUncServerRoot(path)
                ? _shellIconService.GetStockIcon(ShellStockIcon.MyNetwork)
                : _shellIconService.GetFileSystemIcon(path, isDirectory: true);

            if (recordHistory)
            {
                _history.Record(path);
            }

            _settings.LastPath = path;
            UpdateNavigationButtons();
            UpdateCurrentPathFavoriteButtonState();
            StatusText.Text = path;
            if (!_isInitializing &&
                InstantTitleScopeComboBox.SelectedIndex is 1 or 2)
            {
                ScheduleInstantTitleSearch();
            }
            _ = SaveSettingsSafelyAsync();
        }
        catch (OperationCanceledException)
        {
            // A newer navigation request superseded this one.
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ShowError("폴더 내용을 불러오지 못했습니다.", exception);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                SetNavigationBusy(false);
            }
        }
    }

    private async Task ShowComputerViewAsync(bool recordHistory)
    {
        _navigationCancellation?.Cancel();
        _navigationCancellation?.Dispose();
        _navigationCancellation = new CancellationTokenSource();
        var token = _navigationCancellation.Token;

        SetNavigationBusy(true);
        StatusText.Text = "PC의 드라이브와 네트워크 위치를 불러오는 중...";
        try
        {
            var drives = GetNavigationDrives().ToArray();
            var networkLocations = await Task.Run(
                CollectNetworkTreeLocations,
                token);

            var entries = new List<FileSystemEntry>();
            foreach (var drive in drives)
            {
                entries.Add(CreateDriveEntry(drive));
            }

            foreach (var server in networkLocations
                         .Select(location =>
                             NetworkPathService.GetUncServerRoot(location.Path))
                         .Where(server => !string.IsNullOrWhiteSpace(server))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(server => server, StringComparer.CurrentCultureIgnoreCase))
            {
                entries.Add(CreateNetworkServerEntry(server!));
            }

            foreach (var location in networkLocations
                         .Where(location =>
                             NetworkPathService.GetUncShareRoot(location.Path) is not null)
                         .GroupBy(
                             location => GetNetworkLocationIdentity(location.Path),
                             StringComparer.OrdinalIgnoreCase)
                         .Select(group => group
                             .OrderByDescending(location => location.IsConnected)
                             .First())
                         .OrderBy(
                             location => location.Name,
                             StringComparer.CurrentCultureIgnoreCase))
            {
                entries.Add(CreateKnownNetworkLocationEntry(location));
            }

            token.ThrowIfCancellationRequested();
            ApplyFolderEntries(entries);
            _currentPath = null;
            _isComputerView = true;
            PathTextBox.Text = "내 PC";
            CurrentFolderIcon.Source =
                _shellIconService.GetStockIcon(ShellStockIcon.DesktopPc);

            if (recordHistory)
            {
                _history.Record(ComputerVirtualPath);
            }

            UpdateNavigationButtons();
            UpdateCurrentPathFavoriteButtonState();
            StatusText.Text = "내 PC";
            if (!_isInitializing &&
                InstantTitleScopeComboBox.SelectedIndex is 1 or 2)
            {
                ScheduleInstantTitleSearch();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer navigation request superseded this one.
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                SetNavigationBusy(false);
            }
        }
    }

    private void ApplyFolderEntries(IEnumerable<FileSystemEntry> entries)
    {
        FileItems.Clear();
        foreach (var entry in entries)
        {
            FileItems.Add(entry);
        }

        FolderItemCountText.Text = $"{FileItems.Count:N0}개 항목";
        FileEmptyPanel.Visibility = FileItems.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private FileSystemEntry CreateDriveEntry(DriveInfo drive)
    {
        return new FileSystemEntry
        {
            Name = GetDriveDisplayName(drive),
            FullPath = drive.RootDirectory.FullName,
            IsDirectory = true,
            SizeBytes = null,
            SizeDisplay = string.Empty,
            ModifiedAt = DateTime.MinValue,
            ModifiedDisplay = string.Empty,
            TypeDisplay = drive.DriveType == DriveType.Network
                ? "네트워크 드라이브"
                : "드라이브",
            IconGlyph = drive.DriveType == DriveType.Network ? "\uE968" : "\uEDA2",
            IconImage = _shellIconService.GetStockIcon(
                GetDriveStockIcon(drive.DriveType))
        };
    }

    private FileSystemEntry CreateNetworkServerEntry(string serverRoot)
    {
        return new FileSystemEntry
        {
            Name = serverRoot.TrimStart(Path.DirectorySeparatorChar),
            FullPath = serverRoot,
            IsDirectory = true,
            SizeBytes = null,
            SizeDisplay = string.Empty,
            ModifiedAt = DateTime.MinValue,
            ModifiedDisplay = string.Empty,
            TypeDisplay = "네트워크 컴퓨터",
            IconGlyph = "\uE968",
            IconImage = _shellIconService.GetStockIcon(ShellStockIcon.MyNetwork)
        };
    }

    private FileSystemEntry CreateKnownNetworkLocationEntry(
        NetworkTreeLocation location)
    {
        var name = string.IsNullOrWhiteSpace(location.Name)
            ? Path.GetFileName(location.Path)
            : location.Name;
        return new FileSystemEntry
        {
            Name = name,
            FullPath = location.Path,
            IsDirectory = true,
            SizeBytes = null,
            SizeDisplay = string.Empty,
            ModifiedAt = DateTime.MinValue,
            ModifiedDisplay = string.Empty,
            TypeDisplay = location.IsConnected
                ? "연결된 네트워크 공유"
                : "저장된 네트워크 위치",
            IconGlyph = "\uE8CE",
            IconImage = _shellIconService.GetStockIcon(ShellStockIcon.NetworkDrive)
        };
    }

    private FileSystemEntry CreateServerShareEntry(ServerShareInfo share)
    {
        return new FileSystemEntry
        {
            Name = share.Name,
            FullPath = share.Path,
            IsDirectory = true,
            SizeBytes = null,
            SizeDisplay = string.Empty,
            ModifiedAt = DateTime.MinValue,
            ModifiedDisplay = string.Empty,
            TypeDisplay = string.IsNullOrWhiteSpace(share.Remark)
                ? "네트워크 공유"
                : $"네트워크 공유 · {share.Remark}",
            IconGlyph = "\uE8CE",
            IconImage = _shellIconService.GetStockIcon(ShellStockIcon.NetworkDrive)
        };
    }

    private static string NormalizeDirectoryPath(string requestedPath) =>
        NetworkPathService.NormalizeDirectoryPath(requestedPath);

    private void SetNavigationBusy(bool busy)
    {
        NavigationProgressBar.Visibility = busy
            ? Visibility.Visible
            : Visibility.Collapsed;
        FileListView.IsEnabled = !busy;
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _history.CanGoBack;
        ForwardButton.IsEnabled = _history.CanGoForward;
        UpButton.IsEnabled = !_isComputerView &&
                             !string.IsNullOrWhiteSpace(_currentPath) &&
                             (NetworkPathService.IsUncServerRoot(_currentPath) ||
                              NetworkPathService.GetNetworkParentPath(_currentPath) is not null ||
                              FileSystemService.GetParentPath(_currentPath) is not null);
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e) =>
        await NavigateBackAsync();

    private async Task NavigateBackAsync()
    {
        var path = _history.Back();
        if (path is not null)
        {
            await NavigateToAsync(path, recordHistory: false);
        }
    }

    private async void ForwardButton_Click(object sender, RoutedEventArgs e) =>
        await NavigateForwardAsync();

    private async Task NavigateForwardAsync()
    {
        var path = _history.Forward();
        if (path is not null)
        {
            await NavigateToAsync(path, recordHistory: false);
        }
    }

    private async void UpButton_Click(object sender, RoutedEventArgs e) =>
        await NavigateUpAsync();

    private async Task NavigateUpAsync()
    {
        if (_currentPath is null)
        {
            return;
        }

        if (NetworkPathService.IsUncServerRoot(_currentPath))
        {
            await NavigateToAsync(ComputerVirtualPath);
            return;
        }

        var parent = NetworkPathService.GetNetworkParentPath(_currentPath) ??
                     FileSystemService.GetParentPath(_currentPath);
        if (parent is not null)
        {
            await NavigateToAsync(parent);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshCurrentFolderAsync();
    }

    private async Task RefreshCurrentFolderAsync()
    {
        BuildNavigationTree();
        if (_isComputerView)
        {
            await ShowComputerViewAsync(recordHistory: false);
            return;
        }

        if (_currentPath is not null)
        {
            await NavigateToAsync(_currentPath, recordHistory: false);
        }
    }

    private async void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await NavigateToAsync(PathTextBox.Text);
        e.Handled = true;
    }

    private async void AddCurrentPathToFavoritesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isComputerView || string.IsNullOrWhiteSpace(_currentPath))
        {
            StatusText.Text = "즐겨찾기에 추가할 폴더 경로가 없습니다.";
            UpdateCurrentPathFavoriteButtonState();
            return;
        }

        await AddFavoritePathAsync(
            _currentPath,
            GetFavoriteDisplayName(_currentPath));
        UpdateCurrentPathFavoriteButtonState();
    }

    private async void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileListView.SelectedItem is FileSystemEntry entry)
        {
            await OpenEntryAsync(entry);
        }
    }

    private async Task OpenEntryAsync(FileSystemEntry entry)
    {
        try
        {
            if (entry.IsDirectory)
            {
                await NavigateToAsync(entry.FullPath);
            }
            else
            {
                _shellService.OpenPath(entry.FullPath);
            }
        }
        catch (Exception exception)
        {
            ShowError("항목을 열지 못했습니다.", exception);
        }
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is FileSystemEntry entry)
        {
            await OpenEntryAsync(entry);
        }
    }

    private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is not FileSystemEntry entry)
        {
            return;
        }

        try
        {
            _shellService.OpenContainingFolder(entry.FullPath);
        }
        catch (Exception exception)
        {
            ShowError("파일 위치를 열지 못했습니다.", exception);
        }
    }

    private void FileListView_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);
        if (item is null)
        {
            FileListView.SelectedItems.Clear();
            return;
        }

        if (!item.IsSelected)
        {
            FileListView.SelectedItems.Clear();
            item.IsSelected = true;
        }

        item.Focus();
    }

    private void FileListContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var entry = FileListView.SelectedItem as FileSystemEntry;
        var canAdd = entry is { IsDirectory: true } &&
                     IsFavoriteCandidatePath(entry.FullPath);
        var alreadyAdded = canAdd && IsFavoritePath(entry!.FullPath);

        AddSelectedFolderToFavoritesMenuItem.Visibility = canAdd
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddSelectedFolderToFavoritesMenuItem.IsEnabled = canAdd && !alreadyAdded;
        AddSelectedFolderToFavoritesMenuItem.Header = alreadyAdded
            ? "이미 즐겨찾기에 있음"
            : "즐겨찾기에 추가";
    }

    private async void AddSelectedFolderToFavoritesMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is not FileSystemEntry
            { IsDirectory: true } entry)
        {
            return;
        }

        await AddFavoritePathAsync(entry.FullPath, entry.Name);
    }

    private void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath is null)
        {
            return;
        }

        var dialog = new TextPromptDialog(
            this,
            "새 폴더",
            "새 폴더의 이름을 입력해 주세요.",
            "새 폴더");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var createdPath = _fileOperationService.CreateFolder(_currentPath, dialog.Value);
            StatusText.Text = $"폴더를 만들었습니다: {Path.GetFileName(createdPath)}";
            _ = RefreshCurrentFolderAsync();
        }
        catch (Exception exception)
        {
            ShowError("폴더를 만들지 못했습니다.", exception);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        SetClipboard(cut: false);
    }

    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        SetClipboard(cut: true);
    }

    private void SetClipboard(bool cut)
    {
        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            return;
        }

        try
        {
            _clipboardPaths = paths.ToList();
            _clipboardIsCut = cut;
            var data = CreateFileDropData(paths, cut);
            Clipboard.SetDataObject(data, copy: true);
            StatusText.Text = cut
                ? $"{paths.Count}개 항목을 이동할 준비가 되었습니다."
                : $"{paths.Count}개 항목을 복사했습니다.";
        }
        catch (Exception exception)
        {
            ShowError("클립보드에 저장하지 못했습니다.", exception);
        }
    }

    private async void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath is null)
        {
            return;
        }

        IReadOnlyList<string> paths;
        var move = false;

        if (_clipboardPaths.Count > 0 &&
            _clipboardPaths.Any(path => File.Exists(path) || Directory.Exists(path)))
        {
            paths = _clipboardPaths
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .ToArray();
            move = _clipboardIsCut;
        }
        else
        {
            try
            {
                if (!Clipboard.ContainsFileDropList())
                {
                    StatusText.Text = "붙여넣을 파일이 없습니다.";
                    return;
                }

                paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
            }
            catch (Exception exception)
            {
                ShowError("클립보드 내용을 읽지 못했습니다.", exception);
                return;
            }
        }

        await TransferPathsAsync(paths, _currentPath, move);

        if (move)
        {
            _clipboardPaths.Clear();
            _clipboardIsCut = false;
        }
    }

    private async Task TransferPathsAsync(
        IReadOnlyList<string> paths,
        string destination,
        bool move)
    {
        _fileOperationCancellation?.Cancel();
        _fileOperationCancellation?.Dispose();
        _fileOperationCancellation = new CancellationTokenSource();
        var token = _fileOperationCancellation.Token;

        var progress = new Progress<FileOperationProgress>(item =>
        {
            StatusText.Text =
                $"{item.Operation} ({item.CurrentItem}/{item.TotalItems})  {item.ItemName}";
        });

        SetNavigationBusy(true);
        try
        {
            await _fileOperationService.CopyOrMoveAsync(
                paths,
                destination,
                move,
                progress,
                token);
            StatusText.Text = move ? "이동을 완료했습니다." : "복사를 완료했습니다.";
            await RefreshCurrentFolderAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "파일 작업을 취소했습니다.";
        }
        catch (Exception exception)
        {
            ShowError(move ? "항목을 이동하지 못했습니다." : "항목을 복사하지 못했습니다.", exception);
        }
        finally
        {
            SetNavigationBusy(false);
        }
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count != 1 ||
            FileListView.SelectedItem is not FileSystemEntry entry)
        {
            StatusText.Text = "이름을 변경할 항목 하나를 선택해 주세요.";
            return;
        }

        var dialog = new TextPromptDialog(
            this,
            "이름 변경",
            "새 이름을 입력해 주세요.",
            entry.Name);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var renamed = _fileOperationService.Rename(entry.FullPath, dialog.Value);
            StatusText.Text = $"이름을 변경했습니다: {Path.GetFileName(renamed)}";
            _ = RefreshCurrentFolderAsync();
        }
        catch (Exception exception)
        {
            ShowError("이름을 변경하지 못했습니다.", exception);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            return;
        }

        var itemText = paths.Count == 1
            ? $"‘{Path.GetFileName(paths[0])}’"
            : $"{paths.Count}개 항목";
        var result = MessageBox.Show(
            this,
            $"{itemText}을(를) 휴지통으로 이동하시겠습니까?",
            "삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        SetNavigationBusy(true);
        try
        {
            await _fileOperationService.DeleteToRecycleBinAsync(
                paths,
                CancellationToken.None);
            StatusText.Text = $"{paths.Count}개 항목을 휴지통으로 이동했습니다.";
            await RefreshCurrentFolderAsync();
        }
        catch (Exception exception)
        {
            ShowError("일부 항목을 삭제하지 못했습니다.", exception);
        }
        finally
        {
            SetNavigationBusy(false);
        }
    }

    private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText.Text = $"{paths.Count}개 경로를 복사했습니다.";
    }

    private void PropertiesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is not FileSystemEntry entry)
        {
            return;
        }

        try
        {
            _shellService.ShowProperties(entry.FullPath, this);
        }
        catch (Exception exception)
        {
            ShowError("속성 창을 열지 못했습니다.", exception);
        }
    }

    private IReadOnlyList<string> GetSelectedFilePaths() =>
        FileListView.SelectedItems
            .Cast<FileSystemEntry>()
            .Select(entry => entry.FullPath)
            .ToArray();

    private static DataObject CreateFileDropData(IReadOnlyList<string> paths, bool cut)
    {
        var collection = new StringCollection();
        collection.AddRange(paths.ToArray());

        var data = new DataObject();
        data.SetFileDropList(collection);
        data.SetData(
            "Preferred DropEffect",
            new MemoryStream([cut ? (byte)2 : (byte)1, 0, 0, 0]));
        return data;
    }

    private void DragSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsScrollBarInteraction(e.OriginalSource))
        {
            _dragStartPoint = new Point(double.NaN, double.NaN);
            return;
        }

        _dragStartPoint = e.GetPosition(this);
    }

    private void FileListView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!HasDragThresholdBeenExceeded(e))
        {
            return;
        }

        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            return;
        }

        var data = CreateFileDropData(paths, cut: false);
        DragDrop.DoDragDrop(FileListView, data, DragDropEffects.Copy | DragDropEffects.Move);
    }

    private void SearchResultsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!HasDragThresholdBeenExceeded(e))
        {
            return;
        }

        var paths = SearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        DragDrop.DoDragDrop(
            SearchResultsListBox,
            CreateFileDropData(paths, cut: false),
            DragDropEffects.Copy);
    }

    private void TitleSearchResultsListBox_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!HasDragThresholdBeenExceeded(e))
        {
            return;
        }

        var paths = TitleSearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        DragDrop.DoDragDrop(
            TitleSearchResultsListBox,
            CreateFileDropData(paths, cut: false),
            DragDropEffects.Copy);
    }

    private bool HasDragThresholdBeenExceeded(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            double.IsNaN(_dragStartPoint.X) ||
            IsScrollBarInteraction(e.OriginalSource))
        {
            return false;
        }

        var current = e.GetPosition(this);
        return Math.Abs(current.X - _dragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
               Math.Abs(current.Y - _dragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private static bool IsScrollBarInteraction(object? originalSource) =>
        originalSource is DependencyObject source &&
        (FindVisualParent<ScrollBar>(source) is not null ||
         FindVisualParent<Thumb>(source) is not null);

    private void FileListView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey)
            ? DragDropEffects.Move
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void FileListView_Drop(object sender, DragEventArgs e)
    {
        if (_currentPath is null ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths ||
            paths.Length == 0)
        {
            return;
        }

        var destination = _currentPath;
        var item = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is FileSystemEntry { IsDirectory: true } entry)
        {
            destination = entry.FullPath;
        }

        var move = e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey);
        await TransferPathsAsync(paths, destination, move);
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private async void AiModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (await InstallAiModelAsync())
        {
            ScheduleBackgroundIndexing();
        }
    }

    private async Task<bool> EnsureRequiredAiModelAsync()
    {
        if (_aiModelManager.IsInstalled)
        {
            UpdateAiModelUi();
            return true;
        }

        var answer = MessageBox.Show(
            this,
            "지능 검색을 시작하려면 로컬 AI 모델의 초기 설치가 필요합니다.\n\n" +
            "• Multilingual E5 의미 모델, SigLIP 2 이미지 모델과 Qwen3 자연어 모델을 준비합니다.\n" +
            "• CPU 실행기까지 약 2GB를 한 번 다운로드합니다.\n" +
            "• 설치 중에는 약 4GB의 여유 공간이 필요합니다.\n" +
            "• SigLIP 2는 DirectML 내장그래픽을 우선 사용하고, 지원되지 않으면 CPU로 전환합니다.\n" +
            "• 설치 후에는 인터넷 없이 사용할 수 있습니다.\n\n" +
            "‘아니요’를 선택하면 AI 탐색기를 종료합니다.",
            "필수 AI 초기 설정",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes)
        {
            RequestApplicationExit();
            return false;
        }

        return await InstallAiModelAsync();
    }

    private async Task<bool> InstallAiModelAsync()
    {
        if (_aiModelManager.IsInstalled &&
            _aiModelManager.IsLanguageModelInstalled)
        {
            UpdateAiModelUi();
            return true;
        }

        _modelInstallCancellation?.Cancel();
        _modelInstallCancellation?.Dispose();
        _modelInstallCancellation = new CancellationTokenSource();
        var token = _modelInstallCancellation.Token;

        _isSearchBusy = true;
        _searchInputIdleTimer.Stop();
        PauseBackgroundIndexing();
        SearchButton.IsEnabled = false;
        AiModelButton.IsEnabled = false;
        AiModelButton.Content = "설치 중";
        StopSearchButton.Content = "설치 중지";
        StopSearchButton.Visibility = Visibility.Visible;
        SearchProgressBar.Visibility = Visibility.Visible;
        SearchProgressBar.IsIndeterminate = true;
        SearchProgressBar.Value = 0;
        SearchResultCountText.Text = string.Empty;
        SearchEngineStatusText.Text = "로컬 AI 설치 정보를 확인하는 중...";

        var progress = new Progress<AiModelInstallProgress>(state =>
        {
            SearchEngineStatusText.Text = state.Description;
            if (state.TotalBytes is > 0)
            {
                var percentage = Math.Clamp(
                    state.DownloadedBytes * 100d / state.TotalBytes.Value,
                    0d,
                    100d);
                SearchProgressBar.IsIndeterminate = false;
                SearchProgressBar.Value = percentage;
                SearchResultCountText.Text = $"{percentage:0}%";
            }
            else
            {
                SearchProgressBar.IsIndeterminate =
                    state.Phase != AiModelInstallPhase.Completed;
                if (state.Phase == AiModelInstallPhase.Completed)
                {
                    SearchProgressBar.Value = 100;
                }

                SearchResultCountText.Text = string.Empty;
            }
        });

        var installed = false;
        try
        {
            _languageSearchService.Stop();
            await _aiModelManager.InstallAsync(progress, token);

            installed = true;
            AppLog.Info("Fixed local AI bundle installed or repaired.");
            StatusText.Text = "고정 로컬 AI 구성을 준비했습니다.";
            SearchResultCountText.Text = "설치 완료";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "AI 모델 설치를 중지했습니다.";
            SearchEngineStatusText.Text =
                "검색을 사용하려면 필수 AI 설치를 다시 시작해 주세요";
        }
        catch (Exception exception)
        {
            ShowError("AI 모델을 설치하지 못했습니다.", exception);
            SearchEngineStatusText.Text =
                "필수 AI 설치 실패 · 설치 다시 시도를 눌러 주세요";
        }
        finally
        {
            _modelInstallCancellation?.Dispose();
            _modelInstallCancellation = null;
            _isSearchBusy = false;
            StopSearchButton.Content = "중지";
            StopSearchButton.Visibility = Visibility.Collapsed;
            SearchProgressBar.Visibility = Visibility.Collapsed;
            SearchButton.IsEnabled = installed;
            UpdateAiModelUi(updateStatusText: installed);
            if (installed && !_isClosing)
            {
                ScheduleBackgroundIndexing(TimeSpan.FromSeconds(8));
            }
        }

        return installed;
    }

    private void UpdateAiModelUi(bool updateStatusText = true)
    {
        var installed = _aiModelManager.IsInstalled;
        var languageReady =
            _aiModelManager.IsLanguageModelInstalled;
        AiModelButton.Content = installed && languageReady
            ? "AI 준비됨"
            : installed
                ? "AI 구성 복구"
                : "설치 다시 시도";
        AiModelButton.IsEnabled =
            (!installed || !languageReady) &&
            _modelInstallCancellation is null;
        SearchButton.IsEnabled =
            installed &&
            _modelInstallCancellation is null;
        AiModelButton.ToolTip = installed && languageReady
            ? $"{AiModelManager.ModelDisplayName} + " +
              $"{AiModelManager.VisualModelDisplayName} + " +
              $"{AiModelManager.LanguageModelDisplayName} 모델이 이 PC에 설치되어 있습니다"
            : installed
                ? "파일 검색은 준비됐습니다. 문장 뜻과 이전 검색 문맥을 이해하는 로컬 자연어 AI를 추가합니다"
            : "검색에 필요한 로컬 AI 모델 설치를 다시 시도합니다";
        if (updateStatusText)
        {
            SearchEngineStatusText.Text = installed && languageReady
                ? "로컬 AI 준비 · 본문·OCR·이미지 검색"
                : installed
                    ? "검색 AI 준비 · 자연어 AI 미설치"
                    : "필수 AI 설정 필요";
        }
    }

    private void ScheduleBackgroundIndexing(TimeSpan? delay = null)
    {
        if (_isClosing ||
            _isBackgroundIndexingPausedByUser ||
            !_aiModelManager.IsInstalled ||
            _isSearchBusy ||
            SearchQueryTextBox.IsKeyboardFocused ||
            _modelInstallCancellation is not null ||
            _storageMigrationCancellation is not null)
        {
            return;
        }

        _embeddingService.SetLowPriority(true);
        _backgroundIndexCancellation?.Cancel();
        _backgroundIndexCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _backgroundIndexCancellation = cancellation;
        _ = RunBackgroundIndexingAsync(
            cancellation,
            delay ?? TimeSpan.FromSeconds(2));
    }

    private async Task RunBackgroundIndexingAsync(
        CancellationTokenSource cancellation,
        TimeSpan delay)
    {
        var token = cancellation.Token;
        var continueBackgroundIndexing = false;
        try
        {
            await Task.Delay(delay, token);
            if (!ReferenceEquals(
                    _backgroundIndexCancellation,
                    cancellation))
            {
                return;
            }

            var favoriteRoots =
                ResolveFavoriteIndexRootsWithoutProbe();
            var roots = BackgroundIndexRootPlanner.OrderRoots(
                    _priorityIndexRoots,
                    favoriteRoots,
                    ResolveAllAvailableRootsWithoutProbe())
                .Where(Directory.Exists)
                .ToArray();
            if (roots.Length == 0)
            {
                IndexStatusText.Text = "색인 위치 없음";
                return;
            }

            // Keep foreground startup inexpensive. File/folder titles are
            // indexed first; document extraction and large AI models are
            // reserved for a long idle window or tray background work.
            var indexingBudget = BackgroundIndexWorkPolicy.GetBudget(
                _isHiddenToTray,
                delay);
            var allowHeavyAiIndexing =
                indexingBudget.AllowHeavyAiIndexing;
            _isBackgroundIndexing = true;
            _embeddingService.SetLowPriority(true);
            IndexStatusText.Text = allowHeavyAiIndexing
                ? favoriteRoots.Count > 0
                    ? $"즐겨찾기 AI 색인 · {favoriteRoots.Count:N0}곳"
                    : "유휴 AI 색인 시작"
                : favoriteRoots.Count > 0
                    ? $"즐겨찾기 제목 색인 · {favoriteRoots.Count:N0}곳"
                    : "빠른 제목 색인 시작";
            IndexStatusText.ToolTip = allowHeavyAiIndexing
                ? "오랫동안 사용하지 않았거나 트레이에 숨긴 동안 본문·AI 색인을 작은 묶음으로 준비합니다."
                : "앱을 사용하는 동안에는 파일명·폴더명 색인만 먼저 준비합니다.";
            _trayIconService.SetStatus(IndexStatusText.Text);
            var progress = new Progress<SearchProgress>(state =>
            {
                if (_isClosing ||
                    token.IsCancellationRequested ||
                    !ReferenceEquals(
                        _backgroundIndexCancellation,
                        cancellation))
                {
                    return;
                }

                IndexStatusText.Text = state.Phase switch
                {
                    SearchPhase.ContentIndexing =>
                        $"본문 색인 {state.MatchedItems:N0}개",
                    SearchPhase.OcrIndexing =>
                        $"이미지·PDF OCR {state.MatchedItems:N0}개",
                    SearchPhase.SemanticIndexing =>
                        $"AI 색인 {state.MatchedItems:N0}개",
                    SearchPhase.VisualIndexing =>
                        $"시각 AI 색인 {state.MatchedItems:N0}개",
                    _ => $"파일 색인 {state.ScannedItems:N0}개"
                };
                IndexStatusText.ToolTip =
                    string.IsNullOrWhiteSpace(state.CurrentPath)
                        ? "시작 자동 색인을 준비하고 있습니다."
                        : state.CurrentPath;
                _trayIconService.SetStatus(IndexStatusText.Text);
            });

            var result = await _searchService.WarmUpAsync(
                roots,
                progress,
                token,
                maximumMetadataItemsPerRoot: 40_000,
                maximumContentDocumentsPerRoot:
                    indexingBudget.MaximumContentDocumentsPerRoot,
                maximumNewSemanticDocumentsPerRoot:
                    indexingBudget.MaximumNewSemanticDocumentsPerRoot,
                maximumNewVisualDocumentsPerRoot:
                    indexingBudget.MaximumNewVisualDocumentsPerRoot);
            continueBackgroundIndexing =
                result.NewlyIndexedVisualDocuments > 0 ||
                result.NewlyIndexedSemanticDocuments > 0;
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(
                    _backgroundIndexCancellation,
                    cancellation))
            {
                return;
            }

            IndexStatusText.Text = "색인 준비됨";
            IndexStatusText.ToolTip =
                $"{result.Roots:N0}개 위치 · " +
                $"{result.IndexedItems:N0}개 파일 · " +
                $"{result.ContentDocuments:N0}개 본문 · " +
                $"{result.SemanticDocuments:N0}개 의미 AI · " +
                $"{result.VisualDocuments:N0}개 시각 AI";
            _trayIconService.SetStatus("색인 준비됨");
            AppLog.Info(
                "Startup background indexing completed: " +
                $"mode={indexingBudget.ModeLabel}, " +
                $"{result.Roots} roots, {result.IndexedItems} metadata, " +
                $"{result.ContentDocuments} content, " +
                $"{result.SemanticDocuments} semantic, " +
                $"{result.VisualDocuments} visual, " +
                $"{favoriteRoots.Count} favorite roots prioritized.");
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing &&
                ReferenceEquals(
                    _backgroundIndexCancellation,
                    cancellation))
            {
                IndexStatusText.Text = "검색 우선 · 색인 일시 정지";
                IndexStatusText.ToolTip =
                    "현재 검색이 끝나면 자동 색인을 이어서 준비합니다.";
                _trayIconService.SetStatus("검색 우선 · 색인 대기");
            }
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "Startup background indexing skipped: " +
                exception.Message);
            if (!_isClosing &&
                ReferenceEquals(
                    _backgroundIndexCancellation,
                    cancellation))
            {
                IndexStatusText.Text = "자동 색인 재시도 대기";
                IndexStatusText.ToolTip = exception.Message;
                _trayIconService.SetStatus("자동 색인 재시도 대기");
            }
        }
        finally
        {
            if (ReferenceEquals(
                    _backgroundIndexCancellation,
                    cancellation))
            {
                _isBackgroundIndexing = false;
                _backgroundIndexCancellation = null;
                cancellation.Dispose();
                if (!_isClosing &&
                    !_isBackgroundIndexingPausedByUser &&
                    !_isSearchBusy &&
                    !SearchQueryTextBox.IsKeyboardFocused)
                {
                    var nextDelay =
                        BackgroundIndexWorkPolicy.GetNextDelay(
                            _isHiddenToTray,
                            continueBackgroundIndexing);
                    ScheduleBackgroundIndexing(nextDelay);
                }
            }
        }
    }

    private void PauseBackgroundIndexing()
    {
        _backgroundIndexCancellation?.Cancel();
        IndexStatusText.Text = "검색 우선 · 색인 일시 정지";
        _trayIconService.SetStatus(IndexStatusText.Text);
    }

    private void PauseBackgroundIndexingForInput()
    {
        _searchInputIdleTimer.Stop();
        if (_backgroundIndexCancellation is
            { IsCancellationRequested: false })
        {
            _backgroundIndexCancellation.Cancel();
        }

        _embeddingService.SetLowPriority(true);
        if (_isBackgroundIndexing)
        {
            IndexStatusText.Text = "입력 우선 · 색인 일시 정지";
            IndexStatusText.ToolTip =
                "검색어를 입력하는 동안 CPU 자동 색인을 잠시 멈춥니다.";
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private async void AdvancedAnalysisButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunAdvancedAnalysisAsync();
    }

    private async Task RunAdvancedAnalysisAsync()
    {
        var query = _lastSearchQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query) || SearchResults.Count == 0)
        {
            StatusText.Text = "먼저 검색을 실행해 주세요.";
            return;
        }

        _isSearchBusy = true;
        _searchInputIdleTimer.Stop();
        _embeddingService.SetLowPriority(false);
        PauseBackgroundIndexing();
        CancelSearchPreviewLoading();
        _searchPreviewAttemptedPaths.Clear();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        var token = cancellation.Token;

        SearchProgressBar.Visibility = Visibility.Visible;
        SearchProgressBar.IsIndeterminate = false;
        SearchProgressBar.Minimum = 0;
        SearchProgressBar.Maximum = 50;
        SearchProgressBar.Value = 0;
        StopSearchButton.Visibility = Visibility.Visible;
        SearchButton.IsEnabled = false;
        AiModelButton.IsEnabled = false;
        AdvancedAnalysisButton.IsEnabled = false;
        AdvancedAnalysisButton.Content = "재정렬 중";
        SearchEngineStatusText.Text = "상위 결과 정밀 분석 중...";

        var progress = new Progress<SearchProgress>(state =>
        {
            SearchProgressBar.Value = state.ScannedItems;
            SearchResultCountText.Text =
                $"{state.ScannedItems:N0}/" +
                $"{Math.Min(50, SearchResults.Count):N0}개 정밀 재평가";
            SearchEngineStatusText.Text = "E5 의미 벡터 비교 중...";
        });

        var completed = false;
        try
        {
            var analysis = await _advancedAnalysisService.AnalyzeAsync(
                query,
                SearchResults.ToArray(),
                progress,
                token);
            token.ThrowIfCancellationRequested();

            ApplyProgressiveSearchResults(analysis.Results);

            SearchResultCountText.Text =
                $"{SearchResults.Count:N0}개 결과 · " +
                $"{analysis.AnalyzedResults:N0}개 정밀 재평가";
            AiResultCountBadgeText.Text = $"{SearchResults.Count:N0}개";
            SearchEngineStatusText.Text =
                $"정밀 재평가 완료 · 상위 {analysis.AnalyzedResults:N0}개를 " +
                $"{analysis.EmbeddingDimensions:N0}차원으로 재정렬";
            SearchEngineStatusText.ToolTip =
                "기본 검색 결과의 상위 항목만 현재 로컬 AI 모델의 전체 차원으로 " +
                "다시 비교했습니다.";
            StatusText.Text = "검색 결과 정밀 재평가을 완료했습니다.";
            AdvancedAnalysisButton.Content = "재정렬 완료";
            completed = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "정밀 재평가을 중지했습니다.";
            SearchEngineStatusText.Text =
                "기본 검색 결과를 그대로 유지했습니다.";
            AdvancedAnalysisButton.Content = "정밀 재평가";
        }
        catch (Exception exception)
        {
            ShowError("정밀 재평가 중 오류가 발생했습니다.", exception);
            SearchEngineStatusText.Text =
                "정밀 재평가에 실패해 기본 검색 결과를 유지했습니다.";
            AdvancedAnalysisButton.Content = "다시 시도";
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                _searchCancellation.Dispose();
                _searchCancellation = null;
            }

            _isSearchBusy = false;
            SearchProgressBar.Visibility = Visibility.Collapsed;
            SearchProgressBar.IsIndeterminate = true;
            StopSearchButton.Visibility = Visibility.Collapsed;
            SearchButton.IsEnabled = true;
            AdvancedAnalysisButton.IsEnabled = !completed;
            UpdateAiModelUi(updateStatusText: false);
            if (!_isClosing)
            {
                StartSearchPreviewLoading();
                ScheduleBackgroundIndexing(TimeSpan.FromSeconds(8));
            }
        }
    }

    private async Task RunSearchAsync()
    {
        var searchStopwatch = Stopwatch.StartNew();
        var query = _isSearchInputPlaceholderActive
            ? string.Empty
            : SearchQueryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchQueryTextBox.Focus();
            StatusText.Text = "검색어를 입력해 주세요.";
            return;
        }

        var requestedRoots = ResolveSearchRoots();
        if (requestedRoots.Count == 0)
        {
            StatusText.Text = "검색할 수 있는 폴더가 없습니다.";
            return;
        }

        var titleSearchRoots = OrderTitleSearchRoots(requestedRoots);
        IReadOnlyList<string> roots = [];
        var hasNetworkRoots = requestedRoots.Any(root =>
            root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase));
        var maximumScannedItems =
            SearchScopeComboBox.SelectedIndex == 3
                ? Math.Min(
                    240_000,
                    Math.Max(100_000, requestedRoots.Count * 40_000))
                : 100_000;
        const int maximumTitleResults = 3_000;
        const int maximumContentDocuments = 10_000;
        var readiness = SearchIndexReadiness.Ready(requestedRoots.Count);

        // The main input always starts a new disk/index search. Refinement is
        // reserved exclusively for the dedicated result-refinement control.
        ResetResultRefinement(clearText: true);
        _isSearchBusy = true;
        _searchInputIdleTimer.Stop();
        _embeddingService.SetLowPriority(false);
        PauseBackgroundIndexing();
        CancelSearchPreviewLoading();
        _searchPreviewAttemptedPaths.Clear();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        var token = cancellation.Token;

        var deterministicIntent =
            SearchQueryInterpreter.Interpret(query);
        var useDeterministicFastPath =
            CanUseDeterministicFastPath(deterministicIntent);
        SearchProgressBar.Visibility = Visibility.Visible;
        SearchProgressBar.IsIndeterminate = true;
        SearchProgressBar.Value = 0;
        StopSearchButton.Visibility = Visibility.Visible;
        SearchButton.IsEnabled = false;
        AiModelButton.IsEnabled = false;
        SearchEngineStatusText.Text =
            useDeterministicFastPath
                ? "이름 조건 확인 완료"
                : "검색 조건 해석 중...";
        StatusText.Text = "검색 조건 해석 중...";
        var interpretation = SearchPlanCompiler.Compile(
            deterministicIntent,
            SearchPlan.FromDeterministic(deterministicIntent),
            languageModelAvailable: false);

        _activeSearchIntent = interpretation.Intent;
        NaturalLanguageInterpretationBar.Visibility =
            Visibility.Visible;
        NaturalLanguageInterpretationText.Text =
            interpretation.DisplaySummary;
        NaturalLanguageInterpretationBar.ToolTip =
            interpretation.Plan.UsedLanguageModel
                ? $"로컬 LLM 해석 · 확신도 " +
                  $"{interpretation.Plan.Confidence:P0}\n" +
                  interpretation.Plan.Interpretation
                : "정확 규칙 해석 · 자연어 모델이 없어도 검색은 계속됩니다.";
        _allIntegratedSearchResults.Clear();
        _allTitleSearchResults.Clear();
        _resultTextFacts.Clear();
        _lastSearchRoots = requestedRoots.ToArray();
        _selectedResultPane = SearchResultPane.Integrated;
        _resultViewChosenByUser = false;
        ResetResultRefinement(clearText: true);
        SearchResults.Clear();
        TitleSearchResults.Clear();
        UpdateSearchResultsLayout();
        _lastSearchQuery = query;
        AdvancedAnalysisBar.Visibility = Visibility.Collapsed;
        AdvancedAnalysisButton.Content = "정밀 재평가";
        AdvancedAnalysisButton.IsEnabled = false;
        SearchPlaceholderPanel.Visibility = Visibility.Visible;
        SearchPlaceholderTitle.Text = "통합 검색 준비 중";
        SearchPlaceholderDescription.Text = "본문·OCR·AI 결과 표시";
        TitleSearchPlaceholderPanel.Visibility = Visibility.Visible;
        TitleSearchPlaceholderTitle.Text = "파일명과 상위 경로를 확인하는 중";
        TitleSearchPlaceholderDescription.Text = "제목 색인에서 즉시 검색";
        SearchProgressBar.Visibility = Visibility.Visible;
        SearchProgressBar.IsIndeterminate = true;
        SearchProgressBar.Value = 0;
        StopSearchButton.Visibility = Visibility.Visible;
        SearchButton.IsEnabled = false;
        AiModelButton.IsEnabled = false;
        SearchResultCountText.Text = "빠른 검색 시작";
        TitleSearchStatusText.Text = "이름·경로 검색 시작...";
        AiResultCountBadgeText.Text = "0개";
        TitleResultCountBadgeText.Text = "0개";
        SearchEngineStatusText.Text = "이름·경로와 내용 검색 시작";
        StatusText.Text = "통합 검색 시작";

        var progressiveResults = new Dictionary<string, SearchResult>(
            StringComparer.OrdinalIgnoreCase);
        var progressiveOrder = new List<string>();
        SearchResponse? latestResponse = null;
        var totalNewResults = 0;
        var titleResults = new Dictionary<string, SearchResult>(
            StringComparer.OrdinalIgnoreCase);
        var titleProgress = new Progress<TitleSearchProgress>(state =>
        {
            if (!ReferenceEquals(_searchCancellation, cancellation) ||
                token.IsCancellationRequested)
            {
                return;
            }

            if (state.NewHits.Count > 0)
            {
                MergeTitleSearchResults(
                    titleResults,
                    state.NewHits,
                    maximumTitleResults);
                TitleSearchPlaceholderPanel.Visibility = Visibility.Collapsed;
            }

            TitleResultCountBadgeText.Text = $"{TitleSearchResults.Count:N0}개";
            if (!_resultViewChosenByUser &&
                SearchResults.Count == 0 &&
                TitleSearchResults.Count > 0)
            {
                _selectedResultPane = SearchResultPane.Title;
            }
            UpdateSearchResultsLayout();
            TitleSearchStatusText.Text = state.IsCompleted
                ? $"이름·경로 검색 완료 · {state.ScannedItems:N0}개 확인"
                : $"이름·경로 검색 중 · {state.ScannedItems:N0}개 확인";
            if (!state.IsCompleted && TitleSearchResults.Count == 0)
            {
                TitleSearchPlaceholderTitle.Text = "파일명과 상위 경로를 확인하는 중";
                TitleSearchPlaceholderDescription.Text =
                    string.IsNullOrWhiteSpace(state.CurrentPath)
                        ? "검색 위치를 여는 중입니다."
                        : state.CurrentPath;
            }
            else if (state.IsCompleted && TitleSearchResults.Count == 0)
            {
                TitleSearchPlaceholderPanel.Visibility = Visibility.Visible;
                TitleSearchPlaceholderTitle.Text = "이름·경로 일치 결과가 없습니다";
                TitleSearchPlaceholderDescription.Text =
                    "검색어를 줄여 보세요.";
            }
        });
        var titleSearchTask = _instantTitleSearchService
            .SearchNaturalLanguageAsync(
                query,
                titleSearchRoots,
                maximumTitleResults,
                titleProgress,
                token,
                _activeSearchIntent);
        var applicationCatalogTask = SearchApplicationCatalogSafelyAsync(
            interpretation.Intent,
            token);

        var progress = new Progress<SearchProgress>(state =>
        {
            if (state.PartialResults is { Count: > 0 } &&
                ReferenceEquals(_searchCancellation, cancellation) &&
                !token.IsCancellationRequested)
            {
                var partialChanged = MergeProgressiveSearchResults(
                    progressiveResults,
                    progressiveOrder,
                    state.PartialResults,
                    500,
                    out var partialAdded);
                totalNewResults += partialAdded;
                if (partialChanged > 0)
                {
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"새로 발견 {totalNewResults:N0}개";
                    StatusText.Text = hasNetworkRoots
                        ? "공유 폴더 검색 중..."
                        : "폴더 검색 중...";
                }
            }

            switch (state.Phase)
            {
                case SearchPhase.Indexing:
                    SearchEngineStatusText.Text = "파일명·경로 색인 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.ScannedItems:N0}개 파일 확인";
                    break;

                case SearchPhase.TargetedScanning:
                {
                    var scanningNetwork = state.CurrentPath.StartsWith(
                        @"\\",
                        StringComparison.OrdinalIgnoreCase);
                    SearchEngineStatusText.Text = scanningNetwork
                        ? "공유 폴더 직접 검색 중..."
                        : "파일명·경로 보완 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.ScannedItems:N0}개 확인";
                    break;
                }

                case SearchPhase.ContentIndexing:
                    SearchEngineStatusText.Text = "문서 내용 검색 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 문서 분석";
                    break;

                case SearchPhase.OcrIndexing:
                    SearchEngineStatusText.Text = "이미지·PDF OCR 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 본문/OCR";
                    break;

                case SearchPhase.ContentSearching:
                    SearchEngineStatusText.Text = "본문 후보 검색 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 본문 후보";
                    break;

                case SearchPhase.SemanticIndexing:
                    SearchEngineStatusText.Text = "E5 의미 색인 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 AI 색인";
                    break;

                case SearchPhase.SemanticSearching:
                    SearchEngineStatusText.Text = "AI 의미 검색 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 AI 후보";
                    break;

                case SearchPhase.VisualIndexing:
                    SearchEngineStatusText.Text = "이미지·PDF 시각 색인 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 시각 색인";
                    break;

                case SearchPhase.VisualSearching:
                    SearchEngineStatusText.Text = "관련 이미지 검색 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.MatchedItems:N0}개 시각 후보";
                    break;

                default:
                    SearchEngineStatusText.Text = "검색 결과 갱신 중...";
                    SearchResultCountText.Text =
                        $"{SearchResults.Count:N0}개 결과 · " +
                        $"{state.ScannedItems:N0}개 분석";
                    break;
            }
        });

        try
        {
            var applicationMatches = await applicationCatalogTask;
            token.ThrowIfCancellationRequested();
            if (applicationMatches.Count > 0)
            {
                var applicationResults = applicationMatches
                    .Select(CreateApplicationSearchResult)
                    .ToArray();
                MergeTitleSearchResultItems(
                    titleResults,
                    applicationResults,
                    maximumTitleResults);
                _ = MergeProgressiveSearchResults(
                    progressiveResults,
                    progressiveOrder,
                    applicationResults,
                    500,
                    out var applicationResultsAdded);
                totalNewResults += applicationResultsAdded;
                TitleSearchStatusText.Text =
                    $"Windows 앱·바로가기 {applicationResults.Length:N0}개 발견";
                SearchEngineStatusText.Text = "Windows 앱 결과 먼저 표시";
            }

            if (useDeterministicFastPath)
            {
                await ObserveTitleSearchTaskAsync(
                    titleSearchTask,
                    token);
                token.ThrowIfCancellationRequested();
                _ = MergeProgressiveSearchResults(
                    progressiveResults,
                    progressiveOrder,
                    TitleSearchResults.ToArray(),
                    500,
                    out _);
                SearchPlaceholderPanel.Visibility = SearchResults.Count > 0
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                if (SearchResults.Count == 0)
                {
                    SearchPlaceholderTitle.Text =
                        "제목 색인 검색 결과가 없습니다";
                    SearchPlaceholderDescription.Text =
                        "검색어나 범위를 바꿔 보세요.";
                }
                SearchEngineStatusText.Text = "제목·Windows 앱 검색 완료";
                StatusText.Text =
                    $"파일·폴더 이름 결과 {TitleSearchResults.Count:N0}개를 찾았습니다.";
                return;
            }

            // Search local cache files before touching a potentially sleeping
            // SMB root. This gives the AI pane an immediate lexical/content
            // result set while reconnect and model work continue afterward.
            latestResponse = await SearchExistingIndexesAsync(
                query,
                requestedRoots,
                maximumScannedItems,
                maximumContentDocuments,
                allowTargetedScan: false,
                includeAiCandidates: false,
                progress: progress,
                cancellationToken: token);
            token.ThrowIfCancellationRequested();
            AppLog.Info(
                $"Search cache-first stage: {searchStopwatch.ElapsedMilliseconds} ms · " +
                $"{latestResponse.Results.Count} candidates.");

            var changed = MergeProgressiveSearchResults(
                progressiveResults,
                progressiveOrder,
                latestResponse.Results,
                500,
                out var added);
            totalNewResults += added;
            if (changed > 0)
            {
                StartSearchPreviewLoading();
                SearchResultCountText.Text =
                    $"{SearchResults.Count:N0}개 빠른 결과";
                SearchEngineStatusText.Text =
                    "저장된 제목·본문 결과를 먼저 표시했습니다";
                StatusText.Text = "기존 결과 표시 · 공유 폴더 확인 중";
            }

            roots = await EnsureSearchRootsAccessibleAsync(
                requestedRoots,
                token);
            token.ThrowIfCancellationRequested();
            if (roots.Count == 0)
            {
                ShowSearchPlaceholder(
                    "AI 분석 위치를 열지 못했습니다",
                    "공유 폴더 연결과 권한을 확인하세요.");
                SearchEngineStatusText.Text = "이름·경로 검색만 계속합니다";
                await ObserveTitleSearchTaskAsync(titleSearchTask, token);
                return;
            }

            _priorityIndexRoots = roots.ToArray();
            hasNetworkRoots = roots.Any(root =>
                root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase));
            readiness = SearchIndexReadiness.Ready(roots.Count);

            SearchResultCountText.Text =
                $"{SearchResults.Count:N0}개 빠른 결과";
            SearchEngineStatusText.Text = "제목 후보 표시 · AI 분석 준비";
            StatusText.Text = SearchResults.Count > 0
                ? $"빠른 후보 {SearchResults.Count:N0}개를 먼저 표시했습니다."
                : "빠른 후보는 없지만 본문과 AI 분석을 계속합니다.";

            if (!_aiModelManager.IsInstalled)
            {
                ShowSearchPlaceholder(
                    "AI 모델이 설치되지 않았습니다",
                    "본문·OCR·의미 검색에는 AI 설치가 필요합니다.");
                SearchEngineStatusText.Text = "이름·경로 검색 사용 가능 · AI 분석은 설치 후 사용";
                await ObserveTitleSearchTaskAsync(titleSearchTask, token);
                return;
            }

            // Search every AI vector that is already on disk before deciding
            // which missing documents to index. Model startup can take time,
            // but lexical results are already usable on screen at this point.
            latestResponse = await SearchExistingIndexesAsync(
                query,
                roots,
                maximumScannedItems,
                maximumContentDocuments,
                allowTargetedScan: false,
                includeAiCandidates: true,
                progress: progress,
                cancellationToken: token);
            token.ThrowIfCancellationRequested();

            changed = MergeProgressiveSearchResults(
                progressiveResults,
                progressiveOrder,
                latestResponse.Results,
                500,
                out added);
            totalNewResults += added;
            if (changed > 0)
            {
                StartSearchPreviewLoading();
            }

            SearchResultCountText.Text =
                $"{SearchResults.Count:N0}개 결과 · 기존 AI 색인 반영";
            SearchEngineStatusText.Text = "저장된 AI 결과 반영 완료";
            AppLog.Info(
                $"Search cached-AI stage: {searchStopwatch.ElapsedMilliseconds} ms · " +
                $"{SearchResults.Count} visible results.");

            try
            {
                readiness = await _searchService.GetIndexReadinessAsync(
                    query,
                    roots,
                    maximumScannedItems,
                    maximumContentDocuments,
                    token,
                    _activeSearchIntent);
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    "검색 색인 상태를 확인하지 못해 점진 분석으로 계속합니다. " +
                    exception.Message);
                readiness = new SearchIndexReadiness(
                    RequiresIndexing: true,
                    VisualSearchRequested: false,
                    SemanticSearchRequested: false,
                    RootCount: roots.Count,
                    MissingMetadataRoots: roots.Count,
                    StaleMetadataRoots: 0,
                    IncompleteContentRoots: roots.Count,
                    IncompleteSemanticRoots: 0,
                    IncompleteVisualRoots: 0,
                    IndexedItems: 0,
                    ContentDocuments: 0,
                    SemanticDocuments: 0,
                    VisualDocuments: 0,
                    VisualFiles: 0,
                    Summary: "색인 상태 확인 실패 · 점진 분석으로 계속");
            }

            if (hasNetworkRoots && !readiness.RequiresIndexing)
            {
                readiness = readiness with
                {
                    RequiresIndexing = true,
                    StaleMetadataRoots = Math.Max(
                        1,
                        readiness.StaleMetadataRoots),
                    Summary = readiness.Summary +
                              " · 공유 폴더 현재 상태를 다시 확인"
                };
            }

            SearchResultCountText.Text =
                $"{SearchResults.Count:N0}개 결과";
            SearchEngineStatusText.Text = readiness.RequiresIndexing
                ? "기존 결과 표시 · 남은 색인 계속"
                : "준비된 색인 검색 완료";
            StatusText.Text = readiness.RequiresIndexing
                ? "남은 파일은 유휴 시간에 분석합니다."
                : "색인 검색 완료";

            AdvancedAnalysisBar.Visibility = SearchResults.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            AdvancedAnalysisButton.IsEnabled = SearchResults.Count > 0;

            if (latestResponse is not null)
            {
                SearchResultCountText.Text =
                    BuildSearchResultCountText(latestResponse.Diagnostics);
                SearchEngineStatusText.ToolTip =
                    $"적용된 검색 기준: {latestResponse.Diagnostics.IntentSummary} · " +
                    $"{roots.Count:N0}개 위치 · " +
                    $"{latestResponse.Diagnostics.IndexedItems:N0}개 메타데이터 · " +
                    $"{latestResponse.Diagnostics.ContentIndexedDocuments:N0}개 본문 · " +
                    $"{latestResponse.Diagnostics.OcrIndexedDocuments:N0}개 OCR · " +
                    $"{latestResponse.Diagnostics.SemanticIndexedDocuments:N0}/" +
                    $"{latestResponse.Diagnostics.SemanticTotalDocuments:N0}개 의미 AI · " +
                    $"{latestResponse.Diagnostics.VisualIndexedDocuments:N0}/" +
                    $"{latestResponse.Diagnostics.VisualTotalDocuments:N0}개 시각 AI";
            }

            if (readiness.RequiresIndexing)
            {
                SearchEngineStatusText.Text =
                    latestResponse is null
                        ? "1차 분석 완료 · 남은 색인 계속"
                        : $"적용 기준: {latestResponse.Diagnostics.IntentSummary}";
                StatusText.Text =
                    $"‘{query}’ 결과 {SearchResults.Count:N0}개를 표시했습니다. " +
                    "남은 파일은 백그라운드에서 이어서 분석합니다.";
            }
            else
            {
                SearchEngineStatusText.Text =
                    latestResponse is null
                        ? "검색·색인 분석 완료"
                        : $"적용 기준: {latestResponse.Diagnostics.IntentSummary}";
                StatusText.Text =
                    $"‘{query}’ 검색을 완료했습니다.";
            }

            await ObserveTitleSearchTaskAsync(titleSearchTask, token);
            AppLog.Info(
                $"Search completed: {searchStopwatch.ElapsedMilliseconds} ms · " +
                $"{SearchResults.Count} integrated · " +
                $"{TitleSearchResults.Count} title results.");

            if (SearchResults.Count == 0)
            {
                ShowSearchPlaceholder(
                    "통합 검색 결과가 아직 없습니다",
                    TitleSearchResults.Count > 0
                        ? "이름·경로 결과를 먼저 확인하세요."
                        : "검색어나 범위를 바꿔 보세요.");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = SearchResults.Count > 0
                ? $"분석을 중지했습니다. 지금까지 찾은 {SearchResults.Count:N0}개 결과는 유지됩니다."
                : "검색을 중지했습니다.";
            SearchEngineStatusText.Text =
                SearchResults.Count > 0
                    ? "점진 분석 중지 · 현재 결과 유지"
                    : "검색이 중지되었습니다.";
            TitleSearchStatusText.Text =
                $"이름·경로 검색 중지 · {TitleSearchResults.Count:N0}개 유지";
            AdvancedAnalysisBar.Visibility = SearchResults.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            AdvancedAnalysisButton.IsEnabled = SearchResults.Count > 0;
            if (SearchResults.Count == 0)
            {
                ShowSearchPlaceholder(
                    "검색을 중지했습니다",
                    "검색어를 바꾸고 다시 시도할 수 있습니다.");
            }
        }
        catch (Exception exception)
        {
            ShowError("검색 중 오류가 발생했습니다.", exception);
            if (SearchResults.Count > 0)
            {
                StatusText.Text =
                    $"일부 분석에 실패했지만 {SearchResults.Count:N0}개 결과는 유지합니다.";
                SearchEngineStatusText.Text =
                    "일부 분석 실패 · 현재 결과 유지";
                AdvancedAnalysisBar.Visibility = Visibility.Visible;
                AdvancedAnalysisButton.IsEnabled = true;
            }
            else
            {
                ShowSearchPlaceholder(
                    "AI 분석을 완료하지 못했습니다",
                    TitleSearchResults.Count > 0
                        ? "이름·경로 결과는 사용할 수 있습니다."
                        : "연결과 폴더 권한을 확인하세요.");
            }

            if (!token.IsCancellationRequested)
            {
                await ObserveTitleSearchTaskAsync(titleSearchTask, token);
            }
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                _searchCancellation.Dispose();
                _searchCancellation = null;
            }

            _isSearchBusy = false;
            SearchProgressBar.Visibility = Visibility.Collapsed;
            StopSearchButton.Visibility = Visibility.Collapsed;
            SearchButton.IsEnabled = true;
            UpdateAiModelUi(updateStatusText: false);
            if (!_isClosing)
            {
                ScheduleBackgroundIndexing(TimeSpan.FromSeconds(
                    useDeterministicFastPath ? 20 : 15));
            }
        }
    }

    private static bool CanUseDeterministicFastPath(SearchIntent intent) =>
        intent.IsExplicitNameLookup &&
        intent.MetadataTerms.Count > 0 &&
        !intent.RequiresContentAttributes;

    private async Task<IReadOnlyList<WindowsApplicationCatalogMatch>>
        SearchApplicationCatalogSafelyAsync(
            SearchIntent intent,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _applicationCatalogService.SearchAsync(
                intent,
                maximumResults: 120,
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "Windows 앱·바로가기 카탈로그를 일부 읽지 못했습니다. " +
                exception.Message);
            return [];
        }
    }

    private Task<SearchResponse> SearchExistingIndexesAsync(
        string query,
        IReadOnlyList<string> roots,
        int maximumScannedItems,
        int maximumContentDocuments,
        bool allowTargetedScan,
        bool includeAiCandidates,
        IProgress<SearchProgress>? progress,
        CancellationToken cancellationToken,
        int maximumTargetedScanItems = 50_000) =>
        _searchService.SearchAsync(
            new SearchRequest(
                query,
                roots,
                MaximumResults: 500,
                MaximumScannedItems: maximumScannedItems,
                MaximumContentDocuments: maximumContentDocuments,
                IndexingMode: SearchIndexingMode.ExistingIndexOnly,
                AllowTargetedScan: allowTargetedScan,
                IncludeAiCandidates: includeAiCandidates,
                MaximumTargetedScanItems: maximumTargetedScanItems,
                Intent: _activeSearchIntent),
            progress,
            cancellationToken);

    private static async Task ObserveTitleSearchTaskAsync(
        Task<TitleSearchSummary> titleSearchTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await titleSearchTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "이름·경로 검색 일부를 완료하지 못했습니다. " + exception.Message);
        }
    }

    private void MergeTitleSearchResults(
        IDictionary<string, SearchResult> mergedResults,
        IReadOnlyList<TitleSearchHit> incomingHits,
        int maximumResults)
    {
        MergeTitleSearchResultItems(
            mergedResults,
            incomingHits.Select(CreateTitleSearchResult).ToArray(),
            maximumResults);
    }

    private void MergeTitleSearchResultItems(
        IDictionary<string, SearchResult> mergedResults,
        IReadOnlyList<SearchResult> incomingResults,
        int maximumResults)
    {
        foreach (var result in incomingResults)
        {
            if (mergedResults.ContainsKey(result.FullPath))
            {
                continue;
            }

            mergedResults[result.FullPath] = result;
        }

        var desiredResults = mergedResults.Values
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.MatchPercent)
            .ThenBy(result => result.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(maximumResults)
            .ToArray();
        var retained = desiredResults
            .Select(result => result.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removedPath in mergedResults.Keys
                     .Where(path => !retained.Contains(path))
                     .ToArray())
        {
            mergedResults.Remove(removedPath);
        }

        ApplyTitleSearchResults(desiredResults);
    }

    private SearchResult CreateApplicationSearchResult(
        WindowsApplicationCatalogMatch match)
    {
        var entry = match.Entry;
        var extension = entry.IsDirectory
            ? string.Empty
            : Path.GetExtension(entry.FullPath);
        return new SearchResult
        {
            Name = entry.Name,
            FullPath = entry.FullPath,
            DirectoryPath = Path.GetDirectoryName(entry.FullPath) ??
                            entry.FullPath,
            TypeDisplay = entry.IsDirectory
                ? "프로그램 폴더"
                : extension.Equals(
                    ".lnk",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Windows 바로가기"
                    : FileTypeCatalog.GetTypeDisplay(extension),
            ModifiedDisplay = entry.ModifiedUtc == default
                ? "수정 날짜 확인 불가"
                : entry.ModifiedUtc.ToLocalTime().ToString(
                    "yyyy-MM-dd HH:mm"),
            CreatedUtc = default,
            ModifiedUtc = entry.ModifiedUtc,
            Reason = match.Reason,
            IconGlyph = entry.IsDirectory ? "\uE8B7" : "\uE7C3",
            IconImage = _shellIconService.GetFileSystemIcon(
                entry.FullPath,
                entry.IsDirectory),
            Score = match.Score,
            MatchPercent = match.MatchPercent,
            WasAiAnalyzed = false,
            WasVisualAnalyzed = false,
            WasAdvancedAnalyzed = false,
            EvidenceKind = SearchEvidenceKind.Application,
            IsDirectory = entry.IsDirectory
        };
    }

    private SearchResult CreateTitleSearchResult(TitleSearchHit hit)
    {
        var extension = hit.IsDirectory
            ? string.Empty
            : Path.GetExtension(hit.Name);
        return new SearchResult
        {
            Name = hit.Name,
            FullPath = hit.FullPath,
            DirectoryPath = Path.GetDirectoryName(hit.FullPath) ?? hit.FullPath,
            TypeDisplay = hit.IsDirectory
                ? "파일 폴더"
                : FileTypeCatalog.GetTypeDisplay(extension),
            ModifiedDisplay = hit.ModifiedLocal is null
                ? "수정 날짜 확인 불가"
                : hit.ModifiedLocal.Value.ToString("yyyy-MM-dd HH:mm"),
            CreatedUtc = hit.CreatedLocal?.ToUniversalTime() ?? default,
            ModifiedUtc = hit.ModifiedLocal?.ToUniversalTime() ?? default,
            Reason = hit.Reason,
            IconGlyph = hit.IsDirectory ? "\uE8B7" : "\uE7C3",
            IconImage = _shellIconService.GetFileSystemIcon(
                hit.IsDirectory ? "folder" : $"file{extension}",
                hit.IsDirectory),
            Score = hit.Score,
            MatchPercent = hit.MatchPercent,
            WasAiAnalyzed = false,
            WasVisualAnalyzed = false,
            WasAdvancedAnalyzed = false,
            EvidenceKind = hit.IsExactMatch
                ? SearchEvidenceKind.ExactName
                : SearchEvidenceKind.NameCandidate,
            IsDirectory = hit.IsDirectory
        };
    }

    private void ApplyTitleSearchResults(
        IReadOnlyList<SearchResult> desiredResults)
    {
        _allTitleSearchResults.Clear();
        _allTitleSearchResults.AddRange(desiredResults);
        var refined = ResultRefinementService.Refine(
            _activeResultRefinementQuery,
            _allTitleSearchResults,
            _resultTextFacts);
        UpdateTitleSearchResults(SearchResultSortService.Sort(
            refined.Results,
            _searchResultSortMode));
        UpdateResultRefinementBar();
    }

    private void UpdateTitleSearchResults(
        IReadOnlyList<SearchResult> desiredResults)
    {
        var selectedPaths = TitleSearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < desiredResults.Count; index++)
        {
            var desired = desiredResults[index];
            var currentIndex = TitleSearchResults
                .Select((result, resultIndex) => new { result, resultIndex })
                .FirstOrDefault(item => string.Equals(
                    item.result.FullPath,
                    desired.FullPath,
                    StringComparison.OrdinalIgnoreCase))
                ?.resultIndex ?? -1;
            if (currentIndex < 0)
            {
                TitleSearchResults.Insert(index, desired);
            }
            else if (currentIndex != index)
            {
                TitleSearchResults.Move(currentIndex, index);
            }
        }

        while (TitleSearchResults.Count > desiredResults.Count)
        {
            TitleSearchResults.RemoveAt(TitleSearchResults.Count - 1);
        }

        TitleResultCountBadgeText.Text = $"{TitleSearchResults.Count:N0}개";
        if (!_resultViewChosenByUser &&
            SearchResults.Count == 0 &&
            TitleSearchResults.Count > 0)
        {
            _selectedResultPane = SearchResultPane.Title;
        }
        UpdateSearchResultsLayout();
        TitleSearchPlaceholderPanel.Visibility = TitleSearchResults.Count > 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (selectedPaths.Count == 0)
        {
            return;
        }

        TitleSearchResultsListBox.SelectedItems.Clear();
        foreach (var result in TitleSearchResults)
        {
            if (selectedPaths.Contains(result.FullPath))
            {
                TitleSearchResultsListBox.SelectedItems.Add(result);
            }
        }
    }

    private int MergeProgressiveSearchResults(
        IDictionary<string, SearchResult> mergedResults,
        IList<string> orderedPaths,
        IReadOnlyList<SearchResult> incomingResults,
        int maximumResults,
        out int addedResults)
    {
        addedResults = 0;
        var changedResults = 0;
        var previousRanks = orderedPaths
            .Select((path, index) => new { path, index })
            .ToDictionary(
                item => item.path,
                item => item.index,
                StringComparer.OrdinalIgnoreCase);
        var incomingRanks = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var preferVisual = IsVisualSearchQuery(_lastSearchQuery);
        var hasCustomRanking =
            !string.IsNullOrWhiteSpace(_lastSearchQuery) &&
            SearchQueryInterpreter.Interpret(_lastSearchQuery)
                .RankingProfile
                .HasPreferences;

        for (var index = 0; index < incomingResults.Count; index++)
        {
            var incoming = incomingResults[index];
            if (!incomingRanks.TryAdd(incoming.FullPath, index))
            {
                continue;
            }

            if (!mergedResults.TryGetValue(incoming.FullPath, out var current))
            {
                mergedResults[incoming.FullPath] = incoming;
                addedResults++;
                changedResults++;
            }
            else if (ShouldReplaceProgressiveResult(
                         current,
                         incoming,
                         preferVisual))
            {
                incoming.PreviewImage ??= current.PreviewImage;
                mergedResults[incoming.FullPath] = incoming;
                changedResults++;
            }
        }

        var nextOrder = mergedResults.Values
            .OrderByDescending(result =>
                hasCustomRanking
                    ? 0
                    : GetProgressiveEvidencePriority(
                        result.EvidenceKind,
                        preferVisual))
            .ThenByDescending(result => result.Score)
            .ThenByDescending(result =>
                hasCustomRanking
                    ? GetProgressiveEvidencePriority(
                        result.EvidenceKind,
                        preferVisual)
                    : 0)
            .ThenByDescending(result => result.MatchPercent)
            .ThenBy(result =>
                incomingRanks.TryGetValue(result.FullPath, out var rank)
                    ? rank
                    : int.MaxValue)
            .ThenBy(result =>
                previousRanks.TryGetValue(result.FullPath, out var rank)
                    ? rank
                    : int.MaxValue)
            .ThenBy(
                result => result.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .Take(maximumResults)
            .Select(result => result.FullPath)
            .ToArray();

        var retained = new HashSet<string>(
            nextOrder,
            StringComparer.OrdinalIgnoreCase);
        foreach (var removedPath in mergedResults.Keys
                     .Where(path => !retained.Contains(path))
                     .ToArray())
        {
            mergedResults.Remove(removedPath);
        }

        orderedPaths.Clear();
        foreach (var path in nextOrder)
        {
            orderedPaths.Add(path);
        }

        ApplyProgressiveSearchResults(
            nextOrder
                .Select(path => mergedResults[path])
                .ToArray());
        return changedResults;
    }

    private void ApplyProgressiveSearchResults(
        IReadOnlyList<SearchResult> desiredResults)
    {
        _allIntegratedSearchResults.Clear();
        _allIntegratedSearchResults.AddRange(desiredResults);
        var refined = ResultRefinementService.Refine(
            _activeResultRefinementQuery,
            _allIntegratedSearchResults,
            _resultTextFacts);
        UpdateProgressiveSearchResults(SearchResultSortService.Sort(
            refined.Results,
            _searchResultSortMode));
        UpdateResultRefinementBar();
    }

    private void UpdateProgressiveSearchResults(
        IReadOnlyList<SearchResult> desiredResults)
    {
        var selectedPaths = SearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < desiredResults.Count; index++)
        {
            var desired = desiredResults[index];
            if (index < SearchResults.Count &&
                string.Equals(
                    SearchResults[index].FullPath,
                    desired.FullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!ReferenceEquals(SearchResults[index], desired))
                {
                    SearchResults[index] = desired;
                }

                continue;
            }

            var existingIndex = -1;
            for (var candidateIndex = index + 1;
                 candidateIndex < SearchResults.Count;
                 candidateIndex++)
            {
                if (string.Equals(
                        SearchResults[candidateIndex].FullPath,
                        desired.FullPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = candidateIndex;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                SearchResults.Move(existingIndex, index);
                SearchResults[index] = desired;
            }
            else
            {
                SearchResults.Insert(index, desired);
            }
        }

        while (SearchResults.Count > desiredResults.Count)
        {
            SearchResults.RemoveAt(SearchResults.Count - 1);
        }

        AiResultCountBadgeText.Text = $"{SearchResults.Count:N0}개";
        if (!_resultViewChosenByUser &&
            TitleSearchResults.Count == 0 &&
            SearchResults.Count > 0)
        {
            _selectedResultPane = SearchResultPane.Integrated;
        }
        UpdateSearchResultsLayout();
        SearchPlaceholderPanel.Visibility = SearchResults.Count > 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (selectedPaths.Count == 0)
        {
            return;
        }

        SearchResultsListBox.SelectedItems.Clear();
        foreach (var result in SearchResults)
        {
            if (selectedPaths.Contains(result.FullPath))
            {
                SearchResultsListBox.SelectedItems.Add(result);
            }
        }
    }

    private async void RefineResultsButton_Click(
        object sender,
        RoutedEventArgs e) =>
        await ApplyResultRefinementAsync(
            ResultRefineTextBox.Text);

    private void ClearResultRefinementButton_Click(
        object sender,
        RoutedEventArgs e) =>
        ResetResultRefinement(clearText: true);

    private async void ResultRefineTextBox_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ApplyResultRefinementAsync(
                ResultRefineTextBox.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ResetResultRefinement(clearText: true);
            e.Handled = true;
        }
    }

    private async Task ApplyResultRefinementAsync(
        string query,
        SearchIntent? providedIntent = null)
    {
        var requestedQuery = query.Trim();
        if (requestedQuery.Length == 0)
        {
            ResetResultRefinement(clearText: false);
            return;
        }

        SearchIntent intent;
        if (providedIntent is not null)
        {
            intent = providedIntent;
        }
        else if (_languageSearchService.IsAvailable)
        {
            _resultRefinementCancellation?.Cancel();
            _resultRefinementCancellation?.Dispose();
            var languageCancellation =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(45));
            _resultRefinementCancellation =
                languageCancellation;
            RefineResultsButton.IsEnabled = false;
            RefineResultsButton.Content = "뜻 확인 중";
            ResultRefineProgressBar.Visibility =
                Visibility.Visible;
            ResultRefineProgressBar.IsIndeterminate = true;
            ResultRefineStatusText.Text =
                "로컬 LLM이 현재 결과에 적용할 조건을 해석하는 중...";
            try
            {
                _embeddingService.Stop();
                var interpretation =
                    await _languageSearchService.InterpretAsync(
                        requestedQuery,
                        new SearchConversationContext(
                            _lastSearchQuery ??
                            requestedQuery,
                            Math.Max(
                                _allIntegratedSearchResults.Count,
                                _allTitleSearchResults.Count)),
                        languageCancellation.Token);
                intent = interpretation.Intent;
                NaturalLanguageInterpretationBar.Visibility =
                    Visibility.Visible;
                NaturalLanguageInterpretationText.Text =
                    interpretation.DisplaySummary;
                NaturalLanguageInterpretationBar.ToolTip =
                    $"로컬 LLM 해석 · 확신도 " +
                    $"{interpretation.Plan.Confidence:P0}\n" +
                    interpretation.Plan.Interpretation;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (ReferenceEquals(
                        _resultRefinementCancellation,
                        languageCancellation))
                {
                    _resultRefinementCancellation = null;
                }

                languageCancellation.Dispose();
                RefineResultsButton.IsEnabled = true;
                RefineResultsButton.Content = "찾기";
                ResultRefineProgressBar.Visibility =
                    Visibility.Collapsed;
            }
        }
        else
        {
            intent = SearchQueryInterpreter.Interpret(
                requestedQuery);
        }
        if (intent.RequiresContentAttributes)
        {
            _resultRefinementCancellation?.Cancel();
            _resultRefinementCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _resultRefinementCancellation = cancellation;
            var token = cancellation.Token;
            _isResultRefinementAnalyzing = true;
            RefineResultsButton.IsEnabled = false;
            RefineResultsButton.Content = "확인 중";
            ResultRefineProgressBar.Visibility = Visibility.Visible;
            ResultRefineProgressBar.IsIndeterminate = true;
            ResultRefineInterpretationText.Visibility =
                Visibility.Visible;
            ResultRefineInterpretationText.Text =
                $"해석: {intent.Summary}";
            ResultRefineStatusText.Text = "현재 결과 내용 확인 중...";
            var candidatePaths = _allIntegratedSearchResults
                .Concat(_allTitleSearchResults)
                .Select(result => result.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var progress = new Progress<ResultTextFactsProgress>(
                state =>
                {
                    ResultRefineProgressBar.IsIndeterminate = false;
                    ResultRefineProgressBar.Minimum = 0;
                    ResultRefineProgressBar.Maximum =
                        Math.Max(1, state.TotalDocuments);
                    ResultRefineProgressBar.Value =
                        state.AnalyzedDocuments;
                    ResultRefineStatusText.Text =
                        $"현재 후보 내용 확인 중 · " +
                        $"{state.AnalyzedDocuments:N0}/" +
                        $"{state.TotalDocuments:N0}개";
                });
            try
            {
                var facts = await _searchService
                    .GetResultTextFactsAsync(
                        _lastSearchRoots,
                        candidatePaths,
                        maximumOnDemandDocuments: 400,
                        progress,
                        token);
                token.ThrowIfCancellationRequested();
                _resultTextFacts.Clear();
                foreach (var item in facts)
                {
                    _resultTextFacts[item.Key] = item.Value;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    "현재 결과의 내용을 일부 확인하지 못했습니다. " +
                    exception.Message);
                StatusText.Text =
                    "확인 가능한 파일명과 저장된 내용으로 결과를 좁혔습니다.";
            }
            finally
            {
                if (ReferenceEquals(
                        _resultRefinementCancellation,
                        cancellation))
                {
                    _isResultRefinementAnalyzing = false;
                    RefineResultsButton.IsEnabled = true;
                    RefineResultsButton.Content = "찾기";
                    ResultRefineProgressBar.Visibility =
                        Visibility.Collapsed;
                }
            }
        }

        _activeResultRefinementQuery = requestedQuery;
        _activeResultRefinementIntent = intent;
        RefreshResultRefinementViews();
        ResultRefineTextBox.SelectAll();
        ResultRefineTextBox.Focus();
    }

    private void ResetResultRefinement(bool clearText)
    {
        _resultRefinementCancellation?.Cancel();
        _resultRefinementCancellation?.Dispose();
        _resultRefinementCancellation = null;
        _isResultRefinementAnalyzing = false;
        _activeResultRefinementQuery = string.Empty;
        _activeResultRefinementIntent = null;
        if (clearText && ResultRefineTextBox is not null)
        {
            ResultRefineTextBox.Clear();
        }

        if (SearchResultsListBox is not null)
        {
            RefineResultsButton.IsEnabled = true;
            RefineResultsButton.Content = "찾기";
            ResultRefineProgressBar.Visibility =
                Visibility.Collapsed;
            ResultRefineInterpretationText.Visibility =
                Visibility.Collapsed;
            RefreshResultRefinementViews();
        }
    }

    private void RefreshResultRefinementViews()
    {
        var integrated = _activeResultRefinementIntent is null
            ? ResultRefinementService.Refine(
                _activeResultRefinementQuery,
                _allIntegratedSearchResults,
                _resultTextFacts)
            : ResultRefinementService.Refine(
                _activeResultRefinementIntent,
                _allIntegratedSearchResults,
                _resultTextFacts);
        var titles = _activeResultRefinementIntent is null
            ? ResultRefinementService.Refine(
                _activeResultRefinementQuery,
                _allTitleSearchResults,
                _resultTextFacts)
            : ResultRefinementService.Refine(
                _activeResultRefinementIntent,
                _allTitleSearchResults,
                _resultTextFacts);
        UpdateProgressiveSearchResults(SearchResultSortService.Sort(
            integrated.Results,
            _searchResultSortMode));
        UpdateTitleSearchResults(SearchResultSortService.Sort(
            titles.Results,
            _searchResultSortMode));
        UpdateResultRefinementBar(integrated, titles);

        if (_activeResultRefinementQuery.Length == 0)
        {
            return;
        }

        if (_allIntegratedSearchResults.Count > 0 &&
            integrated.ResultCount == 0)
        {
            SearchPlaceholderTitle.Text = integrated.UnknownCount > 0
                ? "내용을 확인하지 못한 항목이 있습니다"
                : "현재 결과에서 일치 항목이 없습니다";
            SearchPlaceholderDescription.Text =
                integrated.UnknownCount > 0
                    ? $"내용 미확인 {integrated.UnknownCount:N0}개"
                    : "조건을 줄이거나 전체 결과로 돌아가세요.";
        }
        if (_allTitleSearchResults.Count > 0 &&
            titles.ResultCount == 0)
        {
            TitleSearchPlaceholderTitle.Text = titles.UnknownCount > 0
                ? "내용을 확인하지 못한 항목이 있습니다"
                : "현재 결과에서 일치 항목이 없습니다";
            TitleSearchPlaceholderDescription.Text =
                titles.UnknownCount > 0
                    ? $"내용 미확인 {titles.UnknownCount:N0}개"
                    : "조건을 줄이거나 전체 결과로 돌아가세요.";
        }
    }

    private void UpdateResultRefinementBar()
    {
        var integrated = ResultRefinementService.Refine(
            _activeResultRefinementQuery,
            _allIntegratedSearchResults,
            _resultTextFacts);
        var titles = ResultRefinementService.Refine(
            _activeResultRefinementQuery,
            _allTitleSearchResults,
            _resultTextFacts);
        UpdateResultRefinementBar(integrated, titles);
    }

    private void UpdateResultRefinementBar(
        ResultRefinementResult integrated,
        ResultRefinementResult titles)
    {
        var sourceCount = integrated.SourceCount + titles.SourceCount;
        var resultCount = integrated.ResultCount + titles.ResultCount;
        ResultRefineBar.Visibility =
            sourceCount > 0 || _activeResultRefinementQuery.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        ClearResultRefinementButton.IsEnabled =
            _activeResultRefinementQuery.Length > 0;
        if (_activeResultRefinementQuery.Length > 0)
        {
            var interpretation = integrated.Interpretation.Length > 0
                ? integrated.Interpretation
                : titles.Interpretation;
            ResultRefineInterpretationText.Visibility =
                Visibility.Visible;
            ResultRefineInterpretationText.Text =
                $"해석: {interpretation}";
        }
        else if (!_isResultRefinementAnalyzing)
        {
            ResultRefineInterpretationText.Visibility =
                Visibility.Collapsed;
        }
        var unknownCount =
            integrated.UnknownCount + titles.UnknownCount;
        ResultRefineStatusText.Text =
            _activeResultRefinementQuery.Length == 0
                ? $"현재 결과 {sourceCount:N0}개에서 재검색"
                : unknownCount > 0
                    ? $"전체 {sourceCount:N0}개 중 {resultCount:N0}개 · " +
                      $"내용 미확인 {unknownCount:N0}개"
                    : $"두 결과 탭 전체 {sourceCount:N0}개 중 {resultCount:N0}개";
        ResultRefineStatusText.ToolTip =
            _activeResultRefinementQuery.Length == 0
                ? "새 디스크 검색 없이 현재 표시된 검색 결과만 좁힙니다."
                : $"적용 조건: {_activeResultRefinementQuery}";
    }

    private static bool ShouldReplaceProgressiveResult(
        SearchResult current,
        SearchResult incoming,
        bool preferVisual)
    {
        var currentPriority = GetProgressiveEvidencePriority(
            current.EvidenceKind,
            preferVisual);
        var incomingPriority = GetProgressiveEvidencePriority(
            incoming.EvidenceKind,
            preferVisual);
        if (incomingPriority != currentPriority)
        {
            return incomingPriority > currentPriority;
        }

        if (incoming.WasAdvancedAnalyzed != current.WasAdvancedAnalyzed)
        {
            return incoming.WasAdvancedAnalyzed;
        }

        if (incoming.WasVisualAnalyzed != current.WasVisualAnalyzed)
        {
            return incoming.WasVisualAnalyzed;
        }

        if (incoming.WasAiAnalyzed != current.WasAiAnalyzed)
        {
            return incoming.WasAiAnalyzed;
        }

        return incoming.Score >= current.Score ||
               incoming.MatchPercent >= current.MatchPercent;
    }

    private static int GetProgressiveEvidencePriority(
        SearchEvidenceKind evidenceKind,
        bool preferVisual) =>
        evidenceKind switch
        {
            SearchEvidenceKind.ExactName => 800,
            SearchEvidenceKind.Application => 790,
            SearchEvidenceKind.Combined => 760,
            SearchEvidenceKind.VisualCandidate when preferVisual => 740,
            SearchEvidenceKind.NameCandidate => 700,
            SearchEvidenceKind.Content => 620,
            SearchEvidenceKind.Path => 540,
            SearchEvidenceKind.SemanticCandidate => 450,
            SearchEvidenceKind.VisualCandidate => 320,
            _ => 100
        };

    private static bool IsVisualSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var intent = SearchQueryInterpreter.Interpret(query);
        return intent.Categories.Contains(FileCategory.Image) ||
               intent.RequestedExtensions.Any(extension =>
                   FileTypeCatalog.GetCategory(extension) ==
                   FileCategory.Image ||
                   extension.Equals(
                       ".pdf",
                       StringComparison.OrdinalIgnoreCase)) ||
               VisualQueryPromptBuilder.HasKnownVisualConcept(query);
    }

    private static string BuildProgressiveSearchStatus(
        SearchIndexReadiness readiness,
        IndexWarmupResult warmup,
        int passNumber)
    {
        var stage = readiness.VisualSearchRequested
            ? "본문·OCR·이미지 AI"
            : readiness.SemanticSearchRequested
                ? "본문·다국어 의미 AI"
                : "파일명·문서 본문";
        return
            $"{stage} 점진 분석 {passNumber:N0}단계 · " +
            $"본문 {warmup.ContentDocuments:N0}개 · " +
            $"의미 AI {warmup.SemanticDocuments:N0}개 · " +
            $"시각 AI {warmup.VisualDocuments:N0}개";
    }


    private string BuildSearchResultCountText(SearchDiagnostics diagnostics)
    {
        var text = $"{SearchResults.Count:N0}개 결과";
        if (diagnostics.UsedVisualSearch)
        {
            text += diagnostics.VisualIndexedDocuments <
                    diagnostics.VisualTotalDocuments
                ? $" · 시각 AI {diagnostics.VisualIndexedDocuments:N0}/" +
                  $"{diagnostics.VisualTotalDocuments:N0}"
                : " · OCR·시각 AI";
        }
        else if (diagnostics.UsedSemanticSearch)
        {
            text += diagnostics.SemanticIndexedDocuments <
                    diagnostics.SemanticTotalDocuments
                ? $" · AI 색인 {diagnostics.SemanticIndexedDocuments:N0}/" +
                  $"{diagnostics.SemanticTotalDocuments:N0}"
                : " · 로컬 AI";
        }
        else if (diagnostics.UsedContentSearch)
        {
            text += " · 본문 검색";
        }
        else if (diagnostics.UsedTargetedScan)
        {
            text += " · 파일명 정밀 재탐색";
        }
        else if (diagnostics.IndexWasTruncated)
        {
            text += " · 색인 상한";
        }

        return text;
    }

    private void StartSearchPreviewLoading()
    {
        if (_searchPreviewCancellation is not null ||
            _searchPreviewAttemptedPaths.Count >=
            MaximumSearchPreviewResults)
        {
            return;
        }

        var remainingBudget =
            MaximumSearchPreviewResults -
            _searchPreviewAttemptedPaths.Count;
        var targets = SearchResults
            .Where(result =>
                !result.IsDirectory &&
                !result.HasPreview &&
                !_searchPreviewAttemptedPaths.Contains(result.FullPath) &&
                _imagePreviewService.CanPreview(result.FullPath))
            .Take(remainingBudget)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        foreach (var result in targets)
        {
            _searchPreviewAttemptedPaths.Add(result.FullPath);
        }

        var cancellation = new CancellationTokenSource();
        _searchPreviewCancellation = cancellation;
        _ = LoadSearchPreviewsAsync(targets, cancellation);
    }

    private async Task LoadSearchPreviewsAsync(
        IReadOnlyList<SearchResult> targets,
        CancellationTokenSource cancellation)
    {
        try
        {
            foreach (var result in targets)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var preview = await _imagePreviewService.LoadAsync(
                    result.FullPath,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (preview is not null)
                {
                    result.PreviewImage = preview;
                }

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
        catch (OperationCanceledException)
        {
            // A new search owns the result list now.
        }
        finally
        {
            if (ReferenceEquals(
                    _searchPreviewCancellation,
                    cancellation))
            {
                _searchPreviewCancellation = null;
                if (!_isClosing &&
                    _searchPreviewAttemptedPaths.Count <
                    MaximumSearchPreviewResults)
                {
                    // Preview loading intentionally continues as a background
                    // dispatcher operation. Explicitly discard the operation
                    // so the compiler does not report an unawaited call.
                    _ = Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(StartSearchPreviewLoading));
                }
            }

            cancellation.Dispose();
        }
    }

    private void CancelSearchPreviewLoading()
    {
        var cancellation = _searchPreviewCancellation;
        _searchPreviewCancellation = null;
        cancellation?.Cancel();
    }

    private IReadOnlyList<string> OrderTitleSearchRoots(
        IReadOnlyList<string> roots)
    {
        // Do not call Directory.Exists here. On an unavailable or sleeping SMB
        // share that synchronous probe can freeze the UI before title search
        // has even been scheduled. The dedicated title worker handles access
        // failures without blocking the search button.
        var ordered = new List<string>();
        if (!string.IsNullOrWhiteSpace(_currentPath))
        {
            ordered.Add(_currentPath);
        }

        ordered.AddRange(roots);
        return ordered
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(root => string.Equals(
                root,
                _currentPath,
                StringComparison.OrdinalIgnoreCase)
                ? 0
                : root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 2)
            .ToArray();
    }

    private IReadOnlyList<string> ResolveSearchRoots()
    {
        // Search-button latency matters more than proving every root here.
        // Return syntactically valid candidates immediately; the AI side
        // performs bounded asynchronous access checks later, while the title
        // worker starts scanning at once.
        if (SearchScopeComboBox.SelectedIndex == 3 || _isComputerView)
        {
            return ResolveAllAvailableRootsWithoutProbe();
        }

        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            return [];
        }

        switch (SearchScopeComboBox.SelectedIndex)
        {
            case 1:
            {
                string? root = null;
                try
                {
                    root = Path.GetPathRoot(_currentPath);
                }
                catch
                {
                    // Keep the current location when Windows cannot parse it.
                }

                return !string.IsNullOrWhiteSpace(root)
                    ? [root]
                    : [_currentPath];
            }

            case 2:
                return new[] { _currentPath }
                    .Concat(ResolveConfiguredNetworkRootsWithoutProbe())
                    .Where(IsSyntacticallyValidSearchRoot)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            default:
                return [_currentPath];
        }
    }

    private IReadOnlyList<string> ResolveAllAvailableRootsWithoutProbe() =>
        GetSearchDriveRootsWithoutProbe()
            .Concat(ResolveConfiguredNetworkRootsWithoutProbe())
            .Concat(_settings.Favorites.Select(item => item.Path))
            .Where(IsSyntacticallyValidSearchRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IReadOnlyList<string> ResolveFavoriteIndexRootsWithoutProbe() =>
        _settings.Favorites
            .Select(item => item.Path)
            .Where(IsSyntacticallyValidSearchRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> GetSearchDriveRootsWithoutProbe()
    {
        var roots = new List<string>();
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch
        {
            return roots;
        }

        foreach (var drive in drives)
        {
            DriveType driveType;
            try
            {
                driveType = drive.DriveType;
            }
            catch
            {
                continue;
            }

            if (driveType is DriveType.CDRom or DriveType.Network)
            {
                continue;
            }

            roots.Add(drive.RootDirectory.FullName);
        }

        return roots;
    }

    private IReadOnlyList<string> ResolveConfiguredNetworkRootsWithoutProbe()
    {
        var roots = new List<string>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? requestedPath)
        {
            if (string.IsNullOrWhiteSpace(requestedPath) ||
                !NetworkPathService.IsPotentialNetworkPath(requestedPath))
            {
                return;
            }

            string normalized;
            try
            {
                normalized = NetworkPathService.NormalizeNetworkLocationPath(
                    requestedPath);
            }
            catch
            {
                return;
            }

            if (identities.Add(GetNetworkLocationIdentity(normalized)))
            {
                roots.Add(normalized);
            }
        }

        // Deliberately avoid NetUseEnum/WNet discovery on the UI thread.
        // Explicitly registered and favorite shares are deterministic, and
        // the current share is already included by ResolveSearchRoots.
        foreach (var location in _settings.NetworkLocations)
        {
            Add(location.Path);
        }

        foreach (var favorite in _settings.Favorites)
        {
            Add(favorite.Path);
        }

        return roots;
    }

    private static bool IsSyntacticallyValidSearchRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (NetworkPathService.IsPotentialNetworkPath(path))
        {
            return true;
        }

        try
        {
            return Path.IsPathFullyQualified(path);
        }
        catch
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<string>> EnsureSearchRootsAccessibleAsync(
        IReadOnlyList<string> requestedRoots,
        CancellationToken cancellationToken)
    {
        var accessibleRoots = new List<string>(requestedRoots.Count);
        foreach (var requestedRoot in requestedRoots)
        {
            if (string.IsNullOrWhiteSpace(requestedRoot))
            {
                continue;
            }

            string root;
            try
            {
                root = NormalizeDirectoryPath(requestedRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
            {
                AppLog.Warning(
                    $"검색 위치 경로를 건너뜁니다: {requestedRoot} · " +
                    exception.Message);
                continue;
            }

            if (!NetworkPathService.IsPotentialNetworkPath(root))
            {
                try
                {
                    var exists = await Task.Run(
                            () => Directory.Exists(root),
                            CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
                    if (exists)
                    {
                        accessibleRoots.Add(root);
                    }
                }
                catch (TimeoutException)
                {
                    AppLog.Warning(
                        $"검색 위치 응답이 늦어 AI 분석에서 제외합니다: {root}");
                }

                continue;
            }

            StatusText.Text = $"네트워크 검색 위치 연결 확인: {root}";
            var access = await _networkPathService.EnsureAccessibleAsync(
                this,
                root,
                promptForConnection: false,
                cancellationToken: cancellationToken);
            if (access.Success)
            {
                if (NetworkPathService.IsUncServerRoot(access.Path))
                {
                    StatusText.Text = $"서버 공유 폴더 검색: {access.Path}";
                    var shares = await _networkPathService.EnumerateServerSharesAsync(
                        access.Path,
                        cancellationToken);
                    if (shares.Success)
                    {
                        accessibleRoots.AddRange(
                            shares.Shares.Select(share => share.Path));
                    }
                    else
                    {
                        AppLog.Warning(
                            $"서버 공유 폴더를 검색 위치로 확장하지 못했습니다: " +
                            $"{access.Path} · {shares.Message}");
                    }
                }
                else
                {
                    accessibleRoots.Add(access.Path);
                }

                continue;
            }

            AppLog.Warning(
                $"접근할 수 없는 네트워크 검색 위치를 건너뜁니다: " +
                $"{root} · {access.Message}");
            StatusText.Text =
                $"접근할 수 없는 네트워크 위치를 AI 분석에서 제외했습니다: {root}";
        }

        return accessibleRoots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void StopSearchButton_Click(object sender, RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _modelInstallCancellation?.Cancel();
        _storageMigrationCancellation?.Cancel();
    }

    private void SearchQueryTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_isSearchInputPlaceholderActive)
        {
            return;
        }

        if (SearchQueryTextBox.IsKeyboardFocused)
        {
            PauseBackgroundIndexingForInput();
        }
    }

    private void SearchQueryTextBox_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        HideSearchInputPlaceholder();
        PauseBackgroundIndexingForInput();
    }

    private void SearchQueryTextBox_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchQueryTextBox.Text))
        {
            ShowSearchInputPlaceholder();
        }

        _searchInputIdleTimer.Stop();
        if (!_isClosing && !_isSearchBusy)
        {
            _searchInputIdleTimer.Start();
        }
    }

    private void SearchInputIdleTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _searchInputIdleTimer.Stop();
        if (!_isClosing &&
            !_isSearchBusy &&
            !SearchQueryTextBox.IsKeyboardFocused)
        {
            ScheduleBackgroundIndexing();
        }
    }

    private async void SearchQueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await RunSearchAsync();
        e.Handled = true;
    }

    private void ShowSearchInputPlaceholder()
    {
        if (_isSearchInputPlaceholderActive ||
            !string.IsNullOrEmpty(SearchQueryTextBox.Text))
        {
            return;
        }

        _isSearchInputPlaceholderActive = true;
        SearchQueryTextBox.SetResourceReference(
            ForegroundProperty,
            "TextSecondaryBrush");
        SearchQueryTextBox.Text = SearchInputPlaceholder;
    }

    private void HideSearchInputPlaceholder()
    {
        if (!_isSearchInputPlaceholderActive)
        {
            return;
        }

        _isSearchInputPlaceholderActive = false;
        SearchQueryTextBox.SetResourceReference(
            ForegroundProperty,
            "TextPrimaryBrush");
        SearchQueryTextBox.Clear();
    }

    private void StartInstantTitleIndexWarmup()
    {
        _instantTitleIndexCancellation?.Cancel();
        _instantTitleIndexCancellation?.Dispose();
        _instantTitleResultCache.Clear();
        var cancellation = new CancellationTokenSource();
        _instantTitleIndexCancellation = cancellation;
        _ = WarmInstantTitleIndexesAsync(cancellation);
    }

    private async Task WarmInstantTitleIndexesAsync(
        CancellationTokenSource cancellation)
    {
        var roots = ResolveInstantTitleIndexRoots();
        if (roots.Count == 0)
        {
            InstantTitleIndexStatusText.Text = "색인할 위치 없음";
            return;
        }

        InstantTitleIndexProgressPanel.Visibility = Visibility.Visible;
        InstantTitleIndexProgressBar.Value = 0d;
        InstantTitleIndexDetailText.Text = "색인 위치를 확인하는 중...";
        InstantTitleIndexDetailText.ToolTip = null;
        InstantTitleIndexStatusText.Text = "색인 준비 중";
        var progress = new Progress<InstantTitleIndexProgress>(state =>
        {
            if (!ReferenceEquals(
                    _instantTitleIndexCancellation,
                    cancellation))
            {
                return;
            }

            InstantTitleIndexProgressBar.Value = state.PercentComplete;
            if (state.IsCompleted)
            {
                InstantTitleIndexStatusText.Text =
                    $"색인 {state.IndexedItems:N0}개 · 최신";
                InstantTitleIndexDetailText.Text = "제목 색인 최신";
                InstantTitleIndexDetailText.ToolTip = null;
                return;
            }

            var activeRootNumber = Math.Min(
                state.TotalRoots,
                state.CompletedRoots + 1);
            InstantTitleIndexStatusText.Text =
                $"색인 {state.IndexedItems:N0}개 · " +
                $"{state.PercentComplete:0}%";
            InstantTitleIndexDetailText.Text =
                $"위치 {activeRootNumber:N0}/{state.TotalRoots:N0} · " +
                state.CurrentPath;
            InstantTitleIndexDetailText.ToolTip = state.CurrentPath;
        });

        try
        {
            await _instantTitleSearchService.WarmIndexesAsync(
                roots,
                progress,
                cancellation.Token);
            if (!ReferenceEquals(
                    _instantTitleIndexCancellation,
                    cancellation))
            {
                return;
            }

            InstantTitleIndexProgressPanel.Visibility = Visibility.Collapsed;
            ScheduleInstantTitleSearch(immediate: true);
        }
        catch (OperationCanceledException)
        {
            // A newer warm-up request superseded this one.
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "제목 검색 색인을 준비하지 못했습니다. " +
                exception.Message);
            InstantTitleIndexProgressPanel.Visibility = Visibility.Collapsed;
            InstantTitleIndexStatusText.Text = "일부 색인 사용 가능";
        }
        finally
        {
            if (ReferenceEquals(
                    _instantTitleIndexCancellation,
                    cancellation))
            {
                cancellation.Dispose();
                _instantTitleIndexCancellation = null;
            }
        }
    }

    private void InstantTitleQueryTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        PauseBackgroundIndexingForInput();
        ScheduleInstantTitleSearch(immediate: true);
    }

    private void InstantTitleSearchFilter_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitializing || InstantTitleQueryTextBox is null)
        {
            return;
        }

        ScheduleInstantTitleSearch();
    }

    private void InstantTitleScopeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isInitializing || InstantTitleQueryTextBox is null)
        {
            return;
        }

        ScheduleInstantTitleSearch();
    }

    private void ClearInstantTitleQueryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstantTitleQueryTextBox.Clear();
        InstantTitleQueryTextBox.Focus();
    }

    private void ScheduleInstantTitleSearch(bool immediate = false)
    {
        _instantTitleSearchIdleTimer.Stop();
        _instantTitleSearchCancellation?.Cancel();
        if (string.IsNullOrWhiteSpace(InstantTitleQueryTextBox.Text))
        {
            InstantTitleSearchResults.Clear();
            InstantTitleSearchPlaceholderPanel.Visibility = Visibility.Visible;
            InstantTitleSearchPlaceholderTitle.Text = "파일명을 입력하세요";
            InstantTitleSearchPlaceholderDescription.Text = "입력 즉시 결과 표시";
            InstantTitleResultStatusText.Text = "검색 대기";
            return;
        }

        if (immediate)
        {
            _ = RunInstantTitleSearchAsync();
        }
        else
        {
            _instantTitleSearchIdleTimer.Start();
        }
    }

    private async void InstantTitleSearchIdleTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _instantTitleSearchIdleTimer.Stop();
        await RunInstantTitleSearchAsync();
    }

    private async Task RunInstantTitleSearchAsync()
    {
        var query = InstantTitleQueryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var roots = ResolveInstantTitleSearchRoots();
        if (roots.Count == 0)
        {
            InstantTitleSearchResults.Clear();
            InstantTitleSearchPlaceholderPanel.Visibility = Visibility.Visible;
            InstantTitleSearchPlaceholderTitle.Text = "검색할 위치가 없습니다";
            InstantTitleSearchPlaceholderDescription.Text =
                "검색 범위를 확인하세요.";
            InstantTitleResultStatusText.Text = "검색 위치 없음";
            return;
        }

        _instantTitleSearchCancellation?.Cancel();
        _instantTitleSearchCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _instantTitleSearchCancellation = cancellation;
        var options = new InstantTitleSearchOptions(
            MatchCase: InstantTitleMatchCaseCheckBox.IsChecked == true,
            MatchWholeWord: InstantTitleWholeWordCheckBox.IsChecked == true,
            UseRegularExpression: InstantTitleRegexCheckBox.IsChecked == true,
            ItemFilter: InstantTitleFoldersRadio.IsChecked == true
                ? InstantTitleItemFilter.Folders
                : InstantTitleFilesRadio.IsChecked == true
                    ? InstantTitleItemFilter.Files
                    : InstantTitleItemFilter.All,
            SortField: _instantTitleSortField,
            SortAscending: _instantTitleSortAscending);

        InstantTitleResultStatusText.Text = "색인에서 찾는 중...";
        try
        {
            var response = await _instantTitleSearchService.SearchAsync(
                query,
                roots,
                options,
                maximumResults: 750,
                cancellation.Token);
            if (!ReferenceEquals(
                    _instantTitleSearchCancellation,
                    cancellation))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.ValidationError))
            {
                InstantTitleSearchResults.Clear();
                InstantTitleSearchPlaceholderPanel.Visibility =
                    Visibility.Visible;
                InstantTitleSearchPlaceholderTitle.Text = "정규식을 확인해 주세요";
                InstantTitleSearchPlaceholderDescription.Text =
                    response.ValidationError;
                InstantTitleResultStatusText.Text = "정규식 오류";
                return;
            }

            ApplyInstantTitleSearchResults(response.Results);
            var elapsedDisplay = response.Elapsed.TotalMilliseconds < 1000d
                ? $"{Math.Max(1d, response.Elapsed.TotalMilliseconds):0}ms"
                : $"{response.Elapsed.TotalSeconds:0.00}초";
            InstantTitleResultStatusText.Text =
                response.TotalMatches > response.Results.Count
                    ? $"{response.TotalMatches:N0}개 결과 · " +
                      $"상위 {response.Results.Count:N0}개 · {elapsedDisplay}"
                    : $"{response.TotalMatches:N0}개 결과 · {elapsedDisplay}";
            InstantTitleIndexStatusText.Text = response.MissingRoots > 0
                ? $"색인 {response.IndexedItems:N0}개 · 준비 중"
                : $"색인 {response.IndexedItems:N0}개 · 최신";

            if (response.Results.Count == 0)
            {
                InstantTitleSearchPlaceholderPanel.Visibility =
                    Visibility.Visible;
                InstantTitleSearchPlaceholderTitle.Text = "일치하는 제목이 없습니다";
                InstantTitleSearchPlaceholderDescription.Text =
                    "범위를 넓히거나 필터를 해제하세요.";
            }
            else
            {
                InstantTitleSearchPlaceholderPanel.Visibility =
                    Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke superseded this search.
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "제목 즉시 검색을 완료하지 못했습니다. " +
                exception.Message);
            InstantTitleResultStatusText.Text = "검색 오류";
        }
        finally
        {
            if (ReferenceEquals(
                    _instantTitleSearchCancellation,
                    cancellation))
            {
                cancellation.Dispose();
                _instantTitleSearchCancellation = null;
                if (!_isClosing)
                {
                    ScheduleBackgroundIndexing(TimeSpan.FromSeconds(5));
                }
            }
        }
    }

    private void ApplyInstantTitleSearchResults(
        IReadOnlyList<InstantTitleSearchItem> records)
    {
        var selectedPaths = InstantTitleResultsListView.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_instantTitleResultCache.Count > 10_000)
        {
            _instantTitleResultCache.Clear();
        }

        var mappedResults = records
            .Select(GetOrCreateInstantTitleSearchResult)
            .ToArray();
        InstantTitleSearchResults.ReplaceAll(mappedResults);

        foreach (var result in InstantTitleSearchResults)
        {
            if (selectedPaths.Contains(result.FullPath))
            {
                InstantTitleResultsListView.SelectedItems.Add(result);
            }
        }
    }

    private SearchResult GetOrCreateInstantTitleSearchResult(
        InstantTitleSearchItem record)
    {
        if (_instantTitleResultCache.TryGetValue(
                record.FullPath,
                out var cached))
        {
            return cached;
        }

        var created = CreateInstantTitleSearchResult(record);
        _instantTitleResultCache[record.FullPath] = created;
        return created;
    }

    private SearchResult CreateInstantTitleSearchResult(
        InstantTitleSearchItem record)
    {
        var extension = record.IsDirectory
            ? string.Empty
            : record.Extension;
        return new SearchResult
        {
            Name = record.Name,
            FullPath = record.FullPath,
            DirectoryPath = record.DirectoryPath,
            TypeDisplay = record.IsDirectory
                ? "파일 폴더"
                : FileTypeCatalog.GetTypeDisplay(extension),
            ModifiedDisplay = record.ModifiedUtc == default
                ? "—"
                : record.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            CreatedUtc = record.CreatedUtc,
            ModifiedUtc = record.ModifiedUtc,
            SizeBytes = record.SizeBytes,
            Reason = "파일 제목에 입력한 문자가 포함되어 있습니다.",
            IconGlyph = record.IsDirectory ? "\uE8B7" : "\uE7C3",
            IconImage = _shellIconService.GetFileSystemIcon(
                record.IsDirectory ? "folder" : $"file{extension}",
                record.IsDirectory),
            Score = 100d,
            MatchPercent = 100d,
            WasAiAnalyzed = false,
            WasVisualAnalyzed = false,
            WasAdvancedAnalyzed = false,
            EvidenceKind = SearchEvidenceKind.ExactName,
            IsDirectory = record.IsDirectory
        };
    }

    private IReadOnlyList<string> ResolveInstantTitleIndexRoots() =>
        GetSearchDriveRootsWithoutProbe()
            .Concat(_settings.NetworkLocations.Select(location => location.Path))
            .Where(IsSyntacticallyValidSearchRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IReadOnlyList<string> ResolveInstantTitleSearchRoots()
    {
        var driveRoots = GetSearchDriveRootsWithoutProbe();
        var networkRoots = _settings.NetworkLocations
            .Select(location => location.Path)
            .Where(IsSyntacticallyValidSearchRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return InstantTitleScopeComboBox.SelectedIndex switch
        {
            1 when !_isComputerView && !string.IsNullOrWhiteSpace(_currentPath) =>
                [_currentPath],
            2 when !_isComputerView && !string.IsNullOrWhiteSpace(_currentPath) =>
                [Path.GetPathRoot(_currentPath) ?? _currentPath],
            3 => networkRoots,
            4 => driveRoots
                .Concat(networkRoots)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => driveRoots
        };
    }

    private void InstantTitleColumnHeader_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader { Tag: string tag } ||
            !Enum.TryParse<InstantTitleSortField>(tag, out var sortField))
        {
            return;
        }

        if (_instantTitleSortField == sortField)
        {
            _instantTitleSortAscending = !_instantTitleSortAscending;
        }
        else
        {
            _instantTitleSortField = sortField;
            _instantTitleSortAscending =
                sortField is InstantTitleSortField.Name or
                    InstantTitleSortField.Path;
        }

        ScheduleInstantTitleSearch(immediate: true);
    }

    private async void InstantTitleResultsListView_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (InstantTitleResultsListView.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private async void OpenInstantTitleResultMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (InstantTitleResultsListView.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private void OpenInstantTitleResultLocationMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (InstantTitleResultsListView.SelectedItem is not SearchResult result)
        {
            return;
        }

        try
        {
            _shellService.OpenContainingFolder(result.FullPath);
        }
        catch (Exception exception)
        {
            ShowError("파일 위치를 열지 못했습니다.", exception);
        }
    }

    private void CopyInstantTitleResultPathMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        var paths = InstantTitleResultsListView.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText.Text = $"{paths.Length:N0}개 경로를 복사했습니다.";
    }

    private void InstantTitleResultsListView_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!HasDragThresholdBeenExceeded(e))
        {
            return;
        }

        var paths = InstantTitleResultsListView.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        DragDrop.DoDragDrop(
            InstantTitleResultsListView,
            CreateFileDropData(paths, cut: false),
            DragDropEffects.Copy);
    }

    private async void SearchResultsListBox_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private async void TitleSearchResultsListBox_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (TitleSearchResultsListBox.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private async Task OpenSearchResultAsync(SearchResult result)
    {
        try
        {
            if (result.IsDirectory)
            {
                await NavigateToAsync(result.FullPath);
            }
            else
            {
                _shellService.OpenPath(result.FullPath);
            }
        }
        catch (Exception exception)
        {
            ShowError("검색 결과를 열지 못했습니다.", exception);
        }
    }

    private async void OpenSearchResultMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private void OpenSearchResultLocationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not SearchResult result)
        {
            return;
        }

        try
        {
            _shellService.OpenContainingFolder(result.FullPath);
        }
        catch (Exception exception)
        {
            ShowError("파일 위치를 열지 못했습니다.", exception);
        }
    }

    private void CopySearchResultPathMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var paths = SearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText.Text = $"{paths.Length}개 경로를 복사했습니다.";
    }

    private async void OpenTitleSearchResultMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TitleSearchResultsListBox.SelectedItem is SearchResult result)
        {
            await OpenSearchResultAsync(result);
        }
    }

    private void OpenTitleSearchResultLocationMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TitleSearchResultsListBox.SelectedItem is not SearchResult result)
        {
            return;
        }

        try
        {
            _shellService.OpenContainingFolder(result.FullPath);
        }
        catch (Exception exception)
        {
            ShowError("파일 위치를 열지 못했습니다.", exception);
        }
    }

    private void CopyTitleSearchResultPathMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        var paths = TitleSearchResultsListBox.SelectedItems
            .Cast<SearchResult>()
            .Select(result => result.FullPath)
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText.Text = $"{paths.Length:N0}개 경로를 복사했습니다.";
    }

    private void ShowSearchPlaceholder(string title, string description)
    {
        if (SearchResults.Count > 0)
        {
            SearchPlaceholderPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SearchPlaceholderTitle.Text = title;
        SearchPlaceholderDescription.Text = description;
        SearchPlaceholderPanel.Visibility = Visibility.Visible;
    }

    private async void SortComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (SortComboBox.SelectedIndex < 0)
        {
            return;
        }

        _settings.SortMode = (FileSortMode)SortComboBox.SelectedIndex;
        if (_isInitializing)
        {
            return;
        }

        await RefreshCurrentFolderAsync();
        _ = SaveSettingsSafelyAsync();
    }

    private void SearchResultSortComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (SearchResultSortComboBox.SelectedIndex < 0)
        {
            return;
        }

        _searchResultSortMode =
            (SearchResultSortMode)SearchResultSortComboBox.SelectedIndex;
        _settings.SearchResultSortMode = _searchResultSortMode;
        if (_isInitializing || SearchResultsListBox is null)
        {
            return;
        }

        RefreshResultRefinementViews();
        StatusText.Text = _searchResultSortMode switch
        {
            SearchResultSortMode.TopLevelPath =>
                "검색 결과를 드라이브와 최상위 경로부터 정렬했습니다.",
            SearchResultSortMode.Name =>
                "검색 결과를 가나다순으로 정렬했습니다.",
            SearchResultSortMode.ModifiedNewest =>
                "검색 결과를 수정 날짜가 최신인 순서로 정렬했습니다.",
            _ => "검색 결과를 일치도순으로 되돌렸습니다."
        };
        _ = SaveSettingsSafelyAsync();
    }

    private void SearchPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetSearchPanelVisible(SearchPanel.Visibility != Visibility.Visible);
    }

    private void CollapseSearchPanelButton_Click(object sender, RoutedEventArgs e)
    {
        SetSearchPanelVisible(false);
    }

    private void SearchPanel_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (SearchPanel.Visibility == Visibility.Visible &&
            SearchPanel.ActualWidth >= 470)
        {
            _lastSearchPanelWidth = SearchPanel.ActualWidth;
        }

        UpdateSearchResultsLayout();
    }

    private void IntegratedResultsViewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _selectedResultPane = SearchResultPane.Integrated;
        _resultViewChosenByUser = true;
        UpdateSearchResultsLayout();
    }

    private void TitleResultsViewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _selectedResultPane = SearchResultPane.Title;
        _resultViewChosenByUser = true;
        UpdateSearchResultsLayout();
    }

    private void UpdateSearchResultsLayout()
    {
        if (SearchResultsHostGrid is null ||
            IntegratedResultsViewButton is null)
        {
            return;
        }

        IntegratedResultsColumn.Width =
            new GridLength(1d, GridUnitType.Star);
        ResultsSplitterColumn.Width = new GridLength(8d);
        TitleResultsColumn.Width =
            new GridLength(1d, GridUnitType.Star);
        IntegratedResultsPane.Visibility = Visibility.Visible;
        ResultsGridSplitter.Visibility = Visibility.Visible;
        TitleResultsPane.Visibility = Visibility.Visible;

        IntegratedResultsViewButton.Content =
            $"통합 결과 {SearchResults.Count:N0}";
        TitleResultsViewButton.Content =
            $"빠른 이름·경로 {TitleSearchResults.Count:N0}";
        SetResultViewButtonStyle(
            IntegratedResultsViewButton,
            _selectedResultPane == SearchResultPane.Integrated);
        SetResultViewButtonStyle(
            TitleResultsViewButton,
            _selectedResultPane == SearchResultPane.Title);
    }

    private static void SetResultViewButtonStyle(
        Button button,
        bool selected) =>
        button.SetResourceReference(
            StyleProperty,
            selected
                ? "PrimaryButtonStyle"
                : "SecondaryButtonStyle");

    private void SetSearchPanelVisible(bool visible, bool persist = true)
    {
        if (visible)
        {
            SearchPanel.Visibility = Visibility.Visible;
            SearchGridSplitter.Visibility = Visibility.Visible;
            SearchSplitterColumn.Width = new GridLength(7d);
            SearchPanelColumn.Width =
                new GridLength(_lastSearchPanelWidth);
        }
        else
        {
            if (SearchPanel.ActualWidth >= 470)
            {
                _lastSearchPanelWidth = SearchPanel.ActualWidth;
            }

            SearchPanel.Visibility = Visibility.Collapsed;
            SearchGridSplitter.Visibility = Visibility.Collapsed;
            SearchSplitterColumn.Width = new GridLength(0d);
            SearchPanelColumn.Width = new GridLength(0d);
        }

        _settings.SearchPanelVisible = visible;
        if (persist)
        {
            CaptureSettings();
            _ = SaveSettingsSafelyAsync();
        }
    }

    private void NavigationTreeItem_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _navigationDragNode = (sender as TreeViewItem)?.DataContext as NavigationNode;
    }

    private void NavigationTreeItem_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_navigationDragNode is not
            { Kind: NavigationNodeKind.Favorite, Path: not null } node ||
            !HasDragThresholdBeenExceeded(e))
        {
            return;
        }

        var data = new DataObject();
        data.SetData(FavoriteReorderDataFormat, node.Path);
        DragDrop.DoDragDrop(
            sender as DependencyObject ?? NavigationTree,
            data,
            DragDropEffects.Move);
        _navigationDragNode = null;
    }

    private void NavigationTree_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(FavoriteReorderDataFormat))
        {
            var target = GetFavoriteReorderTarget(e, out _);
            e.Effects = target is not null
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths ||
            !paths.Any(FavoritePathService.IsSupportedDropSource))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void NavigationTree_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(FavoriteReorderDataFormat) is string reorderSourcePath)
        {
            var target = GetFavoriteReorderTarget(e, out var targetItem);
            if (target is null)
            {
                e.Handled = true;
                return;
            }

            string? targetPath = target.Kind == NavigationNodeKind.Favorite
                ? target.Path
                : null;
            var insertAfter = false;
            if (targetItem is not null && target.Kind == NavigationNodeKind.Favorite)
            {
                var headerHeight = Math.Min(36, targetItem.ActualHeight);
                insertAfter = e.GetPosition(targetItem).Y >= headerHeight / 2;
            }

            if (FavoritePathService.MoveFavorite(
                    _settings.Favorites,
                    reorderSourcePath,
                    targetPath,
                    insertAfter))
            {
                BuildNavigationTree();
                await SaveSettingsSafelyAsync();
                StatusText.Text = "즐겨찾기 순서를 변경했습니다.";
            }

            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths ||
            paths.Length == 0)
        {
            return;
        }

        var existing = _settings.Favorites
            .Select(item => FavoritePathService.GetIdentity(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var sourcePath in paths)
        {
            if (!FavoritePathService.TryResolve(
                    sourcePath,
                    out var favorite,
                    out var error) ||
                favorite is null)
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(error))
                {
                    errors.Add($"{Path.GetFileName(sourcePath)}: {error}");
                }
                continue;
            }

            var identity = FavoritePathService.GetIdentity(favorite.Path);
            if (!existing.Add(identity))
            {
                skipped++;
                continue;
            }

            _settings.Favorites.Add(new FavoriteLocation
            {
                Name = favorite.Name,
                Path = favorite.Path
            });
            added++;
        }

        if (added > 0)
        {
            BuildNavigationTree();
            await SaveSettingsSafelyAsync();
            StatusText.Text = skipped > 0
                ? $"즐겨찾기에 {added:N0}개를 추가하고 {skipped:N0}개를 건너뛰었습니다."
                : $"즐겨찾기에 {added:N0}개를 추가했습니다.";
        }
        else
        {
            StatusText.Text = "추가할 수 있는 새 즐겨찾기가 없습니다.";
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, errors.Take(6)),
                "즐겨찾기 추가",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        e.Handled = true;
    }

    private NavigationNode? GetFavoriteReorderTarget(
        DragEventArgs e,
        out TreeViewItem? targetItem)
    {
        targetItem = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);
        if (targetItem?.DataContext is NavigationNode
            { Kind: NavigationNodeKind.Favorite or NavigationNodeKind.FavoritesSection } node)
        {
            return node;
        }

        if (targetItem?.DataContext is NavigationNode
            { Kind: NavigationNodeKind.Placeholder })
        {
            var parentItem = FindVisualParent<TreeViewItem>(
                VisualTreeHelper.GetParent(targetItem));
            if (parentItem?.DataContext is NavigationNode
                { Kind: NavigationNodeKind.FavoritesSection } parentNode)
            {
                targetItem = parentItem;
                return parentNode;
            }
        }

        targetItem = null;
        return null;
    }

    private bool IsFavoritePath(string path)
    {
        var identity = FavoritePathService.GetIdentity(path);
        return _settings.Favorites.Any(item =>
            string.Equals(
                FavoritePathService.GetIdentity(item.Path),
                identity,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFavoriteCandidatePath(string path) =>
        NetworkPathService.IsPotentialNetworkPath(path) || Directory.Exists(path);

    private void UpdateCurrentPathFavoriteButtonState()
    {
        if (AddCurrentPathToFavoritesButton is null)
        {
            return;
        }

        var canAdd = !_isComputerView &&
                     !string.IsNullOrWhiteSpace(_currentPath) &&
                     IsFavoriteCandidatePath(_currentPath);
        var alreadyAdded = canAdd && IsFavoritePath(_currentPath!);

        AddCurrentPathToFavoritesButton.IsEnabled = canAdd && !alreadyAdded;
        AddCurrentPathToFavoritesButton.Content = alreadyAdded
            ? "★ 즐겨찾기됨"
            : "☆ 즐겨찾기";
        AddCurrentPathToFavoritesButton.ToolTip = alreadyAdded
            ? "현재 경로는 이미 즐겨찾기에 등록되어 있습니다."
            : canAdd
                ? "현재 경로를 즐겨찾기에 추가합니다."
                : "폴더 경로에서 사용할 수 있습니다.";
    }

    private async Task<bool> AddFavoritePathAsync(
        string path,
        string? preferredName)
    {
        if (!FavoritePathService.TryCreateFolderTarget(
                path,
                preferredName,
                out var target,
                out var error) ||
            target is null)
        {
            StatusText.Text = string.IsNullOrWhiteSpace(error)
                ? "이 위치를 즐겨찾기에 추가할 수 없습니다."
                : error;
            return false;
        }

        if (IsFavoritePath(target.Path))
        {
            StatusText.Text = "이미 즐겨찾기에 등록된 위치입니다.";
            return false;
        }

        _settings.Favorites.Add(new FavoriteLocation
        {
            Name = target.Name,
            Path = target.Path
        });
        BuildNavigationTree();
        await SaveSettingsSafelyAsync();
        StatusText.Text = $"‘{target.Name}’을(를) 즐겨찾기에 추가했습니다.";
        ScheduleBackgroundIndexing(TimeSpan.FromSeconds(2));
        return true;
    }

    private async void RenameFavoriteMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (NavigationTree.SelectedItem is not NavigationNode
            { Kind: NavigationNodeKind.Favorite, Path: not null } node)
        {
            StatusText.Text = "이름을 바꿀 즐겨찾기를 먼저 선택해 주세요.";
            return;
        }

        var identity = FavoritePathService.GetIdentity(node.Path);
        var favorite = _settings.Favorites.FirstOrDefault(item =>
            string.Equals(
                FavoritePathService.GetIdentity(item.Path),
                identity,
                StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            StatusText.Text = "선택한 즐겨찾기 정보를 찾지 못했습니다.";
            return;
        }

        var dialog = new TextPromptDialog(
            this,
            "즐겨찾기 이름 변경",
            "왼쪽 탐색 트리에 표시할 이름을 입력해 주세요.",
            favorite.Name);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        favorite.Name = dialog.Value;
        BuildNavigationTree();
        await SaveSettingsSafelyAsync();
        StatusText.Text = "즐겨찾기 이름을 변경했습니다.";
    }

    private async void RemoveFavoriteMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (NavigationTree.SelectedItem is not NavigationNode
            { Kind: NavigationNodeKind.Favorite, Path: not null } node)
        {
            StatusText.Text = "제거할 즐겨찾기를 먼저 선택해 주세요.";
            return;
        }

        var identity = FavoritePathService.GetIdentity(node.Path);
        var favorite = _settings.Favorites.FirstOrDefault(item =>
            string.Equals(
                FavoritePathService.GetIdentity(item.Path),
                identity,
                StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            StatusText.Text = "선택한 즐겨찾기 정보를 찾지 못했습니다.";
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"‘{favorite.Name}’을(를) 즐겨찾기에서 제거하시겠습니까?\n\n" +
            "실제 폴더와 파일은 삭제되지 않습니다.",
            "즐겨찾기 제거",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.Favorites.Remove(favorite);
        BuildNavigationTree();
        await SaveSettingsSafelyAsync();
        StatusText.Text = "즐겨찾기에서 제거했습니다.";
        ScheduleBackgroundIndexing(TimeSpan.FromSeconds(4));
    }

    private async void AddNetworkLocationButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NetworkLocationDialog(this, _networkPathService);
        if (dialog.ShowDialog() != true || dialog.Location is null)
        {
            return;
        }

        if (_settings.NetworkLocations.Any(location =>
                string.Equals(
                    location.Path,
                    dialog.Location.Path,
                    StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                this,
                "이미 등록된 네트워크 위치입니다.",
                "네트워크 위치 추가",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _settings.NetworkLocations.Add(dialog.Location);
        BuildNavigationTree();
        await SaveSettingsSafelyAsync();

        await NavigateToAsync(dialog.Location.Path);
    }

    private async void RefreshNavigationMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        BuildNavigationTree();
        if (_isComputerView)
        {
            await ShowComputerViewAsync(recordHistory: false);
        }

        StatusText.Text = "드라이브와 즐겨찾기 목록을 다시 불러왔습니다.";
    }

    private async void RemoveNetworkLocationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationTree.SelectedItem is not NavigationNode { Path: not null } node)
        {
            StatusText.Text = "제거할 네트워크 위치를 먼저 선택해 주세요.";
            return;
        }

        var location = _settings.NetworkLocations.FirstOrDefault(item =>
            string.Equals(item.Path, node.Path, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            StatusText.Text = "직접 등록한 네트워크 위치만 목록에서 제거할 수 있습니다.";
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"‘{location.Name}’을(를) 탐색 목록에서 제거하시겠습니까?\n\n" +
            "실제 네트워크 폴더와 파일은 삭제되지 않습니다.",
            "네트워크 위치 제거",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.NetworkLocations.Remove(location);
        BuildNavigationTree();
        await SaveSettingsSafelyAsync();
        StatusText.Text = "네트워크 위치를 탐색 목록에서 제거했습니다.";
    }

    private async void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _backgroundIndexCancellation?.Cancel();
        _embeddingService.SetLowPriority(true);
        StatusText.Text = "AI 및 저장 위치 설정을 열었습니다.";
        var dialog = new AiSettingsDialog(
            this,
            _aiModelManager.IsInstalled,
            _settingsService.DataDirectory,
            _settings.UseSystemTrayBackground,
            _settingsService.GetDataDirectorySize);
        if (dialog.ShowDialog() != true)
        {
            StatusText.Text = "설정을 닫았습니다.";
            ScheduleBackgroundIndexing();
            return;
        }

        _settings.UseSystemTrayBackground =
            dialog.UseSystemTrayBackground;
        _trayIconService.SetVisible(
            _settings.UseSystemTrayBackground);
        await SaveSettingsSafelyAsync();

        if (dialog.StorageChangeRequested &&
            !string.IsNullOrWhiteSpace(
                dialog.RequestedDataDirectory))
        {
            await ChangeStorageLocationAsync(
                dialog.RequestedDataDirectory,
                dialog.CurrentStorageBytes);
            return;
        }

        StatusText.Text = _settings.UseSystemTrayBackground
            ? "설정 저장 · 닫을 때 트레이 실행"
            : "설정 저장 · 닫을 때 완전 종료";
        ScheduleBackgroundIndexing();
    }

    private async Task ChangeStorageLocationAsync(
        string requestedDirectory,
        long currentStorageBytes)
    {
        if (currentStorageBytes <= 0)
        {
            currentStorageBytes = await Task.Run(
                _settingsService.GetDataDirectorySize);
        }

        string targetDirectory;
        try
        {
            targetDirectory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(requestedDirectory));
        }
        catch (Exception exception)
        {
            ShowError("새 저장 위치를 확인하지 못했습니다.", exception);
            ScheduleBackgroundIndexing();
            return;
        }

        var targetHasData = false;
        try
        {
            targetHasData =
                Directory.Exists(targetDirectory) &&
                Directory
                    .EnumerateFileSystemEntries(targetDirectory)
                    .Any();
        }
        catch (IOException)
        {
            targetHasData = true;
        }
        catch (UnauthorizedAccessException)
        {
            targetHasData = true;
        }
        var answer = MessageBox.Show(
            this,
            $"현재 위치\n{_settingsService.DataDirectory}\n" +
            $"현재 사용량\n{AiSettingsDialog.FormatBytes(currentStorageBytes)}\n\n" +
            $"새 위치\n{targetDirectory}\n\n" +
            "AI 모델, 설정, 색인을 새 위치로 복사합니다. " +
            "원본 폴더는 자동으로 삭제하지 않습니다." +
            (targetHasData
                ? "\n새 위치에 같은 이름의 데이터가 있으면 새 복사본으로 교체합니다."
                : string.Empty) +
            "\n완료 후 변경 적용을 위해 앱을 다시 시작합니다.\n\n" +
            "계속하시겠습니까?",
            "저장 위치 변경",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes)
        {
            StatusText.Text = "저장 위치 변경을 취소했습니다.";
            ScheduleBackgroundIndexing();
            return;
        }

        CaptureSettings();
        await SaveSettingsSafelyAsync();
        _isSearchBusy = true;
        _searchInputIdleTimer.Stop();
        _searchCancellation?.Cancel();
        _backgroundIndexCancellation?.Cancel();
        _embeddingService.Stop();
        _storageMigrationCancellation?.Cancel();
        _storageMigrationCancellation?.Dispose();
        _storageMigrationCancellation = new CancellationTokenSource();
        var token = _storageMigrationCancellation.Token;

        SearchButton.IsEnabled = false;
        AiModelButton.IsEnabled = false;
        StopSearchButton.Content = "복사 중지";
        StopSearchButton.Visibility = Visibility.Visible;
        SearchProgressBar.Visibility = Visibility.Visible;
        SearchProgressBar.IsIndeterminate = currentStorageBytes <= 0;
        SearchProgressBar.Minimum = 0;
        SearchProgressBar.Maximum = 100;
        SearchProgressBar.Value = 0;
        SearchEngineStatusText.Text =
            "AI 모델과 색인을 새 저장 위치로 복사하는 중...";

        var progress = new Progress<StorageMigrationProgress>(state =>
        {
            var percentage = state.TotalBytes > 0
                ? Math.Clamp(
                    state.CopiedBytes * 100d / state.TotalBytes,
                    0d,
                    100d)
                : 0d;
            SearchProgressBar.IsIndeterminate = state.TotalBytes <= 0;
            SearchProgressBar.Value = percentage;
            SearchResultCountText.Text =
                $"{state.CopiedFiles:N0}/{state.TotalFiles:N0}개 · " +
                $"{percentage:0}%";
            StatusText.Text =
                $"저장 데이터 복사 중 · {state.CurrentFile}";
        });

        try
        {
            var result =
                await _settingsService.ChangeDataDirectoryAsync(
                    targetDirectory,
                    progress,
                    token);
            if (!result.LocationChanged)
            {
                MessageBox.Show(
                    this,
                    "선택한 폴더가 현재 저장 위치와 같습니다.",
                    "저장 위치",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                StatusText.Text = "현재 저장 위치를 유지합니다.";
                return;
            }

            AppLog.Info(
                "Storage location changed to " +
                result.DataDirectory);
            MessageBox.Show(
                this,
                $"저장 위치를 변경했습니다.\n\n" +
                $"새 위치\n{result.DataDirectory}\n\n" +
                $"{result.CopiedFiles:N0}개 파일, " +
                $"{AiSettingsDialog.FormatBytes(result.CopiedBytes)}를 복사했습니다.\n" +
                "기존 위치의 원본 데이터는 그대로 남아 있습니다.\n\n" +
                "이제 AI 탐색기를 다시 시작합니다.",
                "저장 위치 변경 완료",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RestartAfterStorageChange();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text =
                "저장 위치 복사를 중지했습니다. 현재 위치를 계속 사용합니다.";
        }
        catch (Exception exception)
        {
            ShowError("저장 위치를 변경하지 못했습니다.", exception);
            StatusText.Text =
                "저장 위치 변경에 실패해 현재 위치를 계속 사용합니다.";
        }
        finally
        {
            _storageMigrationCancellation?.Dispose();
            _storageMigrationCancellation = null;
            _isSearchBusy = false;
            if (!_isClosing)
            {
                StopSearchButton.Content = "중지";
                StopSearchButton.Visibility = Visibility.Collapsed;
                SearchProgressBar.Visibility = Visibility.Collapsed;
                SearchProgressBar.IsIndeterminate = true;
                UpdateAiModelUi(updateStatusText: false);
                ScheduleBackgroundIndexing(TimeSpan.FromSeconds(4));
            }
        }
    }

    private void RestartAfterStorageChange()
    {
        _isClosing = true;
        var processPath = Environment.ProcessPath;
        try
        {
            if (!string.IsNullOrWhiteSpace(processPath) &&
                File.Exists(processPath) &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(processPath),
                    "AIExplorer",
                    StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show(
                    this,
                    "개발 실행 환경에서는 자동 재시작할 수 없습니다. " +
                    "앱을 다시 실행하면 새 저장 위치가 적용됩니다.",
                    "다시 시작 필요",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "자동 재시작에 실패했습니다. 앱을 직접 다시 실행해 주세요.\n\n" +
                exception.Message,
                "다시 시작 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var textInputFocused = Keyboard.FocusedElement is TextBox;

        if (key == Key.BrowserBack)
        {
            await NavigateBackAsync();
            e.Handled = true;
            return;
        }
        if (key == Key.BrowserForward)
        {
            await NavigateForwardAsync();
            e.Handled = true;
            return;
        }
        if (key == Key.BrowserRefresh)
        {
            await RefreshCurrentFolderAsync();
            e.Handled = true;
            return;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            if (key == Key.Left)
            {
                await NavigateBackAsync();
                e.Handled = true;
            }
            else if (key == Key.Right)
            {
                await NavigateForwardAsync();
                e.Handled = true;
            }
            else if (key == Key.Up)
            {
                await NavigateUpAsync();
                e.Handled = true;
            }
            else if (key == Key.D)
            {
                FocusPathInput();
                e.Handled = true;
            }

            return;
        }

        if (modifiers == ModifierKeys.Control && key == Key.L)
        {
            FocusPathInput();
            e.Handled = true;
            return;
        }
        if ((modifiers == ModifierKeys.Control &&
             key is Key.E or Key.F) ||
            modifiers == ModifierKeys.None &&
            key == Key.F3)
        {
            FocusSearchInput();
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.Control && key == Key.W)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (textInputFocused)
        {
            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.N)
        {
            NewFolderButton_Click(sender, e);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.C)
        {
            CopyButton_Click(sender, e);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.X)
        {
            CutButton_Click(sender, e);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.V)
        {
            PasteButton_Click(sender, e);
            e.Handled = true;
        }
        else if (key == Key.F2)
        {
            RenameButton_Click(sender, e);
            e.Handled = true;
        }
        else if (key == Key.Delete)
        {
            DeleteButton_Click(sender, e);
            e.Handled = true;
        }
        else if (key == Key.F5)
        {
            await RefreshCurrentFolderAsync();
            e.Handled = true;
        }
        else if (key == Key.Back)
        {
            await NavigateBackAsync();
            e.Handled = true;
        }
        else if (key == Key.Enter && FileListView.SelectedItem is FileSystemEntry entry)
        {
            await OpenEntryAsync(entry);
            e.Handled = true;
        }
    }

    private async void Window_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            e.Handled = true;
            await NavigateBackAsync();
        }
        else if (e.ChangedButton == MouseButton.XButton2)
        {
            e.Handled = true;
            await NavigateForwardAsync();
        }
    }

    private void FocusPathInput()
    {
        PathTextBox.Focus();
        PathTextBox.SelectAll();
    }

    private void FocusSearchInput()
    {
        SetSearchPanelVisible(true);
        SearchQueryTextBox.Focus();
        SearchQueryTextBox.SelectAll();
    }

    private void CaptureSettings()
    {
        if (!string.IsNullOrWhiteSpace(_currentPath))
        {
            _settings.LastPath = _currentPath;
        }
        _settings.SearchPanelVisible = SearchPanel.Visibility == Visibility.Visible;
        if (SearchPanel.Visibility == Visibility.Visible &&
            SearchPanel.ActualWidth >= 470)
        {
            _lastSearchPanelWidth = SearchPanel.ActualWidth;
        }
        if (InstantTitleSearchPanel.ActualWidth >= 360)
        {
            _lastInstantTitlePanelWidth = InstantTitleSearchPanel.ActualWidth;
        }

        _settings.SearchPanelWidth = _lastSearchPanelWidth;
        _settings.InstantTitlePanelWidth = _lastInstantTitlePanelWidth;
        _settings.SearchResultSortMode = _searchResultSortMode;
    }

    private async Task SaveSettingsSafelyAsync()
    {
        try
        {
            CaptureSettings();
            await _settingsService.SaveAsync(_settings);
        }
        catch
        {
            // Settings persistence must not interrupt file browsing.
        }
    }

    private void ShowError(string title, Exception exception)
    {
        AppLog.Error(title, exception);
        StatusText.Text = title;
        MessageBox.Show(
            this,
            $"{title}\n\n{exception.Message}",
            "AI 탐색기",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
