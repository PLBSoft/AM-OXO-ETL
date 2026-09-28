using ExcelETL.Application.Archiving;
using ExcelETL.Application.Home;
using ExcelETL.Domain.Archiving;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Home;

// Lot 088 (088.2): pure function, real time zones (IANA ids, resolved by .NET on Windows through
// ICU) so daylight saving time is the real one, not a hand-built approximation.
public class GenerationActivityBuilderTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static readonly TimeZoneInfo Noumea = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Noumea");

    private static readonly DateTime NowUtc = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static GeneratedFileActivityEntry Entry(
        DateTime generatedAtUtc, GeneratedFileArchiveStatus status = GeneratedFileArchiveStatus.Success) =>
        new(generatedAtUtc, status);

    private static DailyGenerationActivity DayOf(IReadOnlyList<DailyGenerationActivity> days, int month, int day) =>
        days.Single(d => d.Day == new DateOnly(2026, month, day));

    [Fact]
    public void Build_WithNoEntries_ReturnsThirtyEmptyDays_FromOldestToToday()
    {
        var days = GenerationActivityBuilder.Build([], Paris, NowUtc);

        days.Should().HaveCount(GenerationActivityBuilder.DayCount);
        days.Select(d => d.Day).Should().Equal(
            Enumerable.Range(0, 30).Select(i => new DateOnly(2026, 8, 30).AddDays(i)));
        days.Should().OnlyContain(d => d.Success == 0 && d.Warning == 0 && d.Rejected == 0 && d.Total == 0);
    }

    [Fact]
    public void Build_CountsEachStatusInItsOwnColumn()
    {
        var entries = new[]
        {
            Entry(Utc(9, 27, 8)),
            Entry(Utc(9, 27, 9)),
            Entry(Utc(9, 27, 10), GeneratedFileArchiveStatus.NonBlockingWarning),
            Entry(Utc(9, 27, 11), GeneratedFileArchiveStatus.Rejected),
        };

        var day = DayOf(GenerationActivityBuilder.Build(entries, Paris, NowUtc), 9, 27);

        day.Success.Should().Be(2);
        day.Warning.Should().Be(1);
        day.Rejected.Should().Be(1);
        day.Total.Should().Be(4);
    }

    [Fact]
    public void Build_PlacesAFileOnTheDayOfTheGivenTimeZone()
    {
        var lateEvening = Entry(Utc(9, 27, 22));
        var beforeMidnightInParis = Entry(Utc(9, 27, 21, 30));

        var noumea = GenerationActivityBuilder.Build([lateEvening], Noumea, NowUtc);
        var paris = GenerationActivityBuilder.Build([lateEvening, beforeMidnightInParis], Paris, NowUtc);

        DayOf(noumea, 9, 28).Total.Should().Be(1);
        DayOf(noumea, 9, 27).Total.Should().Be(0);
        DayOf(paris, 9, 28).Total.Should().Be(1);
        DayOf(paris, 9, 27).Total.Should().Be(1);
    }

    [Fact]
    public void Build_AcrossADaylightSavingChange_KeepsEachFileOnItsLocalDay()
    {
        // Paris switches from UTC+2 to UTC+1 during the night of 25/10/2026.
        var nowUtc = Utc(11, 5, 10);
        var beforeChange = Entry(Utc(10, 24, 22, 30)); // 25/10 00:30 (UTC+2)
        var afterChange = Entry(Utc(10, 26, 23, 30)); // 27/10 00:30 (UTC+1)

        var days = GenerationActivityBuilder.Build([beforeChange, afterChange], Paris, nowUtc);

        DayOf(days, 10, 25).Total.Should().Be(1);
        DayOf(days, 10, 27).Total.Should().Be(1);
        days.Sum(d => d.Total).Should().Be(2);
    }

    [Fact]
    public void Build_IgnoresFilesOutsideTheThirtyLocalDays()
    {
        var justBefore = Entry(Utc(8, 29, 21, 59)); // 29/08 23:59 in Paris
        var firstMinute = Entry(Utc(8, 29, 22)); // 30/08 00:00 in Paris
        var tomorrow = Entry(Utc(9, 28, 23)); // 29/09 01:00 in Paris

        var days = GenerationActivityBuilder.Build([justBefore, firstMinute, tomorrow], Paris, NowUtc);

        days.Sum(d => d.Total).Should().Be(1);
        DayOf(days, 8, 30).Total.Should().Be(1);
    }

    [Fact]
    public void Build_TreatsAnUnspecifiedKindAsUtc()
    {
        // Values read back by EF Core come with DateTimeKind.Unspecified.
        var unspecified = Entry(DateTime.SpecifyKind(Utc(9, 27, 22), DateTimeKind.Unspecified));

        var days = GenerationActivityBuilder.Build([unspecified], Noumea, NowUtc);

        DayOf(days, 9, 28).Total.Should().Be(1);
    }
}
