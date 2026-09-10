namespace ExcelETL.WebAPI.Correlation;

public static class CorrelationIdDefaults
{
    public const string HeaderName = "X-Correlation-Id";

    // Serilog property name pushed via LogContext -- every log line emitted while handling a
    // request carries this, regardless of which class/layer emits it (LogContext is ambient,
    // AsyncLocal-based).
    public const string LogPropertyName = "CorrelationId";
}
