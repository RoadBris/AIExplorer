using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ExcelDataReader;

namespace AIExplorer.Services;

public sealed partial class DocumentTextExtractor
{
    private const int MaximumPlainTextBytes = 256 * 1024;
    private const int MaximumArchiveBytes = 32 * 1024 * 1024;
    private const int MaximumExtractedCharacters = 12_000;
    private const int MaximumSpreadsheetBytes = 64 * 1024 * 1024;
    private const int MaximumSpreadsheetCharacters = 256_000;
    private const int MaximumSpreadsheetCells = 500_000;

    private static readonly HashSet<string> PlainTextExtensions = new(
        [
            ".txt", ".md", ".markdown", ".csv", ".tsv", ".log",
            ".json", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".conf",
            ".config", ".url", ".ps1", ".bat", ".cmd", ".cs", ".xaml",
            ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".cpp", ".c",
            ".h", ".hpp", ".go", ".rs", ".php", ".html", ".htm", ".css",
            ".scss", ".sql"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ArchiveDocumentExtensions = new(
        [".docx", ".pptx", ".hwpx", ".odt", ".ods", ".odp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SpreadsheetExtensions = new(
        [".xls", ".xlsx", ".xlsm", ".xlsb", ".xltx", ".xltm"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IOcrTextExtractor? _ocrTextExtractor;

    static DocumentTextExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DocumentTextExtractor(IOcrTextExtractor? ocrTextExtractor = null)
    {
        _ocrTextExtractor = ocrTextExtractor ??
            (OperatingSystem.IsWindows()
                ? new WindowsOcrService()
                : null);
    }

    public bool CanExtract(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return PlainTextExtensions.Contains(normalized) ||
               SpreadsheetExtensions.Contains(normalized) ||
               ArchiveDocumentExtensions.Contains(normalized) ||
               _ocrTextExtractor is { IsAvailable: true } &&
               _ocrTextExtractor.CanExtract(normalized);
    }

    public bool UsesOcr(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return !PlainTextExtensions.Contains(normalized) &&
               !SpreadsheetExtensions.Contains(normalized) &&
               !ArchiveDocumentExtensions.Contains(normalized) &&
               _ocrTextExtractor is { IsAvailable: true } &&
               _ocrTextExtractor.CanExtract(normalized);
    }

    public async Task<ExtractedDocument?> ExtractAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var extension = NormalizeExtension(Path.GetExtension(path));
        if (!CanExtract(extension))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0)
            {
                return null;
            }

            string text;
            var source = DocumentContentSource.PlainText;
            var analyzedPages = 0;
            var extractorWasTruncated = false;
            if (PlainTextExtensions.Contains(extension))
            {
                text = await ExtractPlainTextAsync(path, cancellationToken);
            }
            else if (SpreadsheetExtensions.Contains(extension))
            {
                source = DocumentContentSource.Spreadsheet;
                if (file.Length > MaximumSpreadsheetBytes)
                {
                    return null;
                }

                text = await Task.Run(
                    () => ExtractSpreadsheet(path, cancellationToken),
                    cancellationToken);
            }
            else if (ArchiveDocumentExtensions.Contains(extension))
            {
                source = DocumentContentSource.ArchiveDocument;
                if (file.Length > MaximumArchiveBytes)
                {
                    return null;
                }

                text = await Task.Run(
                    () => ExtractArchiveDocument(path, extension, cancellationToken),
                    cancellationToken);
            }
            else if (_ocrTextExtractor is { IsAvailable: true })
            {
                var ocr = await _ocrTextExtractor.ExtractAsync(
                    path,
                    cancellationToken);
                if (ocr is null)
                {
                    return null;
                }

                text = ocr.Text;
                source = ocr.Source;
                analyzedPages = ocr.PagesAnalyzed;
                extractorWasTruncated = ocr.WasTruncated;
            }
            else
            {
                return null;
            }

            var normalized = NormalizeWhitespace(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var maximumCharacters =
                SpreadsheetExtensions.Contains(extension)
                    ? MaximumSpreadsheetCharacters
                    : MaximumExtractedCharacters;
            var wasTruncated =
                extractorWasTruncated ||
                normalized.Length > maximumCharacters;
            if (wasTruncated)
            {
                normalized = normalized[..maximumCharacters];
            }

            return new ExtractedDocument(
                normalized,
                wasTruncated,
                source,
                analyzedPages);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            XmlException or
            DecoderFallbackException)
        {
            return null;
        }
    }

    private static async Task<string> ExtractPlainTextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            32_768,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = (int)Math.Min(stream.Length, MaximumPlainTextBytes);
        var bytes = new byte[length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(
                bytes.AsMemory(offset, bytes.Length - offset),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset == 0)
        {
            return string.Empty;
        }

        var content = bytes.AsSpan(0, offset);
        if (!HasKnownTextBom(content) && LooksBinary(content))
        {
            return string.Empty;
        }

        return DecodeText(content);
    }

    private static string ExtractArchiveDocument(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var builder = new StringBuilder(
            Math.Min(MaximumExtractedCharacters, 8_192));

        foreach (var entry in archive.Entries
                     .Where(entry => ShouldReadArchiveEntry(extension, entry.FullName))
                     .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (builder.Length >= MaximumExtractedCharacters)
            {
                break;
            }

            using var entryStream = entry.Open();
            using var reader = XmlReader.Create(
                entryStream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    MaxCharactersInDocument = 8_000_000
                });
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA))
                {
                    continue;
                }

                var value = reader.Value.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                var remaining = MaximumExtractedCharacters - builder.Length;
                builder.Append(value.AsSpan(0, Math.Min(value.Length, remaining)));
                if (builder.Length >= MaximumExtractedCharacters)
                {
                    break;
                }
            }
        }

