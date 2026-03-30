using System.Text.RegularExpressions;

namespace SemanticSearchRedirector;

public static class TextProcessor
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "having", "do", "does", "did", "doing",
        "will", "would", "shall", "should", "may", "might", "must", "can", "could",
        "in", "on", "at", "to", "for", "of", "with", "by", "from", "up", "about",
        "into", "through", "during", "before", "after", "above", "below", "between",
        "out", "off", "over", "under", "again", "further", "then", "once",
        "and", "but", "or", "nor", "not", "so", "yet", "both", "either", "neither",
        "each", "every", "all", "any", "few", "more", "most", "other", "some", "such",
        "no", "only", "own", "same", "than", "too", "very",
        "i", "me", "my", "myself", "we", "our", "ours", "ourselves",
        "you", "your", "yours", "yourself", "yourselves",
        "he", "him", "his", "himself", "she", "her", "hers", "herself",
        "it", "its", "itself", "they", "them", "their", "theirs", "themselves",
        "what", "which", "who", "whom", "this", "that", "these", "those",
        "am", "if", "as", "until", "while", "because", "although", "though",
        "since", "unless", "when", "where", "how", "why",
        "here", "there", "just", "also", "already", "still", "even",
        "now", "down", "well", "back", "much", "many", "get", "got",
        "like", "make", "go", "going", "way", "long", "come", "take",
        "thing", "things", "really", "actually", "please", "want", "need",
        "know", "think", "see", "look", "find", "give", "tell", "say",
        "us", "let", "around", "however", "never", "always", "often",
        "another", "enough", "along", "away", "else", "ever", "upon",
        "across", "among", "within", "without", "towards", "whether",
        "against", "behind", "beyond", "despite", "except", "near",
        "rather", "perhaps", "quite", "somewhat", "together", "apart"
    };

    private static readonly Dictionary<string, string[]> SynonymGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cheap"] = new[] { "affordable", "inexpensive", "budget", "low-cost", "bargain", "economical" },
        ["affordable"] = new[] { "cheap", "inexpensive", "budget", "low-cost", "bargain", "economical" },
        ["inexpensive"] = new[] { "cheap", "affordable", "budget", "low-cost", "bargain", "economical" },
        ["budget"] = new[] { "cheap", "affordable", "inexpensive", "low-cost", "bargain", "economical" },
        ["fast"] = new[] { "quick", "rapid", "speedy", "swift", "express" },
        ["quick"] = new[] { "fast", "rapid", "speedy", "swift", "express" },
        ["rapid"] = new[] { "fast", "quick", "speedy", "swift", "express" },
        ["speedy"] = new[] { "fast", "quick", "rapid", "swift", "express" },
        ["swift"] = new[] { "fast", "quick", "rapid", "speedy", "express" },
        ["big"] = new[] { "large", "huge", "enormous", "giant", "massive" },
        ["large"] = new[] { "big", "huge", "enormous", "giant", "massive" },
        ["huge"] = new[] { "big", "large", "enormous", "giant", "massive" },
        ["enormous"] = new[] { "big", "large", "huge", "giant", "massive" },
        ["small"] = new[] { "tiny", "little", "mini", "compact", "petite" },
        ["tiny"] = new[] { "small", "little", "mini", "compact", "petite" },
        ["little"] = new[] { "small", "tiny", "mini", "compact", "petite" },
        ["mini"] = new[] { "small", "tiny", "little", "compact", "petite" },
        ["compact"] = new[] { "small", "tiny", "little", "mini", "petite" },
        ["good"] = new[] { "great", "excellent", "quality", "premium", "superior", "fine" },
        ["great"] = new[] { "good", "excellent", "quality", "premium", "superior", "fine" },
        ["excellent"] = new[] { "good", "great", "quality", "premium", "superior", "fine" },
        ["quality"] = new[] { "good", "great", "excellent", "premium", "superior", "fine" },
        ["premium"] = new[] { "good", "great", "excellent", "quality", "superior", "fine" },
        ["new"] = new[] { "latest", "recent", "modern", "fresh", "current" },
        ["latest"] = new[] { "new", "recent", "modern", "fresh", "current" },
        ["recent"] = new[] { "new", "latest", "modern", "fresh", "current" },
        ["modern"] = new[] { "new", "latest", "recent", "fresh", "current" },
        ["old"] = new[] { "vintage", "classic", "retro", "used", "antique" },
        ["vintage"] = new[] { "old", "classic", "retro", "used", "antique" },
        ["classic"] = new[] { "old", "vintage", "retro", "used", "antique" },
        ["retro"] = new[] { "old", "vintage", "classic", "used", "antique" },
        ["beautiful"] = new[] { "pretty", "gorgeous", "stunning", "attractive", "lovely" },
        ["pretty"] = new[] { "beautiful", "gorgeous", "stunning", "attractive", "lovely" },
        ["ugly"] = new[] { "unattractive", "hideous", "unsightly" },
        ["happy"] = new[] { "joyful", "cheerful", "glad", "pleased", "content" },
        ["sad"] = new[] { "unhappy", "sorrowful", "melancholy", "gloomy" },
        ["easy"] = new[] { "simple", "straightforward", "effortless", "basic" },
        ["simple"] = new[] { "easy", "straightforward", "effortless", "basic" },
        ["hard"] = new[] { "difficult", "tough", "challenging", "complex" },
        ["difficult"] = new[] { "hard", "tough", "challenging", "complex" },
        ["strong"] = new[] { "powerful", "sturdy", "robust", "durable" },
        ["powerful"] = new[] { "strong", "sturdy", "robust", "durable" },
        ["weak"] = new[] { "fragile", "delicate", "flimsy", "frail" },
        ["hot"] = new[] { "warm", "heated", "scorching", "burning" },
        ["cold"] = new[] { "cool", "chilly", "freezing", "icy", "frigid" },
        ["expensive"] = new[] { "costly", "pricey", "high-end", "luxury", "premium" },
        ["costly"] = new[] { "expensive", "pricey", "high-end", "luxury" },
        ["shoes"] = new[] { "footwear", "sneakers" },
        ["footwear"] = new[] { "shoes", "sneakers", "boots", "sandals" },
        ["sneakers"] = new[] { "shoes", "footwear" },
        ["boots"] = new[] { "footwear" },
        ["sandals"] = new[] { "footwear" },
        ["phone"] = new[] { "smartphone", "mobile", "cellphone", "handset" },
        ["smartphone"] = new[] { "phone", "mobile", "cellphone", "handset" },
        ["laptop"] = new[] { "notebook", "computer", "pc" },
        ["computer"] = new[] { "laptop", "notebook", "pc", "desktop" },
    };

    private static readonly Regex TokenRegex = new(@"[a-z0-9]+(?:-[a-z0-9]+)*", RegexOptions.Compiled);

    /// <summary>
    /// Tokenizes text: lowercases, splits on non-alphanumeric, removes stop words.
    /// </summary>
    public static string[] Tokenize(string? text, bool removeStopWords = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var lower = text.ToLowerInvariant();
        var matches = TokenRegex.Matches(lower);
        var tokens = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            var token = match.Value;
            if (removeStopWords && StopWords.Contains(token))
                continue;
            tokens.Add(token);
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Expands tokens with synonyms, returning the union of original + synonym tokens.
    /// </summary>
    public static string[] ExpandWithSynonyms(string[] tokens)
    {
        var expanded = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            if (SynonymGroups.TryGetValue(token, out var synonyms))
            {
                foreach (var synonym in synonyms)
                    expanded.Add(synonym);
            }
        }

        return expanded.ToArray();
    }

    /// <summary>
    /// Computes Levenshtein distance between two strings.
    /// </summary>
    public static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// Returns true if the candidate is a fuzzy match for the query token (Levenshtein distance within threshold).
    /// </summary>
    public static bool IsFuzzyMatch(string queryToken, string candidateToken, int maxDistance = 1)
    {
        if (string.IsNullOrEmpty(queryToken) || string.IsNullOrEmpty(candidateToken))
            return false;

        // Skip very short tokens for fuzzy matching to avoid false positives
        if (queryToken.Length <= 2 || candidateToken.Length <= 2)
            return false;

        // Length difference must be within maxDistance
        if (Math.Abs(queryToken.Length - candidateToken.Length) > maxDistance)
            return false;

        return LevenshteinDistance(queryToken, candidateToken) <= maxDistance;
    }

    /// <summary>
    /// Generates bigrams from a token array.
    /// </summary>
    public static string[] GenerateBigrams(string[] tokens)
    {
        if (tokens.Length < 2)
            return Array.Empty<string>();

        var bigrams = new string[tokens.Length - 1];
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            bigrams[i] = $"{tokens[i]}_{tokens[i + 1]}";
        }

        return bigrams;
    }

    /// <summary>
    /// Checks if a token is a stop word.
    /// </summary>
    public static bool IsStopWord(string token)
    {
        return StopWords.Contains(token);
    }
}
