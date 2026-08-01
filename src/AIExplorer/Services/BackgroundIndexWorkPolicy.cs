namespace AIExplorer.Services;

public readonly record struct BackgroundIndexWorkBudget(
    bool AllowHeavyAiIndexing,
    int MaximumContentDocumentsPerRoot,
    int MaximumNewSemanticDocumentsPerRoot,
    int MaximumNewVisualDocumentsPerRoot,
    string ModeLabel);

public static class BackgroundIndexWorkPolicy
{
    public static BackgroundIndexWorkBudget GetBudget(
        bool isHiddenToTray,
        TimeSpan scheduledDelay)
    {
        var allowHeavyAiIndexing =
            isHiddenToTray ||
            scheduledDelay >= TimeSpan.FromMinutes(5);
        return allowHeavyAiIndexing
            ? new BackgroundIndexWorkBudget(
                AllowHeavyAiIndexing: true,
                MaximumContentDocumentsPerRoot: 1_200,
                MaximumNewSemanticDocumentsPerRoot: 48,
                MaximumNewVisualDocumentsPerRoot: 12,
                ModeLabel: "idle-ai")
            : new BackgroundIndexWorkBudget(
                AllowHeavyAiIndexing: false,
                MaximumContentDocumentsPerRoot: 0,
                MaximumNewSemanticDocumentsPerRoot: 0,
                MaximumNewVisualDocumentsPerRoot: 0,
                ModeLabel: "title-only");
    }

    public static TimeSpan GetNextDelay(
        bool isHiddenToTray,
        bool newAiDocumentsWereIndexed) =>
        isHiddenToTray
            ? newAiDocumentsWereIndexed
                ? TimeSpan.FromSeconds(20)
                : TimeSpan.FromMinutes(5)
            : TimeSpan.FromMinutes(15);
}
