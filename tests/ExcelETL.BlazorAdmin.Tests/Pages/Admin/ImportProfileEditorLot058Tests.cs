using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 058: button finishing touches -- 58.2 (Tableaux/Applications add-button height, import-only)
// and 58.3 (icon+label gabarit, import side).
public class ImportProfileEditorLot058Tests : BunitContext
{
    public ImportProfileEditorLot058Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot058Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    // ------------------------------------------------------------------------------------------
    // 58.2
    // ------------------------------------------------------------------------------------------

    // Lot 063: fixed in place -- w-md-auto (shrink-to-content at >=768px) was removed so the
    // button always fills its now-fixed col-md-4 column instead of a content width that could
    // overflow it and collide with the field (a mid-viewport overlap client reported).
    [Fact]
    public void TableauAndApplicationAddButtons_CarryFieldInlineActionClass_AndKeepExistingClasses()
    {
        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[] { "add-default-tableau-button", "add-default-application-name-button" })
        {
            var button = cut.Find($"#{id}");
            button.ClassList.Should().Contain("field-inline-action");
            button.ClassList.Should().Contain("btn");
            button.ClassList.Should().Contain("btn-secondary");
            button.ClassList.Should().Contain("w-100");
            button.ClassList.Should().NotContain("w-md-auto");
        }
    }

    [Fact]
    public void TableauAndApplicationAddRows_NoLongerCarryAlignItemsEnd()
    {
        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[] { "add-default-tableau-button", "add-default-application-name-button" })
        {
            var row = cut.Find($"#{id}").ParentElement!.ParentElement!;
            row.ClassList.Should().Contain("row");
            row.ClassList.Should().NotContain("align-items-end");
        }
    }

    [Fact]
    public void InlineEditRows_StillCarryAlignItemsEnd_GuardRailAgainstOverBroadRemoval()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#default-tableau-name-input").Change("TRAVAUX COMPLET");
        cut.Find("#add-default-tableau-button").Click();
        cut.Find("#edit-default-tableau-button-0").Click();

        var editRow = cut.Find("#default-tableau-edit-input-0").ParentElement!.ParentElement!;
        editRow.ClassList.Should().Contain("align-items-end");
    }

    [Fact]
    public void OtherAddButtons_DoNotCarryFieldInlineActionClass_NonGeneralizationGuardRail()
    {
        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[]
        {
            "add-block-field-button", "add-unconditional-colonne-button", "add-point-rule-button",
            "add-sheet-rule-button", "add-header-field-button", "add-header-composite-button",
        })
        {
            cut.Find($"#{id}").ClassList.Should().NotContain("field-inline-action");
        }
    }

    [Fact]
    public void TableauAndApplicationAddButtons_HaveNoInlineHeightStyle()
    {
        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[] { "add-default-tableau-button", "add-default-application-name-button" })
        {
            cut.Find($"#{id}").GetAttribute("style").Should().BeNullOrEmpty();
        }
    }

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static ImportProfile BuildProfileWithOneSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }
}
