using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 080.4 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md): the sheet-name checks
// the domain now owns (80.2/80.3) reach the editor through the existing draft conversion, localized.
public class ExportProfileEditorSheetNameValidationTests : BunitContext
{
    private readonly Mock<IExportProfileStore> _store = new();

    public ExportProfileEditorSheetNameValidationTests()
    {
        Services.AddSingleton(_store.Object);
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    [Fact]
    public void AddSheetRule_WithNameOf32Characters_ShowsLocalizedMessageOnTheRule_AndAddsNothing() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#sheet-generation-rule-name-input").Change(new string('A', 32));
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#add-sheet-generation-rule-button").Click();

            cut.Find(".alert.alert-danger[role='alert']").TextContent.Should()
                .Be("The sheet name must not exceed 31 characters (Excel limit).");
            cut.FindAll(".sheet-rule-card").Should().BeEmpty();
        });

    [Fact]
    public void SaveProfile_WithTwoSheetNamesEqualIgnoringCase_ShowsLocalizedRootMessage_AndNeverSaves() =>
        WithCulture("fr-FR", () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
            cut.Find("#add-sheet-generation-rule-button").Click();

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            cut.Find("#sheet-generation-rule-name-input").Change("parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));

            cut.Find("#save-export-profile-button").Click();

            cut.Markup.Should().Contain(
                "Le nom de feuille 'parents' est utilisé par plusieurs feuilles (Excel ne distingue pas majuscules et minuscules).");
            _store.Verify(s => s.SaveAsync(It.IsAny<ExportProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        });
}
