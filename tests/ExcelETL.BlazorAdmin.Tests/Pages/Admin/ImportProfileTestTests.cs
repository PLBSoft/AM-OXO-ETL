using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.BlazorAdmin.Tests.Pages;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
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
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// F2.1 covers the upload/render mechanics (no HTTP, unlike UploadTestTests -- the pipeline runs in
// process against the uploaded stream). F2.2's per-fixture assertions (one test per real client
// file, including a non-blocking warning and, since lot 085, a duplicated-repère rejection) live in this same class rather than a
// second file, since both need the identical DI wiring and fixture-path helper below.
public class ImportProfileTestTests : BunitContext
{
    private const string ReperePrefix = "OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    public ImportProfileTestTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileTestTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IConditionalPointRuleEvaluator, ConditionalPointRuleEvaluator>();
        Services.AddSingleton<IRepeatingBlockReader, RepeatingBlockReader>();
        Services.AddSingleton<IHeaderRuleResolver, HeaderRuleResolver>();
        Services.AddSingleton<ILogger<ProcedureExtractionService>>(NullLogger<ProcedureExtractionService>.Instance);
        Services.AddSingleton<ILogger<ImportPipelineOrchestrator>>(NullLogger<ImportPipelineOrchestrator>.Instance);
        Services.AddSingleton<IProcedureExtractionService, ProcedureExtractionService>();
        Services.AddSingleton<IElementSheetExtractionService, ElementSheetExtractionService>();
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

