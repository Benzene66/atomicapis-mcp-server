using System.ComponentModel;
using System.Net;
using HtmlAgilityPack;
using ReverseMarkdown;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class WebToMarkdownTools
{
    [McpServerTool, Description(
        "Scrapes a URL and converts the page content to clean Markdown, suitable for RAG/LLM contexts. " +
        "Strips non-essential HTML tags (nav, footer, script, style, header, aside) to minimize tokens. " +
        "Enforces a 5MB page size limit and SSRF protection against private networks.")]
    public static async Task<string> ExtractMarkdown(
        IHttpClientFactory httpClientFactory,
        [Description("The URL to scrape and convert to Markdown")] string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid or missing URL.");

        if (uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Only HTTP and HTTPS URLs are allowed.");

        // SSRF protection: block private/reserved IP ranges
        var addresses = await Dns.GetHostAddressesAsync(uri.Host);
        if (addresses.Length == 0)
            throw new ArgumentException("Could not resolve the URL hostname.");

        foreach (var addr in addresses)
        {
            if (IsPrivateOrReserved(addr))
                throw new ArgumentException("URLs pointing to private or reserved networks are not allowed.");
        }

        const long maxBytes = 5 * 1024 * 1024;

        var client = httpClientFactory.CreateClient("Default");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > maxBytes)
            throw new ArgumentException("The target page exceeds the 5MB size limit.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new ArgumentException("The target page exceeds the 5MB size limit.");
            ms.Write(buffer, 0, read);
        }

        ms.Position = 0;
        using var reader = new StreamReader(ms);
        var html = await reader.ReadToEndAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tagsToRemove = new[] { "nav", "footer", "script", "style", "noscript", "iframe", "svg", "header", "aside" };
        foreach (var tag in tagsToRemove)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
                foreach (var node in nodes)
                    node.Remove();
        }

        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.Bypass,
            GithubFlavored = true,
            RemoveComments = true
        };

        var converter = new Converter(config);
        return converter.Convert(doc.DocumentNode.OuterHtml).Trim();
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   bytes[0] == 127 ||
                   bytes[0] == 0 ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }
        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IPAddress.IsLoopback(address);
    }
}
