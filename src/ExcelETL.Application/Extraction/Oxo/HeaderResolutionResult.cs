namespace ExcelETL.Application.Extraction.Oxo;

// Fields: one HeaderFieldResolution per HeaderFieldRule on the sheet, keyed by Name. Composites: one
// substituted string per HeaderCompositeRule, keyed by Name -- null only when its own Template
// substitution failed to resolve a referenced field to a non-null Value (falls back to "").
public sealed class HeaderResolutionResult(
    IReadOnlyDictionary<string, HeaderFieldResolution> fields, IReadOnlyDictionary<string, string?> composites)
{
    public IReadOnlyDictionary<string, HeaderFieldResolution> Fields { get; } = fields;
    public IReadOnlyDictionary<string, string?> Composites { get; } = composites;
}
