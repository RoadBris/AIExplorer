using System.Text.RegularExpressions;

namespace AIExplorer.Services;

public enum SearchIntentMode
{
    Hybrid,
    ExactName,
    TopicRelated,
    ContentContains,
    Application,
    Visual
}

public sealed record SearchIntentClassification(
    SearchIntentMode Mode,
    bool SearchApplicationCatalog,
    string DisplayLabel);

public static class SearchIntentClassifier
{
    public static SearchIntentClassification Classify(
        SearchIntent intent)
    {
        var query = intent.OriginalQuery;
        if (intent.Categories.Contains(FileCategory.Image) &&
            Regex.IsMatch(
                query,
                @"이미지|사진|그림|스크린샷|image|photo|picture",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
        {
            return new SearchIntentClassification(
                SearchIntentMode.Visual,
                SearchApplicationCatalog: false,
                "이미지 내용 검색");
        }

        if (Regex.IsMatch(
                query,
                @"설치|프로그램|응용\s*프로그램|앱|실행|바로가기|아이콘|launcher|application|shortcut",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
        {
            return new SearchIntentClassification(
                SearchIntentMode.Application,
                SearchApplicationCatalog: true,
                "설치 앱·바로가기 검색");
        }

        if (Regex.IsMatch(
                query,
                @"파일명|폴더명|제목|이름\s*(?:이|가|은|는)?\s*(?:정확히|같|일치|포함)|이라는\s*이름|따옴표",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant) ||
            HasQuotedLiteral(query))
        {
            return new SearchIntentClassification(
                SearchIntentMode.ExactName,
                SearchApplicationCatalog: true,
                "정확한 이름 검색");
        }

        if (Regex.IsMatch(
                query,
                @"본문|내용\s*(?:에|에서)|담긴|담겨|들어간|들어\s*있는|포함된|포함하는",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
        {
            return new SearchIntentClassification(
                SearchIntentMode.ContentContains,
                SearchApplicationCatalog: false,
                "파일 내용 검색");
        }

        if (Regex.IsMatch(
                query,
                @"관련|관한|대한|연관|자료|정보|문서",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
        {
            return new SearchIntentClassification(
                SearchIntentMode.TopicRelated,
                SearchApplicationCatalog: true,
                "관련 자료 통합 검색");
        }

        var compactLookup =
            intent.Terms.Count + intent.LiteralTerms.Count <= 3 &&
            intent.RequestedExtensions.Count == 0 &&
            intent.ModifiedFromUtc is null &&
            intent.ModifiedToUtc is null;
        return new SearchIntentClassification(
            SearchIntentMode.Hybrid,
            SearchApplicationCatalog: compactLookup,
            compactLookup
                ? "이름·내용·앱 통합 검색"
                : "이름·내용 통합 검색");
    }

    private static bool HasQuotedLiteral(string query)
    {
        var firstDouble = query.IndexOf('"');
        if (firstDouble >= 0 &&
            query.IndexOf('"', firstDouble + 1) > firstDouble + 1)
        {
            return true;
        }

        var firstSingle = query.IndexOf('\'');
        return firstSingle >= 0 &&
               query.IndexOf('\'', firstSingle + 1) > firstSingle + 1;
    }
}
