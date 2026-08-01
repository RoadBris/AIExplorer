using System;
using System.Collections.Generic;

namespace AIExplorer.Models;

public sealed record TitleSearchHit(
    string Name,
    string FullPath,
    bool IsDirectory,
    DateTime? ModifiedLocal,
    double Score,
    double MatchPercent,
    bool IsExactMatch,
    string Reason,
    DateTime? CreatedLocal = null);

public sealed record TitleSearchProgress(
    int ScannedItems,
    int MatchedItems,
    string CurrentPath,
    IReadOnlyList<TitleSearchHit> NewHits,
    bool IsCompleted = false);

public sealed record TitleSearchSummary(
    int ScannedItems,
    int MatchedItems,
    int SkippedRoots);

public enum InstantTitleItemFilter
{
    All,
    Files,
    Folders
}

public enum InstantTitleSortField
{
    Name,
    Path,
    Size,
    Modified
}

public sealed record InstantTitleSearchOptions(
    bool MatchCase,
    bool MatchWholeWord,
    bool UseRegularExpression,
    InstantTitleItemFilter ItemFilter,
    InstantTitleSortField SortField,
    bool SortAscending);

public sealed record InstantTitleSearchItem(
    string Name,
    string FullPath,
    string DirectoryPath,
    string Extension,
    bool IsDirectory,
    long? SizeBytes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc);

public sealed record InstantTitleSearchResponse(
    IReadOnlyList<InstantTitleSearchItem> Results,
    int TotalMatches,
    int IndexedItems,
    int IndexedRoots,
    int MissingRoots,
    TimeSpan Elapsed,
    string? ValidationError = null);

public sealed record InstantTitleIndexProgress(
    int CompletedRoots,
    int TotalRoots,
    int IndexedItems,
    string CurrentRoot,
    string CurrentPath,
    double PercentComplete,
    bool IsCompleted = false);
