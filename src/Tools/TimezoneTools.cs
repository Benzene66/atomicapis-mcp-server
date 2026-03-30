using System.ComponentModel;
using TimezoneResolver;
using ModelContextProtocol.Server;

namespace AtomicApisMcpServer.Tools;

[McpServerToolType]
public static class TimezoneTools
{
    [McpServerTool, Description(
        "Resolves the timezone for given geographic coordinates. Returns IANA timezone ID, " +
        "current UTC offset, DST status, local time, and next DST transition. " +
        "Uses offline timezone shape data — no external API calls needed.")]
    public static string ResolveTimezone(
        [Description("Latitude (-90 to 90)")] double latitude,
        [Description("Longitude (-180 to 180)")] double longitude)
    {
        var result = TimezoneService.Resolve(latitude, longitude);

        var response = $"""
            IANA Timezone: {result.IanaTimezone}
            UTC Offset: {result.UtcOffset}
            UTC Offset (seconds): {result.UtcOffsetSeconds}
            Is DST: {result.IsDst}
            Current Local Time: {result.CurrentLocalTime:yyyy-MM-dd HH:mm:ss}
            """;

        if (result.NextTransition != null)
        {
            response += $"\nNext DST Transition: {result.NextTransition:yyyy-MM-dd HH:mm:ss} UTC";
        }

        return response;
    }
}
