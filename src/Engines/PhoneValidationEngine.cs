namespace PhoneValidator;

public static class PhoneValidationEngine
{
    private static readonly List<CountryRule> CountryRules = new()
    {
        // Order matters: more specific dialing codes first, then longer codes before shorter ones
        new("Nigeria", "NG", "234", new[] { 10 }, null, false),
        new("Ireland", "IE", "353", new[] { 9 }, null, false),
        new("Portugal", "PT", "351", new[] { 9 }, null, false),
        new("UAE", "AE", "971", new[] { 9 }, null, false),
        new("India", "IN", "91", new[] { 10 }, null, false),
        new("Turkey", "TR", "90", new[] { 10 }, null, false),
        new("Japan", "JP", "81", new[] { 10 }, null, false),
        new("South Korea", "KR", "82", new[] { 9, 10 }, null, false),
        new("China", "CN", "86", new[] { 11 }, null, false),
        new("Singapore", "SG", "65", new[] { 8 }, null, false),
        new("New Zealand", "NZ", "64", new[] { 8, 9 }, null, true),
        new("Australia", "AU", "61", new[] { 9 }, null, true),
        new("Brazil", "BR", "55", new[] { 10, 11 }, null, false),
        new("Argentina", "AR", "54", new[] { 10 }, null, false),
        new("Colombia", "CO", "57", new[] { 10 }, null, false),
        new("Mexico", "MX", "52", new[] { 10 }, null, false),
        new("Germany", "DE", "49", new[] { 10, 11 }, null, true),
        new("Switzerland", "CH", "41", new[] { 9 }, null, true),
        new("Poland", "PL", "48", new[] { 9 }, null, false),
        new("Norway", "NO", "47", new[] { 8 }, null, false),
        new("Sweden", "SE", "46", new[] { 7, 8, 9 }, null, true),
        new("Denmark", "DK", "45", new[] { 8 }, null, false),
        new("UK", "GB", "44", new[] { 10 }, null, true),
        new("Italy", "IT", "39", new[] { 9, 10 }, null, false),
        new("Spain", "ES", "34", new[] { 9 }, null, false),
        new("France", "FR", "33", new[] { 9 }, null, true),
        new("Netherlands", "NL", "31", new[] { 9 }, null, true),
        new("South Africa", "ZA", "27", new[] { 9 }, null, true),
        new("Russia", "RU", "7", new[] { 10 }, null, false),
        new("US", "US", "1", new[] { 10 }, "CA", false),
    };

    private static readonly Dictionary<string, CountryRule> ByCountryCode;
    private static readonly Dictionary<string, CountryRule> ByDialingCode;

