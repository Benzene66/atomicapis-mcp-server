using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;

namespace SchemaSniff;

public static partial class SchemaInferrer
{
    private const int MaxPayloadSize = 2 * 1024 * 1024; // 2MB

    public static SchemaResult Infer(string payload, string? formatHint = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty.");

        if (payload.Length > MaxPayloadSize)
            throw new ArgumentException("Payload exceeds the 2MB size limit.");

        var format = formatHint is null or "auto"
            ? FormatDetector.Detect(payload)
            : formatHint.ToLowerInvariant();

        JsonObject schema = format switch
        {
            "json" => InferFromJson(payload),
            "xml" => InferFromXml(payload),
            "csv" => InferFromCsv(payload),
            _ => throw new ArgumentException($"Could not detect format. Supported: json, xml, csv.")
        };

        // Add JSON Schema metadata
        schema["$schema"] = "http://json-schema.org/draft-07/schema#";

        var fieldCount = CountFields(schema);

        return new SchemaResult(format, fieldCount, schema);
    }

    // --- JSON inference ---

    private static JsonObject InferFromJson(string payload)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON: {ex.Message}");
        }

        return InferJsonNode(node);
    }

    private static JsonObject InferJsonNode(JsonNode? node)
    {
        if (node is null)
            return new JsonObject { ["type"] = "null" };

        return node switch
        {
            JsonObject obj => InferJsonObject(obj),
            JsonArray arr => InferJsonArray(arr),
            JsonValue val => InferJsonValue(val),
            _ => new JsonObject { ["type"] = "null" }
        };
    }

    private static JsonObject InferJsonObject(JsonObject obj)
    {
        var schema = new JsonObject
        {
            ["type"] = "object"
        };

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var kvp in obj)
        {
            properties[kvp.Key] = InferJsonNode(kvp.Value);
            required.Add(JsonValue.Create(kvp.Key));
        }

        if (properties.Count > 0)
        {
            schema["properties"] = properties;
            schema["required"] = required;
        }

        return schema;
    }

    private static JsonObject InferJsonArray(JsonArray arr)
    {
        var schema = new JsonObject
        {
            ["type"] = "array"
        };

        if (arr.Count == 0)
        {
            schema["items"] = new JsonObject();
            return schema;
        }

        // Merge schemas from all array elements for a comprehensive items schema
        var itemSchemas = arr.Select(InferJsonNode).ToList();
        schema["items"] = MergeSchemas(itemSchemas);

        return schema;
    }

    private static JsonObject InferJsonValue(JsonValue val)
    {
        var element = val.GetValue<JsonElement>();

        return element.ValueKind switch
        {
            JsonValueKind.String => InferStringSchema(element.GetString()!),
            JsonValueKind.Number => element.TryGetInt64(out _)
                ? new JsonObject { ["type"] = "integer" }
                : new JsonObject { ["type"] = "number" },
            JsonValueKind.True or JsonValueKind.False => new JsonObject { ["type"] = "boolean" },
            JsonValueKind.Null => new JsonObject { ["type"] = "null" },
            _ => new JsonObject { ["type"] = "string" }
        };
    }

    private static JsonObject InferStringSchema(string value)
    {
        var schema = new JsonObject { ["type"] = "string" };

        // Detect common string formats
        if (DateTimeRegex().IsMatch(value))
            schema["format"] = "date-time";
        else if (DateRegex().IsMatch(value))
            schema["format"] = "date";
        else if (EmailRegex().IsMatch(value))
            schema["format"] = "email";
        else if (UuidRegex().IsMatch(value))
            schema["format"] = "uuid";
        else if (UriRegex().IsMatch(value))
            schema["format"] = "uri";

        return schema;
    }

    /// <summary>
    /// Merges multiple schemas into one. If all are the same type, returns that type.
    /// If mixed, returns anyOf.
    /// </summary>
    private static JsonObject MergeSchemas(List<JsonObject> schemas)
    {
        if (schemas.Count == 0)
            return new JsonObject();

        if (schemas.Count == 1)
            return CloneJsonObject(schemas[0]);

        // Check if all schemas are the same type
        var types = schemas
            .Select(s => s["type"]?.GetValue<string>())
            .Distinct()
            .ToList();

        if (types.Count == 1 && types[0] == "object")
        {
            // Merge object schemas: union of all properties
            return MergeObjectSchemas(schemas);
        }

        if (types.Count == 1)
        {
            // All same primitive type — return first
            return CloneJsonObject(schemas[0]);
        }

        // Mixed types — use the most common one (simplification)
        var mostCommon = types
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        return CloneJsonObject(schemas.First(s => s["type"]?.GetValue<string>() == mostCommon));
    }

    private static JsonObject MergeObjectSchemas(List<JsonObject> schemas)
    {
        var merged = new JsonObject { ["type"] = "object" };
        var allProperties = new JsonObject();
        var requiredSet = new HashSet<string>();
        var allKeys = new HashSet<string>();

        var first = true;
        foreach (var schema in schemas)
        {
            var props = schema["properties"]?.AsObject();
            if (props is null) continue;

            var currentKeys = new HashSet<string>();
            foreach (var kvp in props)
            {
                currentKeys.Add(kvp.Key);
                allKeys.Add(kvp.Key);
                if (!allProperties.ContainsKey(kvp.Key) && kvp.Value is not null)
                {
                    allProperties[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                }
            }

            if (first)
            {
                requiredSet.UnionWith(currentKeys);
                first = false;
            }
            else
            {
                // Required = intersection (present in ALL objects)
                requiredSet.IntersectWith(currentKeys);
            }
        }

        if (allProperties.Count > 0)
            merged["properties"] = allProperties;

        if (requiredSet.Count > 0)
        {
            var required = new JsonArray();
            foreach (var key in requiredSet)
                required.Add(JsonValue.Create(key));
            merged["required"] = required;
        }

        return merged;
    }

    // --- XML inference ---

    private static JsonObject InferFromXml(string payload)
    {
        var doc = new XmlDocument();
        try
        {
            doc.LoadXml(payload);
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"Invalid XML: {ex.Message}");
        }

        var root = doc.DocumentElement;
        if (root is null)
            throw new ArgumentException("XML document has no root element.");

        var schema = new JsonObject
        {
            ["type"] = "object"
        };

        var properties = new JsonObject
        {
            [root.Name] = InferXmlElement(root)
        };

        schema["properties"] = properties;
        schema["required"] = new JsonArray(JsonValue.Create(root.Name));

        return schema;
    }

    private static JsonObject InferXmlElement(XmlElement element)
    {
        var hasChildren = element.ChildNodes.OfType<XmlElement>().Any();
        var hasAttributes = element.Attributes?.Count > 0;
        var hasText = element.ChildNodes.OfType<XmlText>().Any() ||
                      element.ChildNodes.OfType<XmlCDataSection>().Any();

        if (!hasChildren && !hasAttributes)
        {
            // Leaf node — infer type from text content
            return InferValueType(element.InnerText.Trim());
        }

        var schema = new JsonObject { ["type"] = "object" };
        var properties = new JsonObject();
        var required = new JsonArray();

        // Add attributes as properties (prefixed with @)
        if (element.Attributes != null)
        {
            foreach (XmlAttribute attr in element.Attributes)
            {
                properties[$"@{attr.Name}"] = InferValueType(attr.Value);
                required.Add(JsonValue.Create($"@{attr.Name}"));
            }
        }

        // Add text content if mixed with children
        if (hasText && hasChildren)
        {
            properties["#text"] = new JsonObject { ["type"] = "string" };
        }

        // Group child elements by name to detect arrays
        var childGroups = element.ChildNodes.OfType<XmlElement>()
            .GroupBy(e => e.Name)
            .ToList();

        foreach (var group in childGroups)
        {
            var elements = group.ToList();
            if (elements.Count > 1)
            {
                // Array of elements
                var itemSchemas = elements.Select(InferXmlElement).ToList();
                properties[group.Key] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = MergeSchemas(itemSchemas)
                };
            }
            else
            {
                properties[group.Key] = InferXmlElement(elements[0]);
            }
            required.Add(JsonValue.Create(group.Key));
        }

        if (properties.Count > 0)
            schema["properties"] = properties;
        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    // --- CSV inference ---

    private static JsonObject InferFromCsv(string payload)
    {
        var delimiter = FormatDetector.DetectDelimiter(payload);
        var lines = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            throw new ArgumentException("CSV must have at least a header row and one data row.");

        var headers = ParseCsvLine(lines[0], delimiter);
        if (headers.Length == 0)
            throw new ArgumentException("Could not parse CSV headers.");

        // Sample data rows (up to 100) to infer column types
        var sampleCount = Math.Min(lines.Length - 1, 100);
        var columnTypes = new string[headers.Length];

        for (var col = 0; col < headers.Length; col++)
        {
            var values = new List<string>();
            for (var row = 1; row <= sampleCount; row++)
            {
                var fields = ParseCsvLine(lines[row], delimiter);
                if (col < fields.Length && !string.IsNullOrWhiteSpace(fields[col]))
                    values.Add(fields[col].Trim());
            }
            columnTypes[col] = InferColumnType(values);
        }

        // Build JSON Schema for the row shape
        var schema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = BuildCsvRowSchema(headers, columnTypes)
        };

        return schema;
    }

    private static JsonObject BuildCsvRowSchema(string[] headers, string[] types)
    {
        var rowSchema = new JsonObject { ["type"] = "object" };
        var properties = new JsonObject();
        var required = new JsonArray();

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(header))
                header = $"column_{i + 1}";

            properties[header] = new JsonObject { ["type"] = i < types.Length ? types[i] : "string" };
            required.Add(JsonValue.Create(header));
        }

        rowSchema["properties"] = properties;
        rowSchema["required"] = required;

        return rowSchema;
    }

    private static string InferColumnType(List<string> values)
    {
        if (values.Count == 0) return "string";

        var allInteger = true;
        var allNumber = true;
        var allBoolean = true;

        foreach (var v in values)
        {
            if (!long.TryParse(v, out _))
                allInteger = false;
            if (!double.TryParse(v, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                allNumber = false;
            if (!bool.TryParse(v, out _) && v is not ("0" or "1"))
                allBoolean = false;
        }

        if (allBoolean) return "boolean";
        if (allInteger) return "integer";
        if (allNumber) return "number";
        return "string";
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = "";
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current += '"';
                    i++; // Skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                fields.Add(current);
                current = "";
            }
            else if (ch != '\r')
            {
                current += ch;
            }
        }

        fields.Add(current);
        return fields.ToArray();
    }

    // --- Shared helpers ---

    private static JsonObject InferValueType(string value)
    {
        if (string.IsNullOrEmpty(value))
            return new JsonObject { ["type"] = "string" };

        if (bool.TryParse(value, out _))
            return new JsonObject { ["type"] = "boolean" };

        if (long.TryParse(value, out _))
            return new JsonObject { ["type"] = "integer" };

        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return new JsonObject { ["type"] = "number" };

        return InferStringSchema(value);
    }

    private static int CountFields(JsonObject schema)
    {
        var count = 0;
        CountFieldsRecursive(schema, ref count);
        return count;
    }

    private static void CountFieldsRecursive(JsonNode? node, ref int count)
    {
        if (node is not JsonObject obj) return;

        if (obj.ContainsKey("properties") && obj["properties"] is JsonObject props)
        {
            foreach (var kvp in props)
            {
                count++;
                CountFieldsRecursive(kvp.Value, ref count);
            }
        }

        if (obj.ContainsKey("items"))
            CountFieldsRecursive(obj["items"], ref count);
    }

    /// <summary>
    /// AOT-safe deep clone of a JsonNode via round-trip through string.
    /// </summary>
    private static JsonObject CloneJsonObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())!.AsObject();
    }

    // --- Format detection regexes ---

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.Compiled)]
    private static partial Regex DateTimeRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled)]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"^https?://", RegexOptions.Compiled)]
    private static partial Regex UriRegex();
}

public record SchemaResult(string DetectedFormat, int FieldCount, JsonObject Schema);
