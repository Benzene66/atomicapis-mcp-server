using System.ComponentModel;
using JsonRepair;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class JsonRepairTools
{
    [McpServerTool, Description(
        "Repairs broken or malformed JSON: fixes trailing commas, adds missing brackets/braces, " +
        "wraps unquoted keys, converts single quotes to double quotes, strips comments/trailing text, " +
        "and optionally validates against a JSON Schema.")]
    public static string RepairJson(
        [Description("The broken or malformed JSON string to repair")] string json,
        [Description("Optional JSON Schema to validate the repaired output against")] string? jsonSchema = null)
    {
        var result = JsonRepairEngine.Repair(json, jsonSchema);

        var repairs = result.RepairsApplied.Count > 0
            ? string.Join(", ", result.RepairsApplied)
            : "none";

        var schemaInfo = result.SchemaValid.HasValue
            ? $"\nSchema valid: {result.SchemaValid}" +
              (result.SchemaErrors.Count > 0 ? $"\nSchema errors: {string.Join(", ", result.SchemaErrors)}" : "")
            : "";

        return $"""
            Repaired JSON:
            {result.RepairedJson}

            Was modified: {result.WasModified}
            Repairs applied: {repairs}{schemaInfo}
            """;
    }
}