    static PhoneValidationEngine()
    {
        ByCountryCode = new Dictionary<string, CountryRule>(StringComparer.OrdinalIgnoreCase);
        ByDialingCode = new Dictionary<string, CountryRule>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in CountryRules)
        {
            // First country code wins
            if (!ByCountryCode.ContainsKey(rule.CountryCode))
                ByCountryCode[rule.CountryCode] = rule;
            if (rule.AlternateCountryCode != null && !ByCountryCode.ContainsKey(rule.AlternateCountryCode))
                ByCountryCode[rule.AlternateCountryCode] = rule;
            // For dialing code lookup, first entry wins (more specific first due to ordering)
            if (!ByDialingCode.ContainsKey(rule.DialingCode))
                ByDialingCode[rule.DialingCode] = rule;
        }
    }

    public static PhoneValidationResult Validate(string phoneNumber, string? defaultCountryCode = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new PhoneValidationResult(
                false, null, null, null, null, null, null, null,
                "Phone number is required.");
        }

        // Strip formatting characters but preserve leading +
        var cleaned = StripFormatting(phoneNumber);

        if (string.IsNullOrEmpty(cleaned))
        {
            return new PhoneValidationResult(
                false, null, null, null, null, null, null, null,
                "Phone number contains no digits.");
        }

        bool hasPlus = phoneNumber.TrimStart().StartsWith('+');
        CountryRule? matchedRule = null;
        string nationalNumber = cleaned;

        if (hasPlus)
        {
            // International format: try to match dialing code (longest first)
            matchedRule = MatchDialingCode(cleaned, out nationalNumber);
            if (matchedRule == null)
            {
                return new PhoneValidationResult(
                    false, null, null, null, null, cleaned, null, null,
                    "Unable to determine country from phone number.");
            }
        }
        else
        {
            // No +, need default country or try to infer
            if (!string.IsNullOrWhiteSpace(defaultCountryCode) &&
                ByCountryCode.TryGetValue(defaultCountryCode, out var defaultRule))
            {
                matchedRule = defaultRule;
                nationalNumber = StripNationalPrefix(cleaned, defaultRule);
            }
            else if (!string.IsNullOrWhiteSpace(defaultCountryCode))
            {
                return new PhoneValidationResult(
                    false, null, null, null, null, cleaned, null, null,
                    $"Unknown default country code: {defaultCountryCode}");
            }
            else
            {
                // Try to match by dialing code in case they just omitted the +
                matchedRule = MatchDialingCode(cleaned, out nationalNumber);
                if (matchedRule == null)
                {
                    return new PhoneValidationResult(
                        false, null, null, null, null, cleaned, null, null,
                        "Unable to determine country. Provide a defaultCountry or use international format (+...).");
                }
            }

            if (matchedRule != null && !hasPlus)
            {
                nationalNumber = StripNationalPrefix(cleaned, matchedRule);
            }
            else
            {
                nationalNumber = cleaned;
            }
        }

        // Validate length
        if (!matchedRule!.ValidNationalLengths.Contains(nationalNumber.Length))
        {
            return new PhoneValidationResult(
                false,
                null,
                matchedRule.CountryCode,
                matchedRule.CountryName,
                "+" + matchedRule.DialingCode,
                nationalNumber,
                null,
                null,
                $"Invalid phone number length. Expected {string.Join(" or ", matchedRule.ValidNationalLengths)} digits for {matchedRule.CountryName}, got {nationalNumber.Length}.");
        }

        // Check for obviously invalid numbers (all zeros)
        if (nationalNumber.All(c => c == '0'))
        {
            return new PhoneValidationResult(
                false,
                null,
                matchedRule.CountryCode,
                matchedRule.CountryName,
                "+" + matchedRule.DialingCode,
                nationalNumber,
                null,
                null,
                "Phone number cannot be all zeros.");
        }

        var numberType = DetermineNumberType(matchedRule, nationalNumber);
        var e164 = $"+{matchedRule.DialingCode}{nationalNumber}";
        var formattedNational = FormatNational(matchedRule, nationalNumber);
        var displayCountry = matchedRule.CountryCode == "US" ? "United States" : matchedRule.CountryName;

        return new PhoneValidationResult(
            true,
            e164,
            matchedRule.CountryCode,
            displayCountry,
            "+" + matchedRule.DialingCode,
            nationalNumber,
            numberType,
            formattedNational,
            null);
    }

    private static string StripFormatting(string input)
    {
        var chars = new char[input.Length];
        int pos = 0;
        foreach (var c in input)
        {
            if (c >= '0' && c <= '9')
                chars[pos++] = c;
        }
        return new string(chars, 0, pos);
    }

    private static CountryRule? MatchDialingCode(string digits, out string nationalNumber)
    {
        // Try longest dialing codes first (up to 3 digits)
        for (int len = 3; len >= 1; len--)
        {
            if (digits.Length <= len)
                continue;

            var prefix = digits.Substring(0, len);
            if (ByDialingCode.TryGetValue(prefix, out var rule))
            {
                nationalNumber = digits.Substring(len);
                // Strip national prefix if present after dialing code
                nationalNumber = StripNationalPrefixAfterDialingCode(nationalNumber, rule);
                return rule;
            }
        }

        nationalNumber = digits;
        return null;
    }

    private static string StripNationalPrefixAfterDialingCode(string national, CountryRule rule)
    {
        // Some numbers might have a trunk prefix even after the dialing code (shouldn't happen in E.164
        // but we handle it gracefully)
        if (!rule.HasTrunkPrefix)
            return national;

        // If the number is one digit too long and starts with 0, strip it
        if (national.Length > 0 && national[0] == '0')
        {
            var stripped = national.Substring(1);
            if (rule.ValidNationalLengths.Contains(stripped.Length))
                return stripped;
        }

        return national;
    }

    private static string StripNationalPrefix(string digits, CountryRule rule)
    {
        // If number starts with the dialing code, strip it
        if (digits.StartsWith(rule.DialingCode))
        {
            var afterDialingCode = digits.Substring(rule.DialingCode.Length);
            var stripped = StripNationalPrefixAfterDialingCode(afterDialingCode, rule);
            if (rule.ValidNationalLengths.Contains(stripped.Length))
                return stripped;
        }

        // Strip trunk prefix (leading 0) for countries that use it
        if (rule.HasTrunkPrefix && digits.Length > 0 && digits[0] == '0')
        {
            var stripped = digits.Substring(1);
            if (rule.ValidNationalLengths.Contains(stripped.Length))
                return stripped;
        }

        // For US/CA: if 11 digits starting with 1, strip the 1
        if (rule.DialingCode == "1" && digits.Length == 11 && digits[0] == '1')
        {
            return digits.Substring(1);
        }

        return digits;
    }

    private static string DetermineNumberType(CountryRule rule, string nationalNumber)
    {
        return rule.CountryCode switch
        {
            "US" or "CA" => "fixed_or_mobile",
            "GB" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("7") => "mobile",
                _ when nationalNumber.StartsWith("1") || nationalNumber.StartsWith("2") => "landline",
                _ when nationalNumber.StartsWith("3") => "landline",
                _ when nationalNumber.StartsWith("8") => "toll_free_or_special",
                _ when nationalNumber.StartsWith("9") => "premium",
                _ => "unknown"
            },
            "DE" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("15") || nationalNumber.StartsWith("16") || nationalNumber.StartsWith("17") => "mobile",
                _ => "fixed_or_mobile"
            },
            "FR" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("6") || nationalNumber.StartsWith("7") => "mobile",
                _ when nationalNumber.StartsWith("1") || nationalNumber.StartsWith("2") ||
                       nationalNumber.StartsWith("3") || nationalNumber.StartsWith("4") ||
                       nationalNumber.StartsWith("5") => "landline",
                _ => "unknown"
            },
            "AU" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("4") => "mobile",
                _ when nationalNumber.StartsWith("2") || nationalNumber.StartsWith("3") ||
                       nationalNumber.StartsWith("7") || nationalNumber.StartsWith("8") => "landline",
                _ => "unknown"
            },
            "JP" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("90") || nationalNumber.StartsWith("80") ||
                       nationalNumber.StartsWith("70") => "mobile",
                _ => "landline"
            },
            "IN" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("9") || nationalNumber.StartsWith("8") ||
                       nationalNumber.StartsWith("7") || nationalNumber.StartsWith("6") => "mobile",
                _ => "landline"
            },
            "BR" => nationalNumber switch
            {
                _ when nationalNumber.Length == 11 && (nationalNumber[2] == '9') => "mobile",
                _ when nationalNumber.Length == 10 => "landline",
                _ => "unknown"
            },
            "CN" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("1") => "mobile",
                _ => "landline"
            },
            "MX" => "fixed_or_mobile",
            "KR" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("10") || nationalNumber.StartsWith("11") ||
                       nationalNumber.StartsWith("16") || nationalNumber.StartsWith("17") ||
                       nationalNumber.StartsWith("18") || nationalNumber.StartsWith("19") => "mobile",
                _ => "landline"
            },
            "IT" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("3") => "mobile",
                _ => "landline"
            },
            "ES" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("6") || nationalNumber.StartsWith("7") => "mobile",
                _ when nationalNumber.StartsWith("9") => "landline",
                _ => "unknown"
            },
            "NL" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("6") => "mobile",
                _ => "landline"
            },
            "SE" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("7") => "mobile",
                _ => "landline"
            },
            "NO" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("4") || nationalNumber.StartsWith("9") => "mobile",
                _ => "landline"
            },
            "DK" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("2") || nationalNumber.StartsWith("3") ||
                       nationalNumber.StartsWith("4") || nationalNumber.StartsWith("5") ||
                       nationalNumber.StartsWith("6") || nationalNumber.StartsWith("9") => "mobile",
                _ => "landline"
            },
            "CH" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("7") => "mobile",
                _ => "landline"
            },
            "PL" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("5") || nationalNumber.StartsWith("6") ||
                       nationalNumber.StartsWith("7") || nationalNumber.StartsWith("8") => "mobile",
                _ => "landline"
            },
            "RU" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("9") => "mobile",
                _ => "landline"
            },
            "TR" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("5") => "mobile",
                _ => "landline"
            },
            "ZA" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("6") || nationalNumber.StartsWith("7") ||
                       nationalNumber.StartsWith("8") => "mobile",
                _ => "landline"
            },
            "NG" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("7") || nationalNumber.StartsWith("8") ||
                       nationalNumber.StartsWith("9") => "mobile",
                _ => "landline"
            },
            "AE" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("5") => "mobile",
                _ => "landline"
            },
            "SG" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("8") || nationalNumber.StartsWith("9") => "mobile",
                _ when nationalNumber.StartsWith("6") || nationalNumber.StartsWith("3") => "landline",
                _ => "unknown"
            },
            "NZ" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("2") => "mobile",
                _ => "landline"
            },
            "IE" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("8") => "mobile",
                _ => "landline"
            },
            "PT" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("9") => "mobile",
                _ when nationalNumber.StartsWith("2") => "landline",
                _ => "unknown"
            },
            "AR" => "fixed_or_mobile",
            "CO" => nationalNumber switch
            {
                _ when nationalNumber.StartsWith("3") => "mobile",
                _ => "landline"
            },
            _ => "unknown"
        };
    }

    private static string FormatNational(CountryRule rule, string nationalNumber)
    {
        return rule.CountryCode switch
        {
            "US" or "CA" when nationalNumber.Length == 10 =>
                $"({nationalNumber.Substring(0, 3)}) {nationalNumber.Substring(3, 3)}-{nationalNumber.Substring(6)}",

            "GB" when nationalNumber.Length == 10 && nationalNumber.StartsWith("7") =>
                $"{nationalNumber.Substring(0, 4)} {nationalNumber.Substring(4)}",
            "GB" when nationalNumber.Length == 10 && nationalNumber.StartsWith("20") =>
                $"{nationalNumber.Substring(0, 2)} {nationalNumber.Substring(2, 4)} {nationalNumber.Substring(6)}",
            "GB" when nationalNumber.Length == 10 =>
                $"{nationalNumber.Substring(0, 4)} {nationalNumber.Substring(4)}",

            "FR" when nationalNumber.Length == 9 =>
                $"{nationalNumber.Substring(0, 1)} {nationalNumber.Substring(1, 2)} {nationalNumber.Substring(3, 2)} {nationalNumber.Substring(5, 2)} {nationalNumber.Substring(7, 2)}",

            "DE" when nationalNumber.Length >= 10 =>
                $"{nationalNumber.Substring(0, 3)} {nationalNumber.Substring(3)}",

            "AU" when nationalNumber.Length == 9 && nationalNumber.StartsWith("4") =>
                $"{nationalNumber.Substring(0, 3)} {nationalNumber.Substring(3, 3)} {nationalNumber.Substring(6)}",
            "AU" when nationalNumber.Length == 9 =>
                $"{nationalNumber.Substring(0, 1)} {nationalNumber.Substring(1, 4)} {nationalNumber.Substring(5)}",

            "JP" when nationalNumber.Length == 10 =>
                $"{nationalNumber.Substring(0, 2)} {nationalNumber.Substring(2, 4)} {nationalNumber.Substring(6)}",

            "IN" when nationalNumber.Length == 10 =>
                $"{nationalNumber.Substring(0, 5)} {nationalNumber.Substring(5)}",

            _ => nationalNumber
        };
    }

    private record CountryRule(
        string CountryName,
        string CountryCode,
        string DialingCode,
        int[] ValidNationalLengths,
        string? AlternateCountryCode,
        bool HasTrunkPrefix);
}

public record PhoneValidationResult(
    bool IsValid,
    string? E164,
    string? CountryCode,
    string? CountryName,
    string? DialingCode,
    string? NationalNumber,
    string? NumberType,
    string? FormattedNational,
    string? Error);
