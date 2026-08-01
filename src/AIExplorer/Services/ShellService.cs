using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace AIExplorer.Services;

public sealed class ShellService
{
    private readonly LaunchedProcessTracker _launchedProcesses;

    public ShellService(LaunchedProcessTracker launchedProcesses)
    {
        _launchedProcesses = launchedProcesses;
    }

    public void OpenPath(string path)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        _launchedProcesses.Track(process, path);
    }

    public void TerminateLaunchedProcesses()
    {
        _launchedProcesses.TerminateAll();
    }

    public void OpenContainingFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{SanitizeArgument(path)}\"",
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{SanitizeArgument(path)}\"",
            UseShellExecute = true
        });
    }

    public void ShowProperties(string path, Window owner)
    {
        var info = new ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<ShellExecuteInfo>(),
            fMask = SeeMaskInvokeIdList,
            hwnd = new System.Windows.Interop.WindowInteropHelper(owner).Handle,
            lpVerb = "properties",
            lpFile = path,
            nShow = ShowNormal
        };

        if (!ShellExecuteEx(ref info))
        {
            throw new InvalidOperationException("Windows 파일 속성 창을 열 수 없습니다.");
        }
    }

    private static string SanitizeArgument(string value) => value.Replace("\"", string.Empty);

    private const uint SeeMaskInvokeIdList = 0x0000000C;
    private const int ShowNormal = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpVerb;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpParameters;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDirectory;

        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpClass;

        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIconOrMonitor;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);
}
