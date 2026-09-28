using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;

namespace ExcelETL.Application.Home;

// Lot 088 (088.2): groups archived files into the last DayCount local days (today included), in
// the time zone the caller gives -- the home page passes the browser's own (lot 064's rule: times are
// shown in the viewer's time zone, never the server's). Pure: the clock and the time zone are
// parameters, so the day boundaries (daylight saving time included) are testable.
public static class GenerationActivityBuilder
{
    public const int DayCount = 30;

    public static IReadOnlyList<DailyGenerationActivity> Build(
        IReadOnlyList<GeneratedFileActivityEntry> entries, TimeZoneInfo timeZone, DateTime nowUtc)
    {
        var today = LocalDay(nowUtc, timeZone);
        var firstDay = today.AddDays(-(DayCount - 1));

        var countsByDay = entries
            .Select(entry => (Day: LocalDay(entry.GeneratedAtUtc, timeZone), entry.Status))
            .Where(entry => entry.Day >= firstDay && entry.Day <= today)
            .GroupBy(entry => entry.Day)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Status).ToList());

        return Enumerable.Range(0, DayCount)
            .Select(offset =>
            {
                var day = firstDay.AddDays(offset);
                var statuses = countsByDay.GetValueOrDefault(day) ?? [];
                return new DailyGenerationActivity(
                    day,
                    statuses.Count(s => s == GeneratedFileArchiveStatus.Success),
                    statuses.Count(s => s == GeneratedFileArchiveStatus.NonBlockingWarning),
                    statuses.Count(s => s == GeneratedFileArchiveStatus.Rejected));
            })
            .ToList();
    }

    // Values read back by EF Core carry DateTimeKind.Unspecified; every archived date is UTC.
    private static DateOnly LocalDay(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));
}
