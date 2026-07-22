using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot N (docs/tickets-tdd-blazor-profil-import-lisibilite-plages-excel.md), N1. Pure xUnit, no bUnit
// needed -- BlockFieldRangeFormatter has no Razor/DI dependency of its own.
public class BlockFieldRangeFormatterTests
{
    [Fact]
    public void ToAbsoluteRange_IsolementIdentification_ReturnsAbsoluteExcelRange() =>
        BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow: 19, columnRange: "B:E", rowOffsetStart: 0, rowOffsetEnd: 1)
            .Should().Be("B19:E20");

    [Fact]
    public void ToAbsoluteRange_IsolementTypeElement_ReturnsAbsoluteExcelRange() =>
        BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow: 19, columnRange: "B:E", rowOffsetStart: 3, rowOffsetEnd: 4)
            .Should().Be("B22:E23");

    [Fact]
    public void ToAbsoluteRange_WithNegativeRowOffset_ReturnsAbsoluteExcelRange() =>
        BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow: 19, columnRange: "H:U", rowOffsetStart: -1, rowOffsetEnd: 0)
            .Should().Be("H18:U19");

    [Fact]
    public void ToAbsoluteRange_SingleColumnWithEqualOffsets_ReturnsSingleCell_NotARange() =>
        BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow: 9, columnRange: "B", rowOffsetStart: 0, rowOffsetEnd: 0)
            .Should().Be("B9");

    [Fact]
    public async Task FromAbsoluteRange_RoundTripsExactly_ForEveryBlockFieldInTheSeededDefaultImportProfile()
    {
        var dbContextFactory = new TestDbContextFactory("BlockFieldRangeFormatterTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        await seeder.SeedAsync();

        var profile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);

        foreach (var sheetRule in profile!.SheetRules)
        {
            foreach (var field in sheetRule.Locator.Fields)
            {
                var absoluteRange = BlockFieldRangeFormatter.ToAbsoluteRange(
                    sheetRule.Locator.FirstBlockStartRow, field.ColumnRange, field.RowOffsetStart, field.RowOffsetEnd);

                var result = BlockFieldRangeFormatter.FromAbsoluteRange(absoluteRange, sheetRule.Locator.FirstBlockStartRow);

                var because = $"{sheetRule.SheetName}.{field.Name} ({field.ColumnRange}, {field.RowOffsetStart}-{field.RowOffsetEnd}) -> {absoluteRange}";
                result.IsSuccess.Should().BeTrue(because);
                result.ColumnRange.Should().Be(field.ColumnRange, because);
                result.RowOffsetStart.Should().Be(field.RowOffsetStart, because);
                result.RowOffsetEnd.Should().Be(field.RowOffsetEnd, because);
            }
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("19:B20")]
    [InlineData("B19-E20")]
    [InlineData("")]
    public void FromAbsoluteRange_WithInvalidFormat_ReturnsFailure(string absoluteRange) =>
        BlockFieldRangeFormatter.FromAbsoluteRange(absoluteRange, firstBlockStartRow: 19).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithReversedRowOrder_ReturnsFailure() =>
        BlockFieldRangeFormatter.FromAbsoluteRange("B20:E19", firstBlockStartRow: 19).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithReversedColumnOrder_ReturnsFailure() =>
        BlockFieldRangeFormatter.FromAbsoluteRange("E19:B20", firstBlockStartRow: 19).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithColumnBeyondXfd_ReturnsFailure() =>
        BlockFieldRangeFormatter.FromAbsoluteRange("ZZZ1", firstBlockStartRow: 1).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithRowBeyondOneMillion048576_ReturnsFailure() =>
        BlockFieldRangeFormatter.FromAbsoluteRange("A1048577", firstBlockStartRow: 1).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithRowZero_ReturnsFailure() =>
        BlockFieldRangeFormatter.FromAbsoluteRange("A0", firstBlockStartRow: 1).IsSuccess.Should().BeFalse();

    [Fact]
    public void FromAbsoluteRange_WithColumnExactlyAz_DoesNotFlagBeyondPracticalRange()
    {
        var result = BlockFieldRangeFormatter.FromAbsoluteRange("AZ1", firstBlockStartRow: 1);

        result.IsSuccess.Should().BeTrue();
        result.IsBeyondPracticalRange.Should().BeFalse();
    }

    [Fact]
    public void FromAbsoluteRange_WithColumnJustBeyondAz_FlagsBeyondPracticalRange_ButStillSucceeds()
    {
        var result = BlockFieldRangeFormatter.FromAbsoluteRange("BA1", firstBlockStartRow: 1);

        result.IsSuccess.Should().BeTrue();
        result.IsBeyondPracticalRange.Should().BeTrue();
    }

    [Fact]
    public void FromAbsoluteRange_WithRowExactlyOneThousand_DoesNotFlagBeyondPracticalRange()
    {
        var result = BlockFieldRangeFormatter.FromAbsoluteRange("A1000", firstBlockStartRow: 1);

        result.IsSuccess.Should().BeTrue();
        result.IsBeyondPracticalRange.Should().BeFalse();
    }

    [Fact]
    public void FromAbsoluteRange_WithRowJustBeyondOneThousand_FlagsBeyondPracticalRange_ButStillSucceeds()
    {
        var result = BlockFieldRangeFormatter.FromAbsoluteRange("A1001", firstBlockStartRow: 1);

        result.IsSuccess.Should().BeTrue();
        result.IsBeyondPracticalRange.Should().BeTrue();
    }
}
