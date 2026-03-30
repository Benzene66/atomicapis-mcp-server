using System.ComponentModel;
using System.Text.Json;
using SemanticSearchRedirector;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class SemanticSearchTools
{
    [McpServerTool, Description(
        "Maps a user query to the best-matching items in a catalog using TF-IDF vector similarity. " +
        "Supports synonym expansion, fuzzy matching (Levenshtein), and title weighting. " +
        "Items are provided as a JSON array of objects with 'id', 'text', and optional 'title'/'category' fields.")]
    public static string SemanticSearch(
        [Description("The search query (e.g. 'lightweight running shoes')")] string query,
        [Description("JSON array of catalog items, each with 'id' and 'text' fields (e.g. [{\"id\":\"1\",\"text\":\"blue running shoes\"}])")] string itemsJson,
        [Description("Maximum number of results to return (default: 10)")] int topK = 10,
        [Description("Minimum similarity score 0-1 (default: 0.01)")] double minScore = 0.01)
    {
        CatalogItem[] items;
        try
        {
            items = JsonSerializer.Deserialize<CatalogItem[]>(itemsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<CatalogItem>();
        }
        catch
        {
            return "Error: Could not parse itemsJson. Provide a JSON array of objects with 'id' and 'text' fields.";
        }

        if (items.Length == 0)
            return "Error: No items provided.";

        var request = new SemanticSearchRequest(query, items, topK, minScore);
        var result = SemanticSearchEngine.Search(request);

        var matches = string.Join("\n", result.Results.Select(r =>
            $"  {r.Id} (score: {r.Score:F4}) — {r.Title ?? "(no title)"} [{string.Join(", ", r.MatchedTerms)}]"));

        return $"""
            Query: {result.Query}
            Normalized: {result.NormalizedQuery}
            Matched: {result.MatchedItems}/{result.TotalItems} items
            Duration: {result.SearchDurationMs}ms

            Results:
            {(matches.Length > 0 ? matches : "  (no matches)")}
            """;
    }
}
