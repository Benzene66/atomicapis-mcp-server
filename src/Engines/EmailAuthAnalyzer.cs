namespace EmailAuthGrader;

public static class EmailAuthAnalyzer
{
    private static readonly string[] DefaultDkimSelectors =
    [
        "google",
        "default",
        "selector1",
        "selector2",
        "k1",
        "s1",
        "s2"
    ];

    public static async Task<AuthReport> AnalyzeAsync(string domain, string[]? customDkimSelectors = null)
    {
        return await AnalyzeAsync(domain, DnsLookup.GetTxtRecordsAsync, customDkimSelectors);
    }

    public static async Task<AuthReport> AnalyzeAsync(
        string domain,
        Func<string, Task<List<string>>> getTxtRecords,
        string[]? customDkimSelectors = null)
    {
        // Fetch SPF records (from root domain)
        var rootTxtRecords = await getTxtRecords(domain);
        var spfResult = ParseSpf(rootTxtRecords);

        // Fetch DMARC records
        var dmarcTxtRecords = await getTxtRecords($"_dmarc.{domain}");
        var dmarcResult = ParseDmarc(dmarcTxtRecords);

        // Check DKIM selectors
        var selectors = customDkimSelectors is { Length: > 0 } ? customDkimSelectors : DefaultDkimSelectors;
        var dkimResult = await CheckDkim(domain, selectors, getTxtRecords);

        // Check MTA-STS
        var mtaStsTxtRecords = await getTxtRecords($"_mta-sts.{domain}");
        var mtaStsResult = ParseMtaSts(mtaStsTxtRecords);

        // Check BIMI
        var bimiTxtRecords = await getTxtRecords($"default._bimi.{domain}");
        var bimiResult = ParseBimi(bimiTxtRecords);

        // Calculate grade
        var (grade, score) = CalculateGrade(spfResult, dmarcResult, dkimResult.Detected, mtaStsResult.Detected, bimiResult.Detected);

        // Build recommendations
        var recommendations = BuildRecommendations(spfResult, dmarcResult, dkimResult, mtaStsResult, bimiResult);

        return new AuthReport(
            Domain: domain,
            Grade: grade,
            Score: score,
            Spf: spfResult,
            Dmarc: dmarcResult,
            Dkim: dkimResult,
            MtaSts: mtaStsResult,
            Bimi: bimiResult,
            Recommendations: recommendations);
    }

    public static SpfResult ParseSpf(List<string> txtRecords)
    {
        var spfRecord = txtRecords.FirstOrDefault(r => r.TrimStart().StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase));

        if (spfRecord is null)
        {
            return new SpfResult(
                Record: null,
                Valid: false,
                AllMechanism: null,
                Includes: [],
                Issues: ["No SPF record found"],
                DnsLookupCount: 0);
        }

        var parts = spfRecord.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var includes = new List<string>();
        string? allMechanism = null;
        var issues = new List<string>();
        var dnsLookupCount = 0;

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();

