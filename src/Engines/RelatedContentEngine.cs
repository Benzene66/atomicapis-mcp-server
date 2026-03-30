using System.Diagnostics;

namespace RelatedContent;

public static class RelatedContentEngine
{
    public static RelatedContentResponse FindRelated(RelatedContentRequest request)
    {
        var sw = Stopwatch.StartNew();

        double threshold = Math.Clamp(request.SimilarityThreshold ?? 0.1, 0.0, 1.0);
        int maxResults = Math.Clamp(request.MaxResults ?? 20, 1, 100);
        string algorithm = (request.Algorithm ?? "tfidf").ToLowerInvariant();
        double categoryBoost = Math.Clamp(request.CategoryBoost ?? 0.1, 0.0, 1.0);
        bool includeBreakdown = request.IncludeScoreBreakdown ?? false;

        // Tokenize all documents
        var refTokens = SimilarityCalculator.Tokenize(
            CombineText(request.ReferenceItem));

        var corpusTokens = new string[request.Corpus.Length][];
        for (int i = 0; i < request.Corpus.Length; i++)
        {
            corpusTokens[i] = SimilarityCalculator.Tokenize(
                CombineText(request.Corpus[i]));
        }

        // Build TF-IDF vectors for tfidf and combined algorithms
        Dictionary<string, double>? refTfidfVector = null;
        Dictionary<string, double>[]? corpusTfidfVectors = null;

        if (algorithm is "tfidf" or "combined")
        {
            // All documents for IDF calculation
            var allTokenSets = new string[request.Corpus.Length + 1][];
            allTokenSets[0] = refTokens;
            Array.Copy(corpusTokens, 0, allTokenSets, 1, corpusTokens.Length);

            var idf = SimilarityCalculator.ComputeIdf(allTokenSets);

            var refTf = SimilarityCalculator.ComputeTf(refTokens);
            refTfidfVector = SimilarityCalculator.BuildTfidfVector(refTf, idf);

            corpusTfidfVectors = new Dictionary<string, double>[request.Corpus.Length];
            for (int i = 0; i < request.Corpus.Length; i++)
            {
                var tf = SimilarityCalculator.ComputeTf(corpusTokens[i]);
                corpusTfidfVectors[i] = SimilarityCalculator.BuildTfidfVector(tf, idf);
            }
        }

        // Score each corpus item
        var results = new List<RelatedItem>();

        for (int i = 0; i < request.Corpus.Length; i++)
        {
            var candidate = request.Corpus[i];

            double tfidfScore = 0;
            double jaccardScore = 0;

            if (algorithm is "tfidf" or "combined")
            {
                tfidfScore = SimilarityCalculator.CosineSimilarity(
                    refTfidfVector!, corpusTfidfVectors![i]);
            }

            if (algorithm is "jaccard" or "combined")
            {
                jaccardScore = SimilarityCalculator.JaccardSimilarity(
                    refTokens, corpusTokens[i]);
            }

            // Compute base score based on algorithm
            double baseScore = algorithm switch
            {
                "tfidf" => tfidfScore,
                "jaccard" => jaccardScore,
                "combined" => (tfidfScore * 0.7) + (jaccardScore * 0.3),
                _ => tfidfScore
            };

            // Category boost
            double catBonus = 0;
            if (!string.IsNullOrEmpty(request.ReferenceItem.Category) &&
                !string.IsNullOrEmpty(candidate.Category) &&
                string.Equals(request.ReferenceItem.Category, candidate.Category, StringComparison.OrdinalIgnoreCase))
            {
                catBonus = categoryBoost;
            }

            // Tag overlap bonus
            double tagBonus = 0;
            int sharedTagCount = 0;
            if (request.ReferenceItem.Tags != null && candidate.Tags != null &&
                request.ReferenceItem.Tags.Length > 0 && candidate.Tags.Length > 0)
            {
                var refTags = new HashSet<string>(request.ReferenceItem.Tags, StringComparer.OrdinalIgnoreCase);
                sharedTagCount = candidate.Tags.Count(t => refTags.Contains(t));
                int totalUniqueTags = refTags.Count + candidate.Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count(t => !refTags.Contains(t));
                if (totalUniqueTags > 0)
                    tagBonus = (double)sharedTagCount / totalUniqueTags * 0.1;
            }

            double finalScore = Math.Min(baseScore + catBonus + tagBonus, 1.0);

            if (finalScore >= threshold)
            {
                var sharedTerms = SimilarityCalculator.FindSharedTerms(refTokens, corpusTokens[i]);

                ScoreBreakdown? breakdown = includeBreakdown
                    ? new ScoreBreakdown(tfidfScore, jaccardScore, catBonus, tagBonus)
                    : null;

                results.Add(new RelatedItem(
                    Id: candidate.Id,
                    Score: Math.Round(finalScore, 6),
                    Title: candidate.Title,
                    Category: candidate.Category,
                    Tags: candidate.Tags,
                    SharedTerms: sharedTerms,
                    SharedTagCount: sharedTagCount,
                    Breakdown: breakdown,
                    Metadata: candidate.Metadata
                ));
            }
        }

        // Sort by score descending, then take max
        var sorted = results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Id)
            .Take(maxResults)
            .ToArray();

        sw.Stop();

        return new RelatedContentResponse(
            ReferenceId: request.ReferenceItem.Id,
            CorpusSize: request.Corpus.Length,
            RelatedCount: sorted.Length,
            RelatedItems: sorted,
            ProcessingTimeMs: Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        );
    }

    private static string CombineText(ContentItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Title))
            parts.Add(item.Title);
        if (!string.IsNullOrWhiteSpace(item.Text))
            parts.Add(item.Text);
        return string.Join(" ", parts);
    }
}

// DTOs
public record RelatedContentRequest(
    ContentItem ReferenceItem,
    ContentItem[] Corpus,
    double? SimilarityThreshold = null,
    int? MaxResults = null,
    string? Algorithm = null,
    double? CategoryBoost = null,
    bool? IncludeScoreBreakdown = null
);

public record ContentItem(
    string Id,
    string Text,
    string? Title = null,
    string? Category = null,
    string[]? Tags = null,
    Dictionary<string, string>? Metadata = null
);

public record RelatedContentResponse(
    string ReferenceId,
    int CorpusSize,
    int RelatedCount,
    RelatedItem[] RelatedItems,
    double ProcessingTimeMs
);

public record RelatedItem(
    string Id,
    double Score,
    string? Title,
    string? Category,
    string[]? Tags,
    string[] SharedTerms,
    int SharedTagCount,
    ScoreBreakdown? Breakdown,
    Dictionary<string, string>? Metadata
);

public record ScoreBreakdown(
    double TfidfScore,
    double JaccardScore,
    double CategoryBonus,
    double TagBonus
);
