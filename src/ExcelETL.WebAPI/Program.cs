using ExcelETL.Application.Extraction;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Storage;
using ExcelETL.WebAPI;
using ExcelETL.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
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

builder.Services.AddDbContext<ExcelEtlDbContext>(options =>
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
