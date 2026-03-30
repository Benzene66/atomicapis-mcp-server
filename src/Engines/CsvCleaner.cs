using System.Text;
using System.Text.RegularExpressions;

namespace CsvSurgeon;

public static partial class CsvCleaner
{
    private const int MaxPayloadSize = 5 * 1024 * 1024; // 5MB

    public static CleanResult Clean(string input, CleanOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("CSV content cannot be empty.");

        if (input.Length > MaxPayloadSize)
            throw new ArgumentException("CSV content exceeds the 5MB size limit.");

        options ??= new CleanOptions();

        // Normalize line endings
        input = input.Replace("\r\n", "\n").Replace('\r', '\n');

        var delimiter = options.Delimiter ?? DetectDelimiter(input);
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            throw new ArgumentException("CSV content has no data rows.");

        var headerRow = ParseLine(lines[0], delimiter);
        var headerCount = headerRow.Length;

        // Clean headers
        var cleanedHeaders = CleanHeaders(headerRow, options);

        // Process data rows
        var rows = new List<string[]>();
        var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stats = new CleanStats();

        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseLine(lines[i], delimiter);

            // Skip completely empty rows
            if (fields.All(f => string.IsNullOrWhiteSpace(f)))
            {
                stats.EmptyRowsRemoved++;
                continue;
            }

            // Pad or truncate to match header count
            if (fields.Length < headerCount)
            {
                var padded = new string[headerCount];
                Array.Copy(fields, padded, fields.Length);
                for (var j = fields.Length; j < headerCount; j++)
                    padded[j] = "";
                fields = padded;
                stats.RowsPadded++;
            }
            else if (fields.Length > headerCount)
            {
                fields = fields[..headerCount];
                stats.RowsTruncated++;
            }

            // Clean each field
            for (var j = 0; j < fields.Length; j++)
            {
                var original = fields[j];
                fields[j] = CleanField(fields[j], options);
                if (fields[j] != original)
                    stats.FieldsCleaned++;
            }

            // Deduplication
            if (options.Deduplicate)
            {
                var key = string.Join("\x01", fields);
                if (!duplicateKeys.Add(key))
                {
                    stats.DuplicatesRemoved++;
                    continue;
                }
            }

            rows.Add(fields);
        }

        stats.InputRows = lines.Length - 1;
        stats.OutputRows = rows.Count;

        // Build output
        var outputDelimiter = options.OutputDelimiter ?? delimiter;
        var sb = new StringBuilder();
        sb.AppendLine(FormatRow(cleanedHeaders, outputDelimiter));
        foreach (var row in rows)
        {
            sb.AppendLine(FormatRow(row, outputDelimiter));
        }

        return new CleanResult(sb.ToString().TrimEnd('\n', '\r'), stats);
    }

    private static string[] CleanHeaders(string[] headers, CleanOptions options)
    {
        var result = new string[headers.Length];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim().Trim('"');

            if (options.NormalizeHeaders)
            {
                // Remove non-alphanumeric (keep underscores), lowercase, collapse whitespace
                h = WhitespaceRegex().Replace(h, "_");
                h = NonAlphanumericRegex().Replace(h, "");
                h = h.ToLowerInvariant().Trim('_');
                h = MultipleUnderscoreRegex().Replace(h, "_");
            }

            if (string.IsNullOrWhiteSpace(h))
                h = $"column_{i + 1}";

            // Handle duplicate headers
            var original = h;
            var suffix = 2;
            while (!seen.Add(h))
            {
                h = $"{original}_{suffix++}";
            }

            result[i] = h;
        }

        return result;
    }

    private static string CleanField(string field, CleanOptions options)
    {
        // Trim whitespace
        field = field.Trim();

        // Remove wrapping quotes if present
        if (field.Length >= 2 && field[0] == '"' && field[^1] == '"')
        {
            field = field[1..^1].Replace("\"\"", "\"");
        }

        // Normalize dates if enabled
        if (options.NormalizeDates)
        {
            field = TryNormalizeDate(field);
        }

        // Trim internal excessive whitespace
        if (options.CollapseWhitespace)
        {
            field = WhitespaceRegex().Replace(field, " ");
        }

        return field;
    }

    private static string TryNormalizeDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        // Try common date formats and normalize to ISO 8601 (yyyy-MM-dd)
        string[] formats =
        [
            "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy",
            "d-M-yyyy", "dd-MM-yyyy", "d-M-yy", "dd-MM-yy",
            "d.M.yyyy", "dd.MM.yyyy",
            "yyyy/MM/dd", "yyyy.MM.dd",
            "MMM d, yyyy", "MMMM d, yyyy",
            "d MMM yyyy", "d MMMM yyyy",
            "yyyy-MM-dd" // already ISO — will pass through
        ];

        if (DateTimeOffset.TryParseExact(value, formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd");
        }

        return value;
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        var best = ',';
        var bestCount = 0;
        foreach (var d in new[] { ',', '\t', ';', '|' })
        {
            var count = CountDelimiter(firstLine, d);
            if (count > bestCount)
            {
                bestCount = count;
                best = d;
            }
        }

        return best;
    }

    private static int CountDelimiter(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (ch == delimiter && !inQuotes) count++;
        }
        return count;
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                    current.Append(ch);
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if (ch != '\r')
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static string FormatRow(string[] fields, char delimiter)
    {
        var parts = new string[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            // Quote if field contains delimiter, quotes, or newlines
            if (f.Contains(delimiter) || f.Contains('"') || f.Contains('\n'))
            {
                parts[i] = $"\"{f.Replace("\"", "\"\"")}\"";
            }
            else
            {
                parts[i] = f;
            }
        }
        return string.Join(delimiter, parts);
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9_]", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"_+", RegexOptions.Compiled)]
    private static partial Regex MultipleUnderscoreRegex();
}

public record CleanOptions
{
    public char? Delimiter { get; init; }
    public char? OutputDelimiter { get; init; }
    public bool Deduplicate { get; init; } = true;
    public bool NormalizeDates { get; init; } = true;
    public bool NormalizeHeaders { get; init; } = true;
    public bool CollapseWhitespace { get; init; } = true;
}

public record CleanResult(string Csv, CleanStats Stats);

public record CleanStats
{
    public int InputRows { get; set; }
    public int OutputRows { get; set; }
    public int DuplicatesRemoved { get; set; }
    public int EmptyRowsRemoved { get; set; }
    public int FieldsCleaned { get; set; }
    public int RowsPadded { get; set; }
    public int RowsTruncated { get; set; }
}
