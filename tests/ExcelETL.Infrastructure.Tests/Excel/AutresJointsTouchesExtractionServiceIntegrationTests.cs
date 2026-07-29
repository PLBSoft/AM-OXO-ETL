using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs AutresJointsTouchesExtractionService (Application, Lot C5) against the real
// ClosedXmlWorkbookReader and the 3 real client fixtures.
public class AutresJointsTouchesExtractionServiceIntegrationTests
{
    private const string Sheet = "AUTRES JOINTS TOUCHES";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";
    private const string ReperePrefix = "MAD-OXO-";

    private static readonly string[] UnconditionalColonneNames =
    [
        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
        "CONTRÔLE ETANCHÉITÉS"
    ];

    private readonly AutresJointsTouchesExtractionService _sut =
        new(new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<AutresJointsTouchesExtractionService>.Instance);

    // Lot 047: the "repereEcho" header rule (N6), transcribed from the coordinate previously
    // hardcoded in AutresJointsTouchesExtractionService.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
        UnconditionalColonneNames,
        [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(Sheet, "N6"))],
        []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsNoIsolements()
    {
        // Confirmed against the real file: this dossier's AUTRES JOINTS TOUCHES sheet is present but
        // entirely empty for this dossier -- same "unused sheet" pattern as ORIFICES CAPACITES (C4).
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsAllTuyauterieIsolementsWithPoseEtiquettesPoints()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Isolements.Should().HaveCount(13);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "TUYAUTERIE");
        // 2 unconditional + 1 conditional ("POSE ÉTIQUETTES", since none are "TUBING") per Isolement.
        result.Points.Should().HaveCount(13 * 3);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_G6306BFixture_ExcludesPoseEtiquettesOnlyForTubingIsolements()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Isolements.Should().HaveCount(4);
        result.Isolements.Should().Contain(i => i.TypeElementNom == "TUYAUTERIE")
            .And.Contain(i => i.TypeElementNom == "TUBING");

        var tubingReperes = result.Isolements.Where(i => i.TypeElementNom == "TUBING").Select(i => i.Repere).ToList();
        tubingReperes.Should().HaveCount(2);
        result.Points.Should().NotContain(p => p.ColonneNom == PoseEtiquettesColonneName && tubingReperes.Contains(p.ParentRepere));
        result.Points.Should().Contain(p => p.ColonneNom == PoseEtiquettesColonneName && !tubingReperes.Contains(p.ParentRepere));

        // The 2 TUBING isolements each fail to match POSE ÉTIQUETTES, but both share the same
        // normalized extracted value ("TUBING") -- deduplicated (Lot 055 §55.5) to a single warning
        // entry; the 2 TUYAUTERIE ones produce none.
        result.Errors.Should().ContainSingle().Which.Should().Match<ExtractionError>(
            e => e.Code == ExtractionErrorCode.NoConditionalPointCreated && e.ExtractedValue == "TUBING");
    }

    private IsolementSheetExtractionResult ExtractFromFixture(string fileName)
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
