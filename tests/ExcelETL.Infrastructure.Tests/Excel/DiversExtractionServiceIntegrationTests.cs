using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs the DIVERS settings through ElementSheetExtractionService (lot 084; DiversExtractionService,
// Lot C6, before it) against the real ClosedXmlWorkbookReader and
// the 3 real client fixtures.
public class DiversExtractionServiceIntegrationTests
{
    private const string Sheet = "DIVERS";

    private const string InstrumentationColonne = "SYNCHRONISATION INSTRUMENTATION";
    private const string ZeroEnergieColonne = "ZÉRO ENERGIE EN PRESENCE EE";
    private const string SoupapeConstatColonne = "SOUPAPE : CONSTAT ENCRASSEMENT";
    private const string SoupapeReceptionColonne = "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS";
    private const string PfSignatureColonne = "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES";
    private const string PfValidationColonne = "PF : VALIDATION CONSTAT ENCRASSEMENT";
    private const string PfAccordColonne = "PF : ACCORD TRAVAUX FEU";
    private const string ReperePrefix = "OXO-";

    private readonly ElementSheetExtractionService _sut = new(
        new RepeatingBlockReader(), new ConditionalPointRuleEvaluator(),
        new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ElementSheetExtractionService>.Instance);

    // Lot 047: the "repereEcho" header rule (N6), transcribed from the coordinate previously
    // hardcoded in DiversExtractionService.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 3, ElementFieldNames.Identification,
        [
            new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:G", 0, 2),
            new BlockFieldDefinition(ElementFieldNames.Identification, "H:K", 0, 2),
            new BlockFieldDefinition(ElementFieldNames.Designation, "L:V", 0, 2)
        ]),
        [
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", InstrumentationColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeConstatColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeReceptionColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfSignatureColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfValidationColonne),
            new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfAccordColonne)
        ],
        [],
        [
            new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(Sheet, "N6")),
            // Lot 084.6 (G16): loc1 is the "zone" header field.
            new HeaderFieldRule(ElementFieldNames.ZoneHeader, new DirectCell(Sheet, "B6:E6"))
        ],
        [],
        warnWhenNoConditionalPoint: true);

    [Fact]
    public void Extract_C7401Fixture_ReturnsNoIsolementsButStillReadsLoc1()
    {
        // Confirmed against the real file: this dossier's DIVERS sheet has no repeating-block rows,
        // same "unused sheet" pattern as C4/C5 -- but loc1 (ZONE 1) is still present at the header.
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Zone.Should().Be("ZONE 1");
        result.Elements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsAllZeroEnergieIsolementsWithMatchingPoints()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Zone.Should().Be("ZONE 4");
        result.Elements.Should().HaveCount(13);
        result.Elements.Should().OnlyContain(i => i.TypeElementNom == "ZERO ENERGIE");
        result.Points.Should().HaveCount(13).And.OnlyContain(p => p.ColonneNom == ZeroEnergieColonne);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_G6306BFixture_CoversAllFourTypesIncludingThePointDeFeuMismatch()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Zone.Should().Be("ZONE 4");
        result.Elements.Should().HaveCount(4);
        result.Elements.Should().Contain(i => i.TypeElementNom == "INSTRUMENTATION")
            .And.Contain(i => i.TypeElementNom == "ZERO ENERGIE")
            .And.Contain(i => i.TypeElementNom.Trim() == "SOUPAPE")
            .And.Contain(i => i.TypeElementNom == "POINT DE FEU");

        result.Points.Select(p => p.ColonneNom).Should().BeEquivalentTo(
        [
            InstrumentationColonne,
            ZeroEnergieColonne,
            SoupapeConstatColonne,
            SoupapeReceptionColonne
        ]);

        // "POINT DE FEU" (real cell) vs "POINT FEU" (confirmed base value) is a genuine spelling
        // mismatch -- no PF Points created, exactly one aggregate warning for that one Isolement.
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.NoConditionalPointCreated);
    }

    private ElementSheetExtractionResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule(), ReperePrefix);
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the tests/Fixtures directory.");
        }

        return Path.Combine(directory.FullName, "Fixtures", fileName);
    }
}
