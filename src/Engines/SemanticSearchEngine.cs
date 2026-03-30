using System.Diagnostics;

namespace SemanticSearchRedirector;

public static class SemanticSearchEngine
{
    public static SemanticSearchResponse Search(SemanticSearchRequest request)
    {
        var sw = Stopwatch.StartNew();

        int topK = Math.Clamp(request.TopK ?? 10, 1, 100);
        double minScore = Math.Clamp(request.MinScore ?? 0.01, 0.0, 1.0);
        bool fuzzyMatch = request.FuzzyMatch ?? true;
        bool synonymExpansion = request.SynonymExpansion ?? true;
        double titleWeight = Math.Clamp(request.TitleWeight ?? 2.0, 0.1, 10.0);

        // Tokenize query
        var queryTokens = TextProcessor.Tokenize(request.Query);

        if (queryTokens.Length == 0 || request.Items.Length == 0)
        {
            sw.Stop();
            return new SemanticSearchResponse(
                Query: request.Query,
                NormalizedQuery: "",
                TotalItems: request.Items.Length,
                MatchedItems: 0,
                Results: Array.Empty<SearchResult>(),
                SearchDurationMs: sw.Elapsed.TotalMilliseconds);
        }

        // Expand query with synonyms
        var expandedQueryTokens = synonymExpansion
            ? TextProcessor.ExpandWithSynonyms(queryTokens)
            : queryTokens;

        var normalizedQuery = string.Join(" ", queryTokens);

        // Build query bigrams
        var queryBigrams = TextProcessor.GenerateBigrams(queryTokens);

        // Tokenize all documents
        var docTokenSets = new List<(string[] textTokens, string[] titleTokens, string[] textBigrams, string[] titleBigrams)>();
        var allDocTokens = new List<string[]>();

        foreach (var item in request.Items)
        {
            var textTokens = TextProcessor.Tokenize(item.Text);
            var titleTokens = item.Title != null ? TextProcessor.Tokenize(item.Title) : Array.Empty<string>();
            var textBigrams = TextProcessor.GenerateBigrams(textTokens);
            var titleBigrams = TextProcessor.GenerateBigrams(titleTokens);
            docTokenSets.Add((textTokens, titleTokens, textBigrams, titleBigrams));
            allDocTokens.Add(textTokens);
            if (titleTokens.Length > 0)
                allDocTokens.Add(titleTokens);
        }

        // Build vocabulary from all documents + query
        var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tokens in allDocTokens)
            foreach (var t in tokens)
                vocabulary.Add(t);
        foreach (var t in expandedQueryTokens)
            vocabulary.Add(t);
        foreach (var bg in queryBigrams)
            vocabulary.Add(bg);
        foreach (var (_, _, textBigrams, titleBigrams) in docTokenSets)
        {
            foreach (var bg in textBigrams) vocabulary.Add(bg);
            foreach (var bg in titleBigrams) vocabulary.Add(bg);
        }

