namespace AIExplorer.Services;

public static class BackgroundIndexRootPlanner
{
    public static IReadOnlyList<string> OrderRoots(
        IEnumerable<string> activeSearchRoots,
        IEnumerable<string> favoriteRoots,
        IEnumerable<string> allAvailableRoots)
    {
        ArgumentNullException.ThrowIfNull(activeSearchRoots);
        ArgumentNullException.ThrowIfNull(favoriteRoots);
        ArgumentNullException.ThrowIfNull(allAvailableRoots);

        var orderedRoots = new List<string>();
        var seenRoots = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        AppendDistinct(activeSearchRoots, orderedRoots, seenRoots);
        AppendDistinct(favoriteRoots, orderedRoots, seenRoots);
        AppendDistinct(allAvailableRoots, orderedRoots, seenRoots);
        return orderedRoots;
    }

    private static void AppendDistinct(
        IEnumerable<string> roots,
        ICollection<string> orderedRoots,
        ISet<string> seenRoots)
    {
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var normalized = root.Trim();
            if (seenRoots.Add(normalized))
            {
                orderedRoots.Add(normalized);
            }
        }
    }
}
