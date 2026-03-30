using System.ComponentModel;
using System.Text.Json;
using ProductSchemaAutoRich;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class ProductSchemaTools
{
    [McpServerTool, Description(
        "Generates Schema.org Product JSON-LD markup from e-commerce product data. " +
        "Produces Google-ready structured data with fuzzy availability/condition mapping. " +
        "Returns both the raw JSON-LD object and a ready-to-paste HTML <script> tag.")]
    public static string GenerateProductSchema(
        [Description("Product name")] string name,
        [Description("Product description")] string? description = null,
        [Description("Price as a number (e.g. 29.99)")] decimal price = 0,
        [Description("Currency code (e.g. 'USD', 'EUR')")] string? currency = null,
        [Description("Availability (e.g. 'in stock', 'out of stock', 'pre-order')")] string? availability = null,
        [Description("Condition (e.g. 'new', 'used', 'refurbished')")] string? condition = null,
        [Description("Brand name")] string? brand = null,
        [Description("Product image URL")] string? imageUrl = null,
        [Description("Product page URL")] string? url = null,
        [Description("SKU identifier")] string? sku = null,
        [Description("Average rating (0-5)")] double? ratingValue = null,
        [Description("Number of reviews")] int? reviewCount = null,
        [Description("Seller/organization name")] string? sellerName = null)
    {
        var input = new ProductInput(
            Name: name,
            Description: description,
            Price: price,
            Gtin: null,
            PriceValidUntil: null,
            BestRating: null,
            WorstRating: null,
            Currency: currency,
            Availability: availability,
            Condition: condition,
            Brand: brand,
            ImageUrl: imageUrl,
            Url: url,
            Sku: sku,
            RatingValue: ratingValue,
            ReviewCount: reviewCount,
            SellerName: sellerName);

        var jsonLd = JsonLdGenerator.Generate(input);
        var htmlScript = JsonLdGenerator.GenerateHtmlScript(input);

        return $"""
            JSON-LD:
            {jsonLd.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}

            HTML Script Tag:
            {htmlScript}
            """;
    }
}
