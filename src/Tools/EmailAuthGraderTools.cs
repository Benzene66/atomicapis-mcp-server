using System.ComponentModel;
using EmailAuthGrader;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class EmailAuthGraderTools
{
    [McpServerTool, Description(
        "Grades a domain's email authentication setup: checks SPF, DKIM, DMARC, MTA-STS, and BIMI records. " +
        "Returns a letter grade (A-F), numeric score, detailed results per protocol, and actionable recommendations.")]
    public static async Task<string> GradeEmailAuth(
        [Description("The domain to analyze (e.g. 'example.com')")] string domain)
    {
        var report = await EmailAuthAnalyzer.AnalyzeAsync(domain);

        var recommendations = report.Recommendations.Count > 0
            ? "\nRecommendations:\n" + string.Join("\n", report.Recommendations.Select(r => $"  - {r}"))
            : "";

        return $"""
            Domain: {report.Domain}
            Grade: {report.Grade}
            Score: {report.Score}/100

            SPF: {(report.Spf.Valid ? "Valid" : "Invalid/Missing")} — Record: {report.Spf.Record ?? "none"}
            DMARC: {(report.Dmarc.Valid ? "Valid" : "Invalid/Missing")} — Policy: {report.Dmarc.Policy ?? "none"}
            DKIM: {(report.Dkim.Detected ? $"Detected ({report.Dkim.SelectorsFound.Count} selectors)" : "Not detected")}
            MTA-STS: {(report.MtaSts.Detected ? "Detected" : "Not detected")}
            BIMI: {(report.Bimi.Detected ? "Detected" : "Not detected")}{recommendations}
            """;
    }
}
