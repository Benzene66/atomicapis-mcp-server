using System.Text.RegularExpressions;

namespace RelatedContent;

public static class SimilarityCalculator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "been",
        "be", "have", "has", "had", "do", "does", "did", "will", "would",
        "could", "should", "may", "might", "shall", "can", "need", "dare",
        "ought", "used", "it", "its", "it's", "he", "she", "they", "them",
        "their", "his", "her", "him", "my", "your", "our", "we", "us", "me",
        "i", "you", "this", "that", "these", "those", "which", "who", "whom",
        "what", "where", "when", "how", "why", "all", "each", "every", "both",
        "few", "more", "most", "other", "some", "such", "no", "nor", "not",
        "only", "own", "same", "so", "than", "too", "very", "just", "because",
        "if", "then", "else", "while", "about", "up", "out", "off", "over",
        "under", "again", "further", "once", "here", "there", "any", "also",
        "after", "before", "above", "below", "between", "through", "during",
        "into", "being", "having", "doing", "get", "got", "gets", "getting",
        "make", "made", "making", "go", "going", "gone", "went", "come",
        "came", "take", "took", "taken", "see", "saw", "seen", "know",
        "knew", "known", "think", "thought", "say", "said", "give", "gave",
        "given", "tell", "told", "find", "found", "want", "wanted", "let",
        "seem", "seemed", "still", "well", "back", "even", "new", "now",
        "way", "like", "much", "many", "thing", "things", "man", "men",
        "long", "look", "looked", "day", "must", "don't", "doesn't", "didn't",
        "won't", "wouldn't", "can't", "couldn't", "shouldn't", "isn't",
        "aren't", "wasn't", "weren't", "hasn't", "haven't", "hadn't"
    };

    private static readonly Regex TokenPattern = new(@"[a-zA-Z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Tokenizes text: lowercase, split on non-alphanumeric, remove stop words.
    /// </summary>
    public static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var matches = TokenPattern.Matches(text.ToLowerInvariant());
        var tokens = new List<string>();
        foreach (Match m in matches)
        {
            if (m.Value.Length > 1 && !StopWords.Contains(m.Value))
                tokens.Add(m.Value);
        }
        return tokens.ToArray();
    }

    /// <summary>
    /// Computes TF (term frequency) for a document's tokens.
    /// </summary>
    public static Dictionary<string, double> ComputeTf(string[] tokens)
    {
        var tf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (tokens.Length == 0) return tf;

        foreach (var token in tokens)
        {
            tf.TryGetValue(token, out var count);
            tf[token] = count + 1;
        }

        // Normalize by document length
        foreach (var key in tf.Keys.ToList())
        {
            tf[key] /= tokens.Length;
        }

        return tf;
    }

    /// <summary>
    /// Computes IDF (inverse document frequency) across all documents.
    /// </summary>
    public static Dictionary<string, double> ComputeIdf(string[][] allTokenSets)
    {
        var idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        int totalDocs = allTokenSets.Length;
        if (totalDocs == 0) return idf;

        // Count how many documents contain each term
        var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var tokens in allTokenSets)
        {
            var unique = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
            foreach (var term in unique)
            {
                docFreq.TryGetValue(term, out var count);
                docFreq[term] = count + 1;
            }
        }

        foreach (var kvp in docFreq)
        {
            // Standard IDF formula: log(N / df) + 1 for smoothing
            idf[kvp.Key] = Math.Log((double)totalDocs / kvp.Value) + 1.0;
        }

        return idf;
    }

    /// <summary>
    /// Builds a TF-IDF vector for a document given its TF values and global IDF values.
    /// </summary>
    public static Dictionary<string, double> BuildTfidfVector(
        Dictionary<string, double> tf,
        Dictionary<string, double> idf)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in tf)
        {
            double idfValue = idf.TryGetValue(kvp.Key, out var v) ? v : 1.0;
            vector[kvp.Key] = kvp.Value * idfValue;
        }
        return vector;
    }

    /// <summary>
    /// Computes cosine similarity between two sparse vectors.
    /// </summary>
    public static double CosineSimilarity(
        Dictionary<string, double> vectorA,
        Dictionary<string, double> vectorB)
    {
        if (vectorA.Count == 0 || vectorB.Count == 0)
            return 0.0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        foreach (var kvp in vectorA)
        {
            magnitudeA += kvp.Value * kvp.Value;
            if (vectorB.TryGetValue(kvp.Key, out var bVal))
                dotProduct += kvp.Value * bVal;
        }

        foreach (var kvp in vectorB)
        {
            magnitudeB += kvp.Value * kvp.Value;
        }

        double denominator = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        if (denominator == 0) return 0.0;

        return dotProduct / denominator;
    }

    /// <summary>
    /// Computes Jaccard similarity between two token sets: |intersection| / |union|.
    /// </summary>
    public static double JaccardSimilarity(string[] tokensA, string[] tokensB)
    {
        var setA = new HashSet<string>(tokensA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(tokensB, StringComparer.OrdinalIgnoreCase);

        if (setA.Count == 0 && setB.Count == 0)
            return 0.0;

        var intersection = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        intersection.IntersectWith(setB);

        var union = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(setB);

        if (union.Count == 0) return 0.0;

        return (double)intersection.Count / union.Count;
    }

    /// <summary>
    /// Finds terms shared between two token arrays.
    /// </summary>
    public static string[] FindSharedTerms(string[] tokensA, string[] tokensB)
    {
        var setA = new HashSet<string>(tokensA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(tokensB, StringComparer.OrdinalIgnoreCase);
        setA.IntersectWith(setB);
        return setA.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
