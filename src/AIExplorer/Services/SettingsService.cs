using System.Text.Json;
using System.Text.Json.Serialization;
using AIExplorer.Models;

namespace AIExplorer.Services;

public sealed class SettingsService
{
    private const int CopyBufferSize = 1024 * 1024;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _storageOverridePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsService() : this(null, null)
    {
    }

    public SettingsService(
        string? dataDirectory,
        string? storageOverridePath)
    {
        _storageOverridePath =
            storageOverridePath ?? ResolveDefaultStorageOverridePath();
        DataDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? ResolveDataDirectory(_storageOverridePath)
            : PrepareDirectory(dataDirectory);
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
    }

    public string DataDirectory { get; }

    public string SettingsPath { get; }

    public long GetDataDirectorySize()
    {
        try
        {
            return Directory
                .EnumerateFiles(
                    DataDirectory,
                    "*",
                    CreateEnumerationOptions())
                .Sum(path =>
                {
                    try
                    {
                        return new FileInfo(path).Length;
                    }
                    catch (IOException)
                    {
                        return 0L;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return 0L;
                    }
                });
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(
                       stream,
                       _jsonOptions,
                       cancellationToken)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            TryMoveCorruptedSettings();
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var temporaryPath = SettingsPath + ".tmp";

            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             16_384,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, SettingsPath, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<StorageMigrationResult> ChangeDataDirectoryAsync(
        string newDirectory,
        IProgress<StorageMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newDirectory))
        {
            throw new ArgumentException(
                "새 저장 위치를 지정해 주세요.",
                nameof(newDirectory));
        }

        var sourceDirectory = NormalizePath(DataDirectory);
        var targetDirectory = NormalizePath(newDirectory);
        if (PathsEqual(sourceDirectory, targetDirectory))
        {
            return new StorageMigrationResult(
                targetDirectory,
                0,
                0,
                false);
        }

        if (IsNestedPath(sourceDirectory, targetDirectory) ||
            IsNestedPath(targetDirectory, sourceDirectory))
        {
            throw new InvalidOperationException(
                "현재 저장 폴더의 상위 또는 하위 폴더는 새 저장 위치로 선택할 수 없습니다.");
        }

        EnsureWritableDirectory(targetDirectory);

        var files = Directory
            .EnumerateFiles(
                sourceDirectory,
                "*",
                CreateEnumerationOptions())
            .Where(path =>
                !PathsEqual(path, _storageOverridePath) &&
                !path.EndsWith(
                    ".write-test",
                    StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(
                    ".tmp",
                    StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .ToArray();
        var totalBytes = files.Sum(file => file.Length);
        EnsureAvailableSpace(targetDirectory, totalBytes);

        long copiedBytes = 0;
        var copiedFiles = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(
                sourceDirectory,
                file.FullName);
            var destinationPath = Path.Combine(
                targetDirectory,
                relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath) ??
                targetDirectory);

            await using var source = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[CopyBufferSize];
            while (true)
            {
                var read = await source.ReadAsync(
                    buffer,
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                copiedBytes += read;
                progress?.Report(new StorageMigrationProgress(
                    copiedBytes,
                    totalBytes,
                    copiedFiles,
                    files.Length,
                    relativePath));
            }

            await destination.FlushAsync(cancellationToken);
            TryPreserveLastWriteTime(
                destinationPath,
                file.LastWriteTimeUtc);
            copiedFiles++;
            progress?.Report(new StorageMigrationProgress(
                copiedBytes,
                totalBytes,
                copiedFiles,
                files.Length,
                relativePath));
        }

        await WriteStorageOverrideAsync(
            targetDirectory,
            cancellationToken);
        return new StorageMigrationResult(
            targetDirectory,
            copiedBytes,
            copiedFiles,
            true);
    }

    private static string ResolveDataDirectory(
        string storageOverridePath)
    {
        var customPath = TryReadStorageOverride(storageOverridePath);
        if (!string.IsNullOrWhiteSpace(customPath) &&
            TryPrepareWritableDirectory(customPath, out var preparedCustomPath))
        {
            return preparedCustomPath;
        }

        var portablePath = Path.Combine(AppContext.BaseDirectory, "_AIExplorer_Data");
        if (TryPrepareWritableDirectory(
                portablePath,
                out var preparedPortablePath))
        {
            return preparedPortablePath;
        }

        return ResolveFallbackDirectory();
    }

    private static string ResolveFallbackDirectory()
    {
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIExplorer");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string ResolveDefaultStorageOverridePath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "AIExplorer.Bootstrap",
            "storage-location.txt");

    private static string? TryReadStorageOverride(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var value = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task WriteStorageOverrideAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var parentDirectory =
            Path.GetDirectoryName(_storageOverridePath) ??
            throw new InvalidOperationException(
                "저장 위치 설정 파일 경로를 확인하지 못했습니다.");
        Directory.CreateDirectory(parentDirectory);
        var temporaryPath = _storageOverridePath + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            dataDirectory,
            cancellationToken);
        File.Move(
            temporaryPath,
            _storageOverridePath,
            overwrite: true);
    }

    private static string PrepareDirectory(string path)
    {
        var normalized = NormalizePath(path);
        Directory.CreateDirectory(normalized);
        return normalized;
    }

    private static bool TryPrepareWritableDirectory(
        string path,
        out string preparedPath)
    {
        try
        {
            preparedPath = NormalizePath(path);
            EnsureWritableDirectory(preparedPath);
            return true;
        }
        catch (ArgumentException)
        {
            preparedPath = string.Empty;
            return false;
        }
        catch (IOException)
        {
            preparedPath = string.Empty;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            preparedPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            preparedPath = string.Empty;
            return false;
        }
    }

    private static void EnsureWritableDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var probePath = Path.Combine(
            path,
            $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probePath, string.Empty);
        }
        finally
        {
            try
            {
                File.Delete(probePath);
            }
            catch
            {
                // A failed cleanup does not change the write test result.
            }
        }
    }

    private static void EnsureAvailableSpace(
        string targetDirectory,
        long requiredBytes)
    {
        if (requiredBytes <= 0)
        {
            return;
        }

        try
        {
            var root = Path.GetPathRoot(targetDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var drive = new DriveInfo(root);
            if (drive.IsReady &&
                drive.AvailableFreeSpace <
                requiredBytes + 64L * 1024 * 1024)
            {
                throw new IOException(
                    "새 저장 위치의 여유 공간이 부족합니다.");
            }
        }
        catch (ArgumentException)
        {
            // UNC paths and virtual providers may not expose drive capacity.
        }
        catch (UnauthorizedAccessException)
        {
            // A successful write probe is sufficient when capacity is hidden.
        }
    }

    private static EnumerationOptions CreateEnumerationOptions() =>
        new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip =
                FileAttributes.ReparsePoint |
                FileAttributes.System
        };

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path.Trim()));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsNestedPath(string parent, string candidate)
    {
        var parentWithSeparator =
            NormalizePath(parent) + Path.DirectorySeparatorChar;
        return NormalizePath(candidate).StartsWith(
            parentWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryPreserveLastWriteTime(
        string path,
        DateTime lastWriteTimeUtc)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }
        catch
        {
            // Timestamps are optional on some network and removable drives.
        }
    }

    private void TryMoveCorruptedSettings()
    {
        try
        {
            var backupPath = Path.Combine(
                DataDirectory,
                $"settings.corrupted.{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(SettingsPath, backupPath, true);
        }
        catch
        {
            // A damaged settings file must never prevent the explorer from starting.
        }
    }
}

public sealed record StorageMigrationProgress(
    long CopiedBytes,
    long TotalBytes,
    int CopiedFiles,
    int TotalFiles,
    string CurrentFile);

public sealed record StorageMigrationResult(
    string DataDirectory,
    long CopiedBytes,
    int CopiedFiles,
    bool LocationChanged);
