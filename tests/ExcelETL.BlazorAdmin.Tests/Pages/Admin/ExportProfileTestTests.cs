using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Symmetric to ImportProfileTestTests (F2), but covers both ends of the pipeline: import (Lot D,
// unchanged) then generation (Lot I3/I4) -- both run in process, no HTTP round trip, no file
// archived. Reuses the exact same hardcoded ImportProfile as ImportPipelineOrchestratorIntegrationTests
// / ImportProfileTestTests, plus the same ExportProfile fixture as GenerationPipelineIntegrationTests
// -- same already-validated cell ranges / column mappings the pipeline's own regression guard-rails
// already exercise.
public class ExportProfileTestTests : BunitContext
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    public ExportProfileTestTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileTestTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddSingleton<ITextTransformEvaluator, TextTransformEvaluator>();
        Services.AddSingleton<IConditionalPointRuleEvaluator, ConditionalPointRuleEvaluator>();
        Services.AddSingleton<IRepeatingBlockReader, RepeatingBlockReader>();
        Services.AddSingleton<ILogger<ProcedureExtractionService>>(NullLogger<ProcedureExtractionService>.Instance);
        Services.AddSingleton<ILogger<IsolementExtractionService>>(NullLogger<IsolementExtractionService>.Instance);
        Services.AddSingleton<ILogger<UnconditionalIsolementSheetExtractionService>>(
            NullLogger<UnconditionalIsolementSheetExtractionService>.Instance);
        Services.AddSingleton<ILogger<AutresJointsTouchesExtractionService>>(
            NullLogger<AutresJointsTouchesExtractionService>.Instance);
        Services.AddSingleton<ILogger<DiversExtractionService>>(NullLogger<DiversExtractionService>.Instance);
        Services.AddSingleton<ILogger<ImportPipelineOrchestrator>>(NullLogger<ImportPipelineOrchestrator>.Instance);
        Services.AddSingleton<IProcedureExtractionService, ProcedureExtractionService>();
        Services.AddSingleton<IIsolementExtractionService, IsolementExtractionService>();
        Services.AddSingleton<IUnconditionalIsolementSheetExtractionService, UnconditionalIsolementSheetExtractionService>();
        Services.AddSingleton<IAutresJointsTouchesExtractionService, AutresJointsTouchesExtractionService>();
        Services.AddSingleton<IDiversExtractionService, DiversExtractionService>();
        Services.AddSingleton<IImportPipelineOrchestrator, ImportPipelineOrchestrator>();
        Services.AddSingleton<ISheetGenerationEngine, SheetGenerationEngine>();
        Services.AddSingleton<IWorkbookWriter, ClosedXmlWorkbookWriter>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private async Task<ImportProfile> SeedRealImportProfileAsync()
    {
        var profile = CreateRealImportProfile();
        var store = Services.GetRequiredService<IImportProfileStore>();
        await store.SaveAsync(profile);
        return profile;
    }

    private async Task<ExportProfile> SeedRealExportProfileAsync()
    {
        var profile = CreateRealExportProfile();
        var store = Services.GetRequiredService<IExportProfileStore>();
        await store.SaveAsync(profile);
        return profile;
    }

    private static void SelectImportProfile(IRenderedComponent<ExportProfileTest> cut, Guid profileId)
    {
        cut.WaitForState(() => cut.FindAll("#export-test-import-profile-select option").Count > 1);
        cut.Find("#export-test-import-profile-select").Change(profileId.ToString());
    }

    private static void SelectExportProfile(IRenderedComponent<ExportProfileTest> cut, Guid profileId)
    {
        cut.WaitForState(() => cut.FindAll("#export-test-export-profile-select option").Count > 1);
        cut.Find("#export-test-export-profile-select").Change(profileId.ToString());
    }

    private static InputFileContent FixtureAsInputFile(string fileName)
    {
        var bytes = File.ReadAllBytes(FixturePath(fileName));
        return InputFileContent.CreateFromBinary(bytes, fileName);
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

    private static string ComponentSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root.");
        }

        return Path.Combine(
            directory.FullName, "src", "ExcelETL.BlazorAdmin", "Components", "Pages", "Admin", "ExportProfileTest.razor");
    }

    // Matches ImportPipelineOrchestratorIntegrationTests/ImportProfileTestTests' hardcoded profile
    // exactly -- same already-validated cell ranges for all 6 sheets.
    private static ImportProfile CreateRealImportProfile() => new(
        "Profil OXO standard", ReperePrefix, EquipementTypeElementNom,
        ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], ["PROGRESS"],
        [
            new SheetExtractionRule(
                "PROCEDURE",
                new RepeatingBlockLocator("PROCEDURE", 9, 1, ProcedureFieldNames.Action,
                [
                    new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
                ]),
                [],
                []),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"]),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator("PLATINES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    "POSE ÉTIQUETTES",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS",
                    "RECEPTION DEBUT MAD",
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RECEPTION DEBUT REL",
                    "PLATINES / TAMPONS PLEINS"
                ]),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator("ORIFICES CAPACITES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ]),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"]),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator("DIVERS", 9, 3, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [])
        ]);

    // Matches GenerationPipelineIntegrationTests' ExportProfile fixture exactly (approximation of
    // OXO_TRAME_IMPORT_MAD.xlsx's Parents/Enfants sheets, a mix of mapped and unmapped columns).
    private static ExportProfile CreateRealExportProfile() => new(
        "Profil export OXO standard",
        [
            new SheetGenerationRule(
                "Parents",
                PivotSource.Equipement,
                [
                    new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
                    new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation),
                    new ColumnDefinition("Type Elément", PivotFieldRef.EquipementTypeElementNom),
                    new ColumnDefinition("Zone", PivotFieldRef.EquipementLocalisation),
                    new ColumnDefinition("Fluide", null),
                    new ColumnDefinition("Commentaires", null)
                ],
                [
                    new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet"),
                    new PointColumnDefinition("TRAVAUX DETAIL", "Travaux détail")
                ],
                []),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    new ColumnDefinition("Type", PivotFieldRef.IsolementTypeElementNom),
                    new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation),
                    new ColumnDefinition("Position à la pose", PivotFieldRef.IsolementPositionALaPose),
                    new ColumnDefinition("Zone", PivotFieldRef.IsolementLocalisation),
                    new ColumnDefinition("Phase process", null),
                    new ColumnDefinition("Remarques", null)
                ],
                [
                    new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
                    new PointColumnDefinition("DEPROLOCK VANNES", "Deprolock vannes"),
                    new PointColumnDefinition(ZeroEnergieColonneName, "Zéro énergie en présence EE (PS941)")
                ],
                [])
        ]);

    [Fact]
    public async Task Run_C7401Fixture_GeneratesWorkbookWithKnownValuesInPreview() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());
            cut.Markup.Should().NotContain("File rejected");

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());
            cut.Find("#generated-sheet-Parents-table").InnerHtml.Should().Contain("38-C7401");
            cut.Find("#generated-sheet-Enfants-table").Should().NotBeNull();
            cut.Find("#download-generated-workbook-link").GetAttribute("href")
                .Should().StartWith("data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,");
        });

    [Fact]
    public async Task SelectingFile_ThatFailsProcedureValidation_BlocksGeneration_AndNeverCallsGenerationEngine() =>
        await WithCultureAsync("en-US", async () =>
        {
            var mockEngine = new Mock<ISheetGenerationEngine>();
            Services.AddSingleton(mockEngine.Object);

            var importProfile = await SeedRealImportProfileAsync();
            await SeedRealExportProfileAsync();

            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            workbook.Worksheets.Add("PROCEDURE"); // M2:O2 left blank -> whole-file rejection
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(InputFileContent.CreateFromBinary(stream.ToArray(), "invalid.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("File rejected"));
            cut.FindAll("#generate-workbook-button").Should().BeEmpty();
            cut.FindAll("#export-test-export-profile-select").Should().BeEmpty();

            mockEngine.Verify(engine => engine.Generate(It.IsAny<ImportResult>(), It.IsAny<ExportProfile>()), Times.Never);
        });

    [Fact]
    public async Task Run_D8570Fixture_GeneratesWorkbook_DespiteNonBlockingVanneWarning() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());
            cut.Markup.Should().NotContain("File rejected");

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Enfants-table").Should().NotBeEmpty());
            cut.Find("#generated-sheet-Enfants-table").InnerHtml.Should().Contain("VANNE");
        });

    // Lot T (docs/tickets-tdd-export-taches-multiples.md, T7): the preview already renders one HTML
    // table per physically generated sheet (@foreach over _generatedWorkbook.Sheets) -- since
    // SheetGenerationEngine (T3) now emits one dynamic sheet per distinct TypeTacheMultipleCode, this
    // page needed no code change at all. These tests exist to prove that end-to-end against a real
    // fixture, not just at the Application layer.
    private static ExportProfile CreateRealExportProfileWithTacheMultipleRule()
    {
        var baseProfile = CreateRealExportProfile();
        return new ExportProfile(
            baseProfile.Name,
            [
                .. baseProfile.SheetRules,
                new SheetGenerationRule(
                    "Tâches multiples",
                    PivotSource.TacheMultiple,
                    [
                        new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
                        new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
                        new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
                        new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
                        new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation)
                    ],
                    [],
                    [])
            ]);
    }

    private async Task<ExportProfile> SeedRealExportProfileWithTacheMultipleRuleAsync()
    {
        var profile = CreateRealExportProfileWithTacheMultipleRule();
        var store = Services.GetRequiredService<IExportProfileStore>();
        await store.SaveAsync(profile);
        return profile;
    }

    [Fact]
    public async Task Run_C7401Fixture_WithTacheMultipleRule_RendersOneTablePerDistinctCodeWithCorrectValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileWithTacheMultipleRuleAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());
            cut.Markup.Should().NotContain("File rejected");

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            // C7401's PROCEDURE TacheMultiple block produces both TM_PROC_MAD (59 rows) and
            // TM_PROC_REL (39 rows) -- confirmed against the real fixture while building T5.
            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-TM_PROC_MAD-table").Should().NotBeEmpty());
            cut.FindAll("#generated-sheet-TM_PROC_REL-table").Should().NotBeEmpty();

            var madTable = cut.Find("#generated-sheet-TM_PROC_MAD-table");
            madTable.QuerySelectorAll("tbody tr").Should().HaveCount(59);

            var relTable = cut.Find("#generated-sheet-TM_PROC_REL-table");
            relTable.QuerySelectorAll("tbody tr").Should().HaveCount(39);

            madTable.QuerySelectorAll("thead th").Select(th => th.TextContent).Should().Equal(
                "Ordre", "Action", "Acteur", "Risques", "Date de validation");
        });

    [Fact]
    public async Task Run_C7401Fixture_WithoutTacheMultipleRuleInProfile_RendersNoTacheMultipleTables() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());
            cut.FindAll("#generated-sheet-Enfants-table").Should().NotBeEmpty();
            cut.FindAll("table[id^='generated-sheet-TM_']").Should().BeEmpty();
        });

    // Mirrors ImportProfileTest's own collapsible-section tests (test-table-details/toggle) --
    // same mechanic, applied here to the dynamic per-sheet generated-workbook preview.
    [Fact]
    public async Task GeneratedSheetTables_AreOpenByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            cut.Find("#generated-sheet-Parents-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
            cut.Find("#generated-sheet-Enfants-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
            cut.Find("#generated-sheet-Enfants-table").Should().NotBeNull();
        });

    [Fact]
    public async Task ClickingSheetSummary_CollapsesTable_AndRemovesItFromTheDom() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            cut.Find("#generated-sheet-Parents-details-toggle").Click();

            cut.FindAll("#generated-sheet-Parents-table").Should().BeEmpty();
            cut.Find("#generated-sheet-Parents-details-toggle").ParentElement!.HasAttribute("open").Should().BeFalse();
            cut.Find("#generated-sheet-Enfants-table").Should().NotBeNull();
        });

    [Fact]
    public async Task ClickingSheetSummaryTwice_ReExpandsTable() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            cut.Find("#generated-sheet-Parents-details-toggle").Click();
            cut.Find("#generated-sheet-Parents-details-toggle").Click();

            cut.Find("#generated-sheet-Parents-table").Should().NotBeNull();
            cut.Find("#generated-sheet-Parents-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
        });

    [Fact]
    public async Task CollapsingOneSheet_LeavesOtherSheetExpanded() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            cut.Find("#generated-sheet-Enfants-details-toggle").Click();

            cut.FindAll("#generated-sheet-Enfants-table").Should().BeEmpty();
            cut.Find("#generated-sheet-Parents-table").Should().NotBeNull();
        });

    [Fact]
    public async Task GeneratedSheetTables_AreWrappedInAStickyHeaderScrollContainer() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            foreach (var sheetName in new[] { "Parents", "Enfants" })
            {
                var table = cut.Find($"#generated-sheet-{sheetName}-table");
                table.ClassList.Should().Contain("generated-sheet-table");
                table.ParentElement!.ClassList.Should().Contain("generated-sheet-scroll");
            }
        });

    [Fact]
    public async Task GeneratedSheetSummaries_DisplayItemCountsNextToSheetName_ExcludingFacticeTacheMultipleRows() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileWithTacheMultipleRuleAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            // Compute expectations independently, straight from the real pipeline, rather than hardcoding
            // fixture-specific numbers that could silently drift if the fixture changes.
            var orchestrator = Services.GetRequiredService<IImportPipelineOrchestrator>();
            using var reader = new ClosedXmlWorkbookReader(File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx")));
            var importResult = orchestrator.Run(reader, importProfile);

            var expectedIsolementCount = importResult.Isolements.Count;
            var expectedMadCount = importResult.TachesMultiples.Count(t => t.Ordre.HasValue && t.TypeTacheMultipleCode == "TM_PROC_MAD");
            var expectedRelCount = importResult.TachesMultiples.Count(t => t.Ordre.HasValue && t.TypeTacheMultipleCode == "TM_PROC_REL");

            // Sanity: this fixture genuinely has at least one "ligne de mise en page" (blank Ordre) row,
            // so the exclusion below is actually exercised, not vacuously true.
            importResult.TachesMultiples.Count(t => !t.Ordre.HasValue).Should().BeGreaterThan(0);

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#generated-sheet-Parents-table").Should().NotBeEmpty());

            cut.Find("#generated-sheet-Parents-details-toggle").TextContent.Should().Contain("Parents (1)");
            cut.Find("#generated-sheet-Enfants-details-toggle").TextContent.Should().Contain($"Enfants ({expectedIsolementCount})");
            cut.Find("#generated-sheet-TM_PROC_MAD-details-toggle").TextContent.Should().Contain($"TM_PROC_MAD ({expectedMadCount})");
            cut.Find("#generated-sheet-TM_PROC_REL-details-toggle").TextContent.Should().Contain($"TM_PROC_REL ({expectedRelCount})");
        });

    [Fact]
    public void Component_NeverReferencesHttpClientOrExcelProcessingClient()
    {
        var source = File.ReadAllText(ComponentSourcePath());

        source.Should().NotContain("HttpClient");
        source.Should().NotContain("ExcelProcessingClient");
        source.Should().NotContain("IExcelDownloadInterop");
    }

    [Fact]
    public void BackToListButton_NavigatesToExportProfileList() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileTest>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-export-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles");
    });

    // V10: same Bootstrap-native upload styling as ImportProfileTestTests -- see its comment.
    [Fact]
    public void FileInput_IsWrappedInInputGroupWithIcon_AndHasLargeSizeClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileTest>();

        var inputFileElement = cut.Find("#export-test-file-input");
        inputFileElement.ClassList.Should().Contain("form-control");
        inputFileElement.ClassList.Should().Contain("form-control-lg");

        var wrapper = inputFileElement.ParentElement!;
        wrapper.ClassList.Should().Contain("input-group");
        wrapper.QuerySelector(".input-group-text svg").Should().NotBeNull();
    });
}
