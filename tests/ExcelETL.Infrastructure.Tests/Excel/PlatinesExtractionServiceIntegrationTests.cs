using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Platines;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs PlatinesExtractionService (Application, Lot C3) against the real ClosedXmlWorkbookReader and
// the 3 real client fixtures.
public class PlatinesExtractionServiceIntegrationTests
{
    private const string Sheet = "PLATINES";

    private static readonly string[] UnconditionalColonneNames =
    [
        "POSE ÉTIQUETTES",
        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
        "CONTRÔLE ETANCHÉITÉS",
        "RECEPTION DEBUT MAD",
        "RÉCEPTION PLATINES/TAMPONS PLEINS",
        "RECEPTION DEBUT REL",
        "PLATINES / TAMPONS PLEINS"
    ];

    private readonly PlatinesExtractionService _sut = new(new RepeatingBlockReader(), new TextTransformEvaluator());

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, PlatinesFieldNames.Identification,
        [
            new BlockFieldDefinition(PlatinesFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(PlatinesFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(PlatinesFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        UnconditionalColonneNames);

    [Fact]
    public void Extract_C7401Fixture_ReturnsAllPlatinesWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(15);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE");
        result.Points.Should().HaveCount(15 * 7);
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(21);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
        result.Points.Should().HaveCount(21 * 7);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(5);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
        result.Points.Should().HaveCount(5 * 7);
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
