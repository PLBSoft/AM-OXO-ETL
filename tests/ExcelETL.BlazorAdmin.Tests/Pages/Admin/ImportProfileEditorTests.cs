using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.BlazorAdmin.Tests.Pages;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class ImportProfileEditorTests : BunitContext
{
    public ImportProfileEditorTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
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

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static ImportProfile BuildProfileWithOneSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // Mirrors the real ISOLEMENT sheet's client-reported example (ticket N0): FirstBlockStartRow=19,
    // Identification RowOffsetStart=0/End=1 (-> B19:E20), TypeElement RowOffsetStart=3/End=4 (-> B22:E23).
    private static ImportProfile BuildProfileWithIsolementSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 19,
            step: 7,
            stopFieldName: "Identification",
            fields:
            [
                new BlockFieldDefinition("Identification", "B:E", 0, 1),
                new BlockFieldDefinition("TypeElement", "B:E", 3, 4)
            ]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // Lot 048 (48.1): a PROCEDURE-shaped sheet rule carrying 3 HeaderFieldRules + 1 HeaderCompositeRule
    // -- transcribed from the real DefaultProfileSeeder shape (see the "Lot 047" bullet in CLAUDE.md).
    private static ImportProfile BuildProfileWithHeaderRules(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE",
            firstBlockStartRow: 9,
            step: 1,
            stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);

        var headerFields = new List<HeaderFieldRule>
        {
            new("nomMAD", new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
            new("revision", new DirectCell("PROCEDURE", "P2:Q2")),
            new("dateRev", new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy"),
        };
        var headerComposites = new List<HeaderCompositeRule>
        {
            new("Designation", "Rév {revision} du {dateRev}"),
        };

        var sheetRule = new SheetExtractionRule(
            "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], headerFields, headerComposites);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    private static ImportProfile BuildProfileWithTwoSheetRules(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        var platinesLocator = new RepeatingBlockLocator(
            "PLATINES",
            firstBlockStartRow: 17,
            step: 8,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var platinesRule = new SheetExtractionRule(
            "PLATINES", platinesLocator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [isolementRule, platinesRule]);
    }

    // Fills every field needed to pass the RepeatingBlockLocator/SheetExtractionRule build-up
    // (one block field, one unconditional colonne, one conditional point rule) and clicks
    // "add sheet rule", leaving the render handle positioned after the add.
    // Lot 057 (57.1): the add-sheet-rule form is closed by default in edit mode ("/{id}/edit") --
    // opens it via the toggle button only if it isn't already rendered (creation mode, "/new",
    // renders it open by default, so this is then a no-op). One helper reused everywhere the
    // add-form fields are touched, rather than inserting the same click at every call site.
    private static void OpenAddSheetRuleFormIfClosed(IRenderedComponent<ImportProfileEditor> cut)
    {
        if (cut.FindAll("#sheet-rule-name-input").Count == 0)
        {
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
        }
    }

    private static void AddValidSheetRule(IRenderedComponent<ImportProfileEditor> cut)
    {
        OpenAddSheetRuleFormIfClosed(cut);

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("7");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
        cut.Find("#add-unconditional-colonne-button").Click();

        cut.Find("#point-rule-colonne-name-input").Change("ZÉRO ENERGIE...");
        cut.Find("#point-rule-source-field-name-input").Change("TypeElement");
        cut.Find("#point-rule-comparison-value-input").Change("ZERO ENERGIE");
        cut.Find("#add-point-rule-button").Click();

        cut.Find("#add-sheet-rule-button").Click();
    }

    [Fact]
    public void NewProfile_PrefillsReperePrefixWithDefault_AndLeavesEquipementTypeElementNomEmpty() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-repere-prefix-input").GetAttribute("value").Should().Be(ImportProfile.DefaultReperePrefix);
            cut.Find("#profile-equipement-type-element-nom-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    [Fact]
    public async Task Save_WithEmptyName_DisplaysLocalizedErrorAndDoesNotPersist() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain("Name must not be empty.");
            // Lot 040 (40.1): assistive technology must be told programmatically that an error
            // appeared -- role="alert" implies aria-live="assertive" natively.
            cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");

            var all = await Store.GetAllAsync();
            all.Should().BeEmpty();
        });

    [Fact]
    public void Save_WithEmptyReperePrefix_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-repere-prefix-input").Change(string.Empty);

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Repere prefix must not be empty.");
    });

    [Fact]
    public void Save_WithEmptyEquipementTypeElementNom_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Equipement type element nom must not be empty.");
    });

    [Fact]
    public void Save_WithNoSheetRulesAdded_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Sheet rules must contain at least one rule.");
    });

    [Fact]
    // Lot 057 (57.1): this test's own premise -- the add-sheet-rule form stays open, merely reset
    // to blank, after a successful add -- is exactly what this lot changes: the form now closes
    // entirely (a genuine remount, not a reset), per Simon's own 29/07 decision that adding two
    // sheets in a row costs one extra click. Fixed in place: absence of the field (not merely an
    // empty value) is now the correct proof, and the toggle itself is asserted closed.
    public void AddSheetRule_WithValidInput_DisplaysSheetSummaryAndClosesForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Markup.Should().Contain("ISOLEMENT");
        cut.FindAll("#sheet-rule-name-input").Should().BeEmpty();

        // Lot R3: unconditional colonnes/conditional point rules are collapsed by default.
        cut.Markup.Should().NotContain("PROLOCK VANNES");
        cut.Find("#sheet-rule-details-toggle-0").Click();
        cut.Markup.Should().Contain("PROLOCK VANNES");
        cut.Markup.Should().Contain("ZÉRO ENERGIE...");
        cut.Markup.Should().Contain("TypeElement");
    });

    [Fact]
    public void AddSheetRule_WithNonPositiveStep_DisplaysLocalizedErrorAndDoesNotAddSheet() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("0");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#add-sheet-rule-button").Click();

        cut.Markup.Should().Contain("Step must be positive.");
        cut.Markup.Should().Contain("No sheet rules added yet.");
        // Lot 040 (40.1): SheetRuleForm's own alert-danger block.
        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
    });

    [Fact]
    public async Task Save_WithValidRootFieldsAndOneAddedSheetRule_PersistsProfileAndNavigatesToList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);

            cut.Find("#save-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/import-profiles");

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Name.Should().Be("MAD OXO");
            saved.EquipementTypeElementNom.Should().Be("MAD TRAVAUX");
            saved.ReperePrefix.Should().Be(ImportProfile.DefaultReperePrefix);
            saved.SheetRules.Should().ContainSingle();

            var rule = saved.SheetRules.Single();
            rule.SheetName.Should().Be("ISOLEMENT");
            rule.Locator.FirstBlockStartRow.Should().Be(9);
            rule.Locator.Step.Should().Be(7);
            rule.Locator.StopFieldName.Should().Be("Identification");
            rule.Locator.Fields.Should().ContainSingle(f => f.Name == "Identification" && f.ColumnRange == "B:E");
            rule.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES");
            rule.PointRules.Should().ContainSingle(r => r.ColonneName == "ZÉRO ENERGIE..." && r.ComparisonValue == "ZERO ENERGIE");
        });

    [Fact]
    public void NameInput_HasMaxLength60Attribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").GetAttribute("maxlength").Should().Be("60");
    });

    [Fact]
    public async Task Save_WithNameOver60Characters_DisplaysLocalizedErrorAndDoesNotNavigate() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Bypasses the #profile-name-input maxlength="60" HTML attribute -- .Change() sets the
            // bound model value directly, same as a user editing the DOM/devtools around the client-side
            // guard. The Domain constructor is the only real source of truth (see ImportProfile.MaxNameLength).
            cut.Find("#profile-name-input").Change(new string('A', 61));
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain("Name must not exceed 60 characters.");
            navigationManager.Uri.Should().NotEndWith("/import-profiles");

            var all = await Store.GetAllAsync();
            all.Should().BeEmpty();
        });

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

    [Fact]
    public async Task Save_WithNameOfAnAlreadyExistingProfile_DisplaysLocalizedErrorAndDoesNotNavigate() =>
        await WithCultureAsync("en-US", async () =>
        {
            await Store.SaveAsync(BuildProfileWithOneSheetRule("Profil OXO standard"));

            var cut = Render<ImportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#profile-name-input").Change("Profil OXO standard");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain("A profile named 'Profil OXO standard' already exists.");
            navigationManager.Uri.Should().NotEndWith("/import-profiles");

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
        });

    [Fact]
    public async Task Save_EditWithUnchangedName_SucceedsNormally_NoFalsePositiveOnSelf() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule("Profil OXO standard");
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#save-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/import-profiles");
            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
        });

    [Fact]
    public async Task EditRoute_WithExistingProfile_PrefillsRootFieldsAndSheetRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-name-input").GetAttribute("value").Should().Be("MAD OXO");
            cut.Find("#profile-repere-prefix-input").GetAttribute("value").Should().Be(ImportProfile.DefaultReperePrefix);
            cut.Find("#profile-equipement-type-element-nom-input").GetAttribute("value").Should().Be("MAD TRAVAUX");
            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("Identification");

            // Lot R3: unconditional colonnes are collapsed by default.
            cut.Markup.Should().NotContain("PROLOCK VANNES");
            cut.Find("#sheet-rule-details-toggle-0").Click();
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    [Fact]
    public async Task EditRoute_SaveAfterModification_UsesSameProfileId() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-V2-");
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Id.Should().Be(profile.Id);
            saved.ReperePrefix.Should().Be("MAD-OXO-V2-");
        });

    [Fact]
    public void EditRoute_WithUnknownId_DisplaysErrorAndDoesNotRenderForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.Markup.Should().Contain("Import profile not found.");
        cut.FindAll("#profile-name-input").Should().BeEmpty();
        cut.FindAll("#save-profile-button").Should().BeEmpty();
        // Lot 040 (40.1): same role="alert" treatment as every other alert-danger block.
        cut.Find("#import-profile-not-found").GetAttribute("role").Should().Be("alert");
    });

    [Fact]
    public void RootFields_HaveVisibleLabels_AssociatedByForAttribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='profile-name-input']").TextContent.Should().Be("Profile name");
        cut.Find("label[for='profile-repere-prefix-input']").TextContent.Should().Be("Repere prefix");
        cut.Find("label[for='profile-equipement-type-element-nom-input']").TextContent.Should().Be("Equipement type element name");
    });

    // Lot 030 (30.1/30.2): extends ExportProfileEditor's already-delivered X3 (full-width vertical
    // stacking, no col-md-* grid) and X4 (form-floating) patterns to the import side -- the reference
    // is Export's *actual* markup (a plain "mb-3" div, no "row"/"col-*" at all), not the ticket's own
    // "col-12" wording, confirmed by reading ExportProfileEditor.razor directly in 30.0.
    // Lot 053 (53.2): "Nom du profil" is the one root field this test still covers -- it's the
    // identitaire principal, deliberately excluded from the 2-column grid below. This test doubles
    // as the 53.5 guard-rail for this specific field: it must never gain a col-md-* class.
    [Fact]
    public void NameField_ContainerIsFullWidthFormFloating_WithNoColumnGridClass() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var floatingDiv = cut.Find("#profile-name-input").ParentElement!;
            floatingDiv.GetAttribute("class").Should().Be("form-floating");

            var container = floatingDiv.ParentElement!;
            container.GetAttribute("class").Should().Be("mb-3");
            container.GetAttribute("class").Should().NotContain("col-");
            container.GetAttribute("class").Should().NotContain("row");
        });

    // Lot 053 (53.2): reopens 30.1 above 768px for these two short fields -- corrected in place per
    // the ticket's own instruction, not doubled next to a now-false test.
    [Theory]
    [InlineData("profile-repere-prefix-input")]
    [InlineData("profile-equipement-type-element-nom-input")]
    public void ShortRootField_ContainerIsPairedTwoColumnGrid_AboveMobileBreakpoint(string inputId) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var floatingDiv = cut.Find($"#{inputId}").ParentElement!;
            floatingDiv.GetAttribute("class").Should().Be("form-floating");

            var container = floatingDiv.ParentElement!;
            container.GetAttribute("class").Should().Be("col-12 col-md-6");

            // 53.5: col-12 is always present alongside col-md-6, so mobile stays unaffected.
            container.GetAttribute("class").Should().Contain("col-12");

            container.ParentElement!.ClassList.Should().Contain("row");
        });

    [Fact]
    public void ShortRootFields_AreDirectChildrenOfTheSameRow() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        var reperePrefixColumn = cut.Find("#profile-repere-prefix-input").ParentElement!.ParentElement!;
        var equipementTypeElementNomColumn = cut.Find("#profile-equipement-type-element-nom-input").ParentElement!.ParentElement!;

        reperePrefixColumn.ParentElement.Should().Be(equipementTypeElementNomColumn.ParentElement);
        reperePrefixColumn.ParentElement!.ClassList.Should().Contain("row");
    });

    [Fact]
    public void DefaultTableauxAndApplications_AddFields_AreFullWidthFormFloating_InsideABgLightCard() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var tableauFloatingDiv = cut.Find("#default-tableau-name-input").ParentElement!;
            tableauFloatingDiv.GetAttribute("class").Should().Be("form-floating");

            var tableauCard = cut.Find("#default-tableau-name-input").Closest("div.card")!;
            tableauCard.GetAttribute("class").Should().Be("card bg-light mb-3");

            var applicationFloatingDiv = cut.Find("#default-application-name-input").ParentElement!;
            applicationFloatingDiv.GetAttribute("class").Should().Be("form-floating");

            var applicationCard = cut.Find("#default-application-name-input").Closest("div.card")!;
            applicationCard.GetAttribute("class").Should().Be("card bg-light mb-3");
        });

    // Lot 053 (53.3): the field and its button now share a single row, the field in "col-12
    // col-md" (remaining width) and the button in "col-12 col-md-auto" (natural width) -- above
    // 768px they sit on one line, below they stack full-width (unchanged from Lot V/30.3).
    [Fact]
    public void DefaultTableauxAndApplications_FieldAndButton_AreDirectChildrenOfSameRow() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var tableauFieldContainer = cut.Find("#default-tableau-name-input").ParentElement!.ParentElement!;
            var tableauButtonContainer = cut.Find("#add-default-tableau-button").ParentElement!;
            tableauFieldContainer.ParentElement.Should().Be(tableauButtonContainer.ParentElement);

            var applicationFieldContainer = cut.Find("#default-application-name-input").ParentElement!.ParentElement!;
            var applicationButtonContainer = cut.Find("#add-default-application-name-button").ParentElement!;
            applicationFieldContainer.ParentElement.Should().Be(applicationButtonContainer.ParentElement);
        });

    // Lot 063: fixed in place -- col-md/col-md-auto (flex-grow, flex-shrink:0, 0% basis) let the
    // input's browser-default intrinsic min-width overflow into the button once this card is
    // itself squeezed to half-width by the outer col-md-6 split (~768-1150px). Fixed proportional
    // columns avoid that content-driven overflow at every width in between.
    [Fact]
    public void DefaultTableauxAndApplications_FieldAndButtonContainers_HaveExpectedColumnClasses() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").ParentElement!.ParentElement!.GetAttribute("class").Should().Be("col-12 col-md-8");
            cut.Find("#add-default-tableau-button").ParentElement!.GetAttribute("class").Should().Be("col-12 col-md-4");

            cut.Find("#default-application-name-input").ParentElement!.ParentElement!.GetAttribute("class").Should().Be("col-12 col-md-8");
            cut.Find("#add-default-application-name-button").ParentElement!.GetAttribute("class").Should().Be("col-12 col-md-4");
        });

    // Lot 053 (53.3): the button is now at its row's right edge by construction, so the
    // .right-aligned-actions wrapper it used to need is gone -- convention-ui-blazor-alignement-
    // boutons.md already carves this exact case out, no amendment needed.
    [Fact]
    public void DefaultTableauxAndApplications_AddButtons_AreNotWrappedInRightAlignedActions() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#add-default-tableau-button").ParentElement!.ClassList.Should().NotContain("right-aligned-actions");
            cut.Find("#add-default-application-name-button").ParentElement!.ClassList.Should().NotContain("right-aligned-actions");
        });

    // Lot 053 (53.4): replaces the 30.3 test of the same button pair -- corrected in place, not
    // doubled, per the ticket's own instruction. Solid secondary + Plus icon + visible label
    // (never an icon-only button, so no aria-label/title is added here).
    [Fact]
    // Lot 058 (58.2): both buttons gained .field-inline-action so they can fill their column's
    // height alongside the adjacent field (>=768px only, see app.css).
    // Lot 063: w-md-auto (shrink-to-content at >=768px) is gone -- kept w-100 so the button always
    // fills its now-fixed col-md-4 column instead of a natural content width that could overflow
    // it, closing the same overlap the column-class fix above addresses.
    public void AddDefaultTableauAndApplication_AddButtons_AreSecondaryWithPlusIconAndVisibleLabel() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var tableauButton = cut.Find("#add-default-tableau-button");
            tableauButton.GetAttribute("class").Should().Be("btn btn-secondary w-100 field-inline-action d-flex align-items-center justify-content-center gap-1");
            tableauButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            tableauButton.TextContent.Trim().Should().NotBeNullOrEmpty();

            var applicationButton = cut.Find("#add-default-application-name-button");
            applicationButton.GetAttribute("class").Should().Be("btn btn-secondary w-100 field-inline-action d-flex align-items-center justify-content-center gap-1");
            applicationButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            applicationButton.TextContent.Trim().Should().NotBeNullOrEmpty();
        });

    [Theory]
    [InlineData("sheet-rule-name-input")]
    [InlineData("sheet-rule-first-block-start-row-input")]
    [InlineData("sheet-rule-step-input")]
    [InlineData("sheet-rule-stop-field-name-input")]
    [InlineData("block-field-name-input")]
    [InlineData("block-field-absolute-range-input")]
    [InlineData("unconditional-colonne-name-input")]
    [InlineData("point-rule-colonne-name-input")]
    [InlineData("point-rule-source-field-name-input")]
    [InlineData("point-rule-comparison-value-input")]
    public void SheetRuleForm_Field_ContainerIsFullWidthFormFloating_WithNoColumnGridClass(string inputId) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var floatingDiv = cut.Find($"#{inputId}").ParentElement!;
            floatingDiv.GetAttribute("class").Should().Be("form-floating");

            var container = floatingDiv.ParentElement!;
            container.GetAttribute("class").Should().Be("mb-3");
            container.GetAttribute("class").Should().NotContain("col-");
            container.GetAttribute("class").Should().NotContain("row");
        });

    [Fact]
    public void SheetRuleForm_FieldsPointRulesAndUnconditionalColonnesSubforms_AreEachWrappedInABgLightCard() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#block-field-name-input").Closest("div.card")!.GetAttribute("class").Should().Be("card bg-light mb-3");
            cut.Find("#unconditional-colonne-name-input").Closest("div.card")!.GetAttribute("class").Should().Be("card bg-light mb-3");
            cut.Find("#point-rule-colonne-name-input").Closest("div.card")!.GetAttribute("class").Should().Be("card bg-light mb-3");
        });

    // Lot 030 (30.3) / Lot 053 (53.4): "Ajouter le champ"/"Ajouter une colonne"/"Ajouter la règle
    // conditionnelle"/"Ajouter une règle de feuille" are multi-field forms -- 53.3's row/inline
    // treatment doesn't apply to them (strictly scoped to Tableaux/Applications), but they still get
    // 53.4's color+icon change: full-width secondary button with a Plus icon, staying at the bottom
    // of their form. Corrected in place from the 30.3 outline-button assertion, not doubled.
    [Fact]
    public void SheetRuleForm_IntermediateAddButtons_AreSecondaryFullWidthWithPlusIcon() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[] { "add-block-field-button", "add-unconditional-colonne-button", "add-point-rule-button", "add-sheet-rule-button" })
        {
            var button = cut.Find($"#{id}");
            button.GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
            button.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            button.TextContent.Trim().Should().NotBeNullOrEmpty();
        }
    });

    // Lot 053 (53.3, garde-fou de non-généralisation): multi-field add forms are explicitly out of
    // 53.3's scope -- "Ajouter le champ" must keep its bottom-of-form, .right-aligned-actions-wrapped
    // position, unlike Tableaux/Applications above.
    [Fact]
    public void AddBlockFieldButton_StaysAtBottomOfForm_NotConvertedToInlineRow() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        var button = cut.Find("#add-block-field-button");
        button.ParentElement!.ClassList.Should().Contain("right-aligned-actions");
        button.ParentElement!.GetAttribute("class").Should().NotContain("col-");
    });

    [Fact]
    public void SaveProfileButton_KeepsItsFullPrimaryClass_WhileIntermediateButtonsAreOutline() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#save-profile-button").GetAttribute("class").Should().Be("btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4 d-flex align-items-center justify-content-center gap-1");
        });

    [Fact]
    public async Task ExistingSheetRule_DisplaysModifyButton() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
        });

    // Lot 037: the sheet-rule-card's own Modify/Delete buttons were plain-text (btn-secondary/
    // btn-danger), unlike every other CRUD-action button in this file (block fields, tableaux,
    // applications, unconditional colonnes, point rules), all icon-only per
    // convention-ui-blazor-icones-boutons.md and already matching ExportProfileEditor.razor's own
    // sheet-rule-card buttons. IDs are unchanged -- only the button's internal content moves from
    // visible text to an aria-hidden icon plus an aria-label/title.
    [Fact]
    public async Task SheetRuleCard_ModifyDeleteButtons_AreIconOnly_WithAriaLabelsAndNoVisibleText() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var modifyButton = cut.Find("#modify-sheet-rule-button-0");
            modifyButton.GetAttribute("aria-label").Should().Be("Modify");
            modifyButton.GetAttribute("title").Should().Be("Modify");
            modifyButton.TextContent.Trim().Should().BeEmpty();
            modifyButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();

            var deleteButton = cut.Find("#delete-sheet-rule-button-0");
            deleteButton.GetAttribute("aria-label").Should().Be("Delete");
            deleteButton.GetAttribute("title").Should().Be("Delete");
            deleteButton.TextContent.Trim().Should().BeEmpty();
            deleteButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
        });

    [Fact]
    public async Task ClickingModify_SwitchesOnlyThatRuleIntoEditMode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-sheet-rule-name-input").GetAttribute("value").Should().Be("ISOLEMENT");
            cut.FindAll("#modify-sheet-rule-button-1").Should().HaveCount(1);
            cut.FindAll("#edit-1-sheet-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task EditMode_PrefillsRootLocatorFieldsWithExistingRuleValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-sheet-rule-name-input").GetAttribute("value").Should().Be("ISOLEMENT");
            cut.Find("#edit-0-sheet-rule-first-block-start-row-input").GetAttribute("value").Should().Be("9");
            cut.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("7");
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").GetAttribute("value").Should().Be("Identification");
            cut.Find(".block-field-name").TextContent.Should().Be("Identification");
            cut.Find(".block-field-range").TextContent.Should().Be("B9:E9");
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    [Fact]
    public async Task ExistingSheetRule_DisplaysAbsoluteExcelRanges_NotRawRowOffsets() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            // Scoped to the range-display elements themselves, not the whole page's markup --
            // since Lot 037 gave the sheet-rule-card's own Modify/Delete buttons an inline SVG
            // icon, the *unrelated* path data of that icon coincidentally contains the literal
            // substring "0-1", which would make a page-wide NotContain("0-1") assertion fail for
            // a reason that has nothing to do with the raw-row-offset regression this test guards.
            var rangeTexts = cut.FindAll(".block-field-range").Select(e => e.TextContent).ToList();
            rangeTexts.Should().Contain("B19:E20");
            rangeTexts.Should().Contain("B22:E23");
            rangeTexts.Should().NotContain("0-1");
            rangeTexts.Should().NotContain("3-4");
        });

    // Client feedback (screenshot, 2026-07-22): the read-only sheet-rule summary's field ranges
    // must render with the same name/range two-level, monospace-range styling as the edit form
    // (SheetRuleForm's own block-field list) -- not the plain "Name (Range)" inline text it had
    // before, since the two were visually inconsistent side by side.
    [Fact]
    public async Task Summary_DisplaysFieldNameAndRangeAsSeparateElements_WithMonospaceRangeClass_LikeEditMode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var names = cut.FindAll(".block-field-name");
            var ranges = cut.FindAll(".block-field-range");

            names.Should().Contain(e => e.TextContent == "Identification");
            names.Should().Contain(e => e.TextContent == "TypeElement");
            ranges.Should().Contain(e => e.TextContent == "B19:E20");
            ranges.Should().Contain(e => e.TextContent == "B22:E23");

            foreach (var range in ranges)
            {
                range.ClassList.Should().Contain("font-monospace");
            }
        });

    [Fact]
    public async Task EditMode_PrefillsConditionalPointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "ISOLEMENT",
                firstBlockStartRow: 9,
                step: 7,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var pointRule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...");
            var sheetRule = new SheetExtractionRule(
                "ISOLEMENT", locator, pointRules: [pointRule], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ZÉRO ENERGIE...");
            cut.Markup.Should().Contain("TypeElement");
        });

    // Ticket O2: the read-only sheet-rule summary must show the same section labels as the edit
    // form above the unconditional-colonnes / conditional-point-rules lists -- and only when the
    // underlying collection is non-empty.
    [Fact]
    public async Task Summary_WithUnconditionalColonnesAndPointRules_ShowsBothLabelsAndBulletLists() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "ISOLEMENT",
                firstBlockStartRow: 9,
                step: 7,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var pointRule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...");
            var sheetRule = new SheetExtractionRule(
                "ISOLEMENT", locator, pointRules: [pointRule],
                unconditionalColonneNames: ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            // Scoped to the read-only summary <li>, not the always-visible "Add a sheet rule" card
            // below it -- that card's own SheetRuleForm renders these same two headings unconditionally.
            var summaryItem = cut.Find("li.sheet-rule-card");
            var headings = summaryItem.QuerySelectorAll("h4").Select(h => h.TextContent).ToList();
            headings.Should().Contain("Unconditional colonnes (always create the Point)");
            headings.Should().Contain("Conditional point rules");

            var unconditionalHeading = summaryItem.QuerySelectorAll("h4")
                .Single(h => h.TextContent == "Unconditional colonnes (always create the Point)");
            var unconditionalList = unconditionalHeading.NextElementSibling!;
            unconditionalList.TagName.Should().Be("UL");
            unconditionalList.Children.Select(li => li.TextContent).Should().BeEquivalentTo("PROLOCK VANNES", "DEPROLOCK VANNES");

            var pointRulesHeading = summaryItem.QuerySelectorAll("h4")
                .Single(h => h.TextContent == "Conditional point rules");
            var pointRulesList = pointRulesHeading.NextElementSibling!;
            pointRulesList.TagName.Should().Be("UL");
            pointRulesList.TextContent.Should().Contain("ZÉRO ENERGIE...");
        });

    [Fact]
    public async Task Summary_WithOnlyUnconditionalColonnes_DoesNotShowConditionalPointRulesLabel() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PLATINES",
                firstBlockStartRow: 17,
                step: 8,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "PLATINES", locator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            var summaryItem = cut.Find("li.sheet-rule-card");
            var headings = summaryItem.QuerySelectorAll("h4").Select(h => h.TextContent).ToList();
            headings.Should().Contain("Unconditional colonnes (always create the Point)");
            headings.Should().NotContain("Conditional point rules");
        });

    [Fact]
    public async Task Summary_WithNeitherCollectionPopulated_ShowsNeitherLabel() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PROCEDURE",
                firstBlockStartRow: 9,
                step: 1,
                stopFieldName: "Action",
                fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var summaryItem = cut.Find("li.sheet-rule-card");
            summaryItem.QuerySelectorAll("h4").Should().BeEmpty();
        });

    // Ticket P1: each non-editing sheet rule in the summary is wrapped in its own visually
    // distinct card (one container per rule, not a shared list-group border).
    [Fact]
    public async Task Summary_WithMultipleSheetRules_WrapsEachInADistinctCard() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
        });

    // Lot R1: the sheet-rule cards' parent <ul> is a responsive CSS grid (auto-fill columns),
    // not a one-card-per-row flex stack -- so more of a 6-sheet profile is visible without
    // scrolling on a wide screen. Asserted on the class attribute only, per the ticket's own
    // instruction (bUnit doesn't compute real layout).
    [Fact]
    public async Task SheetRuleList_HasGridCssClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var list = cut.Find("ul.sheet-rule-list");
            list.ClassList.Should().Contain("sheet-rule-grid");

            // No regression on the number of cards or their content.
            cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
        });

    // Lot R2: the block-field list inside a read-only sheet-rule card is a compact multi-column
    // grid, not one field per full-width row.
    [Fact]
    public async Task BlockFieldList_InSummary_HasGridCssClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var fieldList = cut.Find("li.sheet-rule-card ul.block-field-list");
            fieldList.ClassList.Should().Contain("block-field-grid");

            // No regression on field content.
            cut.FindAll(".block-field-name").Select(e => e.TextContent).Should().BeEquivalentTo("Identification", "TypeElement");
        });

    // Lot R3: UnconditionalColonneNames/ConditionalPointRule are collapsed behind a details/summary,
    // closed by default -- the full list must be genuinely absent from the DOM, not just visually
    // hidden (same rule as L2/NavMenu: FindAll empty, not a display:none check).
    [Fact]
    public async Task SheetRuleSublistDetails_CollapsedByDefault_FullListIsAbsentFromDom() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
            cut.Find("li.sheet-rule-card").QuerySelectorAll("h4").Should().BeEmpty();

            var summary = cut.Find("#sheet-rule-details-toggle-0");
            summary.TextContent.Should().Contain("1");
        });

    [Fact]
    public async Task SheetRuleSublistDetails_ClickingSummary_ExpandsFullListWithSameValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    // Ticket R3 (correctif): clicking the summary a second time must collapse the sublist again --
    // no prior test exercised this bidirectional toggle explicitly.
    [Fact]
    public async Task SheetRuleSublistDetails_ClickingSummaryTwice_CollapsesAgain() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#sheet-rule-details-toggle-0");

            toggle.Click();
            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);

            toggle.Click();
            cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
        });

    // Ticket R3 (correctif): expanding one card's sublist must not affect any other card's state.
    [Fact]
    public async Task SheetRuleSublistDetails_ExpandingOneCard_DoesNotAffectOtherCards() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            cut.FindAll("#sheet-rule-details-content-1").Should().BeEmpty();
        });

    // Ticket R3 (correctif): a sheet rule with neither unconditional colonnes nor conditional point
    // rules must still expose a working, clickable toggle -- previously the whole <details> block
    // was omitted for this case, so there was nothing to click at all.
    [Fact]
    public async Task SheetRuleSublistDetails_WithEmptySublist_StillRendersToggle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithEmptySublistSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("0");
        });

    [Fact]
    public async Task SheetRuleSublistDetails_WithEmptySublist_ClickingShowsCoherentEmptyState() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithEmptySublistSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.Find("#sheet-rule-details-content-0").TextContent
                .Should().Contain("No unconditional colonnes, conditional point rules, header fields, or header composites for this sheet.");
            cut.Find("li.sheet-rule-card").QuerySelectorAll("h4").Should().BeEmpty();
        });

    // Ticket R3 (correctif), Refactor step: the native <details> element's `open` attribute must
    // reflect the C# expansion state so assistive technology gets correct disclosure semantics --
    // the click handler prevents the browser's own native toggle (@onclick:preventDefault), so
    // without this binding `open` would never be set by anything.
    [Fact]
    public async Task SheetRuleSublistDetails_OpenAttribute_ReflectsExpandedState() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var details = cut.Find("details.sheet-rule-sublist-details");
            details.HasAttribute("open").Should().BeFalse();

            cut.Find("#sheet-rule-details-toggle-0").Click();
            details = cut.Find("details.sheet-rule-sublist-details");
            details.HasAttribute("open").Should().BeTrue();
        });

    private static ImportProfile BuildProfileWithEmptySublistSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE",
            firstBlockStartRow: 9,
            step: 1,
            stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // Ticket's own requirement: the count shown in the (still-collapsed) summary must reflect
    // whatever the list currently holds, not a value captured at first render.
    [Fact]
    public void SheetRuleSublistDetails_SummaryCount_ReflectsCurrentListSize_NotFirstRenderValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B9:E9");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("1");

            // Add a second unconditional colonne via edit mode, save -- summary count must update.
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#edit-0-add-unconditional-colonne-button").Click();
            cut.Find("#save-sheet-rule-button-0").Click();

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("2");
        });

    [Fact]
    public async Task Summary_SheetNameAndMetadata_AreSeparateElements() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find(".sheet-rule-card-title").TextContent.Should().Be("ISOLEMENT");
            var meta = cut.Find(".sheet-rule-card-meta").TextContent;
            meta.Should().Contain("9");
            meta.Should().Contain("7");
            meta.Should().Contain("Identification");
            meta.Should().NotContain("ISOLEMENT");
        });

    // Ticket P1's own open question, resolved by reading the code before implementing: a rule
    // being edited is replaced in place by SheetRuleForm, not duplicated -- so it never shows up
    // as a second summary card while its edit panel is open.
    [Fact]
    public async Task EditingRule_IsNotRenderedAsACardOrDuplicatedInTheSummary() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.FindAll("li.sheet-rule-card").Should().HaveCount(1);
            cut.FindAll(".sheet-rule-card-title").Should().ContainSingle(e => e.TextContent == "PLATINES");
            cut.FindAll("#edit-0-sheet-rule-name-input").Should().HaveCount(1);
        });

    // Ticket P2: save-profile-button belongs to the root form, not a sheet-rule card, but is
    // governed by the same convention-ui-blazor-alignement-boutons.md rule.
    [Fact]
    public void SaveProfileButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#save-profile-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });

    // Lot Y (Y3, extended to the import side on client request): #save-profile-button is a
    // full-width/large CTA on mobile, natural-width on desktop (same V12 w-md-auto pattern already
    // applied to ExportProfileEditor's #save-export-profile-button), on both the /new and /edit
    // routes since they're the same component/button.
    [Fact]
    public void SaveProfileButton_IsFullWidthLargeCta_WithVerticalMargins_OnNewRoute() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var saveButton = cut.Find("#save-profile-button");

            saveButton.ClassList.Should().Contain("w-100");
            saveButton.ClassList.Should().Contain("w-md-auto");
            saveButton.ClassList.Should().Contain("btn-lg");
            saveButton.ClassList.Should().Contain("mt-4");
            saveButton.ClassList.Should().Contain("mb-4");
        });

    [Fact]
    public async Task SaveProfileButton_IsFullWidthLargeCta_WithVerticalMargins_OnEditRoute() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var saveButton = cut.Find("#save-profile-button");

            saveButton.ClassList.Should().Contain("w-100");
            saveButton.ClassList.Should().Contain("w-md-auto");
            saveButton.ClassList.Should().Contain("btn-lg");
            saveButton.ClassList.Should().Contain("mt-4");
            saveButton.ClassList.Should().Contain("mb-4");
        });

    [Fact]
    public async Task SaveChanges_UpdatesTheRuleInPlace_AndClosesEditMode_WithoutDuplicating() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-rule-step-input").Should().BeEmpty();
            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-rule-button-1").Should().BeEmpty();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var rule = all.Single().SheetRules.Should().ContainSingle().Subject;
            rule.Locator.Step.Should().Be(9);
        });

    [Fact]
    public async Task Cancel_ExitsEditMode_WithoutApplyingChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("999");
            cut.Find("#cancel-sheet-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-rule-step-input").Should().BeEmpty();

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("7");
        });

    // Client feedback (screenshot, 2026-07-22): deleting a sheet rule (unlike a single block field)
    // discards the whole rule's nested state, so it now requires an explicit confirmation step
    // instead of removing on the first click.
    [Fact]
    public async Task Delete_FirstClick_DoesNotRemoveTheRule_ShowsConfirmationInstead() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("Delete this sheet rule? This cannot be undone.");
            cut.FindAll("#confirm-delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#cancel-delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task Delete_Confirm_RemovesTheRuleFromTheInMemoryList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();
            cut.Find("#confirm-delete-sheet-rule-button-0").Click();

            cut.Markup.Should().NotContain("ISOLEMENT");
            cut.Markup.Should().Contain("PLATINES");
        });

    [Fact]
    public async Task Delete_Cancel_KeepsTheRuleAndRestoresTheOriginalButtons() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();
            cut.Find("#cancel-delete-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("PLATINES");
            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#confirm-delete-sheet-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task SaveProfile_PersistsEditedSheetRule_VisibleAfterReload() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            reloaded.Find("#modify-sheet-rule-button-0").Click();

            reloaded.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("9");
        });

    [Fact]
    public async Task AddingNewSheetRule_StillWorks_AfterEditingAnExistingRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();

            AddValidSheetRule(cut);

            cut.FindAll("#modify-sheet-rule-button-1").Should().HaveCount(1);
        });

    [Fact]
    public void SheetRuleForm_RootLocatorFields_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='sheet-rule-name-input']").TextContent.Should().Be("Sheet name");
        cut.Find("label[for='sheet-rule-first-block-start-row-input']").TextContent.Should().Be("First block start row");
        cut.Find("label[for='sheet-rule-step-input']").TextContent.Should().Be("Step");
        cut.Find("label[for='sheet-rule-stop-field-name-input']").TextContent.Should().Be("Stop field name");
    });

    // Client feedback (screenshot, 2026-07-22): every input in the "Add a sheet rule" section
    // should have a visible label, not just a placeholder -- covers the field name (BlockFieldForm),
    // the unconditional-colonne name, and the 4 conditional-point-rule inputs.
    [Fact]
    public void SheetRuleForm_RemainingInputs_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='block-field-name-input']").TextContent.Should().Be("Field name");
        cut.Find("label[for='unconditional-colonne-name-input']").TextContent.Should().Be("Colonne name");
        cut.Find("label[for='point-rule-colonne-name-input']").TextContent.Should().Be("Colonne name");
        cut.Find("label[for='point-rule-source-field-name-input']").TextContent.Should().Be("Source field name");
        cut.Find("label[for='point-rule-operator-select']").TextContent.Should().Be("Operator");
        cut.Find("label[for='point-rule-comparison-value-input']").TextContent.Should().Be("Comparison value");
    });

    [Fact]
    public async Task SheetRuleForm_EditMode_RootLocatorFields_HaveVisibleLabels() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("label[for='edit-0-sheet-rule-name-input']").TextContent.Should().Be("Sheet name");
            cut.Find("label[for='edit-0-sheet-rule-first-block-start-row-input']").TextContent.Should().Be("First block start row");
            cut.Find("label[for='edit-0-sheet-rule-step-input']").TextContent.Should().Be("Step");
            cut.Find("label[for='edit-0-sheet-rule-stop-field-name-input']").TextContent.Should().Be("Stop field name");
        });

    // Client feedback (screenshot, 2026-07-22): the Save/Cancel (or Add) buttons at the bottom of
    // a sheet-rule form were left-aligned, inconsistent with the right-aligned per-field icon
    // buttons above them -- wrapped in a shared right-aligned container (app.css
    // .right-aligned-actions) for both the "Add a sheet rule" card and edit mode.
    [Fact]
    public void SheetRuleForm_AddModeActionButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#add-sheet-rule-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });

    [Fact]
    public async Task SheetRuleForm_EditModeActionButtons_AreInRightAlignedContainer() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").ParentElement!.GetAttribute("class")
                .Should().Contain("right-aligned-actions");
            cut.Find("#cancel-sheet-rule-button-0").ParentElement!.GetAttribute("class")
                .Should().Contain("right-aligned-actions");
        });

    [Fact]
    public void BlockField_AfterAdding_DisplaysModifyAndDeleteButtons() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.FindAll("#modify-block-field-button-0").Should().HaveCount(1);
        cut.FindAll("#delete-block-field-button-0").Should().HaveCount(1);
    });

    [Fact]
    public void BlockFieldForm_AbsoluteRangeInput_HasVisibleLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='block-field-absolute-range-input']").TextContent.Should().Be("Excel range of the 1st block");
    });

    // Ticket example (N3): editing TypeElement (FirstBlockStartRow=19, RowOffsetStart=3/End=4) must
    // prefill the text field with "B22:E23", not the raw offsets.
    [Fact]
    public void BlockField_ClickingModify_PrefillsEditFormWithAbsoluteExcelRange() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
        cut.Find("#block-field-name-input").Change("TypeElement");
        cut.Find("#block-field-absolute-range-input").Change("B22:E23");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();

        cut.Find("#block-field-0-name-input").GetAttribute("value").Should().Be("TypeElement");
        cut.Find("#block-field-0-absolute-range-input").GetAttribute("value").Should().Be("B22:E23");
    });

    [Fact]
    public void BlockField_SaveChanges_UpdatesFieldInPlace_AndClosesEditMode() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();
        cut.Find("#block-field-0-absolute-range-input").Change("C9:F9");
        cut.Find("#save-block-field-button-0").Click();

        cut.FindAll("#block-field-0-name-input").Should().BeEmpty();
        cut.Find(".block-field-name").TextContent.Should().Be("Identification");
        cut.Find(".block-field-range").TextContent.Should().Be("C9:F9");
        cut.FindAll("#modify-block-field-button-1").Should().BeEmpty();
    });

    [Fact]
    public void BlockField_Cancel_DiscardsChanges() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();
        cut.Find("#block-field-0-absolute-range-input").Change("C9:F9");
        cut.Find("#cancel-block-field-button-0").Click();

        cut.Find(".block-field-name").TextContent.Should().Be("Identification");
        cut.Find(".block-field-range").TextContent.Should().Be("B9:E9");
    });

    [Fact]
    public void BlockField_Delete_RemovesFieldFromList() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#block-field-name-input").Change("Designation");
        cut.Find("#block-field-absolute-range-input").Change("H9:U9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#delete-block-field-button-0").Click();

        cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "Designation");
        cut.FindAll(".block-field-range").Should().ContainSingle(e => e.TextContent == "H9:U9");
    });

    [Fact]
    public async Task BlockField_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-modify-block-field-button-0").Click();
            cut.Find("#edit-0-block-field-0-absolute-range-input").Change("C9:F9");
            cut.Find("#edit-0-save-block-field-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var rule = all.Single().SheetRules.Single();
            rule.Locator.Fields.Should().ContainSingle(f => f.Name == "Identification" && f.ColumnRange == "C:F");
        });

    // Ticket example (N3): typing "B19:E20" against FirstBlockStartRow=19 must produce
    // RowOffsetStart=0/RowOffsetEnd=1 on the persisted BlockFieldDefinition.
    [Fact]
    public async Task AddBlockField_WithAbsoluteExcelRange_ComputesCorrectRowOffsets() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B19:E20");
            cut.Find("#add-block-field-button").Click();

            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var field = all.Single().SheetRules.Single().Locator.Fields.Single();
            field.ColumnRange.Should().Be("B:E");
            field.RowOffsetStart.Should().Be(0);
            field.RowOffsetEnd.Should().Be(1);
        });

    [Theory]
    [InlineData("abc")]
    [InlineData("E20:B19")]
    public void AddBlockField_WithInvalidAbsoluteRange_DisplaysErrorAndDoesNotCreateField(string invalidRange) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change(invalidRange);
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("Enter a valid Excel range");
            cut.FindAll("#modify-block-field-button-0").Should().BeEmpty();
            // Lot 040 (40.1): BlockFieldForm's own alert-danger block.
            cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        });

    [Fact]
    public void AddBlockField_WithRowBeyondRealExcelBounds_DisplaysBlockingErrorAndDoesNotCreateField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("1");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B2000000");
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("Enter a valid Excel range");
            cut.FindAll("#modify-block-field-button-0").Should().BeEmpty();
        });

    [Fact]
    public void AddBlockField_BeyondPracticalPlausibilityThreshold_DisplaysWarning_ButStillCreatesField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("1");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("BA1");
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("far beyond the columns/rows");
            cut.FindAll("#modify-block-field-button-0").Should().HaveCount(1);
        });

    // Ticket O1: field name and Excel range must be two distinct elements (not one concatenated
    // string), and the range must carry the monospace styling class -- checked by class, not by a
    // computed font value, per the project's "no selection by text or position" test convention.
    [Fact]
    public async Task BlockField_DisplaysNameAndRangeAsSeparateElements_WithMonospaceRangeClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            var names = cut.FindAll(".block-field-name");
            var ranges = cut.FindAll(".block-field-range");

            names.Should().Contain(e => e.TextContent == "Identification");
            names.Should().Contain(e => e.TextContent == "TypeElement");
            ranges.Should().Contain(e => e.TextContent == "B19:E20");
            ranges.Should().Contain(e => e.TextContent == "B22:E23");

            foreach (var range in ranges)
            {
                range.ClassList.Should().Contain("font-monospace");
            }
        });

    [Fact]
    public void BlockField_IconButtons_HaveAriaLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").GetAttribute("aria-label").Should().Be("Modify");
        cut.Find("#delete-block-field-button-0").GetAttribute("aria-label").Should().Be("Delete");
    });

    // X11 (Lot X): back link now lives in the shared top-row banner via SectionContent/
    // SectionOutlet -- see ImportProfileTestTests' identical comment/host for the rationale.
    private IRenderedComponent<SectionOutletTestHost> RenderWithBackNavHost(Guid? id = null)
        => Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ImportProfileEditor>(0);
                if (id.HasValue)
                {
                    b.AddComponentParameter(1, nameof(ImportProfileEditor.Id), id.Value);
                }

                b.CloseComponent();
            })));

    [Fact]
    public void BackToListButton_NavigatesToProfileList() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-import-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles");
    });

    [Fact]
    public void BackToListButton_IsStillShown_WhenProfileNotFound() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost(Guid.NewGuid());

        cut.FindAll("#back-to-import-profiles-button").Should().HaveCount(1);
    });

    [Fact]
    public void BackToListButton_LivesInsideTheSharedTopRow_AlongsideTheBrandLink() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();

        var topRow = cut.Find(".top-row");
        topRow.QuerySelector("#back-to-import-profiles-button").Should().NotBeNull();
        topRow.QuerySelector(".navbar-brand").Should().NotBeNull();
    });

    // Lot W: edit/delete of an already-added UnconditionalColonneName.

    [Fact]
    public void UnconditionalColonne_ClickingModify_ShowsPrefilledEditInput_AndRemovesStaticText() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();

            cut.Find("#unconditional-colonne-edit-input-0").GetAttribute("value").Should().Be("PROLOCK VANNES");
            cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_SaveChanges_UpdatesValueInPlace_AndClosesEditMode() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#save-unconditional-colonne-button-0").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "DEPROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_SaveWithEmptyValue_ShowsError_AndKeepsEditModeOpen() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("   ");
            cut.Find("#save-unconditional-colonne-button-0").Click();

            cut.Markup.Should().Contain("Colonne name must not be empty.");
            cut.FindAll("#unconditional-colonne-edit-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void UnconditionalColonne_Cancel_DiscardsChanges_RestoresOriginalValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#cancel-unconditional-colonne-edit-button-0").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_EditingOneItem_DoesNotAffectOtherItemInSameList() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-1").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.Find("#unconditional-colonne-edit-input-1").GetAttribute("value").Should().Be("DEPROLOCK VANNES");
            cut.Find(".block-field-name").TextContent.Should().Be("PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#delete-unconditional-colonne-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "DEPROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_DeletingLastRemainingItem_LeavesEmptyListWithNoError() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#delete-unconditional-colonne-button-0").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
        });

    [Fact]
    public async Task UnconditionalColonne_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-edit-unconditional-colonne-button-0").Click();
            cut.Find("#edit-0-unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#edit-0-save-unconditional-colonne-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().UnconditionalColonneNames.Should().ContainSingle("DEPROLOCK VANNES");
        });

    [Fact]
    public async Task UnconditionalColonne_DeleteWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-delete-unconditional-colonne-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().UnconditionalColonneNames.Should().BeEmpty();
        });

    // Lot 035 (35.8): same edit/delete-in-place pattern as Lot W's UnconditionalColonneNames,
    // applied to DefaultTableaux (Lot U1's add-only list).

    [Fact]
    public void DefaultTableau_ClickingModify_ShowsPrefilledEditInput_AndRemovesStaticText() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();

            cut.Find("#default-tableau-edit-input-0").GetAttribute("value").Should().Be("TRAVAUX COMPLET");
            cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "TRAVAUX COMPLET");
        });

    [Fact]
    public void DefaultTableau_SaveChanges_UpdatesValueInPlace_AndClosesEditMode() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("TRAVAUX DETAIL");
            cut.Find("#save-default-tableau-button-0").Click();

            cut.FindAll("#default-tableau-edit-input-0").Should().BeEmpty();
            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "TRAVAUX DETAIL");
        });

    [Fact]
    public void DefaultTableau_SaveWithEmptyValue_ShowsError_AndKeepsEditModeOpen() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("   ");
            cut.Find("#save-default-tableau-button-0").Click();

            cut.Markup.Should().Contain("Tableau name must not be empty.");
            cut.FindAll("#default-tableau-edit-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void DefaultTableau_Cancel_DiscardsChanges_RestoresOriginalValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("TRAVAUX DETAIL");
            cut.Find("#cancel-default-tableau-edit-button-0").Click();

            cut.FindAll("#default-tableau-edit-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("TRAVAUX COMPLET");
        });

    [Fact]
    public void DefaultTableau_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();
            cut.Find("#default-tableau-name-input").Change("TRAVAUX DETAIL");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#delete-default-tableau-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "TRAVAUX DETAIL");
        });

    [Fact]
    public async Task DefaultTableau_AddThenEditThenSave_PersistsAfterSavingProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("Profil de test 35.8 tableaux");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("TRAVAUX DETAIL");
            cut.Find("#save-default-tableau-button-0").Click();

            AddValidSheetRule(cut);
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().DefaultTableaux.Should().ContainSingle("TRAVAUX DETAIL");
        });

    [Fact]
    public async Task DefaultTableau_AddThenDeleteThenSave_PersistsAfterSavingProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("Profil de test 35.8 tableaux suppression");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#delete-default-tableau-button-0").Click();

            AddValidSheetRule(cut);
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().DefaultTableaux.Should().BeEmpty();
        });

    // Lot 035 (35.8): same treatment for DefaultApplicationNames.

    [Fact]
    public void DefaultApplicationName_ClickingModify_ShowsPrefilledEditInput_AndRemovesStaticText() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();

            cut.Find("#default-application-name-edit-input-0").GetAttribute("value").Should().Be("PROGRESS");
            cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "PROGRESS");
        });

    [Fact]
    public void DefaultApplicationName_SaveChanges_UpdatesValueInPlace_AndClosesEditMode() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();
            cut.Find("#default-application-name-edit-input-0").Change("AUTRE");
            cut.Find("#save-default-application-name-button-0").Click();

            cut.FindAll("#default-application-name-edit-input-0").Should().BeEmpty();
            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "AUTRE");
        });

    [Fact]
    public void DefaultApplicationName_SaveWithEmptyValue_ShowsError_AndKeepsEditModeOpen() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();
            cut.Find("#default-application-name-edit-input-0").Change("   ");
            cut.Find("#save-default-application-name-button-0").Click();

            cut.Markup.Should().Contain("Application name must not be empty.");
            cut.FindAll("#default-application-name-edit-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void DefaultApplicationName_Cancel_DiscardsChanges_RestoresOriginalValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();
            cut.Find("#default-application-name-edit-input-0").Change("AUTRE");
            cut.Find("#cancel-default-application-name-edit-button-0").Click();

            cut.FindAll("#default-application-name-edit-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("PROGRESS");
        });

    [Fact]
    public void DefaultApplicationName_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();
            cut.Find("#default-application-name-input").Change("AUTRE");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#delete-default-application-name-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "AUTRE");
        });

    [Fact]
    public async Task DefaultApplicationName_AddThenEditThenSave_PersistsAfterSavingProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("Profil de test 35.8 applications");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();
            cut.Find("#default-application-name-edit-input-0").Change("AUTRE");
            cut.Find("#save-default-application-name-button-0").Click();

            AddValidSheetRule(cut);
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().DefaultApplicationNames.Should().ContainSingle("AUTRE");
        });

    [Fact]
    public async Task DefaultApplicationName_AddThenDeleteThenSave_PersistsAfterSavingProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("Profil de test 35.8 applications suppression");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#delete-default-application-name-button-0").Click();

            AddValidSheetRule(cut);
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().DefaultApplicationNames.Should().BeEmpty();
        });

    // Lot 059 (59.2): AddDefaultTableau/AddDefaultApplicationName and the in-line Save actions now
    // route through ImportProfile's own public validator (59.1) instead of a silent blank-check --
    // the same validation path used by the Domain constructor.

    [Fact]
    public void DefaultTableau_AddDuplicateName_ShowsAlert_KeepsSingleItem_AndPreservesTypedInput() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("zzz");
            cut.Find("#add-default-tableau-button").Click();
            cut.Find("#default-tableau-name-input").Change("zzz");
            cut.Find("#add-default-tableau-button").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle();
            cut.Find("[role='alert']").TextContent.Should().Contain("zzz");
            cut.Find("#default-tableau-name-input").GetAttribute("value").Should().Be("zzz");
        });

    [Fact]
    public void DefaultTableau_AddCaseInsensitiveDuplicateName_ShowsAlert_KeepsSingleItem() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("zzz");
            cut.Find("#add-default-tableau-button").Click();
            cut.Find("#default-tableau-name-input").Change("ZZZ");
            cut.Find("#add-default-tableau-button").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle();
            cut.Find("[role='alert']").Should().NotBeNull();
        });

    [Fact]
    public void DefaultTableau_AddNameOf51Characters_ShowsAlert_AddsNothing() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change(new string('A', 51));
            cut.Find("#add-default-tableau-button").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
            cut.Find("[role='alert']").Should().NotBeNull();
        });

    [Fact]
    public void DefaultTableau_AddBlankName_ShowsAlert_InsteadOfSilentlyDoingNothing() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("   ");
            cut.Find("#add-default-tableau-button").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
            cut.Find("[role='alert']").Should().NotBeNull();
        });

    [Fact]
    public void DefaultTableau_AddNameWithSurroundingWhitespace_StoresTrimmedValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("  zzz  ");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find(".block-field-name").TextContent.Should().Be("zzz");
        });

    [Fact]
    public void DefaultTableau_RenameItemToItsOwnValue_IsAccepted_NoAlert() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("zzz");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("zzz");
            cut.Find("#save-default-tableau-button-0").Click();

            cut.FindAll("#default-tableau-edit-input-0").Should().BeEmpty();
            cut.FindAll("[role='alert']").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("zzz");
        });

    [Fact]
    public void DefaultTableau_RenameItemToAnotherItemsName_ShowsAlert_KeepsEditModeOpen_DoesNotOverwriteOriginal() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("zzz");
            cut.Find("#add-default-tableau-button").Click();
            cut.Find("#default-tableau-name-input").Change("yyy");
            cut.Find("#add-default-tableau-button").Click();

            cut.Find("#edit-default-tableau-button-1").Click();
            cut.Find("#default-tableau-edit-input-1").Change("zzz");
            cut.Find("#save-default-tableau-button-1").Click();

            cut.FindAll("#default-tableau-edit-input-1").Should().HaveCount(1);
            cut.Find("[role='alert']").Should().NotBeNull();
            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "zzz");
        });

    [Fact]
    public void DefaultTableau_AddRejectedOnFreshProfile_UnsavedChangesIndicatorStaysAbsent() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-tableau-name-input").Change("");
            cut.Find("#add-default-tableau-button").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    [Fact]
    public void DefaultApplicationName_AddDuplicateName_ShowsAlert_KeepsSingleItem_AndPreservesTypedInput() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();
            cut.Find("#default-application-name-input").Change("progress");
            cut.Find("#add-default-application-name-button").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle();
            cut.Find("[role='alert']").Should().NotBeNull();
            cut.Find("#default-application-name-input").GetAttribute("value").Should().Be("progress");
        });

    [Fact]
    public void DefaultApplicationName_RenameItemToItsOwnValue_IsAccepted_NoAlert() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#default-application-name-input").Change("PROGRESS");
            cut.Find("#add-default-application-name-button").Click();

            cut.Find("#edit-default-application-name-button-0").Click();
            cut.Find("#default-application-name-edit-input-0").Change("PROGRESS");
            cut.Find("#save-default-application-name-button-0").Click();

            cut.FindAll("#default-application-name-edit-input-0").Should().BeEmpty();
            cut.FindAll("[role='alert']").Should().BeEmpty();
        });

    // Lot W: edit/delete of an already-added ConditionalPointRule.

    private static void AddPointRule(IRenderedComponent<ImportProfileEditor> cut, string colonneName, string sourceFieldName,
        string operatorValue, string comparisonValue)
    {
        cut.Find("#point-rule-colonne-name-input").Change(colonneName);
        cut.Find("#point-rule-source-field-name-input").Change(sourceFieldName);
        cut.Find("#point-rule-operator-select").Change(operatorValue);
        cut.Find("#point-rule-comparison-value-input").Change(comparisonValue);
        cut.Find("#add-point-rule-button").Click();
    }

    [Fact]
    public void ConditionalPointRule_ClickingModify_ShowsPrefilledEditFields_IncludingOperatorSelect() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "NotEquals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();

            cut.Find("#conditional-point-rule-edit-colonne-name-input-0").GetAttribute("value").Should().Be("ZERO ENERGIE");
            cut.Find("#conditional-point-rule-edit-source-field-input-0").GetAttribute("value").Should().Be("TypeElement");
            cut.Find("#conditional-point-rule-edit-operator-select-0").GetAttribute("value").Should().Be("NotEquals");
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").GetAttribute("value").Should().Be("TUBING");
        });

    [Fact]
    public void ConditionalPointRule_SaveChanges_WithOnlyOneFieldModified_UpdatesOnlyThatField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#save-conditional-point-rule-button-0").Click();

            cut.FindAll("#conditional-point-rule-edit-colonne-name-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("ZERO ENERGIE");
            cut.Find(".block-field-range").TextContent.Should().Be("TypeElement Equals TUYAUTERIE");
        });

    [Theory]
    [InlineData("", "TypeElement", "TUBING")]
    [InlineData("ZERO ENERGIE", "", "TUBING")]
    [InlineData("ZERO ENERGIE", "TypeElement", "")]
    public void ConditionalPointRule_SaveWithAnyFieldEmpty_ShowsError_AndKeepsEditModeOpen(
        string colonneName, string sourceFieldName, string comparisonValue) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-colonne-name-input-0").Change(colonneName);
            cut.Find("#conditional-point-rule-edit-source-field-input-0").Change(sourceFieldName);
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change(comparisonValue);
            cut.Find("#save-conditional-point-rule-button-0").Click();

            cut.FindAll(".alert-danger").Should().HaveCount(1);
            cut.FindAll("#conditional-point-rule-edit-colonne-name-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void ConditionalPointRule_Cancel_DiscardsChanges_RestoresOriginalValues() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#cancel-conditional-point-rule-edit-button-0").Click();

            cut.FindAll("#conditional-point-rule-edit-comparison-value-input-0").Should().BeEmpty();
            cut.Find(".block-field-range").TextContent.Should().Be("TypeElement Equals TUBING");
        });

    [Fact]
    public void ConditionalPointRule_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");
            AddPointRule(cut, "SOUPAPE", "TypeElement", "Equals", "SOUPAPE");

            cut.Find("#delete-conditional-point-rule-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "SOUPAPE");
        });

    [Fact]
    public void ConditionalPointRule_DeletingLastRemainingItem_LeavesEmptyListWithNoError() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#delete-conditional-point-rule-button-0").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
        });

    [Fact]
    public async Task ConditionalPointRule_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "DIVERS", firstBlockStartRow: 9, step: 3, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "DIVERS", locator, pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "TUBING", "ZERO ENERGIE")],
                unconditionalColonneNames: [], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-edit-conditional-point-rule-button-0").Click();
            cut.Find("#edit-0-conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#edit-0-save-conditional-point-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().PointRules.Single().ComparisonValue.Should().Be("TUYAUTERIE");
        });

    [Fact]
    public async Task ConditionalPointRule_DeleteWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "DIVERS", firstBlockStartRow: 9, step: 3, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "DIVERS", locator, pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "TUBING", "ZERO ENERGIE")],
                unconditionalColonneNames: [], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-delete-conditional-point-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().PointRules.Should().BeEmpty();
        });

    [Fact]
    public void ConditionalPointRule_IconButtons_HaveAriaLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

        cut.Find("#edit-conditional-point-rule-button-0").GetAttribute("aria-label").Should().Be("Modify");
        cut.Find("#delete-conditional-point-rule-button-0").GetAttribute("aria-label").Should().Be("Delete");
    });

    // Lot 041 (41.2): save-profile-button is one of the CTA/Enregistrer buttons the audit found
    // without an icon (convention-ui-blazor-icones-boutons.md's matrix already required one --
    // the implementation was lagging, not the rule).
    [Fact]
    public void SaveProfileButton_HasIcon() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#save-profile-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
    });

    // Lot 041 (41.3): SheetRuleForm's own Submit button doubles as "Add" (top-level, always-present
    // card) and "Save changes" (edit mode, ShowCancel=true) -- only listed in scope as one of the "6
    // nested sub-forms' Enregistrer buttons" (41.0), never as an "Ajouter" button, so the icon is
    // conditional on ShowCancel, not unconditional.
    // Lot 053 (53.4): both now carry an icon -- Add gets Plus (btn-secondary), Save changes keeps
    // its pre-existing Check (btn-outline-secondary). Corrected in place, not doubled: the "add
    // button has no icon" half of the old assertion is exactly what 53.4 changes.
    [Fact]
    public async Task SheetRuleForm_AddButtonHasPlusIcon_SaveChangesButtonHasCheckIcon() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            OpenAddSheetRuleFormIfClosed(cut);

            cut.Find("#add-sheet-rule-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            cut.Find("#add-sheet-rule-button").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");

            cut.Find("#modify-sheet-rule-button-0").Click();

            // Lot 056 (56.7): the icon-conditional assertion still holds (Check in edit mode, Lot
            // 041, unchanged), but the class is now always solid btn-secondary, in both modes --
            // previously btn-outline-secondary here, indistinguishable from "Cancel" right next to
            // it. Fixed in place, not duplicated (same instruction as Lots 51.2/53.2).
            cut.Find("#save-sheet-rule-button-0").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            cut.Find("#save-sheet-rule-button-0").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
        });

    // Lot 041 (41.3) / Lot 053 (53.4): BlockFieldForm is one of the "6 nested sub-forms" -- same
    // Add-gets-Plus/Save-changes-keeps-Check icon rule as SheetRuleForm above.
    [Fact]
    public async Task BlockFieldForm_AddButtonHasPlusIcon_SaveChangesButtonHasCheckIcon() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-add-block-field-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            cut.Find("#edit-0-add-block-field-button").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");

            cut.Find("#edit-0-modify-block-field-button-0").Click();

            // Lot 056 (56.7): fixed in place -- class is now solid btn-secondary in both modes.
            cut.Find("#edit-0-save-block-field-button-0").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            cut.Find("#edit-0-save-block-field-button-0").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
        });

    // Lot 041 (41.3): confirms the previously-missing `title` on the block-field Modify/Delete
    // buttons, matching the pre-existing `aria-label` in content, per the convention's requirement
    // that an icon-only button carry both.
    [Fact]
    public async Task BlockField_ModifyDeleteButtons_HaveTitleMatchingAriaLabel() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            var modifyButton = cut.Find("#edit-0-modify-block-field-button-0");
            modifyButton.GetAttribute("title").Should().Be(modifyButton.GetAttribute("aria-label"));
            modifyButton.GetAttribute("title").Should().NotBeNullOrEmpty();

            var deleteButton = cut.Find("#edit-0-delete-block-field-button-0");
            deleteButton.GetAttribute("title").Should().Be(deleteButton.GetAttribute("aria-label"));
            deleteButton.GetAttribute("title").Should().NotBeNullOrEmpty();
        });

    // Lot 042 (42.3): closes the container-fluid/px-3 divergence with ExportProfileEditor.razor
    // (X6) -- 42.0's investigation found no functional reason for it, just a scope-timing gap
    // (X6 only ever targeted the export screenshot).
    [Fact]
    public void RootContainer_HasContainerFluidWithPadding() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        var container = cut.Find(".container-fluid.px-3");
        container.Should().NotBeNull();
    });

    // Lot 042 (42.2): the page previously skipped from h1 straight to h3 (section headings) and
    // from a card's h4 title down to h5 sub-headings with no h4 in between once flattened -- fixed
    // by shifting every level down one notch (h3->h2, h4->h3, h5->h4), keeping each heading's
    // pre-existing visual size via a Bootstrap `.hN` utility class.
    [Fact]
    public async Task ExistingProfileWithSheetRule_ExpandedDetails_HasNoHeadingLevelSkip() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
        });

    // Lot 043 (43.0): confirms bUnit's FakeNavigationManager (a plain NavigationManager subclass)
    // triggers registered location-changing handlers on NavigateTo -- that pipeline lives in the
    // base NavigationManager class since .NET 8, not per-hosting-model, so <NavigationLock>'s
    // OnBeforeInternalNavigation fires end-to-end under bUnit exactly like in a real browser.
    // This is the feasibility conclusion the ticket requires before writing 43.1's own tests.
    [Fact]
    public void NavigationLock_InterceptsInternalNavigation_WhenUnsavedChangesArePresent() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#profile-name-input").Change("MAD OXO");

        navigationManager.NavigateTo("export-profiles");

        navigationManager.Uri.Should().NotEndWith("/export-profiles");
        cut.Find("#unsaved-changes-navigation-confirmation").Should().NotBeNull();
    });

    // Lot 043 (43.1)
    [Fact]
    public async Task NavigationLock_ConfirmExternalNavigation_IsFalse_OnInitialLoad_ForNewAndExistingProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var newProfileCut = Render<ImportProfileEditor>();
            newProfileCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
            // Lot 043 (43.3): the badge is absent, not merely hidden, when nothing is dirty yet.
            newProfileCut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();

            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);
            var editCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            editCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
            editCut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    [Fact]
    public void NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterRootFieldChange() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
        cut.Find("#unsaved-changes-indicator").Should().NotBeNull();
    });

    [Fact]
    public void NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterAddingASheetRule() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        AddValidSheetRule(cut);

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
    });

    [Fact]
    public async Task NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterModifyingAnExistingSheetRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#save-sheet-rule-button-0").Click();

            cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
        });

    [Fact]
    public async Task NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterDeletingAnExistingSheetRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();

            cut.Find("#delete-sheet-rule-button-0").Click();
            cut.Find("#confirm-delete-sheet-rule-button-0").Click();

            cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
        });

    [Fact]
    public void NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterAddingADefaultTableau() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
        cut.Find("#add-default-tableau-button").Click();

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
    });

    [Fact]
    public void NavigationLock_ConfirmExternalNavigation_BecomesTrue_AfterAddingADefaultApplicationName() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#default-application-name-input").Change("PROGRESS");
        cut.Find("#add-default-application-name-button").Click();

        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
    });

    [Fact]
    public async Task HasUnsavedChanges_ResetsToFalse_AfterSuccessfulSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);
            cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();

            cut.Find("#save-profile-button").Click();

            // Navigated away on success -- re-render a fresh instance for the same (now-persisted)
            // profile to confirm the flag doesn't linger; the pre-save instance is gone.
            var all = await Store.GetAllAsync();
            var saved = all.Single();
            var reopened = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, saved.Id));
            reopened.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
        });

    [Fact]
    public void HasUnsavedChanges_StaysTrue_WhenSaveFails() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        // No EquipementTypeElementNom / no sheet rules -> SaveAsync throws DomainValidationException,
        // caught inside SaveProfileAsync -- reuses the existing failure path already tested above
        // (Save_WithEmptyEquipementTypeElementNom_DisplaysLocalizedError) rather than a new mocked failure.

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Equipement type element nom must not be empty.");
        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
    });

    [Fact]
    public void DiscardChangesAndLeaveButton_NavigatesToTheTargetLocation() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        navigationManager.NavigateTo("export-profiles");
        cut.Find("#unsaved-changes-navigation-confirmation").Should().NotBeNull();

        cut.Find("#discard-changes-and-leave-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles");
        cut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
    });

    [Fact]
    public void StayOnPageButton_DoesNotNavigate_AndClosesTheConfirmation() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var originalUri = navigationManager.Uri;

        cut.Find("#profile-name-input").Change("MAD OXO");
        navigationManager.NavigateTo("export-profiles");
        cut.Find("#unsaved-changes-navigation-confirmation").Should().NotBeNull();

        cut.Find("#stay-on-page-button").Click();

        navigationManager.Uri.Should().Be(originalUri);
        cut.FindAll("#unsaved-changes-navigation-confirmation").Should().BeEmpty();
        // Profile is still dirty and the field value survived -- confirmation only closed, no reset.
        cut.Find("#profile-name-input").GetAttribute("value").Should().Be("MAD OXO");
    });

    [Fact]
    public void NavigationLock_DoesNotInterceptNavigation_WhenNoUnsavedChanges() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo("export-profiles");

        navigationManager.Uri.Should().EndWith("/export-profiles");
        cut.FindAll("#unsaved-changes-navigation-confirmation").Should().BeEmpty();
    });

    // ---------------------------------------------------------------------------------------------
    // Lot 048: profile-driven header rules (HeaderFieldRule / HeaderCompositeRule), editable from
    // SheetRuleForm. Helper to fill in a minimal valid sheet rule (name/locator/one block field),
    // stopping just before "add sheet rule" -- mirrors AddValidSheetRule but for a PROCEDURE-shaped
    // rule (needed by the header-field tests, since header rules are only meaningful there).
    // ---------------------------------------------------------------------------------------------
    private static void FillMinimalProcedureSheetRule(IRenderedComponent<ImportProfileEditor> cut, string idPrefix = "")
    {
        cut.Find($"#{idPrefix}sheet-rule-name-input").Change("PROCEDURE");
        cut.Find($"#{idPrefix}sheet-rule-first-block-start-row-input").Change("9");
        cut.Find($"#{idPrefix}sheet-rule-step-input").Change("1");
        cut.Find($"#{idPrefix}sheet-rule-stop-field-name-input").Change("Action");
        cut.Find($"#{idPrefix}block-field-name-input").Change("Action");
        cut.Find($"#{idPrefix}block-field-absolute-range-input").Change("C9:L9");
        cut.Find($"#{idPrefix}add-block-field-button").Click();
    }

    // 48.1 -- SheetRuleForm.Submit() used to pass [], [] for HeaderFields/HeaderComposites regardless
    // of InitialRule, silently wiping a sheet rule's header rules on any edit-and-resubmit. Red first:
    // reverting the [.. _headerFields]/[.. _headerComposites] snapshots in SheetRuleForm.Submit back
    // to literal [], [] makes these two tests fail.
    [Fact]
    public async Task EditingSheetRule_WithoutChanges_PreservesHeaderFieldsAndComposites() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithHeaderRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            var rule = reloaded.SheetRules.Single();
            rule.HeaderFields.Should().BeEquivalentTo(profile.SheetRules[0].HeaderFields);
            rule.HeaderComposites.Should().BeEquivalentTo(profile.SheetRules[0].HeaderComposites);
        });

    [Fact]
    public async Task EditingSheetRule_ChangingUnrelatedField_PreservesHeaderRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithHeaderRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("2");
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            var rule = reloaded.SheetRules.Single();
            rule.Locator.Step.Should().Be(2);
            rule.HeaderFields.Should().HaveCount(3);
            rule.HeaderComposites.Should().HaveCount(1);
        });

    [Fact]
    public async Task EditingSheetRuleWithoutHeaderRules_RemainsSubmittedWithEmptyHeaderLists() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            var rule = reloaded.SheetRules.Single();
            rule.HeaderFields.Should().BeEmpty();
            rule.HeaderComposites.Should().BeEmpty();
        });

    // 48.2 -- HeaderFieldRuleForm, reached via the always-present "add a sheet rule" card's own
    // HeaderFieldRuleForm (unprefixed ids, since the outer SheetRuleForm has no IdPrefix there).
    [Fact]
    public async Task AddHeaderField_WithValidInput_PersistsDefaultsAndNoDateFormat() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            FillMinimalProcedureSheetRule(cut);

            cut.Find("#header-field-header-field-name-input").Change("nomMAD");
            cut.Find("#header-field-header-field-range-input").Change("M2:O2");
            cut.Find("#add-header-field-button").Click();

            cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "nomMAD");

            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var field = (await Store.GetAllAsync()).Single().SheetRules.Single().HeaderFields.Single();
            field.Name.Should().Be("nomMAD");
            field.StripReperePrefix.Should().BeFalse();
            field.DateFormat.Should().BeNull();
        });

    [Fact]
    public async Task AddHeaderField_CheckedStripPrefixAndDateFormat_PersistsBothFlags() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            FillMinimalProcedureSheetRule(cut);

            cut.Find("#header-field-header-field-name-input").Change("nomMAD");
            cut.Find("#header-field-header-field-range-input").Change("M2:O2");
            cut.Find("#header-field-header-field-date-format-input").Change("dd/MM/yyyy");
            cut.Find("#header-field-header-field-strip-prefix-checkbox").Change(true);
            cut.Find("#add-header-field-button").Click();

            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var field = (await Store.GetAllAsync()).Single().SheetRules.Single().HeaderFields.Single();
            field.StripReperePrefix.Should().BeTrue();
            field.DateFormat.Should().Be("dd/MM/yyyy");
        });

    [Fact]
    public async Task AddHeaderField_WithDateFormatLeftBlank_PersistsNullNotEmptyString() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            FillMinimalProcedureSheetRule(cut);

            cut.Find("#header-field-header-field-name-input").Change("nomMAD");
            cut.Find("#header-field-header-field-range-input").Change("M2:O2");
            cut.Find("#add-header-field-button").Click();

            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var field = (await Store.GetAllAsync()).Single().SheetRules.Single().HeaderFields.Single();
            field.DateFormat.Should().BeNull();
        });

    [Fact]
    public void AddHeaderField_WithEmptyName_ShowsLocalizedAlertAndDoesNotAdd() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#add-header-field-button").Click();

        cut.Markup.Should().Contain("Name must not be empty.");
        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        cut.FindAll(".block-field-name").Should().BeEmpty();
    });

    [Theory]
    [InlineData("m2:o2")]
    [InlineData("ZZZZ1")]
    [InlineData("foo")]
    public void AddHeaderField_WithInvalidRange_ShowsLocalizedAlertAndDoesNotAdd(string invalidRange) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#header-field-header-field-name-input").Change("nomMAD");
            cut.Find("#header-field-header-field-range-input").Change(invalidRange);
            cut.Find("#add-header-field-button").Click();

            cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
            cut.FindAll(".block-field-name").Should().BeEmpty();
        });

    [Fact]
    public void ModifyHeaderField_PrefillsAllFields_IncludingCheckboxAndDateFormat() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-field-header-field-name-input").Change("nomMAD");
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#header-field-header-field-date-format-input").Change("dd/MM/yyyy");
        cut.Find("#header-field-header-field-strip-prefix-checkbox").Change(true);
        cut.Find("#add-header-field-button").Click();

        cut.Find("#modify-header-field-button-0").Click();

        cut.Find("#header-field-0-header-field-name-input").GetAttribute("value").Should().Be("nomMAD");
        cut.Find("#header-field-0-header-field-range-input").GetAttribute("value").Should().Be("M2:O2");
        cut.Find("#header-field-0-header-field-date-format-input").GetAttribute("value").Should().Be("dd/MM/yyyy");
    });

    [Fact]
    public void ModifyHeaderField_SubmitReturnsUpdatedVersion() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-field-header-field-name-input").Change("nomMAD");
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#add-header-field-button").Click();

        cut.Find("#modify-header-field-button-0").Click();
        cut.Find("#header-field-0-header-field-range-input").Change("M2:O3");
        cut.Find("#save-header-field-button-0").Click();

        cut.FindAll(".block-field-range").Select(e => e.TextContent).Should().Contain("M2:O3");
    });

    // 48.3 -- HeaderCompositeRuleForm.
    [Fact]
    public void AddHeaderComposite_WithValidInput_RendersSummaryLine() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#header-composite-header-composite-template-input").Change("Rév {revision} du {dateRev}");
        cut.Find("#add-header-composite-button").Click();

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Designation");
        cut.FindAll(".block-field-range").Should().Contain(e => e.TextContent == "Rév {revision} du {dateRev}");
    });

    [Fact]
    public void AddHeaderComposite_WithEmptyName_ShowsLocalizedAlertAndDoesNotAdd() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-template-input").Change("Rév {revision}");
        cut.Find("#add-header-composite-button").Click();

        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        cut.FindAll(".block-field-name").Should().BeEmpty();
    });

    [Fact]
    public void AddHeaderComposite_WithEmptyTemplate_ShowsLocalizedAlertAndDoesNotAdd() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#add-header-composite-button").Click();

        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        cut.FindAll(".block-field-name").Should().BeEmpty();
    });

    // Ticket 48.3's own explicit instruction: no "unknown placeholder" test at this component level --
    // the domain allows a literal template with no placeholder at all, and PlaceholderNames() returns
    // an empty list either way, so cross-validation never fires here regardless of content.
    [Fact]
    public void AddHeaderComposite_WithoutAnyPlaceholder_IsAccepted() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#header-composite-header-composite-template-input").Change("Literal text only");
        cut.Find("#add-header-composite-button").Click();

        cut.FindAll(".alert-danger").Should().BeEmpty();
        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Designation");
    });

    [Fact]
    public void ModifyHeaderComposite_PrefillsFields_AndSubmitReturnsUpdatedVersion() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#header-composite-header-composite-template-input").Change("Rév {revision} du {dateRev}");
        cut.Find("#add-header-composite-button").Click();

        cut.Find("#modify-header-composite-button-0").Click();

        cut.Find("#header-composite-0-header-composite-name-input").GetAttribute("value").Should().Be("Designation");
        cut.Find("#header-composite-0-header-composite-template-input").GetAttribute("value").Should().Be("Rév {revision} du {dateRev}");

        cut.Find("#header-composite-0-header-composite-template-input").Change("Rév {revision} du {dateRev} edited");
        cut.Find("#save-header-composite-button-0").Click();

        cut.FindAll(".block-field-range").Select(e => e.TextContent).Should().Contain("Rév {revision} du {dateRev} edited");
    });

    // 48.4 -- integration in SheetRuleForm: delete without confirmation, sheet-rename propagation,
    // unknown-placeholder cross-validation surfaced at submission, Lot 043's unsaved-changes flag.
    [Fact]
    public void DeleteHeaderField_RemovesImmediately_WithoutConfirmation() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-field-header-field-name-input").Change("nomMAD");
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#add-header-field-button").Click();

        cut.Find("#delete-header-field-button-0").Click();

        cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "nomMAD");
        cut.Markup.Should().NotContain("cannot be undone");
    });

    [Fact]
    public void DeleteHeaderComposite_RemovesImmediately_WithoutConfirmation() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#header-composite-header-composite-template-input").Change("Rév {revision}");
        cut.Find("#add-header-composite-button").Click();

        cut.Find("#delete-header-composite-button-0").Click();

        cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "Designation");
        cut.Markup.Should().NotContain("cannot be undone");
    });

    [Fact]
    public async Task RenamingSheet_AfterAddingHeaderField_UpdatesCellSheetOnSubmit() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            FillMinimalProcedureSheetRule(cut);

            cut.Find("#header-field-header-field-name-input").Change("nomMAD");
            cut.Find("#header-field-header-field-range-input").Change("M2:O2");
            cut.Find("#add-header-field-button").Click();

            cut.Find("#sheet-rule-name-input").Change("AUTRES JOINTS TOUCHES");
            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var rule = (await Store.GetAllAsync()).Single().SheetRules.Single();
            rule.SheetName.Should().Be("AUTRES JOINTS TOUCHES");
            rule.HeaderFields.Single().Cell.Sheet.Should().Be("AUTRES JOINTS TOUCHES");
        });

    [Fact]
    public void SubmittingSheetRule_WithCompositeReferencingUnknownPlaceholder_ShowsLocalizedAlert_AndDoesNotSubmit() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            FillMinimalProcedureSheetRule(cut);

            cut.Find("#header-composite-header-composite-name-input").Change("Designation");
            cut.Find("#header-composite-header-composite-template-input").Change("Rév {inconnu}");
            cut.Find("#add-header-composite-button").Click();

            cut.Find("#add-sheet-rule-button").Click();

            cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
            cut.Markup.Should().NotContain("SheetExtractionRule_HeaderCompositeReferencesUnknownField");
            cut.FindAll("li.sheet-rule-card").Should().BeEmpty();
        });

    // Lot 056 (56.3): this test's own original premise -- that adding a header field to the
    // always-rendered add-sheet-rule form shows no indicator until the whole sheet rule is
    // submitted -- is exactly the blind spot 56.3 closes (SheetRuleForm.OnDirty now fires from
    // every in-form mutation, including this one, not just the root editor's own 8 mutation
    // points). Fixed in place, not duplicated: the indicator is now expected already present right
    // after the header field is added, and stays present once the sheet rule itself is submitted.
    [Fact]
    public void AddingSheetRuleWithHeaderField_ThenSubmitting_ShowsUnsavedChangesIndicator() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        FillMinimalProcedureSheetRule(cut);

        cut.Find("#header-field-header-field-name-input").Change("nomMAD");
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#add-header-field-button").Click();

        cut.FindAll("#unsaved-changes-indicator").Should().HaveCount(1);

        cut.Find("#add-sheet-rule-button").Click();

        cut.FindAll("#unsaved-changes-indicator").Should().HaveCount(1);
    });

    // 48.5 -- non-blocking warning on well-known header names expected by the extraction services.
    [Fact]
    public void SheetRuleForm_ProcedureSheetWithoutHeaderRules_ShowsWarningListingAllFourNames() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#sheet-rule-name-input").Change("PROCEDURE");

            var warning = cut.Find("#header-well-known-names-warning");
            warning.GetAttribute("role").Should().Be("alert");
            warning.TextContent.Should().Contain("nomMAD");
            warning.TextContent.Should().Contain("revision");
            warning.TextContent.Should().Contain("dateRev");
            warning.TextContent.Should().Contain("Designation");
        });

    [Fact]
    public void SheetRuleForm_ProcedureSheetWithAllExpectedNames_ShowsNoWarning() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#sheet-rule-name-input").Change("PROCEDURE");

        cut.Find("#header-field-header-field-name-input").Change("nomMAD");
        cut.Find("#header-field-header-field-range-input").Change("M2:O2");
        cut.Find("#add-header-field-button").Click();
        cut.Find("#header-field-header-field-name-input").Change("revision");
        cut.Find("#header-field-header-field-range-input").Change("P2:Q2");
        cut.Find("#add-header-field-button").Click();
        cut.Find("#header-field-header-field-name-input").Change("dateRev");
        cut.Find("#header-field-header-field-range-input").Change("R2:T2");
        cut.Find("#add-header-field-button").Click();

        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        cut.Find("#header-composite-header-composite-template-input").Change("Rév {revision} du {dateRev}");
        cut.Find("#add-header-composite-button").Click();

        cut.FindAll("#header-well-known-names-warning").Should().BeEmpty();
    });

    // Ordinal/case-sensitive comparison, matching the real resolver's own Dictionary<string, ...>.
    [Fact]
    public void SheetRuleForm_ProcedureSheetWithWronglyCasedName_StillWarnsForTheCorrectCasing() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#sheet-rule-name-input").Change("PROCEDURE");

            cut.Find("#header-field-header-field-name-input").Change("nomMad");
            cut.Find("#header-field-header-field-range-input").Change("M2:O2");
            cut.Find("#add-header-field-button").Click();

            cut.Find("#header-well-known-names-warning").TextContent.Should().Contain("nomMAD");
        });

    [Fact]
    public void SheetRuleForm_IsolementSheet_ShowsNoWarning() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");

        cut.FindAll("#header-well-known-names-warning").Should().BeEmpty();
    });

    [Fact]
    public void SheetRuleForm_SubmittingDespiteWarning_StillInvokesOnSubmit() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        FillMinimalProcedureSheetRule(cut);

        cut.Find("#add-sheet-rule-button").Click();

        cut.FindAll("li.sheet-rule-card").Should().HaveCount(1);
    });

    // 48.6 -- read-only visibility of header rules in the sheet-rule card's <details>.
    [Fact]
    public async Task Details_WithHeaderRules_ShowsNamesRangesAndTemplateWhenExpanded() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithHeaderRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            var content = cut.Find("#sheet-rule-details-content-0");
            content.TextContent.Should().Contain("nomMAD");
            content.TextContent.Should().Contain("M2:O2");
            content.TextContent.Should().Contain("Designation");
            content.TextContent.Should().Contain("Rév {revision} du {dateRev}");
        });

    [Fact]
    public async Task Details_SheetWithoutAnySublist_StillShowsNoItemsMessage() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithEmptySublistSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.Find("#sheet-rule-details-content-0").TextContent.Should()
                .Contain("No unconditional colonnes, conditional point rules, header fields, or header composites for this sheet.");
        });
}
