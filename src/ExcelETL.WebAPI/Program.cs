using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ExcelEtlDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_ExcelEtl")));

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

app.MapControllers();

app.Run();

public partial class Program;
