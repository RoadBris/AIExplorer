using AIExplorer.Models;

namespace AIExplorer.Services;

/// <summary>
/// Read-only in-memory accelerator built from a completed metadata snapshot.
/// Item identifiers follow name order, so the common name sort does not need
/// to sort matches again for every keystroke.
/// </summary>
public sealed class InstantTitleMemoryIndex
{
    private static readonly int[] EmptyCandidates = [];
    private readonly IndexedFileRecord[] _itemsByName;
    private readonly Dictionary<char, int[]> _nameCharacterPostings;
    private readonly Dictionary<char, int[]> _contextCharacterPostings;

    private InstantTitleMemoryIndex(
        IndexedFileRecord[] itemsByName,
        Dictionary<char, int[]> nameCharacterPostings,
        Dictionary<char, int[]> contextCharacterPostings)
    {
        _itemsByName = itemsByName;
        _nameCharacterPostings = nameCharacterPostings;
        _contextCharacterPostings = contextCharacterPostings;
    }

    public int Count => _itemsByName.Length;

    public IndexedFileRecord this[int itemId] => _itemsByName[itemId];

    public static InstantTitleMemoryIndex Create(
        IReadOnlyList<IndexedFileRecord> source,
        CancellationToken cancellationToken = default)
    {
        var items = source
            .OrderBy(
                item => item.Name,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                item => item.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var namePostings = new Dictionary<char, List<int>>();
        var contextPostings = new Dictionary<char, List<int>>();
        for (var itemId = 0; itemId < items.Length; itemId++)
        {
            if ((itemId & 2047) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = items[itemId];
            AddDistinctCharacters(
                namePostings,
                item.Name,
                itemId);
            AddDistinctCharacters(
                contextPostings,
                item.Name,
                itemId);
            AddDistinctCharacters(
                contextPostings,
                BuildParentContext(item.FullPath),
                itemId);
        }

        return new InstantTitleMemoryIndex(
            items,
            Freeze(namePostings),
            Freeze(contextPostings));
    }

    public IReadOnlyList<int> FindNameCandidates(string query) =>
        FindRarestPosting(
            _nameCharacterPostings,
            query);

    public IReadOnlyList<int> FindContextCandidates(
        IEnumerable<string> alternatives)
    {
        var candidatePostings = new List<IReadOnlyList<int>>();
        foreach (var alternative in alternatives
                     .Where(term => !string.IsNullOrWhiteSpace(term))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var posting = FindRarestPosting(
                _contextCharacterPostings,
                alternative);
            if (posting.Count == 0)
            {
                continue;
            }

            candidatePostings.Add(posting);
        }

        if (candidatePostings.Count == 0)
        {
            return EmptyCandidates;
        }

        // Rare terms are examined first. A distinctive character or project
        // name therefore reaches the matcher before broad words such as
        // "file", "image", or "mod" can fill the result limit.
        var seen = new HashSet<int>();
        var prioritized = new List<int>();
        foreach (var posting in candidatePostings
                     .OrderBy(item => item.Count))
        {
            foreach (var itemId in posting)
            {
                if (seen.Add(itemId))
                {
                    prioritized.Add(itemId);
                }
            }
        }

        return prioritized;
    }

    private static IReadOnlyList<int> FindRarestPosting(
        IReadOnlyDictionary<char, int[]> postings,
        string text)
    {
        if (text.Length == 1)
        {
            return postings.TryGetValue(
                char.ToUpperInvariant(text[0]),
                out var directPosting)
                ? directPosting
                : EmptyCandidates;
        }

        int[]? rarest = null;
        var examined = new HashSet<char>();
        foreach (var character in text)
        {
            var key = char.ToUpperInvariant(character);
            if (!examined.Add(key))
            {
                continue;
            }

            if (!postings.TryGetValue(key, out var posting))
            {
                return EmptyCandidates;
            }

            if (rarest is null || posting.Length < rarest.Length)
            {
                rarest = posting;
            }
        }

        return rarest ?? EmptyCandidates;
    }

    private static void AddDistinctCharacters(
        Dictionary<char, List<int>> postings,
        string text,
        int itemId)
    {
        foreach (var character in text)
        {
            var key = char.ToUpperInvariant(character);
            if (!postings.TryGetValue(key, out var posting))
            {
                posting = [];
                postings.Add(key, posting);
            }

            // Items are processed consecutively. The last identifier is
            // therefore enough to avoid duplicate characters per item.
            if (posting.Count == 0 || posting[^1] != itemId)
            {
                posting.Add(itemId);
            }
        }
    }

    private static Dictionary<char, int[]> Freeze(
        Dictionary<char, List<int>> source)
    {
        var frozen = new Dictionary<char, int[]>(source.Count);
        foreach (var (character, posting) in source)
        {
            frozen.Add(character, posting.ToArray());
        }

        return frozen;
    }

    private static string BuildParentContext(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        var segments = new Stack<string>(3);
        var current = directory;
        for (var depth = 0; depth < 3; depth++)
        {
            var name = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(current));
            if (!string.IsNullOrWhiteSpace(name))
            {
                segments.Push(name);
            }

            var parent = Path.GetDirectoryName(
                Path.TrimEndingDirectorySeparator(current));
            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(
                    parent,
                    current,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return string.Join(' ', segments);
    }
}
