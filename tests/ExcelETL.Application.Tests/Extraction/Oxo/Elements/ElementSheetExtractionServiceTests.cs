using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Elements;

// Lot 084.4 (docs/tickets/tickets-tdd-lot-084-moteur-generique-feuilles-elements.md).
public class ElementSheetExtractionServiceTests
{
    private const string Sheet = "PLATINES";

    private readonly ElementSheetExtractionService _sut = new(
        new RepeatingBlockReader(), new ConditionalPointRuleEvaluator(),
        new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ElementSheetExtractionService>.Instance);

    // PLATINES-shaped block: first block at row 17, step 8.
    private static readonly BlockFieldDefinition Identification = new("Identification", "B:E", 0, 1);
    private static readonly BlockFieldDefinition Designation = new("Designation", "H:V", -1, 0);
    private static readonly BlockFieldDefinition TypeElement = new("TypeElement", "B:E", 3, 5);

    private static SheetExtractionRule Rule(
        IReadOnlyList<BlockFieldDefinition>? extraFields = null,
        IReadOnlyList<ConditionalPointRule>? pointRules = null,
        IReadOnlyList<string>? unconditional = null,
        IReadOnlyList<HeaderFieldRule>? headerFields = null,
        bool warn = false,
        string? defaultCouleur = null,
        IReadOnlyList<string>? allowedCouleurs = null,
        IReadOnlyList<BlockFieldDefinition>? fields = null) =>
        new(
            Sheet,
            new RepeatingBlockLocator(Sheet, 17, 8, "Identification",
                fields ?? [Identification, Designation, TypeElement, .. extraFields ?? []]),
            pointRules ?? [],
            unconditional ?? [],
            headerFields ?? [new HeaderFieldRule("repereEcho", new DirectCell(Sheet, "K6:U6"))],
            [],
            defaultCouleurEtiquette: defaultCouleur,
            allowedCouleursEtiquette: allowedCouleurs,
            warnWhenNoConditionalPoint: warn);

    // Two PLATINE blocks (rows 17 and 25), third block empty.
    private static Dictionary<string, string?> TwoBlocks() => new()
    {
        ["K6:U6"] = "C7401",
        ["B17:E18"] = "PT1", ["H16:V17"] = "Aspiration", ["B20:E22"] = "PLATINE",
        ["B25:E26"] = "PT2", ["H24:V25"] = "Refoulement", ["B28:E30"] = "PLATINE",
        ["B33:E34"] = null
    };

