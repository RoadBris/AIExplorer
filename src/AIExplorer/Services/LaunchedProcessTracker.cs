using System.Diagnostics;

namespace AIExplorer.Services;

public sealed class LaunchedProcessTracker : IDisposable
{
    private static readonly TimeSpan GracefulShutdownTimeout =
        TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan ForcedShutdownTimeout =
        TimeSpan.FromMilliseconds(1_500);

    private readonly object _sync = new();
    private readonly Dictionary<int, Process> _processes = [];
    private bool _disposed;

    public void Track(Process? process, string sourcePath)
    {
        if (process is null)
        {
            // Windows reused an application that was already running.
            // It does not belong to AIExplorer and must not be terminated.
            return;
        }

        var shouldTerminateImmediately = false;
        var keepProcess = false;
        var processId = 0;
        var processName = "unknown";

        try
        {
            processId = process.Id;
            if (processId == Environment.ProcessId || process.HasExited)
            {
                return;
            }

            processName = GetProcessName(process);
            lock (_sync)
            {
                if (_disposed)
                {
                    shouldTerminateImmediately = true;
                }
                else if (_processes.TryGetValue(
                             processId,
                             out var existing))
                {
                    if (IsRunning(existing))
                    {
                        return;
                    }

                    _processes.Remove(processId);
                    existing.Dispose();
                    _processes.Add(processId, process);
                    keepProcess = true;
                }
                else
                {
                    _processes.Add(processId, process);
                    keepProcess = true;
                }
            }

            if (keepProcess)
            {
                AppLog.Info(
                    "Tracking process opened by AIExplorer. " +
                    $"Name={processName}; Id={processId}; " +
                    $"File={Path.GetFileName(sourcePath)}");
            }
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "Could not register a shell-launched process for cleanup. " +
                exception.Message);
        }
        finally
        {
            if (!keepProcess)
            {
                if (shouldTerminateImmediately)
                {
                    TerminateImmediately(process);
                }

                process.Dispose();
            }
        }
    }

    public void TerminateAll()
    {
        Process[] processes;
        lock (_sync)
        {
            if (_processes.Count == 0)
            {
                return;
            }

            processes = _processes.Values.ToArray();
            _processes.Clear();
        }

        AppLog.Info(
            $"Closing {processes.Length} process(es) opened by AIExplorer.");

        foreach (var process in processes)
        {
            try
            {
                if (IsRunning(process))
                {
                    _ = process.CloseMainWindow();
                }
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    $"Could not request process {GetProcessLabel(process)} " +
                    $"to close normally. {exception.Message}");
            }
        }

        WaitForProcesses(processes, GracefulShutdownTimeout);

        var forcedCount = 0;
        foreach (var process in processes)
        {
            try
            {
                if (!IsRunning(process))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                forcedCount++;
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    $"Could not terminate process {GetProcessLabel(process)}. " +
                    exception.Message);
            }
        }

        WaitForProcesses(processes, ForcedShutdownTimeout);

        var remainingCount = processes.Count(IsRunning);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        AppLog.Info(
            "Launched process cleanup completed. " +
            $"Forced={forcedCount}; Remaining={remainingCount}");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        TerminateAll();
    }

    private static void TerminateImmediately(Process process)
    {
        try
        {
            if (IsRunning(process))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(
                    (int)ForcedShutdownTimeout.TotalMilliseconds);
            }
        }
        catch
        {
            // Application shutdown is already in progress.
        }
    }

    private static void WaitForProcesses(
        IEnumerable<Process> processes,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        foreach (var process in processes)
        {
            if (!IsRunning(process))
            {
                continue;
            }

            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                process.WaitForExit(
                    Math.Max(1, (int)remaining.TotalMilliseconds));
            }
            catch
            {
                // The final running-state check records any process that remains.
            }
        }
    }

    private static bool IsRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string GetProcessLabel(Process process)
    {
        try
        {
            return $"{GetProcessName(process)} ({process.Id})";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}
