using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class HeaderRuleResolverTests
{
    private const string Sheet = "PROCEDURE";
    private const string ReperePrefix = "MAD-OXO-";

    private readonly HeaderRuleResolver _sut = new(new TextTransformEvaluator());

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(Sheet, It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock;
    }

    private static SheetExtractionRule CreateSheetRule(
        IReadOnlyList<HeaderFieldRule> headerFields, IReadOnlyList<HeaderCompositeRule> headerComposites) => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 1, "Action", [new BlockFieldDefinition("Action", "C:L", 0, 0)]),
        [],
        [],
        headerFields,
        headerComposites);

    [Fact]
    public void Resolve_WithDirectFieldNoTransforms_ReturnsRawValueAsIs()
    {
        var rule = CreateSheetRule([new HeaderFieldRule("repereEcho", new DirectCell(Sheet, "N6"))], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["N6"] = "38-C7401" });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Fields["repereEcho"].Value.Should().Be("38-C7401");
        result.Fields["repereEcho"].RawValue.Should().Be("38-C7401");
        result.Fields["repereEcho"].ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Resolve_WithStripReperePrefixTrue_RemovesOnlyTheProfilePrefix()
    {
        var rule = CreateSheetRule(
            [new HeaderFieldRule("nomMAD", new DirectCell(Sheet, "M2:O2"), stripReperePrefix: true)], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["M2:O2"] = "MAD-OXO-38-C7401" });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Fields["nomMAD"].Value.Should().Be("38-C7401");
    }

    [Fact]
    public void Resolve_WithStripReperePrefixTrueAndValueNotStartingWithPrefix_ReturnsNullValueAndErrorMessage()
    {
        var rule = CreateSheetRule(
            [new HeaderFieldRule("nomMAD", new DirectCell(Sheet, "M2:O2"), stripReperePrefix: true)], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["M2:O2"] = "OTHER-38-C7401" });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Fields["nomMAD"].Value.Should().BeNull();
        result.Fields["nomMAD"].RawValue.Should().Be("OTHER-38-C7401");
        result.Fields["nomMAD"].ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WithDateFormat_ReformatsTheDateExactly()
    {
        var rule = CreateSheetRule(
            [new HeaderFieldRule("dateRev", new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["R2:T2"] = "12/12/2025 00:00:00" });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Fields["dateRev"].Value.Should().Be("12/12/2025");
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData(null)]
    public void Resolve_WithDateFormatAndUnparsableRawValue_ReturnsNullValueAndErrorMessage(string? rawDate)
    {
        var rule = CreateSheetRule(
            [new HeaderFieldRule("dateRev", new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["R2:T2"] = rawDate });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Fields["dateRev"].Value.Should().BeNull();
        result.Fields["dateRev"].ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WithComposite_SubstitutesResolvedFieldValuesIntoTemplate()
    {
        var rule = CreateSheetRule(
        [
            new HeaderFieldRule("revision", new DirectCell(Sheet, "P2:Q2")),
            new HeaderFieldRule("dateRev", new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")
        ],
        [
            new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")
        ]);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?>
        {
            ["P2:Q2"] = "2",
            ["R2:T2"] = "12/12/2025 00:00:00"
        });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Composites["Designation"].Should().Be("Rév 2 du 12/12/2025");
    }

    [Fact]
    public void Resolve_WithCompositeReferencingUnresolvedField_SubstitutesEmptyString()
    {
        var rule = CreateSheetRule(
        [
            new HeaderFieldRule("dateRev", new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")
        ],
        [
            new HeaderCompositeRule("Designation", "Rév du {dateRev}")
        ]);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?> { ["R2:T2"] = "not-a-date" });

        var result = _sut.Resolve(workbookReader.Object, rule, ReperePrefix);

        result.Composites["Designation"].Should().Be("Rév du ");
    }

    [Fact]
    public void Resolve_WithTwoDifferentProfileCoordinates_ReadsFromWhicheverCellTheProfileDeclares()
    {
        // Anti-hardcoding guard-rail (ticket 47.5's own pattern, Lot C1's EquipementTypeElementNom
        // precedent): the resolver must never assume a literal cell coordinate.
        var ruleA = CreateSheetRule([new HeaderFieldRule("nomMAD", new DirectCell(Sheet, "M2:O2"))], []);
        var ruleB = CreateSheetRule([new HeaderFieldRule("nomMAD", new DirectCell(Sheet, "X9:Y9"))], []);
        var workbookReader = CreateWorkbookReader(new Dictionary<string, string?>
        {
            ["M2:O2"] = "FROM-M2O2",
            ["X9:Y9"] = "FROM-X9Y9"
        });

        _sut.Resolve(workbookReader.Object, ruleA, ReperePrefix).Fields["nomMAD"].Value.Should().Be("FROM-M2O2");
        _sut.Resolve(workbookReader.Object, ruleB, ReperePrefix).Fields["nomMAD"].Value.Should().Be("FROM-X9Y9");
    }

    // "Placeholder inconnu -> erreur typée" (ticket 47.2's own test list): SheetExtractionRule's
    // construction-time cross-validation (SheetExtractionRuleTests, Domain) already makes this
    // unreachable through the resolver's only public entry point, Resolve(SheetExtractionRule, ...) --
    // a SheetExtractionRule with a composite referencing an unknown field simply cannot be constructed.
    // SubstituteTemplate's own defensive UnknownFieldReferenceException throw (same typed-exception
    // precedent as TextTransformEvaluator's Concat/FieldRef) is exercised as dead-code-for-now defense
    // in depth, per the ticket's own 47.1 recommendation to put the real validation on
    // SheetExtractionRule -- not duplicated here as a reflection-bypassing test.

    [Fact]
    public void Resolve_WithNullWorkbookReader_ThrowsArgumentNullException()
    {
        var act = () => _sut.Resolve(null!, CreateSheetRule([], []), ReperePrefix);

        act.Should().Throw<ArgumentNullException>();
    }
}
