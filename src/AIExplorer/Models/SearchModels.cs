using AIExplorer.Services;

namespace AIExplorer.Models;

public sealed record SearchRequest(
    string Query,
    IReadOnlyList<string> Roots,
    int MaximumResults = 500,
    int MaximumScannedItems = 100_000,
    int MaximumContentDocuments = 10_000,
    SearchIndexingMode IndexingMode = SearchIndexingMode.BuildMissing,
    bool AllowTargetedScan = true,
    bool IncludeAiCandidates = true,
    int MaximumTargetedScanItems = 50_000,
    SearchIntent? Intent = null);

public enum SearchIndexingMode
{
    BuildMissing,
    ExistingIndexOnly
}

public sealed record SearchIndexReadiness(
    bool RequiresIndexing,
    bool VisualSearchRequested,
    bool SemanticSearchRequested,
    int RootCount,
    int MissingMetadataRoots,
    int StaleMetadataRoots,
    int IncompleteContentRoots,
    int IncompleteSemanticRoots,
    int IncompleteVisualRoots,
    int IndexedItems,
    int ContentDocuments,
    int SemanticDocuments,
    int VisualDocuments,
    int VisualFiles,
    string Summary)
{
    public static SearchIndexReadiness Ready(int rootCount) =>
        new(
            RequiresIndexing: false,
            VisualSearchRequested: false,
            SemanticSearchRequested: false,
            RootCount: rootCount,
            MissingMetadataRoots: 0,
            StaleMetadataRoots: 0,
            IncompleteContentRoots: 0,
            IncompleteSemanticRoots: 0,
            IncompleteVisualRoots: 0,
            IndexedItems: 0,
            ContentDocuments: 0,
            SemanticDocuments: 0,
            VisualDocuments: 0,
            VisualFiles: 0,
            Summary: "검색 색인이 준비되어 있습니다.");
}

public sealed record SearchProgress(
    int ScannedItems,
    int MatchedItems,
    string CurrentPath,
    SearchPhase Phase,
    IReadOnlyList<SearchResult>? PartialResults = null,
    double? PercentComplete = null);

public sealed record SearchResponse(
    IReadOnlyList<SearchResult> Results,
    SearchDiagnostics Diagnostics);

public sealed record IndexWarmupResult(
    int Roots,
    int IndexedItems,
    int ContentDocuments,
    int SemanticDocuments,
    int NewlyIndexedSemanticDocuments,
    int VisualDocuments,
    int NewlyIndexedVisualDocuments);

public sealed record AdvancedAnalysisResult(
    IReadOnlyList<SearchResult> Results,
    int AnalyzedResults,
    int EmbeddingDimensions);

public sealed record SearchDiagnostics(
    int IndexedItems,
    int TargetedScannedItems,
    int ContentIndexedDocuments,
    int OcrIndexedDocuments,
    int SemanticIndexedDocuments,
    int SemanticTotalDocuments,
    int VisualIndexedDocuments,
    int VisualTotalDocuments,
    bool UsedCachedIndex,
    bool UsedTargetedScan,
    bool UsedContentSearch,
    bool UsedSemanticSearch,
    bool UsedVisualSearch,
    bool AiModelReady,
    bool VisualModelReady,
    bool IndexWasTruncated,
    bool UsedExistingIndexOnly,
    string IntentSummary);

public enum SearchPhase
{
    Indexing,
    Searching,
    TargetedScanning,
    ContentIndexing,
    OcrIndexing,
    ContentSearching,
    SemanticIndexing,
    SemanticSearching,
    VisualIndexing,
    VisualSearching,
    AdvancedAnalyzing
}
