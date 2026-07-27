using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs DiversExtractionService (Application, Lot C6) against the real ClosedXmlWorkbookReader and
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
    private const string ReperePrefix = "MAD-OXO-";

    private readonly DiversExtractionService _sut =
        new(new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<DiversExtractionService>.Instance);

    // Lot 047: the "repereEcho" header rule (N6), transcribed from the coordinate previously
    // hardcoded in DiversExtractionService.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 3, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
            new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
        ]),
        [
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", InstrumentationColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeConstatColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeReceptionColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfSignatureColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfValidationColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfAccordColonne)
        ],
        [],
        [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(Sheet, "N6"))],
        []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsNoIsolementsButStillReadsLoc1()
    {
        // Confirmed against the real file: this dossier's DIVERS sheet has no repeating-block rows,
        // same "unused sheet" pattern as C4/C5 -- but loc1 (ZONE 1) is still present at the header.
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Loc1.Should().Be("ZONE 1");
        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsAllZeroEnergieIsolementsWithMatchingPoints()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Loc1.Should().Be("ZONE 4");
        result.Isolements.Should().HaveCount(13);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "ZERO ENERGIE");
        result.Points.Should().HaveCount(13).And.OnlyContain(p => p.ColonneNom == ZeroEnergieColonne);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_G6306BFixture_CoversAllFourTypesIncludingThePointDeFeuMismatch()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Loc1.Should().Be("ZONE 4");
        result.Isolements.Should().HaveCount(4);
        result.Isolements.Should().Contain(i => i.TypeElementNom == "INSTRUMENTATION")
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
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnrecognizedTypeElement);
    }

    private DiversSheetExtractionResult ExtractFromFixture(string fileName)
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
