using System.Text.Json.Nodes;

namespace ProductSchemaAutoRich;

public static class JsonLdGenerator
{
    /// <summary>
    /// Generates a Google-ready JSON-LD Product schema from structured product data.
    /// Follows https://schema.org/Product and Google's Rich Results requirements.
    /// </summary>
    public static JsonObject Generate(ProductInput input)
    {
        ValidateRequired(input);

        var product = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = input.Name.Trim()
        };

        if (!string.IsNullOrWhiteSpace(input.Description))
            product["description"] = input.Description.Trim();

        if (!string.IsNullOrWhiteSpace(input.ImageUrl))
            product["image"] = input.ImageUrl.Trim();

        if (!string.IsNullOrWhiteSpace(input.Sku))
            product["sku"] = input.Sku.Trim();

        if (!string.IsNullOrWhiteSpace(input.Gtin))
            product["gtin"] = input.Gtin.Trim();

        if (!string.IsNullOrWhiteSpace(input.Brand))
        {
            product["brand"] = new JsonObject
            {
                ["@type"] = "Brand",
                ["name"] = input.Brand.Trim()
            };
        }

        if (!string.IsNullOrWhiteSpace(input.Url))
            product["url"] = input.Url.Trim();

        // Offers — mandatory for Google Rich Results
        var offer = new JsonObject
        {
            ["@type"] = "Offer",
            ["price"] = input.Price,
            ["priceCurrency"] = (input.Currency ?? "USD").Trim().ToUpperInvariant()
        };

        // Availability — map common strings to Schema.org ItemAvailability
        var availability = MapAvailability(input.Availability);
        offer["availability"] = availability;

        if (!string.IsNullOrWhiteSpace(input.Condition))
        {
            var condition = MapCondition(input.Condition);
            offer["itemCondition"] = condition;
        }

        if (!string.IsNullOrWhiteSpace(input.SellerName))
        {
            offer["seller"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = input.SellerName.Trim()
            };
        }

        if (!string.IsNullOrWhiteSpace(input.PriceValidUntil))
            offer["priceValidUntil"] = input.PriceValidUntil.Trim();

        if (!string.IsNullOrWhiteSpace(input.Url))
            offer["url"] = input.Url.Trim();

        product["offers"] = offer;

        // AggregateRating (optional but valuable for rich results)
        if (input.RatingValue.HasValue && input.ReviewCount.HasValue)
        {
            product["aggregateRating"] = new JsonObject
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = input.RatingValue.Value,
                ["reviewCount"] = input.ReviewCount.Value,
                ["bestRating"] = input.BestRating ?? 5,
                ["worstRating"] = input.WorstRating ?? 1
            };
        }

        return product;
    }

    /// <summary>
    /// Generates the full HTML script tag wrapping the JSON-LD.
    /// </summary>
    public static string GenerateHtmlScript(ProductInput input)
    {
        var jsonLd = Generate(input);
        var json = jsonLd.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        return $"<script type=\"application/ld+json\">\n{json}\n</script>";
    }

    private static void ValidateRequired(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Product name is required.");

        if (input.Price < 0)
            throw new ArgumentException("Price cannot be negative.");
    }

    private static string MapAvailability(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
            return "https://schema.org/InStock";

        return availability.Trim().ToLowerInvariant() switch
        {
            "in stock" or "instock" or "in_stock" or "available" => "https://schema.org/InStock",
            "out of stock" or "outofstock" or "out_of_stock" or "unavailable" or "sold out" or "soldout" => "https://schema.org/OutOfStock",
            "pre-order" or "preorder" or "pre_order" => "https://schema.org/PreOrder",
            "backorder" or "back order" or "back_order" => "https://schema.org/BackOrder",
            "discontinued" => "https://schema.org/Discontinued",
            "limited" or "limited availability" or "limited_availability" => "https://schema.org/LimitedAvailability",
            "online only" or "onlineonly" or "online_only" => "https://schema.org/OnlineOnly",
            "in store only" or "instoreonly" or "in_store_only" => "https://schema.org/InStoreOnly",
            _ => "https://schema.org/InStock"
        };
    }

    private static string MapCondition(string condition)
    {
        return condition.Trim().ToLowerInvariant() switch
        {
            "new" => "https://schema.org/NewCondition",
            "used" => "https://schema.org/UsedCondition",
            "refurbished" or "renewed" => "https://schema.org/RefurbishedCondition",
            "damaged" => "https://schema.org/DamagedCondition",
            _ => "https://schema.org/NewCondition"
        };
    }
}

public record ProductInput(
    string Name,
    decimal Price,
    string? Currency = null,
    string? Description = null,
    string? ImageUrl = null,
    string? Sku = null,
    string? Gtin = null,
    string? Brand = null,
    string? Url = null,
    string? Availability = null,
    string? Condition = null,
    string? SellerName = null,
    string? PriceValidUntil = null,
    double? RatingValue = null,
    int? ReviewCount = null,
    int? BestRating = null,
    int? WorstRating = null);
