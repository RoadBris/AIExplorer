using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AIExplorer.Services;

public interface IImageTaggingService : IDisposable
{
    bool IsAvailable { get; }

    string ModelId { get; }

    bool CanAnalyze(string extension);

    Task<ImageTagEvidence?> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed class LocalImageTaggingService : IImageTaggingService
{
    public const string TaggerModelId = "wd-vit-tagger-v3-790b0e9";

    private const int DefaultImageSize = 448;
    private const double GeneralThreshold = 0.35d;
    private const double CharacterThreshold = 0.62d;
    private const int MaximumGeneralTags = 32;
    private const int MaximumCharacterTags = 8;
    private const long MaximumImageBytes = 64L * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
            ".webp", ".heic"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _modelPath;
    private readonly string _labelsPath;
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private readonly object _loadLock = new();
    private InferenceSession? _session;
    private IReadOnlyList<ImageTagLabel>? _labels;
    private string? _inputName;
    private string? _outputName;
    private int _imageSize = DefaultImageSize;
    private bool _usingDirectMl;
    private bool _disposed;

    public LocalImageTaggingService(string modelPath, string labelsPath)
    {
        _modelPath = modelPath;
        _labelsPath = labelsPath;
    }

    public bool IsAvailable =>
        !_disposed &&
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) &&
        File.Exists(_modelPath) &&
        File.Exists(_labelsPath);

    public string ModelId => TaggerModelId;

    public bool CanAnalyze(string extension) =>
        ImageExtensions.Contains(NormalizeExtension(extension));

    public async Task<ImageTagEvidence?> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        var file = new FileInfo(path);
        if (!file.Exists ||
            file.Length <= 0 ||
            file.Length > MaximumImageBytes ||
            !CanAnalyze(file.Extension))
        {
            return null;
        }

        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();
            var pixels = await Task.Run(
                () => PrepareImage(path, _imageSize, cancellationToken),
                cancellationToken);
            if (pixels is null)
            {
                return null;
            }

            var scores = await Task.Run(
                () => RunWithCpuFallback(pixels, cancellationToken),
                cancellationToken);
            return BuildEvidence(scores);
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private ImageTagEvidence BuildEvidence(IReadOnlyList<float> scores)
    {
        var count = Math.Min(scores.Count, _labels!.Count);
        var predictions = Enumerable.Range(0, count)
            .Select(index => new ImageTagPrediction(
                _labels[index].Name,
                _labels[index].Category,
                Math.Clamp(scores[index], 0f, 1f)))
            .ToArray();
        var rating = predictions
            .Where(item => item.Category == ImageTagCategory.Rating)
            .OrderByDescending(item => item.Confidence)
            .Take(1);
        var general = predictions
            .Where(item =>
                item.Category == ImageTagCategory.General &&
                item.Confidence >= GeneralThreshold)
            .OrderByDescending(item => item.Confidence)
            .Take(MaximumGeneralTags);
        var characters = predictions
            .Where(item =>
                item.Category == ImageTagCategory.Character &&
                item.Confidence >= CharacterThreshold)
            .OrderByDescending(item => item.Confidence)
            .Take(MaximumCharacterTags);
        var selected = rating
            .Concat(characters)
            .Concat(general)
            .ToArray();
        return selected.Length == 0
            ? ImageTagEvidence.Empty
            : new ImageTagEvidence(selected);
    }

    private float[] RunWithCpuFallback(
        float[] pixels,
        CancellationToken cancellationToken)
    {
        try
        {
            return RunCore(pixels, cancellationToken);
        }
        catch (OnnxRuntimeException exception) when (_usingDirectMl)
        {
            AppLog.Warning(
                "캐릭터 태거의 DirectML 추론에 실패해 CPU로 전환합니다. " +
                exception.Message);
            RecreateCpuSession();
            return RunCore(pixels, cancellationToken);
        }
    }

    private float[] RunCore(
        float[] pixels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tensor = new DenseTensor<float>(
            pixels,
            [1, _imageSize, _imageSize, 3]);
        using var results = _session!.Run(
            [NamedOnnxValue.CreateFromTensor(_inputName!, tensor)],
            [_outputName!]);
        cancellationToken.ThrowIfCancellationRequested();
        var output = results.FirstOrDefault(result =>
            result.Name.Equals(_outputName, StringComparison.Ordinal));
        if (output is null)
        {
            throw new InvalidDataException(
                "캐릭터 태거가 예측 결과를 반환하지 않았습니다.");
        }

        return output.AsTensor<float>().ToArray();
    }

