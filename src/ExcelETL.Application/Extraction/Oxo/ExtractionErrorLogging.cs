using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Every per-sheet extraction service logs its own ExtractionErrors through this one mapping so the
// Warning/Error split stays consistent instead of being duplicated per service. NoConditionalPointCreated
// (model doc §3.2, e.g. ISOLEMENT's "VANNE" case), TacheMultipleTypeMismatch (Lot 032,
// decision 8: non-blocking), and UnexpectedZeroEnergieValue (Lot 063: the isolement is still extracted
// normally, HasZeroEnergie just falls back to false) are the three codes explicitly non-blocking ->
// Warning; RequiredFieldMissing/UnparsableValue mean a block (or, for PROCEDURE, the whole file) was
// skipped/rejected -> Error.
internal static class ExtractionErrorLogging
{
    public static void Log(ILogger logger, ExtractionError error)
    {
        var level = error.Code is ExtractionErrorCode.NoConditionalPointCreated
            or ExtractionErrorCode.TacheMultipleTypeMismatch
            or ExtractionErrorCode.UnexpectedZeroEnergieValue
            ? LogLevel.Warning
            : LogLevel.Error;
        logger.Log(
            level,
            "Extraction {Code} on sheet {Sheet}, block {BlockIdentifier}: {Message}",
            error.Code, error.Sheet, error.BlockIdentifier, error.Message);
    }
}
