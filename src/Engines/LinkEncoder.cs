using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UtmLinkCloaker;

public static class LinkEncoder
{
    /// <summary>
    /// Encodes a destination URL and UTM parameters into a compact, URL-safe token.
    /// The token is deflate-compressed, base64url-encoded JSON.
    /// </summary>
    public static string Encode(LinkPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Url))
            throw new ArgumentException("Destination URL is required.");

        if (!Uri.TryCreate(payload.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("Destination URL must be an absolute HTTP(S) URL.");

        var json = JsonSerializer.SerializeToUtf8Bytes(payload, LinkPayloadContext.Default.LinkPayload);

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(json);
        }

        return Base64UrlEncode(output.ToArray());
    }

    /// <summary>
    /// Decodes a token back into the original payload.
    /// </summary>
    public static LinkPayload Decode(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.");

        byte[] compressed;
        try
        {
            compressed = Base64UrlDecode(token);
        }
        catch
        {
            throw new ArgumentException("Invalid token format.");
        }

        try
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);

            var payload = JsonSerializer.Deserialize(output.ToArray(), LinkPayloadContext.Default.LinkPayload);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Url))
                throw new ArgumentException("Invalid token: missing URL.");

            return payload;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch
        {
            throw new ArgumentException("Invalid or corrupted token.");
        }
    }

    /// <summary>
    /// Builds the final redirect URL by appending UTM parameters to the destination.
    /// </summary>
    public static string BuildRedirectUrl(LinkPayload payload)
    {
        var uriBuilder = new UriBuilder(payload.Url);
        var queryParams = new List<string>();

        // Preserve existing query string
        if (!string.IsNullOrEmpty(uriBuilder.Query))
        {
            queryParams.Add(uriBuilder.Query.TrimStart('?'));
        }

        if (!string.IsNullOrWhiteSpace(payload.UtmSource))
            queryParams.Add($"utm_source={Uri.EscapeDataString(payload.UtmSource)}");
        if (!string.IsNullOrWhiteSpace(payload.UtmMedium))
            queryParams.Add($"utm_medium={Uri.EscapeDataString(payload.UtmMedium)}");
        if (!string.IsNullOrWhiteSpace(payload.UtmCampaign))
            queryParams.Add($"utm_campaign={Uri.EscapeDataString(payload.UtmCampaign)}");
        if (!string.IsNullOrWhiteSpace(payload.UtmTerm))
            queryParams.Add($"utm_term={Uri.EscapeDataString(payload.UtmTerm)}");
        if (!string.IsNullOrWhiteSpace(payload.UtmContent))
            queryParams.Add($"utm_content={Uri.EscapeDataString(payload.UtmContent)}");

        uriBuilder.Query = string.Join("&", queryParams);
        return uriBuilder.Uri.AbsoluteUri;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        // Pad to multiple of 4
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}

public record LinkPayload(
    string Url,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null,
    string? UtmTerm = null,
    string? UtmContent = null);

[JsonSerializable(typeof(LinkPayload))]
internal partial class LinkPayloadContext : JsonSerializerContext
{
}
