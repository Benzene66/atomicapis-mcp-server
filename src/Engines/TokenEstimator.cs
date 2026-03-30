namespace TokenCounter;

public static class TokenEstimator
{
    private static readonly Dictionary<string, ModelInfo> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-4o family (o200k_base)
        ["gpt-4o"] = new("o200k_base", 128_000, 2.50m, 10.00m),
        ["gpt-4o-mini"] = new("o200k_base", 128_000, 0.15m, 0.60m),

        // GPT-4 Turbo (cl100k_base)
        ["gpt-4-turbo"] = new("cl100k_base", 128_000, 10.00m, 30.00m),

        // GPT-3.5 Turbo (cl100k_base)
        ["gpt-3.5-turbo"] = new("cl100k_base", 16_385, 0.50m, 1.50m),

        // Claude models
        ["claude-sonnet-4-5"] = new("claude", 200_000, 3.00m, 15.00m),
        ["claude-opus-4"] = new("claude", 200_000, 15.00m, 75.00m),
        ["claude-haiku-3.5"] = new("claude", 200_000, 0.80m, 4.00m),

        // Gemini models
        ["gemini-2.0-flash"] = new("gemini", 1_048_576, 0.10m, 0.40m),
        ["gemini-1.5-pro"] = new("gemini", 2_097_152, 1.25m, 5.00m),

        // Llama models
        ["llama-3.1-70b"] = new("llama", 128_000, 0.35m, 0.40m),
        ["llama-3.1-8b"] = new("llama", 128_000, 0.05m, 0.08m),

        // Mistral models
        ["mistral-large"] = new("mistral", 128_000, 2.00m, 6.00m),
        ["mistral-small"] = new("mistral", 128_000, 0.20m, 0.60m),

        // o1/o3 reasoning models (o200k_base)
        ["o1"] = new("o200k_base", 200_000, 15.00m, 60.00m),
        ["o3"] = new("o200k_base", 200_000, 10.00m, 40.00m),
    };

    private static readonly Dictionary<string, double> CharsPerToken = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cl100k_base"] = 3.7,
        ["o200k_base"] = 3.9,
        ["claude"] = 3.5,
        ["gemini"] = 4.0,
        ["llama"] = 4.0,
        ["mistral"] = 4.0,
    };

    private const double DefaultCharsPerToken = 4.0;
    private const int DefaultContextWindow = 128_000;
    private const double WordsToTokenRatio = 0.75;

    public static TokenResult Estimate(string text, string model)
    {
        if (string.IsNullOrEmpty(model))
            model = "gpt-4o";

        var hasModel = Models.TryGetValue(model, out var modelInfo);
        if (!hasModel)
        {
            modelInfo = new ModelInfo("unknown", DefaultContextWindow, 0m, 0m);
        }

        var family = modelInfo!.Family;
        var charsPerToken = CharsPerToken.GetValueOrDefault(family, DefaultCharsPerToken);

        // Method 1: character-based estimation
        var charBasedTokens = (int)Math.Ceiling(text.Length / charsPerToken);

        // Method 2: word-based estimation (cross-check)
        var wordCount = CountWords(text);
        var wordBasedTokens = (int)Math.Ceiling(wordCount / WordsToTokenRatio);

        // Average both methods
        var estimatedTokens = (int)Math.Ceiling((charBasedTokens + wordBasedTokens) / 2.0);

        // Ensure at least 1 token for non-empty text
        if (estimatedTokens < 1 && text.Length > 0)
            estimatedTokens = 1;

        var contextWindow = modelInfo.ContextWindow;
        var contextUsagePercent = Math.Round((double)estimatedTokens / contextWindow * 100, 2);
        var exceedsContext = estimatedTokens > contextWindow;

        var estimatedInputCost = Math.Round(estimatedTokens * modelInfo.InputPricePerMillion / 1_000_000m, 6);
        var estimatedOutputCost = Math.Round(estimatedTokens * modelInfo.OutputPricePerMillion / 1_000_000m, 6);

        return new TokenResult(
            EstimatedTokens: estimatedTokens,
            Model: model,
            TokenizerFamily: family,
            EstimatedInputCost: estimatedInputCost,
            EstimatedOutputCost: estimatedOutputCost,
            ContextWindowSize: contextWindow,
            ContextUsagePercent: contextUsagePercent,
            ExceedsContext: exceedsContext,
            IsExactCount: false,
            Method: "character_ratio_estimation"
        );
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var count = 0;
        var inWord = false;

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }
}

public record ModelInfo(
    string Family,
    int ContextWindow,
    decimal InputPricePerMillion,
    decimal OutputPricePerMillion);

public record TokenResult(
    int EstimatedTokens,
    string Model,
    string TokenizerFamily,
    decimal EstimatedInputCost,
    decimal EstimatedOutputCost,
    int ContextWindowSize,
    double ContextUsagePercent,
    bool ExceedsContext,
    bool IsExactCount,
    string Method);