    private void EnsureLoaded()
    {
        if (_session is not null && _labels is not null)
        {
            return;
        }

        lock (_loadLock)
        {
            if (_session is not null && _labels is not null)
            {
                return;
            }

            var labels = LoadLabels(_labelsPath);
            try
            {
                using var directMlOptions = CreateSessionOptions(
                    useDirectMl: true);
                _session = new InferenceSession(_modelPath, directMlOptions);
                _usingDirectMl = true;
            }
            catch (Exception exception) when (
                exception is OnnxRuntimeException or
                EntryPointNotFoundException or
                DllNotFoundException or
                NotSupportedException)
            {
                AppLog.Warning(
                    "DirectML 내장그래픽을 사용할 수 없어 캐릭터 태거를 CPU로 " +
                    "실행합니다. " + exception.Message);
                _session?.Dispose();
                using var cpuOptions = CreateSessionOptions(
                    useDirectMl: false);
                _session = new InferenceSession(_modelPath, cpuOptions);
                _usingDirectMl = false;
            }

            try
            {
                _inputName = _session.InputMetadata.Keys.Single();
                _outputName = _session.OutputMetadata.Keys.Single();
                var dimensions = _session.InputMetadata[_inputName].Dimensions;
                if (dimensions.Length != 4)
                {
                    throw new InvalidDataException(
                        "캐릭터 태거의 이미지 입력 형식을 확인하지 못했습니다.");
                }

                var height = dimensions[1];
                var width = dimensions[2];
                if (height <= 0 || width <= 0 || height != width)
                {
                    throw new InvalidDataException(
                        "캐릭터 태거가 요구하는 이미지 크기를 확인하지 못했습니다.");
                }

                _imageSize = height;
                _labels = labels;
            }
            catch
            {
                _session.Dispose();
                _session = null;
                _labels = null;
                _inputName = null;
                _outputName = null;
                throw;
            }
        }
    }

    private static IReadOnlyList<ImageTagLabel> LoadLabels(string path)
    {
        var labels = new List<ImageTagLabel>(11_000);
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var columns = ParseCsvLine(line);
            if (columns.Count < 4 ||
                !int.TryParse(
                    columns[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var categoryValue))
            {
                continue;
            }

            var category = categoryValue switch
            {
                4 => ImageTagCategory.Character,
                9 => ImageTagCategory.Rating,
                _ => ImageTagCategory.General
            };
            labels.Add(new ImageTagLabel(columns[1].Trim(), category));
        }

        if (labels.Count == 0)
        {
            throw new InvalidDataException(
                "캐릭터 태거의 라벨 파일을 읽지 못했습니다.");
        }

        return labels;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>(4);
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static float[]? PrepareImage(
        string path,
        int imageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BitmapFrame frame;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                65_536,
                FileOptions.SequentialScan);
            frame = BitmapFrame.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat |
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
        }
        catch (Exception exception) when (
            exception is NotSupportedException or
            FileFormatException or
            ArgumentException)
        {
            return null;
        }

        var scale = Math.Min(
            imageSize / (double)Math.Max(1, frame.PixelWidth),
            imageSize / (double)Math.Max(1, frame.PixelHeight));
        var width = Math.Max(1d, frame.PixelWidth * scale);
        var height = Math.Max(1d, frame.PixelHeight * scale);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                Brushes.White,
                pen: null,
                new System.Windows.Rect(0, 0, imageSize, imageSize));
            context.DrawImage(
                frame,
                new System.Windows.Rect(
                    (imageSize - width) / 2d,
                    (imageSize - height) / 2d,
                    width,
                    height));
        }

        var rendered = new RenderTargetBitmap(
            imageSize,
            imageSize,
            96d,
            96d,
            PixelFormats.Pbgra32);
        rendered.Render(visual);
        BitmapSource source = new FormatConvertedBitmap(
            rendered,
            PixelFormats.Bgra32,
            destinationPalette: null,
            alphaThreshold: 0);
        var stride = imageSize * 4;
        var bytes = new byte[stride * imageSize];
        source.CopyPixels(bytes, stride, 0);
        var pixels = new float[imageSize * imageSize * 3];
        for (var index = 0; index < imageSize * imageSize; index++)
        {
            var byteIndex = index * 4;
            var pixelIndex = index * 3;
            pixels[pixelIndex] = bytes[byteIndex];
            pixels[pixelIndex + 1] = bytes[byteIndex + 1];
            pixels[pixelIndex + 2] = bytes[byteIndex + 2];
        }

        return pixels;
    }

    private static SessionOptions CreateSessionOptions(bool useDirectMl)
    {
        var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = useDirectMl
                ? 1
                : Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
            InterOpNumThreads = 1,
            EnableCpuMemArena = true,
            EnableMemoryPattern = !useDirectMl,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
        };
        if (useDirectMl)
        {
            options.AppendExecutionProvider_DML(0);
        }

        return options;
    }

    private void RecreateCpuSession()
    {
        lock (_loadLock)
        {
            _session?.Dispose();
            using var options = CreateSessionOptions(useDirectMl: false);
            _session = new InferenceSession(_modelPath, options);
            _usingDirectMl = false;
        }
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "캐릭터 태거 모델이 준비되지 않았습니다.");
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_loadLock)
        {
            _session?.Dispose();
            _session = null;
            _labels = null;
        }
        _inferenceLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record ImageTagLabel(
        string Name,
        ImageTagCategory Category);
}

public enum ImageTagCategory
{
    General,
    Character,
    Rating
}

public sealed record ImageTagPrediction(
    string Name,
    ImageTagCategory Category,
    double Confidence);

public sealed record ImageTagEvidence(
    IReadOnlyList<ImageTagPrediction> Predictions)
{
    public static ImageTagEvidence Empty { get; } = new([]);
}
