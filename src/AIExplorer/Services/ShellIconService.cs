using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AIExplorer.Services;

public sealed class ShellIconService
{
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiSmallIcon = 0x00000001;
    private const uint ShgfiUseFileAttributes = 0x00000010;
    private const uint ShgsiIcon = 0x00000100;
    private const uint ShgsiSmallIcon = 0x00000001;

    private readonly ConcurrentDictionary<string, ImageSource> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? GetFileSystemIcon(
        string path,
        bool isDirectory,
        bool preferSpecific = false)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var extension = isDirectory
            ? string.Empty
            : Path.GetExtension(path).ToLowerInvariant();
        var useActualPath = preferSpecific ||
                            extension is ".exe" or ".lnk" or ".url" or ".ico";
        var cacheKey = useActualPath
            ? $"path:{path}"
            : isDirectory
                ? "type:folder"
                : $"type:{extension}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var icon = useActualPath
            ? LoadFileSystemIcon(path, 0, useFileAttributes: false)
            : LoadFileSystemIcon(
                isDirectory ? "folder" : $"file{extension}",
                isDirectory ? FileAttributeDirectory : FileAttributeNormal,
                useFileAttributes: true);

        if (icon is not null)
        {
            _cache.TryAdd(cacheKey, icon);
        }

        return icon;
    }

    public ImageSource? GetStockIcon(ShellStockIcon stockIcon)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var cacheKey = $"stock:{(uint)stockIcon}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;
        try
        {
            using var com = new ComScope();
            var info = new ShellStockIconInfo
            {
                Size = (uint)Marshal.SizeOf<ShellStockIconInfo>()
            };
            var result = SHGetStockIconInfo(
                (uint)stockIcon,
                ShgsiIcon | ShgsiSmallIcon,
                ref info);

            if (result >= 0 && info.IconHandle != IntPtr.Zero)
            {
                icon = CreateFrozenImage(info.IconHandle);
            }
            else if (info.IconHandle != IntPtr.Zero)
            {
                DestroyIcon(info.IconHandle);
            }
        }
        catch
        {
            // Icons are decorative; the UI keeps its glyph fallback on failure.
        }

        if (icon is not null)
        {
            _cache.TryAdd(cacheKey, icon);
        }

        return icon;
    }

    private static ImageSource? LoadFileSystemIcon(
        string path,
        uint attributes,
        bool useFileAttributes)
    {
        try
        {
            using var com = new ComScope();
            var info = new ShellFileInfo();
            var flags = ShgfiIcon | ShgfiSmallIcon;
            if (useFileAttributes)
            {
                flags |= ShgfiUseFileAttributes;
            }

            var result = SHGetFileInfo(
                path,
                attributes,
                ref info,
                (uint)Marshal.SizeOf<ShellFileInfo>(),
                flags);

            if (result != IntPtr.Zero && info.IconHandle != IntPtr.Zero)
            {
                return CreateFrozenImage(info.IconHandle);
            }

            if (info.IconHandle != IntPtr.Zero)
            {
                DestroyIcon(info.IconHandle);
            }

            return null;
        }
        catch
        {
            // Missing icon associations must never prevent folder enumeration.
            return null;
        }
    }

    private static ImageSource? CreateFrozenImage(IntPtr iconHandle)
    {
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr SHGetFileInfoW(
        string path,
        uint fileAttributes,
        ref ShellFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    private static IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShellFileInfo fileInfo,
        uint fileInfoSize,
        uint flags) =>
        SHGetFileInfoW(path, fileAttributes, ref fileInfo, fileInfoSize, flags);

    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(
        uint stockIconId,
        uint flags,
        ref ShellStockIconInfo stockIconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string? DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string? TypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellStockIconInfo
    {
        public uint Size;
        public IntPtr IconHandle;
        public int SystemImageIndex;
        public int IconIndex;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string? Path;
    }

    private readonly struct ComScope : IDisposable
    {
        private readonly bool _shouldUninitialize;

        public ComScope()
        {
            _shouldUninitialize = CoInitializeEx(IntPtr.Zero, 0) >= 0;
        }

        public void Dispose()
        {
            if (_shouldUninitialize)
            {
                CoUninitialize();
            }
        }
    }
}

public enum ShellStockIcon : uint
{
    Folder = 3,
    RemovableDrive = 7,
    FixedDrive = 8,
    NetworkDrive = 9,
    OpticalDrive = 11,
    RamDrive = 12,
    MyNetwork = 17,
    UnknownDrive = 58,
    DesktopPc = 94
}
