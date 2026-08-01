using System.Collections.Concurrent;
using System.Text;

namespace AIExplorer.Services;

public static class FileMetadataDescriptor
{
    public const int DescriptorVersion = 2;

    private static readonly Dictionary<string, string[]> ExtensionConcepts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".ppk"] =
            [
                "PuTTY SSH 개인키",
                "SSH private key",
                "서버 접속 키",
                "계정 인증 자격 증명"
            ],
            [".pem"] =
            [
                "PEM 인증서 또는 개인키",
                "certificate private key",
                "TLS SSH 서버 인증 자격 증명"
            ],
            [".key"] =
            [
                "개인키 또는 암호화 키",
                "private secret encryption key",
                "서버 인증 자격 증명"
            ],
            [".pub"] =
            [
                "SSH 공개키",
                "public key",
                "서버 접속 인증 키"
            ],
            [".crt"] = ["디지털 인증서", "TLS SSL certificate"],
            [".cer"] = ["디지털 인증서", "TLS SSL certificate"],
            [".der"] = ["DER 디지털 인증서", "binary certificate"],
            [".pfx"] =
            [
                "PKCS12 인증서와 개인키",
                "certificate private key bundle"
            ],
            [".p12"] =
            [
                "PKCS12 인증서와 개인키",
                "certificate private key bundle"
            ],
            [".csr"] = ["인증서 서명 요청", "certificate signing request"],
            [".jks"] = ["Java 키 저장소", "Java key store certificate"],
            [".keystore"] = ["키 저장소", "key store certificate"],
            [".kubeconfig"] =
            [
                "Kubernetes 접속 설정",
                "cluster account credential configuration"
            ],
            [".ovpn"] =
            [
                "OpenVPN 접속 설정",
                "VPN account credential configuration"
            ],
            [".rdp"] =
            [
                "원격 데스크톱 접속 설정",
                "remote desktop connection"
            ],
            [".env"] =
            [
                "환경 변수 설정",
                "application configuration secrets"
            ],
            [".ini"] = ["프로그램 설정", "configuration"],
            [".conf"] = ["프로그램 서버 설정", "configuration"],
            [".config"] = ["프로그램 설정", "configuration"],
            [".toml"] = ["프로그램 설정", "configuration"],
            [".properties"] = ["프로그램 설정", "configuration"]
        };
    private static readonly ConcurrentDictionary<
        string,
        IReadOnlyCollection<string>> SearchTermsCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetExtensionConcepts(
        string extension)
    {
        var normalized = NormalizeExtension(extension);
        return ExtensionConcepts.TryGetValue(normalized, out var concepts)
            ? concepts
            : [];
    }

    public static IReadOnlyCollection<string> GetSearchTerms(
        string extension)
    {
        var normalized = NormalizeExtension(extension);
        return SearchTermsCache.GetOrAdd(
            normalized,
            BuildSearchTerms);
    }

    private static IReadOnlyCollection<string> BuildSearchTerms(
        string normalized)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(normalized))
        {
            terms.Add(normalized.TrimStart('.'));
        }

        foreach (var concept in GetExtensionConcepts(normalized))
        {
            terms.UnionWith(SearchQueryInterpreter.TokenizeText(concept));
        }

        return terms.ToArray();
    }

    public static string BuildSemanticText(
        string root,
        IndexedFileRecord item)
    {
        var relativeDirectory = GetRelativeDirectory(
            root,
            item.DirectoryPath);
        var extension = NormalizeExtension(item.Extension);
        var extensionName = extension.TrimStart('.');
        var category = FileTypeCatalog.GetCategory(extension);
        var concepts = GetExtensionConcepts(extension);
        var identifierWords = SplitIdentifier(
            Path.GetFileNameWithoutExtension(item.Name));

        var builder = new StringBuilder(256);
        builder.Append("메타데이터 설명 버전 ")
            .Append(DescriptorVersion)
            .Append(". 파일명 ")
            .Append(Path.GetFileNameWithoutExtension(item.Name))
            .Append('.');

        if (!string.IsNullOrWhiteSpace(identifierWords))
        {
            builder.Append(" 파일명 단어 ")
                .Append(identifierWords)
                .Append('.');
        }

        builder.Append(" 폴더 ")
            .Append(relativeDirectory)
            .Append(". 종류 ")
            .Append(FileTypeCatalog.GetCategoryLabel(category))
            .Append('.');

        if (!string.IsNullOrEmpty(extensionName))
        {
            builder.Append(" 확장자 ")
                .Append(extensionName)
                .Append('.');
        }

        if (concepts.Count > 0)
        {
            builder.Append(" 형식 의미 ")
                .Append(string.Join(", ", concepts))
                .Append('.');
        }
        else if (category == FileCategory.Other)
        {
            builder.Append(" 사용자 정의 또는 특수 파일 형식.");
        }

        return builder.ToString();
    }

    public static string GetFormatDescription(string extension)
    {
        var normalized = NormalizeExtension(extension);
        var concepts = GetExtensionConcepts(normalized);
        if (concepts.Count > 0)
        {
            return string.Join(", ", concepts);
        }

        var category = FileTypeCatalog.GetCategory(normalized);
        var extensionName = normalized.TrimStart('.');
        return string.IsNullOrEmpty(extensionName)
            ? FileTypeCatalog.GetCategoryLabel(category)
            : $"{FileTypeCatalog.GetCategoryLabel(category)}, {extensionName}";
    }

    public static string GetRelativeDirectory(
        string root,
        string directoryPath)
    {
        try
        {
            return Path.GetRelativePath(root, directoryPath);
        }
        catch
        {
            return directoryPath;
        }
    }

    private static string SplitIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsLetterOrDigit(character))
            {
                AppendSpace(builder);
                continue;
            }

            if (index > 0 &&
                char.IsUpper(character) &&
                char.IsLower(value[index - 1]))
            {
                AppendSpace(builder);
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Trim();
    }

    private static void AppendSpace(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != ' ')
        {
            builder.Append(' ');
        }
    }

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
}
