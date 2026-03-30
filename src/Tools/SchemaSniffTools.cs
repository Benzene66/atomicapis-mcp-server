using System.ComponentModel;
using SchemaSniff;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class SchemaSniffTools
{
    [McpServerTool, Description(
        "Auto-detects the schema of a JSON, XML, or CSV payload and returns a JSON Schema (draft-07). " +
        "Handles nested objects, arrays, mixed types, and detects string formats " +
        "(date-time, email, uuid, uri). For CSV, auto-detects the delimiter and infers column types.")]
    public static string InferSchema(
        [Description("The JSON, XML, or CSV payload to analyze")] string payload,
        [Description("Format hint: 'json', 'xml', or 'csv'. Auto-detected if omitted.")] string? formatHint = null)
    {
        var result = SchemaInferrer.Infer(payload, formatHint);

        return $"""
            Detected format: {result.DetectedFormat}
            Field count: {result.FieldCount}

            JSON Schema:
            {result.Schema.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
            """;
    }
}
