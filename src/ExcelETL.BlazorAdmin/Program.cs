using ExcelETL.Application.Diagnostics;
using ExcelETL.Application.Extraction;
using ExcelETL.BlazorAdmin.Components;
using ExcelETL.BlazorAdmin.Components.Account;
using ExcelETL.Infrastructure.Diagnostics;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Both hosts (WebAPI and BlazorAdmin) write to the same SystemLogs table so the dashboard
// below can show a unified view; the Application property distinguishes which process
// emitted a given entry. Serilog owns and auto-creates this table's schema -- it is
// intentionally outside the EF Core Code-First migrations used for the domain database.
//
// AutoCreateSqlTable makes the sink open a real connection during host startup, which would
// otherwise break WebApplicationFactory-based integration tests that never point at a real SQL
// Server. Tests disable the sink via this switch instead of stubbing out Serilog entirely.
var enableMsSqlServerLogSink = builder.Configuration.GetValue("Serilog:EnableMsSqlServerSink", defaultValue: true);

builder.Host.UseSerilog((_, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ExcelETL.BlazorAdmin")
        .WriteTo.Console();

    if (enableMsSqlServerLogSink)
    {
        loggerConfiguration.WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions { TableName = "SystemLogs", AutoCreateSqlTable = true });
    }
});

// Read-only access to the SystemLogs table for the /dashboard page. This context carries no
// migrations of its own -- see the UseSerilog configuration above for why Serilog owns the
// physical schema of this table.
builder.Services.AddDbContextFactory<SystemLogsDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ISystemLogRepository, SystemLogRepository>();

builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity")));

// Interactive Server components share a circuit across multiple renders, so a directly
// injected scoped DbContext would be used concurrently/beyond its intended lifetime. This
// factory registration is consumed exclusively by the repositories below -- Razor components
// and endpoints in this app talk to ExtractionConfig/ExtractionHistory only through
// IExtractionConfigRepository/IExtractionHistoryRepository, never through EF Core directly.
builder.Services.AddDbContextFactory<ExcelEtlDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_ExcelEtl")));

builder.Services.AddScoped<IExtractionConfigRepository, ExtractionConfigRepository>();
builder.Services.AddScoped<IExtractionHistoryRepository, ExtractionHistoryRepository>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();
app.MapAdminEndpoints();
app.MapCultureEndpoints();

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
