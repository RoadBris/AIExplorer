using System.Windows.Media;

namespace AIExplorer.Models;

public sealed class FileSystemEntry
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public long? SizeBytes { get; init; }

    public required string SizeDisplay { get; init; }

    public DateTime ModifiedAt { get; init; }

    public required string ModifiedDisplay { get; init; }

    public required string TypeDisplay { get; init; }

    public required string IconGlyph { get; init; }

    public ImageSource? IconImage { get; init; }
}
