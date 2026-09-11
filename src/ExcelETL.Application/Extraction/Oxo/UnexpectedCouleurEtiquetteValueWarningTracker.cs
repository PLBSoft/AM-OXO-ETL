using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Builds and deduplicates UnexpectedCouleurEtiquetteValue warnings for one sheet within a single
// Extract() call -- client feedback (2026-09-11), mirroring UnexpectedZeroEnergieValueWarningTracker's
// own shape (Lot 063) rather than inventing a parallel mechanism. Fires when CouleurEtiquetteResolver
// reads a non-blank cell value that matches none of the sheet's configured
// SheetExtractionRule.AllowedCouleursEtiquette -- the isolement is still extracted normally
// (CouleurEtiquette simply falls back to ""), this warning is purely informational. Deduplicated by
// Trim+OrdinalIgnoreCase-normalized value. One instance created fresh per Extract() call -- never
// static/shared.
public sealed class UnexpectedCouleurEtiquetteValueWarningTracker(string sheet)
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
            sheet, blockIdentifier, ExtractionErrorCode.UnexpectedCouleurEtiquetteValue, BuildMessage(extractedValue), extractedValue);
        ExtractionErrorLogging.Log(logger, error);
        errors.Add(error);
    }

    private static string BuildMessage(string extractedValue) =>
        $"La cellule « couleur d'étiquette » contient une valeur inattendue « {extractedValue} » : elle ne " +
        "correspond à aucune des couleurs autorisées configurées sur le profil d'import. " +
        "L'isolement est extrait normalement, sans couleur d'étiquette associée.";
}
