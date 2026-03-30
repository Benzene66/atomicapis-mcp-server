using DnsClient;
using DnsClient.Protocol;

namespace EmailAuthGrader;

public static class DnsLookup
{
    private static readonly LookupClient Client = new(new LookupClientOptions
    {
        UseCache = true,
        Timeout = TimeSpan.FromSeconds(5),
        Retries = 2
    });

    public static async Task<List<string>> GetTxtRecordsAsync(string domain)
    {
        try
        {
            var result = await Client.QueryAsync(domain, QueryType.TXT);
            var records = new List<string>();

            foreach (var record in result.Answers.TxtRecords())
            {
                // TXT records can be split across multiple strings; join them
                var text = string.Join("", record.Text);
                records.Add(text);
            }

            return records;
        }
        catch (DnsResponseException)
        {
            return [];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
