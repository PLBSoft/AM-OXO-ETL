using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Builds and deduplicates UnexpectedZeroEnergieValue warnings for one ISOLEMENT sheet within a single
// Extract() call -- Lot 063 §63.3/§63.4, mirroring NoConditionalPointCreatedWarningTracker's own shape
// (Lot 055) rather than inventing a parallel mechanism. Fires when the dedicated "zero energie" cell
// (column V) holds a non-blank value that matches neither blank nor the profile's configured
// SheetExtractionRule.ZeroEnergieExpectedValue -- the isolement is still extracted normally
// (HasZeroEnergie simply falls back to false), this warning is purely informational. Deduplicated by
// Trim+OrdinalIgnoreCase-normalized value, same convention as ConditionalPointRuleEvaluator's own
// comparison and as the Lot 055 tracker. One instance created fresh per Extract() call -- never
// static/shared.
public sealed class UnexpectedZeroEnergieValueWarningTracker(string sheet)
{
    private readonly HashSet<string> _reportedNormalizedValues = new(StringComparer.OrdinalIgnoreCase);

    public void RecordIfNew(string blockIdentifier, string extractedValue, ILogger logger, List<ExtractionError> errors)
    {
        var normalizedValue = extractedValue.Trim();
        if (!_reportedNormalizedValues.Add(normalizedValue))
        {
            return;
        }

        var error = new ExtractionError(
            sheet, blockIdentifier, ExtractionErrorCode.UnexpectedZeroEnergieValue, BuildMessage(extractedValue), extractedValue);
        ExtractionErrorLogging.Log(logger, error);
        errors.Add(error);
    }

    private static string BuildMessage(string extractedValue) =>
        $"La cellule « zéro énergie » contient une valeur inattendue « {extractedValue} » : ni vide, ni la " +
        "valeur configurée sur le profil d'import. L'isolement est extrait normalement, sans le point conditionnel associé.";
}
