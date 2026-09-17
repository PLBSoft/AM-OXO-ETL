namespace ExcelETL.Domain.Generation.Profile;

// Lot 081 (docs/tickets/tickets-tdd-lot-081-comparaison-noms-colonne-points-export.md, 81.1): the single
// comparison rule for a "Colonne"/"Application" name -- trimmed, then case-insensitive. Used both by
// SheetGenerationEngine (Application, deciding whether a Point/Application column is marked) and by
// SheetGenerationRule's own duplicate-name checks (Domain) -- one place, usable directly in a GroupBy.
// No diacritics normalization beyond casing: a genuine spelling difference ("POINT DE FEU" vs
// "POINT FEU") stays a real mismatch, same guard-rail as Lot 055's ConditionalPointGroupEvaluator.
public sealed class ColonneNameComparer : StringComparer
{
    public static ColonneNameComparer Instance { get; } = new();

    private ColonneNameComparer()
    {
    }

    public override int Compare(string? x, string? y) => OrdinalIgnoreCase.Compare(Normalize(x), Normalize(y));

    public override bool Equals(string? x, string? y) => OrdinalIgnoreCase.Equals(Normalize(x), Normalize(y));

    public override int GetHashCode(string obj) => OrdinalIgnoreCase.GetHashCode(obj.Trim());

    private static string? Normalize(string? value) => value?.Trim();
}
