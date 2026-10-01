using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Archiving;

public class ArchiveRetentionSettingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(ArchiveRetentionSetting.MaxRetentionDays)]
    public void Constructor_WithDaysInRange_Stores(int days)
    {
        new ArchiveRetentionSetting(days).RetentionDays.Should().Be(days);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ArchiveRetentionSetting.MaxRetentionDays + 1)]
    public void Constructor_WithDaysOutOfRange_Throws(int days)
    {
        var act = () => new ArchiveRetentionSetting(days);

        act.Should().Throw<DomainArgumentOutOfRangeException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ArchiveRetentionSetting_DaysOutOfRange);
    }

    [Fact]
    public void ChangeRetentionDays_ValidatesAndUpdates()
    {
        var setting = new ArchiveRetentionSetting(90);

        setting.ChangeRetentionDays(30);
        setting.RetentionDays.Should().Be(30);

        var act = () => setting.ChangeRetentionDays(-5);
        act.Should().Throw<DomainArgumentOutOfRangeException>();
        setting.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void DefaultRetentionDays_Is90()
    {
        ArchiveRetentionSetting.DefaultRetentionDays.Should().Be(90);
    }
}
