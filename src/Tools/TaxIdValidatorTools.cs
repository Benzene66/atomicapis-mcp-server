using System.ComponentModel;
using TaxIdValidator;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class TaxIdValidatorTools
{
    [McpServerTool, Description(
        "Validates tax identification numbers: EU VAT (live VIES check), UK VAT, US EIN, " +
        "Australian ABN, and Indian GSTIN. Returns formatted ID, country, business name/address " +
        "when available, and active status.")]
    public static async Task<string> ValidateTaxId(
        [Description("The tax ID to validate (e.g. 'DE123456789', 'GB123456789', '12-3456789')")] string taxId,
        [Description("Country hint if not detectable from the ID (e.g. 'US', 'DE', 'IN')")] string? countryHint = null)
    {
        var result = await TaxIdValidationEngine.ValidateAsync(taxId, countryHint);

        if (!result.IsValid)
        {
            return $"""
                Tax ID: {taxId}
                Valid: false
                Error: {result.Error ?? "Invalid tax ID"}
                """;
        }

        var businessInfo = result.BusinessName != null ? $"\nBusiness name: {result.BusinessName}" : "";
        businessInfo += result.BusinessAddress != null ? $"\nBusiness address: {result.BusinessAddress}" : "";
        var activeInfo = result.IsActive.HasValue ? $"\nActive: {result.IsActive}" : "";

        return $"""
            Tax ID: {taxId}
            Valid: true
            Type: {result.TaxIdType}
            Country: {result.Country}
            Formatted: {result.FormattedId}
            Validation method: {result.ValidationMethod}{businessInfo}{activeInfo}
            """;
    }
}
