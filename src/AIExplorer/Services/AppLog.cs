using System.Text;

namespace AIExplorer.Services;

public static class AppLog
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;

    public static string? LogDirectory { get; private set; }

    public static void Initialize(string dataDirectory)
    {
        lock (Sync)
        {
            if (_writer is not null)
            {
                return;
            }

            try
            {
                LogDirectory = Path.Combine(dataDirectory, "logs");
                Directory.CreateDirectory(LogDirectory);
                var logPath = Path.Combine(
                    LogDirectory,
                    $"AIExplorer_{DateTime.Now:yyyyMMdd}.log");
                _writer = new StreamWriter(
                    new FileStream(
                        logPath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
            }
            catch
            {
                _writer = null;
                LogDirectory = null;
            }
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception exception) =>
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_writer is null)
            {
                return;
            }

            WriteCore("INFO", "Application stopped.");
            _writer.Dispose();
            _writer = null;
        }
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            WriteCore(level, message);
        }
    }

    private static void WriteCore(string level, string message)
    {
        try
        {
            _writer?.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
        }
        catch
        {
            // Logging must never stop the application.
        }
    }
}
