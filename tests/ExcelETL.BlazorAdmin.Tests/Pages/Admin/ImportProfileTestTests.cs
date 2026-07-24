using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// F2.1 covers the upload/render mechanics (no HTTP, unlike UploadTestTests -- the pipeline runs in
// process against the uploaded stream). F2.2's per-fixture assertions (one test per real client
// file, including D8570's "VANNE" non-blocking warning) live in this same class rather than a
// second file, since both need the identical DI wiring and fixture-path helper below.
public class ImportProfileTestTests : BunitContext
{
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    public ImportProfileTestTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileTestTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
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

    private async Task<ImportProfile> SeedRealProfileAsync()
    {
        var profile = CreateRealProfile();
        var store = Services.GetRequiredService<IImportProfileStore>();
        await store.SaveAsync(profile);
        return profile;
    }

    private void SelectProfile(IRenderedComponent<ImportProfileTest> cut, Guid profileId)
    {
        cut.WaitForState(() => cut.FindAll("#test-profile-select option").Count > 1);
        cut.Find("#test-profile-select").Change(profileId.ToString());
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

    // Matches ImportPipelineOrchestratorIntegrationTests' hardcoded profile exactly (Infrastructure.Tests)
    // -- same already-validated cell ranges for all 6 sheets, so this UI is proven against the same
    // configuration the pipeline's own regression guard-rail already exercises.
    private static ImportProfile CreateRealProfile() => new(
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

    // V9: de-emphasized intro paragraph -- text unchanged (same resx key), only presentation.
    [Fact]
    public void IntroParagraph_IsDeEmphasized_ButTextIsUnchanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        var intro = cut.Find("p.text-muted.small");
        intro.TextContent.Should().Contain("Upload an .xlsx file to run it through the extraction pipeline in memory");
    });

