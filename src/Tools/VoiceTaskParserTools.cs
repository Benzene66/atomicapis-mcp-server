using System.ComponentModel;
using VoiceTaskParser;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class VoiceTaskParserTools
{
    [McpServerTool, Description(
        "Extracts structured to-do items from voice transcripts or meeting notes. " +
        "Detects action verbs, deadlines, priorities (high/medium/low), and assignees (@mentions or name patterns). " +
        "Filters out chatter, greetings, and filler. Returns numbered tasks with confidence scores.")]
    public static string ParseTasks(
        [Description("The transcript or meeting notes to parse for tasks")] string transcript,
        [Description("Default assignee when none is detected in the text")] string? defaultAssignee = null,
        [Description("Minimum confidence threshold 0-1 for task detection (default: 0.3)")] double confidenceThreshold = 0.3)
    {
        var request = new TaskParseRequest(transcript, defaultAssignee, confidenceThreshold);
        var result = TaskDetectionEngine.Parse(request);

        if (result.TasksDetected == 0)
        {
            return $"""
                No tasks detected in transcript ({result.SentencesAnalyzed} sentences analyzed).
                """;
        }

        var tasks = string.Join("\n", result.Tasks.Select(t =>
        {
            var parts = new List<string> { $"  #{t.TaskNumber}: {t.Description}" };
            if (t.Assignee != null) parts.Add($"    Assignee: {t.Assignee}");
            parts.Add($"    Priority: {t.Priority}");
            if (t.Deadline != null) parts.Add($"    Deadline: {t.Deadline}");
            parts.Add($"    Confidence: {t.Confidence:F2}");
            return string.Join("\n", parts);
        }));

        return $"""
            Tasks detected: {result.TasksDetected} (from {result.SentencesAnalyzed} sentences)

            {tasks}
            """;
    }
}
