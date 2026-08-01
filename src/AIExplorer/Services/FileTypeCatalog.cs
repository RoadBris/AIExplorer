namespace AIExplorer.Services;

public static class FileTypeCatalog
{
    private static readonly Dictionary<string, FileCategory> ExtensionCategories =
        BuildExtensionCategories();

    private static readonly Dictionary<FileCategory, string[]> CategoryAliases = new()
    {
        [FileCategory.ThreeDimensionalModel] =
        [
            "3d", "3차원", "모델링", "메시", "mesh", "블렌더", "blender",
            "마야", "maya", "스컬프팅", "sculpt", "렌더링", "rendering"
        ],
        [FileCategory.CadDrawing] =
        [
            "cad", "캐드", "도면", "설계도", "기계설계", "건축설계"
        ],
        [FileCategory.Image] =
        [
            "사진", "이미지", "그림", "스크린샷", "캡처", "photo", "image", "picture"
        ],
        [FileCategory.Video] =
        [
            "영상", "동영상", "비디오", "녹화", "movie", "video"
        ],
        [FileCategory.Audio] =
        [
            "음악", "음원", "오디오", "녹음", "소리", "music", "audio", "sound"
        ],
        [FileCategory.Document] =
        [
            "문서", "보고서", "텍스트", "글", "document", "report"
        ],
        [FileCategory.Spreadsheet] =
        [
            "엑셀", "스프레드시트", "표", "excel", "spreadsheet"
        ],
        [FileCategory.Presentation] =
        [
            "파워포인트", "프레젠테이션", "발표자료", "ppt", "powerpoint", "presentation"
        ],
        [FileCategory.Archive] =
        [
            "압축", "압축파일", "아카이브", "zip", "archive"
        ],
        [FileCategory.SourceCode] =
        [
            "소스", "소스코드", "소스 코드", "프로그래밍", "개발코드",
            "스크립트", "source code", "sourcecode", "script"
        ],
        [FileCategory.Executable] =
        [
            "프로그램", "실행파일", "설치파일", "앱", "application", "executable", "installer"
        ],
        [FileCategory.Font] =
        [
            "폰트", "글꼴", "font"
        ],
        [FileCategory.Database] =
        [
            "데이터베이스", "디비", "database"
        ],
        [FileCategory.DiskImage] =
        [
            "디스크이미지", "디스크 이미지", "가상이미지", "iso"
        ]
    };

    public static FileCategory GetCategory(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return ExtensionCategories.TryGetValue(normalized, out var category)
            ? category
            : FileCategory.Other;
    }

    public static IReadOnlyCollection<string> GetKnownExtensions() =>
        ExtensionCategories.Keys;

    public static IReadOnlyList<string> GetAliases(FileCategory category) =>
        CategoryAliases.TryGetValue(category, out var aliases)
            ? aliases
            : [];

    public static IReadOnlyCollection<FileCategory> DetectCategories(
        string query,
        IReadOnlyCollection<string> tokens)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var results = new HashSet<FileCategory>();

        foreach (var pair in CategoryAliases)
        {
            if (pair.Value.Any(alias =>
                    alias.Contains(' ')
                        ? normalizedQuery.Contains(alias, StringComparison.OrdinalIgnoreCase)
                        : tokens.Contains(alias, StringComparer.OrdinalIgnoreCase)))
            {
                results.Add(pair.Key);
            }
        }

        // In natural-language searches, "문서" usually means the whole
        // office-document family rather than only text/PDF extensions.
        // Keep an explicit "엑셀 문서" or "프레젠테이션 문서" request
        // narrow, but broaden a generic document-only request.
        if (results.Count == 1 &&
            results.Contains(FileCategory.Document))
        {
            results.Add(FileCategory.Spreadsheet);
            results.Add(FileCategory.Presentation);
        }

        if (results.Contains(FileCategory.ThreeDimensionalModel))
        {
            results.Add(FileCategory.CadDrawing);
        }

