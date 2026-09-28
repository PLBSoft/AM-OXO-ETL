namespace ExcelETL.BlazorAdmin.Services;

// Lot 088 (088.4): turns the IANA id the browser reports (ILocalTimeFormatter.GetBrowserTimeZoneIdAsync)
// into a TimeZoneInfo. .NET accepts IANA ids on Windows (through ICU), daylight saving time included.
// A missing or unknown id falls back to UTC -- the home page then keeps its UTC grouping, never fails.
public static class BrowserTimeZoneResolver
{
    public static TimeZoneInfo Resolve(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(ianaId.Trim(), out var timeZone)
            ? timeZone
            : TimeZoneInfo.Utc;
    }
}
