using System.Text.RegularExpressions;

namespace PromptInjectionDetector;

public record DetectInjectionRequest(
    string Text,
    double? Threshold = null,
    bool? IncludeDetails = null);

public record DetectInjectionResponse(
    double RiskScore,
    string RiskLevel,
    string RecommendedAction,
    FlaggedSpan[] FlaggedSpans,
    CategoryScore[] CategoryScores,
    HeuristicDetail[]? HeuristicDetails);

public record FlaggedSpan(
    int StartIndex,
    int EndIndex,
    string MatchedText,
    string Category,
    string PatternName,
    double Weight);

public record CategoryScore(
    string Category,
    double Score,
    int MatchCount);

public record HeuristicDetail(
    string Name,
    double Score,
    string Description);

public static class InjectionDetectionEngine
{
    private static readonly string[] ImperativeVerbs =
    {
        "ignore", "forget", "disregard", "override", "bypass", "disable", "remove",
        "execute", "run", "decode", "pretend", "imagine", "act", "behave", "respond",
        "tell", "say", "write", "output", "print", "reveal", "show", "display",
        "stop", "start", "begin", "do", "must", "shall", "should", "obey", "follow",
        "comply", "switch", "change", "enable", "activate", "unlock"
    };

    private static readonly Regex ImperativeRegex = new(
        @"\b(" + string.Join("|", ImperativeVerbs) + @")\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExcessiveCapsRegex = new(
        @"[A-Z]{5,}",
        RegexOptions.Compiled);

    private static readonly Regex SpecialCharDensityRegex = new(
        @"[^\w\s]",
        RegexOptions.Compiled);

    private static readonly Regex Base64Regex = new(
        @"[A-Za-z0-9+/]{20,}={0,2}",
        RegexOptions.Compiled);

    public static DetectInjectionResponse Analyze(string text, double threshold = 50.0, bool includeDetails = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DetectInjectionResponse(
                0, "safe", "allow",
                Array.Empty<FlaggedSpan>(),
                Array.Empty<CategoryScore>(),
                includeDetails ? Array.Empty<HeuristicDetail>() : null);
        }

        // Layer 1: Pattern matching
        var flaggedSpans = new List<FlaggedSpan>();
        var categoryHits = new Dictionary<string, (double totalWeight, int count)>();
        double patternScore = 0;

        foreach (var pattern in InjectionPatterns.All)
        {
            var matches = pattern.Pattern.Matches(text);
            foreach (Match match in matches)
            {
                flaggedSpans.Add(new FlaggedSpan(
                    match.Index,
                    match.Index + match.Length,
                    match.Value,
                    pattern.Category,
                    pattern.Name,
                    pattern.Weight));

                patternScore += pattern.Weight;

                if (!categoryHits.ContainsKey(pattern.Category))
                    categoryHits[pattern.Category] = (0, 0);

                var current = categoryHits[pattern.Category];
                categoryHits[pattern.Category] = (current.totalWeight + pattern.Weight, current.count + 1);
            }
        }

        // Layer 2: Heuristic analysis
        var heuristics = new List<HeuristicDetail>();
        double heuristicScore = 0;

        // Instruction density
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;
        if (wordCount > 0)
        {
            int imperativeCount = ImperativeRegex.Matches(text).Count;
            double density = (double)imperativeCount / wordCount;
            if (density > 0.15)
            {
                double densityScore = Math.Min(15, density * 50);
                heuristicScore += densityScore;
                heuristics.Add(new HeuristicDetail(
                    "instruction_density",
                    densityScore,
                    $"High imperative verb density: {density:P1} ({imperativeCount}/{wordCount} words)"));
            }
        }

        // Excessive caps
        int capsMatches = ExcessiveCapsRegex.Matches(text).Count;
        if (capsMatches >= 2)
        {
            double capsScore = Math.Min(10, capsMatches * 3.0);
            heuristicScore += capsScore;
            heuristics.Add(new HeuristicDetail(
                "excessive_caps",
                capsScore,
                $"Excessive capitalization detected ({capsMatches} sequences)"));
        }

