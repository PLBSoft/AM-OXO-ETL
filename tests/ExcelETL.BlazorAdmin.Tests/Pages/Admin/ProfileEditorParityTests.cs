using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot R: densification de l'affichage des profils (import + export). The ticket's own explicit
// requirement is a *dedicated* test comparing the two editors' generated CSS class strings
// directly, rather than each editor's own test file separately asserting "has a non-empty class"
// -- a guard-rail against the two screens silently drifting apart in the future. One test class,
// rendering both ImportProfileEditor and ExportProfileEditor side by side, covers R1/R2/R3.
public class ProfileEditorParityTests : BunitContext
{
    public ProfileEditorParityTests()
    {
        var dbContextFactory = new TestDbContextFactory("ProfileEditorParityTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore ImportStore => Services.GetRequiredService<IImportProfileStore>();

    private IExportProfileStore ExportStore => Services.GetRequiredService<IExportProfileStore>();

    private static ImportProfile BuildImportProfileWithTwoSheetRules()
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

        return new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [isolementRule, platinesRule]);
    }

    private static ExportProfile BuildExportProfileWithTwoSheetRules() =>
        new("Profil export OXO",
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")],
                    []),
                new SheetGenerationRule(
                    "Enfants",
                    PivotSource.Isolement,
                    [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
                    [],
                    [])
            ]);

    // R1: same number of sheet rules on both sides -- the container class string must match
    // character-for-character, not merely "both non-empty".
    [Fact]
    public async Task SheetRuleGrid_CssClass_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            var importListClass = importCut.Find("ul.sheet-rule-list").GetAttribute("class");
            var exportListClass = exportCut.Find("ul.sheet-rule-list").GetAttribute("class");

            importListClass.Should().Be(exportListClass);
            importListClass.Should().Contain("sheet-rule-grid");
        });

    // R2: the field/column grid inside a read-only sheet-rule card -- same class string on both
    // sides, reusing the same global CSS rather than each screen inventing its own.
    [Fact]
    public async Task BlockFieldGrid_CssClass_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));
            exportCut.Find("#sheet-rule-details-toggle-0").Click();

            var importFieldListClass = importCut.Find("li.sheet-rule-card ul.block-field-list").GetAttribute("class");
            var exportFieldListClass = exportCut.Find("li.sheet-rule-card ul.block-field-list").GetAttribute("class");

            importFieldListClass.Should().Be(exportFieldListClass);
            importFieldListClass.Should().Contain("block-field-grid");
        });

    // R3: both screens collapse their unbounded-size sub-list behind the same
    // .sheet-rule-sublist-details structure, closed by default, expanding on click.
    [Fact]
    public async Task SheetRuleSublistDetails_CollapsedByDefaultBehavior_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            // Both start collapsed: the same class on the wrapping <details>, and the full-list
            // content div is absent from the DOM on both sides.
            var importDetailsClass = importCut.Find("details.sheet-rule-sublist-details").GetAttribute("class");
            var exportDetailsClass = exportCut.Find("details.sheet-rule-sublist-details").GetAttribute("class");
            importDetailsClass.Should().Be(exportDetailsClass);

            importCut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
            exportCut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();

            // Clicking the summary reveals the content div on both sides.
            importCut.Find("#sheet-rule-details-toggle-0").Click();
            exportCut.Find("#sheet-rule-details-toggle-0").Click();

            importCut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            exportCut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
        });

    // Lot 030 (30.5): explicit structural-parity guard-rail for the X3/X4/X5/Y3 patterns Part A of
    // this lot extended from ExportProfileEditor to ImportProfileEditor -- string comparison, not
    // just "both non-empty", per the ticket's own requirement, mirroring R1-R3's precedent above.
    [Fact]
    public void RootFieldContainer_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importContainerClass = importCut.Find("#profile-name-input").ParentElement!.ParentElement!.GetAttribute("class");
        var exportContainerClass = exportCut.Find("#export-profile-name-input").ParentElement!.ParentElement!.GetAttribute("class");

        importContainerClass.Should().Be(exportContainerClass);
        importContainerClass.Should().Be("mb-3");
    });

    [Fact]
    public void SubformCardContainer_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importCardClass = importCut.Find("div.card.bg-light").GetAttribute("class");
        var exportCardClass = exportCut.Find("div.card.bg-light").GetAttribute("class");

        importCardClass.Should().Be(exportCardClass);
        importCardClass.Should().Be("card bg-light mb-3");
    });

    // Lot 053 (53.4/53.6): corrected in place -- the intermediate Add button is now solid
    // secondary + Plus icon, not outline, on both editors.
    [Fact]
    public void IntermediateAddButton_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importAddButton = importCut.Find("#add-block-field-button");
        var exportAddButton = exportCut.Find("#add-column-definition-button");

        importAddButton.GetAttribute("class").Should().Be(exportAddButton.GetAttribute("class"));
        importAddButton.GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
        importAddButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
        exportAddButton.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
    });

    // Lot 053 (53.1/53.6): the root container is comparable between the two editors for the first
    // time -- before this lot, import had no equivalent wrapper at all (§2.3 of the design audit).
    [Fact]
    public void RootContainer_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importContainer = importCut.Find(".profile-editor-container");
        var exportContainer = exportCut.Find(".profile-editor-container");

        importContainer.GetAttribute("class").Should().Be(exportContainer.GetAttribute("class"));
        importContainer.GetAttribute("class").Should().Be("container-fluid px-3 profile-editor-container");
    });

    // Lot 053 (53.2/53.6): the short-field 2-column grid has no export-side counterpart to compare
    // against -- 53.0's investigation found ExportProfileEditor.razor has exactly one root field
    // ("Name"), which plays the same "identitaire principal" role as import's "Nom du profil" and
    // stays col-12 on both sides (see RootFieldContainer_CssClass_IsIdenticalBetweenImportAndExportEditors
    // below, unchanged by this lot). There is nothing to grid on the export side, so nothing to pair
    // here -- documented rather than an artificial comparison invented for symmetry's own sake.
    [Fact]
    public void ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();

        importCut.Find("#profile-repere-prefix-input").ParentElement!.ParentElement!.GetAttribute("class")
            .Should().Be("col-12 col-md-6");

        // Export's single root field has no col-md-* class at all -- confirmed by the pre-existing
        // RootFieldContainer_CssClass_IsIdenticalBetweenImportAndExportEditors test below, which
        // still passes unmodified ("mb-3" on both sides).
    });

    [Fact]
    public void FinalSaveButton_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importSaveButtonClass = importCut.Find("#save-profile-button").GetAttribute("class");
        var exportSaveButtonClass = exportCut.Find("#save-export-profile-button").GetAttribute("class");

        importSaveButtonClass.Should().Be(exportSaveButtonClass);
        importSaveButtonClass.Should().Be("btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4 d-flex align-items-center justify-content-center gap-1");
    });

    // Lot 037: closes the one sheet-rule-card element R1-R3/30.5 never compared -- the Modify/
    // Delete buttons themselves. ExportProfileEditor.razor was already icon-only per
    // convention-ui-blazor-icones-boutons.md; ImportProfileEditor.razor's own version was still
    // plain text until this lot. Comparing the button's own class string plus its inner-markup
    // shape (icon present, no visible text) guards against this specific point silently
    // diverging a second time without being caught before a client-facing visual review.
    [Fact]
    public async Task SheetRuleCardModifyDeleteButtons_CssClassAndIconStructure_AreIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            var importModify = importCut.Find("#modify-sheet-rule-button-0");
            var exportModify = exportCut.Find("#modify-sheet-generation-rule-button-0");
            importModify.GetAttribute("class").Should().Be(exportModify.GetAttribute("class"));
            importModify.GetAttribute("class").Should().Be("btn btn-sm btn-outline-secondary block-field-icon-btn");
            importModify.TextContent.Trim().Should().BeEmpty();
            exportModify.TextContent.Trim().Should().BeEmpty();
            importModify.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            exportModify.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();

            var importDelete = importCut.Find("#delete-sheet-rule-button-0");
            var exportDelete = exportCut.Find("#delete-sheet-generation-rule-button-0");
            importDelete.GetAttribute("class").Should().Be(exportDelete.GetAttribute("class"));
            importDelete.GetAttribute("class").Should().Be("btn btn-sm btn-outline-danger block-field-icon-btn");
            importDelete.TextContent.Trim().Should().BeEmpty();
            exportDelete.TextContent.Trim().Should().BeEmpty();
            importDelete.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            exportDelete.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
        });

    // Lot 043 (43.2): unsaved-changes navigation guard -- same mechanism/structure on both editors
    // (NavigationLock presence + confirmation banner ids), string-compared per the parity file's
    // own established pattern rather than each editor's test file separately asserting "it exists".
    // The two editors are exercised one at a time, not simultaneously mounted with unresolved dirty
    // state -- the real .NET NavigationManager invokes registered location-changing handlers
    // sequentially and stops at the first one that calls PreventNavigation(), so a second component
    // registered on the same shared NavigationManager would never see a navigation intercepted by
    // an earlier one still reporting unsaved changes. Clicking "discard and leave" on the import
    // side before rendering the export side clears its own dirty flag (its handler stays
    // registered, but harmlessly no-ops from then on) so the export side's own handler is the one
    // that actually intercepts the second navigation -- this never occurs in the real app anyway,
    // since only one of the two editors is ever mounted at a time.
    [Fact]
    public void NavigationLockAndUnsavedChangesConfirmation_AreStructurallyIdenticalBetweenImportAndExportEditors() =>
        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfileEditor>();

            importCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
            importCut.FindAll("#unsaved-changes-navigation-confirmation").Should().BeEmpty();

            importCut.Find("#profile-name-input").Change("MAD OXO");
            importCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
            var importIndicatorClass = importCut.Find("#unsaved-changes-indicator").GetAttribute("class");

            var navigationManager = Services.GetRequiredService<NavigationManager>();
            navigationManager.NavigateTo("export-profiles");

            var importConfirmationClass = importCut.Find("#unsaved-changes-navigation-confirmation").GetAttribute("class");
            var importDiscardClass = importCut.Find("#discard-changes-and-leave-button").GetAttribute("class");
            var importStayClass = importCut.Find("#stay-on-page-button").GetAttribute("class");

            // Clears import's own dirty flag (its handler stays registered but becomes a permanent
            // no-op) so it doesn't keep intercepting navigations meant for the export side below.
            importCut.Find("#discard-changes-and-leave-button").Click();

            var exportCut = Render<ExportProfileEditor>();

            exportCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeFalse();
            exportCut.FindAll("#unsaved-changes-navigation-confirmation").Should().BeEmpty();

            exportCut.Find("#export-profile-name-input").Change("Profil export OXO");
            exportCut.FindComponent<NavigationLock>().Instance.ConfirmExternalNavigation.Should().BeTrue();
            var exportIndicatorClass = exportCut.Find("#unsaved-changes-indicator").GetAttribute("class");

            navigationManager.NavigateTo("import-profiles");

            var exportConfirmationClass = exportCut.Find("#unsaved-changes-navigation-confirmation").GetAttribute("class");
            var exportDiscardClass = exportCut.Find("#discard-changes-and-leave-button").GetAttribute("class");
            var exportStayClass = exportCut.Find("#stay-on-page-button").GetAttribute("class");

            importIndicatorClass.Should().Be(exportIndicatorClass);
            importConfirmationClass.Should().Be(exportConfirmationClass);
            importDiscardClass.Should().Be(exportDiscardClass);
            importStayClass.Should().Be(exportStayClass);
        });

    // Lot 053 (53.5): explicit mobile non-regression guard-rail. The client review that triggered
    // this lot was desktop-only, and three of its four decisions are breakpoint-conditioned -- this
    // catches a future field silently added to the grid without its col-12 mobile pendant, without
    // needing to enumerate ids by hand (any element carrying a "col-md*" class is checked).
    [Fact]
    public void ImportEditor_GridColumnContainers_AlwaysCarryCol12Alongside_AnyMdColumnClass() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var mdColumns = cut.FindAll("[class*='col-md']");
            mdColumns.Should().NotBeEmpty();
            foreach (var column in mdColumns)
            {
                column.ClassList.Should().Contain("col-12");
            }
        });

    // Export currently has no grid-bearing field at all (53.0: its only root field, "Name", stays
    // col-12 like import's "Nom du profil") -- this stays a guard-rail for if that ever changes,
    // not a currently-exercised assertion.
    [Fact]
    public void ExportEditor_GridColumnContainers_AlwaysCarryCol12Alongside_AnyMdColumnClass() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ExportProfileEditor>();

            var mdColumns = cut.FindAll("[class*='col-md']");
            foreach (var column in mdColumns)
            {
                column.ClassList.Should().Contain("col-12");
            }
        });

    [Fact]
    public void AllAddButtons_CarryW100_OnBothEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        foreach (var id in new[]
        {
            "add-default-tableau-button", "add-default-application-name-button",
            "add-block-field-button", "add-unconditional-colonne-button", "add-point-rule-button",
            "add-sheet-rule-button", "add-header-field-button", "add-header-composite-button",
        })
        {
            importCut.Find($"#{id}").ClassList.Should().Contain("w-100");
        }

        var exportCut = Render<ExportProfileEditor>();
        foreach (var id in new[]
        {
            "add-sheet-generation-rule-button", "add-column-definition-button",
            "add-point-column-definition-button", "add-application-column-definition-button",
        })
        {
            exportCut.Find($"#{id}").ClassList.Should().Contain("w-100");
        }
    });

    // Lot 053 (53.1/53.5): the container applies only max-width (a CSS class, see app.css) -- no
    // inline style, and in particular no fixed width/min-width, which would break mobile.
    [Fact]
    public void ProfileEditorContainer_HasNoInlineStyle_OnlyTheMaxWidthCssClass() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        foreach (var container in new[] { importCut.Find(".profile-editor-container"), exportCut.Find(".profile-editor-container") })
        {
            container.GetAttribute("style").Should().BeNullOrEmpty();
        }
    });

    // Lot 056 (56.8, closing test of the lot): compares the sticky save bar (56.6) and the
    // sub-form submit/cancel buttons in edit mode (56.7) between the two editors -- strict string
    // comparison, not "both non-empty", per this file's own established convention. Deliberately
    // the last test written for lot 056: if it passed before 56.1-56.7 were done on both sides,
    // it wouldn't actually be comparing what it claims to.
    [Fact]
    public void SaveBar_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importBarClass = importCut.Find(".profile-editor-save-bar").GetAttribute("class");
        var exportBarClass = exportCut.Find(".profile-editor-save-bar").GetAttribute("class");

        importBarClass.Should().Be(exportBarClass);
        importBarClass.Should().Be("profile-editor-save-bar");
    });

    [Fact]
    public async Task SheetRuleFormEditMode_SubmitAndCancelButtonClasses_AreIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            importCut.Find("#modify-sheet-rule-button-0").Click();
            exportCut.Find("#modify-sheet-generation-rule-button-0").Click();

            var importSubmitClass = importCut.Find("#save-sheet-rule-button-0").GetAttribute("class");
            var exportSubmitClass = exportCut.Find("#save-sheet-generation-rule-button-0").GetAttribute("class");
            importSubmitClass.Should().Be(exportSubmitClass);
            importSubmitClass.Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");

            var importCancelClass = importCut.Find("#cancel-sheet-rule-button-0").GetAttribute("class");
            var exportCancelClass = exportCut.Find("#cancel-sheet-generation-rule-button-0").GetAttribute("class");
            importCancelClass.Should().Be(exportCancelClass);
            importCancelClass.Should().Be("btn btn-outline-secondary w-100 mt-3");

            importSubmitClass.Should().NotBe(importCancelClass);
        });

    // Lot 057 (57.3, closing test of the lot): the add-sheet-rule toggle button (57.1), the last
    // element this lot introduces that didn't exist in either editor before it. Strict class-string
    // comparison, per this file's own established convention -- and the mutual-exclusion behavior
    // (57.2) is already exercised on both sides by ImportProfileEditorLot057Tests.cs/
    // ExportProfileEditorLot057Tests.cs, so parity here isn't only cosmetic.
    [Fact]
    public void AddSheetRuleToggleButton_CssClass_IsIdenticalBetweenImportAndExportEditors() => WithCulture("en-US", () =>
    {
        var importCut = Render<ImportProfileEditor>();
        var exportCut = Render<ExportProfileEditor>();

        var importToggleClass = importCut.Find("#toggle-add-sheet-rule-form-button").GetAttribute("class");
        var exportToggleClass = exportCut.Find("#toggle-add-sheet-generation-rule-form-button").GetAttribute("class");

        importToggleClass.Should().Be(exportToggleClass);
        importToggleClass.Should().Be("btn btn-sm btn-outline-secondary d-flex align-items-center justify-content-center gap-1");
    });

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
}
