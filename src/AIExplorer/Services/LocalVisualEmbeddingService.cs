using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AIExplorer.Services;

public interface IVisualEmbeddingService : IDisposable
{
    bool IsAvailable { get; }

    string ModelId { get; }

    bool CanAnalyze(string extension);

    Task<float[]> EmbedQueryAsync(
        string query,
        CancellationToken cancellationToken);

    Task<float[]> EmbedPromptAsync(
        string prompt,
        CancellationToken cancellationToken);

    Task<float[]?> EmbedFileAsync(
        string path,
        CancellationToken cancellationToken);

    async Task<IReadOnlyList<float[]>> EmbedFileRegionsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var vector = await EmbedFileAsync(path, cancellationToken);
        return vector is null ? [] : [vector];
    }
}

public sealed class LocalVisualEmbeddingService : IVisualEmbeddingService
{
    public const string VisualModelId =
        "siglip2-base-patch16-224-int8-768d";

    private readonly string _modelPath;
    private readonly string _tokenizerPath;
    private readonly VisualFrameLoader _frameLoader = new();
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private readonly object _loadLock = new();
    private InferenceSession? _session;
    private SiglipTokenizer? _tokenizer;
    private string? _textOutputName;
    private string? _imageOutputName;
    private bool _usingDirectMl;
    private bool _disposed;

    public LocalVisualEmbeddingService(
        string modelPath,
        string tokenizerPath)
    {
        _modelPath = modelPath;
        _tokenizerPath = tokenizerPath;
    }

