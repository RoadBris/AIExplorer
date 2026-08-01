using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public sealed partial class AiModelManager : IDisposable
{
    public const string ModelId =
        "multilingual-e5-base-q4-k-m-768d";
    public const string ModelDisplayName =
        "Multilingual E5 Base Q4_K_M · 768D";
    public const string ModelVersion =
        "ff190f44542a3ee0-q4-k-m-768d";
    public const string ModelFileName =
        "multilingual-e5-base-q4_k_m.gguf";
    public const string ModelSha256 =
        "3c33cbe9ce46b45ab71f47ddc8ae3bc6af0e049aef29de15cefbc494fba1732b";
    public const string VisualModelDisplayName =
        "SigLIP 2 Base INT8 + WD ViT Tagger v3";
    public const string VisualModelVersion =
        "ba1f3b0843f24bc5-siglip2-790b0e9-wd-v3";
    public const string VisualModelFileName =
        "siglip2-base-patch16-224-int8.onnx";
    public const string VisualTokenizerFileName =
        "siglip2-tokenizer.model";
    public const string VisualModelSha256 =
        "bfe28fe2ccdb685874586648035ea349593e487ce33bd0939b28813681a8f167";
    public const string VisualTokenizerSha256 =
        "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2";
    public const string ImageTaggerModelFileName =
        "wd-vit-tagger-v3.onnx";
    public const string ImageTaggerLabelsFileName =
        "wd-vit-tagger-v3-tags.csv";
    public const string ImageTaggerModelSha256 =
        "35f23693620b668f4d53fd3c62bf65e40af739bc52c7eb0fbc49258b58d065b6";
    public const string ImageTaggerLabelsSha256 =
        "298633d94d0031d2081c0893f29c82eab7f0df00b08483ba8f29d1e979441217";
    public const string LanguageModelDisplayName =
        "Qwen3 1.7B Q4_K_M · 자연어 검색 해석";
    public const string LanguageModelVersion =
        "daeb8e2-qwen3-1.7b-q4-k-m";
    public const string LanguageModelFileName =
        "Qwen3-1.7B-Q4_K_M.gguf";
    public const string LanguageModelSha256 =
        "d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5";
    public const long ModelFileApproximateBytes =
        219L * 1024 * 1024;
    public const long VisualBundleApproximateBytes =
        762L * 1024 * 1024;
    public const long LanguageModelApproximateBytes =
        1280L * 1024 * 1024;
    public const long ApproximateDownloadBytes = 2420L * 1024 * 1024;
    public const long MinimumTemporaryFreeBytes =
        4096L * 1024 * 1024;

    private static readonly Uri ModelUri = new(
        "https://huggingface.co/dinab/" +
        "multilingual-e5-base-Q4_K_M-GGUF/" +
        "resolve/ff190f44542a3ee01e865c936450c41c8b159805/" +
        "multilingual-e5-base-q4_k_m.gguf?download=true");
    private static readonly Uri VisualModelUri = new(
        "https://huggingface.co/onnx-community/" +
        "siglip2-base-patch16-224-ONNX/" +
        "resolve/ba1f3b0843f24bc5417d38e19c37b287d719b2f4/" +
        "onnx/model_int8.onnx?download=true");
    private static readonly Uri VisualTokenizerUri = new(
        "https://huggingface.co/onnx-community/" +
        "siglip2-base-patch16-224-ONNX/" +
        "resolve/ba1f3b0843f24bc5417d38e19c37b287d719b2f4/" +
        "tokenizer.model?download=true");
    private static readonly Uri ImageTaggerModelUri = new(
        "https://huggingface.co/SmilingWolf/wd-vit-tagger-v3/" +
        "resolve/790b0e92cefd2a0221451604e7831fe643ab7c4f/" +
        "model.onnx?download=true");
    private static readonly Uri ImageTaggerLabelsUri = new(
        "https://huggingface.co/SmilingWolf/wd-vit-tagger-v3/" +
        "resolve/790b0e92cefd2a0221451604e7831fe643ab7c4f/" +
        "selected_tags.csv?download=true");
    private static readonly Uri LanguageModelUri = new(
        "https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF/" +
        "resolve/daeb8e2/Qwen3-1.7B-Q4_K_M.gguf?download=true");
    private static readonly string[] ObsoleteModelFileNames =
    [
        "nomic-embed-text-v2-moe.Q4_K_M.gguf"
    ];
    private static readonly string[] ObsoleteVisualModelFileNames =
    [
        "tinyclip-vit-8m-16-text-3m-fp16.onnx",
        "tinyclip-vit-39m-16-text-19m-q4f16.onnx",
        "openclip-vit-b32-laion2b-int8.onnx",
        "tinyclip-tokenizer.json"
    ];
    private static readonly Uri LatestRuntimeReleaseUri = new(
        "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest");

    private readonly HttpClient _httpClient;
    private readonly string _modelDirectory;
    private readonly string _runtimeDirectory;
    private readonly string _visualModelDirectory;
    private readonly string _languageModelDirectory;
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private bool _disposed;

    public AiModelManager(string modelDirectory)
    {
        _modelDirectory = modelDirectory;
        _runtimeDirectory = Path.Combine(modelDirectory, "llama-runtime");
        var modelsRoot = Path.GetDirectoryName(modelDirectory) ??
                         modelDirectory;
        _visualModelDirectory = Path.Combine(modelsRoot, "visual");
        _languageModelDirectory = Path.Combine(modelsRoot, "language");
        ModelPath = Path.Combine(modelDirectory, ModelFileName);
        ServerExecutablePath = Path.Combine(
            _runtimeDirectory,
            "llama-server.exe");
        ModelVersionPath = Path.Combine(
            modelDirectory,
            "model-version.txt");
        RuntimeVersionPath = Path.Combine(
            modelDirectory,
            "runtime-version.txt");
        RuntimeLogPath = Path.Combine(
            modelDirectory,
            "local-ai-runtime.log");
        VisualModelPath = Path.Combine(
            _visualModelDirectory,
            VisualModelFileName);
        VisualTokenizerPath = Path.Combine(
            _visualModelDirectory,
            VisualTokenizerFileName);
        ImageTaggerModelPath = Path.Combine(
            _visualModelDirectory,
            ImageTaggerModelFileName);
        ImageTaggerLabelsPath = Path.Combine(
            _visualModelDirectory,
            ImageTaggerLabelsFileName);
        VisualModelVersionPath = Path.Combine(
            _visualModelDirectory,
            "model-version.txt");
        LanguageModelPath = Path.Combine(
            _languageModelDirectory,
            LanguageModelFileName);
        LanguageModelVersionPath = Path.Combine(
            _languageModelDirectory,
            "model-version.txt");
        LanguageRuntimeLogPath = Path.Combine(
            _languageModelDirectory,
            "local-language-runtime.log");

        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("AIExplorer", "0.82.4"));
    }

    public string ModelPath { get; }

    public string ServerExecutablePath { get; }

    public string ModelVersionPath { get; }

    public string RuntimeVersionPath { get; }

    public string RuntimeLogPath { get; }

    public string VisualModelPath { get; }

    public string VisualTokenizerPath { get; }

    public string ImageTaggerModelPath { get; }

    public string ImageTaggerLabelsPath { get; }

    public string VisualModelVersionPath { get; }

    public string LanguageModelPath { get; }

    public string LanguageModelVersionPath { get; }

    public string LanguageRuntimeLogPath { get; }

    public bool IsInstalled =>
        File.Exists(ModelPath) &&
        File.Exists(ServerExecutablePath) &&
        File.Exists(VisualModelPath) &&
        File.Exists(VisualTokenizerPath);

    public bool IsImageTaggerInstalled =>
        File.Exists(ImageTaggerModelPath) &&
        File.Exists(ImageTaggerLabelsPath);

    public bool IsLanguageModelInstalled =>
        File.Exists(LanguageModelPath) &&
        File.Exists(ServerExecutablePath);

    public string InstalledModelVersion =>
        ReadVersionFile(ModelVersionPath) ??
        (File.Exists(ModelPath) ? "기존 설치" : "미설치");

    public string InstalledRuntimeVersion =>
        ReadVersionFile(RuntimeVersionPath) ??
        (File.Exists(ServerExecutablePath) ? "기존 설치" : "미설치");

    public string InstalledVisualModelVersion =>
        ReadVersionFile(VisualModelVersionPath) ??
        (File.Exists(VisualModelPath) ? "기존 설치" : "미설치");

    public string InstalledLanguageModelVersion =>
        ReadVersionFile(LanguageModelVersionPath) ??
        (File.Exists(LanguageModelPath) ? "기존 설치" : "미설치");

    public Task InstallAsync(
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken) =>
        InstallOrUpgradeAsync(
            checkLatestRuntime: false,
            progress,
            cancellationToken);

    public Task UpgradeAsync(
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken) =>
        InstallOrUpgradeAsync(
            checkLatestRuntime: true,
            progress,
            cancellationToken);

    public async Task<AiModelUpgradeStatus> CheckForUpgradeAsync(
        CancellationToken cancellationToken)
    {
        EnsureSupportedPlatform();
        var modelIsCurrent = await HasExpectedSha256Async(
            ModelPath,
            ModelSha256,
            cancellationToken);
        var visualModelIsCurrent =
            await IsVisualModelCurrentAsync(cancellationToken);
        var languageModelIsCurrent = await HasExpectedSha256Async(
            LanguageModelPath,
            LanguageModelSha256,
            cancellationToken);
        var latestRuntime = await GetLatestRuntimeReleaseAsync(
            cancellationToken);
        var installedModelVersion = InstalledModelVersion;
        var installedRuntimeVersion = InstalledRuntimeVersion;
        var installedVisualModelVersion =
            InstalledVisualModelVersion;
        var installedLanguageModelVersion =
            InstalledLanguageModelVersion;
        var visualModelDownloadRequired = !visualModelIsCurrent;
        var visualModelUpgradeAvailable =
            visualModelDownloadRequired ||
            !string.Equals(
                installedVisualModelVersion,
                VisualModelVersion,
                StringComparison.OrdinalIgnoreCase);
        var modelDownloadRequired = !modelIsCurrent;
        var languageModelDownloadRequired =
            !languageModelIsCurrent;
        var languageModelUpgradeAvailable =
            languageModelDownloadRequired ||
            !string.Equals(
                installedLanguageModelVersion,
                LanguageModelVersion,
                StringComparison.OrdinalIgnoreCase);
        var modelUpgradeAvailable =
            !modelIsCurrent ||
            !string.Equals(
                installedModelVersion,
                ModelVersion,
                StringComparison.OrdinalIgnoreCase) ||
            visualModelUpgradeAvailable ||
            languageModelUpgradeAvailable;
        var runtimeUpgradeAvailable =
            !File.Exists(ServerExecutablePath) ||
            !string.Equals(
                installedRuntimeVersion,
                latestRuntime.TagName,
                StringComparison.OrdinalIgnoreCase);
        var upgradeAvailable =
            modelUpgradeAvailable || runtimeUpgradeAvailable;
        var estimatedDownloadBytes =
            (!modelIsCurrent
                ? ModelFileApproximateBytes
                : 0L) +
            (visualModelDownloadRequired
                ? VisualBundleApproximateBytes
                : 0L) +
            (languageModelDownloadRequired
                ? LanguageModelApproximateBytes
                : 0L) +
            (runtimeUpgradeAvailable
                ? latestRuntime.Asset.SizeBytes ??
                  Math.Max(
                      0,
                      ApproximateDownloadBytes -
                      ModelFileApproximateBytes -
                      VisualBundleApproximateBytes)
                : 0L);
        var description = upgradeAvailable
            ? string.Join(
                " · ",
                new[]
                {
                    !modelIsCurrent
                        ? $"다국어 의미 모델 {ModelVersion}"
                        : !string.Equals(
                            installedModelVersion,
                            ModelVersion,
                            StringComparison.OrdinalIgnoreCase)
                            ? "의미 모델 버전 정보 정리"
                            : null,
                    visualModelUpgradeAvailable
                        ? visualModelDownloadRequired
                            ? $"시각 모델 {VisualModelVersion}"
                            : "시각 모델 버전 정보 정리"
                        : null,
                    languageModelUpgradeAvailable
                        ? languageModelDownloadRequired
                            ? $"자연어 해석 모델 {LanguageModelVersion}"
                            : "자연어 해석 모델 버전 정보 정리"
                        : null,
                    runtimeUpgradeAvailable
                        ? $"CPU 실행기 {latestRuntime.TagName}"
                        : null
                }.Where(text => text is not null))
            : "E5·SigLIP 2·자연어 해석 모델과 CPU 실행기가 모두 최신입니다.";

        return new AiModelUpgradeStatus(
            upgradeAvailable,
            installedModelVersion,
            ModelVersion,
            installedRuntimeVersion,
            latestRuntime.TagName,
            modelUpgradeAvailable,
            modelDownloadRequired,
            runtimeUpgradeAvailable,
            estimatedDownloadBytes,
            description,
            installedVisualModelVersion,
            VisualModelVersion,
            visualModelUpgradeAvailable,
            visualModelDownloadRequired,
            installedLanguageModelVersion,
            LanguageModelVersion,
            languageModelUpgradeAvailable,
            languageModelDownloadRequired);
    }

    private async Task InstallOrUpgradeAsync(
        bool checkLatestRuntime,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        EnsureSupportedPlatform();

        await _installLock.WaitAsync(cancellationToken);
        try
        {
            var modelIsCurrent = await HasExpectedSha256Async(
                ModelPath,
                ModelSha256,
                cancellationToken);
            var visualModelIsCurrent =
                await IsVisualModelCurrentAsync(cancellationToken);
            var languageModelIsCurrent =
                await HasExpectedSha256Async(
                    LanguageModelPath,
                    LanguageModelSha256,
                    cancellationToken);
            if (!checkLatestRuntime &&
                IsInstalled &&
                modelIsCurrent &&
                visualModelIsCurrent &&
                languageModelIsCurrent)
            {
                await WriteVersionFileAsync(
                    ModelVersionPath,
                    ModelVersion,
                    cancellationToken);
                await WriteVersionFileAsync(
                    VisualModelVersionPath,
                    VisualModelVersion,
                    cancellationToken);
                await WriteVersionFileAsync(
                    LanguageModelVersionPath,
                    LanguageModelVersion,
                    cancellationToken);
                DeleteObsoleteModels();
                DeleteObsoleteVisualModels();
                progress?.Report(new AiModelInstallProgress(
                    AiModelInstallPhase.Completed,
                    1,
                    1,
                    "AI 모델이 이미 설치되어 있습니다."));
                return;
            }

            EnsureAvailableDiskSpace();
            Directory.CreateDirectory(_modelDirectory);
            Directory.CreateDirectory(_visualModelDirectory);
            Directory.CreateDirectory(_languageModelDirectory);
            var stagingDirectory = Path.Combine(
                _modelDirectory,
                $"install-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                RuntimeRelease? latestRuntime = null;
                if (checkLatestRuntime ||
                    !File.Exists(ServerExecutablePath))
                {
                    progress?.Report(new AiModelInstallProgress(
                        AiModelInstallPhase.ResolvingRuntime,
                        0,
                        null,
                        checkLatestRuntime
                            ? "AI 업그레이드를 확인하는 중..."
                            : "CPU AI 실행기를 확인하는 중..."));
                    latestRuntime = await GetLatestRuntimeReleaseAsync(
                        cancellationToken);
                }

                if (latestRuntime is not null &&
                    (!File.Exists(ServerExecutablePath) ||
                     !string.Equals(
                         InstalledRuntimeVersion,
                         latestRuntime.TagName,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    await InstallRuntimeAsync(
                        stagingDirectory,
                        latestRuntime,
                        progress,
                        cancellationToken);
                }

                if (!modelIsCurrent)
                {
                    await InstallModelAsync(
                        stagingDirectory,
                        progress,
                        cancellationToken);
                }
                else
                {
                    await WriteVersionFileAsync(
                        ModelVersionPath,
                        ModelVersion,
                        cancellationToken);
                }

                if (!visualModelIsCurrent)
                {
                    await InstallVisualModelAsync(
                        stagingDirectory,
                        progress,
                        cancellationToken);
                }
                else
                {
                    await WriteVersionFileAsync(
                        VisualModelVersionPath,
                        VisualModelVersion,
                        cancellationToken);
                }

                if (!languageModelIsCurrent)
                {
                    await InstallLanguageModelAsync(
                        stagingDirectory,
                        progress,
                        cancellationToken);
                }
                else
                {
                    await WriteVersionFileAsync(
                        LanguageModelVersionPath,
                        LanguageModelVersion,
                        cancellationToken);
                }
            }
            finally
            {
                TryDeleteDirectory(stagingDirectory);
            }

            if (!IsInstalled || !IsLanguageModelInstalled)
            {
                throw new InvalidOperationException(
                    "AI 모델 설치 파일을 확인하지 못했습니다.");
            }

            DeleteObsoleteModels();
            DeleteObsoleteVisualModels();
            progress?.Report(new AiModelInstallProgress(
                AiModelInstallPhase.Completed,
                1,
                1,
                checkLatestRuntime
                    ? "E5·SigLIP 2·자연어 해석 모델과 CPU 실행기 업그레이드를 완료했습니다."
                    : "의미·시각·자연어 해석 AI 모델 설치를 완료했습니다."));
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task InstallRuntimeAsync(
        string stagingDirectory,
        RuntimeRelease runtimeRelease,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var runtimeArchive = Path.Combine(stagingDirectory, "llama-runtime.zip");
        await DownloadFileAsync(
            runtimeRelease.Asset.DownloadUri,
            runtimeArchive,
            AiModelInstallPhase.DownloadingRuntime,
            "CPU AI 실행기 다운로드",
            progress,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(runtimeRelease.Asset.Sha256) ||
            !await HasExpectedSha256Async(
                runtimeArchive,
                runtimeRelease.Asset.Sha256,
                cancellationToken))
        {
            throw new InvalidDataException(
                "CPU AI 실행기 무결성 검증에 실패했습니다.");
        }

        progress?.Report(new AiModelInstallProgress(
            AiModelInstallPhase.ExtractingRuntime,
            0,
            null,
            "CPU AI 실행기를 설치하는 중..."));
        var extractedDirectory = Path.Combine(stagingDirectory, "runtime-extracted");
        ZipFile.ExtractToDirectory(
            runtimeArchive,
            extractedDirectory,
            overwriteFiles: true);
        var serverExecutable = Directory
            .EnumerateFiles(
                extractedDirectory,
                "llama-server.exe",
                SearchOption.AllDirectories)
            .FirstOrDefault();
        if (serverExecutable is null)
        {
            throw new InvalidDataException(
                "압축 파일에서 llama-server.exe를 찾지 못했습니다.");
        }

        var sourceDirectory = Path.GetDirectoryName(serverExecutable)
                              ?? throw new InvalidDataException(
                                  "AI 실행기 폴더를 확인하지 못했습니다.");
        var stagedRuntime = Path.Combine(stagingDirectory, "runtime-ready");
        CopyDirectory(sourceDirectory, stagedRuntime);
        if (Directory.Exists(_runtimeDirectory))
        {
            Directory.Delete(_runtimeDirectory, recursive: true);
        }

        Directory.Move(stagedRuntime, _runtimeDirectory);
        await WriteVersionFileAsync(
            RuntimeVersionPath,
            runtimeRelease.TagName,
            cancellationToken);
    }

    private async Task InstallModelAsync(
        string stagingDirectory,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stagedModel = Path.Combine(stagingDirectory, ModelFileName);
        await DownloadFileAsync(
            ModelUri,
            stagedModel,
            AiModelInstallPhase.DownloadingModel,
            "다국어 의미 검색 모델 다운로드",
            progress,
            cancellationToken);
        if (!await HasExpectedSha256Async(
                stagedModel,
                ModelSha256,
                cancellationToken))
        {
            throw new InvalidDataException(
                "다국어 AI 모델 무결성 검증에 실패했습니다.");
        }

        File.Move(stagedModel, ModelPath, overwrite: true);
        DeleteObsoleteModels();
        await WriteVersionFileAsync(
            ModelVersionPath,
            ModelVersion,
            cancellationToken);
    }

    private async Task InstallVisualModelAsync(
        string stagingDirectory,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stagedModel = Path.Combine(
            stagingDirectory,
            VisualModelFileName);
        var stagedTokenizer = Path.Combine(
            stagingDirectory,
            VisualTokenizerFileName);
        var stagedTaggerModel = Path.Combine(
            stagingDirectory,
            ImageTaggerModelFileName);
        var stagedTaggerLabels = Path.Combine(
            stagingDirectory,
            ImageTaggerLabelsFileName);
        var visualModelIsCurrent = await HasExpectedSha256Async(
            VisualModelPath,
            VisualModelSha256,
            cancellationToken);
        var visualTokenizerIsCurrent = await HasExpectedSha256Async(
            VisualTokenizerPath,
            VisualTokenizerSha256,
            cancellationToken);
        var taggerModelIsCurrent = await HasExpectedSha256Async(
            ImageTaggerModelPath,
            ImageTaggerModelSha256,
            cancellationToken);
        var taggerLabelsAreCurrent = await HasExpectedSha256Async(
            ImageTaggerLabelsPath,
            ImageTaggerLabelsSha256,
            cancellationToken);
        if (!visualModelIsCurrent)
        {
            await DownloadFileAsync(
                VisualModelUri,
                stagedModel,
                AiModelInstallPhase.DownloadingVisualModel,
                "SigLIP 2 이미지 의미 모델 다운로드",
                progress,
                cancellationToken);
        }
        if (!visualTokenizerIsCurrent)
        {
            await DownloadFileAsync(
                VisualTokenizerUri,
                stagedTokenizer,
                AiModelInstallPhase.DownloadingVisualModel,
                "SigLIP 2 다국어 토크나이저 다운로드",
                progress,
                cancellationToken);
        }
        if (!taggerModelIsCurrent)
        {
            await DownloadFileAsync(
                ImageTaggerModelUri,
                stagedTaggerModel,
                AiModelInstallPhase.DownloadingVisualModel,
                "캐릭터·일러스트 태거 다운로드",
                progress,
                cancellationToken);
        }
        if (!taggerLabelsAreCurrent)
        {
            await DownloadFileAsync(
                ImageTaggerLabelsUri,
                stagedTaggerLabels,
                AiModelInstallPhase.DownloadingVisualModel,
                "캐릭터 태그 사전 다운로드",
                progress,
                cancellationToken);
        }
        var visualModelIsInvalid =
            !visualModelIsCurrent &&
            !await HasExpectedSha256Async(
                stagedModel,
                VisualModelSha256,
                cancellationToken);
        var visualTokenizerIsInvalid =
            !visualTokenizerIsCurrent &&
            !await HasExpectedSha256Async(
                stagedTokenizer,
                VisualTokenizerSha256,
                cancellationToken);
        var taggerModelIsInvalid =
            !taggerModelIsCurrent &&
            !await HasExpectedSha256Async(
                stagedTaggerModel,
                ImageTaggerModelSha256,
                cancellationToken);
        var taggerLabelsAreInvalid =
            !taggerLabelsAreCurrent &&
            !await HasExpectedSha256Async(
                stagedTaggerLabels,
                ImageTaggerLabelsSha256,
                cancellationToken);
        if (visualModelIsInvalid ||
            visualTokenizerIsInvalid ||
            taggerModelIsInvalid ||
            taggerLabelsAreInvalid)
        {
            throw new InvalidDataException(
                "시각 AI 또는 캐릭터 태거 무결성 검증에 실패했습니다.");
        }

        Directory.CreateDirectory(_visualModelDirectory);
        if (!visualModelIsCurrent)
        {
            File.Move(stagedModel, VisualModelPath, overwrite: true);
        }
        if (!visualTokenizerIsCurrent)
        {
            File.Move(
                stagedTokenizer,
                VisualTokenizerPath,
                overwrite: true);
        }
        if (!taggerModelIsCurrent)
        {
            File.Move(
                stagedTaggerModel,
                ImageTaggerModelPath,
                overwrite: true);
        }
        if (!taggerLabelsAreCurrent)
        {
            File.Move(
                stagedTaggerLabels,
                ImageTaggerLabelsPath,
                overwrite: true);
        }
        await WriteVersionFileAsync(
            VisualModelVersionPath,
            VisualModelVersion,
            cancellationToken);
        DeleteObsoleteVisualModels();
    }

    private async Task InstallLanguageModelAsync(
        string stagingDirectory,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stagedModel = Path.Combine(
            stagingDirectory,
            LanguageModelFileName);
        await DownloadFileAsync(
            LanguageModelUri,
            stagedModel,
            AiModelInstallPhase.DownloadingLanguageModel,
            "Qwen3 자연어 검색 해석 모델 다운로드",
            progress,
            cancellationToken);
        if (!await HasExpectedSha256Async(
                stagedModel,
                LanguageModelSha256,
                cancellationToken))
        {
            throw new InvalidDataException(
                "자연어 검색 해석 모델 무결성 검증에 실패했습니다.");
        }

        Directory.CreateDirectory(_languageModelDirectory);
        File.Move(
            stagedModel,
            LanguageModelPath,
            overwrite: true);
        await WriteVersionFileAsync(
            LanguageModelVersionPath,
            LanguageModelVersion,
            cancellationToken);
    }

    private void DeleteObsoleteModels()
    {
        foreach (var fileName in ObsoleteModelFileNames)
        {
            var path = Path.Combine(_modelDirectory, fileName);
            if (string.Equals(
                    path,
                    ModelPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException)
            {
                AppLog.Warning(
                    $"이전 의미 AI 모델을 정리하지 못했습니다: " +
                    $"{path} · {exception.Message}");
            }
        }
    }

    private void DeleteObsoleteVisualModels()
    {
        foreach (var fileName in ObsoleteVisualModelFileNames)
        {
            var path = Path.Combine(_visualModelDirectory, fileName);
            if (string.Equals(
                    path,
                    VisualModelPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException)
            {
                AppLog.Warning(
                    $"이전 시각 AI 모델을 정리하지 못했습니다: " +
                    $"{path} · {exception.Message}");
            }
        }
    }

    private async Task<bool> IsVisualModelCurrentAsync(
        CancellationToken cancellationToken) =>
        await HasExpectedSha256Async(
            VisualModelPath,
            VisualModelSha256,
            cancellationToken) &&
        await HasExpectedSha256Async(
            VisualTokenizerPath,
            VisualTokenizerSha256,
            cancellationToken) &&
        await HasExpectedSha256Async(
            ImageTaggerModelPath,
            ImageTaggerModelSha256,
            cancellationToken) &&
        await HasExpectedSha256Async(
            ImageTaggerLabelsPath,
            ImageTaggerLabelsSha256,
            cancellationToken);

    private async Task DownloadFileAsync(
        Uri uri,
        string destinationPath,
        AiModelInstallPhase phase,
        string description,
        IProgress<AiModelInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[131_072];
        long downloadedBytes = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
            downloadedBytes += read;
            progress?.Report(new AiModelInstallProgress(
                phase,
                downloadedBytes,
                totalBytes,
                description));
        }

        await destination.FlushAsync(cancellationToken);
    }

    private async Task<RuntimeRelease> GetLatestRuntimeReleaseAsync(
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            LatestRuntimeReleaseUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var release = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);
        var tagName = release.RootElement.TryGetProperty(
                "tag_name",
                out var tagElement)
            ? tagElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new InvalidDataException(
                "CPU AI 실행기 버전 정보를 읽지 못했습니다.");
        }

        return new RuntimeRelease(
            tagName,
            ResolveRuntimeAsset(release.RootElement));
    }

    private static RuntimeReleaseAsset ResolveRuntimeAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "CPU AI 실행기 배포 정보를 읽지 못했습니다.");
        }

        JsonElement? selectedAsset = null;
        var selectedPriority = int.MaxValue;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            var priority = GetWindowsCpuRuntimePriority(name);
            if (priority >= selectedPriority)
            {
                continue;
            }

            selectedAsset = asset;
            selectedPriority = priority;
        }

        if (selectedAsset is null)
        {
            throw new InvalidDataException(
                "Windows x64 CPU용 AI 실행기를 찾지 못했습니다.");
        }

        var resolvedAsset = selectedAsset.Value;
        var url = resolvedAsset.GetProperty(
            "browser_download_url").GetString();
        var digest = resolvedAsset.TryGetProperty(
                "digest",
                out var digestElement)
            ? digestElement.GetString()
            : null;
        long? sizeBytes =
            resolvedAsset.TryGetProperty("size", out var sizeElement) &&
            sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : null;
        var sha256 = digest?.StartsWith(
                "sha256:",
                StringComparison.OrdinalIgnoreCase) == true
            ? digest[7..]
            : null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var downloadUri))
        {
            throw new InvalidDataException(
                "CPU AI 실행기 다운로드 주소를 읽지 못했습니다.");
        }

        return new RuntimeReleaseAsset(
            downloadUri,
            sha256,
            sizeBytes);
    }

    private static async Task<bool> HasExpectedSha256Async(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(
            expectedSha256,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadVersionFile(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.ReadAllText(path).Trim()
                : null;
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

    private static async Task WriteVersionFileAsync(
        string path,
        string version,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ??
            throw new InvalidDataException(
                "AI 버전 정보 폴더를 확인하지 못했습니다."));
        await File.WriteAllTextAsync(
            path,
            version + Environment.NewLine,
            cancellationToken);
    }

    private static void EnsureSupportedPlatform()
    {
        if (!OperatingSystem.IsWindows() ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                "AI 모델 자동 설치와 업그레이드는 Windows x64에서 지원됩니다.");
        }
    }

    private void EnsureAvailableDiskSpace()
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_modelDirectory));
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var drive = new DriveInfo(root);
            if (drive.IsReady &&
                drive.AvailableFreeSpace < MinimumTemporaryFreeBytes)
            {
                throw new IOException(
                    "AI 모델 설치와 업그레이드에는 최소 4GB의 여유 공간이 필요합니다.");
            }
        }
        catch (ArgumentException)
        {
            // UNC and unusual portable paths do not always expose drive capacity.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(
                file,
                Path.Combine(destination, Path.GetFileName(file)),
                overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // A partial staging folder can be removed on a later install.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        if (_installLock.Wait(0))
        {
            _installLock.Dispose();
        }
    }

    private static int GetWindowsCpuRuntimePriority(string assetName)
    {
        if (AcceleratedWindowsRuntimeRegex().IsMatch(assetName))
        {
            return int.MaxValue;
        }

        if (DefaultWindowsCpuRuntimeRegex().IsMatch(assetName))
        {
            return 0;
        }

        if (NamedWindowsCpuRuntimeRegex().IsMatch(assetName))
        {
            return 1;
        }

        if (Avx2WindowsCpuRuntimeRegex().IsMatch(assetName))
        {
            return 2;
        }

        if (CompatibleWindowsCpuRuntimeRegex().IsMatch(assetName))
        {
            return 10;
        }

        return int.MaxValue;
    }

    [GeneratedRegex(
        @"^llama-b.+-bin-win-x64\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DefaultWindowsCpuRuntimeRegex();

    [GeneratedRegex(
        @"^llama-b.+-bin-win-cpu-x64\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamedWindowsCpuRuntimeRegex();

    [GeneratedRegex(
        @"^llama-b.+-bin-win-avx2-x64\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Avx2WindowsCpuRuntimeRegex();

    [GeneratedRegex(
        @"^llama-b.+-bin-win.*-?x64\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompatibleWindowsCpuRuntimeRegex();

    [GeneratedRegex(
        @"(?:cuda|cudart|vulkan|sycl|hip|openvino|rpc|kompute|clblast)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AcceleratedWindowsRuntimeRegex();

    private sealed record RuntimeReleaseAsset(
        Uri DownloadUri,
        string? Sha256,
        long? SizeBytes);

    private sealed record RuntimeRelease(
        string TagName,
        RuntimeReleaseAsset Asset);
}

public sealed record AiModelUpgradeStatus(
    bool IsUpgradeAvailable,
    string InstalledModelVersion,
    string TargetModelVersion,
    string InstalledRuntimeVersion,
    string TargetRuntimeVersion,
    bool IsModelUpgradeAvailable,
    bool IsModelDownloadRequired,
    bool IsRuntimeUpgradeAvailable,
    long EstimatedDownloadBytes,
    string Description,
    string InstalledVisualModelVersion,
    string TargetVisualModelVersion,
    bool IsVisualModelUpgradeAvailable,
    bool IsVisualModelDownloadRequired,
    string InstalledLanguageModelVersion,
    string TargetLanguageModelVersion,
    bool IsLanguageModelUpgradeAvailable,
    bool IsLanguageModelDownloadRequired);

public sealed record AiModelInstallProgress(
    AiModelInstallPhase Phase,
    long DownloadedBytes,
    long? TotalBytes,
    string Description);

public enum AiModelInstallPhase
{
    ResolvingRuntime,
    DownloadingRuntime,
    ExtractingRuntime,
    DownloadingModel,
    DownloadingVisualModel,
    DownloadingLanguageModel,
    Completed
}
