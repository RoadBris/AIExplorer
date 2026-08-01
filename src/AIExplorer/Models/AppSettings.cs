namespace AIExplorer.Models;

public sealed class AppSettings
{
    public string? LastPath { get; set; }

    public List<NetworkLocation> NetworkLocations { get; set; } = [];

    public List<FavoriteLocation> Favorites { get; set; } = [];

    public bool SearchPanelVisible { get; set; } = true;

    public double SearchPanelWidth { get; set; } = 620;

    public double InstantTitlePanelWidth { get; set; } = 460;

    public double SearchPanelHeight { get; set; } = 430;

    public FileSortMode SortMode { get; set; } = FileSortMode.Name;

    public SearchResultSortMode SearchResultSortMode { get; set; } =
        SearchResultSortMode.Relevance;

    public bool CompactRows { get; set; }

    public bool UseSystemTrayBackground { get; set; } = true;
}

public sealed class FavoriteLocation
{
    public required string Name { get; set; }

    public required string Path { get; set; }
}

public sealed class NetworkLocation
{
    public required string Name { get; set; }

    public required string Path { get; set; }
}

public enum FileSortMode
{
    Name,
    Modified,
    Type,
    Size
}

public enum SearchResultSortMode
{
    Relevance,
    TopLevelPath,
    Name,
    ModifiedNewest
}
