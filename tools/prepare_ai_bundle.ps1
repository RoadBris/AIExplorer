[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BundleRoot
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$modelFileName = "multilingual-e5-base-q4_k_m.gguf"
$modelVersion = "ff190f44542a3ee0-q4-k-m-768d"
$modelSha256 = "3c33cbe9ce46b45ab71f47ddc8ae3bc6af0e049aef29de15cefbc494fba1732b"
$modelUrl = "https://huggingface.co/dinab/multilingual-e5-base-Q4_K_M-GGUF/resolve/ff190f44542a3ee01e865c936450c41c8b159805/multilingual-e5-base-q4_k_m.gguf?download=true"
$visualModelFileName = "siglip2-base-patch16-224-int8.onnx"
$visualTokenizerFileName = "siglip2-tokenizer.model"
$visualModelVersion = "ba1f3b0843f24bc5-siglip2-790b0e9-wd-v3"
$visualModelSha256 = "bfe28fe2ccdb685874586648035ea349593e487ce33bd0939b28813681a8f167"
$visualTokenizerSha256 = "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2"
$visualModelUrl = "https://huggingface.co/onnx-community/siglip2-base-patch16-224-ONNX/resolve/ba1f3b0843f24bc5417d38e19c37b287d719b2f4/onnx/model_int8.onnx?download=true"
$visualTokenizerUrl = "https://huggingface.co/onnx-community/siglip2-base-patch16-224-ONNX/resolve/ba1f3b0843f24bc5417d38e19c37b287d719b2f4/tokenizer.model?download=true"
$imageTaggerFileName = "wd-vit-tagger-v3.onnx"
$imageTaggerLabelsFileName = "wd-vit-tagger-v3-tags.csv"
$imageTaggerSha256 = "35f23693620b668f4d53fd3c62bf65e40af739bc52c7eb0fbc49258b58d065b6"
$imageTaggerLabelsSha256 = "298633d94d0031d2081c0893f29c82eab7f0df00b08483ba8f29d1e979441217"
$imageTaggerUrl = "https://huggingface.co/SmilingWolf/wd-vit-tagger-v3/resolve/790b0e92cefd2a0221451604e7831fe643ab7c4f/model.onnx?download=true"
$imageTaggerLabelsUrl = "https://huggingface.co/SmilingWolf/wd-vit-tagger-v3/resolve/790b0e92cefd2a0221451604e7831fe643ab7c4f/selected_tags.csv?download=true"
$languageModelFileName = "Qwen3-1.7B-Q4_K_M.gguf"
$languageModelVersion = "daeb8e2-qwen3-1.7b-q4-k-m"
$languageModelSha256 = "d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5"
$languageModelUrl = "https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF/resolve/daeb8e2/Qwen3-1.7B-Q4_K_M.gguf?download=true"
$runtimeReleaseUrl = "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest"
$runtimeReleasesUrl = "https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=10"
$runtimePatterns = @(
    "^llama-b.+-bin-win-x64\.zip$",
    "^llama-b.+-bin-win-cpu-x64\.zip$",
    "^llama-b.+-bin-win-avx2-x64\.zip$",
    "^llama-b.+-bin-win-avx-x64\.zip$",
    "^llama-b.+-bin-win-noavx-x64\.zip$"
)
$runtimeExcludedPattern =
    "(?:cuda|cudart|vulkan|sycl|hip|openvino|rpc|kompute|clblast)"
$headers = @{
    "User-Agent" = "AIExplorer-Build/0.82.4"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

function Test-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Expected
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    return $actual.Equals(
        $Expected,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-WindowsX64CpuRuntimeAsset {
    param(
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]]$Assets = @()
    )

    if ($null -eq $Assets -or $Assets.Count -eq 0) {
        return $null
    }

    foreach ($pattern in $runtimePatterns) {
        $matched = $Assets |
            Where-Object {
                $_.name -match $pattern -and
                $_.name -notmatch $runtimeExcludedPattern
            } |
            Select-Object -First 1
        if ($null -ne $matched) {
            return $matched
        }
    }

    return $Assets |
        Where-Object {
            $_.name -match "^llama-b.+-bin-win.*-?x64\.zip$" -and
            $_.name -notmatch $runtimeExcludedPattern
        } |
        Sort-Object -Property name |
        Select-Object -First 1
}

function Get-ReleaseAssets {
    param(
        [AllowNull()]
        [object]$Release
    )

    if ($null -eq $Release) {
        return @()
    }

    $assets = @($Release.assets)
    if ($assets.Count -gt 0) {
        return $assets
    }

    $assetsUrl = [string]$Release.assets_url
    if ([string]::IsNullOrWhiteSpace($assetsUrl)) {
        return @()
    }

    try {
        Write-Host "The embedded release asset list is empty; checking assets_url..."
        return @(Invoke-RestMethod `
            -Uri $assetsUrl `
            -Headers $headers `
            -Method Get)
    }
    catch {
        Write-Warning (
            "Unable to read llama.cpp release assets from assets_url: " +
            $_.Exception.Message)
        return @()
    }
}

function Find-WindowsX64CpuRuntimeRelease {
    param(
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]]$Releases = @()
    )

    foreach ($candidate in $Releases) {
        if ($null -eq $candidate) {
            continue
        }

        $candidateAssets = @(Get-ReleaseAssets -Release $candidate)
        $candidateAsset = Get-WindowsX64CpuRuntimeAsset `
            -Assets $candidateAssets
        if ($null -ne $candidateAsset) {
            return [pscustomobject]@{
                Release = $candidate
                Asset = $candidateAsset
            }
        }
    }

    return $null
}

function Download-VerifiedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        [Parameter(Mandatory = $true)]
        [string]$Sha256,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $temporary = "$Destination.download"
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }

    Write-Host "$Description download in progress..."
    Invoke-WebRequest `
        -Uri $Uri `
        -Headers $headers `
        -OutFile $temporary `
        -UseBasicParsing
    if (-not (Test-Sha256 -Path $temporary -Expected $Sha256)) {
        Remove-Item -LiteralPath $temporary -Force
        throw "$Description SHA-256 verification failed."
    }

    Move-Item -LiteralPath $temporary -Destination $Destination -Force
}