    private async Task SeedSecondMinimalProfileAsync()
    {
        var locator = new RepeatingBlockLocator(
            firstBlockStartRow: 9,
            step: 7,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);
        var profile = new ImportProfile("Second profile", EquipementTypeElementNom, [], [], [sheetRule]);

        var store = Services.GetRequiredService<IImportProfileStore>();
        await store.SaveAsync(profile);
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

    // Lot 031: computes the expected element counts by running the real pipeline directly against
    // the fixture, mirroring exactly what the page itself does -- so section-title assertions never
    // pin a magic number that could silently drift from the actual orchestrator output.
    private ImportResult RunOrchestratorDirectly(ImportProfile profile, string fixtureFileName)
    {
        var orchestrator = Services.GetRequiredService<IImportPipelineOrchestrator>();
        using var stream = File.OpenRead(FixturePath(fixtureFileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return orchestrator.Run(workbookReader, profile);
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
                new RepeatingBlockLocator(9, 1,
                [
                    new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
                ]),
                [],
                [],
                [
                    new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, "M2:O2", stripReperePrefix: true),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, "P2:Q2"),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, "R2:T2", dateFormat: "dd/MM/yyyy")
                ],
                [
                    new HeaderCompositeRule(
                        ProcedureHeaderFieldNames.Designation,
                        $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
                ]),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator(19, 7,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:U", -1, 0, isRequired: false),
                    new BlockFieldDefinition(ElementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "K6:T6")], [],
                warnWhenNoConditionalPoint: true),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator(17, 8,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 5)
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
                ],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "K6:U6")], []),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator(17, 8,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "K6:U6")], []),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator(17, 7,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "N6")],
                [],
                warnWhenNoConditionalPoint: true),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator(9, 3,
                [
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(ElementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [],
                [
                    new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "N6"),
                    new HeaderFieldRule("zone", "B6:E6")
                ],
                [],
                warnWhenNoConditionalPoint: true)
        ]);

    // V13: result block as a card component instead of a plain alert, content unchanged.
    [Fact]
    public async Task Run_C7401Fixture_ResultBlock_IsACardWithShadowAndSuccessTint_NotAPlainAlert() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#equipement-table").Should().NotBeEmpty());

            var resultBlock = cut.Find("#test-status");
            resultBlock.ClassList.Should().Contain("card");
            resultBlock.ClassList.Should().Contain("shadow-sm");
            resultBlock.ClassList.Should().Contain("bg-success-subtle");
            resultBlock.ClassList.Should().NotContain("alert-success");
            cut.Find("#equipement-table").Should().NotBeNull();
        });

    // V11: large (44-48px) touch targets -- bUnit can't measure real pixels, so this checks the
    // Bootstrap size classes that produce them.
    [Fact]
    public void ProfileSelect_HasLargeSizeClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        cut.Find("#test-profile-select").ClassList.Should().Contain("form-select-lg");
    });

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

    // X11 (Lot X): the back link is now projected into the shared top-row banner via
    // SectionContent/SectionOutlet, no longer rendered in the page's own content flow -- rendering
    // ImportProfileTest alone (as before X11) would leave the SectionContent's content unrendered
    // anywhere (native SectionOutlet behavior with no matching outlet), so these tests now render
    // it inside SectionOutletTestHost, representative of NavMenu's real top-row.
    private IRenderedComponent<SectionOutletTestHost> RenderWithBackNavHost()
        => Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ImportProfileTest>(0);
                b.CloseComponent();
            })));

    [Fact]
    public void BackToListButton_NavigatesToImportProfileList() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-import-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles");
    });

    // X11: back link now lives in the shared top-row banner (common ancestor with the brand link),
    // not the page's own content -- same id/navigation/aria-label as the Lot V8 page-banner it
    // replaces.
    [Fact]
    public void BackToListButton_HasAriaLabel_AndLivesInsideTheSharedTopRow() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();

        var backButton = cut.Find("#back-to-import-profiles-button");
        backButton.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
        backButton.QuerySelector("svg").Should().NotBeNull();

        var topRow = cut.Find(".top-row");
        topRow.QuerySelector("#back-to-import-profiles-button").Should().NotBeNull();
        topRow.QuerySelector(".navbar-brand").Should().NotBeNull();
    });

    // X11: non-collision -- both the back link and the brand text are present simultaneously in
    // the same top-row container, neither one displacing the other.
    [Fact]
    public void BackToListButton_AndBrandLink_BothPresentSimultaneously_InTheSameTopRow() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();

        var topRow = cut.Find(".top-row");
        var backButton = topRow.QuerySelector("#back-to-import-profiles-button");
        var brandLink = topRow.QuerySelector(".navbar-brand");

        backButton.Should().NotBeNull();
        brandLink.Should().NotBeNull();
        brandLink!.TextContent.Should().Contain("Alpha - MAD / REL OXO");
    });

    [Fact]
    public async Task SelectingFile_WithoutProfileSelected_ShowsErrorAndDoesNotProcess() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedRealProfileAsync();
            // A single profile would be auto-selected on load -- seed a second one so this scenario
            // (no selection made) is still reachable.
            await SeedSecondMinimalProfileAsync();
            var cut = Render<ImportProfileTest>();

            var inputFileComponent = cut.FindComponent<InputFile>();
            var file = InputFileContent.CreateFromText("dummy content", "dossier.xlsx");
            inputFileComponent.UploadFiles(file);

            cut.Markup.Should().Contain("Select an import profile.");
            cut.Markup.Should().NotContain("Equipement");
            // Lot 040 (40.1): ImportProfileTest.razor's own alert-danger block.
            cut.Find("#test-status").GetAttribute("role").Should().Be("alert");
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
            // Lot 040 (40.1): the per-file "rejected" block is also an alert-danger.
            cut.Find("#rejected").GetAttribute("role").Should().Be("alert");
        });

    // Lot 042 (42.2): the rejected-file heading previously skipped straight from h1 to h4 -- fixed
    // to h2, keeping its pre-existing visual size via the Bootstrap `.h4` utility class.
    [Fact]
    public async Task SelectingFile_ThatFailsProcedureValidation_HasNoHeadingLevelSkip() =>
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

            HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
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
    public async Task Run_G4010AFixture_ShowsWarningAsNonBlocking_NotAsFileRejection() =>
        await WithCultureAsync("en-US", async () =>
        {
            // Lot 085: D8570 (used until then) is now rejected for duplicated repères.
            var profile = await SeedRealProfileAsync();
            var expected = RunOrchestratorDirectly(profile, "Dossier de MaD IDL -  G4010A.xlsx");
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("771-G4010A"));

            cut.Markup.Should().NotContain("File rejected");
            cut.Find("#isolements-table").Should().NotBeNull();
            cut.FindAll("#isolements-table tbody tr").Should().HaveCount(expected.Isolements.Count);
            cut.Markup.Should().Contain("Non-blocking warnings");
            cut.Markup.Should().Contain("NoConditionalPointCreated");

            // Lot 031: section title now shows the element count, same "{0} ({1})"-shaped format as
            // ExportProfileTest.razor's sheet titles (e.g. "Parents (1)").
            cut.Find("#isolements-details-toggle").TextContent.Should()
                .Contain($"Isolements ({expected.Isolements.Count})");
        });

    // Lot 085.6 (docs/tickets/tickets-tdd-lot-085-rejet-reperes-en-double.md): a file with a
    // duplicated repère shows as rejected, one line per duplicated repère naming every row (lot 086).
    [Fact]
    public async Task Run_LRS4504Fixture_ShowsDuplicatedRepereRowsAsFileRejection() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  LRS4504.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("File rejected"));
            cut.Markup.Should().NotContain("Non-blocking warnings");
            cut.FindAll("#rejected li").Select(li => li.TextContent).Should().Equal(
                "DIVERS / 9: Repère « LRS4504-LRS4504 » en double (DIVERS ligne 9, DIVERS ligne 12) : " +
                "chaque élément doit avoir une identification unique.");
        });

    // Lot 031: one test per remaining section (Equipement, Points, Taches multiples, Warnings),
    // computing the expected count by running the real pipeline directly against the same fixture
    // rather than a hardcoded magic number, per the ticket's own explicit requirement.
    [Fact]
    public async Task Run_C7401Fixture_SectionTitles_ShowActualElementCounts_FromTheRealPipelineRun() =>
        await WithCultureAsync("en-US", async () =>
        {
            const string fixtureFileName = "Dossier.de.MaD.IDL.-.C7401.xlsx";
            var profile = await SeedRealProfileAsync();
            var expected = RunOrchestratorDirectly(profile, fixtureFileName);

            expected.Equipement.Should().NotBeNull();

            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile(fixtureFileName));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Find("#equipement-details-toggle").TextContent.Should().Contain("Equipement (1)");
            cut.Find("#points-details-toggle").TextContent.Should().Contain($"Points ({expected.Points.Count})");
            cut.Find("#taches-multiples-details-toggle").TextContent.Should()
                .Contain($"Taches multiples ({expected.TachesMultiples.Count})");
            cut.Find("#isolements-details-toggle").TextContent.Should()
                .Contain($"Isolements ({expected.Isolements.Count})");
        });

    [Fact]
    public async Task Run_G4010AFixture_WarningsSectionTitle_ShowsActualNonBlockingErrorCount() =>
        await WithCultureAsync("en-US", async () =>
        {
            const string fixtureFileName = "Dossier de MaD IDL -  G4010A.xlsx";
            var profile = await SeedRealProfileAsync();
            var expected = RunOrchestratorDirectly(profile, fixtureFileName);

            expected.Errors.Should().NotBeEmpty();

            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile(fixtureFileName));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("NoConditionalPointCreated"));

            cut.Find("#warnings-details-toggle").TextContent.Should()
                .Contain($"Non-blocking warnings ({expected.Errors.Count})");
        });

    // Lot 055 §55.7: the context column shows the raw extracted value for NoConditionalPointCreated
    // entries, since there is no single block/repère to designate once no ConditionalPointRule
    // matched. G4010A has exactly one such entry (PROLOCK) -- selection by the stable per-row id, never
    // by text or position. (D8570, used until lot 085, is now rejected for duplicated repères; the
    // two-distinct-values dedup it showed stays covered at the ISOLEMENT sheet level.)
    [Fact]
    public async Task Run_G4010AFixture_WarningsTable_ShowsExtractedValueInContextColumn() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var expected = RunOrchestratorDirectly(profile, "Dossier de MaD IDL -  G4010A.xlsx");
            expected.Errors.Should().ContainSingle()
                .Which.Code.Should().Be(ExtractionErrorCode.NoConditionalPointCreated);

            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("NoConditionalPointCreated"));

            var contextValues = expected.Errors.Select((error, index) =>
                cut.Find($"#warning-context-{index}").TextContent).ToList();
            contextValues.Should().Equal("PROLOCK");
        });

    // The context column must stay unchanged for every other code -- TacheMultipleTypeMismatch keeps
    // rendering its original section-title BlockIdentifier, not an extracted value.
    [Fact]
    public async Task Run_C7401Fixture_WarningsTable_KeepsBlockIdentifierContextForTacheMultipleTypeMismatch() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var expected = RunOrchestratorDirectly(profile, "Dossier.de.MaD.IDL.-.C7401.xlsx");
            var mismatchIndex = expected.Errors.ToList()
                .FindIndex(e => e.Code == ExtractionErrorCode.TacheMultipleTypeMismatch);
            mismatchIndex.Should().BeGreaterThanOrEqualTo(0);
            var expectedBlockIdentifier = expected.Errors[mismatchIndex].BlockIdentifier;

            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("TacheMultipleTypeMismatch"));

            cut.Find($"#warning-context-{mismatchIndex}").TextContent.Should().Be(expectedBlockIdentifier);
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
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("771-G4010A"));

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
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("771-G4010A"));

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
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("NoConditionalPointCreated"));

            cut.Find("#warnings-details-toggle").Click();
            cut.FindAll("#warnings-table").Should().BeEmpty();

            cut.Find("#warnings-details-toggle").Click();
            cut.Find("#warnings-table").Should().NotBeNull();
        });

    // Lot 033: <InputFile multiple> batch validation (33.1) -- reject before any file is processed.
    [Fact]
    public async Task SelectingTwentyOneFiles_ShowsTooManyFilesError_AndProcessesNothing() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var files = Enumerable.Range(0, 21)
                .Select(i => InputFileContent.CreateFromText("dummy", $"f{i}.xlsx"))
                .ToArray();

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("21 files selected, the maximum is 20"));
            cut.FindAll("#batch-summary").Should().BeEmpty();
        });

    [Fact]
    public async Task SelectingExactlyTwentyFiles_IsAccepted_LimitIsInclusive() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var files = Enumerable.Range(0, 20)
                .Select(i => InputFileContent.CreateFromText("dummy", $"f{i}.xlsx"))
                .ToArray();

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());
            cut.Markup.Should().NotContain("files selected, the maximum is");
            cut.Find("#batch-summary").TextContent.Should().Contain("20 file(s) processed:");
        });

    [Fact]
    public void StatusRegion_HasAriaLivePolite_PresentFromInitialRender_BeforeAnyProcessing() =>
        WithCulture("en-US", () =>
        {
            // Lot 040 (40.2): the wrapper must already carry aria-live="polite" before any file is
            // ever selected -- inserting the whole subtree (including the attribute) at the same
            // moment content changes is not reliably announced by assistive technology.
            var cut = Render<ImportProfileTest>();

            cut.Find("#test-status-region").GetAttribute("aria-live").Should().Be("polite");
        });

    [Fact]
    public async Task StatusRegion_AfterBatchProcessing_StillHasAriaLivePolite_AndSummaryTextUnchanged() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());

            cut.Find("#test-status-region").GetAttribute("aria-live").Should().Be("polite");
            cut.Find("#batch-summary").TextContent.Should().Contain("1 file(s) processed:");
        });

    [Fact]
    public async Task SelectingElevenMegabyteFile_ShowsFileTooLargeError_NamingTheFile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var bytes = new byte[11 * 1024 * 1024];
            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(InputFileContent.CreateFromBinary(bytes, "big.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("big.xlsx"));
            cut.Markup.Should().Contain("exceed the maximum size of 10 MB");
            cut.FindAll("#batch-summary").Should().BeEmpty();
        });

    [Fact]
    public async Task SelectingExactlyTenMegabyteFile_IsAccepted_LimitIsInclusive() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var bytes = new byte[10 * 1024 * 1024];
            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(InputFileContent.CreateFromBinary(bytes, "exact.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());
            cut.Markup.Should().NotContain("exceed the maximum size");
        });

    // Lot 033 (33.2): sequential batch processing, summary, per-file accordion.
    [Fact]
    public async Task BatchOfThreeRealFixtures_ShowsSummaryAndPerFileAccordions_WithCorrectStatuses() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var files = new[]
            {
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"),
                FixtureAsInputFile("Dossier de MaD IDL -  G4010A.xlsx"),
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx")
            };

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(files);

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());

            // All 3 real fixtures currently carry their own non-blocking warning (C7401: Lot 032
            // TYPE-incoherence in PROCEDURE; G4010A: an ISOLEMENT "PROLOCK" NoConditionalPointCreated;
            // G6306B: the same kind of ISOLEMENT warning) -- none is a plain OK today. G4010A replaced
            // D8570 at lot 085 (D8570 is now rejected for duplicated repères).
            var summary = cut.Find("#batch-summary").TextContent;
            summary.Should().Contain("3 file(s) processed:");
            summary.Should().Contain("3 non-blocking warning(s)");
            summary.Should().NotContain(" OK");

            cut.FindAll(".batch-file-details").Should().HaveCount(3);
            cut.Markup.Should().Contain("Dossier.de.MaD.IDL.-.C7401.xlsx");
            cut.Markup.Should().Contain("Dossier de MaD IDL -  G4010A.xlsx");
            cut.Markup.Should().Contain("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");
        });

    [Fact]
    public async Task SingleFileBatch_FileLevelAccordion_IsExpandedByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            var inputFileComponent = cut.FindComponent<InputFile>();
            inputFileComponent.UploadFiles(FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"));

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("38-C7401"));

            cut.Find("#file-details-toggle-0").ParentElement!.HasAttribute("open").Should().BeTrue();
        });

    [Fact]
    public async Task MultiFileBatch_FileLevelAccordions_AreCollapsedByDefault() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

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

    [Fact]
    public async Task BatchWithOneRejectedFile_MixedWithValidFixtures_OnlyThatFileShowsRejected() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

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

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());

            // C7401/G6306B each carry their own non-blocking warning today (see the batch-of-3 test
            // above), so the two valid fixtures land as Warning, not OK.
            var summary = cut.Find("#batch-summary").TextContent;
            summary.Should().Contain("2 non-blocking warning(s)");
            summary.Should().Contain("1 rejected");

            for (var i = 0; i < 3; i++)
            {
                cut.Find($"#file-details-toggle-{i}").Click();
            }

            cut.Markup.Should().Contain("File rejected");
            cut.FindAll("#equipement-table-0").Should().NotBeEmpty();
            cut.FindAll("#equipement-table-1").Should().BeEmpty();
            cut.FindAll("#equipement-table-2").Should().NotBeEmpty();
        });

    [Fact]
    public async Task BatchWithOneWorkbookTheExcelLibraryCannotLoad_ShowsItAsRejectedWithTheReason_OthersProcessNormally() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

            cut.FindComponent<InputFile>().UploadFiles(
                FixtureAsInputFile("Dossier.de.MaD.IDL.-.C7401.xlsx"),
                UnreadableWorkbookAsInputFile("illisible.xlsx"));

            cut.WaitForAssertion(() => cut.FindAll("#batch-summary").Should().NotBeEmpty());
            var summary = cut.Find("#batch-summary").TextContent;
            summary.Should().Contain("1 rejected");
            summary.Should().NotContain("technical error");

            cut.Find("#file-details-toggle-1").Click();
            var rejected = cut.Find("#rejected-1").TextContent;
            rejected.Should().Contain("Classeur / illisible.xlsx");
            rejected.Should().Contain("n'a pas pu être lu");
            cut.FindAll("#technical-error-1").Should().BeEmpty();
        });

    [Fact]
    public async Task BatchWithOneCorruptedFile_ShowsTechnicalError_OthersProcessNormally() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedRealProfileAsync();
            var cut = Render<ImportProfileTest>();
            SelectProfile(cut, profile.Id);

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

            for (var i = 0; i < 3; i++)
            {
                cut.Find($"#file-details-toggle-{i}").Click();
            }

            cut.Find("#technical-error-1").Should().NotBeNull();
            cut.FindAll("#equipement-table-0").Should().NotBeEmpty();
            cut.FindAll("#equipement-table-2").Should().NotBeEmpty();

            // No exception bubbled up and broke rendering of the rest of the page.
            cut.Markup.Should().Contain("Dossier.de.MaD.IDL.-.C7401.xlsx");
            cut.Markup.Should().Contain("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");
        });

    [Fact]
    public void FileInput_HasMultipleAttribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileTest>();

        cut.Find("#test-file-input").HasAttribute("multiple").Should().BeTrue();
    });

    // A second sheet is declared in workbook.xml but its part is gone: a valid package ClosedXML
    // can't load (same class of failure as the production "Sequence contains no matching element").
    private static InputFileContent UnreadableWorkbookAsInputFile(string fileName)
    {
        using var stream = new MemoryStream();
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            workbook.Worksheets.Add("PROCEDURE");
            workbook.Worksheets.Add("ISOLEMENT");
            workbook.SaveAs(stream);
        }

        using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, isEditable: true))
        {
            var workbookPart = document.WorkbookPart!;
            workbookPart.DeletePart(workbookPart.WorksheetParts.Last());
        }

        return InputFileContent.CreateFromBinary(stream.ToArray(), fileName);
    }
}
