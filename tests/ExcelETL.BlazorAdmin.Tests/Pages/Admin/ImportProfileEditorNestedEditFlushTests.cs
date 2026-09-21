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

// Regression guard-rail for the 2026-09-16 incident: editing an *already-added* item inside a
// nested sub-form (BlockFieldForm/HeaderFieldRuleForm/HeaderCompositeRuleForm, and
// FieldPresencePointRuleForm until lot 084) via its own "Modify" button, then saving the profile directly --
// without ever clicking that nested item's own "Save changes" button -- used to silently discard
// the edit, keeping the old value with no error shown. Lot 056 (29/07) only fixed the equivalent
// gap one level up (an open SheetRuleForm flushed on profile save); it never generalized to the
// sub-forms nested *inside* SheetRuleForm, which is exactly the gap this file locks down: each
// test here reproduces the real incident end-to-end (open the sheet rule, open an existing item's
// edit mode, change a field, click "Save profile" directly) for every nested sub-form type, so a
// future sub-form added without wiring it into the parent's flush chain fails a test immediately
// instead of waiting for the next production incident.
public class ImportProfileEditorNestedEditFlushTests : BunitContext
{
    public ImportProfileEditorNestedEditFlushTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorNestedEditFlushTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    [Fact]
    public async Task EditingAnExistingBlockField_ThenSavingProfileDirectly_PersistsTheNewName() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: [], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-block-field-button-0").Click();
            cut.Find("#edit-0-block-field-0-name-input").Change("IdentificationRenommee");

            // Deliberately never click #edit-0-save-block-field-button-0.
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().Locator.Fields.Single().Name.Should().Be("IdentificationRenommee");
        });

    [Fact]
    public async Task EditingAnExistingHeaderField_ThenSavingProfileDirectly_PersistsTheNewRange() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
                fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
            var headerFields = new List<HeaderFieldRule> { new("nomMAD", new DirectCell("PROCEDURE", "M2:O2")) };
            var sheetRule = new SheetExtractionRule(
                "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], headerFields, []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-header-field-button-0").Click();
            cut.Find("#edit-0-header-field-0-header-field-range-input").Change("M3:O3");

            // Deliberately never click #edit-0-save-header-field-button-0.
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().HeaderFields.Single().Cell.Range.Should().Be("M3:O3");
        });

    [Fact]
    public async Task EditingAnExistingHeaderComposite_ThenSavingProfileDirectly_PersistsTheNewTemplate() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
                fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
            var headerFields = new List<HeaderFieldRule> { new("revision", new DirectCell("PROCEDURE", "P2:Q2")) };
            var headerComposites = new List<HeaderCompositeRule> { new("Designation", "Rev {revision}") };
            var sheetRule = new SheetExtractionRule(
                "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], headerFields, headerComposites);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-header-composite-button-0").Click();
            cut.Find("#edit-0-header-composite-0-header-composite-template-input").Change("Revision {revision}");

            // Deliberately never click #edit-0-save-header-composite-button-0.
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().HeaderComposites.Single().Template.Should().Be("Revision {revision}");
        });

}
