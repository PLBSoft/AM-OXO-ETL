using System.Net;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using IsolementFieldNames = ExcelETL.Application.Extraction.Oxo.Isolement.IsolementFieldNames;
using ProcedureFieldNames = ExcelETL.Application.Extraction.Oxo.Procedure.ProcedureFieldNames;
using ProcedureHeaderFieldNames = ExcelETL.Application.Extraction.Oxo.Procedure.ProcedureHeaderFieldNames;

namespace ExcelETL.WebAPI.Tests.Oxo;

// K1/K2: HTTP-level mirror of ExcelProcessEndpointTests but for the OXO pipeline. The hardcoded
// ImportProfile/ExportProfile below duplicates ImportPipelineOrchestratorIntegrationTests'/
// GenerationPipelineIntegrationTests' fixtures rather than sharing them, per this repo's established
// no-shared-test-helper convention.
public class OxoProcessEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidApiKey = "test-api-key-12345";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    private readonly string _generatedFilesArchiveRoot =
        Path.Combine(Path.GetTempPath(), "OxoProcessEndpointTests_GeneratedFilesArchive_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public OxoProcessEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "OxoProcessTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("GeneratedFilesArchive:RootPath", _generatedFilesArchiveRoot);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    [Fact]
    public async Task Process_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Lot 036.1: ImportProfileId/ExportProfileId totally absent from the multipart body must
    // produce an explicit 400, distinct from the 404 "unknown profile" case below (which requires
    // a syntactically-valid Guid).
    [Fact]
    public async Task Process_WithoutImportProfileIdField_ReturnsBadRequestMentioningImportProfileId()
    {
        var client = CreateAuthenticatedClient();
        var (_, exportProfileId) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        var fileContent = new StreamContent(sourceWorkbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(exportProfileId.ToString()), "ExportProfileId" },
            { fileContent, "File", "source.xlsx" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The ImportProfileId parameter is required.");
    }

    [Fact]
    public async Task Process_WithoutExportProfileIdField_ReturnsBadRequestMentioningExportProfileId()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, _) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        var fileContent = new StreamContent(sourceWorkbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(importProfileId.ToString()), "ImportProfileId" },
            { fileContent, "File", "source.xlsx" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The ExportProfileId parameter is required.");
    }

    [Fact]
    public async Task Process_WithBothProfileIdFieldsMissing_ReturnsBadRequestMentioningImportProfileIdFirst()
    {
        // Documented, not assumed: both checks run sequentially in the controller, so when both
        // fields are absent, only the first one checked (ImportProfileId) is reported.
        var client = CreateAuthenticatedClient();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        var fileContent = new StreamContent(sourceWorkbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { fileContent, "File", "source.xlsx" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The ImportProfileId parameter is required.");
        body.Should().NotContain("The ExportProfileId parameter is required.");
    }

    [Fact]
    public async Task Process_WithUnknownImportProfileId_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();
        var (_, exportProfileId) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        using var content = BuildMultipartContent(Guid.NewGuid(), exportProfileId, sourceWorkbook);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Process_WithUnknownExportProfileId_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, _) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        using var content = BuildMultipartContent(importProfileId, Guid.NewGuid(), sourceWorkbook);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Process_WithRejectedFile_ReturnsUnprocessableEntityAndNoGeneratedFile()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceWorkbook);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().NotBe(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // Lot 036.2: a non-Excel byte stream (plain text renamed .xlsx) makes XLWorkbook's own
    // constructor throw System.IO.FileFormatException -- confirmed by direct investigation (36.0),
    // not assumed -- which is caught explicitly in OxoController and translated to 400, never an
    // unqualified 500.
    [Fact]
    public async Task Process_WithNonExcelFileContent_ReturnsBadRequestWithExplicitMessage()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var invalidContent = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("this is not an excel file, just plain text content"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, invalidContent);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The uploaded file is not a valid Excel workbook or is corrupted.");
    }

    [Fact]
    public async Task Process_WithValidRequestAgainstC7401Fixture_ReturnsGeneratedWorkbookWithParentsAndEnfants()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var generated = new XLWorkbook(new MemoryStream(bytes));
        generated.Worksheets.Select(ws => ws.Name).Should().Equal("Parents", "Enfants");
        generated.Worksheet("Parents").Cell(2, 1).GetString().Should().Be("38-C7401");
    }

    [Fact]
    public async Task Process_WithD8570Fixture_KeepsNoConditionalPointCreatedIsolementAsNormalRow()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var generated = new XLWorkbook(new MemoryStream(bytes));
        var enfants = generated.Worksheet("Enfants");
        enfants.RowsUsed().Should().Contain(row => row.Cell(2).GetString() == "VANNE");
    }

    // Lot 034: the request/response contract is unchanged by any of the assertions above (still
    // asserting 200/422/content-type exactly as Lot K did) -- these new tests only add archive-side
    // assertions on top of the pre-existing, untouched ones.

    [Fact]
    public async Task Process_WithValidRequestAgainstC7401Fixture_PersistsArchiveRecordAndBothFilesOnDisk()
    {
        // C7401's real fixture carries its own non-blocking warning since Lot 032 (a TYPE-incoherence
        // anomaly in PROCEDURE's tâches multiples) -- Status here is NonBlockingWarning, not Success;
        // the genuine Success-status mapping is covered at the Application unit-test level instead
        // (ProcessOxoFileServiceTests.ProcessAsync_WhenFileIsAccepted_...), where a zero-error
        // ImportResult can be constructed directly rather than depending on a real fixture staying
        // warning-free forever.
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream, "C7401.xlsx");

        var response = await client.PostAsync("/api/oxo/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var records = await SearchArchiveAsync();
        var record = records.Should().ContainSingle().Which;
        record.Status.Should().Be(GeneratedFileArchiveStatus.NonBlockingWarning);
        record.EquipementRepere.Should().Be("38-C7401");
        record.SourceFileName.Should().Be("C7401.xlsx");
        record.TargetFileName.Should().NotBeNull();
        record.TargetFilePath.Should().NotBeNull();
        record.ImportProfileId.Should().Be(importProfileId);
        record.ExportProfileId.Should().Be(exportProfileId);

        File.Exists(Path.Combine(_generatedFilesArchiveRoot, record.SourceFilePath)).Should().BeTrue();
        File.Exists(Path.Combine(_generatedFilesArchiveRoot, record.TargetFilePath!)).Should().BeTrue();
    }

    [Fact]
    public async Task Process_WithD8570Fixture_PersistsNonBlockingWarningArchiveRecordWithBothFiles()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream, "D8570.xlsx");

        var response = await client.PostAsync("/api/oxo/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var records = await SearchArchiveAsync();
        var record = records.Should().ContainSingle().Which;
        record.Status.Should().Be(GeneratedFileArchiveStatus.NonBlockingWarning);
        record.TargetFilePath.Should().NotBeNull();
        File.Exists(Path.Combine(_generatedFilesArchiveRoot, record.SourceFilePath)).Should().BeTrue();
        File.Exists(Path.Combine(_generatedFilesArchiveRoot, record.TargetFilePath!)).Should().BeTrue();
    }

    [Fact]
    public async Task Process_WithRejectedFile_PersistsRejectedArchiveRecordWithSourceOnlyOnDisk()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceWorkbook, "corrompu.xlsx");

        var response = await client.PostAsync("/api/oxo/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var records = await SearchArchiveAsync();
        var record = records.Should().ContainSingle().Which;
        record.Status.Should().Be(GeneratedFileArchiveStatus.Rejected);
        record.EquipementRepere.Should().BeNull();
        record.SourceFileName.Should().Be("corrompu.xlsx");
        record.TargetFileName.Should().BeNull();
        record.TargetFilePath.Should().BeNull();

        File.Exists(Path.Combine(_generatedFilesArchiveRoot, record.SourceFilePath)).Should().BeTrue();
        Directory.GetFiles(_generatedFilesArchiveRoot, "*_target_*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Process_WhenArchivingFails_StillReturns200WithGeneratedFile_AndLogsTheFailure()
    {
        var sink = new CapturedLogEntries();
        using var brokenArchiveFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<ProcessOxoFileService>>(new CapturingLogger<ProcessOxoFileService>(sink));

                var throwingWriter = new Mock<IGeneratedFileWriter>();
                throwingWriter
                    .Setup(w => w.WriteSourceAsync(
                        It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("simulated disk failure"));
                services.RemoveAll<IGeneratedFileWriter>();
                services.AddSingleton<IGeneratedFileWriter>(throwingWriter.Object);
            });
        });
        var client = brokenArchiveFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var (importProfileId, exportProfileId) = await SeedProfilesAsync(brokenArchiveFactory);
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream, "C7401.xlsx");

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();

        sink.Entries.Should().Contain(e =>
            e.Level == LogLevel.Error && e.Message.Contains("Failed to archive generated files"));

        using var scope = brokenArchiveFactory.Services.CreateScope();
        var archiveStore = scope.ServiceProvider.GetRequiredService<IGeneratedFileArchiveStore>();
        (await archiveStore.SearchAsync(null)).Should().BeEmpty();
    }

    private async Task<IReadOnlyList<GeneratedFileRecord>> SearchArchiveAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var archiveStore = scope.ServiceProvider.GetRequiredService<IGeneratedFileArchiveStore>();
        return await archiveStore.SearchAsync(null);
    }

    // K2: a successful call to the route produces an upload log entry and an egress log entry,
    // asserted at the ILogger<T> boundary (CapturingLogger) rather than by inspecting real
    // SystemLogs rows -- see tickets-tdd-migration-webapi-oxo.md's K2 amendment for why no
    // Serilog-sink-level test convention exists in this repo.
    [Fact]
    public async Task Process_WithValidRequest_LogsUploadAndEgress()
    {
        var sink = new CapturedLogEntries();
        using var loggingFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<OxoController>>(new CapturingLogger<OxoController>(sink));
            });
        });
        var client = loggingFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var (importProfileId, exportProfileId) = await SeedProfilesAsync(loggingFactory);
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream, "C7401.xlsx");

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sink.Entries.Should().Contain(e => e.Message.Contains("OXO upload") && e.Message.Contains("C7401.xlsx"));
        sink.Entries.Should().Contain(e => e.Message.Contains("OXO egress") && e.Message.Contains("200"));
    }

    // Lot 036.3: no production-code change here -- these tests only add the missing HTTP coverage.
    // Investigation found the "no File field at all" case never reaches OxoController's own
    // request.File is null check: [ApiController]'s automatic model-state validation treats the
    // non-nullable IFormFile property as implicitly required and short-circuits with its own
    // standard ProblemDetails body before the action runs -- verified here rather than assumed.
    [Fact]
    public async Task Process_WithoutFileFieldAtAll_ReturnsBadRequestFromAutomaticModelValidation()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();

        using var content = new MultipartFormDataContent
        {
            { new StringContent(importProfileId.ToString()), "ImportProfileId" },
            { new StringContent(exportProfileId.ToString()), "ExportProfileId" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The File field is required.");
    }

    [Fact]
    public async Task Process_WithZeroLengthFile_ReturnsBadRequestWithSameMessageAsMissingFile()
    {
        var client = CreateAuthenticatedClient();
        var (importProfileId, exportProfileId) = await SeedProfilesAsync();
        using var emptyStream = new MemoryStream();
        using var content = BuildMultipartContent(importProfileId, exportProfileId, emptyStream);

        var response = await client.PostAsync("/api/oxo/process", content);

        // Same code path as the missing-file-field case (request.File.Length == 0), verified to
        // produce byte-for-byte the same message rather than assumed.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("A non-empty .xlsx file must be uploaded.");
    }

    // Lot 065.1: an exception with no resource key (not IHasDomainErrorCode/IHasApplicationErrorCode)
    // reaching the global handler must no longer produce an opaque 500 -- the response body now
    // carries the exception's short type name and message, never a stack trace, so a caller like
    // /api-test can display something actionable instead of "check the server logs".
    [Fact]
    public async Task Process_WhenAnUnmappedExceptionOccurs_ReturnsInternalServerErrorWithExceptionTypeAndMessage()
    {
        const string exceptionMessage = "simulated unexpected failure";
        var throwingService = new Mock<IProcessOxoFileService>();
        throwingService
            .Setup(s => s.ProcessAsync(It.IsAny<ProcessOxoFileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        using var throwingFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProcessOxoFileService>();
                services.AddScoped(_ => throwingService.Object);
            });
        });
        var client = throwingFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var (importProfileId, exportProfileId) = await SeedProfilesAsync(throwingFactory);
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(nameof(InvalidOperationException));
        body.Should().Contain(exceptionMessage);
    }

    // Explicit guard-rail: this must keep failing a future refactor that starts serializing
    // exception.StackTrace onto the response, whether via a literal StackTrace property or by
    // passing the raw exception through ToString().
    [Fact]
    public async Task Process_WhenAnUnmappedExceptionOccurs_NeverIncludesTheStackTrace()
    {
        var throwingService = new Mock<IProcessOxoFileService>();
        throwingService
            .Setup(s => s.ProcessAsync(It.IsAny<ProcessOxoFileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(BuildExceptionWithRealStackTrace());

        using var throwingFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProcessOxoFileService>();
                services.AddScoped(_ => throwingService.Object);
            });
        });
        var client = throwingFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var (importProfileId, exportProfileId) = await SeedProfilesAsync(throwingFactory);
        using var sourceStream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.C7401.xlsx"));
        using var content = BuildMultipartContent(importProfileId, exportProfileId, sourceStream);

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("   at ");
        body.Should().NotContainEquivalentOf("StackTrace");
    }

    [Fact]
    public async Task Process_WithoutExportProfileIdField_StillReturnsExactlyTheLot036BadRequestBody()
    {
        // Non-regression: an exception already mapped by an explicit business check (Lot 036) keeps
        // exactly its own response shape -- no exceptionType/exceptionMessage extension, since a
        // ProblemDetails for this case never even reaches GlobalExceptionHandler (it's built and
        // returned directly by the controller).
        var client = CreateAuthenticatedClient();
        var (importProfileId, _) = await SeedProfilesAsync();
        using var sourceWorkbook = BuildRejectedSourceWorkbook();
        var fileContent = new StreamContent(sourceWorkbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(importProfileId.ToString()), "ImportProfileId" },
            { fileContent, "File", "source.xlsx" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("The ExportProfileId parameter is required.");
        body.Should().NotContain("exceptionType");
        body.Should().NotContain("exceptionMessage");
    }

    private static InvalidOperationException BuildExceptionWithRealStackTrace()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private async Task<(Guid ImportProfileId, Guid ExportProfileId)> SeedProfilesAsync(WebApplicationFactory<Program>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        var importProfileStore = scope.ServiceProvider.GetRequiredService<IImportProfileStore>();
        var exportProfileStore = scope.ServiceProvider.GetRequiredService<IExportProfileStore>();

        var importProfile = CreateImportProfile();
        var exportProfile = CreateExportProfile();
        await importProfileStore.SaveAsync(importProfile);
        await exportProfileStore.SaveAsync(exportProfile);

        return (importProfile.Id, exportProfile.Id);
    }

    private static ImportProfile CreateImportProfile() => new(
        "Profil OXO standard", "MAD-OXO-", "MAD TRAVAUX",
        ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], ["PROGRESS"],
        [
            new SheetExtractionRule(
                "PROCEDURE",
                new RepeatingBlockLocator("PROCEDURE", 9, 1, ProcedureFieldNames.Action,
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
                    new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell("PROCEDURE", "P2:Q2")),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy")
                ],
                [
                    new HeaderCompositeRule(
                        ProcedureHeaderFieldNames.Designation,
                        $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
                ]),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator("PLATINES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
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
                ], [], []),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator("ORIFICES CAPACITES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ], [], []),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("AUTRES JOINTS TOUCHES", "N6"))],
                []),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator("DIVERS", 9, 3, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("DIVERS", "N6"))],
                [])
        ]);

    private static ExportProfile CreateExportProfile() => new(
        "Profil export OXO standard",
        [
            new SheetGenerationRule(
                "Parents",
                PivotSource.Equipement,
                [
                    new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
                    new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation),
                    new ColumnDefinition("Zone", PivotFieldRef.EquipementLocalisation)
                ],
                [
                    new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet"),
                    new PointColumnDefinition("TRAVAUX DETAIL", "Travaux détail")
                ],
                []),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    new ColumnDefinition("Type", PivotFieldRef.IsolementTypeElementNom),
                    new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation)
                ],
                [
                    new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
                    new PointColumnDefinition("DEPROLOCK VANNES", "Deprolock vannes")
                ],
                [])
        ]);

    private static MemoryStream BuildRejectedSourceWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("PROCEDURE"); // M2:O2 left blank -> whole-file rejection.

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static MultipartFormDataContent BuildMultipartContent(
        Guid importProfileId, Guid exportProfileId, Stream fileStream, string fileName = "source.xlsx")
    {
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent
        {
            { new StringContent(importProfileId.ToString()), "ImportProfileId" },
            { new StringContent(exportProfileId.ToString()), "ExportProfileId" },
            { fileContent, "File", fileName }
        };
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

    public void Dispose()
    {
        if (Directory.Exists(_generatedFilesArchiveRoot))
        {
            Directory.Delete(_generatedFilesArchiveRoot, recursive: true);
        }
    }
}
