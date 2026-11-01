namespace ExcelETL.Infrastructure.Plans;

// A plan sheet could not be printed to PDF (LibreOffice missing, failed, too slow, or produced nothing).
// Developer/operator-facing only: it is logged, never shown to a user (lot 091, D11).
public sealed class PlanRenderingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
