using System.Text.RegularExpressions;

namespace DisposableEmailShield;

public static partial class EmailValidator
{
    // Simple but effective email format validation
    // Avoids overly complex regex — just checks basic structure
    [GeneratedRegex(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();

    public static bool IsValidFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;

        try
        {
            return EmailRegex().IsMatch(email);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
