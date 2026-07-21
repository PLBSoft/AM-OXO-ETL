using Microsoft.Extensions.Logging;

namespace ExcelETL.WebAPI.Tests.Oxo;

// Duplicates ExcelETL.Infrastructure.Tests.Excel.CapturingLogger (Lot G2) rather than sharing it --
// it's `internal` there, and this repo's established convention is to duplicate small test-only
// helpers per project rather than introduce a shared test-helper assembly (see CLAUDE.md's
// "no-shared-test-helper convention").
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
