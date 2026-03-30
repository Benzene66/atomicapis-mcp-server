using System.ComponentModel;
using System.Text.Json;
using RelatedContent;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class RelatedContentTools
{
    [McpServerTool, Description(
        "Finds related content from a corpus based on similarity to a reference item. " +
        "Supports TF-IDF cosine, Jaccard, and combined algorithms with category boosting and tag overlap. " +
        "Reference and corpus items are provided as JSON objects with 'id', 'text', and optional 'title'/'category'/'tags' fields.")]
    public static string FindRelatedContent(
        [Description("JSON object for the reference item (e.g. {\"id\":\"1\",\"text\":\"machine learning tutorial\",\"category\":\"AI\"})")] string referenceItemJson,
        [Description("JSON array of corpus items (e.g. [{\"id\":\"2\",\"text\":\"deep learning guide\"}])")] string corpusJson,
        [Description("Algorithm: 'tfidf', 'jaccard', or 'combined' (default: 'combined')")] string algorithm = "combined",
        [Description("Minimum similarity score 0-1 (default: 0.1)")] double similarityThreshold = 0.1,
        [Description("Maximum number of results (default: 10)")] int maxResults = 10)
    {
        ContentItem referenceItem;
        ContentItem[] corpus;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            referenceItem = JsonSerializer.Deserialize<ContentItem>(referenceItemJson, opts)!;
            corpus = JsonSerializer.Deserialize<ContentItem[]>(corpusJson, opts) ?? Array.Empty<ContentItem>();
        }
        catch
        {
            return "Error: Could not parse JSON. Provide reference as an object and corpus as an array, both with 'id' and 'text' fields.";
        }

        if (corpus.Length == 0)
            return "Error: Corpus is empty.";

        var request = new RelatedContentRequest(referenceItem, corpus, similarityThreshold, maxResults, algorithm);
        var result = RelatedContentEngine.FindRelated(request);

        var items = string.Join("\n", result.RelatedItems.Select(r =>
            $"  {r.Id} (score: {r.Score:F4}) — {r.Title ?? "(no title)"} [{string.Join(", ", r.SharedTerms.Take(5))}]"));

        return $"""
            Reference: {result.ReferenceId}
            Algorithm: {algorithm}
            Related: {result.RelatedCount}/{result.CorpusSize} items above threshold
            Duration: {result.ProcessingTimeMs}ms

            Results:
            {(items.Length > 0 ? items : "  (no matches)")}
            """;
    }
}
