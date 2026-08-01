using System.Windows;
using System.Windows.Controls;
using AIExplorer.Models;
using AIExplorer.Services;

namespace AIExplorer.Dialogs;

public partial class NetworkLocationDialog : Window
{
    private readonly NetworkPathService _networkPathService;
    private CancellationTokenSource? _connectionTestCancellation;

    public NetworkLocationDialog(
        Window owner,
        NetworkPathService networkPathService)
    {
        _networkPathService = networkPathService;
        InitializeComponent();
        Owner = owner;
        Loaded += Window_Loaded;
        Closed += (_, _) => _connectionTestCancellation?.Cancel();
    }

    public NetworkLocation? Location { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MaxHeight = Math.Max(430, SystemParameters.WorkArea.Height - 48);
        NameTextBox.Focus();
    }

    private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized || ConnectionStatusText is null)
        {
            return;
        }

        ConnectionStatusText.Text = "경로 변경 · 연결 확인 필요";
        ConnectionStatusText.Foreground =
            (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }

    private async void TestConnectionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string path;
        try
        {
            path = NetworkPathService.NormalizeNetworkLocationPath(PathTextBox.Text);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            ShowValidation("올바른 네트워크 경로를 입력해 주세요.", PathTextBox);
            return;
        }

        if (!NetworkPathService.IsSupportedNetworkLocation(path))
        {
            ShowValidation(
                @"192.168.0.10, \\서버, \\서버\공유폴더 또는 Z:\ 형식으로 입력해 주세요.",
                PathTextBox);
            return;
        }

        _connectionTestCancellation?.Cancel();
        _connectionTestCancellation?.Dispose();
        _connectionTestCancellation = new CancellationTokenSource();
        TestConnectionButton.IsEnabled = false;
        ConnectionStatusText.Text = "네트워크 연결을 확인하는 중...";

        try
        {
            var result = await _networkPathService.EnsureAccessibleAsync(
                this,
                path,
                promptForConnection: true,
                cancellationToken: _connectionTestCancellation.Token);
            if (result.Success)
            {
                PathTextBox.Text = result.Path;
            }

            ConnectionStatusText.Text = result.Success
                ? NetworkPathService.IsUncServerRoot(result.Path)
                    ? "연결 완료 · 공유 폴더 확인 가능"
                    : "연결 완료 · 폴더 읽기 가능"
                : result.Message;
            ConnectionStatusText.Foreground = result.Success
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }
        catch (OperationCanceledException)
        {
            ConnectionStatusText.Text = "연결 확인이 취소되었습니다.";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        string path;
        try
        {
            path = NetworkPathService.NormalizeNetworkLocationPath(PathTextBox.Text);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            ShowValidation("올바른 네트워크 경로를 입력해 주세요.", PathTextBox);
            return;
        }

        if (!NetworkPathService.IsSupportedNetworkLocation(path))
        {
            ShowValidation(
                @"192.168.0.10, \\서버, \\서버\공유폴더 또는 Z:\ 형식으로 입력해 주세요.",
                PathTextBox);
            return;
        }

        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = NetworkPathService.GetUncShareRoot(path)?.Split(
                           Path.DirectorySeparatorChar,
                           StringSplitOptions.RemoveEmptyEntries)
                       .LastOrDefault() ??
                   NetworkPathService.GetUncServerRoot(path)?.TrimStart(Path.DirectorySeparatorChar) ??
                   path;
        }

        Location = new NetworkLocation
        {
            Name = name,
            Path = path
        };
        DialogResult = true;
    }

    private void ShowValidation(string message, Control target)
    {
        MessageBox.Show(
            this,
            message,
            "네트워크 위치 추가",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        target.Focus();
    }
}
