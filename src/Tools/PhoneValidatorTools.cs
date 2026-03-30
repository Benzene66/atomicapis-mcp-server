using System.ComponentModel;
using PhoneValidator;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class PhoneValidatorTools
{
    [McpServerTool, Description(
        "Validates and formats phone numbers for 38+ countries. Returns E.164 format, " +
        "country detection, number type (mobile/landline/toll-free), and formatted national display.")]
    public static string ValidatePhone(
        [Description("The phone number to validate (e.g. '+1 (555) 123-4567' or '07911 123456')")] string phoneNumber,
        [Description("Default ISO country code if not detectable from the number (e.g. 'US', 'GB')")] string? defaultCountryCode = null)
    {
        var result = PhoneValidationEngine.Validate(phoneNumber, defaultCountryCode);

        if (!result.IsValid)
        {
            return $"""
                Phone: {phoneNumber}
                Valid: false
                Error: {result.Error ?? "Invalid phone number"}
                """;
        }

        return $"""
            Phone: {phoneNumber}
            Valid: true
            E.164: {result.E164}
            Country: {result.CountryName} ({result.CountryCode})
            Dialing code: +{result.DialingCode}
            National number: {result.NationalNumber}
            Number type: {result.NumberType}
            Formatted: {result.FormattedNational}
            """;
    }
}