        var vocabList = vocabulary.ToList();
        var vocabIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < vocabList.Count; i++)
            vocabIndex[vocabList[i]] = i;

        // Build document frequency (how many documents contain each term)
        int totalDocs = request.Items.Length;
        var docFreq = new int[vocabList.Count];

        for (int d = 0; d < totalDocs; d++)
        {
            var (textTokens, titleTokens, textBigrams, titleBigrams) = docTokenSets[d];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in textTokens) seen.Add(t);
            foreach (var t in titleTokens) seen.Add(t);
            foreach (var bg in textBigrams) seen.Add(bg);
            foreach (var bg in titleBigrams) seen.Add(bg);

            // Also add fuzzy variants so IDF accounts for them
            if (fuzzyMatch)
            {
                var allItemTokens = new HashSet<string>(textTokens, StringComparer.OrdinalIgnoreCase);
                foreach (var t in titleTokens) allItemTokens.Add(t);

                foreach (var qt in expandedQueryTokens)
                {
                    foreach (var dt in allItemTokens)
                    {
                        if (TextProcessor.IsFuzzyMatch(qt, dt))
                            seen.Add(qt); // treat as if doc contains this query term
                    }
                }
            }

            foreach (var t in seen)
            {
                if (vocabIndex.TryGetValue(t, out var idx))
                    docFreq[idx]++;
            }
        }

        // Compute IDF
        var idf = new double[vocabList.Count];
        for (int i = 0; i < vocabList.Count; i++)
        {
            idf[i] = docFreq[i] > 0
                ? Math.Log(1.0 + (double)totalDocs / docFreq[i])
                : 0.0;
        }

        // Compute query TF-IDF vector
        var queryVec = new double[vocabList.Count];
        var queryTf = ComputeTermFrequency(expandedQueryTokens, queryBigrams, vocabIndex);
        for (int i = 0; i < vocabList.Count; i++)
            queryVec[i] = queryTf[i] * idf[i];

        // Compute document TF-IDF vectors and similarities
        var results = new List<SearchResult>();

        for (int d = 0; d < totalDocs; d++)
        {
            var item = request.Items[d];
            var (textTokens, titleTokens, textBigrams, titleBigrams) = docTokenSets[d];

            // Combined doc tokens with title weighting
            var docVec = new double[vocabList.Count];

            // Text TF-IDF
            var textTf = ComputeTermFrequency(textTokens, textBigrams, vocabIndex);
            for (int i = 0; i < vocabList.Count; i++)
                docVec[i] += textTf[i] * idf[i];

            // Title TF-IDF (weighted)
            if (titleTokens.Length > 0)
            {
                var titleTf = ComputeTermFrequency(titleTokens, titleBigrams, vocabIndex);
                for (int i = 0; i < vocabList.Count; i++)
                    docVec[i] += titleTf[i] * idf[i] * titleWeight;
            }

            // Fuzzy match boost: if query token fuzzy-matches a doc token, add to doc vector
            if (fuzzyMatch)
            {
                var allItemTokens = new HashSet<string>(textTokens, StringComparer.OrdinalIgnoreCase);
                foreach (var t in titleTokens) allItemTokens.Add(t);

                foreach (var qt in expandedQueryTokens)
                {
                    if (vocabIndex.TryGetValue(qt, out var qIdx))
                    {
                        foreach (var dt in allItemTokens)
                        {
                            if (!string.Equals(qt, dt, StringComparison.OrdinalIgnoreCase) &&
                                TextProcessor.IsFuzzyMatch(qt, dt))
                            {
                                // Add a fraction of the IDF as fuzzy boost
                                docVec[qIdx] += 0.5 * idf[qIdx];
                            }
                        }
                    }
                }
            }

            // Cosine similarity
            double score = CosineSimilarity(queryVec, docVec);

            if (score >= minScore)
            {
                // Determine matched terms
                var matchedTerms = new List<string>();
                var allDocTerms = new HashSet<string>(textTokens, StringComparer.OrdinalIgnoreCase);
                foreach (var t in titleTokens) allDocTerms.Add(t);

                foreach (var qt in queryTokens)
                {
                    if (allDocTerms.Contains(qt))
                    {
                        matchedTerms.Add(qt);
                    }
                    else if (synonymExpansion)
                    {
                        var expanded = TextProcessor.ExpandWithSynonyms(new[] { qt });
                        if (expanded.Any(e => allDocTerms.Contains(e)))
                            matchedTerms.Add(qt + " (synonym)");
                    }

                    if (fuzzyMatch && !matchedTerms.Any(m => m.StartsWith(qt)))
                    {
                        foreach (var dt in allDocTerms)
                        {
                            if (TextProcessor.IsFuzzyMatch(qt, dt))
                            {
                                matchedTerms.Add(qt + " (fuzzy)");
                                break;
                            }
                        }
                    }
                }

                results.Add(new SearchResult(
                    Id: item.Id,
                    Score: Math.Round(score, 6),
                    Title: item.Title,
                    Category: item.Category,
                    MatchedTerms: matchedTerms.Distinct().ToArray(),
                    Metadata: item.Metadata));
            }
        }

        // Sort by score descending, take TopK
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (results.Count > topK)
            results = results.Take(topK).ToList();

        sw.Stop();

        return new SemanticSearchResponse(
            Query: request.Query,
            NormalizedQuery: normalizedQuery,
            TotalItems: request.Items.Length,
            MatchedItems: results.Count,
            Results: results.ToArray(),
            SearchDurationMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2));
    }

    private static double[] ComputeTermFrequency(string[] tokens, string[] bigrams, Dictionary<string, int> vocabIndex)
    {
        var tf = new double[vocabIndex.Count];
        var totalTerms = tokens.Length + bigrams.Length;
        if (totalTerms == 0) return tf;

        foreach (var token in tokens)
        {
            if (vocabIndex.TryGetValue(token, out var idx))
                tf[idx] += 1.0 / totalTerms;
        }

        foreach (var bigram in bigrams)
        {
            if (vocabIndex.TryGetValue(bigram, out var idx))
                tf[idx] += 1.0 / totalTerms;
        }

        return tf;
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        double dot = 0, magA = 0, magB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

// DTOs
public record SemanticSearchRequest(
    string Query,
    CatalogItem[] Items,
    int? TopK = null,
    double? MinScore = null,
    bool? FuzzyMatch = null,
    bool? SynonymExpansion = null,
    double? TitleWeight = null
);

public record CatalogItem(
    string Id,
    string Text,
    string? Title = null,
    string? Category = null,
    Dictionary<string, string>? Metadata = null
);

public record SemanticSearchResponse(
    string Query,
    string NormalizedQuery,
    int TotalItems,
    int MatchedItems,
    SearchResult[] Results,
    double SearchDurationMs
);

public record SearchResult(
    string Id,
    double Score,
    string? Title,
    string? Category,
    string[] MatchedTerms,
    Dictionary<string, string>? Metadata
);
