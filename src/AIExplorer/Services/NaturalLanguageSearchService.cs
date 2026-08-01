using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIExplorer.Services;

public sealed class NaturalLanguageSearchService : IDisposable
{
    private const string ServerModelAlias = "aiexplorer-language";
    private static readonly TimeSpan ServerStartupTimeout =
        TimeSpan.FromSeconds(120);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiModelManager _modelManager;
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private Process? _serverProcess;
    private HttpClient? _httpClient;
    private bool _disposed;

    public NaturalLanguageSearchService(
        AiModelManager modelManager)
    {
        _modelManager = modelManager;
    }

    public bool IsAvailable =>
        !_disposed &&
        _modelManager.IsLanguageModelInstalled;

    public async Task<NaturalLanguageSearchInterpretation> InterpretAsync(
        string query,
        SearchConversationContext? context,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var deterministic =
            SearchQueryInterpreter.Interpret(query);
        var fallback = SearchPlan.FromDeterministic(
            deterministic,
            context);
        if (!IsAvailable)
        {
            return SearchPlanCompiler.Compile(
                deterministic,
                fallback,
                languageModelAvailable: false);
        }

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureServerStartedAsync(cancellationToken);
            var responseJson = await RequestPlanAsync(
                query,
                context,
                cancellationToken);
            var plan = ParsePlanResponse(
                responseJson,
                query,
                context,
                fallback);
            return SearchPlanCompiler.Compile(
                deterministic,
                plan,
                languageModelAvailable: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "자연어 해석 모델을 사용하지 못해 정확 규칙으로 계속합니다. " +
                exception.Message);
            return SearchPlanCompiler.Compile(
                deterministic,
                fallback,
                languageModelAvailable: true);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public static SearchPlan ParsePlanJson(
        string json,
        string query,
        SearchConversationContext? context = null)
    {
        var fallback = SearchPlan.FromDeterministic(
            SearchQueryInterpreter.Interpret(query),
            context);
        return ParsePlanContent(
            json,
            query,
            context,
            fallback);
    }

    private async Task<string> RequestPlanAsync(
        string query,
        SearchConversationContext? context,
        CancellationToken cancellationToken)
    {
        var userPayload = JsonSerializer.Serialize(
            new
            {
                query,
                previous_search = context is null
                    ? null
                    : new
                    {
                        query = context.PreviousQuery,
                        result_count = context.ResultCount
                    }
            },
            JsonOptions);
        var requestJson = JsonSerializer.Serialize(
            new
            {
                model = ServerModelAlias,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = BuildSystemPrompt()
                    },
                    new
                    {
                        role = "user",
                        content = userPayload
                    }
                },
                temperature = 0,
                max_tokens = 420,
                chat_template_kwargs = new
                {
                    enable_thinking = false
                },
                response_format = BuildResponseFormat()
            },
            JsonOptions);
        using var content = new StringContent(
            requestJson,
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient!.PostAsync(
            "v1/chat/completions",
            content,
            cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "자연어 검색 계획 요청이 실패했습니다. " +
                TrimForMessage(responseJson));
        }

