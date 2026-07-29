using ClosedXML.Excel;
using ExcelETL.Application.Extraction.Oxo;
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

// M2/M3's own "Tests" requirement: run the real pipeline against the 3 real client fixtures using the
// profile as SEEDED and fetched back via IImportProfileStore/IExportProfileStore -- not rebuilt inline
// like ImportPipelineOrchestratorIntegrationTests/GenerationPipelineIntegrationTests do -- so a pass
// here confirms the seeder's transcription of coordinates/literals is correct, not just that some
// hand-built test profile happens to work.
public class DefaultProfileSeederPipelineIntegrationTests
{
    private readonly IDbContextFactory<ExcelETL.Infrastructure.Persistence.ExcelEtlDbContext> _dbContextFactory =
        new TestExcelEtlDbContextFactory("DefaultProfileSeederPipelineIntegrationTests_" + Guid.NewGuid());

    private readonly ImportPipelineOrchestrator _orchestrator = new(
        new ProcedureExtractionService(new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ProcedureExtractionService>.Instance),
        new IsolementExtractionService(
            new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(), NullLogger<IsolementExtractionService>.Instance),
        new UnconditionalIsolementSheetExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(),
            NullLogger<UnconditionalIsolementSheetExtractionService>.Instance),
        new AutresJointsTouchesExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<AutresJointsTouchesExtractionService>.Instance),
        new DiversExtractionService(
            new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<DiversExtractionService>.Instance),
        NullLogger<ImportPipelineOrchestrator>.Instance);

    private readonly SheetGenerationEngine _generationEngine = new(NullLogger<SheetGenerationEngine>.Instance);
    private readonly ClosedXmlWorkbookWriter _writer = new(NullLogger<ClosedXmlWorkbookWriter>.Instance);

    private async Task<(ImportProfile ImportProfile, ExportProfile ExportProfile)> SeedAndFetchProfilesAsync()
    {
        var importProfileStore = new EfImportProfileStore(_dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(_dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);

        await seeder.SeedAsync();

        var importProfile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        return (importProfile!, exportProfile!);
    }

    [Fact]
    public async Task Run_C7401Fixture_WithSeededProfile_ProducesSameResultsAsTheHandBuiltProfile()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.C7401.xlsx", importProfile);

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("38-C7401");
        result.Equipement.Localisation.Should().Be("ZONE 1");

        // ISOLEMENT(8) + PLATINES(15) + ORIFICES CAPACITES(0) + AUTRES JOINTS TOUCHES(0) + DIVERS(0)
        result.Isolements.Should().HaveCount(23);
        result.Isolements.Should().OnlyContain(i => i.Localisation == "ZONE 1");
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task Run_D8570Fixture_WithSeededProfile_ExtractsVanneIsolementAlongsideEverythingElse()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx", importProfile);

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("644-D8570");

        // ISOLEMENT(15, incl. VANNE) + PLATINES(21) + ORIFICES CAPACITES(5) + AUTRES JOINTS TOUCHES(13) + DIVERS(13)
        result.Isolements.Should().HaveCount(67);
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);

        var vanne = result.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE").Which;
        result.Errors.Should().Contain(e =>
            e.Code == ExtractionErrorCode.NoConditionalPointCreated && e.BlockIdentifier == vanne.Repere);
    }

    [Fact]
    public async Task Run_G6306BFixture_WithSeededProfile_ProducesSameResultsAsTheHandBuiltProfile()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx", importProfile);

        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("602-G6306B");

        // ISOLEMENT(3) + PLATINES(5) + ORIFICES CAPACITES(2) + AUTRES JOINTS TOUCHES(4) + DIVERS(4)
        result.Isolements.Should().HaveCount(18);
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task Generate_C7401Fixture_WithSeededProfiles_ProducesExpectedHeadersAndValues()
    {
        var (importProfile, exportProfile) = await SeedAndFetchProfilesAsync();

        ImportResult importResult;
        using (var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx")))
        using (var workbookReader = new ClosedXmlWorkbookReader(sourceStream))
        {
            importResult = _orchestrator.Run(workbookReader, importProfile);
        }

        var generatedWorkbook = _generationEngine.Generate(importResult, exportProfile);
        using var destination = new MemoryStream();
        _writer.Write(generatedWorkbook, destination);
        using var reread = new XLWorkbook(destination);

        // C7401's PROCEDURE TacheMultiple block produces both TM_PROC_MAD and TM_PROC_REL rows (see
        // Generate_C7401Fixture_WithSeededProfiles_ProducesTacheMultipleSheetsFromRealCodes below), so
        // both dynamic sheets are expected here too, alphabetically after Parents/Enfants.
        reread.Worksheets.Select(ws => ws.Name).Should().Equal("Parents", "Enfants", "TM_PROC_MAD", "TM_PROC_REL");

        var parents = reread.Worksheet("Parents");
        parents.Cell(1, 1).GetString().Should().Be("Repère");
        parents.Cell(2, 1).GetString().Should().Be("38-C7401");
        parents.Cell(2, 3).GetString().Should().Be("ZONE 1");

        var enfants = reread.Worksheet("Enfants");
        enfants.Cell(1, 1).GetString().Should().Be("Numéro");
        enfants.RowsUsed().Should().HaveCount(1 + importResult.Isolements.Count);
    }

    [Fact]
    public async Task Generate_C7401Fixture_WithSeededProfiles_ProducesTacheMultipleSheetsFromRealCodes()
    {
        var (importProfile, exportProfile) = await SeedAndFetchProfilesAsync();

        ImportResult importResult;
        using (var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx")))
        using (var workbookReader = new ClosedXmlWorkbookReader(sourceStream))
        {
            importResult = _orchestrator.Run(workbookReader, importProfile);
        }

        var generatedWorkbook = _generationEngine.Generate(importResult, exportProfile);
        using var destination = new MemoryStream();
        _writer.Write(generatedWorkbook, destination);
        using var reread = new XLWorkbook(destination);

        var expectedCounts = importResult.TachesMultiples
            .GroupBy(t => t.TypeTacheMultipleCode)
            .ToDictionary(g => g.Key, g => g.Count());

        expectedCounts.Should().ContainKeys("TM_PROC_MAD", "TM_PROC_REL");

        foreach (var (code, count) in expectedCounts)
        {
            var sheet = reread.Worksheet(code);
            sheet.Cell(1, 1).GetString().Should().Be("Ordre");
            sheet.Cell(1, 2).GetString().Should().Be("Action");
            sheet.RowsUsed().Should().HaveCount(1 + count);
        }
    }

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), U6: header order (and content)
    // for the newly seeded Tableaux/PROGRESS/ELEMENT PARENT/Type Elément columns, against the profile
    // as seeded and fetched back -- not a hand-built profile.
    [Fact]
    public async Task Generate_C7401Fixture_WithSeededProfiles_ProducesExpectedHeaderOrderAndNewColumnValues()
    {
        var (importProfile, exportProfile) = await SeedAndFetchProfilesAsync();

        ImportResult importResult;
        using (var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx")))
        using (var workbookReader = new ClosedXmlWorkbookReader(sourceStream))
        {
            importResult = _orchestrator.Run(workbookReader, importProfile);
        }

        var generatedWorkbook = _generationEngine.Generate(importResult, exportProfile);
        using var destination = new MemoryStream();
        _writer.Write(generatedWorkbook, destination);
        using var reread = new XLWorkbook(destination);

        var parents = reread.Worksheet("Parents");
        parents.Cell(1, 1).GetString().Should().Be("Repère");
        parents.Cell(1, 2).GetString().Should().Be("Type Elément");
        parents.Cell(1, 3).GetString().Should().Be("Zone");
        parents.Cell(1, 4).GetString().Should().Be("Désignation");
        parents.Cell(1, 5).GetString().Should().Be("Tableaux");
        parents.Cell(1, 6).GetString().Should().Be("PROGRESS");
        parents.Cell(1, 7).GetString().Should().Be("TRAVAUX COMPLET");
        parents.Cell(2, 5).GetString().Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
        parents.Cell(2, 6).GetString().Should().Be("O");

        var enfants = reread.Worksheet("Enfants");
        enfants.Cell(1, 1).GetString().Should().Be("Numéro");
        enfants.Cell(1, 2).GetString().Should().Be("Type Elément");
        enfants.Cell(1, 3).GetString().Should().Be("Zone");
        enfants.Cell(1, 4).GetString().Should().Be("ELEMENT PARENT");
        enfants.Cell(1, 5).GetString().Should().Be("Désignation");
        enfants.Cell(1, 6).GetString().Should().Be("Position à la pose");
        enfants.Cell(1, 7).GetString().Should().Be("Tableaux");
        enfants.Cell(1, 8).GetString().Should().Be("PROGRESS");
        enfants.Cell(1, 9).GetString().Should().Be("PROLOCK VANNES");

        // Every Enfants row (23 for C7401) shares the same ELEMENT PARENT/Tableaux/PROGRESS values --
        // broadcast onto every Isolement of the run, same as Localisation.
        foreach (var row in enfants.RowsUsed().Skip(1))
        {
            row.Cell(4).GetString().Should().Be("38-C7401");
            row.Cell(7).GetString().Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
            row.Cell(8).GetString().Should().Be("O");
        }
    }

    private ImportResult RunOnFixture(string fileName, ImportProfile profile)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _orchestrator.Run(workbookReader, profile);
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