            if (lower.StartsWith("include:"))
            {
                includes.Add(part["include:".Length..]);
                dnsLookupCount++;
            }
            else if (lower.StartsWith("a:") || lower == "a")
            {
                dnsLookupCount++;
            }
            else if (lower.StartsWith("mx:") || lower == "mx")
            {
                dnsLookupCount++;
            }
            else if (lower.StartsWith("ptr:") || lower == "ptr")
            {
                dnsLookupCount++;
                issues.Add("SPF record uses 'ptr' mechanism which is deprecated.");
            }
            else if (lower.StartsWith("redirect="))
            {
                dnsLookupCount++;
            }
            else if (lower.StartsWith("exists:"))
            {
                dnsLookupCount++;
            }
            else if (lower is "-all" or "~all" or "?all" or "+all")
            {
                allMechanism = lower;
            }
        }

        if (allMechanism is null)
        {
            issues.Add("SPF record is missing an 'all' mechanism.");
        }
        else if (allMechanism == "~all")
        {
            issues.Add("SPF uses softfail (~all) instead of hardfail (-all). Consider switching to -all for stricter enforcement.");
        }
        else if (allMechanism == "?all")
        {
            issues.Add("SPF uses neutral (?all) which provides no protection. Use -all or ~all.");
        }
        else if (allMechanism == "+all")
        {
            issues.Add("SPF uses +all which allows any server to send mail. This is extremely insecure.");
        }

        if (dnsLookupCount > 10)
        {
            issues.Add($"SPF record requires {dnsLookupCount} DNS lookups, exceeding the limit of 10.");
        }

        return new SpfResult(
            Record: spfRecord,
            Valid: true,
            AllMechanism: allMechanism,
            Includes: includes,
            Issues: issues,
            DnsLookupCount: dnsLookupCount);
    }

    public static DmarcResult ParseDmarc(List<string> txtRecords)
    {
        var dmarcRecord = txtRecords.FirstOrDefault(r => r.TrimStart().StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));

        if (dmarcRecord is null)
        {
            return new DmarcResult(
                Record: null,
                Valid: false,
                Policy: null,
                SubdomainPolicy: null,
                Percentage: null,
                AggregateReportUri: null,
                ForensicReportUri: null,
                Issues: ["No DMARC record found"]);
        }

        var tags = ParseDmarcTags(dmarcRecord);
        var issues = new List<string>();

        tags.TryGetValue("p", out var policy);
        tags.TryGetValue("sp", out var subdomainPolicy);
        tags.TryGetValue("rua", out var rua);
        tags.TryGetValue("ruf", out var ruf);
        tags.TryGetValue("pct", out var pctStr);

        int? pct = null;
        if (pctStr is not null && int.TryParse(pctStr, out var pctVal))
        {
            pct = pctVal;
        }

        if (policy is null)
        {
            issues.Add("DMARC record is missing the required 'p' (policy) tag.");
        }
        else if (policy.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("DMARC policy is set to 'none', which only monitors but does not enforce. Consider 'quarantine' or 'reject'.");
        }
        else if (policy.Equals("quarantine", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("DMARC policy is 'quarantine'. Consider upgrading to 'reject' for maximum protection.");
        }

        if (rua is null)
        {
            issues.Add("DMARC record has no aggregate report URI (rua). Add one to receive reports.");
        }

        if (pct is not null && pct < 100)
        {
            issues.Add($"DMARC policy only applies to {pct}% of messages. Consider increasing to 100%.");
        }

        return new DmarcResult(
            Record: dmarcRecord,
            Valid: true,
            Policy: policy,
            SubdomainPolicy: subdomainPolicy,
            Percentage: pct,
            AggregateReportUri: rua,
            ForensicReportUri: ruf,
            Issues: issues);
    }

    private static Dictionary<string, string> ParseDmarcTags(string record)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = record.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                var value = trimmed[(eqIndex + 1)..].Trim();
                tags[key] = value;
            }
        }

        return tags;
    }

    private static async Task<DkimResult> CheckDkim(
        string domain,
        string[] selectors,
        Func<string, Task<List<string>>> getTxtRecords)
    {
        var foundSelectors = new List<string>();

        foreach (var selector in selectors)
        {
            var dkimDomain = $"{selector}._domainkey.{domain}";
            var records = await getTxtRecords(dkimDomain);

            if (records.Any(r => r.Contains("v=DKIM1", StringComparison.OrdinalIgnoreCase) ||
                                 r.Contains("k=rsa", StringComparison.OrdinalIgnoreCase) ||
                                 r.Contains("p=", StringComparison.OrdinalIgnoreCase)))
            {
                foundSelectors.Add(selector);
            }
        }

        return new DkimResult(
            Detected: foundSelectors.Count > 0,
            SelectorsFound: foundSelectors);
    }

    internal static MtaStsResult ParseMtaSts(List<string> txtRecords)
    {
        var record = txtRecords.FirstOrDefault(r => r.TrimStart().StartsWith("v=STSv1", StringComparison.OrdinalIgnoreCase));
        return new MtaStsResult(
            Detected: record is not null,
            Record: record);
    }

    internal static BimiResult ParseBimi(List<string> txtRecords)
    {
        var record = txtRecords.FirstOrDefault(r => r.TrimStart().StartsWith("v=BIMI1", StringComparison.OrdinalIgnoreCase));
        return new BimiResult(
            Detected: record is not null,
            Record: record);
    }

    public static (string Grade, int Score) CalculateGrade(
        SpfResult spf,
        DmarcResult dmarc,
        bool dkimFound,
        bool mtaStsFound,
        bool bimiFound)
    {
        var score = 100;

        // SPF scoring
        if (!spf.Valid)
        {
            score -= 30;
        }
        else if (spf.AllMechanism == "~all")
        {
            score -= 10;
        }

        // DMARC scoring
        if (!dmarc.Valid)
        {
            score -= 30;
        }
        else if (dmarc.Policy is not null)
        {
            if (dmarc.Policy.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                score -= 15;
            }
            else if (dmarc.Policy.Equals("quarantine", StringComparison.OrdinalIgnoreCase))
            {
                score -= 5;
            }
        }

        // DKIM scoring
        if (!dkimFound)
        {
            score -= 20;
        }

        // MTA-STS scoring
        if (!mtaStsFound)
        {
            score -= 5;
        }

        // BIMI scoring
        if (!bimiFound)
        {
            score -= 5;
        }

        var grade = score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };

        return (grade, score);
    }

    private static List<string> BuildRecommendations(
        SpfResult spf,
        DmarcResult dmarc,
        DkimResult dkim,
        MtaStsResult mtaSts,
        BimiResult bimi)
    {
        var recommendations = new List<string>();

        if (!spf.Valid)
        {
            recommendations.Add("Add an SPF record to specify which mail servers are authorized to send email for your domain.");
        }
        else
        {
            if (spf.AllMechanism is "~all" or "?all" or "+all")
            {
                recommendations.Add("Tighten your SPF record by using '-all' (hardfail) to reject unauthorized senders.");
            }
            if (spf.DnsLookupCount > 10)
            {
                recommendations.Add("Reduce the number of DNS lookups in your SPF record to stay within the 10-lookup limit.");
            }
        }

        if (!dmarc.Valid)
        {
            recommendations.Add("Add a DMARC record (_dmarc.yourdomain.com) to protect against email spoofing.");
        }
        else
        {
            if (dmarc.Policy is not null && dmarc.Policy.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add("Upgrade your DMARC policy from 'none' to 'quarantine' or 'reject' for active protection.");
            }
            if (dmarc.AggregateReportUri is null)
            {
                recommendations.Add("Add a 'rua' tag to your DMARC record to receive aggregate reports.");
            }
        }

        if (!dkim.Detected)
        {
            recommendations.Add("Configure DKIM signing for your domain to cryptographically authenticate your emails.");
        }

        if (!mtaSts.Detected)
        {
            recommendations.Add("Implement MTA-STS to enforce TLS encryption for inbound email delivery.");
        }

        if (!bimi.Detected)
        {
            recommendations.Add("Add a BIMI record to display your brand logo in supporting email clients.");
        }

        return recommendations;
    }
}

// --- DTOs ---

public record SpfResult(
    string? Record,
    bool Valid,
    string? AllMechanism,
    List<string> Includes,
    List<string> Issues,
    int DnsLookupCount);

public record DmarcResult(
    string? Record,
    bool Valid,
    string? Policy,
    string? SubdomainPolicy,
    int? Percentage,
    string? AggregateReportUri,
    string? ForensicReportUri,
    List<string> Issues);

public record DkimResult(
    bool Detected,
    List<string> SelectorsFound);

public record MtaStsResult(
    bool Detected,
    string? Record);

public record BimiResult(
    bool Detected,
    string? Record);

public record AuthReport(
    string Domain,
    string Grade,
    int Score,
    SpfResult Spf,
    DmarcResult Dmarc,
    DkimResult Dkim,
    MtaStsResult MtaSts,
    BimiResult Bimi,
    List<string> Recommendations);