        return responseJson;
    }

    private static SearchPlan ParsePlanResponse(
        string responseJson,
        string query,
        SearchConversationContext? context,
        SearchPlan fallback)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty(
                "choices",
                out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidDataException(
                "자연어 모델 응답에 검색 계획이 없습니다.");
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                "자연어 모델 응답 형식이 올바르지 않습니다.");
        }

        return ParsePlanContent(
            content.GetString() ?? string.Empty,
            query,
            context,
            fallback);
    }

    private static SearchPlan ParsePlanContent(
        string content,
        string query,
        SearchConversationContext? context,
        SearchPlan fallback)
    {
        var dto = JsonSerializer.Deserialize<LanguagePlanDto>(
            content,
            JsonOptions);
        if (dto is null)
        {
            return fallback;
        }

        var termGroups = (dto.TermGroups ?? [])
            .Where(group =>
                !string.IsNullOrWhiteSpace(group.Term))
            .Select(group => new SearchPlanTerm(
                SanitizeText(group.Term!, 80),
                (group.Alternatives ?? [])
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value))
                    .Select(value => SanitizeText(value, 80))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(16)
                    .ToArray()))
            .Where(group =>
                !string.IsNullOrWhiteSpace(group.Term))
            .DistinctBy(
                group => group.Term,
                StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        var target = dto.Target?.Trim().ToLowerInvariant() switch
        {
            "file" => SearchPlanTarget.File,
            "folder" => SearchPlanTarget.Folder,
            _ => SearchPlanTarget.Any
        };
        var sort = dto.Sort?.Trim().ToLowerInvariant() switch
        {
            "created_newest" => SearchPlanSort.CreatedNewest,
            "created_oldest" => SearchPlanSort.CreatedOldest,
            "modified_newest" => SearchPlanSort.ModifiedNewest,
            "modified_oldest" => SearchPlanSort.ModifiedOldest,
            "name_match" => SearchPlanSort.NameMatch,
            "path_match" => SearchPlanSort.PathMatch,
            "large_first" => SearchPlanSort.LargeFirst,
            "small_first" => SearchPlanSort.SmallFirst,
            _ => SearchPlanSort.Relevance
        };
        var confidence = Math.Clamp(
            dto.Confidence ?? 0.5d,
            0d,
            1d);
        var usePreviousResults =
            context is { ResultCount: > 0 } &&
            dto.UsePreviousResults == true;
        var interpretation = SanitizeText(
            dto.Interpretation ??
            fallback.Interpretation,
            200);
        if (confidence < 0.4d)
        {
            return fallback with
            {
                Confidence = confidence,
                Interpretation =
                    "확실하게 확인된 검색 조건만 적용",
                UsedLanguageModel = true
            };
        }

        return new SearchPlan(
            query.Trim(),
            termGroups.Length > 0
                ? termGroups
                : fallback.TermGroups,
            (dto.RequestedExtensions ?? [])
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => SanitizeText(value, 40))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray(),
            target == SearchPlanTarget.Any
                ? fallback.Target
                : target,
            sort == SearchPlanSort.Relevance
                ? fallback.Sort
                : sort,
            usePreviousResults ||
            fallback.UsePreviousResults,
            confidence,
            interpretation,
            true);
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
                    Path.GetDirectoryName(
                        _modelManager.ServerExecutablePath) ??
                    AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in BuildServerArguments(
                         port,
                         apiKey))
            {
                startInfo.ArgumentList.Add(argument);
            }

            _serverProcess = Process.Start(startInfo) ??
                             throw new InvalidOperationException(
                                 "자연어 AI 실행기를 시작하지 못했습니다.");
            TryApplyLowPriority(_serverProcess);
            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri($"http://127.0.0.1:{port}/"),
                Timeout = TimeSpan.FromMinutes(2)
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);
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

    private IReadOnlyList<string> BuildServerArguments(
        int port,
        string apiKey) =>
        [
            "-m",
            _modelManager.LanguageModelPath,
            "--alias",
            ServerModelAlias,
            "--host",
            "127.0.0.1",
            "--port",
            port.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            "--ctx-size",
            "4096",
            "--batch-size",
            "512",
            "--ubatch-size",
            "256",
            "--threads",
            Math.Clamp(
                    Environment.ProcessorCount / 2,
                    1,
                    8)
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            "--parallel",
            "1",
            "--jinja",
            "--sleep-idle-seconds",
            "180",
            "--api-key",
            apiKey,
            "--no-webui",
            "--log-file",
            _modelManager.LanguageRuntimeLogPath,
            "--log-verbosity",
            "2"
        ];

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
                    "자연어 AI 실행기가 예기치 않게 종료되었습니다. " +
                    $"종료 코드: {process.ExitCode}");
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
                // The local server is still loading the model.
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested)
            {
                // A health request can briefly time out while loading.
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException(
            "자연어 모델을 2분 안에 불러오지 못했습니다.");
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
        var listener = new TcpListener(
            IPAddress.Loopback,
            0);
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

    private static object BuildResponseFormat() =>
        new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "search_plan",
                strict = true,
                schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        term_groups = new
                        {
                            type = "array",
                            maxItems = 10,
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    term = new
                                    {
                                        type = "string"
                                    },
                                    alternatives = new
                                    {
                                        type = "array",
                                        maxItems = 16,
                                        items = new
                                        {
                                            type = "string"
                                        }
                                    }
                                },
                                required = new[]
                                {
                                    "term",
                                    "alternatives"
                                }
                            }
                        },
                        requested_extensions = new
                        {
                            type = "array",
                            maxItems = 16,
                            items = new
                            {
                                type = "string"
                            }
                        },
                        target = new
                        {
                            type = "string",
                            @enum = new[]
                            {
                                "any",
                                "file",
                                "folder"
                            }
                        },
                        sort = new
                        {
                            type = "string",
                            @enum = new[]
                            {
                                "relevance",
                                "created_newest",
                                "created_oldest",
                                "modified_newest",
                                "modified_oldest",
                                "name_match",
                                "path_match",
                                "large_first",
                                "small_first"
                            }
                        },
                        use_previous_results = new
                        {
                            type = "boolean"
                        },
                        confidence = new
                        {
                            type = "number",
                            minimum = 0,
                            maximum = 1
                        },
                        interpretation = new
                        {
                            type = "string",
                            maxLength = 200
                        }
                    },
                    required = new[]
                    {
                        "term_groups",
                        "requested_extensions",
                        "target",
                        "sort",
                        "use_previous_results",
                        "confidence",
                        "interpretation"
                    }
                }
            }
        };

    private static string BuildSystemPrompt() =>
        """
        당신은 Windows 파일 검색기의 한국어 검색 계획기다.
        사용자의 실제 의도를 짧고 보수적으로 구조화하라.
        파일 경로나 파일을 지어내지 말고 검색 표현만 만든다.

        규칙:
        - term_groups에는 실제로 찾을 핵심 개념만 넣는다.
        - 각 alternatives에는 같은 대상을 가리키는 띄어쓰기 변형,
          약어, 한국어/영어 표현, 일반적인 파일명 표현을 넣는다.
        - 예: AWS SSH 키는 aws, ssh, key/private key,
          ppk/pem/putty와 관련될 수 있다.
        - 사용자가 확장자를 직접 제한한 경우에만
          requested_extensions에 넣는다. 의미상 관련된 확장자는
          alternatives에 넣어 결과를 강제 배제하지 않는다.
        - 생성일과 수정일을 구분한다.
        - "그중", "결과에서", "방금 찾은 것" 또는 문맥상 이전
          결과를 가리키면 use_previous_results를 true로 한다.
        - 폴더를 찾으면 target=folder, 파일을 찾으면 target=file,
          불분명하면 target=any다.
        - confidence는 해석 확신도다.
        - interpretation은 초보자가 이해할 수 있는 한 문장이다.
        """;

    private static string SanitizeText(
        string value,
        int maximumLength)
    {
        var singleLine = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();
        while (singleLine.Contains(
                   "  ",
                   StringComparison.Ordinal))
        {
            singleLine = singleLine.Replace(
                "  ",
                " ",
                StringComparison.Ordinal);
        }

        return singleLine.Length <= maximumLength
            ? singleLine
            : singleLine[..maximumLength];
    }

    private static void TryApplyLowPriority(Process process)
    {
        try
        {
            process.PriorityClass =
                ProcessPriorityClass.BelowNormal;
        }
        catch
        {
            // Some managed PCs do not permit priority changes.
        }
    }

    private static string TrimForMessage(string text)
    {
        var singleLine = SanitizeText(text, 240);
        return singleLine.Length == 0
            ? "응답 내용 없음"
            : singleLine;
    }

    public void Stop()
    {
        if (!_disposed)
        {
            StopOwnedServer();
        }
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
            // The process may already have exited during shutdown.
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
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
        _startupLock.Dispose();
        _requestLock.Dispose();
    }

    private sealed class LanguagePlanDto
    {
        [JsonPropertyName("term_groups")]
        public List<LanguageTermGroupDto>? TermGroups { get; init; }

        [JsonPropertyName("requested_extensions")]
        public List<string>? RequestedExtensions { get; init; }

        [JsonPropertyName("target")]
        public string? Target { get; init; }

        [JsonPropertyName("sort")]
        public string? Sort { get; init; }

        [JsonPropertyName("use_previous_results")]
        public bool? UsePreviousResults { get; init; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; init; }

        [JsonPropertyName("interpretation")]
        public string? Interpretation { get; init; }
    }

    private sealed class LanguageTermGroupDto
    {
        [JsonPropertyName("term")]
        public string? Term { get; init; }

        [JsonPropertyName("alternatives")]
        public List<string>? Alternatives { get; init; }
    }
}
