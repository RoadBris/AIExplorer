using System.Diagnostics;
using System.Windows;
using AIExplorer.Services;
using Microsoft.Win32;

namespace AIExplorer.Dialogs;

public partial class AiSettingsDialog : Window
{
    private readonly string _currentDataDirectory;
    private readonly Func<long> _storageSizeProvider;

    public AiSettingsDialog(
        Window owner,
        bool fixedAiBundleReady,
        string currentDataDirectory,
        bool useSystemTrayBackground,
        Func<long> storageSizeProvider)
    {
        InitializeComponent();
        Owner = owner;
        _currentDataDirectory = currentDataDirectory;
        _storageSizeProvider = storageSizeProvider;

        InstallStatusText.Text = fixedAiBundleReady
            ? "고정 구성 정상"
            : "자동 복구 필요";
        StoragePathTextBox.Text = currentDataDirectory;
        CurrentStorageSizeText.Text = "현재 사용량 계산 중...";
        UseSystemTrayBackgroundCheckBox.IsChecked =
            useSystemTrayBackground;
    }

    public long CurrentStorageBytes { get; private set; }

    public bool StorageChangeRequested { get; private set; }

    public string? RequestedDataDirectory { get; private set; }

    public bool UseSystemTrayBackground =>
        UseSystemTrayBackgroundCheckBox.IsChecked == true;

    private void Window_Loaded(
        object sender,
        RoutedEventArgs e) =>
        _ = LoadStorageSizeAsync();

    private async Task LoadStorageSizeAsync()
    {
        try
        {
            CurrentStorageBytes = await Task.Run(_storageSizeProvider);
            CurrentStorageSizeText.Text =
                $"현재 사용량 {FormatBytes(CurrentStorageBytes)}";
        }
        catch
        {
            CurrentStorageSizeText.Text =
                "현재 사용량을 계산하지 못했습니다.";
        }
    }

    private void OpenStorageButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_currentDataDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _currentDataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"저장 폴더를 열지 못했습니다.\n\n{exception.Message}",
                "저장 위치",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ChangeStorageButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "AI 탐색기 데이터를 저장할 상위 폴더 선택",
            Multiselect = false,
            InitialDirectory =
                Path.GetDirectoryName(_currentDataDirectory) ??
                _currentDataDirectory
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var selectedDirectory =
            Path.TrimEndingDirectorySeparator(dialog.FolderName);
        RequestedDataDirectory = string.Equals(
                Path.GetFileName(selectedDirectory),
                "_AIExplorer_Data",
                StringComparison.OrdinalIgnoreCase)
            ? selectedDirectory
            : Path.Combine(selectedDirectory, "_AIExplorer_Data");
        StorageChangeRequested = true;
        DialogResult = true;
    }

    private void SaveButton_Click(
        object sender,
        RoutedEventArgs e) =>
        DialogResult = true;

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes:N0} B";
        }

        var units = new[] { "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unitIndex = -1;
        do
        {
            value /= 1024;
            unitIndex++;
        }
        while (value >= 1024 && unitIndex < units.Length - 1);

        return $"{value:N1} {units[unitIndex]}";
    }
}
