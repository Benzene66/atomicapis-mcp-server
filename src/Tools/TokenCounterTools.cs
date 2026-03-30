using System.ComponentModel;
using TokenCounter;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class TokenCounterTools
{
    [McpServerTool, Description(
        "Estimates LLM token count for a given text and model. Supports GPT-4/4o, GPT-3.5, " +
        "Claude 3/3.5/4, Gemini, Llama, Mistral, and more. Returns estimated tokens, cost, " +
        "context window usage, and whether the text exceeds the model's context limit.")]
    public static string CountTokens(
        [Description("The text to count tokens for")] string text,
        [Description("The model name (e.g. 'gpt-4o', 'claude-3-sonnet', 'gemini-pro')")] string model = "gpt-4o")
    {
        var result = TokenEstimator.Estimate(text, model);

        return $"""
            Model: {result.Model}
            Tokenizer family: {result.TokenizerFamily}
            Estimated tokens: {result.EstimatedTokens}
            Context window: {result.ContextWindowSize}
            Context usage: {result.ContextUsagePercent:F1}%
            Exceeds context: {result.ExceedsContext}
            Estimated input cost: ${result.EstimatedInputCost:F6}
            Estimated output cost: ${result.EstimatedOutputCost:F6}
            Method: {result.Method}
            """;
    }
}
