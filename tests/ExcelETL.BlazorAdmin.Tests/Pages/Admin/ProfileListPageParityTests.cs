using System.Globalization;
using Bunit;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 030 (30.7): explicit structural-parity guard-rail for ImportProfiles.razor/ExportProfiles.razor's
// header action buttons -- string comparison of the shared wrapper's and each button's CSS classes,
// same pattern as R1/30.5 above, rather than trusting the two pages' own independent test files not
// to drift apart silently over time.
public class ProfileListPageParityTests : BunitContext
{
    public ProfileListPageParityTests()
    {
        var dbContextFactory = new TestDbContextFactory("ProfileListPageParityTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
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
}
