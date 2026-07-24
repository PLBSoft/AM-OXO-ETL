using System.Net;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
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
using Xunit;
using IsolementFieldNames = ExcelETL.Application.Extraction.Oxo.Isolement.IsolementFieldNames;
using ProcedureFieldNames = ExcelETL.Application.Extraction.Oxo.Procedure.ProcedureFieldNames;

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

    private readonly string _archiveDirectory = Path.Combine(Path.GetTempPath(), "OxoProcessEndpointTests_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public OxoProcessEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "OxoProcessTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("FileStorage:ArchiveDirectory", _archiveDirectory);
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

        Directory.Exists(_archiveDirectory).Should().BeTrue();
        Directory.GetFiles(_archiveDirectory, "*.xlsx").Should().ContainSingle();
    }

    [Fact]
    public async Task Process_WithD8570Fixture_KeepsUnrecognizedTypeElementIsolementAsNormalRow()
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
                []),
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
                ["PROLOCK VANNES", "DEPROLOCK VANNES"]),
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
                ]),
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
                ]),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"]),
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
        if (Directory.Exists(_archiveDirectory))
        {
            Directory.Delete(_archiveDirectory, recursive: true);
        }
    }
}
