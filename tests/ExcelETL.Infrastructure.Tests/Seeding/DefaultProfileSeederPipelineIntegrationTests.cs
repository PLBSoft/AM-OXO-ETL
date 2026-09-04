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

    // Lot 063's central invariant, verified against the profile as seeded and fetched back via
    // IImportProfileStore (not a hand-built rule) -- only C7401's "V4" block changes observable
    // behavior; isolement/point/warning counts elsewhere are untouched.
    [Fact]
    public async Task Run_C7401Fixture_WithSeededProfile_V4IsolementHasZeroEnergieAndPS941PointWithoutWarning()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.C7401.xlsx", importProfile);

        var v4 = result.Isolements.Should().ContainSingle(i => i.Repere == "C7401-V4").Which;
        v4.HasZeroEnergie.Should().BeTrue();
        result.Points.Should().Contain(new PointPivot("ZÉRO ENERGIE EN PRESENCE EE (PS941)", "C7401-V4"));
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);
        result.Isolements.Should().HaveCount(23);
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

    // Lot 055 §55.8: the deduplicated NoConditionalPointCreated warnings expected against the real
    // seeded profile -- exactly one entry per (feuille, valeur normalisée) that produced no
    // conditional Point, and nothing else. Confirms both the emission rule (55.4) and the
    // deduplication (55.5) end-to-end against real fixture data, not just hand-built unit cases.
    [Fact]
    public async Task Run_C7401Fixture_WithSeededProfile_ProducesExactlyOneNoConditionalPointCreatedWarning()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.C7401.xlsx", importProfile);

        var warnings = result.Errors.Where(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated).ToList();
        var warning = warnings.Should().ContainSingle().Subject;
        warning.Sheet.Should().Be("ISOLEMENT");
        warning.ExtractedValue.Should().Be("PROLOCK");

        // Same isolement count as the pre-existing regression assertions above -- extraction itself
        // is unaffected by this lot, only the warnings are.
        result.Isolements.Should().HaveCount(23);
    }

    [Fact]
    public async Task Run_D8570Fixture_WithSeededProfile_ProducesExactlyTwoNoConditionalPointCreatedWarnings()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx", importProfile);

        var warnings = result.Errors.Where(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated).ToList();
        warnings.Should().HaveCount(2).And.OnlyContain(e => e.Sheet == "ISOLEMENT");
        warnings.Select(e => e.ExtractedValue).Should().BeEquivalentTo(["PROLOCK", "VANNE"]);

        // AUTRES JOINTS TOUCHES' 13 TUYAUTERIE isolements and DIVERS' 13 ZERO ENERGIE ones all
        // legitimately match their sheet's own conditional rule -- confirmed absent above via
        // OnlyContain(Sheet == "ISOLEMENT").
        result.Isolements.Should().HaveCount(67);
    }

    [Fact]
    public async Task Run_G6306BFixture_WithSeededProfile_ProducesExactlyThreeNoConditionalPointCreatedWarnings_OnePerSheet()
    {
        var (importProfile, _) = await SeedAndFetchProfilesAsync();
        var result = RunOnFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx", importProfile);

        var warnings = result.Errors.Where(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated).ToList();
        warnings.Should().HaveCount(3);
        warnings.Should().Contain(e => e.Sheet == "ISOLEMENT" && e.ExtractedValue == "PROLOCK");
        warnings.Should().Contain(e => e.Sheet == "AUTRES JOINTS TOUCHES" && e.ExtractedValue == "TUBING");
        warnings.Should().Contain(e => e.Sheet == "DIVERS" && e.ExtractedValue == "POINT DE FEU");

        // PLATINES / ORIFICES CAPACITES never carry a ConditionalPointRule in this profile -- neither
        // can ever produce this code.
        warnings.Should().NotContain(e => e.Sheet == "PLATINES" || e.Sheet == "ORIFICES CAPACITES");

        result.Isolements.Should().HaveCount(18);
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

        // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
        // header resolved dynamically from the profile itself (not fixed positions), same "figer par le
        // test, pas par un décompte manuel" convention as Parents/Enfants below.
        var tacheMultipleRule = exportProfile.SheetRules.Single(r => r.PivotSource == PivotSource.TacheMultiple);
        var expectedTacheMultipleHeaders = tacheMultipleRule.ColumnDefinitions.Select(c => c.Header)
            .Concat(tacheMultipleRule.ConstantColumnDefinitions.Select(c => c.Header))
            .ToList();

        var expectedCounts = importResult.TachesMultiples
            .GroupBy(t => t.TypeTacheMultipleCode)
            .ToDictionary(g => g.Key, g => g.Count());

        expectedCounts.Should().ContainKeys("TM_PROC_MAD", "TM_PROC_REL");

        foreach (var (code, count) in expectedCounts)
        {
            var sheet = reread.Worksheet(code);
            sheet.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedTacheMultipleHeaders);
            sheet.RowsUsed().Should().HaveCount(1 + count);
        }
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md), 67.4:
    // against the profile as seeded and fetched back -- every row of both dynamic sheets carries the
    // right Repère TM/TYPE ELEMENT CODE/Colonne Travaux value.
    //
    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
    // extended to cover the 8 new columns per row, column indices resolved dynamically from the
    // profile (headers can shift order across an EF round trip, same caveat noted in
    // DefaultProfileSeederTests.cs for this owned collection).
    [Fact]
    public async Task Generate_C7401Fixture_WithSeededProfiles_ProducesRepereTypeAndColonneTravauxOnBothTacheMultipleSheets()
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

        var tacheMultipleRule = exportProfile.SheetRules.Single(r => r.PivotSource == PivotSource.TacheMultiple);
        var expectedHeaders = tacheMultipleRule.ColumnDefinitions.Select(c => c.Header)
            .Concat(tacheMultipleRule.ConstantColumnDefinitions.Select(c => c.Header))
            .ToList();
        int Col(string header) => expectedHeaders.IndexOf(header) + 1;

        var expectedRepere = importResult.Equipement!.Repere;
        var expectedZone = importResult.Equipement.Localisation;

        void AssertSheet(string sheetName, string expectedColonneTravaux)
        {
            var sheet = reread.Worksheet(sheetName);
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                row.Cell(Col("GUID")).GetString().Should().BeEmpty();
                row.Cell(Col("TYPE TACHE")).GetString().Should().Be(sheetName);
                row.Cell(Col("Repère TM")).GetString().Should().Be(expectedRepere);
                row.Cell(Col("ZONE")).GetString().Should().Be(expectedZone);
                row.Cell(Col("LOC2")).GetString().Should().BeEmpty();
                row.Cell(Col("LOC3")).GetString().Should().BeEmpty();
                row.Cell(Col("TYPE ELEMENT CODE")).GetString().Should().Be("MAD TRAVAUX");
                row.Cell(Col("LOT")).GetString().Should().BeEmpty();
                row.Cell(Col("Ressource")).GetString().Should().BeEmpty();
                row.Cell(Col("Ligne")).GetValue<int>().Should().BePositive();
                row.Cell(Col("Colonne Travaux")).GetString().Should().Be(expectedColonneTravaux);
                row.Cell(Col("CRITERE")).GetString().Should().Be("A faire");
                row.Cell(Col("AVANCEMENT")).GetString().Should().Be("0");
                row.Cell(Col("SUPPRESSION")).GetString().Should().Be("N");
            }
        }

        AssertSheet("TM_PROC_MAD", "Procédure MAD");
        AssertSheet("TM_PROC_REL", "Procédure REL");
    }

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), U6: header order (and content)
    // for the newly seeded Tableaux/PROGRESS/ELEMENT PARENT/Type Elément columns, against the profile
    // as seeded and fetched back -- not a hand-built profile.
    //
    // Lot 066 (docs/tickets/tickets-tdd-lot-066-completion-colonnes-parents-enfants-export.md), 66.5:
    // rewritten to resolve headers/column indices dynamically from the profile itself, rather than
    // hardcoded numeric indices -- per the ticket's own explicit instruction to avoid a manual-counting
    // divergence risk once Parents/Enfants both carry ~30 columns.
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

        var parentsRule = exportProfile.SheetRules.Single(r => r.SheetName == "Parents");
        var expectedParentsHeaders = parentsRule.ColumnDefinitions.Select(c => c.Header)
            .Concat(parentsRule.ApplicationColumnDefinitions.Select(a => a.Header))
            .Concat(parentsRule.PointColumnDefinitions.Select(p => p.Header))
            .ToList();

        var parents = reread.Worksheet("Parents");
        parents.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedParentsHeaders);

        int ParentsCol(string header) => expectedParentsHeaders.IndexOf(header) + 1;
        parents.Cell(1, ParentsCol("Repère")).GetString().Should().Be("Repère");
        parents.Cell(2, ParentsCol("Repère")).GetString().Should().Be("38-C7401");
        parents.Cell(2, ParentsCol("Tableaux")).GetString().Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
        parents.Cell(2, ParentsCol("PROGRESS")).GetString().Should().Be("O");

        // 66.3's new aggregation exercised in real conditions: PROLOCK VANNES/PS941 are produced by
        // C7401's ISOLEMENT isolements (unconditional/HasZeroEnergie's "C7401-V4"), never directly by
        // the Equipement itself -- yet they still mark Parents.
        parents.Cell(2, ParentsCol("PROLOCK VANNES")).GetString().Should().Be("X");
        parents.Cell(2, ParentsCol("ZÉRO ENERGIE EN PRESENCE EE (PS941)")).GetString().Should().Be("X");
        // C7401's DIVERS sheet produces zero isolements -- nothing aggregates its own Points onto
        // Parents, so this column stays legitimately empty (non-regression against over-aggregating).
        parents.Cell(2, ParentsCol("SYNCHRONISATION INSTRUMENTATION")).GetString().Should().Be("");

        var enfantsRule = exportProfile.SheetRules.Single(r => r.SheetName == "Enfants");
        var expectedEnfantsHeaders = enfantsRule.ColumnDefinitions.Select(c => c.Header)
            .Concat(enfantsRule.ApplicationColumnDefinitions.Select(a => a.Header))
            .Concat(enfantsRule.PointColumnDefinitions.Select(p => p.Header))
            .ToList();

        var enfants = reread.Worksheet("Enfants");
        enfants.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedEnfantsHeaders);

        int EnfantsCol(string header) => expectedEnfantsHeaders.IndexOf(header) + 1;

        // Every Enfants row (23 for C7401) shares the same ELEMENT PARENT/Tableaux/PROGRESS values --
        // broadcast onto every Isolement of the run, same as Localisation.
        foreach (var row in enfants.RowsUsed().Skip(1))
        {
            row.Cell(EnfantsCol("ELEMENT PARENT")).GetString().Should().Be("38-C7401");
            row.Cell(EnfantsCol("Tableaux")).GetString().Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
            row.Cell(EnfantsCol("PROGRESS")).GetString().Should().Be("O");
        }

        // Lot 068 (couleur d'étiquette, client remark): "ETIQUETTE" is populated only for the 15
        // PLATINES rows (all ROUGE on the real C7401 fixture, confirmed in
        // PlatinesExtractionServiceIntegrationTests) -- every other Enfants row (ISOLEMENT, AUTRES
        // JOINTS TOUCHES, DIVERS -- C7401's ORIFICES CAPACITES is empty) stays blank.
        var typeElementCol = EnfantsCol("Type Elément");
        var etiquetteCol = EnfantsCol("ETIQUETTE");
        var platinesRows = enfants.RowsUsed().Skip(1)
            .Where(row => row.Cell(typeElementCol).GetString() == "PLATINE").ToList();
        platinesRows.Should().HaveCount(15);
        platinesRows.Should().OnlyContain(row => row.Cell(etiquetteCol).GetString() == "ROUGE");

        var nonPlatinesRows = enfants.RowsUsed().Skip(1)
            .Where(row => row.Cell(typeElementCol).GetString() != "PLATINE");
        nonPlatinesRows.Should().OnlyContain(row => row.Cell(etiquetteCol).GetString() == "");
    }

    // Lot 066 (docs/tickets/tickets-tdd-lot-066-completion-colonnes-parents-enfants-export.md), 66.5:
    // one closing integration test per remaining fixture (C7401's own equivalent lives above, predating
    // this lot), each checking in one place: Parents/Enfants' full header (resolved dynamically from
    // the profile, per the ticket's own "figer par le test, pas par un décompte manuel" instruction),
    // at least one data row per sheet with unmapped identity columns genuinely empty, and at least one
    // Point column correctly aggregated (66.3) onto Parents from a child isolement.
    [Fact]
    public async Task Generate_D8570Fixture_WithSeededProfiles_ProducesCompleteHeadersAndAggregatedPoints()
    {
        var (importResult, reread, parentsRule, enfantsRule) =
            await GenerateForFixtureAsync("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        var expectedParentsHeaders = ExpectedHeaders(parentsRule);
        var parents = reread.Worksheet("Parents");
        parents.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedParentsHeaders);

        int ParentsCol(string header) => expectedParentsHeaders.IndexOf(header) + 1;
        parents.RowsUsed().Should().HaveCount(2); // header + 1 Equipement row
        parents.Cell(2, ParentsCol("Repère")).GetString().Should().Be("644-D8570");
        parents.Cell(2, ParentsCol("LOC2")).GetString().Should().Be("");
        parents.Cell(2, ParentsCol("COMMENTAIRES")).GetString().Should().Be("");

        // DIVERS' 13 "ZERO ENERGIE" isolements now target the same "(PS941)" Colonne as ISOLEMENT
        // (66.1's merge) -- none of them is ISOLEMENT's own repère, so this column is marked on
        // Parents only via aggregation, not via a direct Equipement-attached Point.
        parents.Cell(2, ParentsCol("ZÉRO ENERGIE EN PRESENCE EE (PS941)")).GetString().Should().Be("X");

        var expectedEnfantsHeaders = ExpectedHeaders(enfantsRule);
        var enfants = reread.Worksheet("Enfants");
        enfants.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedEnfantsHeaders);
        enfants.RowsUsed().Should().HaveCount(1 + importResult.Isolements.Count);

        int EnfantsCol(string header) => expectedEnfantsHeaders.IndexOf(header) + 1;
        enfants.Cell(2, EnfantsCol("REMARQUES")).GetString().Should().Be("");
        enfants.Cell(2, EnfantsCol("POSITION A LA DEPOSE")).GetString().Should().Be("");
    }

    [Fact]
    public async Task Generate_G6306BFixture_WithSeededProfiles_ProducesCompleteHeadersAndAggregatedPoints()
    {
        var (importResult, reread, parentsRule, enfantsRule) =
            await GenerateForFixtureAsync("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        var expectedParentsHeaders = ExpectedHeaders(parentsRule);
        var parents = reread.Worksheet("Parents");
        parents.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedParentsHeaders);

        int ParentsCol(string header) => expectedParentsHeaders.IndexOf(header) + 1;
        parents.RowsUsed().Should().HaveCount(2);
        parents.Cell(2, ParentsCol("Repère")).GetString().Should().Be("602-G6306B");
        parents.Cell(2, ParentsCol("FLUIDE")).GetString().Should().Be("");

        // DIVERS' one matching "INSTRUMENTATION" isolement marks this column on Parents purely via
        // aggregation -- no Point is ever attached to the Equipement itself for this Colonne.
        parents.Cell(2, ParentsCol("SYNCHRONISATION INSTRUMENTATION")).GetString().Should().Be("X");
        // ISOLEMENT's unconditional Points aggregate onto Parents too.
        parents.Cell(2, ParentsCol("PROLOCK VANNES")).GetString().Should().Be("X");
        // DIVERS' one "POINT DE FEU" row never matches the confirmed base value "POINT FEU" --
        // no PF Point is ever produced for this run, so every PF column stays empty on Parents.
        parents.Cell(2, ParentsCol("PF : ACCORD TRAVAUX FEU")).GetString().Should().Be("");

        var expectedEnfantsHeaders = ExpectedHeaders(enfantsRule);
        var enfants = reread.Worksheet("Enfants");
        enfants.Row(1).CellsUsed().Select(c => c.GetString()).Should().Equal(expectedEnfantsHeaders);
        enfants.RowsUsed().Should().HaveCount(1 + importResult.Isolements.Count);

        int EnfantsCol(string header) => expectedEnfantsHeaders.IndexOf(header) + 1;
        enfants.Cell(2, EnfantsCol("NATURE JOINT")).GetString().Should().Be("");
    }

    private static List<string> ExpectedHeaders(SheetGenerationRule rule) => rule.ColumnDefinitions.Select(c => c.Header)
        .Concat(rule.ApplicationColumnDefinitions.Select(a => a.Header))
        .Concat(rule.PointColumnDefinitions.Select(p => p.Header))
        .ToList();

    private async Task<(ImportResult ImportResult, XLWorkbook Generated, SheetGenerationRule ParentsRule, SheetGenerationRule EnfantsRule)>
        GenerateForFixtureAsync(string fixtureFileName)
    {
        var (importProfile, exportProfile) = await SeedAndFetchProfilesAsync();

        ImportResult importResult;
        using (var sourceStream = File.OpenRead(FixturePath(fixtureFileName)))
        using (var workbookReader = new ClosedXmlWorkbookReader(sourceStream))
        {
            importResult = _orchestrator.Run(workbookReader, importProfile);
        }

        var generatedWorkbook = _generationEngine.Generate(importResult, exportProfile);
        using var destination = new MemoryStream();
        _writer.Write(generatedWorkbook, destination);
        var reread = new XLWorkbook(destination);

        var parentsRule = exportProfile.SheetRules.Single(r => r.SheetName == "Parents");
        var enfantsRule = exportProfile.SheetRules.Single(r => r.SheetName == "Enfants");
        return (importResult, reread, parentsRule, enfantsRule);
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
