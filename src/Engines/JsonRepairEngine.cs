using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JsonRepair;

public static class JsonRepairEngine
{
    public static RepairResult Repair(string input, string? jsonSchema = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new RepairResult(
                RepairedJson: string.Empty,
                WasModified: false,
                RepairsApplied: [],
                SchemaValid: null,
                SchemaErrors: []);
        }

        var repairs = new List<string>();
        var text = input;

        // 1. Strip markdown code fences
        text = StripMarkdownFences(text, repairs);

        // 2. Extract JSON from surrounding conversational text
        text = ExtractJsonFromText(text, repairs);

        // 3. Remove comments (// and /* */)
        text = RemoveComments(text, repairs);

        // 4. Remove control characters
        text = RemoveControlCharacters(text, repairs);

        // 5. Fix single-quoted strings to double-quoted
        text = FixSingleQuotes(text, repairs);

        // 6. Remove trailing commas before } or ]
        text = RemoveTrailingCommas(text, repairs);

        // 7. Balance unclosed brackets/braces
        text = BalanceBrackets(text, repairs);

        text = text.Trim();

        bool wasModified = text != input.Trim();

        // Schema validation
        bool? schemaValid = null;
        var schemaErrors = new List<string>();

        if (jsonSchema != null)
        {
            ValidateSchema(text, jsonSchema, out schemaValid, schemaErrors);
        }

