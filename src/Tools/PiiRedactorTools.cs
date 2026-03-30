using System.ComponentModel;
using PiiRedactor;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class PiiRedactorTools
{
    [McpServerTool, Description(
        "Redacts personally identifiable information (PII) from text. " +
        "Detects and replaces emails, SSNs, credit card numbers, phone numbers, " +
        "URLs, IP addresses, street addresses, and dates of birth with tagged placeholders.")]
    public static string RedactPii(
        [Description("The text containing PII to redact")] string text,
        [Description("Include a mapping of placeholders to original values (default: false)")] bool includeMapping = false)
    {
        var result = PiiRedactorEngine.Redact(text, includeMapping);

        var counts = string.Join(", ", result.PiiCounts.Select(kv => $"{kv.Key}: {kv.Value}"));

        var mappingInfo = result.Mapping != null
            ? "\nMapping:\n" + string.Join("\n", result.Mapping.Select(kv => $"  {kv.Key} → {kv.Value}"))
            : "";

        return $"""
            Redacted text:
            {result.RedactedText}

            Total PII found: {result.TotalPiiFound}
            Breakdown: {counts}{mappingInfo}
            """;
    }
}
