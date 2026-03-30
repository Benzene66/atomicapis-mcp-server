using System.Text.RegularExpressions;

namespace PiiRedactor;

public static partial class PiiRedactorEngine
{
    // --- AOT-safe GeneratedRegex patterns (most specific to least specific) ---

    [GeneratedRegex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b(?:4[0-9]{3}[\s\-]?[0-9]{4}[\s\-]?[0-9]{4}[\s\-]?[0-9]{4}|5[1-5][0-9]{2}[\s\-]?[0-9]{4}[\s\-]?[0-9]{4}[\s\-]?[0-9]{4}|3[47][0-9]{2}[\s\-]?[0-9]{6}[\s\-]?[0-9]{5}|6(?:011|5[0-9]{2})[\s\-]?[0-9]{4}[\s\-]?[0-9]{4}[\s\-]?[0-9]{4})\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"https?://[^\s""'<>\)]+", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?:\+1[\s\-]?)?\(?\d{3}\)?[\s\-]?\d{3}[\s\-]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex UsPhoneRegex();

    [GeneratedRegex(@"\+[2-9]\d{6,14}\b", RegexOptions.Compiled)]
    private static partial Regex InternationalPhoneRegex();

    [GeneratedRegex(@"\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b", RegexOptions.Compiled)]
    private static partial Regex Ipv6Regex();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b", RegexOptions.Compiled)]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(@"\b\d{1,5}\s+[A-Za-z0-9\.\s]+?\b(?:Street|St|Avenue|Ave|Boulevard|Blvd|Drive|Dr|Road|Rd|Lane|Ln|Way|Court|Ct)\.?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StreetAddressRegex();

    [GeneratedRegex(@"\b(?:(?:0[1-9]|1[0-2])[/\-](?:0[1-9]|[12]\d|3[01])[/\-](?:19|20)\d{2}|(?:0[1-9]|[12]\d|3[01])[/\-](?:0[1-9]|1[0-2])[/\-](?:19|20)\d{2}|(?:19|20)\d{2}[/\-](?:0[1-9]|1[0-2])[/\-](?:0[1-9]|[12]\d|3[01]))\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    // Pattern definitions in processing order (most specific first)
    private static readonly (string TypeKey, string PlaceholderPrefix, Func<Regex> GetRegex)[] Patterns =
    [
        ("url", "URL", UrlRegex),
        ("email", "EMAIL", EmailRegex),
        ("credit_card", "CREDIT_CARD", CreditCardRegex),
        ("ssn", "SSN", SsnRegex),
        ("ipv6", "IP_ADDRESS", Ipv6Regex),
        ("ipv4", "IP_ADDRESS", Ipv4Regex),
        ("street_address", "STREET_ADDRESS", StreetAddressRegex),
        ("dob", "DOB", DobRegex),
        ("phone", "PHONE", UsPhoneRegex),
        ("international_phone", "PHONE", InternationalPhoneRegex),
    ];

    public static RedactionResult Redact(string text, bool includeMapping = false)
    {
        var redacted = text;
        var totalPiiFound = 0;

        // Counters per placeholder prefix (e.g. EMAIL -> 2)
        var prefixCounters = new Dictionary<string, int>();

        // Track unique original value -> placeholder
        var valuePlaceholderMap = new Dictionary<string, string>();

        // Reverse mapping: placeholder -> original (for re-hydration)
        var reverseMapping = new Dictionary<string, string>();

        // Counts per type key
        var piiCounts = new Dictionary<string, int>();

        foreach (var (typeKey, placeholderPrefix, getRegex) in Patterns)
        {
            var regex = getRegex();
            var matches = regex.Matches(redacted);

            // Process matches in reverse order to preserve string positions
            var matchList = new List<Match>();
            foreach (Match m in matches)
            {
                // Make sure this match position is not inside an already-placed placeholder
                var matchText = m.Value;
                if (matchText.StartsWith('[') && matchText.Contains(']'))
                    continue;
                matchList.Add(m);
            }

            // Process in reverse to keep indices valid
            matchList.Reverse();

            foreach (var match in matchList)
            {
                var originalValue = match.Value;

                // Skip if this looks like it's already been redacted (inside brackets)
                if (IsInsidePlaceholder(redacted, match.Index))
                    continue;

                string placeholder;
                if (valuePlaceholderMap.TryGetValue(originalValue, out var existingPlaceholder))
                {
                    placeholder = existingPlaceholder;
                }
                else
                {
                    if (!prefixCounters.TryGetValue(placeholderPrefix, out var count))
                        count = 0;
                    count++;
                    prefixCounters[placeholderPrefix] = count;

                    placeholder = $"[{placeholderPrefix}_{count}]";
                    valuePlaceholderMap[originalValue] = placeholder;
                    reverseMapping[placeholder] = originalValue;
                }

                redacted = redacted.Remove(match.Index, match.Length).Insert(match.Index, placeholder);
                totalPiiFound++;

                if (!piiCounts.TryGetValue(typeKey, out var typeCount))
                    typeCount = 0;
                piiCounts[typeKey] = typeCount + 1;
            }
        }

        // Merge ipv4/ipv6 counts into ip_address
        if (piiCounts.ContainsKey("ipv4") || piiCounts.ContainsKey("ipv6"))
        {
            var ipCount = 0;
            if (piiCounts.TryGetValue("ipv4", out var v4)) { ipCount += v4; piiCounts.Remove("ipv4"); }
            if (piiCounts.TryGetValue("ipv6", out var v6)) { ipCount += v6; piiCounts.Remove("ipv6"); }
            piiCounts["ip_address"] = ipCount;
        }

        // Merge international_phone into phone
        if (piiCounts.ContainsKey("international_phone"))
        {
            var intlCount = piiCounts["international_phone"];
            piiCounts.Remove("international_phone");
            if (!piiCounts.TryGetValue("phone", out var phoneCount))
                phoneCount = 0;
            piiCounts["phone"] = phoneCount + intlCount;
        }

        return new RedactionResult(
            redacted,
            totalPiiFound,
            piiCounts,
            includeMapping ? reverseMapping : null);
    }

    public static string Rehydrate(string redactedText, Dictionary<string, string> mapping)
    {
        var result = redactedText;
        foreach (var (placeholder, original) in mapping)
        {
            result = result.Replace(placeholder, original);
        }
        return result;
    }

    private static bool IsInsidePlaceholder(string text, int index)
    {
        // Check if the index falls within an existing [PLACEHOLDER_N] pattern
        var openBracket = text.LastIndexOf('[', index);
        if (openBracket < 0) return false;

        var closeBracket = text.IndexOf(']', openBracket);
        if (closeBracket < 0) return false;

        // If the match index is between [ and ], it's inside a placeholder
        return index > openBracket && index <= closeBracket;
    }
}

public record RedactionResult(
    string RedactedText,
    int TotalPiiFound,
    Dictionary<string, int> PiiCounts,
    Dictionary<string, string>? Mapping);
