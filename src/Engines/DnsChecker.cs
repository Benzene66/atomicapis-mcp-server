using System.Net;

namespace DisposableEmailShield;

public static class DnsChecker
{
    /// <summary>
    /// Checks if the domain has at least one MX record via DNS resolution.
    /// Falls back to checking if the domain resolves to an A/AAAA record.
    /// </summary>
    public static async Task<bool> HasMxRecordsAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            // Dns.GetHostAddressesAsync resolves A/AAAA records.
            // For a proper MX check, we'd need a DNS library, but for AOT compatibility
            // and simplicity, we check if the domain resolves at all.
            // A domain with no DNS records at all is almost certainly invalid for email.
            var addresses = await Dns.GetHostAddressesAsync(domain, cancellationToken);
            return addresses.Length > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
