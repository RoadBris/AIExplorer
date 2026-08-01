using System.Windows;
using System.Windows.Threading;
using AIExplorer.Services;

namespace AIExplorer;

public partial class App : Application
{
    internal LaunchedProcessTracker LaunchedProcesses { get; } = new();

    internal bool IsSessionEnding { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var settingsService = new SettingsService();
        AppLog.Initialize(settingsService.DataDirectory);
        AppLog.Info(
            $"Application starting. Version={typeof(App).Assembly.GetName().Version}; " +
            $"OS={Environment.OSVersion}; Is64Bit={Environment.Is64BitProcess}");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LaunchedProcesses.Dispose();
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppLog.Shutdown();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(
        SessionEndingCancelEventArgs e)
    {
        IsSessionEnding = true;
        base.OnSessionEnding(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show(
            $"예기치 않은 오류가 발생했습니다.\n\n{e.Exception.Message}",
            "AI 탐색기",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        AppLog.Error("Unhandled application exception.", exception);
        MessageBox.Show(
            $"프로그램을 계속 실행할 수 없는 오류가 발생했습니다.\n\n{exception.Message}",
            "AI 탐색기",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