    [Fact]
    public void ImportProfileTest_WithNoProfiles_RendersFileInputAndNoProfilesMessage() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        cut.FindComponent<InputFile>().Should().NotBeNull();
        cut.Markup.Should().Contain("No import profiles exist yet.");
    });

    // V10: Bootstrap-native upload styling -- the critical non-regression check is that
    // FindComponent<InputFile>()/UploadFiles still work at all (proven unmodified by every other
    // upload test in this file, per the ticket's own explicit instruction not to rewrite them).
    [Fact]
    public void FileInput_IsWrappedInInputGroupWithIcon_AndHasLargeSizeClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        var inputFileElement = cut.Find("#test-file-input");
        inputFileElement.ClassList.Should().Contain("form-control");
        inputFileElement.ClassList.Should().Contain("form-control-lg");

        var wrapper = inputFileElement.ParentElement!;
        wrapper.ClassList.Should().Contain("input-group");
        wrapper.QuerySelector(".input-group-text svg").Should().NotBeNull();
    });

    [Fact]
    public void BackToListButton_NavigatesToImportProfileList() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-import-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles");
    });

    // V8: back link moved into a thin page banner, icon-only styling, but same id/navigation.
    [Fact]
    public void BackToListButton_HasAriaLabel_AndLivesInsideThePageBanner() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        var backButton = cut.Find("#back-to-import-profiles-button");
        backButton.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
        backButton.QuerySelector("svg").Should().NotBeNull();
        backButton.ParentElement!.ClassList.Should().Contain("page-banner");
    });

    [Fact]
    public async Task SelectingFile_WithoutProfileSelected_ShowsErrorAndDoesNotProcess() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();

            var inputFileComponent = cut.FindComponent<InputFile>();
            var file = InputFileContent.CreateFromText("dummy content", "dossier.xlsx");
            inputFileComponent.UploadFiles(file);

            cut.Markup.Should().Contain("Select an import profile.");
            cut.Markup.Should().NotContain("Equipement");
        });

    [Fact]
    public async Task SelectingFile_ThatFailsProcedureValidation_ShowsRejectedFileSection_NotAsAWarning() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            workbook.Worksheets.Add("PROCEDURE"); // M2:O2 left blank -> whole-file rejection
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var inputFileComponent = cut.FindComponent<InputFile>();
            var file = InputFileContent.CreateFromBinary(stream.ToArray(), "invalid.xlsx");
            inputFileComponent.UploadFiles(file);

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("File rejected"));
            cut.Markup.Should().NotContain("Non-blocking warnings");
        });

    [Fact]
    public async Task Run_C7401Fixture_RendersEquipementIsolementsPointsAndTachesMultiples_NoBlockingErrors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Markup.Should().NotContain("File rejected");
            cut.Find("#equipement-table").Should().NotBeNull();
            cut.Find("#isolements-table").Should().NotBeNull();
            cut.Find("#points-table").Should().NotBeNull();
            cut.Find("#taches-multiples-table").Should().NotBeNull();
            cut.FindAll("#isolements-table tbody tr").Should().HaveCount(23);
        });

    [Fact]
    public async Task Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("644-D8570"));

            cut.Markup.Should().NotContain("File rejected");
            cut.Find("#isolements-table").Should().NotBeNull();
            cut.FindAll("#isolements-table tbody tr").Should().HaveCount(67);
            cut.Markup.Should().Contain("Non-blocking warnings");
            cut.Markup.Should().Contain("UnrecognizedTypeElement");
        });

    [Fact]
    public async Task Run_G6306BFixture_RendersExpectedIsolementCount_NoBlockingErrors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("602-G6306B"));

            cut.Markup.Should().NotContain("File rejected");
            cut.FindAll("#isolements-table tbody tr").Should().HaveCount(18);
        });

    [Fact]
    public async Task ResultTables_AreOpenByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Find("#equipement-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
            cut.Find("#isolements-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
            cut.Find("#equipement-table").Should().NotBeNull();
        });

    [Fact]
    public async Task ClickingSummary_CollapsesTable_AndRemovesItFromTheDom() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Find("#equipement-details-toggle").Click();

            cut.FindAll("#equipement-table").Should().BeEmpty();
            cut.Find("#equipement-details-toggle").ParentElement!.HasAttribute("open").Should().BeFalse();
            cut.Find("#isolements-table").Should().NotBeNull();
        });

    [Fact]
    public async Task ClickingSummaryTwice_ReExpandsTable() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Find("#equipement-details-toggle").Click();
            cut.Find("#equipement-details-toggle").Click();

            cut.Find("#equipement-table").Should().NotBeNull();
            cut.Find("#equipement-details-toggle").ParentElement!.HasAttribute("open").Should().BeTrue();
        });

    [Fact]
    public async Task CollapsingOneTable_LeavesOtherTablesExpanded() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("644-D8570"));

            cut.Find("#isolements-details-toggle").Click();

            cut.FindAll("#isolements-table").Should().BeEmpty();
            cut.Find("#equipement-table").Should().NotBeNull();
            cut.Find("#points-table").Should().NotBeNull();
            cut.Find("#taches-multiples-table").Should().NotBeNull();
            cut.Find("#warnings-table").Should().NotBeNull();
        });

    [Fact]
    public async Task ResultTables_AreWrappedInAStickyHeaderScrollContainer() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("644-D8570"));

            foreach (var tableId in new[] { "equipement-table", "isolements-table", "points-table", "taches-multiples-table", "warnings-table" })
            {
                var table = cut.Find($"#{tableId}");
                table.ClassList.Should().Contain("test-table");
                table.ParentElement!.ClassList.Should().Contain("test-table-scroll");
            }
        });

    [Fact]
    public async Task WarningsSection_CanBeCollapsedAndExpanded() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("UnrecognizedTypeElement"));

            cut.Find("#warnings-details-toggle").Click();
            cut.FindAll("#warnings-table").Should().BeEmpty();

            cut.Find("#warnings-details-toggle").Click();
            cut.Find("#warnings-table").Should().NotBeNull();
        });
}
