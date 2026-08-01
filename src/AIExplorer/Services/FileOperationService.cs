using Microsoft.VisualBasic.FileIO;

namespace AIExplorer.Services;

public sealed class FileOperationService
{
    private static readonly EnumerationOptions CopyOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public string CreateFolder(string parentPath, string requestedName)
    {
        var name = ValidateLeafName(requestedName);
        var target = GetUniqueDestination(Path.Combine(parentPath, name), isDirectory: true);
        Directory.CreateDirectory(target);
        return target;
    }

    public string Rename(string sourcePath, string requestedName)
    {
        var name = ValidateLeafName(requestedName);
        var parent = Path.GetDirectoryName(sourcePath)
                     ?? throw new InvalidOperationException("상위 폴더를 확인할 수 없습니다.");
        var target = Path.Combine(parent, name);

        if (string.Equals(sourcePath, target, StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        if (File.Exists(target) || Directory.Exists(target))
        {
            throw new IOException("같은 이름의 항목이 이미 있습니다.");
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, target);
        }
        else
        {
            File.Move(sourcePath, target);
        }

        return target;
    }

    public Task DeleteToRecycleBinAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(path))
                {
                    FileSystem.DeleteDirectory(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.DoNothing);
                }
                else if (File.Exists(path))
                {
                    FileSystem.DeleteFile(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.DoNothing);
                }
            }
        }, cancellationToken);
    }

    public async Task CopyOrMoveAsync(
        IReadOnlyList<string> sourcePaths,
        string destinationDirectory,
        bool move,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("대상 폴더를 찾을 수 없습니다.");
        }

        for (var index = 0; index < sourcePaths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sourcePaths[index];
            var isDirectory = Directory.Exists(source);
            if (!isDirectory && !File.Exists(source))
            {
                continue;
            }

            var leafName = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
            var rawTarget = Path.Combine(destinationDirectory, leafName);

            if (isDirectory && IsSameOrDescendant(destinationDirectory, source))
            {
                throw new IOException("폴더를 자기 자신 또는 하위 폴더 안으로 복사할 수 없습니다.");
            }

            if (move && string.Equals(
                    Path.GetDirectoryName(source),
                    destinationDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = GetUniqueDestination(rawTarget, isDirectory);
            progress?.Report(new FileOperationProgress(
                index + 1,
                sourcePaths.Count,
                leafName,
                move ? "이동 중" : "복사 중"));

            try
            {
                if (move)
                {
                    await MoveAsync(source, target, isDirectory, cancellationToken);
                }
                else if (isDirectory)
                {
                    await CopyDirectoryAsync(source, target, cancellationToken);
                }
                else
                {
                    await CopyFileAsync(source, target, cancellationToken);
                }
            }
            catch
            {
                TryDeleteIncompleteTarget(target, isDirectory);
                throw;
            }
        }
    }

    private static async Task MoveAsync(
        string source,
        string target,
        bool isDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            if (isDirectory)
            {
                Directory.Move(source, target);
            }
            else
            {
                File.Move(source, target);
            }

            return;
        }
        catch (IOException)
        {
            // Cross-volume moves need a copy followed by a delete.
        }

        if (isDirectory)
        {
            await CopyDirectoryAsync(source, target, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(source, recursive: true);
        }
        else
        {
            await CopyFileAsync(source, target, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(source);
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", CopyOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyFileAsync(
                file,
                Path.Combine(targetDirectory, Path.GetFileName(file)),
                cancellationToken);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", CopyOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyDirectoryAsync(
                directory,
                Path.Combine(targetDirectory, Path.GetFileName(directory)),
                cancellationToken);
        }

        Directory.SetLastWriteTimeUtc(
            targetDirectory,
            Directory.GetLastWriteTimeUtc(sourceDirectory));
    }

    private static async Task CopyFileAsync(
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 128;

        try
        {
            await using var sourceStream = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var targetStream = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await sourceStream.CopyToAsync(targetStream, bufferSize, cancellationToken);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
        }
        catch
        {
            TryDeleteIncompleteTarget(target, isDirectory: false);
            throw;
        }
    }

    private static string ValidateLeafName(string value)
    {
        var name = value.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("이름을 입력해 주세요.");
        }

        if (name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith(' ') ||
            name.EndsWith('.'))
        {
            throw new ArgumentException("Windows에서 사용할 수 없는 이름입니다.");
        }

        return name;
    }

    private static string GetUniqueDestination(string requestedPath, bool isDirectory)
    {
        if (!File.Exists(requestedPath) && !Directory.Exists(requestedPath))
        {
            return requestedPath;
        }

        var parent = Path.GetDirectoryName(requestedPath)
                     ?? throw new InvalidOperationException("대상 폴더를 확인할 수 없습니다.");
        var originalName = Path.GetFileName(requestedPath);
        var extension = isDirectory ? string.Empty : Path.GetExtension(originalName);
        var stem = isDirectory ? originalName : Path.GetFileNameWithoutExtension(originalName);

        for (var number = 2; number < 10_000; number++)
        {
            var suffix = number == 2 ? " - 복사본" : $" - 복사본 ({number - 1})";
            var candidate = Path.Combine(parent, $"{stem}{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("사용 가능한 새 이름을 만들 수 없습니다.");
    }

    private static bool IsSameOrDescendant(string candidatePath, string parentPath)
    {
        var candidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var parent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteIncompleteTarget(string target, bool isDirectory)
    {
        try
        {
            if (isDirectory && Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
            else if (!isDirectory && File.Exists(target))
            {
                File.Delete(target);
            }
        }
        catch
        {
            // Cleanup is best-effort; the original source is never removed on failure.
        }
    }
}

public sealed record FileOperationProgress(
    int CurrentItem,
    int TotalItems,
    string ItemName,
    string Operation);