        return results;
    }

    public static bool TryResolveExtensionToken(string token, out string extension)
    {
        var normalized = NormalizeExtension(token);
        if (ExtensionCategories.ContainsKey(normalized))
        {
            extension = normalized;
            return true;
        }

        extension = string.Empty;
        return false;
    }

    public static string GetCategoryLabel(FileCategory category) =>
        category switch
        {
            FileCategory.ThreeDimensionalModel => "3D 모델",
            FileCategory.CadDrawing => "CAD 도면",
            FileCategory.Image => "이미지",
            FileCategory.Video => "동영상",
            FileCategory.Audio => "오디오",
            FileCategory.Document => "문서",
            FileCategory.Spreadsheet => "스프레드시트",
            FileCategory.Presentation => "프레젠테이션",
            FileCategory.Archive => "압축 파일",
            FileCategory.SourceCode => "소스 코드",
            FileCategory.Executable => "실행 파일",
            FileCategory.Font => "글꼴",
            FileCategory.Database => "데이터베이스",
            FileCategory.DiskImage => "디스크 이미지",
            _ => "일반 파일"
        };

    public static string GetTypeDisplay(string extension)
    {
        var normalized = NormalizeExtension(extension);
        var category = GetCategory(normalized);
        return category switch
        {
            FileCategory.ThreeDimensionalModel =>
                $"{normalized.TrimStart('.').ToUpperInvariant()} 3D 모델",
            FileCategory.CadDrawing =>
                $"{normalized.TrimStart('.').ToUpperInvariant()} CAD 도면",
            FileCategory.Image => "이미지 파일",
            FileCategory.Video => "비디오 파일",
            FileCategory.Audio => "오디오 파일",
            FileCategory.Spreadsheet when normalized == ".xlsx" => "Excel 통합 문서",
            FileCategory.Spreadsheet => "스프레드시트",
            FileCategory.Presentation when normalized == ".pptx" => "PowerPoint 프레젠테이션",
            FileCategory.Presentation => "프레젠테이션",
            FileCategory.Archive => "압축 파일",
            FileCategory.SourceCode => "소스 코드",
            FileCategory.Executable => "응용 프로그램",
            FileCategory.Font => "글꼴 파일",
            FileCategory.Database => "데이터베이스 파일",
            FileCategory.DiskImage => "디스크 이미지",
            FileCategory.Document => GetDocumentTypeDisplay(normalized),
            _ when string.IsNullOrEmpty(normalized) => "파일",
            _ => $"{normalized.TrimStart('.').ToUpperInvariant()} 파일"
        };
    }

    private static string GetDocumentTypeDisplay(string extension) =>
        extension switch
        {
            ".pdf" => "PDF 문서",
            ".doc" or ".docx" => "Word 문서",
            ".hwp" => "한글 문서",
            ".hwpx" => "한글 표준 문서",
            ".txt" => "텍스트 문서",
            ".md" => "Markdown 문서",
            ".log" => "로그 파일",
            _ => "문서"
        };

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.')
            ? normalized
            : $".{normalized}";
    }

    private static Dictionary<string, FileCategory> BuildExtensionCategories()
    {
        var results = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase);

        Add(
            results,
            FileCategory.ThreeDimensionalModel,
            ".blend", ".fbx", ".obj", ".stl", ".3mf", ".gltf", ".glb", ".dae",
            ".3ds", ".max", ".ma", ".mb", ".c4d", ".lwo", ".ply", ".x3d",
            ".usd", ".usda", ".usdc", ".usdz", ".abc", ".sldprt", ".sldasm");
        Add(
            results,
            FileCategory.CadDrawing,
            ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs", ".ifc", ".skp",
            ".ipt", ".iam", ".catpart", ".catproduct");
        Add(
            results,
            FileCategory.Image,
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff",
            ".heic", ".svg", ".psd", ".ai", ".xcf", ".raw", ".cr2", ".nef");
        Add(
            results,
            FileCategory.Video,
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mts");
        Add(
            results,
            FileCategory.Audio,
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus");
        Add(
            results,
            FileCategory.Document,
            ".pdf", ".doc", ".docx", ".hwp", ".hwpx", ".txt", ".md", ".rtf",
            ".odt", ".log", ".epub");
        Add(
            results,
            FileCategory.Spreadsheet,
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".xltx", ".xltm",
            ".csv", ".ods");
        Add(
            results,
            FileCategory.Presentation,
            ".ppt", ".pptx", ".odp");
        Add(
            results,
            FileCategory.Archive,
            ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz");
        Add(
            results,
            FileCategory.SourceCode,
            ".cs", ".xaml", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".cpp",
            ".c", ".h", ".hpp", ".go", ".rs", ".php", ".html", ".css", ".scss",
            ".json", ".xml", ".yaml", ".yml", ".ps1", ".bat", ".cmd", ".sh", ".sql");
        Add(
            results,
            FileCategory.Executable,
            ".exe", ".msi", ".msix", ".appx", ".com", ".scr");
        Add(results, FileCategory.Font, ".ttf", ".otf", ".woff", ".woff2");
        Add(
            results,
            FileCategory.Database,
            ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb");
        Add(
            results,
            FileCategory.DiskImage,
            ".iso", ".img", ".vhd", ".vhdx", ".vmdk");

        return results;
    }

    private static void Add(
        IDictionary<string, FileCategory> target,
        FileCategory category,
        params string[] extensions)
    {
        foreach (var extension in extensions)
        {
            target[extension] = category;
        }
    }
}

public enum FileCategory
{
    Other,
    ThreeDimensionalModel,
    CadDrawing,
    Image,
    Video,
    Audio,
    Document,
    Spreadsheet,
    Presentation,
    Archive,
    SourceCode,
    Executable,
    Font,
    Database,
    DiskImage
}
