using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Configuration;

// Lot 038 (38.1): appsettings.Development.json must carry real BaseUrl/ApiKey values so a locally
// run BlazorAdmin can call a locally run ExcelETL.WebAPI (same dev API key value as that project's
// own appsettings.Development.json -- see ApiKeyAuthentication:ApiKey there). appsettings.json
// (production defaults) deliberately carries no "OxoApiTestClient" section at all -- same
// fail-fast-if-unset convention as ApiKeyAuthentication:ApiKey in ExcelETL.WebAPI, the real
// production value is expected via user secrets/env var at deployment time, never committed here.
public class OxoApiTestClientConfigurationTests
{
    [Fact]
    public void AppSettingsDevelopmentJson_OxoApiTestClient_HasNonEmptyBaseUrlAndApiKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(BlazorAdminSettingsPath("appsettings.Development.json"), optional: false)
            .Build();

        var section = configuration.GetSection("OxoApiTestClient");
        section["BaseUrl"].Should().NotBeNullOrWhiteSpace();
        section["ApiKey"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AppSettingsJson_HasNoOxoApiTestClientSection_ProductionValueComesFromSecretsOrEnv()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(BlazorAdminSettingsPath("appsettings.json"), optional: false)
            .Build();

        configuration.GetSection("OxoApiTestClient")["BaseUrl"].Should().BeNullOrEmpty();
        configuration.GetSection("OxoApiTestClient")["ApiKey"].Should().BeNullOrEmpty();
    }

    private static string BlazorAdminSettingsPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
        }

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", fileName);
    }
}
