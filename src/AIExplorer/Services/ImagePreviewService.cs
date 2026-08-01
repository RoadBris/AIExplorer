using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace AIExplorer.Services;

public sealed class ImagePreviewService
{
    private const long MaximumPreviewFileBytes = 64L * 1024L * 1024L;
    private const int MaximumPreviewPixels = 420;

    private static readonly HashSet<string> SupportedExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".webp", ".heic"
        ],
        StringComparer.OrdinalIgnoreCase);

    public bool CanPreview(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task<BitmapSource?> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!CanPreview(path))
        {
            return null;
        }

        return await Task.Run(
            () => LoadCore(path, cancellationToken),
            cancellationToken);
    }

    private static BitmapSource? LoadCore(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            if (!file.Exists ||
                file.Length <= 0 ||
                file.Length > MaximumPreviewFileBytes)
            {
                return null;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                32_768,
                FileOptions.SequentialScan);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile |
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null ||
                frame.PixelWidth <= 0 ||
                frame.PixelHeight <= 0)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            var preview = new BitmapImage();
            preview.BeginInit();
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.CreateOptions =
                BitmapCreateOptions.IgnoreColorProfile;
            if (frame.PixelWidth >= frame.PixelHeight)
            {
                preview.DecodePixelWidth = Math.Min(
                    MaximumPreviewPixels,
                    frame.PixelWidth);
            }
            else
            {
                preview.DecodePixelHeight = Math.Min(
                    MaximumPreviewPixels,
                    frame.PixelHeight);
            }

            preview.StreamSource = stream;
            preview.EndInit();
            preview.Freeze();
            return preview;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            ArgumentException or
            InvalidOperationException or
            OverflowException or
            FileFormatException or
            COMException)
        {
            return null;
        }
    }
}
