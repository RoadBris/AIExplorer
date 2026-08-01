using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AIExplorer.Services;

public sealed class VisualFrameLoader
{
    public const int ImageSize = 224;

    private const uint PdfRenderWidth = 896;
    private const int MaximumPdfPages = 3;
    private const long MaximumImageBytes = 64L * 1024 * 1024;
    private const long MaximumPdfBytes = 128L * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
            ".webp", ".heic"
        ],
        StringComparer.OrdinalIgnoreCase);

    public bool CanAnalyze(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return ImageExtensions.Contains(normalized) ||
               normalized.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<float[]>> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var extension = NormalizeExtension(Path.GetExtension(path));
        if (!CanAnalyze(extension))
        {
            return [];
        }

        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0)
        {
            return [];
        }

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return file.Length <= MaximumPdfBytes
                ? await LoadPdfAsync(path, cancellationToken)
                : [];
        }

        if (file.Length > MaximumImageBytes)
        {
            return [];
        }

        var pixels = await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    65_536,
                    FileOptions.SequentialScan);
                return DecodeImageFrames(stream);
            },
            cancellationToken);
        return pixels;
    }

    private static async Task<IReadOnlyList<float[]>> LoadPdfAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return [];
        }

        var storageFile = await StorageFile.GetFileFromPathAsync(path);
        cancellationToken.ThrowIfCancellationRequested();
        var document = await PdfDocument.LoadFromFileAsync(storageFile);
        var pageIndexes = SelectPdfPages(document.PageCount);
        var frames = new List<float[]>(pageIndexes.Count);
        foreach (var pageIndex in pageIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = document.GetPage(pageIndex);
            using var randomAccessStream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(
                randomAccessStream,
                new PdfPageRenderOptions
                {
                    DestinationWidth = PdfRenderWidth
                });
            randomAccessStream.Seek(0);

            var bytes = new byte[checked((int)randomAccessStream.Size)];
            using (var reader = new DataReader(
                       randomAccessStream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)bytes.Length);
                reader.ReadBytes(bytes);
            }

            var normalized = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream(
                        bytes,
                        writable: false);
                    return DecodeWholeFrameAndNormalize(stream);
                },
                cancellationToken);
            if (normalized is not null)
            {
                frames.Add(normalized);
            }
        }

        return frames;
    }

    private static IReadOnlyList<float[]> DecodeImageFrames(Stream stream)
    {
        var source = DecodeBitmap(stream);
        if (source is null)
        {
            return [];
        }

        var frames = new List<float[]>(2);
        var whole = NormalizePixels(RenderWholeFrame(source));
        if (whole is not null)
        {
            frames.Add(whole);
        }

        var longSide = Math.Max(source.PixelWidth, source.PixelHeight);
        var shortSide = Math.Max(1, Math.Min(source.PixelWidth, source.PixelHeight));
        if (longSide / (double)shortSide >= 1.18d)
        {
            var center = NormalizePixels(RenderCenterCrop(source));
            if (center is not null)
            {
                frames.Add(center);
            }
        }

        return frames;
    }

    private static float[]? DecodeWholeFrameAndNormalize(Stream stream)
    {
        var source = DecodeBitmap(stream);
        return source is null
            ? null
            : NormalizePixels(RenderWholeFrame(source));
    }

    private static BitmapSource? DecodeBitmap(Stream stream)
    {
        try
        {
            return BitmapFrame.Create(
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
    }

    private static BitmapSource RenderWholeFrame(BitmapSource source)
    {
        var scale = Math.Min(
            ImageSize / (double)Math.Max(1, source.PixelWidth),
            ImageSize / (double)Math.Max(1, source.PixelHeight));
        var width = Math.Max(1d, source.PixelWidth * scale);
        var height = Math.Max(1d, source.PixelHeight * scale);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                Brushes.White,
                pen: null,
                new Rect(0, 0, ImageSize, ImageSize));
            context.DrawImage(
                source,
                new Rect(
                    (ImageSize - width) / 2d,
                    (ImageSize - height) / 2d,
                    width,
                    height));
        }

        var rendered = new RenderTargetBitmap(
            ImageSize,
            ImageSize,
            96d,
            96d,
            PixelFormats.Pbgra32);
        rendered.Render(visual);
        return rendered;
    }

    private static BitmapSource RenderCenterCrop(BitmapSource source)
    {
        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(
                source,
                PixelFormats.Bgra32,
                destinationPalette: null,
                alphaThreshold: 0);
        }

        var scale = Math.Max(
            ImageSize / (double)Math.Max(1, source.PixelWidth),
            ImageSize / (double)Math.Max(1, source.PixelHeight));
        var scaledWidth = Math.Max(
            ImageSize,
            (int)Math.Round(source.PixelWidth * scale));
        var scaledHeight = Math.Max(
            ImageSize,
            (int)Math.Round(source.PixelHeight * scale));
        if (scaledWidth != source.PixelWidth ||
            scaledHeight != source.PixelHeight)
        {
            source = new TransformedBitmap(
                source,
                new ScaleTransform(
                    scaledWidth / (double)source.PixelWidth,
                    scaledHeight / (double)source.PixelHeight));
        }

        var left = Math.Max(0, (source.PixelWidth - ImageSize) / 2);
        var top = Math.Max(0, (source.PixelHeight - ImageSize) / 2);
        source = new CroppedBitmap(
            source,
            new Int32Rect(
                left,
                top,
                ImageSize,
                ImageSize));
        return source;
    }

    private static float[]? NormalizePixels(BitmapSource source)
    {
        if (source.PixelWidth != ImageSize || source.PixelHeight != ImageSize)
        {
            return null;
        }

        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(
                source,
                PixelFormats.Bgra32,
                destinationPalette: null,
                alphaThreshold: 0);
        }

        var stride = ImageSize * 4;
        var bytes = new byte[stride * ImageSize];
        source.CopyPixels(bytes, stride, 0);

        var values = new float[3 * ImageSize * ImageSize];
        var planeLength = ImageSize * ImageSize;
        ReadOnlySpan<float> means = [0.5f, 0.5f, 0.5f];
        ReadOnlySpan<float> standardDeviations = [0.5f, 0.5f, 0.5f];
        for (var pixelIndex = 0;
             pixelIndex < planeLength;
             pixelIndex++)
        {
            var byteIndex = pixelIndex * 4;
            var blue = bytes[byteIndex] / 255f;
            var green = bytes[byteIndex + 1] / 255f;
            var red = bytes[byteIndex + 2] / 255f;
            values[pixelIndex] =
                (red - means[0]) / standardDeviations[0];
            values[planeLength + pixelIndex] =
                (green - means[1]) / standardDeviations[1];
            values[planeLength * 2 + pixelIndex] =
                (blue - means[2]) / standardDeviations[2];
        }

        return values;
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
