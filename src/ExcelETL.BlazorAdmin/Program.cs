using ExcelETL.Application.Diagnostics;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components;
using ExcelETL.BlazorAdmin.Components.Account;
using ExcelETL.BlazorAdmin.ExternalApi;
using ExcelETL.Hosting;
using ExcelETL.Infrastructure.Diagnostics;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Both hosts (WebAPI and BlazorAdmin) write to the same SystemLogs table so the dashboard
// below can show a unified view; the Application property distinguishes which process
// emitted a given entry. Serilog owns and auto-creates this table's schema -- it is
// intentionally outside the EF Core Code-First migrations used for the domain database.
// The sink/enrichment setup itself lives in ExcelETL.Hosting (see AddOxoHostLogging) so it is
// defined exactly once for every host, not re-typed per Program.cs -- see CLAUDE.md, "Lot G3".
builder.Host.AddOxoHostLogging("ExcelETL.BlazorAdmin", connectionString);

// Read-only access to the SystemLogs table for the /dashboard page. This context carries no
// migrations of its own -- see the UseSerilog configuration above for why Serilog owns the
// physical schema of this table.
builder.Services.AddDbContextFactory<SystemLogsDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ISystemLogRepository, SystemLogRepository>();

// AddEntityFrameworkStores<ApplicationIdentityDbContext>() below needs a directly-injectable
// scoped ApplicationIdentityDbContext, so that registration is kept. IUserRepository is the only
// consumer of the factory below -- unlike the Singleton factories elsewhere in this file, this one
// must be registered Scoped: AddDbContextFactory's default Singleton lifetime would register a
// singleton DbContextOptions<ApplicationIdentityDbContext>, which then can't resolve the Scoped
// options configuration also added by AddDbContext above (fails at first use with "Cannot resolve
// scoped service ... from root provider"). A Scoped factory still creates a fresh, short-lived
// DbContext on every CreateDbContextAsync() call -- the safety property Interactive Server
// components need -- it's only the factory *instance* that is now one-per-circuit instead of
// shared app-wide.
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity")));
builder.Services.AddDbContextFactory<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity")),
    lifetime: ServiceLifetime.Scoped);
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Interactive Server components share a circuit across multiple renders, so a directly
// injected scoped DbContext would be used concurrently/beyond its intended lifetime. This
// factory registration is consumed exclusively by the repositories below -- Razor components
// and endpoints in this app talk to ExtractionConfig/ExtractionHistory only through
// IExtractionConfigRepository/IExtractionHistoryRepository, never through EF Core directly.
builder.Services.AddDbContextFactory<ExcelEtlDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_ExcelEtl")));

builder.Services.AddScoped<IExtractionConfigRepository, ExtractionConfigRepository>();
builder.Services.AddScoped<IExtractionHistoryRepository, ExtractionHistoryRepository>();
builder.Services.AddScoped<IImportProfileStore, EfImportProfileStore>();
builder.Services.AddSingleton<BusinessExceptionLocalizer>();

// The OXO extraction pipeline (Lot A-D), wired here so the /import-profiles/test admin page can run
// it in-process against an uploaded file -- no host has needed these registrations until now, since
// WebAPI still only exposes the older ExtractionConfig pipeline. All stateless, so Singleton.
builder.Services.AddSingleton<ITextTransformEvaluator, TextTransformEvaluator>();
builder.Services.AddSingleton<IConditionalPointRuleEvaluator, ConditionalPointRuleEvaluator>();
builder.Services.AddSingleton<IRepeatingBlockReader, RepeatingBlockReader>();
builder.Services.AddSingleton<IProcedureExtractionService, ProcedureExtractionService>();
builder.Services.AddSingleton<IIsolementExtractionService, IsolementExtractionService>();
builder.Services.AddSingleton<IUnconditionalIsolementSheetExtractionService, UnconditionalIsolementSheetExtractionService>();
builder.Services.AddSingleton<IAutresJointsTouchesExtractionService, AutresJointsTouchesExtractionService>();
builder.Services.AddSingleton<IDiversExtractionService, DiversExtractionService>();
builder.Services.AddSingleton<IImportPipelineOrchestrator, ImportPipelineOrchestrator>();

// Lot J: the target-workbook generation pipeline (Lot I), wired here so /export-profiles/test can
// run it in process. IExportProfileStore is Scoped to match IImportProfileStore's lifetime; the
// generation engine and writer are stateless, so Singleton, matching the OXO pipeline services above.
builder.Services.AddScoped<IExportProfileStore, EfExportProfileStore>();
builder.Services.AddSingleton<ISheetGenerationEngine, SheetGenerationEngine>();
builder.Services.AddSingleton<IWorkbookWriter, ClosedXmlWorkbookWriter>();

// Deliberate, narrow exception to the "never talk to the Web API over HTTP" Clean Architecture
// rule above -- see ExcelProcessingClient for why. Used only by the /upload-test admin page.
builder.Services.Configure<WebApiClientOptions>(builder.Configuration.GetSection(WebApiClientOptions.SectionName));
builder.Services.AddHttpClient<ExcelProcessingClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<WebApiClientOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        throw new InvalidOperationException(
            $"Configuration value '{WebApiClientOptions.SectionName}:{nameof(WebApiClientOptions.BaseUrl)}' is required.");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            $"Configuration value '{WebApiClientOptions.SectionName}:{nameof(WebApiClientOptions.ApiKey)}' is required.");
    }

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = ExcelProcessingClient.DefaultTimeout;
    client.DefaultRequestHeaders.Add(ExcelProcessingClient.ApiKeyHeaderName, options.ApiKey);
});
builder.Services.AddScoped<IExcelDownloadInterop, ExcelDownloadInterop>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IdentitySeeder>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "fr-FR" };
    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    // Default provider order (query string, then this cookie, then Accept-Language) is kept as-is:
    // an admin's chosen language should stick across visits without depending on browser settings.
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();
app.MapAdminEndpoints();
app.MapCultureEndpoints();

// Applies any pending EF Core migrations for both databases this host owns, before identity
// seeding below (which needs the Identity schema to already exist). See
// DatabaseMigrationHostExtensions for the Database:AutoMigrate/IsRelational() gating and why
// it's safe for both hosts (WebAPI/BlazorAdmin) to do this independently. The
// HistoryDownloadEndpointTests integration test sets Database:AutoMigrate=false explicitly,
// since it never swaps ApplicationIdentityDbContext to the InMemory provider (only
// ExcelEtlDbContext) and would otherwise require a real, reachable SQL Server just to start.
await app.Services.MigrateIfEnabledAsync<ExcelEtlDbContext>(app.Configuration);
await app.Services.MigrateIfEnabledAsync<ApplicationIdentityDbContext>(app.Configuration);

// Ensures the fixed set of administrator accounts this deployment relies on exists on every
// startup (local or a fresh server) -- idempotent, so restarts and redeploys are safe. See
// IdentitySeeder for why passwords are never read from a committed configuration file.
//
// WebApplicationFactory-based integration tests spin up this Program against the real Identity
// database (only ExcelEtlDbContext is swapped for an in-memory one in those tests), and xUnit
// runs test classes in parallel -- multiple hosts seeding concurrently race on the same rows.
// Tests disable seeding via this switch rather than each one stubbing IdentitySeeder out.
var enableIdentitySeeding = builder.Configuration.GetValue("IdentitySeeding:Enabled", defaultValue: true);
if (enableIdentitySeeding)
{
    using var scope = app.Services.CreateScope();
    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();
}

app.Run();

public partial class Program;