    private static IWorkbookReader Reader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock.Object;
    }

    private static readonly BlockFieldDefinition HasDebMad = new("HasDebMad", "H:N", 2, 2, isRequired: false);
    private static readonly BlockFieldDefinition HasDebRel = new("HasDebRel", "H:N", 3, 3, isRequired: false);

    [Fact]
    public void Extract_TheJ2M76CaseAsTheClientEnteredIt_TicksTheColonnes_AndKeepsEveryBlock()
    {
        var cells = TwoBlocks();
        cells["H19:N19"] = "DEBUT MAD";
        cells["H20:N20"] = " debut rel ";
        var rule = Rule(
            extraFields: [HasDebMad, HasDebRel],
            pointRules:
            [
                new ConditionalPointRule("HasDebMad", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD"),
                new ConditionalPointRule("HasDebRel", ConditionOperator.Equals, "DEBUT REL", "RECEPTION DEBUT REL")
            ]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Errors.Should().BeEmpty();
        result.Elements.Select(e => e.Repere).Should().Equal("C7401-PT1", "C7401-PT2");
        result.Points.Should().Equal(
            new PointPivot("RECEPTION DEBUT MAD", "C7401-PT1"),
            new PointPivot("RECEPTION DEBUT REL", "C7401-PT1"));
    }

    [Fact]
    public void Extract_FillsThePivotFromTheKnownFieldNames()
    {
        var cells = TwoBlocks();
        cells["H18:O19"] = "FERMÉE";
        var rule = Rule(extraFields: [new BlockFieldDefinition("PositionALaPose", "H:O", 1, 2, isRequired: false)]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Elements[0].Should().BeEquivalentTo(new
        {
            Repere = "C7401-PT1", Designation = "Aspiration", TypeElementNom = "PLATINE", PositionALaPose = "FERMÉE",
            SourceSheetName = Sheet, CouleurEtiquette = ""
        });
        result.Elements[1].PositionALaPose.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithoutDesignationOrPositionFields_LeavesThemEmpty()
    {
        var result = _sut.Extract(Reader(TwoBlocks()), Rule(fields: [Identification, TypeElement]), "MAD-OXO-");

        result.Elements.Should().OnlyContain(e => e.Designation == "" && e.PositionALaPose == "");
    }

    [Fact]
    public void Extract_NotEqualsOnABlankOptionalField_TicksTheColonne()
    {
        var rule = Rule(
            extraFields: [HasDebMad],
            pointRules: [new ConditionalPointRule("HasDebMad", ConditionOperator.NotEquals, "FIN MAD", "A")]);

        var result = _sut.Extract(Reader(TwoBlocks()), rule, "MAD-OXO-");

        result.Points.Should().Equal(new PointPivot("A", "C7401-PT1"), new PointPivot("A", "C7401-PT2"));
    }

    [Fact]
    public void Extract_IsNotBlank_TicksOnlyTheBlocksWhoseFieldHoldsAValue()
    {
        var cells = TwoBlocks();
        cells["H27:N27"] = "FIN MAD";
        cells["H19:N19"] = "   ";
        var rule = Rule(
            extraFields: [HasDebMad],
            pointRules: [new ConditionalPointRule("HasDebMad", ConditionOperator.IsNotBlank, null, "RENSEIGNÉ")]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Points.Should().Equal(new PointPivot("RENSEIGNÉ", "C7401-PT2"));
    }

    [Fact]
    public void Extract_TwoRulesOfTheSameColonneSatisfied_CreateASinglePoint()
    {
        var cells = TwoBlocks();
        cells["H19:N19"] = "DEBUT MAD";
        cells["H20:N20"] = "DEBUT MAD";
        var rule = Rule(
            extraFields: [HasDebMad, HasDebRel],
            pointRules:
            [
                new ConditionalPointRule("HasDebMad", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD"),
                new ConditionalPointRule("HasDebRel", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD")
            ]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Points.Should().ContainSingle().Which.Should().Be(new PointPivot("RECEPTION DEBUT MAD", "C7401-PT1"));
    }

    [Fact]
    public void Extract_UnconditionalColonnesComeFirst_ThenConditionalOnesInRuleOrder()
    {
        var cells = TwoBlocks();
        cells["H19:N19"] = "DEBUT MAD";
        var rule = Rule(
            extraFields: [HasDebMad],
            unconditional: ["U1", "U2"],
            pointRules:
            [
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "PLATINE", "C1"),
                new ConditionalPointRule("HasDebMad", ConditionOperator.IsNotBlank, null, "C2")
            ]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Points.Where(p => p.ParentRepere == "C7401-PT1").Select(p => p.ColonneNom).Should().Equal("U1", "U2", "C1", "C2");
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Extract_NoConditionalPointWarning_FollowsTheSheetSetting(bool warn, int expectedWarnings)
    {
        var rule = Rule(
            warn: warn,
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")]);

        var result = _sut.Extract(Reader(TwoBlocks()), rule, "MAD-OXO-");

        // Both PLATINE elements miss the rule, but the warning is reported once per value.
        result.Errors.Where(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated).Should().HaveCount(expectedWarnings);
        if (warn)
        {
            var warning = result.Errors.Single();
            warning.ExtractedValue.Should().Be("PLATINE");
            warning.BlockIdentifier.Should().Be("C7401-PT1");
            warning.Sheet.Should().Be(Sheet);
        }
    }

    [Fact]
    public void Extract_WarningSetButNoPointRule_ReportsNothing()
    {
        var result = _sut.Extract(Reader(TwoBlocks()), Rule(warn: true, unconditional: ["U1"]), "MAD-OXO-");

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WarningNotReported_WhenOneColonneIsTicked()
    {
        var rule = Rule(
            warn: true,
            pointRules:
            [
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A"),
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "PLATINE", "B")
            ]);

        var result = _sut.Extract(Reader(TwoBlocks()), rule, "MAD-OXO-");

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_BlankRequiredField_DropsTheBlockWithRequiredFieldMissing()
    {
        var cells = TwoBlocks();
        cells["B28:E30"] = null;

        var result = _sut.Extract(Reader(cells), Rule(), "MAD-OXO-");

        result.Elements.Should().ContainSingle();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
    }

    [Theory]
    [InlineData("Identification")]
    [InlineData("TypeElement")]
    public void Extract_WithoutAMandatoryKnownField_ThrowsAConfigurationError(string missingField)
    {
        var fields = new[] { Identification, Designation, TypeElement }.Where(f => f.Name != missingField).ToList();
        var rule = new SheetExtractionRule(
            Sheet, new RepeatingBlockLocator(Sheet, 17, 8, fields[0].Name, fields), [], [],
            [new HeaderFieldRule("repereEcho", new DirectCell(Sheet, "K6:U6"))], []);

        var act = () => _sut.Extract(Reader(TwoBlocks()), rule, "MAD-OXO-");

        act.Should().Throw<UnknownFieldReferenceException>().Which.FieldName.Should().Be(missingField);
    }

    [Fact]
    public void Extract_WithoutTheRepereEchoHeaderField_ThrowsAConfigurationError()
    {
        var act = () => _sut.Extract(Reader(TwoBlocks()), Rule(headerFields: []), "MAD-OXO-");

        act.Should().Throw<UnknownFieldReferenceException>().Which.FieldName.Should().Be("repereEcho");
    }

    [Theory]
    [InlineData("K6:U6", "C7401")]
    [InlineData("N6", "AUTRE")]
    public void Extract_ReadsTheRepereFromTheProfilesOwnHeaderCell_NotAHardcodedOne(string range, string expectedPrefix)
    {
        var cells = TwoBlocks();
        cells["N6"] = "AUTRE";
        var rule = Rule(headerFields: [new HeaderFieldRule("repereEcho", new DirectCell(Sheet, range))]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Elements[0].Repere.Should().Be($"{expectedPrefix}-PT1");
    }

    [Theory]
    [InlineData("B6:E6", "ZONE 1")]
    [InlineData("C3", "ZONE 2")]
    public void Extract_ReadsTheZoneFromTheProfilesOwnHeaderCell_NotAHardcodedOne(string range, string expectedZone)
    {
        var cells = TwoBlocks();
        cells["B6:E6"] = "ZONE 1";
        cells["C3"] = "ZONE 2";
        var rule = Rule(headerFields:
        [
            new HeaderFieldRule("repereEcho", new DirectCell(Sheet, "K6:U6")),
            new HeaderFieldRule("zone", new DirectCell(Sheet, range))
        ]);

        _sut.Extract(Reader(cells), rule, "MAD-OXO-").Zone.Should().Be(expectedZone);
    }

    [Fact]
    public void Extract_WithoutZoneHeaderField_ReturnsAnEmptyZone()
    {
        _sut.Extract(Reader(TwoBlocks()), Rule(), "MAD-OXO-").Zone.Should().BeEmpty();
    }

    private static readonly BlockFieldDefinition CouleurField = new("CouleurEtiquette", "H:N", 1, 1, isRequired: false);

    [Fact]
    public void Extract_CouleurFromTheBlockField_UsesTheAllowedSpelling_AndWarnsOnceForAnUnexpectedValue()
    {
        var cells = TwoBlocks();
        cells["H18:N18"] = " rouge ";
        cells["H26:N26"] = "DATE";
        var rule = Rule(extraFields: [CouleurField], allowedCouleurs: ["ROUGE", "BLANC"], defaultCouleur: "BLEUE");

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Elements.Select(e => e.CouleurEtiquette).Should().Equal("ROUGE", "");
        var warning = result.Errors.Should().ContainSingle().Which;
        warning.Code.Should().Be(ExtractionErrorCode.UnexpectedCouleurEtiquetteValue);
        warning.ExtractedValue.Should().Be("DATE");
    }

    [Fact]
    public void Extract_CouleurFieldWithoutAllowedList_KeepsTheTrimmedValue_AndBlankStaysBlank()
    {
        var cells = TwoBlocks();
        cells["H18:N18"] = " JAUNE ";
        var rule = Rule(extraFields: [CouleurField]);

        var result = _sut.Extract(Reader(cells), rule, "MAD-OXO-");

        result.Elements.Select(e => e.CouleurEtiquette).Should().Equal("JAUNE", "");
    }

    [Fact]
    public void Extract_WithoutCouleurField_UsesTheDefaultCouleur()
    {
        var result = _sut.Extract(Reader(TwoBlocks()), Rule(defaultCouleur: "BLEUE"), "MAD-OXO-");

        result.Elements.Should().OnlyContain(e => e.CouleurEtiquette == "BLEUE");
    }
}
