using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Configuration;

// Lot 029: guards against either appsettings file drifting back to the retired ExcelEtl
// database name, or being forgotten when the connection string is renamed again in the future.
public class ConnectionStringConfigurationTests
{
    private const string ExpectedDatabaseName = "AM-OXO-ETL-MAD-REL";
    private const string RetiredDatabaseName = "ExcelEtl";

    [Fact]
    public void AppSettingsJson_DefaultConnection_UsesRenamedDatabase()
    {
        var connectionString = LoadDefaultConnection("appsettings.json");

        connectionString.Should().Contain(ExpectedDatabaseName);
        connectionString.Should().NotContain(RetiredDatabaseName);
    }

    [Fact]
    public void AppSettingsDevelopmentJson_DefaultConnection_UsesRenamedDatabase()
    {
        var connectionString = LoadDefaultConnection("appsettings.Development.json");

        connectionString.Should().Contain(ExpectedDatabaseName);
        connectionString.Should().NotContain(RetiredDatabaseName);
    }

    private static string LoadDefaultConnection(string fileName)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(BlazorAdminSettingsPath(fileName), optional: false)
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException($"No DefaultConnection found in {fileName}.");
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