    public bool IsAvailable =>
        !_disposed &&
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) &&
        File.Exists(_modelPath) &&
        File.Exists(_tokenizerPath);

    public string ModelId => VisualModelId;

    public bool CanAnalyze(string extension) =>
        _frameLoader.CanAnalyze(extension);

    public async Task<float[]> EmbedQueryAsync(
        string query,
        CancellationToken cancellationToken) =>
        await EmbedPromptAsync(
            VisualQueryPromptBuilder.Build(query),
            cancellationToken);

    public async Task<float[]> EmbedPromptAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();
            var tokens = _tokenizer!.Encode(prompt);
            var emptyImage =
                new float[
                    3 *
                    VisualFrameLoader.ImageSize *
                    VisualFrameLoader.ImageSize];
            return await Task.Run(
                () => RunWithCpuFallback(
                    tokens,
                    emptyImage,
                    isTextOutput: true,
                    cancellationToken),
                cancellationToken);
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    public async Task<float[]?> EmbedFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var vectors = await EmbedFileRegionsAsync(path, cancellationToken);
        return vectors.Count == 0 ? null : AverageAndNormalize(vectors);
    }

    public async Task<IReadOnlyList<float[]>> EmbedFileRegionsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        var frames = await _frameLoader.LoadAsync(
            path,
            cancellationToken);
        if (frames.Count == 0)
        {
            return [];
        }

        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();
            var placeholder = _tokenizer!.Encode("image");
            var vectors = new List<float[]>(frames.Count);
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                vectors.Add(
                    await Task.Run(
                        () => RunWithCpuFallback(
                            placeholder,
                            frame,
                            isTextOutput: false,
                            cancellationToken),
                        cancellationToken));
            }

            return vectors;
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private float[] RunWithCpuFallback(
        TokenizedText tokens,
        float[] pixels,
        bool isTextOutput,
        CancellationToken cancellationToken)
    {
        try
        {
            return RunCore(
                tokens,
                pixels,
                isTextOutput,
                cancellationToken);
        }
        catch (OnnxRuntimeException exception) when (_usingDirectMl)
        {
            AppLog.Warning(
                "내장그래픽 DirectML 추론에 실패해 CPU로 전환합니다. " +
                exception.Message);
            RecreateCpuSession();
            return RunCore(
                tokens,
                pixels,
                isTextOutput,
                cancellationToken);
        }
    }

    private float[] RunCore(
        TokenizedText tokens,
        float[] pixels,
        bool isTextOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputIds = new DenseTensor<long>(
            tokens.InputIds,
            [1, SiglipTokenizer.ContextLength]);
        var attentionMask = new DenseTensor<long>(
            tokens.AttentionMask,
            [1, SiglipTokenizer.ContextLength]);
        var pixelValues = new DenseTensor<float>(
            pixels,
            [
                1,
                3,
                VisualFrameLoader.ImageSize,
                VisualFrameLoader.ImageSize
            ]);
        var inputs = new List<NamedOnnxValue>();
        foreach (var inputName in _session!.InputMetadata.Keys)
        {
            if (inputName.Equals(
                    "input_ids",
                    StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(
                    inputName,
                    inputIds));
            }
            else if (inputName.Equals(
                         "attention_mask",
                         StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(
                    inputName,
                    attentionMask));
            }
            else if (inputName.Equals(
                         "pixel_values",
                         StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(
                    inputName,
                    pixelValues));
            }
            else
            {
                throw new InvalidDataException(
                    $"SigLIP 2가 지원하지 않는 입력을 요구합니다: {inputName}");
            }
        }

        var outputName = isTextOutput
            ? _textOutputName!
            : _imageOutputName!;
        using var results = _session.Run(inputs, [outputName]);
        cancellationToken.ThrowIfCancellationRequested();
        var output = results.FirstOrDefault(
            result => string.Equals(
                result.Name,
                outputName,
                StringComparison.Ordinal));
        if (output is null)
        {
            throw new InvalidDataException(
                $"SigLIP 2가 {outputName} 출력을 반환하지 않았습니다.");
        }

        return Normalize(output.AsTensor<float>().ToArray());
    }

    private void EnsureLoaded()
    {
        if (_session is not null && _tokenizer is not null)
        {
            return;
        }

        lock (_loadLock)
        {
            if (_session is not null && _tokenizer is not null)
            {
                return;
            }

            _tokenizer = new SiglipTokenizer(_tokenizerPath);
            try
            {
                var directMlOptions = CreateSessionOptions(
                    useDirectMl: true);
                try
                {
                    _session = new InferenceSession(
                        _modelPath,
                        directMlOptions);
                    _usingDirectMl = true;
                }
                finally
                {
                    directMlOptions.Dispose();
                }
            }
            catch (Exception exception) when (
                exception is OnnxRuntimeException or
                EntryPointNotFoundException or
                DllNotFoundException or
                NotSupportedException)
            {
                AppLog.Warning(
                    "DirectML 내장그래픽을 사용할 수 없어 SigLIP 2를 CPU로 " +
                    "실행합니다. " + exception.Message);
                _session?.Dispose();
                _session = null;
                using var cpuOptions = CreateSessionOptions(
                    useDirectMl: false);
                _session = new InferenceSession(_modelPath, cpuOptions);
                _usingDirectMl = false;
            }

            try
            {
                ResolveOutputNames();
            }
            catch
            {
                _session?.Dispose();
                _session = null;
                _tokenizer = null;
                _textOutputName = null;
                _imageOutputName = null;
                throw;
            }
        }
    }

    private static SessionOptions CreateSessionOptions(bool useDirectMl)
    {
        var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel =
                GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = useDirectMl
                ? 1
                : Math.Clamp(Environment.ProcessorCount / 3, 1, 2),
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
            using var cpuOptions = CreateSessionOptions(useDirectMl: false);
            _session = new InferenceSession(_modelPath, cpuOptions);
            _usingDirectMl = false;
            ResolveOutputNames();
        }
    }

    private void ResolveOutputNames()
    {
        _textOutputName = ResolveOutputName("text_embeds", "text");
        _imageOutputName = ResolveOutputName("image_embeds", "image");
    }

    private string ResolveOutputName(string preferredName, string keyword)
    {
        if (_session!.OutputMetadata.ContainsKey(preferredName))
        {
            return preferredName;
        }

        var candidate = _session.OutputMetadata.Keys.FirstOrDefault(name =>
            name.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("feature", StringComparison.OrdinalIgnoreCase)));
        return candidate ?? throw new InvalidDataException(
            $"SigLIP 2 ONNX에서 {keyword} 임베딩 출력을 찾지 못했습니다. " +
            $"출력: {string.Join(", ", _session.OutputMetadata.Keys)}");
    }

    private static float[] AverageAndNormalize(
        IReadOnlyList<float[]> vectors)
    {
        if (vectors.Count == 0)
        {
            throw new InvalidDataException(
                "시각 AI가 이미지 벡터를 만들지 못했습니다.");
        }

        var dimensions = vectors[0].Length;
        var average = new float[dimensions];
        foreach (var vector in vectors)
        {
            if (vector.Length != dimensions)
            {
                throw new InvalidDataException(
                    "시각 AI 벡터 차원이 일치하지 않습니다.");
            }

            for (var index = 0; index < dimensions; index++)
            {
                average[index] += vector[index] / vectors.Count;
            }
        }

        return Normalize(average);
    }

    private static float[] Normalize(float[] vector)
    {
        var sumOfSquares = 0d;
        foreach (var value in vector)
        {
            sumOfSquares += value * value;
        }

        var magnitude = Math.Sqrt(sumOfSquares);
        if (magnitude <= double.Epsilon)
        {
            throw new InvalidDataException(
                "시각 AI가 빈 임베딩 벡터를 반환했습니다.");
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / magnitude);
        }

        return vector;
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "SigLIP 2 시각 검색 모델이 준비되지 않았습니다.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session?.Dispose();
        _session = null;
        if (_inferenceLock.Wait(0))
        {
            _inferenceLock.Dispose();
        }
    }
}
