using System.Text.RegularExpressions;

namespace VoiceTaskParser;

public static class TaskDetectionEngine
{
    // Action verb patterns that strongly indicate a task
    private static readonly string[] ActionVerbs =
    {
        "schedule", "send", "email", "call", "follow up", "follow-up", "prepare",
        "create", "update", "review", "check", "confirm", "book", "arrange",
        "set up", "setup", "organize", "plan", "draft", "write", "submit",
        "order", "buy", "purchase", "cancel", "reschedule", "move", "transfer",
        "assign", "delegate", "notify", "remind", "contact", "reach out",
        "finish", "complete", "deliver", "ship", "fix", "resolve", "address",
        "investigate", "research", "analyze", "look into", "figure out",
        "meet with", "talk to", "discuss", "coordinate", "sync", "align",
        "file", "register", "renew", "upgrade", "downgrade", "onboard",
        "hire", "interview", "train", "document", "audit", "approve", "sign",
        "negotiate", "close", "launch", "deploy", "release", "test", "verify",
        "clean up", "clean", "remove", "delete", "archive", "backup", "migrate"
    };

    // Modal/imperative patterns that signal obligation
    private static readonly Regex[] TaskIndicatorPatterns =
    {
        new(@"\b(?:need|needs)\s+to\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:have|has)\s+to\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:must|shall|should)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:make\s+sure|ensure|be\s+sure)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:don'?t\s+forget|remember)\s+to\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:let'?s|we\s+should|we\s+need|we\s+have)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:action\s+item|todo|to-do|task)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:deadline|due\s+(?:by|date|on))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:asap|urgently?|immediately|right\s+away|priority)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:can\s+you|could\s+you|would\s+you|will\s+you)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:i'?ll|i\s+will|i\s+am\s+going\s+to|i'?m\s+going\s+to)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b(?:assigned?\s+to|owner|responsible)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    // Date/time patterns
    private static readonly Regex DatePattern = new(
        @"\b(?:today|tomorrow|tonight|this\s+(?:week|month|afternoon|morning|evening)|" +
        @"next\s+(?:week|month|monday|tuesday|wednesday|thursday|friday|saturday|sunday)|" +
        @"by\s+(?:end\s+of\s+(?:day|week|month)|(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday))|" +
        @"(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday)|" +
        @"(?:january|february|march|april|may|june|july|august|september|october|november|december)\s+\d{1,2}(?:st|nd|rd|th)?|" +
        @"\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?|" +
        @"(?:in\s+)?\d+\s+(?:days?|weeks?|months?|hours?)(?:\s+(?:from\s+now))?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Priority indicators
    private static readonly Regex HighPriorityPattern = new(
        @"\b(?:urgent(?:ly)?|asap|critical|immediately|right\s+away|top\s+priority|high\s+priority|p0|p1|blocker)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MediumPriorityPattern = new(
        @"\b(?:important|soon|this\s+week|priority|should|p2)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Assignee patterns
    private static readonly Regex AssigneePattern = new(
        @"\b(?:(?:assign(?:ed)?|give|hand|pass)\s+(?:it\s+)?(?:to|over\s+to)\s+)(\w+(?:\s+\w+)?)|" +
        @"\b(\w+(?:\s+\w+)?)\s+(?:will|should|needs?\s+to|is\s+going\s+to|can|could)\b|" +
        @"\b(?:@)(\w+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Words that disqualify a sentence from being a task
    private static readonly Regex ChatterPattern = new(
        @"^\s*(?:yeah|yes|no|okay|ok|sure|right|exactly|absolutely|definitely|" +
        @"uh+|um+|hmm+|huh|wow|oh|ah|like|so|well|anyway|anyhow|" +
        @"good\s+(?:morning|afternoon|evening)|hi\s+everyone|hello|hey|" +
        @"thanks?(?:\s+you)?|thank\s+you|sounds?\s+good|got\s+it|" +
        @"i\s+(?:think|agree|see|understand|know)|that'?s\s+(?:true|right|correct|fair)|" +
        @"(?:how|what)\s+(?:are|is)\s+(?:you|everyone|things)|" +
        @"nice|great|awesome|perfect|wonderful|cool|interesting)\s*[.!?,]*\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TaskParseResponse Parse(TaskParseRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var sentences = SplitIntoSentences(request.Transcript);
        var tasks = new List<ExtractedTask>();
        int taskNumber = 0;

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 5)
                continue;

            // Skip obvious chatter
            if (ChatterPattern.IsMatch(trimmed))
                continue;

            var score = ScoreSentence(trimmed);
            double threshold = request.ConfidenceThreshold ?? 0.3;

            if (score.Total >= threshold)
            {
                taskNumber++;
                var task = BuildTask(trimmed, score, taskNumber, request.DefaultAssignee);
                tasks.Add(task);
            }
        }

        sw.Stop();

        return new TaskParseResponse(
            TranscriptLength: request.Transcript.Length,
            SentencesAnalyzed: sentences.Length,
            TasksDetected: tasks.Count,
            Tasks: tasks.ToArray(),
            ParseDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2));
    }

    internal static string[] SplitIntoSentences(string text)
    {
        // Split on sentence-ending punctuation, newlines, or semicolons
        var parts = Regex.Split(text, @"(?<=[.!?;])\s+|\r?\n+");
        var result = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                result.Add(trimmed);
        }

        return result.ToArray();
    }

    internal static SentenceScore ScoreSentence(string sentence)
    {
        double actionVerbScore = 0;
        double indicatorScore = 0;
        double dateScore = 0;
        string? matchedVerb = null;

        // Check action verbs
        var lowerSentence = sentence.ToLowerInvariant();
        foreach (var verb in ActionVerbs)
        {
            // Word boundary check
            var pattern = @"\b" + Regex.Escape(verb) + @"\b";
            if (Regex.IsMatch(lowerSentence, pattern))
            {
                actionVerbScore = 0.4;
                matchedVerb = verb;
                break;
            }
        }

        // Check task indicator patterns
        int indicatorMatches = 0;
        foreach (var pattern in TaskIndicatorPatterns)
        {
            if (pattern.IsMatch(sentence))
                indicatorMatches++;
        }
        indicatorScore = Math.Min(indicatorMatches * 0.2, 0.4);

        // Check for dates/deadlines
        if (DatePattern.IsMatch(sentence))
            dateScore = 0.2;

        double total = Math.Min(actionVerbScore + indicatorScore + dateScore, 1.0);

        return new SentenceScore(total, actionVerbScore, indicatorScore, dateScore, matchedVerb);
    }

    private static ExtractedTask BuildTask(string sentence, SentenceScore score, int number, string? defaultAssignee)
    {
        // Extract deadline
        string? deadline = null;
        var dateMatch = DatePattern.Match(sentence);
        if (dateMatch.Success)
            deadline = dateMatch.Value.Trim();

        // Determine priority
        string priority;
        if (HighPriorityPattern.IsMatch(sentence))
            priority = "high";
        else if (MediumPriorityPattern.IsMatch(sentence))
            priority = "medium";
        else
            priority = "low";

        // Extract assignee
        string? assignee = ExtractAssignee(sentence) ?? defaultAssignee;

        // Clean up the task description
        string description = CleanTaskDescription(sentence);

        return new ExtractedTask(
            TaskNumber: number,
            Description: description,
            RawSentence: sentence,
            Assignee: assignee,
            Priority: priority,
            Deadline: deadline,
            Confidence: Math.Round(score.Total, 3),
            ActionVerb: score.MatchedVerb);
    }

    internal static string? ExtractAssignee(string sentence)
    {
        // Check for @mentions first
        var atMatch = Regex.Match(sentence, @"@(\w+)", RegexOptions.IgnoreCase);
        if (atMatch.Success)
            return atMatch.Groups[1].Value;

        // Check for "assign ... to X" pattern
        var assignMatch = Regex.Match(sentence,
            @"\b(?:assign(?:ed)?|give|hand|pass)\s+(?:\w+\s+)*?(?:to|over\s+to)\s+(\w+)",
            RegexOptions.IgnoreCase);
        if (assignMatch.Success)
        {
            var name = assignMatch.Groups[1].Value;
            if (!IsCommonWord(name))
                return name;
        }

        // Check for "X will/should/needs to" pattern (only if it's a proper name — capitalized)
        var nameMatch = Regex.Match(sentence,
            @"\b([A-Z]\w+)\s+(?:will|should|needs?\s+to|is\s+going\s+to)\b");
        if (nameMatch.Success)
        {
            var name = nameMatch.Groups[1].Value;
            if (!IsCommonWord(name) && name.Length > 1)
                return name;
        }

        return null;
    }

    private static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "i", "we", "you", "he", "she", "it", "they", "someone", "everyone", "anybody",
            "the", "a", "an", "this", "that", "these", "those", "my", "your", "our", "their",
            "who", "what", "when", "where", "which", "there", "here", "then", "now",
            "also", "just", "too", "very", "really", "maybe", "perhaps", "probably",
            "team", "management", "client", "customer", "company", "department"
        };
        return common.Contains(word);
    }

    private static string CleanTaskDescription(string sentence)
    {
        var desc = sentence.Trim();

        // Remove leading filler
        desc = Regex.Replace(desc, @"^(?:so\s+|well\s+|okay\s+|and\s+|also\s+|then\s+|um\s+|uh\s+)", "",
            RegexOptions.IgnoreCase).Trim();

        // Remove trailing punctuation excess
        desc = Regex.Replace(desc, @"[.!?,;]+$", ".").Trim();

        // Capitalize first letter
        if (desc.Length > 0 && char.IsLower(desc[0]))
            desc = char.ToUpper(desc[0]) + desc[1..];

        return desc;
    }
}

// Internal scoring record
internal record SentenceScore(
    double Total,
    double ActionVerbScore,
    double IndicatorScore,
    double DateScore,
    string? MatchedVerb);

// Public DTOs
public record TaskParseRequest(
    string Transcript,
    string? DefaultAssignee = null,
    double? ConfidenceThreshold = null
);

public record TaskParseResponse(
    int TranscriptLength,
    int SentencesAnalyzed,
    int TasksDetected,
    ExtractedTask[] Tasks,
    double ParseDurationMs
);

public record ExtractedTask(
    int TaskNumber,
    string Description,
    string RawSentence,
    string? Assignee,
    string Priority,
    string? Deadline,
    double Confidence,
    string? ActionVerb
);
