using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 088 (088.4): the browser reports an IANA id (Intl.DateTimeFormat().resolvedOptions().timeZone);
// .NET resolves it on Windows through ICU. Anything unusable falls back to UTC, never an exception.
public class BrowserTimeZoneResolverTests
{
    [Theory]
    [InlineData("Europe/Paris", 2)] // 28/09/2026: summer time
    [InlineData("Pacific/Noumea", 11)]
    public void Resolve_WithAKnownIanaId_ReturnsThatTimeZone(string ianaId, int expectedOffsetHours)
    {
        var timeZone = BrowserTimeZoneResolver.Resolve(ianaId);

        timeZone.GetUtcOffset(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc))
            .Should().Be(TimeSpan.FromHours(expectedOffsetHours));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/A_Real_Zone")]
    public void Resolve_WithAMissingOrUnknownId_FallsBackToUtc(string? ianaId)
    {
        BrowserTimeZoneResolver.Resolve(ianaId).Should().Be(TimeZoneInfo.Utc);
    }
}
