using System.Globalization;
using Bunit;
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
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 030 (30.7): explicit structural-parity guard-rail for ImportProfiles.razor/ExportProfiles.razor's
// header action buttons -- string comparison of the shared wrapper's and each button's CSS classes,
// same pattern as R1/30.5 above, rather than trusting the two pages' own independent test files not
// to drift apart silently over time.
public class ProfileListPageParityTests : BunitContext
{
    private readonly Mock<IDefaultProfileSeeder> _defaultProfileSeederMock = new();

    public ProfileListPageParityTests()
    {
        var dbContextFactory = new TestDbContextFactory("ProfileListPageParityTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();

        // 2026-09-16: mirrors ImportProfilesTests/ExportProfilesTests' own setup -- both pages now
        // inject IDefaultProfileSeeder for the "reset the standard profile" row action.
        _defaultProfileSeederMock.Setup(s => s.ImportProfileId).Returns(DefaultProfileSeeder.ImportProfileId);
        _defaultProfileSeederMock.Setup(s => s.ExportProfileId).Returns(DefaultProfileSeeder.ExportProfileId);
        Services.AddSingleton(_defaultProfileSeederMock.Object);
        Services.AddLocalization();
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

    [Fact]
    public void HeaderButtonWrapper_CssClass_IsIdenticalBetweenImportAndExportProfilesLists() =>
        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfiles>();
            var exportCut = Render<ExportProfiles>();

            var importWrapperClass = importCut.Find("#test-import-profile-button").ParentElement!.GetAttribute("class");
            var exportWrapperClass = exportCut.Find("#test-export-profile-button").ParentElement!.GetAttribute("class");

            importWrapperClass.Should().Be(exportWrapperClass);
            importWrapperClass.Should().Be("right-aligned-actions d-flex gap-2 mb-3");
        });

    [Fact]
    public void HeaderButtons_CssClass_IsIdenticalBetweenImportAndExportProfilesLists() =>
        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfiles>();
            var exportCut = Render<ExportProfiles>();

            var importTestButtonClass = importCut.Find("#test-import-profile-button").GetAttribute("class");
            var exportTestButtonClass = exportCut.Find("#test-export-profile-button").GetAttribute("class");
            importTestButtonClass.Should().Be(exportTestButtonClass);

            var importCreateButtonClass = importCut.Find("#create-profile-button").GetAttribute("class");
            var exportCreateButtonClass = exportCut.Find("#create-export-profile-button").GetAttribute("class");
            importCreateButtonClass.Should().Be(exportCreateButtonClass);
            importCreateButtonClass.Should().Contain("flex-fill");
        });

    // Lot 059 (59.7/59.8): row-level delete buttons harmonized onto the exact outline-danger class
    // already used by the editors' own delete-row buttons (e.g. delete-default-tableau-button-0) --
    // strict string comparison, both between the two profile lists and against that editor
    // reference, token order included. Modify/Duplicate stay outline-secondary on both lists.
    private const string EditorDeleteButtonReferenceClass = "btn btn-sm btn-outline-danger block-field-icon-btn";

    private static ImportProfile BuildImportProfileForParity()
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);
        return new ImportProfile("MAD OXO parity", "MAD TRAVAUX", [], [], [sheetRule]);
    }

    private static ExportProfile BuildExportProfileForParity() =>
        new("MAD OXO export parity",
            [
                new SheetGenerationRule(
                    "Parents", PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")],
                    [])
            ]);

    [Fact]
    public async Task DeleteButton_CssClass_IsIdenticalBetweenImportAndExportProfilesLists_AndMatchesEditorReference()
    {
        var importProfile = BuildImportProfileForParity();
        var exportProfile = BuildExportProfileForParity();
        await Services.GetRequiredService<IImportProfileStore>().SaveAsync(importProfile);
        await Services.GetRequiredService<IExportProfileStore>().SaveAsync(exportProfile);

        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfiles>();
            var exportCut = Render<ExportProfiles>();

            var importDeleteClass = importCut.Find($"#delete-profile-button-{importProfile.Id}").GetAttribute("class");
            var exportDeleteClass = exportCut.Find($"#delete-export-profile-button-{exportProfile.Id}").GetAttribute("class");

            importDeleteClass.Should().Be(exportDeleteClass);
            importDeleteClass.Should().Be(EditorDeleteButtonReferenceClass);
        });
    }

    [Fact]
    public async Task ModifyAndDuplicateButtons_StayOutlineSecondary_OnBothProfileLists()
    {
        var importProfile = BuildImportProfileForParity();
        var exportProfile = BuildExportProfileForParity();
        await Services.GetRequiredService<IImportProfileStore>().SaveAsync(importProfile);
        await Services.GetRequiredService<IExportProfileStore>().SaveAsync(exportProfile);

        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfiles>();
            var exportCut = Render<ExportProfiles>();

            const string nonDestructiveClass = "btn btn-outline-secondary btn-sm block-field-icon-btn";

            // Lot 079.8: the "view details" button, first of the row on both lists.
            importCut.Find($"#details-profile-button-{importProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
            exportCut.Find($"#details-export-profile-button-{exportProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
            importCut.Find($"#edit-profile-button-{importProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
            importCut.Find($"#duplicate-profile-button-{importProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
            exportCut.Find($"#edit-export-profile-button-{exportProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
            exportCut.Find($"#duplicate-export-profile-button-{exportProfile.Id}").GetAttribute("class").Should().Be(nonDestructiveClass);
        });
    }

    // 2026-09-16: the "reset the standard profile to its seed definition" row action -- only ever
    // rendered for the row matching DefaultProfileSeeder.ImportProfileId/ExportProfileId, and
    // styled identically to Modify/Duplicate (non-destructive, outline-secondary) on both lists.
    [Fact]
    public async Task ResetButton_CssClass_IsIdenticalBetweenImportAndExportProfilesLists_AndIsNonDestructive()
    {
        var importProfile = new ImportProfile(
            DefaultProfileSeeder.ImportProfileId, "Profil OXO standard", ImportProfile.DefaultReperePrefix,
            "MAD TRAVAUX", [], [], BuildImportProfileForParity().SheetRules);
        var exportProfile = new ExportProfile(
            DefaultProfileSeeder.ExportProfileId, "Profil OXO standard", BuildExportProfileForParity().SheetRules);
        await Services.GetRequiredService<IImportProfileStore>().SaveAsync(importProfile);
        await Services.GetRequiredService<IExportProfileStore>().SaveAsync(exportProfile);

        WithCulture("en-US", () =>
        {
            var importCut = Render<ImportProfiles>();
            var exportCut = Render<ExportProfiles>();

            const string nonDestructiveClass = "btn btn-outline-secondary btn-sm block-field-icon-btn";

            var importResetClass = importCut.Find($"#reset-profile-button-{importProfile.Id}").GetAttribute("class");
            var exportResetClass = exportCut.Find($"#reset-export-profile-button-{exportProfile.Id}").GetAttribute("class");

            importResetClass.Should().Be(exportResetClass);
            importResetClass.Should().Be(nonDestructiveClass);
        });
    }
}
