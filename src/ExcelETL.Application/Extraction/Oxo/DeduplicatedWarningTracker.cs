using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Lot 084 (G11): one tracker for every non-blocking warning reported once per value and per sheet
// within a single Extract() call. Values are compared trimmed, ignoring case; the first raw form is
// the one reported. Never static/shared: a value seen on one sheet or run never hides it elsewhere.
public sealed class DeduplicatedWarningTracker(string sheet, ExtractionErrorCode code, Func<string?, string> buildMessage)
{
    private readonly HashSet<string> _reportedNormalizedValues = new(StringComparer.OrdinalIgnoreCase);

    public void RecordIfNew(string blockIdentifier, string? extractedValue, ILogger logger, List<ExtractionError> errors)
    {
        if (!_reportedNormalizedValues.Add((extractedValue ?? string.Empty).Trim()))
        {
            return;
        }

        var error = new ExtractionError(sheet, blockIdentifier, code, buildMessage(extractedValue), extractedValue);
        ExtractionErrorLogging.Log(logger, error);
        errors.Add(error);
    }

    // Same texts as the lot 055 / 2026-09-11 trackers this class replaces.
    public static DeduplicatedWarningTracker ForNoConditionalPointCreated(string sheet) => new(
        sheet, ExtractionErrorCode.NoConditionalPointCreated, value =>
            string.IsNullOrWhiteSpace(value)
                ? "Aucun Point conditionnel n'a été créé : aucune valeur n'a été extraite pour cette feuille, " +
                  "et aucune condition du profil d'import ne correspond à une valeur absente."
                : $"Aucun Point conditionnel n'a été créé pour la valeur « {value} » : aucune condition " +
                  "du profil d'import ne correspond à cette valeur pour cette feuille.");

    public static DeduplicatedWarningTracker ForUnexpectedCouleurEtiquetteValue(string sheet) => new(
        sheet, ExtractionErrorCode.UnexpectedCouleurEtiquetteValue, value =>
            $"La cellule « couleur d'étiquette » contient une valeur inattendue « {value} » : elle ne " +
            "correspond à aucune des couleurs autorisées configurées sur le profil d'import. " +
            "L'isolement est extrait normalement, sans couleur d'étiquette associée.");
}
