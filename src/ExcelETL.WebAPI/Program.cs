using ExcelETL.Application.Extraction;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Storage;
using ExcelETL.WebAPI;
using ExcelETL.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

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
        .Enrich.WithProperty("Application", "ExcelETL.WebAPI")
        .WriteTo.Console();

    if (enableMsSqlServerLogSink)
    {
        loggerConfiguration.WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions { TableName = "SystemLogs", AutoCreateSqlTable = true });
    }
});

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

builder.Services.AddScoped<IExcelExtractionService, ClosedXmlExtractionService>();
builder.Services.AddScoped<IExcelGeneratorService, ClosedXmlGeneratorService>();
builder.Services.AddScoped<IExtractionConfigRepository, ExtractionConfigRepository>();
builder.Services.AddScoped<IExtractionHistoryRepository, ExtractionHistoryRepository>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IProcessExcelFileService, ProcessExcelFileService>();

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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();

app.MapControllers();

app.Run();

public partial class Program;
