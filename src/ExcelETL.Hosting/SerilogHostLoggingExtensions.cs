using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace ExcelETL.Hosting;

/// <summary>
/// Shared Serilog composition-root wiring for every host that resolves OXO pipeline services
/// (or any other <c>ILogger&lt;T&gt;</c> consumer) -- both hosts must write to the same
/// <see cref="SystemLogsTableName"/> table so BlazorAdmin's log dashboard shows a unified view.
/// </summary>
public static class SerilogHostLoggingExtensions
{
    public const string SystemLogsTableName = "SystemLogs";

    /// <summary>
    /// Configures the console sink plus the shared <c>SystemLogs</c> SQL Server sink (gated by the
    /// <c>Serilog:EnableMsSqlServerSink</c> config switch so tests never open a real connection),
    /// enriched with an <c>Application</c> property so log entries from different hosts stay
    /// distinguishable in the shared table.
    /// </summary>
    public static IHostBuilder AddOxoHostLogging(
        this IHostBuilder hostBuilder, string applicationName, string connectionString)
        => hostBuilder.UseSerilog((context, loggerConfiguration) =>
            Configure(loggerConfiguration, applicationName, connectionString, context.Configuration));

    public static void Configure(
        LoggerConfiguration loggerConfiguration,
        string applicationName,
        string connectionString,
        IConfiguration configuration)
    {
        var enableMsSqlServerLogSink = configuration.GetValue("Serilog:EnableMsSqlServerSink", defaultValue: true);

        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .WriteTo.Console();

        if (enableMsSqlServerLogSink)
        {
            loggerConfiguration.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions { TableName = SystemLogsTableName, AutoCreateSqlTable = true },
                columnOptions: BuildSystemLogsColumnOptions());
        }
    }

    /// <summary>
    /// Column mapping for the shared <c>SystemLogs</c> table -- extracted from <see cref="Configure"/>
    /// so <c>TimeStamp.ConvertToUtc</c> (Lot 064: the sink otherwise writes the log-issuing host's own
    /// local wall-clock time, indistinguishable in the column from UTC, which made the Logs page
    /// unreadable for a client in a different time zone than the production server) is directly
    /// assertable by a test without opening a real SQL Server connection.
    /// </summary>
    public static ColumnOptions BuildSystemLogsColumnOptions()
    {
        var columnOptions = new ColumnOptions();
        columnOptions.TimeStamp.ConvertToUtc = true;
        return columnOptions;
    }
}
