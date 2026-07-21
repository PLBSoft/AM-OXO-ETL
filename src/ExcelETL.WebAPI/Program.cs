using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.Hosting;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Storage;
using ExcelETL.WebAPI;
using ExcelETL.WebAPI.Authentication;
using ExcelETL.WebAPI.ExceptionHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Excel workbooks can be large and processing is synchronous, so the default Kestrel body-size
// limit and slow-connection kill-switches would drop legitimate uploads/downloads mid-transfer.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = UploadLimits.MaxExcelFileSizeBytes;
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy(UploadLimits.ExcelProcessingTimeoutPolicy, UploadLimits.ExcelProcessingTimeout);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Both hosts (WebAPI and BlazorAdmin) write to the same SystemLogs table so the BlazorAdmin
// dashboard can show a unified view; the Application property distinguishes which process
// emitted a given entry. Serilog owns and auto-creates this table's schema -- it is
// intentionally outside the EF Core Code-First migrations used for the domain database.
// The sink/enrichment setup itself lives in ExcelETL.Hosting (see AddOxoHostLogging) so it is
// defined exactly once for every host, not re-typed per Program.cs -- see CLAUDE.md, "Lot G3".
builder.Host.AddOxoHostLogging("ExcelETL.WebAPI", connectionString);

// Registered as a factory (not AddDbContext) so the repositories in Infrastructure can use
// the same short-lived-context-per-operation pattern regardless of host (WebAPI or BlazorAdmin).
builder.Services.AddDbContextFactory<ExcelEtlDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_ExcelEtl")));

builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));

var apiKeySection = builder.Configuration.GetSection("ApiKeyAuthentication");
if (string.IsNullOrWhiteSpace(apiKeySection["ApiKey"]))
{
    throw new InvalidOperationException("Configuration value 'ApiKeyAuthentication:ApiKey' must be set.");
}

builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.SchemeName, _ => { });
builder.Services.Configure<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationDefaults.SchemeName, apiKeySection);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder(ApiKeyAuthenticationDefaults.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// OXO pipeline (Lot K1/K2) -- since Lot K4's removal of the old ExtractionConfig/ProcessExcelFile
// pipeline, the only pipeline this host exposes. Same registrations as
// ExcelETL.BlazorAdmin/Program.cs. All stateless -- Singleton, matching BlazorAdmin's lifetimes --
// except the two profile stores, which are Scoped like every other repository here (short-lived
// DbContext per operation via IDbContextFactory).
builder.Services.AddScoped<IImportProfileStore, EfImportProfileStore>();
builder.Services.AddScoped<IExportProfileStore, EfExportProfileStore>();
builder.Services.AddSingleton<ITextTransformEvaluator, TextTransformEvaluator>();
builder.Services.AddSingleton<IConditionalPointRuleEvaluator, ConditionalPointRuleEvaluator>();
builder.Services.AddSingleton<IRepeatingBlockReader, RepeatingBlockReader>();
builder.Services.AddSingleton<IProcedureExtractionService, ProcedureExtractionService>();
builder.Services.AddSingleton<IIsolementExtractionService, IsolementExtractionService>();
builder.Services.AddSingleton<IUnconditionalIsolementSheetExtractionService, UnconditionalIsolementSheetExtractionService>();
builder.Services.AddSingleton<IAutresJointsTouchesExtractionService, AutresJointsTouchesExtractionService>();
builder.Services.AddSingleton<IDiversExtractionService, DiversExtractionService>();
builder.Services.AddSingleton<IImportPipelineOrchestrator, ImportPipelineOrchestrator>();
builder.Services.AddSingleton<ISheetGenerationEngine, SheetGenerationEngine>();
builder.Services.AddSingleton<IWorkbookWriter, ClosedXmlWorkbookWriter>();
builder.Services.AddScoped<IProcessOxoFileService, ProcessOxoFileService>();
// Singleton: GlobalExceptionHandler is registered as a singleton by AddExceptionHandler<T>(), and
// this has no state of its own beyond the two singleton IStringLocalizer<T> it wraps.
builder.Services.AddSingleton<BusinessExceptionLocalizer>();

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "fr-FR" };
    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    // This is an M2M endpoint with no browser session, so cookie/query-string culture negotiation
    // (the ASP.NET Core default providers) would be dead weight. The legacy client is the only
    // caller and negotiates culture the same way any HTTP client would: the Accept-Language header.
    options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// UseRequestLocalization runs before UseExceptionHandler so the resolved culture is set in the
// execution context ExceptionHandlerMiddleware captures at pipeline start. ExceptionHandler
// restores that captured context around its handlers when an exception unwinds a request, so if
// it ran first, the culture set later by RequestLocalization would be invisible to
// GlobalExceptionHandler and every localized error would fall back to the process's default
// (OS) culture instead of the request's negotiated one.
app.UseRequestLocalization();
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();

app.MapControllers();

// Applies any pending EF Core migrations for ExcelEtlDbContext on every startup -- see
// DatabaseMigrationHostExtensions for the Database:AutoMigrate/IsRelational() gating and why
// it's safe for both hosts to do this independently. WebApplicationFactory-based integration
// tests for this host set Database:AutoMigrate=false explicitly (see HealthPingTests,
// ApiKeyAuthenticationTests, OxoProcessEndpointTests) since some of them never swap
// ExcelEtlDbContext to the InMemory provider and would otherwise require a real, reachable
// SQL Server just to start the host.
await app.Services.MigrateIfEnabledAsync<ExcelEtlDbContext>(app.Configuration);

app.Run();

public partial class Program;
