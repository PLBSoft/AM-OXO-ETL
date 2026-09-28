using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo.Elements;

// Lot 085 (docs/tickets/tickets-tdd-lot-085-rejet-reperes-en-double.md): AlphaMaintenance identifies an
// isolement by its repère, so two elements of one file must never share one. Runs on the elements of
// every sheet at once, since the repère has to be unique in the generated "Enfants" sheet as a whole.
//
// - comparison: trimmed, case-insensitive (AlphaMaintenance's own Trim().ToLower());
// - one blocking DuplicateRepere entry per duplicated row, not only the second one, so the user sees
//   every row to fix;
// - groups in order of first appearance, rows in processing order (sheet order of the pipeline, then row).
public static class DuplicateRepereDetector
{
    public static IReadOnlyList<ExtractionError> Detect(IReadOnlyList<IsolementPivot> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        return
        [
            .. elements
                .GroupBy(e => e.Repere.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .SelectMany(ToErrors)
        ];
    }

    private static IEnumerable<ExtractionError> ToErrors(IGrouping<string, IsolementPivot> group)
    {
        var rows = string.Join(", ", group.Select(e => $"{e.SourceSheetName} ligne {e.LigneSource}"));
        var message =
            $"Repère « {group.Key} » en double ({rows}) : chaque élément doit avoir une identification unique.";

        return group.Select(e => new ExtractionError(
            e.SourceSheetName, e.LigneSource.ToString(), ExtractionErrorCode.DuplicateRepere, message,
            extractedValue: e.Repere));
    }
}
