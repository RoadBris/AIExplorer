using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AIExplorer.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _pauseIndexingItem;
    private readonly Drawing.Icon _icon;
    private bool _disposed;
    private bool _backgroundNoticeShown;

    public TrayIconService()
    {
        _icon = LoadApplicationIcon();
        _contextMenu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("AI 탐색기 열기");
        openItem.Font = new Drawing.Font(
            openItem.Font,
            Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => RestoreRequested?.Invoke(
            this,
            EventArgs.Empty);
        _pauseIndexingItem =
            new Forms.ToolStripMenuItem("백그라운드 색인 일시 중지");
        _pauseIndexingItem.Click += (_, _) =>
            ToggleIndexingRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new Forms.ToolStripMenuItem("완전히 종료");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(
            this,
            EventArgs.Empty);
        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(_pauseIndexingItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "AI 탐색기 · 백그라운드 색인 준비",
            ContextMenuStrip = _contextMenu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) =>
            RestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RestoreRequested;

    public event EventHandler? ToggleIndexingRequested;

    public event EventHandler? ExitRequested;

    public void SetVisible(bool visible)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = visible;
    }

    public void SetIndexingPaused(bool paused)
    {
        _pauseIndexingItem.Text = paused
            ? "백그라운드 색인 다시 시작"
            : "백그라운드 색인 일시 중지";
        SetStatus(paused
            ? "자동 색인 일시 중지"
            : "백그라운드 색인 준비");
    }

    public void SetStatus(string status)
    {
        if (_disposed)
        {
            return;
        }

        var normalized = string.IsNullOrWhiteSpace(status)
            ? "AI 탐색기"
            : $"AI 탐색기 · {status.Trim()}";
        _notifyIcon.Text = normalized.Length <= 63
            ? normalized
            : normalized[..63];
    }

    public void ShowBackgroundNotice()
    {
        if (_disposed || _backgroundNoticeShown)
        {
            return;
        }

        _backgroundNoticeShown = true;
        _notifyIcon.BalloonTipTitle = "AI 탐색기가 백그라운드에서 실행 중입니다";
        _notifyIcon.BalloonTipText =
            "즐겨찾기 폴더의 검색 색인을 미리 준비합니다. " +
            "트레이 아이콘을 두 번 누르면 다시 열립니다.";
        _notifyIcon.BalloonTipIcon =
            Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(4_000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _icon.Dispose();
    }

    private static Drawing.Icon LoadApplicationIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(
                    processPath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // Fall back to a stable Windows icon in development hosts.
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}
