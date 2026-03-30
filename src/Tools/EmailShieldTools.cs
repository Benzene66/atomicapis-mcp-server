using System.ComponentModel;
using DisposableEmailShield;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class EmailShieldTools
{
    [McpServerTool, Description(
        "Verifies an email address: checks format validity, whether the domain is a known " +
        "disposable/temporary email provider (500+ domains), and whether the domain has MX records. " +
        "Returns a verdict: 'valid', 'disposable', 'invalid_format', 'no_mx_records', or 'dns_error'.")]
    public static async Task<string> VerifyEmail(
        [Description("The email address to verify")] string email)
    {
        if (!EmailValidator.IsValidFormat(email))
        {
            return $"""
                Email: {email}
                Valid format: false
                Verdict: invalid_format
                """;
        }

        var domain = email.Split('@')[1].ToLowerInvariant();
        var isDisposable = DisposableDomains.IsDisposable(domain);

        if (isDisposable)
        {
            return $"""
                Email: {email}
                Domain: {domain}
                Valid format: true
                Is disposable: true
                Verdict: disposable
                """;
        }

        try
        {
            var hasMx = await DnsChecker.HasMxRecordsAsync(domain);
            var verdict = hasMx ? "valid" : "no_mx_records";

            return $"""
                Email: {email}
                Domain: {domain}
                Valid format: true
                Is disposable: false
                Has MX records: {hasMx}
                Verdict: {verdict}
                """;
        }
        catch
        {
            return $"""
                Email: {email}
                Domain: {domain}
                Valid format: true
                Is disposable: false
                Has MX records: unknown
                Verdict: dns_error
                """;
        }
    }
}
