using AIExplorer.Models;

namespace AIExplorer.Services;

public static class SearchResultSortService
{
    public static IReadOnlyList<SearchResult> Sort(
        IEnumerable<SearchResult> results,
        SearchResultSortMode mode)
    {
        var source = results as IReadOnlyList<SearchResult> ??
                     results.ToArray();
        return mode switch
        {
            SearchResultSortMode.TopLevelPath => source
                .OrderBy(
                    result => GetRootPath(result.FullPath),
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    result => GetTopLevelPath(result.FullPath),
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    result => result.FullPath,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            SearchResultSortMode.Name => source
                .OrderBy(
                    result => result.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    result => result.FullPath,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            SearchResultSortMode.ModifiedNewest => source
                .OrderByDescending(result => result.ModifiedUtc)
                .ThenBy(
                    result => result.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    result => result.FullPath,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            _ => source.ToArray()
        };
    }

    private static string GetRootPath(string fullPath)
    {
        try
        {
            return Path.GetPathRoot(fullPath) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetTopLevelPath(string fullPath)
    {
        try
        {
            var root = Path.GetPathRoot(fullPath) ?? string.Empty;
            var remainder = fullPath[root.Length..]
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var separator = remainder.IndexOfAny(
                [
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ]);
            var firstSegment = separator < 0
                ? remainder
                : remainder[..separator];
            return string.IsNullOrWhiteSpace(firstSegment)
                ? root
                : Path.Combine(root, firstSegment);
        }
        catch
        {
            return fullPath;
        }
    }
}
