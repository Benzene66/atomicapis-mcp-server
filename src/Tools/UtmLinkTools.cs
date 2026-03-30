using System.ComponentModel;
using UtmLinkCloaker;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class UtmLinkTools
{
    [McpServerTool, Description(
        "Creates a cloaked redirect link with UTM tracking parameters encoded in a stateless token. " +
        "The token is a deflate-compressed, base64url-encoded payload — no database needed. " +
        "Returns the token and the full destination URL with UTM params appended.")]
    public static string CloakLink(
        [Description("Destination URL (must be absolute HTTP/HTTPS)")] string url,
        [Description("UTM source (e.g. 'google', 'newsletter')")] string? utmSource = null,
        [Description("UTM medium (e.g. 'cpc', 'email', 'social')")] string? utmMedium = null,
        [Description("UTM campaign name")] string? utmCampaign = null,
        [Description("UTM term (keyword)")] string? utmTerm = null,
        [Description("UTM content (ad variant)")] string? utmContent = null)
    {
        var payload = new LinkPayload(
            Url: url,
            UtmSource: utmSource,
            UtmMedium: utmMedium,
            UtmCampaign: utmCampaign,
            UtmTerm: utmTerm,
            UtmContent: utmContent);

        var token = LinkEncoder.Encode(payload);
        var redirectUrl = LinkEncoder.BuildRedirectUrl(payload);

        return $"""
            Token: {token}
            Original URL: {url}
            Destination URL (with UTM): {redirectUrl}
            """;
    }

    [McpServerTool, Description(
        "Decodes a cloaked link token to reveal the original URL and UTM parameters.")]
    public static string DecodeLink(
        [Description("The cloaked link token to decode")] string token)
    {
        var payload = LinkEncoder.Decode(token);
        var redirectUrl = LinkEncoder.BuildRedirectUrl(payload);

        return $"""
            Original URL: {payload.Url}
            Destination URL (with UTM): {redirectUrl}
            UTM Source: {payload.UtmSource ?? "(none)"}
            UTM Medium: {payload.UtmMedium ?? "(none)"}
            UTM Campaign: {payload.UtmCampaign ?? "(none)"}
            UTM Term: {payload.UtmTerm ?? "(none)"}
            UTM Content: {payload.UtmContent ?? "(none)"}
            """;
    }

    [McpServerTool, Description(
        "Builds a UTM-tagged URL without cloaking. Appends utm_source, utm_medium, utm_campaign, " +
        "utm_term, and utm_content parameters to the destination URL.")]
    public static string BuildUtmUrl(
        [Description("Destination URL")] string url,
        [Description("UTM source")] string? utmSource = null,
        [Description("UTM medium")] string? utmMedium = null,
        [Description("UTM campaign")] string? utmCampaign = null,
        [Description("UTM term")] string? utmTerm = null,
        [Description("UTM content")] string? utmContent = null)
    {
        var payload = new LinkPayload(
            Url: url,
            UtmSource: utmSource,
            UtmMedium: utmMedium,
            UtmCampaign: utmCampaign,
            UtmTerm: utmTerm,
            UtmContent: utmContent);

        var taggedUrl = LinkEncoder.BuildRedirectUrl(payload);

        return $"""
            Original URL: {url}
            Tagged URL: {taggedUrl}
            """;
    }
}