        // Special character density
        if (text.Length > 10)
        {
            int specialCount = SpecialCharDensityRegex.Matches(text).Count;
            double specialDensity = (double)specialCount / text.Length;
            if (specialDensity > 0.3)
            {
                double specialScore = Math.Min(10, specialDensity * 20);
                heuristicScore += specialScore;
                heuristics.Add(new HeuristicDetail(
                    "special_char_density",
                    specialScore,
                    $"High special character density: {specialDensity:P1}"));
            }
        }

        // Base64 content analysis
        var base64Matches = Base64Regex.Matches(text);
        foreach (Match b64 in base64Matches)
        {
            try
            {
                // Pad if needed
                string candidate = b64.Value;
                int pad = candidate.Length % 4;
                if (pad > 0) candidate += new string('=', 4 - pad);

                byte[] decoded = Convert.FromBase64String(candidate);
                string decodedText = System.Text.Encoding.UTF8.GetString(decoded);

                // Check if decoded text contains suspicious patterns
                bool suspicious = false;
                foreach (var pattern in InjectionPatterns.All)
                {
                    if (pattern.Pattern.IsMatch(decodedText))
                    {
                        suspicious = true;
                        break;
                    }
                }

                if (suspicious)
                {
                    double b64Score = 25;
                    heuristicScore += b64Score;
                    heuristics.Add(new HeuristicDetail(
                        "encoded_suspicious_content",
                        b64Score,
                        "Base64 content decodes to suspicious text"));
                }
            }
            catch
            {
                // Not valid base64, skip
            }
        }

        // Multiple categories triggered - compound effect
        int categoriesTriggered = categoryHits.Count;
        double categoryMultiplier = 1.0;
        if (categoriesTriggered > 1)
        {
            categoryMultiplier = 1.0 + 0.1 * (categoriesTriggered - 1);
            heuristics.Add(new HeuristicDetail(
                "multi_category",
                categoriesTriggered,
                $"Multiple attack categories detected ({categoriesTriggered} categories), score multiplied by {categoryMultiplier:F1}x"));
        }

        // Short text with injection = concentrated attack
        if (text.Length < 100 && patternScore > 10)
        {
            double concentrationBoost = 5;
            heuristicScore += concentrationBoost;
            heuristics.Add(new HeuristicDetail(
                "concentrated_attack",
                concentrationBoost,
                "Short text with injection patterns (concentrated attack)"));
        }

        // Long text with few matches = likely benign context
        if (text.Length > 1000 && flaggedSpans.Count <= 1 && patternScore < 15)
        {
            double reductionAmount = Math.Min(patternScore * 0.3, 10);
            heuristicScore -= reductionAmount;
            if (reductionAmount > 0)
            {
                heuristics.Add(new HeuristicDetail(
                    "long_benign_context",
                    -reductionAmount,
                    "Long text with minimal matches suggests benign context"));
            }
        }

        // Layer 3: Composite scoring
        double rawScore = (patternScore + heuristicScore) * categoryMultiplier;
        double finalScore = Math.Clamp(rawScore, 0, 100);

        string riskLevel = finalScore switch
        {
            <= 20 => "safe",
            <= 40 => "low",
            <= 60 => "medium",
            <= 80 => "high",
            _ => "critical"
        };

        string recommendedAction = riskLevel switch
        {
            "safe" => "allow",
            "low" => finalScore >= threshold ? "flag_for_review" : "allow",
            "medium" => "flag_for_review",
            "high" => "block",
            "critical" => "block_and_alert",
            _ => "allow"
        };

        // Build category scores
        var categoryScores = categoryHits.Select(kvp =>
        {
            double catScore = Math.Clamp(kvp.Value.totalWeight, 0, 100);
            return new CategoryScore(kvp.Key, catScore, kvp.Value.count);
        }).OrderByDescending(c => c.Score).ToArray();

        return new DetectInjectionResponse(
            Math.Round(finalScore, 2),
            riskLevel,
            recommendedAction,
            flaggedSpans.OrderBy(s => s.StartIndex).ToArray(),
            categoryScores,
            includeDetails ? heuristics.ToArray() : null);
    }
}
