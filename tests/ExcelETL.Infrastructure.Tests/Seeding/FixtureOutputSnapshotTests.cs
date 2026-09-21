using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ClosedXML.Excel;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExcelETL.Infrastructure.Tests.Seeding;

// Lot 084.0 (docs/tickets/tickets-tdd-lot-084-moteur-generique-feuilles-elements.md): safety net for
// the element-sheet engine rewrite. For every real client fixture, the ImportResult and every cell of
// the generated workbook -- standard profiles as seeded and read back from the stores -- are compared
// with a committed reference file (Snapshots/{fixture}.txt). A reference is only ever regenerated on
// purpose: set UPDATE_FIXTURE_SNAPSHOTS=1, run this class, and justify every diff in the commit.
//
// Errors are compared sorted on (code, sheet, extracted value) -- their order is an accident of each
// service's walk. Block identifiers and message texts live in a separate, also sorted, section.
// Elements and points are compared in order: they drive the generated rows' order.
public class FixtureOutputSnapshotTests
{
    private const string UpdateVariable = "UPDATE_FIXTURE_SNAPSHOTS";

    private readonly IDbContextFactory<ExcelETL.Infrastructure.Persistence.ExcelEtlDbContext> _dbContextFactory =
        new TestExcelEtlDbContextFactory("FixtureOutputSnapshotTests_" + Guid.NewGuid());

    private readonly ImportPipelineOrchestrator _orchestrator = new(
        new ProcedureExtractionService(new HeaderRuleResolver(new TextTransformEvaluator()), new ConditionalPointRuleEvaluator(), NullLogger<ProcedureExtractionService>.Instance),
        new ElementSheetExtractionService(
            new RepeatingBlockReader(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ElementSheetExtractionService>.Instance),
        NullLogger<ImportPipelineOrchestrator>.Instance);

    private readonly SheetGenerationEngine _generationEngine = new(NullLogger<SheetGenerationEngine>.Instance);
    private readonly ClosedXmlWorkbookWriter _writer = new(NullLogger<ClosedXmlWorkbookWriter>.Instance);

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(FixturesDirectory(), "Dossier*.xlsx").OrderBy(p => p, StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_ImportAndGeneratedWorkbook_MatchTheCommittedReference(string fixtureFileName)
    {
        var (importProfile, exportProfile) = await SeedAndFetchProfilesAsync();

        var actual = Describe(fixtureFileName, importProfile, exportProfile);
        var referencePath = Path.Combine(SnapshotsDirectory(), Path.GetFileNameWithoutExtension(fixtureFileName) + ".txt");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(SnapshotsDirectory());
            File.WriteAllText(referencePath, actual, new UTF8Encoding(false));
        }

        File.Exists(referencePath).Should().BeTrue(
            $"the reference {referencePath} must be committed (regenerate on purpose with {UpdateVariable}=1)");
        Normalize(actual).Should().Be(Normalize(File.ReadAllText(referencePath)));
    }

    private string Describe(string fixtureFileName, ImportProfile importProfile, ExportProfile exportProfile)
    {
        ImportResult result;
        using (var stream = File.OpenRead(Path.Combine(FixturesDirectory(), fixtureFileName)))
        using (var reader = new ClosedXmlWorkbookReader(stream))
        {
            result = _orchestrator.Run(reader, importProfile);
        }

        var text = new StringBuilder();
        text.Append("# Fixture: ").AppendLine(fixtureFileName);

        text.AppendLine().AppendLine("## Equipement");
        if (result.Equipement is null)
        {
            text.AppendLine("(none)");
        }
        else
        {
            var e = result.Equipement;
            text.AppendLine(Join(e.Repere, e.Designation, e.TypeElementNom, e.Localisation,
                List(e.Tableaux), List(e.Applications), e.SourceSheetName));
        }

        text.AppendLine().Append("## Elements (").Append(result.Isolements.Count).AppendLine(")");
        foreach (var i in result.Isolements)
        {
            text.AppendLine(Join(i.SourceSheetName, i.Repere, i.Designation, i.TypeElementNom, i.PositionALaPose,
                i.Localisation, List(i.Tableaux), List(i.Applications), i.RepereParent, i.CouleurEtiquette));
        }

        text.AppendLine().Append("## Points (").Append(result.Points.Count).AppendLine(")");
        foreach (var p in result.Points)
        {
            text.AppendLine(Join(p.ParentRepere, p.ColonneNom));
        }

        text.AppendLine().Append("## Tasks (").Append(result.TachesMultiples.Count).AppendLine(")");
        foreach (var t in result.TachesMultiples)
        {
            text.AppendLine(Join(
                t.LigneSource.ToString(CultureInfo.InvariantCulture),
                t.Ordre?.ToString(CultureInfo.InvariantCulture) ?? "",
                t.Action, t.Acteur, t.Risques, t.TypeTacheMultipleCode,
                t.DateValidation?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                t.EstFactice ? "factice" : "", t.Repere, t.TypeElementNom, t.ColonneTravaux, t.Localisation));
        }

        text.AppendLine().Append("## Errors (").Append(result.Errors.Count).AppendLine(")");
        foreach (var line in result.Errors
                     .Select(e => Join(e.Code.ToString(), e.Sheet, e.ExtractedValue ?? "(null)"))
                     .OrderBy(l => l, StringComparer.Ordinal))
        {
            text.AppendLine(line);
        }

        text.AppendLine().AppendLine("## Error block identifiers and messages");
        foreach (var line in result.Errors
                     .Select(e => Join(e.Code.ToString(), e.Sheet, e.BlockIdentifier, e.Message))
                     .OrderBy(l => l, StringComparer.Ordinal))
        {
            text.AppendLine(line);
        }

        text.AppendLine().AppendLine("## Generated workbook");
        using var destination = new MemoryStream();
        try
        {
            _writer.Write(_generationEngine.Generate(result, exportProfile), destination);
        }
        catch (Exception exception)
        {
            text.Append("(generation failed: ").Append(exception.GetType().Name).AppendLine(")");
            return text.ToString();
        }

        using var workbook = new XLWorkbook(destination);
        foreach (var worksheet in workbook.Worksheets)
        {
            text.AppendLine().Append("### Sheet: ").AppendLine(worksheet.Name);
            var used = worksheet.RangeUsed();
            if (used is null)
            {
                continue;
            }

            var lastColumn = used.LastColumn().ColumnNumber();
            foreach (var row in used.Rows())
            {
                var cells = Enumerable.Range(1, lastColumn)
                    .Select(column => worksheet.Cell(row.RowNumber(), column).GetString());
                text.AppendLine(string.Join(" | ", cells));
            }
        }

        return text.ToString();
    }

    private async Task<(ImportProfile ImportProfile, ExportProfile ExportProfile)> SeedAndFetchProfilesAsync()
    {
        var importProfileStore = new EfImportProfileStore(_dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(_dbContextFactory);
        await new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance)
            .SeedAsync();

        return ((await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId))!,
            (await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId))!);
    }

    private static string Join(params string[] values) => string.Join(" | ", values);

    private static string List(IReadOnlyList<string> values) => "[" + string.Join(", ", values) + "]";

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string SnapshotsDirectory([CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(sourceFile))!, "Snapshots");

    private static string FixturesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate the tests/Fixtures directory.")
            : Path.Combine(directory.FullName, "Fixtures");
    }
}
