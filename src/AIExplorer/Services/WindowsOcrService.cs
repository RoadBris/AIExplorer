using System.Runtime.InteropServices;
using System.Text;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AIExplorer.Services;

public interface IOcrTextExtractor
{
    bool IsAvailable { get; }

    bool CanExtract(string extension);

    Task<OcrTextExtraction?> ExtractAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed class WindowsOcrService : IOcrTextExtractor
{
    private const long MaximumImageBytes = 48L * 1024 * 1024;
    private const long MaximumPdfBytes = 96L * 1024 * 1024;
    private const int MaximumPdfPages = 3;
    private const int MaximumCharacters = 12_000;
    private const uint PdfRenderWidth = 1_600;

    private static readonly HashSet<string> ImageExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
            ".webp", ".heic"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly Lazy<OcrEngine?> _engine = new(
        CreateEngine,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public bool IsAvailable =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) &&
        _engine.Value is not null;

    public bool CanExtract(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return normalized.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               ImageExtensions.Contains(normalized);
    }

    public async Task<OcrTextExtraction?> ExtractAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable || !CanExtract(Path.GetExtension(path)))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                return null;
            }

            return Path.GetExtension(path).Equals(
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
                ? fileInfo.Length <= MaximumPdfBytes
                    ? await ExtractPdfAsync(path, cancellationToken)
                    : null
                : fileInfo.Length <= MaximumImageBytes
                    ? await ExtractImageAsync(path, cancellationToken)
                    : null;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            COMException or
            ArgumentException)
        {
            AppLog.Warning(
                $"OCR에서 파일을 건너뜁니다: {path} · {exception.Message}");
            return null;
        }
    }

    private async Task<OcrTextExtraction?> ExtractImageAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var bitmap = await DecodeForOcrAsync(stream, cancellationToken);
        if (bitmap is null)
        {
            return null;
        }

        using (bitmap)
        {
            var text = await RecognizeAsync(bitmap, cancellationToken);
            return CreateResult(
                text,
                DocumentContentSource.ImageOcr,
                pagesAnalyzed: 1);
        }
    }

    private async Task<OcrTextExtraction?> ExtractPdfAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        cancellationToken.ThrowIfCancellationRequested();
        var document = await PdfDocument.LoadFromFileAsync(file);
        var pageIndexes = SelectPdfPages(document.PageCount);
        if (pageIndexes.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var pageIndex in pageIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = document.GetPage(pageIndex);
            using var rendered = new InMemoryRandomAccessStream();
            var options = new PdfPageRenderOptions
            {
                DestinationWidth = PdfRenderWidth
            };
            await page.RenderToStreamAsync(rendered, options);
            rendered.Seek(0);

            var bitmap = await DecodeForOcrAsync(
                rendered,
                cancellationToken);
            if (bitmap is null)
            {
                continue;
            }

            using (bitmap)
            {
                var pageText = await RecognizeAsync(
                    bitmap,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(pageText))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                var remaining = MaximumCharacters - builder.Length;
                builder.Append(
                    pageText.AsSpan(
                        0,
                        Math.Min(pageText.Length, remaining)));
                if (builder.Length >= MaximumCharacters)
                {
                    break;
                }
            }
        }

        return CreateResult(
            builder.ToString(),
            DocumentContentSource.PdfOcr,
            pageIndexes.Count);
    }

    private async Task<SoftwareBitmap?> DecodeForOcrAsync(
        IRandomAccessStream stream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        cancellationToken.ThrowIfCancellationRequested();

        var maximumDimension = (uint)Math.Min(
            2_400,
            OcrEngine.MaxImageDimension);
        var scale = Math.Min(
            1d,
            maximumDimension /
            (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(
                1u,
                (uint)Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(
                1u,
                (uint)Math.Round(decoder.PixelHeight * scale))
        };

        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
    }

    private async Task<string> RecognizeAsync(
        SoftwareBitmap bitmap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _engine.Value!.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Text;
    }

    private static OcrTextExtraction? CreateResult(
        string text,
        DocumentContentSource source,
        int pagesAnalyzed)
    {
        var normalized = string.Join(
            " ",
            text.Split(
                ['\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var wasTruncated = normalized.Length > MaximumCharacters;
        if (wasTruncated)
        {
            normalized = normalized[..MaximumCharacters];
        }

        return new OcrTextExtraction(
            normalized,
            source,
            pagesAnalyzed,
            wasTruncated);
    }

    private static IReadOnlyList<uint> SelectPdfPages(uint pageCount)
    {
        if (pageCount == 0)
        {
            return [];
        }

        if (pageCount <= MaximumPdfPages)
        {
            return Enumerable.Range(0, (int)pageCount)
                .Select(index => (uint)index)
                .ToArray();
        }

        return new[]
        {
            0u,
            pageCount / 2,
            pageCount - 1
        };
    }

    private static OcrEngine? CreateEngine()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return null;
        }

        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.')
            ? normalized
            : $".{normalized}";
    }
}

public sealed record OcrTextExtraction(
    string Text,
    DocumentContentSource Source,
    int PagesAnalyzed,
    bool WasTruncated);
