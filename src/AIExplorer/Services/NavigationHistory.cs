namespace AIExplorer.Services;

public sealed class NavigationHistory
{
    private readonly List<string> _entries = [];
    private int _index = -1;

    public bool CanGoBack => _index > 0;

    public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;

    public string? Current => _index >= 0 && _index < _entries.Count
        ? _entries[_index]
        : null;

    public void Record(string path)
    {
        if (string.Equals(Current, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_index < _entries.Count - 1)
        {
            _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        }

        _entries.Add(path);
        _index = _entries.Count - 1;
    }

    public string? Back()
    {
        if (!CanGoBack)
        {
            return null;
        }

        _index--;
        return _entries[_index];
    }

    public string? Forward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        _index++;
        return _entries[_index];
    }
}