$resolvedBundleRoot = [System.IO.Path]::GetFullPath($BundleRoot)
New-Item -ItemType Directory -Path $resolvedBundleRoot -Force | Out-Null

$modelPath = Join-Path $resolvedBundleRoot $modelFileName
if (-not (Test-Sha256 -Path $modelPath -Expected $modelSha256)) {
    Download-VerifiedFile `
        -Uri $modelUrl `
        -Destination $modelPath `
        -Sha256 $modelSha256 `
        -Description "Multilingual E5 local AI model"
}
Set-Content `
    -LiteralPath (Join-Path $resolvedBundleRoot "model-version.txt") `
    -Value $modelVersion `
    -Encoding ASCII

$modelsRoot = Split-Path -Parent $resolvedBundleRoot
$visualRoot = Join-Path $modelsRoot "visual"
New-Item -ItemType Directory -Path $visualRoot -Force | Out-Null
$visualModelPath = Join-Path $visualRoot $visualModelFileName
if (-not (Test-Sha256 `
    -Path $visualModelPath `
    -Expected $visualModelSha256)) {
    Download-VerifiedFile `
        -Uri $visualModelUrl `
        -Destination $visualModelPath `
        -Sha256 $visualModelSha256 `
        -Description "SigLIP 2 visual search model"
}

$visualTokenizerPath = Join-Path $visualRoot $visualTokenizerFileName
if (-not (Test-Sha256 `
    -Path $visualTokenizerPath `
    -Expected $visualTokenizerSha256)) {
    Download-VerifiedFile `
        -Uri $visualTokenizerUrl `
        -Destination $visualTokenizerPath `
        -Sha256 $visualTokenizerSha256 `
        -Description "SigLIP 2 tokenizer"
}

$imageTaggerPath = Join-Path $visualRoot $imageTaggerFileName
if (-not (Test-Sha256 `
    -Path $imageTaggerPath `
    -Expected $imageTaggerSha256)) {
    Download-VerifiedFile `
        -Uri $imageTaggerUrl `
        -Destination $imageTaggerPath `
        -Sha256 $imageTaggerSha256 `
        -Description "WD ViT character image tagger"
}

$imageTaggerLabelsPath = Join-Path `
    $visualRoot `
    $imageTaggerLabelsFileName
if (-not (Test-Sha256 `
    -Path $imageTaggerLabelsPath `
    -Expected $imageTaggerLabelsSha256)) {
    Download-VerifiedFile `
        -Uri $imageTaggerLabelsUrl `
        -Destination $imageTaggerLabelsPath `
        -Sha256 $imageTaggerLabelsSha256 `
        -Description "WD ViT image tag labels"
}

Set-Content `
    -LiteralPath (Join-Path $visualRoot "model-version.txt") `
    -Value $visualModelVersion `
    -Encoding ASCII

$languageRoot = Join-Path $modelsRoot "language"
New-Item -ItemType Directory -Path $languageRoot -Force | Out-Null
$languageModelPath = Join-Path $languageRoot $languageModelFileName
if (-not (Test-Sha256 `
    -Path $languageModelPath `
    -Expected $languageModelSha256)) {
    Download-VerifiedFile `
        -Uri $languageModelUrl `
        -Destination $languageModelPath `
        -Sha256 $languageModelSha256 `
        -Description "Qwen3 natural-language search model"
}

