using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo.Procedure;

// One PROCEDURE "tâche multiple factice" section: Title is the Action text of the factice
// (EstFactice) row that introduced it, Tasks is the ordered (Ordre, TypeTacheMultipleCode) pairs of
// the real tasks that follow, up to (excluding) the next factice row.
public sealed record TacheMultipleSection(string Title, IReadOnlyList<(int Ordre, string Type)> Tasks);

// Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md), note d'efficacité #4:
// ProcedureExtractionService.ReadTachesMultiples produces a flat list with no section grouping --
// this is a new, discrete, independently-tested step consumed by TacheMultipleTypeCoherenceAnalyzer
// (32.1), not folded inline into that analyzer or into the reading loop itself.
public static class TacheMultipleSectionGrouper
{
    public static IReadOnlyList<TacheMultipleSection> GroupBySection(IReadOnlyList<TacheMultiplePivot> tachesMultiples)
    {
        ArgumentNullException.ThrowIfNull(tachesMultiples);

        var sections = new List<TacheMultipleSection>();
        var currentTitle = "";
        var currentTasks = new List<(int Ordre, string Type)>();

        foreach (var tache in tachesMultiples)
        {
            if (tache.EstFactice)
            {
                if (currentTasks.Count > 0)
                {
                    sections.Add(new TacheMultipleSection(currentTitle, currentTasks));
                }

                currentTitle = tache.Action;
                currentTasks = [];
                continue;
            }

            // A non-factice row with an unparsable Ordre (pre-existing edge case, see
            // ProcedureExtractionService.ReadTachesMultiples) has no task number to anchor a run --
            // excluded from detection rather than crashing it.
            if (tache.Ordre.HasValue)
            {
                currentTasks.Add((tache.Ordre.Value, tache.TypeTacheMultipleCode));
            }
        }

        if (currentTasks.Count > 0)
        {
            sections.Add(new TacheMultipleSection(currentTitle, currentTasks));
        }

        return sections;
    }
}
