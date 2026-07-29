using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs IsolementExtractionService (Application, Lot C2) against the real ClosedXmlWorkbookReader and
// the 3 real client fixtures. None of the 3 files contain a "ZERO ENERGIE"-typed row in ISOLEMENT, so
// every extracted isolement (all "PROLOCK" except D8570's one "VANNE") legitimately produces an
// NoConditionalPointCreated warning for the unmatched conditional Colonne -- see
// IsolementExtractionService's comment on why that's correct here, not a bug. Since Lot 055 (§55.5),
// these warnings are deduplicated per (feuille, valeur normalisée) at emission time, so a sheet full
// of identical "PROLOCK" isolements produces exactly one warning entry, not one per element.
public class IsolementExtractionServiceIntegrationTests
{
    private const string Sheet = "ISOLEMENT";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";

    private readonly IsolementExtractionService _sut =
        new(new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(), NullLogger<IsolementExtractionService>.Instance);

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
        ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsAllPlainProlockIsolements()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Isolements.Should().HaveCount(8);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PROLOCK");
        result.Points.Should().HaveCount(8 * 2);
        result.Errors.Should().ContainSingle().Which.Should().Match<ExtractionError>(
            e => e.Code == ExtractionErrorCode.NoConditionalPointCreated && e.ExtractedValue == "PROLOCK");
    }

    [Fact]
    public void Extract_D8570Fixture_ExtractsVanneIsolementNormallyAlongsideProlockOnes()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Isolements.Should().HaveCount(15);
        var vanne = result.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE").Which;
        vanne.Repere.Should().Be("D8570-V4");
        vanne.Designation.Should().BeEmpty();
        result.Points.Should().Contain(
        [
            new PointPivot("PROLOCK VANNES", "D8570-V4"),
            new PointPivot("DEPROLOCK VANNES", "D8570-V4")
        ]);
        result.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated);
        result.Errors.Select(e => e.ExtractedValue).Should().BeEquivalentTo(["PROLOCK", "VANNE"]);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsAllPlainProlockIsolements()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Isolements.Should().HaveCount(3);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PROLOCK");
        result.Points.Should().HaveCount(3 * 2);
        result.Errors.Should().ContainSingle().Which.Should().Match<ExtractionError>(
            e => e.Code == ExtractionErrorCode.NoConditionalPointCreated && e.ExtractedValue == "PROLOCK");
    }

    private IsolementSheetExtractionResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule());
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
