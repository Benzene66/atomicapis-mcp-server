using GeoTimeZone;

namespace TimezoneResolver;

public static class TimezoneService
{
    /// <summary>
    /// Resolves timezone information for the given coordinates.
    /// Uses offline embedded shape data — no external API calls.
    /// </summary>
    public static TimezoneResult Resolve(double latitude, double longitude, DateTimeOffset? asOf = null)
    {
        var now = asOf ?? DateTimeOffset.UtcNow;

        // Validate coordinate ranges
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");

        // Lookup IANA timezone from coordinates using embedded shape data
        var tzResult = TimeZoneLookup.GetTimeZone(latitude, longitude);
        var ianaId = tzResult.Result;

        if (string.IsNullOrEmpty(ianaId))
            throw new InvalidOperationException("Could not determine timezone for the given coordinates.");

        // Resolve the TimeZoneInfo from the IANA ID
        // On Linux, TimeZoneInfo natively supports IANA IDs
        var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaId);

        var utcOffset = tz.GetUtcOffset(now);
        var isDst = tz.IsDaylightSavingTime(now);

        // Find the next DST transition
        DateTimeOffset? nextTransition = FindNextTransition(tz, now);

        // Current local time in this timezone
        var localTime = TimeZoneInfo.ConvertTime(now, tz);

        return new TimezoneResult(
            IanaTimezone: ianaId,
            UtcOffset: FormatOffset(utcOffset),
            UtcOffsetSeconds: (int)utcOffset.TotalSeconds,
            IsDst: isDst,
            CurrentLocalTime: localTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            NextTransition: nextTransition?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    private static DateTimeOffset? FindNextTransition(TimeZoneInfo tz, DateTimeOffset fromUtc)
    {
        var rules = tz.GetAdjustmentRules();
        if (rules.Length == 0)
            return null;

        // Search up to 2 years ahead for the next transition
        var searchEnd = fromUtc.AddYears(2);
        var current = fromUtc;

        // Step through time in monthly increments to find transitions
        var currentIsDst = tz.IsDaylightSavingTime(current);
        var step = TimeSpan.FromDays(1);

        while (current < searchEnd)
        {
            var next = current.Add(step);
            var nextIsDst = tz.IsDaylightSavingTime(next);

            if (nextIsDst != currentIsDst)
            {
                // Found a transition somewhere between current and next
                // Binary search for the exact transition point
                return BinarySearchTransition(tz, current, next, currentIsDst);
            }

            current = next;
            currentIsDst = nextIsDst;
        }

        return null;
    }

    private static DateTimeOffset BinarySearchTransition(
        TimeZoneInfo tz, DateTimeOffset low, DateTimeOffset high, bool lowIsDst)
    {
        // Narrow down to within 1 minute
        while ((high - low).TotalMinutes > 1)
        {
            var mid = low + (high - low) / 2;
            if (tz.IsDaylightSavingTime(mid) == lowIsDst)
                low = mid;
            else
                high = mid;
        }

        return high;
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"{sign}{abs.Hours:D2}:{abs.Minutes:D2}";
    }
}

public record TimezoneResult(
    string IanaTimezone,
    string UtcOffset,
    int UtcOffsetSeconds,
    bool IsDst,
    string CurrentLocalTime,
    string? NextTransition);
