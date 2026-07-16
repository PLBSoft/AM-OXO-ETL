using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class SheetExtractionRuleTests
{
    private static RepeatingBlockLocator Locator(string sheet) => new(
        sheet, 19, 7, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);

    [Fact]
    public void Constructor_WithValidArguments_CreatesSheetExtractionRule()
    {
        var locator = Locator("ISOLEMENT");
        IReadOnlyList<ConditionalPointRule> pointRules =
        [
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")
        ];

        var rule = new SheetExtractionRule("ISOLEMENT", locator, pointRules);

        rule.SheetName.Should().Be("ISOLEMENT");
        rule.Locator.Should().Be(locator);
        rule.PointRules.Should().BeEquivalentTo(pointRules);
    }

    [Fact]
    public void Constructor_WithEmptyPointRules_CreatesSheetExtractionRule()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), []);

        rule.PointRules.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheetName_ThrowsDomainValidationException(string? invalidSheetName)
    {
        var act = () => new SheetExtractionRule(invalidSheetName!, Locator("ISOLEMENT"), []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_EmptySheetName);
    }

    [Fact]
    public void Constructor_WithNullLocator_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPointRules_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithSheetNameNotMatchingLocatorSheet_ThrowsDomainRuleViolationException()
    {
        var act = () => new SheetExtractionRule("PLATINES", Locator("ISOLEMENT"), []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_SheetNameLocatorMismatch);
    }
}
