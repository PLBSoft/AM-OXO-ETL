using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Tests.Layout;
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
    public async Task ClickingGenerate_WithoutExportProfileSelected_ShowsError_WithRoleAlert() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#generate-workbook-button").Should().NotBeEmpty());
            cut.Find("#generate-workbook-button").Click();

            // Lot 040 (40.1): the top-level generation-error alert-danger block.
            cut.Markup.Should().Contain("Select an export profile.");
            cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
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
            // Lot 040 (40.1): the per-file "rejected" block is also an alert-danger.
            cut.Find("#rejected").GetAttribute("role").Should().Be("alert");

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

    // Client-reported gap: a file badged "Warning" (BatchFileStatus.Warning, e.g. D8570's non-blocking
    // "VANNE" UnrecognizedTypeElement) showed no way to see what the warning actually was on this page,
    // unlike ImportProfileTest.razor's own warnings table -- fixed by rendering the same
    // details/summary + table for ImportResult.Errors, right where the import page shows it.
    [Fact]
    public async Task Run_D8570Fixture_ShowsWarningsDetails_WithSameShapeAsImportPage() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("Non-blocking warnings"));

            cut.Find("#warnings-table").Should().NotBeNull();
            cut.Find("#warnings-table").InnerHtml.Should().Contain("UnrecognizedTypeElement");
        });

    [Fact]
    public async Task WarningsDetails_TogglesOpenAndClosed() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("UnrecognizedTypeElement"));

            cut.Find("#warnings-details-toggle").Click();
            cut.FindAll("#warnings-table").Should().BeEmpty();

            cut.Find("#warnings-details-toggle").Click();
            cut.Find("#warnings-table").Should().NotBeNull();
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

    // V13: result block as a card component instead of a plain alert, content unchanged.
    [Fact]
    public async Task GeneratedWorkbookResultBlock_IsACardWithShadowAndSuccessTint_NotAPlainAlert() =>
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

            cut.WaitForAssertion(() => cut.FindAll("#download-generated-workbook-link").Should().NotBeEmpty());

            var resultBlock = cut.Find("#download-generated-workbook-link").ParentElement!.ParentElement!;
            resultBlock.ClassList.Should().Contain("card");
            resultBlock.ClassList.Should().Contain("shadow-sm");
            resultBlock.ClassList.Should().Contain("bg-success-subtle");
            resultBlock.ClassList.Should().NotContain("alert-success");
            cut.Find("#generated-sheet-Parents-table").Should().NotBeNull();
        });

    // V11: large (44-48px) touch targets on the selects and action buttons.
    [Fact]
    public void ImportProfileSelect_HasLargeSizeClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileTest>();

        cut.Find("#export-test-import-profile-select").ClassList.Should().Contain("form-select-lg");
    });

    [Fact]
    public async Task ExportProfileSelectAndActionButtons_HaveLargeSizeClasses() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();

            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            cut.Find("#export-test-export-profile-select").ClassList.Should().Contain("form-select-lg");
            cut.Find("#generate-workbook-button").ClassList.Should().Contain("btn-lg");

            // V12: full width below md, natural width from md up -- the primary action button only.
            cut.Find("#generate-workbook-button").ClassList.Should().Contain("w-100");
            cut.Find("#generate-workbook-button").ClassList.Should().Contain("w-md-auto");

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            cut.WaitForAssertion(() => cut.FindAll("#download-generated-workbook-link").Should().NotBeEmpty());
            cut.Find("#download-generated-workbook-link").ClassList.Should().Contain("btn-lg");
        });

    // X1 (Lot X): download button marks the successful end of the process -- green, full width,
    // large touch target, consistent with V11/V12's other buttons on this page.
    [Fact]
    public async Task DownloadButton_UsesSuccessColorAndFullWidth() =>
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

            cut.WaitForAssertion(() => cut.FindAll("#download-generated-workbook-link").Should().NotBeEmpty());
            var downloadLink = cut.Find("#download-generated-workbook-link");
            downloadLink.ClassList.Should().Contain("btn-success");
            downloadLink.ClassList.Should().Contain("w-100");
            downloadLink.ClassList.Should().Contain("btn-lg");
            downloadLink.ClassList.Should().NotContain("btn-secondary");
        });

    // V9: same de-emphasized intro paragraph as ImportProfileTestTests.
    [Fact]
    public void IntroParagraph_IsDeEmphasized_ButTextIsUnchanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileTest>();

        var intro = cut.Find("p.text-muted.small");
        intro.TextContent.Should().Contain("Upload an .xlsx file to run it through the extraction pipeline");
    });

    // X11 (Lot X): same shared-top-row-host rendering as ImportProfileTestTests -- see its comment.
    private IRenderedComponent<SectionOutletTestHost> RenderWithBackNavHost()
        => Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ExportProfileTest>(0);
                b.CloseComponent();
            })));

    [Fact]
    public void BackToListButton_NavigatesToExportProfileList() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-export-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles");
    });

    // X11: back link now lives in the shared top-row banner -- see ImportProfileTestTests' comment.
    [Fact]
    public void BackToListButton_HasAriaLabel_AndLivesInsideTheSharedTopRow() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();

        var backButton = cut.Find("#back-to-export-profiles-button");
        backButton.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
        backButton.QuerySelector("svg").Should().NotBeNull();

        var topRow = cut.Find(".top-row");
        topRow.QuerySelector("#back-to-export-profiles-button").Should().NotBeNull();
        topRow.QuerySelector(".navbar-brand").Should().NotBeNull();
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

    [Fact]
    public void FileInput_HasMultipleAttribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileTest>();

        cut.Find("#export-test-file-input").HasAttribute("multiple").Should().BeTrue();
    });

    // Lot 033: <InputFile multiple> batch validation (33.1) -- reject before any file is processed.
    [Fact]
    public async Task SelectingTwentyOneFiles_ShowsTooManyFilesError_AndProcessesNothing() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var files = Enumerable.Range(0, 21)
                .Select(i => InputFileContent.CreateFromText("dummy", $"f{i}.xlsx"))
                .ToArray();

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("21 files selected, the maximum is 20"));
            cut.FindAll("#batch-summary").Should().BeEmpty();
            // Lot 040 (40.1): ExportProfileTest.razor's own alert-danger block.
            cut.Find("#export-test-status").GetAttribute("role").Should().Be("alert");
        });

    [Fact]
    public async Task SelectingElevenMegabyteFile_ShowsFileTooLargeError_NamingTheFile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var bytes = new byte[11 * 1024 * 1024];
            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(InputFileContent.CreateFromBinary(bytes, "big.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("big.xlsx"));
            cut.Markup.Should().Contain("exceed the maximum size of 10 MB");
            cut.FindAll("#batch-summary").Should().BeEmpty();
        });

    [Fact]
    public void StatusRegion_HasAriaLivePolite_PresentFromInitialRender_BeforeAnyProcessing() =>
        WithCulture("en-US", () =>
        {
            // Lot 040 (40.2): same stable aria-live wrapper as ImportProfileTest.razor -- see its
            // comment there.
            var cut = Render<ExportProfileTest>();

            cut.Find("#export-test-status-region").GetAttribute("aria-live").Should().Be("polite");
        });

    [Fact]
    public async Task StatusRegion_AfterBatchProcessing_StillHasAriaLivePolite_AndSummaryTextUnchanged() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());

            cut.Find("#export-test-status-region").GetAttribute("aria-live").Should().Be("polite");
            cut.Find("#batch-summary").TextContent.Should().Contain("1 file(s) processed:");
        });

    // Lot 033 (33.3): sequential batch processing + per-file generation/download, mirroring 33.2.
    [Fact]
    public async Task BatchOfThreeRealFixtures_GeneratesEachFile_WithDownloadNamedFromSourceFileName() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var fixtureNames = new[]
            {
                "Dossier.de.MaD.IDL.-.C7401.xlsx",
                "Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx",
                "Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"
            };
            var files = fixtureNames.Select(FixtureAsInputFile).ToArray();

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            for (var i = 0; i < 3; i++)
            {
                cut.Find($"#file-details-toggle-{i}").Click();
            }

            cut.WaitForAssertion(() => cut.FindAll("a[id^='download-generated-workbook-link']").Should().HaveCount(3));

            foreach (var name in fixtureNames)
            {
                var expectedDownloadName = $"{Path.GetFileNameWithoutExtension(name)}_export.xlsx";
                cut.Markup.Should().Contain($"download=\"{expectedDownloadName}\"");
            }
        });

    [Fact]
    public async Task BatchWithOneRejectedFile_SkipsGenerationForItOnly_OthersGeneratedNormally() =>
        await WithCultureAsync("en-US", async () =>
        {
            var mockEngine = new Mock<ISheetGenerationEngine>();
            mockEngine.Setup(e => e.Generate(It.IsAny<ImportResult>(), It.IsAny<ExportProfile>()))
                .Returns(new GeneratedWorkbook([]));
            Services.AddSingleton(mockEngine.Object);

            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            using var invalidWorkbook = new ClosedXML.Excel.XLWorkbook();
            invalidWorkbook.Worksheets.Add("PROCEDURE"); // M2:O2 left blank -> whole-file rejection
            using var invalidStream = new MemoryStream();
            invalidWorkbook.SaveAs(invalidStream);

            var files = new[]
            {
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"),
                InputFileContent.CreateFromBinary(invalidStream.ToArray(), "invalid.xlsx"),
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx")
            };

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            mockEngine.Verify(e => e.Generate(It.IsAny<ImportResult>(), It.IsAny<ExportProfile>()), Times.Exactly(2));

            for (var i = 0; i < 3; i++)
            {
                cut.Find($"#file-details-toggle-{i}").Click();
            }

            cut.Markup.Should().Contain("File rejected");
            cut.FindAll("#download-generated-workbook-link-1").Should().BeEmpty();
        });

    [Fact]
    public async Task BatchWithOneCorruptedFile_ShowsTechnicalError_OthersGeneratedNormally() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var exportProfile = await SeedRealExportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var files = new[]
            {
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"),
                InputFileContent.CreateFromText("not an excel file", "corrupt.xlsx"),
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx")
            };

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());
            var summary = cut.Find("#batch-summary").TextContent;
            summary.Should().Contain("2 non-blocking warning(s)");
            summary.Should().Contain("1 technical error(s)");

            SelectExportProfile(cut, exportProfile.Id);
            cut.Find("#generate-workbook-button").Click();

            for (var i = 0; i < 3; i++)
            {
                cut.Find($"#file-details-toggle-{i}").Click();
            }

            cut.Find("#technical-error-1").Should().NotBeNull();
            cut.FindAll("a[id^='download-generated-workbook-link']").Should().HaveCount(2);
        });

    [Fact]
    public async Task SingleFileBatch_FileLevelAccordion_IsExpandedByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#export-test-export-profile-select").Should().NotBeEmpty());

            cut.Find("#file-details-toggle-0").ParentElement!.HasAttribute("open").Should().BeTrue();
        });

    [Fact]
    public async Task MultiFileBatch_FileLevelAccordions_AreCollapsedByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = await SeedRealImportProfileAsync();
            var cut = Render<ExportProfileTest>();
            SelectImportProfile(cut, importProfile.Id);

            var files = new[]
            {
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"),
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx")
            };
            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());

            cut.Find("#file-details-toggle-0").ParentElement!.HasAttribute("open").Should().BeFalse();
            cut.Find("#file-details-toggle-1").ParentElement!.HasAttribute("open").Should().BeFalse();
        });
}
