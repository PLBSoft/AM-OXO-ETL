using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Bunit;
using ExcelETL.Application.Extraction;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.ExternalApi;
using ExcelETL.BlazorAdmin.Tests.ExternalApi;
using ExcelETL.Domain.Entities;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class UploadTestTests : BunitContext
{
    private const string BaseUrl = "http://localhost/";

    private readonly FakeExcelDownloadInterop _downloadInterop = new();

    public UploadTestTests()
    {
        var dbContextFactory = new TestDbContextFactory("UploadTestTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExtractionConfigRepository, ExtractionConfigRepository>();
        Services.AddSingleton<IExcelDownloadInterop>(_downloadInterop);
        Services.AddLocalization();
    }

    private sealed class FakeExcelDownloadInterop : IExcelDownloadInterop
    {
        public string? DownloadedFileName { get; private set; }
        public byte[]? DownloadedBytes { get; private set; }

        public async Task DownloadFileFromStreamAsync(string fileName, Stream content)
        {
            DownloadedFileName = fileName;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            DownloadedBytes = buffer.ToArray();
        }
    }

    private async Task SeedConfigAsync(ExtractionConfig config)
    {
        var repository = Services.GetRequiredService<IExtractionConfigRepository>();
        await repository.AddAsync(config);
    }

    private void RegisterExcelProcessingClient(FakeHttpMessageHandler fakeHandler)
    {
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        Services.AddSingleton(new ExcelProcessingClient(httpClient));
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

    private static HttpResponseMessage SuccessResponse(byte[] bytes, string fileName = "processed.xlsx")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = fileName
        };
        return response;
    }

    [Fact]
    public void UploadTest_WithNoConfigs_RendersFileInputAndNoConfigsMessage() => WithCulture("en-US", () =>
    {
        RegisterExcelProcessingClient(new FakeHttpMessageHandler(
            _ => throw new InvalidOperationException("No HTTP call should be made when no file is uploaded.")));

        var cut = Render<UploadTest>();

        cut.FindComponent<InputFile>().Should().NotBeNull();
        cut.Markup.Should().Contain("No extraction configurations exist yet.");
    });

    [Fact]
    public async Task UploadTest_SelectingFileWithoutConfig_ShowsErrorAndMakesNoHttpCall() => await WithCultureAsync("en-US", async () =>
    {
        var calledHttp = false;
        RegisterExcelProcessingClient(new FakeHttpMessageHandler(_ =>
        {
            calledHttp = true;
            return Task.FromResult(SuccessResponse([1, 2, 3]));
        }));

        var cut = Render<UploadTest>();

        var inputFileComponent = cut.FindComponent<InputFile>();
        var file = InputFileContent.CreateFromText("dummy content", "invoice.xlsx");
        inputFileComponent.UploadFiles(file);

        cut.WaitForAssertion(() => cut.Find("#upload-status").ClassList.Should().Contain("alert-danger"));

        calledHttp.Should().BeFalse();
    });

    [Fact]
    public void UploadTest_WithFileSelectedButNoConfig_DoesNotEnterUploadingState() => WithCulture("en-US", () =>
    {
        RegisterExcelProcessingClient(new FakeHttpMessageHandler(
            _ => throw new InvalidOperationException("No HTTP call should be made when no config is selected.")));

        var cut = Render<UploadTest>();

        var inputFileComponent = cut.FindComponent<InputFile>();
        var file = InputFileContent.CreateFromText("dummy content", "invoice.xlsx");
        inputFileComponent.UploadFiles(file);

        cut.Find("#upload-status").ClassList.Should().NotContain("alert-info");
        cut.Find("#upload-status").ClassList.Should().Contain("alert-danger");
    });

    [Fact]
    public async Task UploadTest_WithConfigAndFileSelected_SendsApiKeyAndReachesSuccess() => await WithCultureAsync("en-US", async () =>
    {
        var config = new ExtractionConfig("Invoices");

        Guid? capturedApiKeyPresence = null;
        var fakeHandler = new FakeHttpMessageHandler(request =>
        {
            capturedApiKeyPresence = request.Headers.Contains("X-Api-Key") ? config.Id : null;
            return Task.FromResult(SuccessResponse([1, 2, 3], "processed-invoice.xlsx"));
        });
        RegisterExcelProcessingClient(fakeHandler);

        await SeedConfigAsync(config);

        var cut = Render<UploadTest>();
        cut.WaitForState(() => cut.FindAll("#extraction-config-select option").Count > 1);

        cut.Find("#extraction-config-select").Change(config.Id.ToString());

        var inputFileComponent = cut.FindComponent<InputFile>();
        var file = InputFileContent.CreateFromText("dummy content", "invoice.xlsx");
        inputFileComponent.UploadFiles(file);

        cut.WaitForAssertion(() => cut.Find("#upload-status").ClassList.Should().Contain("alert-success"));

        capturedApiKeyPresence.Should().Be(config.Id);
        _downloadInterop.DownloadedFileName.Should().Be("processed-invoice.xlsx");
        _downloadInterop.DownloadedBytes.Should().Equal(1, 2, 3);
    });

    [Fact]
    public async Task UploadTest_OnApiFailure_ShowsErrorMessage() => await WithCultureAsync("en-US", async () =>
    {
        var config = new ExtractionConfig("Invoices");

        var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"detail\":\"Extraction config not found.\"}")
            }));
        RegisterExcelProcessingClient(fakeHandler);

        await SeedConfigAsync(config);

        var cut = Render<UploadTest>();
        cut.WaitForState(() => cut.FindAll("#extraction-config-select option").Count > 1);

        cut.Find("#extraction-config-select").Change(config.Id.ToString());

        var inputFileComponent = cut.FindComponent<InputFile>();
        var file = InputFileContent.CreateFromText("dummy content", "invoice.xlsx");
        inputFileComponent.UploadFiles(file);

        cut.WaitForAssertion(() => cut.Find("#upload-status").ClassList.Should().Contain("alert-danger"));
        cut.Markup.Should().Contain("404");
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
}
