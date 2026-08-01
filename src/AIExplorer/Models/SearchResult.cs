using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AIExplorer.Models;

public sealed class SearchResult : INotifyPropertyChanged
{
    private static readonly HashSet<string> PreviewExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".webp", ".heic"
        ],
        StringComparer.OrdinalIgnoreCase);

    private ImageSource? _previewImage;

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required string DirectoryPath { get; init; }

    public required string TypeDisplay { get; init; }

    public required string ModifiedDisplay { get; init; }

    public DateTime CreatedUtc { get; init; }

    public DateTime ModifiedUtc { get; init; }

    public long? SizeBytes { get; init; }

    public required string Reason { get; init; }

    public required string IconGlyph { get; init; }

    public ImageSource? IconImage { get; init; }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (ReferenceEquals(_previewImage, value))
            {
                return;
            }

            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreview));
        }
    }

    public bool HasPreview => PreviewImage is not null;

    public bool CanReservePreviewSpace =>
        PreviewExtensions.Contains(Path.GetExtension(FullPath));

    public double Score { get; init; }

    public double MatchPercent { get; init; }

    public bool WasAiAnalyzed { get; init; }

    public bool WasVisualAnalyzed { get; init; }

    public bool WasAdvancedAnalyzed { get; init; }

    public SearchEvidenceKind EvidenceKind { get; init; }

    public string MatchDisplay =>
        WasAdvancedAnalyzed
            ? $"정밀 AI {MatchPercent:0}%"
            : EvidenceKind switch
            {
                SearchEvidenceKind.ExactName => "정확 일치",
                SearchEvidenceKind.Application => "앱·바로가기",
                SearchEvidenceKind.Combined =>
                    "이름·내용 일치",
                SearchEvidenceKind.NameCandidate => "이름 일치",
                SearchEvidenceKind.Content => "본문 일치",
                SearchEvidenceKind.Path => "경로 단서",
                SearchEvidenceKind.Metadata => "메타데이터 일치",
                SearchEvidenceKind.VisualCandidate => $"시각 후보 {MatchPercent:0}%",
                SearchEvidenceKind.SemanticCandidate => $"AI 후보 {MatchPercent:0}%",
                _ => $"검색 일치 {MatchPercent:0}%"
            };

    public string RelevanceDisplay => EvidenceKind switch
    {
        SearchEvidenceKind.ExactName => "파일명 직접 일치",
        SearchEvidenceKind.Application => "Windows 앱 직접 일치",
        SearchEvidenceKind.Combined => "이름·내용 근거",
        SearchEvidenceKind.NameCandidate => "파일명 근거",
        SearchEvidenceKind.Path => "상위 경로 근거",
        SearchEvidenceKind.Content => "본문 근거",
        SearchEvidenceKind.Metadata => "메타데이터 근거",
        _ => MatchPercent switch
        {
            >= 90d => "연관성 매우 높음",
            >= 75d => "연관성 높음",
            >= 55d => "연관성 보통",
            _ => "연관성 낮음"
        }
    };

    public string SizeDisplay =>
        IsDirectory
            ? "—"
            : SizeBytes switch
            {
                null => "—",
                < 1024 => $"{SizeBytes.Value:N0} B",
                < 1024 * 1024 => $"{SizeBytes.Value / 1024d:N1} KB",
                < 1024L * 1024L * 1024L =>
                    $"{SizeBytes.Value / (1024d * 1024d):N1} MB",
                _ => $"{SizeBytes.Value / (1024d * 1024d * 1024d):N1} GB"
            };

    public bool IsDirectory { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}


public enum SearchEvidenceKind
{
    Metadata,
    ExactName,
    Application,
    Combined,
    NameCandidate,
    Path,
    Content,
    SemanticCandidate,
    VisualCandidate
}
