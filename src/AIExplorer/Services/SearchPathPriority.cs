namespace AIExplorer.Services;

public static class SearchPathPriority
{
    public static IReadOnlyList<string> GetTraversalRoots(string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfInside(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddIfInside(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        var userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddIfInside(Path.Combine(userProfile, "Downloads"));
            AddIfInside(userProfile);
        }

        AddIfInside(normalizedRoot);
        return paths;

        void AddIfInside(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath) &&
                    IsInsideRoot(fullPath, normalizedRoot) &&
                    seen.Add(fullPath))
                {
                    paths.Add(fullPath);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                IOException or
                UnauthorizedAccessException)
            {
                // An unavailable shell folder is simply not prioritized.
            }
        }
    }

    public static int GetPathPriority(string path)
    {
        var priority = 0;
        priority = Math.Max(
            priority,
            GetSpecialFolderPriority(
                path,
                Environment.SpecialFolder.DesktopDirectory,
                50));
        priority = Math.Max(
            priority,
            GetSpecialFolderPriority(
                path,
                Environment.SpecialFolder.MyDocuments,
                45));

        var userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            priority = Math.Max(
                priority,
                IsInsideRoot(path, Path.Combine(userProfile, "Downloads"))
                    ? 45
                    : 0);
            priority = Math.Max(
                priority,
                IsInsideRoot(path, userProfile)
                    ? 30
                    : 0);
        }

        return priority;
    }

    public static bool IsInsideRoot(string path, string root)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root);
            if (fullPath.Equals(
                    fullRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var rootWithSeparator = fullRoot.EndsWith(
                    Path.DirectorySeparatorChar)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static int GetSpecialFolderPriority(
        string path,
        Environment.SpecialFolder folder,
        int priority)
    {
        var specialPath = Environment.GetFolderPath(folder);
        return !string.IsNullOrWhiteSpace(specialPath) &&
               IsInsideRoot(path, specialPath)
            ? priority
            : 0;
    }
}
