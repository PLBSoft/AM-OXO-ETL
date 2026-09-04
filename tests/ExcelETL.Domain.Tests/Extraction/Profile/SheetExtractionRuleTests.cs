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
        IReadOnlyList<string> unconditionalColonneNames = ["PROLOCK VANNES", "DEPROLOCK VANNES"];

        var rule = new SheetExtractionRule("ISOLEMENT", locator, pointRules, unconditionalColonneNames, [], []);

        rule.SheetName.Should().Be("ISOLEMENT");
        rule.Locator.Should().Be(locator);
        rule.PointRules.Should().BeEquivalentTo(pointRules);
        rule.UnconditionalColonneNames.Should().BeEquivalentTo(unconditionalColonneNames);
        rule.HeaderFields.Should().BeEmpty();
        rule.HeaderComposites.Should().BeEmpty();
        rule.ZeroEnergieExpectedValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithZeroEnergieExpectedValue_AssignsProperty()
    {
        var rule = new SheetExtractionRule(
            "ISOLEMENT", Locator("ISOLEMENT"), [], [], [], [], zeroEnergieExpectedValue: "ZERO ENERGIE");

        rule.ZeroEnergieExpectedValue.Should().Be("ZERO ENERGIE");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankZeroEnergieExpectedValue_ThrowsDomainValidationException(string blankValue)
    {
        var act = () => new SheetExtractionRule(
            "ISOLEMENT", Locator("ISOLEMENT"), [], [], [], [], zeroEnergieExpectedValue: blankValue);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("zeroEnergieExpectedValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_BlankZeroEnergieExpectedValue);
    }

    [Fact]
    public void Constructor_WithEmptyPointRules_CreatesSheetExtractionRule()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.PointRules.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyUnconditionalColonneNames_CreatesSheetExtractionRule()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.UnconditionalColonneNames.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNoFieldPresencePointRulesArgument_DefaultsToEmptyList()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.FieldPresencePointRules.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithFieldPresencePointRules_PassesThemThrough()
    {
        var fieldPresenceRule = new FieldPresencePointRule(
            new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD");

        var rule = new SheetExtractionRule(
            "PLATINES", Locator("PLATINES"), [], [], [], [],
            fieldPresencePointRules: [fieldPresenceRule]);

        rule.FieldPresencePointRules.Should().ContainSingle().Which.Should().Be(fieldPresenceRule);
    }

    [Fact]
    public void Constructor_WithNoCouleurEtiquetteCellArgument_DefaultsToNull()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.CouleurEtiquetteCell.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithCouleurEtiquetteCell_AssignsProperty()
    {
        var couleurEtiquetteCell = new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1);

        var rule = new SheetExtractionRule(
            "PLATINES", Locator("PLATINES"), [], [], [], [],
            couleurEtiquetteCell: couleurEtiquetteCell);

        rule.CouleurEtiquetteCell.Should().Be(couleurEtiquetteCell);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheetName_ThrowsDomainValidationException(string? invalidSheetName)
    {
        var act = () => new SheetExtractionRule(invalidSheetName!, Locator("ISOLEMENT"), [], [], [], []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheetName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_EmptySheetName);
    }

    [Fact]
    public void Constructor_WithNullLocator_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", null!, [], [], [], []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPointRules_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), null!, [], [], []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUnconditionalColonneNames_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], null!, [], []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullHeaderFields_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullHeaderComposites_ThrowsArgumentNullException()
    {
        var act = () => new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithSheetNameNotMatchingLocatorSheet_ThrowsDomainRuleViolationException()
    {
        var act = () => new SheetExtractionRule("PLATINES", Locator("ISOLEMENT"), [], [], [], []);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_SheetNameLocatorMismatch);
    }

    [Fact]
    public void Constructor_WithHeaderFieldsAndMatchingComposite_CreatesSheetExtractionRule()
    {
        IReadOnlyList<HeaderFieldRule> headerFields =
        [
            new HeaderFieldRule("revision", new DirectCell("PROCEDURE", "P2:Q2")),
            new HeaderFieldRule("dateRev", new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy")
        ];
        IReadOnlyList<HeaderCompositeRule> headerComposites =
        [
            new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")
        ];

        var rule = new SheetExtractionRule("PROCEDURE", Locator("PROCEDURE"), [], [], headerFields, headerComposites);

        rule.HeaderFields.Should().BeEquivalentTo(headerFields);
        rule.HeaderComposites.Should().BeEquivalentTo(headerComposites);
    }

    [Fact]
    public void Constructor_WithCompositeReferencingUnknownPlaceholder_ThrowsDomainRuleViolationException()
    {
        IReadOnlyList<HeaderFieldRule> headerFields = [new HeaderFieldRule("revision", new DirectCell("PROCEDURE", "P2:Q2"))];
        IReadOnlyList<HeaderCompositeRule> headerComposites = [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")];

        var act = () => new SheetExtractionRule("PROCEDURE", Locator("PROCEDURE"), [], [], headerFields, headerComposites);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_HeaderCompositeReferencesUnknownField);
    }
}
