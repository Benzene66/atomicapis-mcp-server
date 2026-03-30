using System.ComponentModel;
using CsvSurgeon;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class CsvSurgeonTools
{
    [McpServerTool, Description(
        "Cleans, deduplicates, and normalizes messy CSV/TSV data. " +
        "Auto-detects delimiters (comma, tab, semicolon, pipe). " +
        "Normalizes headers to snake_case, standardizes dates to ISO 8601, " +
        "trims whitespace, removes empty/duplicate rows, and pads/truncates uneven rows.")]
    public static string CleanCsv(
        [Description("The raw CSV/TSV content to clean")] string csv,
        [Description("Remove duplicate rows (default: true)")] bool deduplicate = true,
        [Description("Normalize dates to ISO 8601 (default: true)")] bool normalizeDates = true,
        [Description("Normalize headers to snake_case (default: true)")] bool normalizeHeaders = true,
        [Description("Collapse multiple spaces to single space (default: true)")] bool collapseWhitespace = true,
        [Description("Convert output to this delimiter character (e.g. ',' or '\\t')")] string? outputDelimiter = null)
    {
        var options = new CleanOptions
        {
            Deduplicate = deduplicate,
            NormalizeDates = normalizeDates,
            NormalizeHeaders = normalizeHeaders,
            CollapseWhitespace = collapseWhitespace,
            OutputDelimiter = outputDelimiter?.Length > 0 ? outputDelimiter[0] : null
        };

        var result = CsvCleaner.Clean(csv, options);

        return $"""
            Cleaned CSV:
            {result.Csv}

            Stats: {result.Stats.InputRows} input rows → {result.Stats.OutputRows} output rows, {result.Stats.DuplicatesRemoved} duplicates removed, {result.Stats.EmptyRowsRemoved} empty rows removed, {result.Stats.RowsPadded} rows padded, {result.Stats.RowsTruncated} rows truncated
            """;
    }
}
