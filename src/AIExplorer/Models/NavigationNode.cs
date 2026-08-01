using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AIExplorer.Models;

public sealed class NavigationNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isLoaded;

    public NavigationNode(
        string name,
        string? path,
        string iconGlyph,
        NavigationNodeKind kind,
        bool canExpand = false,
        ImageSource? iconImage = null)
    {
        Name = name;
        Path = path;
        IconGlyph = iconGlyph;
        Kind = kind;
        IconImage = iconImage;

        if (canExpand)
        {
            Children.Add(CreatePlaceholder());
        }
    }

    public string Name { get; }

    public string? Path { get; }

    public string IconGlyph { get; }

    public ImageSource? IconImage { get; }

    public NavigationNodeKind Kind { get; }

    public bool IsSelectable =>
        Kind == NavigationNodeKind.Computer ||
        !string.IsNullOrWhiteSpace(Path);

    public ObservableCollection<NavigationNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (_isLoaded == value)
            {
                return;
            }

            _isLoaded = value;
            OnPropertyChanged();
        }
    }

    public bool IsPlaceholder => Kind == NavigationNodeKind.Placeholder;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static NavigationNode CreatePlaceholder() =>
        new(string.Empty, null, string.Empty, NavigationNodeKind.Placeholder);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum NavigationNodeKind
{
    Section,
    FavoritesSection,
    Computer,
    SpecialFolder,
    Favorite,
    Drive,
    Folder,
    Network,
    NetworkServer,
    NetworkShare,
    Placeholder
}
