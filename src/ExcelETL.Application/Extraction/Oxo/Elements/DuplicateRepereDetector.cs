using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo.Elements;

// Lot 085 (docs/tickets/tickets-tdd-lot-085-rejet-reperes-en-double.md): AlphaMaintenance identifies an
// isolement by its repère, so two elements of one file must never share one. Runs on the elements of
// every sheet at once, since the repère has to be unique in the generated "Enfants" sheet as a whole.
//
// - comparison: trimmed, case-insensitive (AlphaMaintenance's own Trim().ToLower());
// - one blocking DuplicateRepere entry per duplicated repère (lot 086), located on its first row in
//   processing order (sheet order of the pipeline, then row); the message names every row, so the user
//   still sees all of them;
// - entries in order of first appearance.
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
                .Select(ToError)
        ];
    }

    private static ExtractionError ToError(IGrouping<string, IsolementPivot> group)
    {
        var rows = string.Join(", ", group.Select(e => $"{e.SourceSheetName} ligne {e.LigneSource}"));
        var message =
            $"Repère « {group.Key} » en double ({rows}) : chaque élément doit avoir une identification unique.";
        var first = group.First();

        return new ExtractionError(
            first.SourceSheetName, first.LigneSource.ToString(), ExtractionErrorCode.DuplicateRepere, message,
            extractedValue: first.Repere);
    }
}
