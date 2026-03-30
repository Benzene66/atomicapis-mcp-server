namespace SchemaSniff;

public static class FormatDetector
{
    public static string Detect(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "unknown";

        var trimmed = payload.TrimStart();

        // JSON starts with { or [
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return "json";

        // XML starts with < (could be <?xml or <root>)
        if (trimmed.StartsWith('<'))
            return "xml";

        // CSV: check if it looks like delimited tabular data
        // Heuristic: multiple lines, consistent delimiter count per line
        if (LooksCsv(trimmed))
            return "csv";

        return "unknown";
    }

    private static bool LooksCsv(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return false;

        // Check common delimiters: comma, tab, semicolon, pipe
        foreach (var delimiter in new[] { ',', '\t', ';', '|' })
        {
            var headerCount = CountDelimiter(lines[0], delimiter);
            if (headerCount == 0)
                continue;

            // Check if at least the first few data rows have a similar delimiter count
            var consistent = true;
            var checkLines = Math.Min(lines.Length, 5);
            for (var i = 1; i < checkLines; i++)
            {
                var rowCount = CountDelimiter(lines[i], delimiter);
                if (Math.Abs(rowCount - headerCount) > 1)
                {
                    consistent = false;
                    break;
                }
            }

            if (consistent)
                return true;
        }

        return false;
    }

    private static int CountDelimiter(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
                inQuotes = !inQuotes;
            else if (ch == delimiter && !inQuotes)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Detects the delimiter used in CSV content.
    /// </summary>
    public static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        var best = ',';
        var bestCount = 0;
        foreach (var delimiter in new[] { ',', '\t', ';', '|' })
        {
            var count = CountDelimiter(firstLine, delimiter);
            if (count > bestCount)
            {
                bestCount = count;
                best = delimiter;
            }
        }

        return best;
    }
}
