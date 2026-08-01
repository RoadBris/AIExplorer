using System.Windows;
using AIExplorer.Models;

namespace AIExplorer.Dialogs;

public partial class SearchIndexChoiceDialog : Window
{
    public SearchIndexChoiceDialog(
        Window owner,
        SearchIndexReadiness readiness)
    {
        InitializeComponent();
        Owner = owner;
        ReadinessSummaryText.Text = readiness.Summary;
        ReadinessCountText.Text =
            $"현재 확인된 파일 {readiness.IndexedItems:N0}개 · " +
            $"본문 {readiness.ContentDocuments:N0}개" +
            (readiness.SemanticSearchRequested
                ? $" · 문서 AI {readiness.SemanticDocuments:N0}개"
                : string.Empty) +
            (readiness.VisualSearchRequested
                ? $" · 이미지 AI {readiness.VisualDocuments:N0}/" +
                  $"{readiness.VisualFiles:N0}개"
                : string.Empty);
    }

    public SearchIndexChoice Choice { get; private set; } =
        SearchIndexChoice.Cancel;

    private void IndexFirstButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Choice = SearchIndexChoice.IndexFirst;
        DialogResult = true;
    }

    private void SearchNowButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Choice = SearchIndexChoice.SearchNow;
        DialogResult = true;
    }
}

public enum SearchIndexChoice
{
    Cancel,
    IndexFirst,
    SearchNow
}
