using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIExplorer.Services;

public interface ITextEmbeddingService : IDisposable
{
    bool IsAvailable { get; }

    string ModelId { get; }

    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken,
        EmbeddingResolution resolution = EmbeddingResolution.Compact);
}

public enum EmbeddingPurpose
{
    Query,
    Passage
}

public enum EmbeddingResolution
{
    Compact,
    Full
}

public sealed class LocalEmbeddingService : ITextEmbeddingService
{
    private const string ServerModelAlias = "aiexplorer-embed";
    private const int StoredEmbeddingDimensions = 768;
    private const int MaximumCharactersPerSegment = 240;
    private const int MaximumPreparedTextsPerRequest = 4;
    private const int MaximumFullPassageSegments = 5;
    private static readonly TimeSpan ServerStartupTimeout =
        TimeSpan.FromSeconds(120);

    private readonly AiModelManager _modelManager;
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private Process? _serverProcess;
    private HttpClient? _httpClient;
    private bool _preferLowPriority;
    private bool _disposed;

    public LocalEmbeddingService(AiModelManager modelManager)
    {
        _modelManager = modelManager;
    }

    public bool IsAvailable =>
        !_disposed &&
        _modelManager.IsInstalled;

    public string ModelId => AiModelManager.ModelId;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken,
        EmbeddingResolution resolution = EmbeddingResolution.Compact)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (texts.Count == 0)
        {
            return [];
        }

        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "Multilingual E5 의미 모델이 설치되어 있지 않습니다.");
        }

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureServerStartedAsync(cancellationToken);
            var groups = texts
                .Select(text => PrepareSegments(text, purpose, resolution))
                .ToArray();
            var workItems = groups
                .SelectMany((segments, ownerIndex) =>
                    segments.Select(segment =>
                        new PreparedEmbedding(ownerIndex, segment)))
                .ToArray();
            var accumulated = new double[texts.Count][];
            var counts = new int[texts.Count];

            for (var offset = 0;
                 offset < workItems.Length;
                 offset += MaximumPreparedTextsPerRequest)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = workItems
                    .Skip(offset)
                    .Take(MaximumPreparedTextsPerRequest)
                    .ToArray();
                var vectors = await RequestPreparedEmbeddingsAsync(
                    batch.Select(item => item.Text).ToArray(),
                    cancellationToken);
                if (vectors.Count != batch.Length)
                {
                    throw new InvalidDataException(
                        "Multilingual E5 임베딩 결과 개수가 입력과 일치하지 않습니다.");
                }

                for (var index = 0; index < batch.Length; index++)
                {
                    var vector = vectors[index];
                    if (vector.Length != StoredEmbeddingDimensions)
                    {
                        throw new InvalidDataException(
                            $"Multilingual E5가 {vector.Length}차원 벡터를 " +
                            $"반환했습니다. 예상값은 {StoredEmbeddingDimensions}차원입니다.");
                    }

                    var ownerIndex = batch[index].OwnerIndex;
                    var accumulator = accumulated[ownerIndex] ??=
                        new double[vector.Length];
                    for (var dimension = 0;
                         dimension < vector.Length;
                         dimension++)
                    {
                        accumulator[dimension] += vector[dimension];
                    }

                    counts[ownerIndex]++;
                }
            }

            var results = new float[texts.Count][];
            for (var index = 0; index < results.Length; index++)
            {
                if (accumulated[index] is null || counts[index] == 0)
                {
                    throw new InvalidDataException(
                        "Multilingual E5가 일부 문장의 임베딩을 반환하지 않았습니다.");
                }

                var average = accumulated[index]
                    .Select(value => (float)(value / counts[index]))
                    .ToArray();
                results[index] = Normalize(average);
            }

            return results;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task<IReadOnlyList<float[]>> RequestPreparedEmbeddingsAsync(
        IReadOnlyList<string> preparedTexts,
        CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            model = ServerModelAlias,
            input = preparedTexts,
            encoding_format = "float"
        });
        using var content = new StringContent(
            requestJson,
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient!.PostAsync(
            "v1/embeddings",
            content,
            cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (IsPromptCapacityError(responseJson) &&
                preparedTexts.Count > 1)
            {
                var recovered = new List<float[]>(preparedTexts.Count);
                foreach (var preparedText in preparedTexts)
                {
                    recovered.AddRange(
                        await RequestPreparedEmbeddingsAsync(
                            [preparedText],
                            cancellationToken));
                }

                return recovered;
            }

            if (IsPromptCapacityError(responseJson) &&
                preparedTexts.Count == 1 &&
                preparedTexts[0].Length > 120)
            {
                var shortened = ShortenPreparedText(preparedTexts[0]);
                return await RequestPreparedEmbeddingsAsync(
                    [shortened],
                    cancellationToken);
            }

            throw new InvalidOperationException(
                "Multilingual E5 임베딩 요청이 실패했습니다. " +
                TrimForMessage(responseJson));
        }

        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Multilingual E5 응답에 임베딩 데이터가 없습니다.");
        }

        var vectors = new float[preparedTexts.Count][];
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("index", out var indexElement) ||
                !indexElement.TryGetInt32(out var index) ||
                index < 0 ||
                index >= vectors.Length ||
                !item.TryGetProperty("embedding", out var embedding) ||
                embedding.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    "Multilingual E5 임베딩 응답 형식이 올바르지 않습니다.");
            }

            vectors[index] = Normalize(
                embedding
                    .EnumerateArray()
                    .Select(value => value.GetSingle())
                    .ToArray());
        }

        if (vectors.Any(vector => vector is null || vector.Length == 0))
        {
            throw new InvalidDataException(
                "Multilingual E5가 일부 문장의 임베딩을 반환하지 않았습니다.");
        }

        return vectors;
    }

    private async Task EnsureServerStartedAsync(
        CancellationToken cancellationToken)
    {
        if (IsServerRunning())
        {
            return;
        }

        await _startupLock.WaitAsync(cancellationToken);
        try
        {
            if (IsServerRunning())
            {
                return;
            }

            StopOwnedServer();
            var port = ReserveLoopbackPort();
            var apiKey = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(24));
            var startInfo = new ProcessStartInfo
            {
                FileName = _modelManager.ServerExecutablePath,
                WorkingDirectory =
                    Path.GetDirectoryName(_modelManager.ServerExecutablePath) ??
                    AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            AddServerArguments(startInfo, port, apiKey);

            _serverProcess = Process.Start(startInfo) ??
                             throw new InvalidOperationException(
                                 "E5 CPU 실행기를 시작하지 못했습니다.");
            ApplyProcessPriority();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
                Timeout = TimeSpan.FromMinutes(3)
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                await WaitUntilHealthyAsync(
                    _serverProcess,
                    _httpClient,
                    cancellationToken);
            }
            catch
            {
                StopOwnedServer();
                throw;
            }
        }
        finally
        {
            _startupLock.Release();
        }
    }

    private void AddServerArguments(
        ProcessStartInfo startInfo,
        int port,
        string apiKey)
    {
        var arguments = new[]
        {
            "-m",
            _modelManager.ModelPath,
            "--alias",
            ServerModelAlias,
            "--host",
            "127.0.0.1",
            "--port",
            port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--embedding",
            "--pooling",
            "mean",
            "--ctx-size",
            "512",
            "--batch-size",
            "2048",
            "--ubatch-size",
            "512",
            "--threads",
            Math.Clamp(Environment.ProcessorCount / 2, 1, 6)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--parallel",
            "1",
            "--sleep-idle-seconds",
            "300",
            "--api-key",
            apiKey,
            "--no-webui",
            "--log-file",
            _modelManager.RuntimeLogPath,
            "--log-verbosity",
            "2"
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static async Task WaitUntilHealthyAsync(
        Process process,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < ServerStartupTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"E5 CPU 실행기가 종료되었습니다. 종료 코드: {process.ExitCode}");
            }

            try
            {
                using var response = await client.GetAsync(
                    "health",
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The local server has not opened its port yet.
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested)
            {
                // A short health request can time out while the model loads.
            }

            await Task.Delay(300, cancellationToken);
        }

        throw new TimeoutException(
            "Multilingual E5 모델을 2분 안에 불러오지 못했습니다.");
    }

    private bool IsServerRunning()
    {
        try
        {
            return _serverProcess is { HasExited: false } &&
                   _httpClient is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static IReadOnlyList<string> PrepareSegments(
        string text,
        EmbeddingPurpose purpose,
        EmbeddingResolution resolution)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "빈 파일 정보";
        }

        var rawSegments = resolution == EmbeddingResolution.Full &&
                          purpose == EmbeddingPurpose.Passage
            ? BuildRepresentativeSegments(normalized)
            : [BuildCompactSegment(normalized)];
        var prefix = purpose == EmbeddingPurpose.Query
            ? "query: "
            : "passage: ";
        return rawSegments
            .Select(segment => prefix + segment)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeText(string text) =>
        string.Join(
            " ",
            text
                .Replace('\0', ' ')
                .Split(
                    ['\r', '\n', '\t'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));

    private static string BuildCompactSegment(string text)
    {
        if (text.Length <= MaximumCharactersPerSegment)
        {
            return text;
        }

        const int tailLength = 72;
        var headLength = MaximumCharactersPerSegment - tailLength - 3;
        return text[..headLength] + " … " + text[^tailLength..];
    }

    private static IReadOnlyList<string> BuildRepresentativeSegments(
        string text)
    {
        if (text.Length <= MaximumCharactersPerSegment)
        {
            return [text];
        }

        var maximumStart = text.Length - MaximumCharactersPerSegment;
        var starts = Enumerable.Range(0, MaximumFullPassageSegments)
            .Select(index => (int)Math.Round(
                maximumStart * index /
                (double)(MaximumFullPassageSegments - 1)))
            .Distinct()
            .ToArray();
        return starts
            .Select(start => text.Substring(
                start,
                Math.Min(MaximumCharactersPerSegment, text.Length - start)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsPromptCapacityError(string responseJson) =>
        responseJson.Contains(
            "prompt is too large",
            StringComparison.OrdinalIgnoreCase) ||
        responseJson.Contains(
            "exceeds the physical batch size",
            StringComparison.OrdinalIgnoreCase) ||
        responseJson.Contains(
            "too many tokens",
            StringComparison.OrdinalIgnoreCase);

    private static string ShortenPreparedText(string preparedText)
    {
        var prefixEnd = preparedText.IndexOf(':');
        var prefix = prefixEnd >= 0
            ? preparedText[..(prefixEnd + 1)] + " "
            : string.Empty;
        var body = prefix.Length > 0
            ? preparedText[prefix.Length..]
            : preparedText;
        var newLength = Math.Max(80, body.Length * 2 / 3);
        return prefix + BuildCompactSegment(body[..newLength]);
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
                "Multilingual E5가 빈 임베딩 벡터를 반환했습니다.");
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / magnitude);
        }

        return vector;
    }

    private static string TrimForMessage(string text)
    {
        var singleLine = text
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return singleLine.Length <= 240
            ? singleLine
            : singleLine[..240] + "…";
    }

    private void StopOwnedServer()
    {
        _httpClient?.Dispose();
        _httpClient = null;

        if (_serverProcess is null)
        {
            return;
        }

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(3_000);
            }
        }
        catch
        {
            // The process may already have exited during application shutdown.
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        StopOwnedServer();
    }

    public void SetLowPriority(bool lowPriority)
    {
        if (_disposed || _preferLowPriority == lowPriority)
        {
            return;
        }

        _preferLowPriority = lowPriority;
        ApplyProcessPriority();
    }

    private void ApplyProcessPriority()
    {
        try
        {
            if (_serverProcess is { HasExited: false })
            {
                _serverProcess.PriorityClass = _preferLowPriority
                    ? ProcessPriorityClass.BelowNormal
                    : ProcessPriorityClass.Normal;
            }
        }
        catch
        {
            // Some managed PCs do not allow changing process priority.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopOwnedServer();
    }

    private sealed record PreparedEmbedding(int OwnerIndex, string Text);
}
