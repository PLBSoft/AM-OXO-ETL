using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Profile;

public class SheetExtractionRuleTests
{
    private static RepeatingBlockLocator Locator(string sheet) => new(
        19, 7,
        [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("TypeElement", "B:E", 3, 4)]);

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
    public void Constructor_WithNoDefaultCouleurEtiquetteArgument_DefaultsToNull()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.DefaultCouleurEtiquette.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDefaultCouleurEtiquette_AssignsProperty()
    {
        var rule = new SheetExtractionRule(
            "AUTRES JOINTS TOUCHES", Locator("AUTRES JOINTS TOUCHES"), [], [], [], [],
            defaultCouleurEtiquette: "BLEUE");

        rule.DefaultCouleurEtiquette.Should().Be("BLEUE");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankDefaultCouleurEtiquette_ThrowsDomainValidationException(string blankValue)
    {
        var act = () => new SheetExtractionRule(
            "AUTRES JOINTS TOUCHES", Locator("AUTRES JOINTS TOUCHES"), [], [], [], [],
            defaultCouleurEtiquette: blankValue);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("defaultCouleurEtiquette")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_BlankDefaultCouleurEtiquette);
    }

    [Fact]
    public void Constructor_WithNoAllowedCouleursEtiquetteArgument_DefaultsToNull()
    {
        var rule = new SheetExtractionRule("PLATINES", Locator("PLATINES"), [], [], [], []);

        rule.AllowedCouleursEtiquette.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllowedCouleursEtiquette_AssignsProperty()
    {
        var rule = new SheetExtractionRule(
            "PLATINES", Locator("PLATINES"), [], [], [], [],
            allowedCouleursEtiquette: ["ROUGE", "BLEUE", "JAUNE"]);

        rule.AllowedCouleursEtiquette.Should().BeEquivalentTo(["ROUGE", "BLEUE", "JAUNE"]);
    }

    [Fact]
    public void Constructor_WithEmptyAllowedCouleursEtiquette_CreatesSheetExtractionRule()
    {
        var rule = new SheetExtractionRule(
            "PLATINES", Locator("PLATINES"), [], [], [], [],
            allowedCouleursEtiquette: []);

        rule.AllowedCouleursEtiquette.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankEntryInAllowedCouleursEtiquette_ThrowsDomainValidationException(string blankEntry)
    {
        var act = () => new SheetExtractionRule(
            "PLATINES", Locator("PLATINES"), [], [], [], [],
            allowedCouleursEtiquette: ["ROUGE", blankEntry]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("allowedCouleursEtiquette")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_BlankAllowedCouleurEtiquette);
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
    public void Constructor_WithHeaderFieldsAndMatchingComposite_CreatesSheetExtractionRule()
    {
        IReadOnlyList<HeaderFieldRule> headerFields =
        [
            new HeaderFieldRule("revision", "P2:Q2"),
            new HeaderFieldRule("dateRev", "R2:T2", dateFormat: "dd/MM/yyyy")
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
        IReadOnlyList<HeaderFieldRule> headerFields = [new HeaderFieldRule("revision", "P2:Q2")];
        IReadOnlyList<HeaderCompositeRule> headerComposites = [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")];

        var act = () => new SheetExtractionRule("PROCEDURE", Locator("PROCEDURE"), [], [], headerFields, headerComposites);

        act.Should().Throw<DomainRuleViolationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_HeaderCompositeReferencesUnknownField);
    }

    // Lot 084.1
    [Fact]
    public void Constructor_WithPointRuleOnFieldOutsideTheBlock_ThrowsDomainRuleViolationException()
    {
        IReadOnlyList<ConditionalPointRule> pointRules =
            [new ConditionalPointRule("HasDebMad", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD")];

        var act = () => new SheetExtractionRule("PLATINES", Locator("PLATINES"), pointRules, [], [], []);

        var exception = act.Should().Throw<DomainRuleViolationException>().Which;
        exception.ErrorCode.Should().Be(DomainErrorCode.SheetExtractionRule_PointRuleReferencesUnknownBlockField);
        exception.Args.Should().Equal("RECEPTION DEBUT MAD", "HasDebMad");
    }

    [Fact]
    public void Constructor_WithPointRuleOnABlockField_CreatesSheetExtractionRule()
    {
        var locator = new RepeatingBlockLocator(17, 8,
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            new BlockFieldDefinition("HasDebMad", "H:N", 2, 2, isRequired: false)
        ]);
        IReadOnlyList<ConditionalPointRule> pointRules =
            [new ConditionalPointRule("HasDebMad", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD")];

        var rule = new SheetExtractionRule("PLATINES", locator, pointRules, [], [], []);

        rule.PointRules.Should().Equal(pointRules);
    }

    [Fact]
    public void Constructor_WithNoWarnWhenNoConditionalPointArgument_DefaultsToFalse()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []);

        rule.WarnWhenNoConditionalPoint.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithWarnWhenNoConditionalPoint_AssignsProperty()
    {
        var rule = new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], [], warnWhenNoConditionalPoint: true);

        rule.WarnWhenNoConditionalPoint.Should().BeTrue();
    }
}
