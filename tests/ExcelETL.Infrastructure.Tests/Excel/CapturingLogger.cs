using Microsoft.Extensions.Logging;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Hand-rolled capturing ILogger<T>, in the same spirit as the repo's existing FakeHttpMessageHandler
// (see CLAUDE.md's "Test conventions in practice" -- avoids a new test-only dependency for equivalent
// capability, since Microsoft.Extensions.Logging.Testing/Diagnostics.Testing aren't referenced
// anywhere in this repo). Distinct from a Mock<ILogger<T>> + Verify: this captures the real formatted
// output of a full pipeline run for black-box assertions, not per-call-site verification of individual
// LogXxx invocations (see CLAUDE.md's Lot G1 note on why Mock<ILogger> was rejected as a convention).
internal sealed record CapturedLogEntry(LogLevel Level, string Message, Exception? Exception);

internal sealed class CapturedLogEntries
{
    public List<CapturedLogEntry> Entries { get; } = [];
}

internal sealed class CapturingLogger<T>(CapturedLogEntries sink) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        sink.Entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), exception));
    }
}
