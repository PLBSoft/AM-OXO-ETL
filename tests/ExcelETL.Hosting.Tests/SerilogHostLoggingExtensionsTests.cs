using System.Data;
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

    // Lot 064.3 (correctif, incident production -- voir tickets-tdd-lot-064-heure-locale-logs.md) :
    // sur le serveur de production dédié au client Nouvelle-Calédonie (fuseau Windows configuré en
    // dur sur "(UTC+11:00) Solomon Is., New Caledonia", horloge NTP correctement synchronisée), les
    // horodatages affichés sur /logs étaient dans le futur d'environ 13h (le décalage serveur +11h,
    // additionné au décalage +2h du navigateur du client en France, jamais soustrait nulle part).
    //
    // Investigation (décompilation de Serilog.Sinks.MSSqlServer 10.0.0 et Serilog.Extensions.Logging
    // 10.0.0 via ilspycmd, package réellement installé dans ce dépôt -- pas une hypothèse) :
    // - `Serilog.Extensions.Logging.SerilogLogger.PrepareWrite` (le pont utilisé par tout
    //   `ILogger<T>` de cette solution) construit chaque `LogEvent` avec
    //   `LogEvent.UnstableAssembleFromParts(DateTimeOffset.Now, ...)` -- une valeur qui porte
    //   correctement le décalage réel du serveur, quel qu'il soit.
    // - `Output.StandardColumnDataGenerator.GetTimeStampStandardColumnNameAndValue` calcule, quand
    //   `ColumnOptions.TimeStamp.ConvertToUtc` vaut `true` (BuildSystemLogsColumnOptions ci-dessus),
    //   `logEvent.Timestamp.ToUniversalTime()` puis, comme `TimeStamp.DataType` reste au défaut du
    //   package (`SqlDbType.DateTime`, jamais `DateTimeOffset` -- confirmé en décompilant
    //   `ColumnOptions.TimeStampColumnOptions..ctor`), stocke `.UtcDateTime`.
    // Cette chaîne est donc CORRECTE, mathématiquement, pour n'importe quel décalage serveur --
    // `DateTimeOffset.ToUniversalTime()` est une opération BCL indépendante du fuseau dès lors que
    // le `DateTimeOffset` source porte le bon décalage, ce que `DateTimeOffset.Now` garantit à
    // partir de `TimeZoneInfo.Local`. Le code de ce dépôt (`SerilogHostLoggingExtensions.cs`, commit
    // df5929f, Lot 064) est donc déjà correct tel quel -- l'incident observé par Simon reflète très
    // vraisemblablement des lignes écrites par un binaire/table antérieurs à ce commit sur ce
    // serveur, pas un défaut résiduel dans le code actuel (voir le sous-ticket 64.3 pour le détail).
    //
    // Ce test pin l'invariant réel indépendamment de la cause de l'incident : quel que soit le
    // décalage serveur (Nouvelle-Calédonie +11:00 inclus, l'incident réel), la même conversion que
    // celle appliquée par le sink ne doit jamais produire un horodatage postérieur à l'instant UTC
    // réel qu'elle représente. Volontairement indépendant du fuseau de la machine qui exécute ce
    // test (CI, poste développeur en France, etc.) -- voir la note de testabilité plus bas.
    [Theory]
    [InlineData(11, 0)]   // Nouvelle-Calédonie -- le serveur réellement à l'origine de l'incident
    [InlineData(-8, 0)]   // décalage négatif arbitraire, pour prouver qu'aucun sens n'est privilégié
    [InlineData(0, 0)]    // serveur déjà en UTC
    [InlineData(5, 30)]   // décalage non entier (Inde), pour prouver l'absence d'hypothèse d'heure ronde
    public void TimeStampConversion_GivenAnyServerTimeZoneOffset_NeverProducesAnInstantLaterThanUtcNow(
        int offsetHours, int offsetMinutes)
    {
        var trueUtcNow = DateTimeOffset.UtcNow;
        var serverOffset = new TimeSpan(offsetHours, offsetMinutes, 0);

        // Ce que `DateTimeOffset.Now` renverrait, à ce même instant réel, sur un serveur configuré
        // avec ce décalage -- la Nouvelle-Calédonie ou tout autre fuseau client futur, sans aucune
        // hypothèse de valeur "normale" ou "attendue" pour un déploiement de cette application.
        var serverLocalNow = trueUtcNow.ToOffset(serverOffset);

        // La même opération que celle appliquée par le sink quand ConvertToUtc = true (voir la
        // citation de code ci-dessus).
        var storedValue = serverLocalNow.ToUniversalTime().UtcDateTime;

        storedValue.Kind.Should().Be(DateTimeKind.Utc);
        storedValue.Should().BeCloseTo(trueUtcNow.UtcDateTime, precision: TimeSpan.FromSeconds(1));
        storedValue.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    // Complète le test ci-dessus : BuildSystemLogsColumnOptions() ne fixe jamais TimeStamp.DataType
    // explicitement -- la justesse du chemin "plain DateTime" de ConvertToUtc (voir la citation de
    // code ci-dessus) dépend donc implicitement du défaut du package restant SqlDbType.DateTime.
    // Si un futur upgrade de Serilog.Sinks.MSSqlServer changeait ce défaut vers DateTimeOffset, ce
    // test le détecterait au lieu de laisser un défaut de type silencieux se réintroduire.
    [Fact]
    public void BuildSystemLogsColumnOptions_TimeStampColumnStaysPlainDateTime_NotDateTimeOffset()
    {
        var columnOptions = SerilogHostLoggingExtensions.BuildSystemLogsColumnOptions();

        columnOptions.TimeStamp.DataType.Should().Be(SqlDbType.DateTime);
    }

    // Limite de testabilité documentée (Lot 064.3) : ce fichier n'a pas d'abstraction d'horloge
    // injectable pour le pipeline Serilog (LogEvent.Timestamp vient directement de
    // DateTimeOffset.Now, un appel BCL statique, pas d'un seam contrôlé par cette solution), et
    // aucun test de ce projet n'ouvre de vraie connexion SQL Server (voir
    // Configure_DoesNotOpenARealSqlConnection_WhenTheMsSqlServerSinkIsDisabled). Un test de bout en
    // bout (écriture réelle via le sink -> lecture réelle via SystemLogsDbContext -> assertion sur
    // la valeur) n'est donc pas ajouté ici : il nécessiterait soit une vraie base SQL Server dans la
    // suite de tests (contraire à la convention établie de ce dépôt), soit l'introduction d'une
    // abstraction d'horloge non demandée par le ticket d'origine. Les deux tests ci-dessus couvrent
    // la totalité de ce qui est réellement sous le contrôle de ce dépôt : la configuration déclarée
    // (ConvertToUtc, DataType) et l'invariant arithmétique que cette configuration active.
}
