using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 038 (38.3): profiles are loaded in process via IImportProfileStore/IExportProfileStore (real
// EF InMemory provider, same convention as every other profile-consuming page) -- only the actual
// POST /api/oxo/process call goes through a mocked IOxoApiTestClient (Moq), per the ticket's own
// explicit instruction: no real HTTP call in bUnit tests.
public class ApiTestTests : BunitContext
{
    private readonly Mock<IOxoApiTestClient> _oxoApiTestClientMock = new();

    public ApiTestTests()
    {
        var dbContextFactory = new TestDbContextFactory("ApiTestTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddSingleton(_oxoApiTestClientMock.Object);
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

    private IImportProfileStore ImportProfileStore => Services.GetRequiredService<IImportProfileStore>();

    private IExportProfileStore ExportProfileStore => Services.GetRequiredService<IExportProfileStore>();

    private static ImportProfile BuildImportProfile(string name = "Import profile") =>
        new(
            name,
            "MAD TRAVAUX",
            [],
            [],
            [
                new SheetExtractionRule(
                    "ISOLEMENT",
                    new RepeatingBlockLocator(
                        "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
                        fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]),
                    pointRules: [],
                    unconditionalColonneNames: ["PROLOCK VANNES"])
            ]);

    private static ExportProfile BuildExportProfile(string name = "Export profile") =>
        new(
            name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [],
                    [])
            ]);

    private async Task<(ImportProfile ImportProfile, ExportProfile ExportProfile)> SeedProfilesAsync()
    {
        var importProfile = BuildImportProfile();
        var exportProfile = BuildExportProfile();
        await ImportProfileStore.SaveAsync(importProfile);
        await ExportProfileStore.SaveAsync(exportProfile);
        return (importProfile, exportProfile);
    }

    private static void SelectFile(IRenderedComponent<ApiTest> cut)
    {
        cut.FindComponent<InputFile>()
            .UploadFiles(InputFileContent.CreateFromText("dummy content", "source.xlsx"));
    }

    // Lot 041 (41.2): process-button was one of the audit's flagged icon-less CTAs.
    [Fact]
    public void ProcessButton_HasIcon() => WithCulture("en-US", () =>
    {
        var cut = Render<ApiTest>();

        cut.Find("#process-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
    });

    [Fact]
    public void ProcessButton_DisabledUntilProfilesAndFileSelected_ThenEnabled() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();

        var cut = Render<ApiTest>();

        cut.Find("#process-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#process-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        cut.Find("#process-button").HasAttribute("disabled").Should().BeTrue();

        SelectFile(cut);
        cut.Find("#process-button").HasAttribute("disabled").Should().BeFalse();
    }));

    [Fact]
    public void ProcessButton_Click_WithSuccessResult_RendersDownloadLinkAndFileName() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(importProfile.Id, exportProfile.Id, It.IsAny<Stream>(), "source.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.Success(new MemoryStream([1, 2, 3]), "MAD_38-C7401_20260101120000.xlsx"));

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        var result = cut.Find("#api-test-result");
        result.ClassList.Should().Contain("bg-success-subtle");
        result.TextContent.Should().Contain("MAD_38-C7401_20260101120000.xlsx");
        cut.Find("#download-generated-workbook-link").GetAttribute("download").Should().Be("MAD_38-C7401_20260101120000.xlsx");
    }));

    [Fact]
    public void ProcessButton_Click_WithBusinessRejection_RendersErrorList() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.BusinessRejection(
                [new OxoApiTestRejectionError("PROCEDURE", "M2:O2", "RequiredFieldMissing", "Repere is required.")]));

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        var result = cut.Find("#api-test-result");
        result.ClassList.Should().Contain("alert-danger");
        result.TextContent.Should().Contain("Repere is required.");
    }));

    [Fact]
    public void ProcessButton_Click_WithProfileNotFound_RendersDetailMessage() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.ProfileNotFound("Import profile 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' was not found."));

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        cut.Find("#api-test-result").TextContent.Should().Contain("Import profile").And.Contain("was not found");
    }));

    [Fact]
    public void ProcessButton_Click_WithUnauthorized_RendersServerConfigurationMessage() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.Unauthorized());

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        cut.Find("#api-test-result").TextContent.Should().Contain("OxoApiTestClient:ApiKey");
    }));

    [Fact]
    public void ProcessButton_Click_WithTechnicalError_RendersGenericMessage_NoRawDetail() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.TechnicalError(500));

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        var result = cut.Find("#api-test-result");
        result.TextContent.Should().Contain("unexpected error");
        result.TextContent.Should().NotContain("500");
    }));

    [Fact]
    public void ProcessButton_Click_WithConnectionError_RendersServerUnreachableMessage() => WithCulture("en-US", () => RunAsync(async () =>
    {
        var (importProfile, exportProfile) = await SeedProfilesAsync();
        _oxoApiTestClientMock
            .Setup(c => c.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OxoApiTestResult.ConnectionError());

        var cut = Render<ApiTest>();
        cut.Find("#import-profile-select").Change(importProfile.Id.ToString());
        cut.Find("#export-profile-select").Change(exportProfile.Id.ToString());
        SelectFile(cut);

        cut.Find("#process-button").Click();

        var result = cut.Find("#api-test-result");
        result.ClassList.Should().Contain("alert-danger");
        result.TextContent.Should().Contain("OxoApiTestClient:BaseUrl");
    }));

    private static void RunAsync(Func<Task> action) => action().GetAwaiter().GetResult();
}