Set-Content `
    -LiteralPath (Join-Path $languageRoot "model-version.txt") `
    -Value $languageModelVersion `
    -Encoding ASCII

Write-Host "Checking the latest llama.cpp CPU runtime..."
$latestRelease = Invoke-RestMethod `
    -Uri $runtimeReleaseUrl `
    -Headers $headers `
    -Method Get
$runtimeSelection = Find-WindowsX64CpuRuntimeRelease `
    -Releases @($latestRelease)

if ($null -eq $runtimeSelection) {
    Write-Host "The latest release has no usable CPU asset; checking recent releases..."
    $recentReleases = @(Invoke-RestMethod `
        -Uri $runtimeReleasesUrl `
        -Headers $headers `
        -Method Get)
    $runtimeSelection = Find-WindowsX64CpuRuntimeRelease `
        -Releases $recentReleases
}

if ($null -eq $runtimeSelection) {
    $latestAssets = @(Get-ReleaseAssets -Release $latestRelease)
    $windowsAssets = @($latestAssets |
        Where-Object { $_.name -match "bin-win" } |
        ForEach-Object { $_.name }) -join ", "
    if ([string]::IsNullOrWhiteSpace($windowsAssets)) {
        $windowsAssets = "none returned by the GitHub API"
    }
    throw (
        "The Windows x64 llama.cpp CPU runtime was not found after " +
        "checking the latest and 10 recent releases. " +
        "Latest release Windows assets: " + $windowsAssets)
}

$release = $runtimeSelection.Release
$runtimeAsset = $runtimeSelection.Asset
Write-Host (
    "Selected llama.cpp runtime asset: " +
    $runtimeAsset.name +
    " (" +
    $release.tag_name +
    ")")
if ([string]::IsNullOrWhiteSpace($runtimeAsset.digest) -or
    -not $runtimeAsset.digest.StartsWith(
        "sha256:",
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The llama.cpp runtime release has no SHA-256 digest."
}

$runtimeVersion = [string]$release.tag_name
$runtimeDirectory = Join-Path $resolvedBundleRoot "llama-runtime"
$serverPath = Join-Path $runtimeDirectory "llama-server.exe"
$runtimeVersionPath = Join-Path $resolvedBundleRoot "runtime-version.txt"
$installedRuntimeVersion = if (
    Test-Path -LiteralPath $runtimeVersionPath -PathType Leaf
) {
    (Get-Content -LiteralPath $runtimeVersionPath -Raw).Trim()
} else {
    ""
}

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf) -or
    -not $installedRuntimeVersion.Equals(
        $runtimeVersion,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    $temporaryRoot = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("AIExplorer-AI-Bundle-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    try {
        $archivePath = Join-Path $temporaryRoot "llama-runtime.zip"
        $runtimeSha256 = $runtimeAsset.digest.Substring(7)
        Download-VerifiedFile `
            -Uri ([string]$runtimeAsset.browser_download_url) `
            -Destination $archivePath `
            -Sha256 $runtimeSha256 `
            -Description "llama.cpp CPU runtime"

        $extractPath = Join-Path $temporaryRoot "extracted"
        Expand-Archive `
            -LiteralPath $archivePath `
            -DestinationPath $extractPath `
            -Force
        $serverFile = Get-ChildItem `
            -LiteralPath $extractPath `
            -Filter "llama-server.exe" `
            -File `
            -Recurse |
            Select-Object -First 1
        if ($null -eq $serverFile) {
            throw "llama-server.exe was not found in the runtime archive."
        }

        if (Test-Path -LiteralPath $runtimeDirectory) {
            Remove-Item -LiteralPath $runtimeDirectory -Recurse -Force
        }
        New-Item `
            -ItemType Directory `
            -Path $runtimeDirectory `
            -Force | Out-Null
        Copy-Item `
            -Path (Join-Path $serverFile.Directory.FullName "*") `
            -Destination $runtimeDirectory `
            -Recurse `
            -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}

Set-Content `
    -LiteralPath $runtimeVersionPath `
    -Value $runtimeVersion `
    -Encoding ASCII

Write-Host "AI bundle preparation completed:"
Write-Host "  Model: $modelFileName"
Write-Host "  Model version: $modelVersion"
Write-Host "  Visual model: $visualModelFileName"
Write-Host "  Visual model version: $visualModelVersion"
Write-Host "  Image tagger: $imageTaggerFileName"
Write-Host "  Image tag labels: $imageTaggerLabelsFileName"
Write-Host "  Language model: $languageModelFileName"
Write-Host "  Language model version: $languageModelVersion"
Write-Host "  CPU runtime: $runtimeVersion"