        return builder.ToString();
    }

    private static string ExtractSpreadsheet(
        string path,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            65_536,
            FileOptions.SequentialScan);
        using var reader = ExcelReaderFactory.CreateReader(
            stream,
            new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(949),
                LeaveOpen = false
            });
        var builder = new StringBuilder(
            Math.Min(MaximumSpreadsheetCharacters, 32_768));
        var uniqueValues = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var scannedCells = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendSpreadsheetValue(
                builder,
                uniqueValues,
                $"시트 {reader.Name}");

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    if (scannedCells >= MaximumSpreadsheetCells ||
                        builder.Length >= MaximumSpreadsheetCharacters)
                    {
                        return builder.ToString();
                    }

                    scannedCells++;
                    var value = FormatSpreadsheetValue(
                        reader.GetValue(column));
                    AppendSpreadsheetValue(
                        builder,
                        uniqueValues,
                        value);
                }
            }
        }
        while (reader.NextResult());

        return builder.ToString();
    }

    private static string FormatSpreadsheetValue(object? value) =>
        value switch
        {
            null => string.Empty,
            string text => text,
            DateTime dateTime =>
                dateTime.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset =>
                dateTimeOffset.ToString(
                    "yyyy-MM-dd HH:mm:ss zzz",
                    CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "TRUE" : "FALSE",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ??
                string.Empty,
            _ => value.ToString() ?? string.Empty
        };

    private static void AppendSpreadsheetValue(
        StringBuilder builder,
        ISet<string> uniqueValues,
        string value)
    {
        var normalized = NormalizeWhitespace(value);
        if (normalized.Length == 0 ||
            !uniqueValues.Add(normalized) ||
            builder.Length >= MaximumSpreadsheetCharacters)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        var remaining = MaximumSpreadsheetCharacters - builder.Length;
        builder.Append(
            normalized.AsSpan(
                0,
                Math.Min(normalized.Length, remaining)));
    }

    private static bool ShouldReadArchiveEntry(
        string extension,
        string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        return extension switch
        {
            ".docx" =>
                normalized.Equals(
                    "word/document.xml",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    "word/header",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    "word/footer",
                    StringComparison.OrdinalIgnoreCase),
            ".pptx" =>
                normalized.StartsWith(
                    "ppt/slides/slide",
                    StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase),
            ".xlsx" =>
                normalized.Equals(
                    "xl/sharedStrings.xml",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    "xl/worksheets/sheet",
                    StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase),
            ".hwpx" =>
                normalized.StartsWith(
                    "Contents/section",
                    StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase),
            ".odt" or ".ods" or ".odp" =>
                normalized.Equals("content.xml", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool LooksBinary(ReadOnlySpan<byte> bytes)
    {
        var controls = 0;
        var sampleLength = Math.Min(bytes.Length, 4_096);
        for (var index = 0; index < sampleLength; index++)
        {
            var value = bytes[index];
            if (value == 0)
            {
                return true;
            }

            if (value < 8 || value is > 13 and < 32)
            {
                controls++;
            }
        }

        return sampleLength > 0 && controls > sampleLength / 20;
    }

    private static bool HasKnownTextBom(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 &&
        bytes[0] == 0xEF &&
        bytes[1] == 0xBB &&
        bytes[2] == 0xBF ||
        bytes.Length >= 2 &&
        ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
         (bytes[0] == 0xFE && bytes[1] == 0xFF));

    private static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes[2..]);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFE &&
            bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }

        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(
                    949,
                    EncoderFallback.ReplacementFallback,
                    DecoderFallback.ReplacementFallback)
                .GetString(bytes);
        }
    }

    private static string NormalizeWhitespace(string text) =>
        WhitespaceRegex().Replace(text, " ").Trim();

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

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed record ExtractedDocument(
    string Text,
    bool WasTruncated,
    DocumentContentSource Source = DocumentContentSource.PlainText,
    int AnalyzedPages = 0);

public enum DocumentContentSource
{
    PlainText,
    ArchiveDocument,
    Spreadsheet,
    ImageOcr,
    PdfOcr
}
