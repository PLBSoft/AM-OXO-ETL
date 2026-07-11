using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Entities;

public class ExtractionConfigTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesExtractionConfig()
    {
        var config = new ExtractionConfig("Standard Purchase Order Template");

        config.Name.Should().Be("Standard Purchase Order Template");
        config.Id.Should().NotBeEmpty();
        config.Sheets.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        var act = () => new ExtractionConfig(invalidName!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionConfig_EmptyName);
    }

    [Fact]
    public void AddSheet_WithNewSheet_AddsToCollection()
    {
        var config = new ExtractionConfig("Standard Purchase Order Template");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);

        config.AddSheet(sheet);

        config.Sheets.Should().ContainSingle().Which.Should().Be(sheet);
    }

    [Fact]
    public void AddSheet_WithDuplicateSheetIndex_ThrowsInvalidOperationException()
    {
        var config = new ExtractionConfig("Standard Purchase Order Template");
        config.AddSheet(new SheetConfig("Summary", sheetIndex: 0));

        var act = () => config.AddSheet(new SheetConfig("Details", sheetIndex: 0));

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*index 0*")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionConfig_DuplicateSheetIndex);
    }

    [Fact]
    public void AddSheet_WithNull_ThrowsArgumentNullException()
    {
        var config = new ExtractionConfig("Standard Purchase Order Template");

        var act = () => config.AddSheet(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSheet_BeyondFiveSheets_ThrowsInvalidOperationException()
    {
        var config = new ExtractionConfig("Standard Purchase Order Template");
        for (var i = 0; i < 5; i++)
        {
            config.AddSheet(new SheetConfig($"Sheet{i}", sheetIndex: i));
        }

        var act = () => config.AddSheet(new SheetConfig("SixthSheet", sheetIndex: 5));

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*4-5*")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ExtractionConfig_TooManySheets);
    }
}
