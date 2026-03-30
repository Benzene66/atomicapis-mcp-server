using System.ComponentModel;
using PromptInjectionDetector;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class PromptInjectionTools
{
    [McpServerTool, Description(
        "Analyzes text for prompt injection attacks. Scores risk from 0-100 across 5 categories: " +
        "direct injection, jailbreak, indirect injection, role-play manipulation, and encoded payloads. " +
        "Returns risk level, recommended action, flagged spans, and category breakdown.")]
    public static string DetectPromptInjection(
        [Description("The text to analyze for prompt injection")] string text,
        [Description("Risk score threshold (0-100) above which text is flagged (default: 50)")] double threshold = 50.0)
    {
        var result = InjectionDetectionEngine.Analyze(text, threshold, includeDetails: true);

        var flagged = result.FlaggedSpans.Length > 0
            ? "\nFlagged spans:\n" + string.Join("\n", result.FlaggedSpans.Select(s =>
                $"  [{s.StartIndex}-{s.EndIndex}] \"{s.MatchedText}\" ({s.Category}, weight: {s.Weight})"))
            : "";

        var categories = string.Join(", ", result.CategoryScores.Select(c =>
            $"{c.Category}: {c.Score:F1} ({c.MatchCount} matches)"));

        return $"""
            Risk score: {result.RiskScore:F1}/100
            Risk level: {result.RiskLevel}
            Recommended action: {result.RecommendedAction}
            Category scores: {categories}{flagged}
            """;
    }
}
