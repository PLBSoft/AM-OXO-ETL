using ExcelETL.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ExcelETL.Hosting.Tests;

// Proves the two hosts (WebAPI/BlazorAdmin) get byte-identical Serilog behavior from
// SerilogHostLoggingExtensions.Configure -- the only thing that differs between call sites is
// the applicationName/connectionString arguments, never a re-typed copy of the sink/enrichment
// setup itself. Every test disables the MSSqlServer sink via the same config switch the two
// Program.cs files already used, so nothing here opens a real SQL Server connection.
public sealed class SerilogHostLoggingExtensionsTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static IConfiguration BuildConfiguration(bool enableMsSqlServerLogSink) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:EnableMsSqlServerSink"] = enableMsSqlServerLogSink.ToString(),
            })
            .Build();

    [Theory]
    [InlineData("ExcelETL.WebAPI")]
    [InlineData("ExcelETL.BlazorAdmin")]
    public void Configure_EnrichesEveryLogEventWithTheGivenApplicationName(string applicationName)
    {
        var sink = new CapturingSink();
        var loggerConfiguration = new LoggerConfiguration();

        SerilogHostLoggingExtensions.Configure(
            loggerConfiguration, applicationName, connectionString: "unused", BuildConfiguration(enableMsSqlServerLogSink: false));
        loggerConfiguration.WriteTo.Sink(sink);

        using var logger = loggerConfiguration.CreateLogger();
        logger.Information("test event");

        sink.Events.Should().ContainSingle();
        sink.Events[0].Properties.Should().ContainKey("Application");
        sink.Events[0].Properties["Application"].ToString().Should().Contain(applicationName);
    }

    [Theory]
    [InlineData("ExcelETL.WebAPI")]
    [InlineData("ExcelETL.BlazorAdmin")]
    public void Configure_OverridesMicrosoftAspNetCoreMinimumLevelToWarning_ForBothHosts(string applicationName)
    {
        var sink = new CapturingSink();
        var loggerConfiguration = new LoggerConfiguration();

        SerilogHostLoggingExtensions.Configure(
            loggerConfiguration, applicationName, connectionString: "unused", BuildConfiguration(enableMsSqlServerLogSink: false));
        loggerConfiguration.WriteTo.Sink(sink);

        using var logger = loggerConfiguration.CreateLogger();
        var aspNetCoreLogger = logger.ForContext("SourceContext", "Microsoft.AspNetCore.Routing.EndpointMiddleware");
        aspNetCoreLogger.Information("filtered out below the override");
        aspNetCoreLogger.Warning("kept at warning");

        sink.Events.Should().ContainSingle(e => e.RenderMessage() == "kept at warning");
    }

    [Theory]
    [InlineData("ExcelETL.WebAPI")]
    [InlineData("ExcelETL.BlazorAdmin")]
    public void Configure_DoesNotOpenARealSqlConnection_WhenTheMsSqlServerSinkIsDisabled(string applicationName)
    {
        var loggerConfiguration = new LoggerConfiguration();

        var act = () =>
        {
            SerilogHostLoggingExtensions.Configure(
                loggerConfiguration,
                applicationName,
                connectionString: "Server=unreachable-host;Database=x;",
                BuildConfiguration(enableMsSqlServerLogSink: false));
            using var logger = loggerConfiguration.CreateLogger();
            logger.Information("no sql attempted");
        };

        act.Should().NotThrow();
    }

    // Lot 064: the default Serilog/MSSqlServer sink behavior writes the log-issuing host's own
    // local wall-clock time into the TimeStamp column (no offset, indistinguishable from UTC) --
    // this is what made the Logs page unreadable for a client in a different time zone than the
    // production server. `TimeStamp.ConvertToUtc` is the sink's own documented fix, but it only
    // takes effect inside the SQL writer at write time, not on the shared LogEvent object visible
    // to other sinks -- so this test asserts the built ColumnOptions directly (the actual
    // conversion behavior is the sink library's own responsibility, not re-tested here), same
    // "assert the declared configuration, not real SQL Server behavior" convention already used
    // for the EF Core model in ApplicationIdentityDbContextModelTests.
    [Fact]
    public void BuildSystemLogsColumnOptions_ConvertsTheTimeStampColumnToUtc()
    {
        var columnOptions = SerilogHostLoggingExtensions.BuildSystemLogsColumnOptions();

        columnOptions.TimeStamp.ConvertToUtc.Should().BeTrue();
    }
}
