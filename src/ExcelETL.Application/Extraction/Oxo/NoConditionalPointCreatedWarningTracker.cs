using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Builds and deduplicates NoConditionalPointCreated warnings for one sheet within a single Extract()
// call -- Lot 055 §55.3/§55.4/§55.5. The emission decision itself (per element, not per rule) is
// already made by ConditionalPointGroupEvaluator; this class owns everything downstream of that
// decision: the French, referential-agnostic user-facing message (no Colonne name -- §55.6), the
// structured ExtractedValue field (§55.3), and deduplication by Trim+OrdinalIgnoreCase-normalized
// value, keeping the first raw form encountered for display (§55.5). One instance is created fresh
// per Extract() call by each of Isolement/Divers/AutresJointsTouches's own extraction service --
// never static/shared, so a value seen on one sheet or one file never suppresses the same value on a
// different sheet or a later run.
public sealed class NoConditionalPointCreatedWarningTracker(string sheet)
{
    private readonly HashSet<string> _reportedNormalizedValues = new(StringComparer.OrdinalIgnoreCase);

    public void RecordIfNew(string blockIdentifier, string? extractedValue, ILogger logger, List<ExtractionError> errors)
    {
        var normalizedValue = (extractedValue ?? string.Empty).Trim();
        if (!_reportedNormalizedValues.Add(normalizedValue))
        {
            return;
        }

        var error = new ExtractionError(
            sheet, blockIdentifier, ExtractionErrorCode.NoConditionalPointCreated, BuildMessage(extractedValue), extractedValue);
        ExtractionErrorLogging.Log(logger, error);
        errors.Add(error);
    }

    private static string BuildMessage(string? extractedValue) =>
        string.IsNullOrWhiteSpace(extractedValue)
            ? "Aucun Point conditionnel n'a été créé : aucune valeur n'a été extraite pour cette feuille, " +
              "et aucune condition du profil d'import ne correspond à une valeur absente."
            : $"Aucun Point conditionnel n'a été créé pour la valeur « {extractedValue} » : aucune condition " +
              "du profil d'import ne correspond à cette valeur pour cette feuille.";
}
