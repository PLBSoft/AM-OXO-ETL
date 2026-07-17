using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

// Evaluates every conditional Colonne-group for one isolement's extracted fields, aggregating the
// result across the whole sheet rather than per group: model doc §3.2's non-blocking warning fires
// when the TypeElement matches *none* of the sheet's configured conditions, not whenever any single
// Colonne's own condition happens not to match. For a sheet with a single conditional group
// (ISOLEMENT, AUTRES JOINTS TOUCHES) this collapses to the same result either way; it matters once a
// sheet has several mutually-exclusive conditional types (DIVERS) -- a SOUPAPE isolement there
// correctly gets its own 2 Colonnes and no warning, instead of also warning for not matching
// INSTRUMENTATION's or POINT FEU's groups.
public static class ConditionalPointGroupEvaluator
{
    public static (IReadOnlyList<string> ColonneNamesToCreate, string? Warning) Evaluate(
        IConditionalPointRuleEvaluator evaluator,
        IEnumerable<IGrouping<string, ConditionalPointRule>> groups,
        IReadOnlyDictionary<string, string> extractedFields)
    {
        var colonneNames = new List<string>();
        string? firstWarning = null;

        foreach (var group in groups)
        {
            var (shouldCreate, warning) = evaluator.Evaluate(group.ToList(), extractedFields);
            if (shouldCreate)
            {
                colonneNames.Add(group.Key);
            }
            else
            {
                firstWarning ??= warning;
            }
        }

        return (colonneNames, colonneNames.Count == 0 ? firstWarning : null);
    }
}