        return new RepairResult(
            RepairedJson: text,
            WasModified: wasModified,
            RepairsApplied: repairs,
            SchemaValid: schemaValid,
            SchemaErrors: schemaErrors);
    }

    internal static string StripMarkdownFences(string text, List<string> repairs)
    {
        // Match ```json ... ``` or ``` ... ```
        var fencePattern = new Regex(@"```(?:json|JSON)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.None);
        var match = fencePattern.Match(text);
        if (match.Success)
        {
            repairs.Add("Stripped markdown code fences");
            return match.Groups[1].Value;
        }
        return text;
    }

    internal static string ExtractJsonFromText(string text, List<string> repairs)
    {
        var trimmed = text.Trim();

        // If already starts with { or [, no extraction needed
        if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
            return trimmed;

        // Find first { or [
        int firstBrace = text.IndexOf('{');
        int firstBracket = text.IndexOf('[');

        int start;
        char closeChar;

        if (firstBrace == -1 && firstBracket == -1)
            return text;

        if (firstBrace == -1)
        {
            start = firstBracket;
            closeChar = ']';
        }
        else if (firstBracket == -1)
        {
            start = firstBrace;
            closeChar = '}';
        }
        else if (firstBrace < firstBracket)
        {
            start = firstBrace;
            closeChar = '}';
        }
        else
        {
            start = firstBracket;
            closeChar = ']';
        }

        // Find the last matching close character
        int lastClose = text.LastIndexOf(closeChar);
        if (lastClose > start)
        {
            repairs.Add("Extracted JSON from surrounding text");
            return text.Substring(start, lastClose - start + 1);
        }

        // If no matching close, take from start to end
        repairs.Add("Extracted JSON from surrounding text");
        return text.Substring(start);
    }

    internal static string RemoveComments(string text, List<string> repairs)
    {
        bool hasLineComments = text.Contains("//");
        bool hasBlockComments = text.Contains("/*");

        if (!hasLineComments && !hasBlockComments)
            return text;

        var sb = new StringBuilder(text.Length);
        bool inString = false;
        bool modified = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Track string boundaries
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inString = !inString;
                sb.Append(c);
                continue;
            }

            if (inString)
            {
                sb.Append(c);
                continue;
            }

            // Check for line comment
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                modified = true;
                // Skip until end of line
                while (i < text.Length && text[i] != '\n')
                    i++;
                if (i < text.Length)
                    sb.Append('\n');
                continue;
            }

            // Check for block comment
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                modified = true;
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                    i++;
                i++; // skip closing /
                continue;
            }

            sb.Append(c);
        }

        if (modified)
        {
            repairs.Add("Removed comments");
        }

        return sb.ToString();
    }

    internal static string RemoveControlCharacters(string text, List<string> repairs)
    {
        var sb = new StringBuilder(text.Length);
        bool modified = false;
        bool inString = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inString = !inString;
                sb.Append(c);
                continue;
            }

            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                modified = true;
                continue;
            }

            sb.Append(c);
        }

        if (modified)
        {
            repairs.Add("Removed control characters");
        }

        return sb.ToString();
    }

    internal static string FixSingleQuotes(string text, List<string> repairs)
    {
        // Only fix if the text looks like it uses single quotes for JSON strings
        // Check if there are single-quoted keys/values pattern
        if (!Regex.IsMatch(text, @"'[^']*'\s*:") && !Regex.IsMatch(text, @":\s*'[^']*'"))
            return text;

        var sb = new StringBuilder(text.Length);
        bool inDoubleString = false;
        bool modified = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Track double-quoted strings (don't modify inside them)
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inDoubleString = !inDoubleString;
                sb.Append(c);
                continue;
            }

            if (inDoubleString)
            {
                sb.Append(c);
                continue;
            }

            if (c == '\'')
            {
                sb.Append('"');
                modified = true;
                continue;
            }

            sb.Append(c);
        }

        if (modified)
        {
            repairs.Add("Fixed single-quoted strings to double-quoted");
        }

        return sb.ToString();
    }

    internal static string RemoveTrailingCommas(string text, List<string> repairs)
    {
        var pattern = new Regex(@",\s*([}\]])", RegexOptions.None);
        if (pattern.IsMatch(text))
        {
            repairs.Add("Removed trailing commas");
            return pattern.Replace(text, "$1");
        }
        return text;
    }

    internal static string BalanceBrackets(string text, List<string> repairs)
    {
        var stack = new Stack<char>();
        bool inString = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{') stack.Push('}');
            else if (c == '[') stack.Push(']');
            else if (c == '}' || c == ']')
            {
                if (stack.Count > 0 && stack.Peek() == c)
                    stack.Pop();
            }
        }

        if (stack.Count > 0)
        {
            var sb = new StringBuilder(text);
            while (stack.Count > 0)
            {
                sb.Append(stack.Pop());
            }
            repairs.Add("Balanced unclosed brackets/braces");
            return sb.ToString();
        }

        return text;
    }

    private static void ValidateSchema(string json, string schemaJson, out bool? schemaValid, List<string> schemaErrors)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(json);
            using var schemaDoc = JsonDocument.Parse(schemaJson);

            var schema = schemaDoc.RootElement;
            var data = jsonDoc.RootElement;

            schemaValid = ValidateElement(data, schema, "$", schemaErrors);
        }
        catch (JsonException ex)
        {
            schemaValid = false;
            schemaErrors.Add($"Failed to parse JSON: {ex.Message}");
        }
    }

    private static bool ValidateElement(JsonElement data, JsonElement schema, string path, List<string> errors)
    {
        bool valid = true;

        // Check "type"
        if (schema.TryGetProperty("type", out var typeEl))
        {
            var expectedType = typeEl.GetString();
            bool typeMatch = expectedType switch
            {
                "object" => data.ValueKind == JsonValueKind.Object,
                "array" => data.ValueKind == JsonValueKind.Array,
                "string" => data.ValueKind == JsonValueKind.String,
                "number" => data.ValueKind == JsonValueKind.Number,
                "integer" => data.ValueKind == JsonValueKind.Number,
                "boolean" => data.ValueKind == JsonValueKind.True || data.ValueKind == JsonValueKind.False,
                "null" => data.ValueKind == JsonValueKind.Null,
                _ => true
            };

            if (!typeMatch)
            {
                errors.Add($"{path}: Expected type '{expectedType}' but got '{data.ValueKind}'");
                return false;
            }
        }

        // Check "required" for objects
        if (data.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var requiredEl))
        {
            foreach (var req in requiredEl.EnumerateArray())
            {
                var fieldName = req.GetString()!;
                if (!data.TryGetProperty(fieldName, out _))
                {
                    errors.Add($"{path}: Missing required field '{fieldName}'");
                    valid = false;
                }
            }
        }

        // Check "properties" for objects
        if (data.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var propsEl))
        {
            foreach (var prop in propsEl.EnumerateObject())
            {
                if (data.TryGetProperty(prop.Name, out var dataValue))
                {
                    if (!ValidateElement(dataValue, prop.Value, $"{path}.{prop.Name}", errors))
                    {
                        valid = false;
                    }
                }
            }
        }

        // Check "items" for arrays
        if (data.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var itemsEl))
        {
            int index = 0;
            foreach (var item in data.EnumerateArray())
            {
                if (!ValidateElement(item, itemsEl, $"{path}[{index}]", errors))
                {
                    valid = false;
                }
                index++;
            }
        }

        return valid;
    }
}

public record RepairResult(
    string RepairedJson,
    bool WasModified,
    List<string> RepairsApplied,
    bool? SchemaValid,
    List<string> SchemaErrors);
