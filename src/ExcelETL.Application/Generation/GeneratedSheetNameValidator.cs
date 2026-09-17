using ExcelETL.Application.Exceptions;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Application.Generation;

// Lot 080.5 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md, D4): checks every sheet
// name SheetGenerationEngine produced, against the same rules as the domain (ExcelSheetName). Covers what the
// profile alone can't decide (task codes come from the imported file) and profiles saved before lot 080.
public static class GeneratedSheetNameValidator
{
    public static void Validate(IEnumerable<string> sheetNames)
    {
        ArgumentNullException.ThrowIfNull(sheetNames);

        var seen = new HashSet<string>(ExcelSheetName.NameComparer);
        foreach (var sheetName in sheetNames)
        {
            if (!ExcelSheetName.IsValid(sheetName))
            {
                throw GeneratedSheetNameException.Invalid(sheetName);
            }

            if (!seen.Add(sheetName))
            {
                throw GeneratedSheetNameException.Conflict(sheetName);
            }
        }
    }
}
