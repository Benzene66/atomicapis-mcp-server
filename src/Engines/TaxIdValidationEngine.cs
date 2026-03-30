using System.Text.RegularExpressions;

namespace TaxIdValidator;

public static partial class TaxIdValidationEngine
{
    private static HttpClient? _httpClient;

    public static void SetHttpClient(HttpClient client) => _httpClient = client;

    private static HttpClient GetHttpClient()
    {
        if (_httpClient != null) return _httpClient;
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _httpClient = client;
        return client;
    }

    // EU country codes that use VIES
    private static readonly HashSet<string> EuCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    // Greece uses EL in VAT but GR as ISO country code
    private static readonly Dictionary<string, string> VatPrefixToCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = "AT", ["BE"] = "BE", ["BG"] = "BG", ["HR"] = "HR", ["CY"] = "CY",
        ["CZ"] = "CZ", ["DK"] = "DK", ["EE"] = "EE", ["FI"] = "FI", ["FR"] = "FR",
        ["DE"] = "DE", ["EL"] = "GR", ["GR"] = "GR", ["HU"] = "HU", ["IE"] = "IE",
        ["IT"] = "IT", ["LV"] = "LV", ["LT"] = "LT", ["LU"] = "LU", ["MT"] = "MT",
        ["NL"] = "NL", ["PL"] = "PL", ["PT"] = "PT", ["RO"] = "RO", ["SK"] = "SK",
        ["SI"] = "SI", ["ES"] = "ES", ["SE"] = "SE",
        ["GB"] = "GB", ["AU"] = "AU", ["IN"] = "IN"
    };

    // Valid IRS campus prefixes for US EIN
    private static readonly HashSet<int> ValidEinPrefixes = new()
    {
        01, 02, 03, 04, 05, 06,
        10, 11, 12, 13, 14, 15, 16,
        20, 21, 22, 23, 24, 25, 26, 27,
        30, 31, 32, 33, 34, 35, 36, 37, 38,
        40, 41, 42, 43, 44, 45, 46, 47, 48,
        50, 51, 52, 53, 54, 55, 56, 57, 58, 59,
        60, 61, 62, 63, 64, 65, 66, 67, 68,
        71, 72, 73, 74, 75, 76, 77,
        80, 81, 82, 83, 84, 85, 86, 87, 88,
        90, 91, 92, 93, 94, 95, 96, 97, 98, 99
    };

    // EU VAT format regexes per country (prefix already stripped)
    private static readonly Dictionary<string, Regex> EuVatFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = AtVatRegex(),
        ["BE"] = BeVatRegex(),
        ["BG"] = BgVatRegex(),
        ["HR"] = HrVatRegex(),
        ["CY"] = CyVatRegex(),
        ["CZ"] = CzVatRegex(),
        ["DK"] = DkVatRegex(),
        ["EE"] = EeVatRegex(),
        ["FI"] = FiVatRegex(),
        ["FR"] = FrVatRegex(),
        ["DE"] = DeVatRegex(),
        ["GR"] = GrVatRegex(),
        ["HU"] = HuVatRegex(),
        ["IE"] = IeVatRegex(),
        ["IT"] = ItVatRegex(),
        ["LV"] = LvVatRegex(),
        ["LT"] = LtVatRegex(),
        ["LU"] = LuVatRegex(),
        ["MT"] = MtVatRegex(),
        ["NL"] = NlVatRegex(),
        ["PL"] = PlVatRegex(),
        ["PT"] = PtVatRegex(),
        ["RO"] = RoVatRegex(),
        ["SK"] = SkVatRegex(),
        ["SI"] = SiVatRegex(),
        ["ES"] = EsVatRegex(),
        ["SE"] = SeVatRegex(),
    };

    [GeneratedRegex(@"^U\d{8}$")]
    private static partial Regex AtVatRegex();
    [GeneratedRegex(@"^[01]\d{9}$")]
    private static partial Regex BeVatRegex();
    [GeneratedRegex(@"^\d{9,10}$")]
    private static partial Regex BgVatRegex();
    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex HrVatRegex();
    [GeneratedRegex(@"^\d{8}[A-Z]$")]
    private static partial Regex CyVatRegex();
    [GeneratedRegex(@"^\d{8,10}$")]
    private static partial Regex CzVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex DkVatRegex();
    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex EeVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex FiVatRegex();
    [GeneratedRegex(@"^[A-Z0-9]{2}\d{9}$")]
    private static partial Regex FrVatRegex();
    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex DeVatRegex();
    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex GrVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex HuVatRegex();
    [GeneratedRegex(@"^\d{7}[A-Z]{1,2}$|^\d[A-Z+*]\d{5}[A-Z]$")]
    private static partial Regex IeVatRegex();
    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex ItVatRegex();
    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex LvVatRegex();
    [GeneratedRegex(@"^\d{9}$|^\d{12}$")]
    private static partial Regex LtVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex LuVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex MtVatRegex();
    [GeneratedRegex(@"^\d{9}B\d{2}$")]
    private static partial Regex NlVatRegex();
    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex PlVatRegex();
    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex PtVatRegex();
    [GeneratedRegex(@"^\d{2,10}$")]
    private static partial Regex RoVatRegex();
    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex SkVatRegex();
    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex SiVatRegex();
    [GeneratedRegex(@"^[A-Z0-9]\d{7}[A-Z0-9]$")]
    private static partial Regex EsVatRegex();
    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex SeVatRegex();

    [GeneratedRegex(@"^GB(\d{9}|\d{12})$")]
    private static partial Regex UkVatRegex();
    [GeneratedRegex(@"^\d{2}-\d{7}$")]
    private static partial Regex UsEinRegex();
    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex AuAbnRegex();
    [GeneratedRegex(@"^\d{2}[A-Z]{5}\d{4}[A-Z]\d[Z][A-Z0-9]$")]
    private static partial Regex InGstinRegex();

    public static async Task<TaxIdResult> ValidateAsync(string taxId, string? countryHint = null)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return new TaxIdResult(false, "unknown", "unknown", taxId ?? "", null, null, null, null, "Tax ID is required.");
        }

        var cleaned = taxId.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

        // Try to detect the type
        var (type, country, normalizedId) = DetectTaxIdType(cleaned, taxId.Trim().ToUpperInvariant(), countryHint);

        return type switch
        {
            "eu_vat" => await ValidateEuVatAsync(country!, normalizedId, taxId.Trim()),
            "uk_vat" => ValidateUkVat(normalizedId, taxId.Trim()),
            "us_ein" => ValidateUsEin(taxId.Trim()),
            "au_abn" => ValidateAuAbn(normalizedId, taxId.Trim()),
            "in_gstin" => ValidateInGstin(normalizedId, taxId.Trim()),
            _ => new TaxIdResult(false, "unknown", country ?? "unknown", taxId.Trim(), null, null, null, null,
                "Unable to determine tax ID type. Provide a valid tax ID with country prefix or specify countryHint.")
        };
    }

    internal static (string Type, string? Country, string NormalizedId) DetectTaxIdType(string cleaned, string original, string? countryHint)
    {
        // US EIN: XX-XXXXXXX pattern (check original since we strip dashes in cleaned)
        var originalNorm = original.Replace(" ", "");
        if (UsEinRegex().IsMatch(originalNorm))
        {
            return ("us_ein", "US", originalNorm);
        }

        // Check for 2-letter prefix
        if (cleaned.Length >= 3 && char.IsLetter(cleaned[0]) && char.IsLetter(cleaned[1]))
        {
            var prefix = cleaned[..2];
            var rest = cleaned[2..];

            // UK VAT — any GB prefix is treated as UK VAT attempt
            if (prefix == "GB")
            {
                return ("uk_vat", "GB", rest);
            }

            // EU VAT — map prefix to country
            var vatPrefix = prefix;
            if (VatPrefixToCountry.TryGetValue(vatPrefix, out var isoCountry) && EuCountryCodes.Contains(isoCountry))
            {
                // Use EL for Greece in VIES
                return ("eu_vat", isoCountry, rest);
            }

            // IN GSTIN — 15 chars starting with digits
            // Actually GSTIN starts with digits, handled below
        }

        // AU ABN: 11 digits
        if (AuAbnRegex().IsMatch(cleaned) && (countryHint == null || countryHint.Equals("AU", StringComparison.OrdinalIgnoreCase)))
        {
            if (countryHint?.Equals("AU", StringComparison.OrdinalIgnoreCase) == true)
                return ("au_abn", "AU", cleaned);
        }

        // IN GSTIN: 15 chars, starts with 2-digit state code
        if (cleaned.Length == 15 && char.IsDigit(cleaned[0]) && char.IsDigit(cleaned[1]))
        {
            if (InGstinRegex().IsMatch(cleaned))
                return ("in_gstin", "IN", cleaned);
        }

        // Use countryHint to determine type
        if (!string.IsNullOrWhiteSpace(countryHint))
        {
            var hint = countryHint.ToUpperInvariant();

            if (hint == "GB")
            {
                // Try adding GB prefix
                if (UkVatRegex().IsMatch("GB" + cleaned))
                    return ("uk_vat", "GB", cleaned);
            }
            else if (hint == "US")
            {
                return ("us_ein", "US", originalNorm);
            }
            else if (hint == "AU")
            {
                return ("au_abn", "AU", cleaned);
            }
            else if (hint == "IN")
            {
                return ("in_gstin", "IN", cleaned);
            }
            else if (EuCountryCodes.Contains(hint))
            {
                return ("eu_vat", hint, cleaned);
            }
        }

        return ("unknown", null, cleaned);
    }

    private static async Task<TaxIdResult> ValidateEuVatAsync(string country, string vatNumber, string originalInput)
    {
        // Get the VIES prefix for the country
        var viesPrefix = country == "GR" ? "EL" : country;
        var formattedId = viesPrefix + vatNumber;

        // Check format first
        if (EuVatFormats.TryGetValue(country, out var regex) && !regex.IsMatch(vatNumber))
        {
            return new TaxIdResult(false, "eu_vat", country, formattedId, null, null, null, "format_only",
                $"Invalid EU VAT format for country {country}.");
        }

        // Try VIES SOAP lookup
        try
        {
            var soapRequest = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:urn=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
  <soapenv:Header/>
  <soapenv:Body>
    <urn:checkVat>
      <urn:countryCode>{viesPrefix}</urn:countryCode>
      <urn:vatNumber>{vatNumber}</urn:vatNumber>
    </urn:checkVat>
  </soapenv:Body>
</soapenv:Envelope>";

            var client = GetHttpClient();
            var content = new StringContent(soapRequest, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync("https://ec.europa.eu/taxation_customs/vies/services/checkVatService", content);

            if (response.IsSuccessStatusCode)
            {
                var xml = await response.Content.ReadAsStringAsync();
                var result = ParseViesResponse(xml, country, formattedId);
                if (result != null)
                    return result;
            }
        }
        catch
        {
            // VIES unavailable — fallback to format-only
        }

        // Format-only fallback
        return new TaxIdResult(true, "eu_vat", country, formattedId, null, null, null, "vies_fallback_format",
            "VIES service unavailable. Format validation only.");
    }

    internal static TaxIdResult? ParseViesResponse(string xml, string country, string formattedId)
    {
        try
        {
            var validStr = ExtractXmlValue(xml, "valid");
            if (validStr == null) return null;

            var isValid = validStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            var name = ExtractXmlValue(xml, "name");
            var address = ExtractXmlValue(xml, "address");

            // Clean up "---" responses from VIES
            if (name == "---") name = null;
            if (address == "---") address = null;

            return new TaxIdResult(isValid, "eu_vat", country, formattedId, name, address, isValid, "vies_live", null);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractXmlValue(string xml, string elementName)
    {
        // Simple XML extraction — find <ns:elementName> or <elementName>
        // Look for the element with or without namespace prefix
        var patterns = new[]
        {
            $"<ns2:{elementName}>", $"</ns2:{elementName}>",
            $"<{elementName}>", $"</{elementName}>",
        };

        for (int i = 0; i < patterns.Length - 1; i += 2)
        {
            var start = xml.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                start += patterns[i].Length;
                var end = xml.IndexOf(patterns[i + 1], start, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                    return xml[start..end].Trim();
            }
        }

        return null;
    }

    internal static TaxIdResult ValidateUkVat(string digits, string originalInput)
    {
        var formattedId = "GB" + digits;

        if (digits.Length != 9 && digits.Length != 12)
        {
            return new TaxIdResult(false, "uk_vat", "GB", formattedId, null, null, null, "format_only",
                "UK VAT number must be 9 or 12 digits after GB prefix.");
        }

        if (!digits.All(char.IsDigit))
        {
            return new TaxIdResult(false, "uk_vat", "GB", formattedId, null, null, null, "format_only",
                "UK VAT number must contain only digits after GB prefix.");
        }

        // Modulus 97 check for 9-digit numbers
        if (digits.Length >= 9)
        {
            var first9 = digits[..9];
            if (long.TryParse(first9, out var num))
            {
                var isChecksumValid = num % 97 == 0;
                if (digits.Length == 9)
                {
                    return new TaxIdResult(isChecksumValid, "uk_vat", "GB", formattedId, null, null, isChecksumValid, "checksum",
                        isChecksumValid ? null : "UK VAT checksum validation failed.");
                }
                else
                {
                    // 12-digit: first 9 must pass mod 97, last 3 are branch code
                    return new TaxIdResult(isChecksumValid, "uk_vat", "GB", formattedId, null, null, isChecksumValid, "checksum",
                        isChecksumValid ? null : "UK VAT checksum validation failed.");
                }
            }
        }

        return new TaxIdResult(false, "uk_vat", "GB", formattedId, null, null, null, "format_only",
            "Unable to validate UK VAT number.");
    }

    internal static TaxIdResult ValidateUsEin(string originalInput)
    {
        var cleaned = originalInput.Replace(" ", "");
        var formattedId = cleaned;

        if (!UsEinRegex().IsMatch(cleaned))
        {
            return new TaxIdResult(false, "us_ein", "US", formattedId, null, null, null, "format_only",
                "US EIN must be in XX-XXXXXXX format.");
        }

        var prefixStr = cleaned[..2];
        if (int.TryParse(prefixStr, out var prefix))
        {
            var isValid = ValidEinPrefixes.Contains(prefix);
            return new TaxIdResult(isValid, "us_ein", "US", formattedId, null, null, isValid, "checksum",
                isValid ? null : $"Invalid IRS campus prefix: {prefixStr}.");
        }

        return new TaxIdResult(false, "us_ein", "US", formattedId, null, null, null, "format_only",
            "Invalid US EIN format.");
    }

    internal static TaxIdResult ValidateAuAbn(string digits, string originalInput)
    {
        var formattedId = digits;

        if (digits.Length != 11 || !digits.All(char.IsDigit))
        {
            return new TaxIdResult(false, "au_abn", "AU", formattedId, null, null, null, "format_only",
                "Australian ABN must be exactly 11 digits.");
        }

        // ABN checksum: subtract 1 from first digit, apply weights, sum mod 89 must be 0
        int[] weights = [10, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19];
        int sum = 0;
        for (int i = 0; i < 11; i++)
        {
            var d = digits[i] - '0';
            if (i == 0) d -= 1;
            sum += d * weights[i];
        }

        var isValid = sum % 89 == 0;
        return new TaxIdResult(isValid, "au_abn", "AU", formattedId, null, null, isValid, "checksum",
            isValid ? null : "Australian ABN checksum validation failed.");
    }

    internal static TaxIdResult ValidateInGstin(string gstin, string originalInput)
    {
        var formattedId = gstin;

        if (gstin.Length != 15)
        {
            return new TaxIdResult(false, "in_gstin", "IN", formattedId, null, null, null, "format_only",
                "Indian GSTIN must be exactly 15 characters.");
        }

        // Check format: 2-digit state (01-37), 10-char PAN, 1 entity digit, Z, check digit
        if (!InGstinRegex().IsMatch(gstin))
        {
            return new TaxIdResult(false, "in_gstin", "IN", formattedId, null, null, null, "format_only",
                "Invalid GSTIN format.");
        }

        // Validate state code (01-37)
        var stateCode = int.Parse(gstin[..2]);
        if (stateCode < 1 || stateCode > 37)
        {
            return new TaxIdResult(false, "in_gstin", "IN", formattedId, null, null, null, "format_only",
                $"Invalid state code: {stateCode}. Must be between 01 and 37.");
        }

        // Luhn mod 36 check digit validation
        var isChecksumValid = ValidateGstinCheckDigit(gstin);
        return new TaxIdResult(isChecksumValid, "in_gstin", "IN", formattedId, null, null, isChecksumValid, "checksum",
            isChecksumValid ? null : "GSTIN check digit validation failed.");
    }

    internal static bool ValidateGstinCheckDigit(string gstin)
    {
        // Luhn mod 36 algorithm
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int factor = 1;
        int sum = 0;

        for (int i = 0; i < gstin.Length - 1; i++)
        {
            int codePoint = chars.IndexOf(gstin[i]);
            if (codePoint < 0) return false;

            int addend = factor * codePoint;
            factor = (factor == 2) ? 1 : 2;
            addend = (addend / 36) + (addend % 36);
            sum += addend;
        }

        int remainder = sum % 36;
        int checkCodePoint = (36 - remainder) % 36;
        char expectedCheck = chars[checkCodePoint];

        return gstin[^1] == expectedCheck;
    }
}

public record TaxIdResult(
    bool IsValid,
    string TaxIdType,
    string Country,
    string FormattedId,
    string? BusinessName,
    string? BusinessAddress,
    bool? IsActive,
    string? ValidationMethod,
    string? Error);
