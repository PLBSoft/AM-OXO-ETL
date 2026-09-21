using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of SheetExtractionRule (Domain), with its RepeatingBlockLocator
// flattened in, the way the form shows it. The two optional colour settings are plain text: blank means "not
// configured" (null in the Domain). AllowedCouleursEtiquette is the comma-separated text of the single
// input that edits that list.
//
// Each Pending... draft is the always-present "Add a ..." row of one list -- see ImportProfileDraftMapper
// for how a non-blank pending row is folded into its list at conversion time (defect A suppressed by
// construction).
public sealed class SheetExtractionRuleDraft : IDraftWithError
{
    public string SheetName { get; set; } = string.Empty;
    public int FirstBlockStartRow { get; set; }
    public int Step { get; set; }
    public string StopFieldName { get; set; } = string.Empty;
    public string DefaultCouleurEtiquette { get; set; } = string.Empty;
    public string AllowedCouleursEtiquette { get; set; } = string.Empty;
    public bool WarnWhenNoConditionalPoint { get; set; }

    public List<BlockFieldDefinitionDraft> Fields { get; set; } = [];
    public List<StringItemDraft> UnconditionalColonneNames { get; set; } = [];
    public List<ConditionalPointRuleDraft> PointRules { get; set; } = [];
    public List<HeaderFieldRuleDraft> HeaderFields { get; set; } = [];
    public List<HeaderCompositeRuleDraft> HeaderComposites { get; set; } = [];

    public BlockFieldDefinitionDraft PendingField { get; set; } = new();
    public StringItemDraft PendingUnconditionalColonneName { get; set; } = new();
    public ConditionalPointRuleDraft PendingPointRule { get; set; } = new();
    public HeaderFieldRuleDraft PendingHeaderField { get; set; } = new();
    public HeaderCompositeRuleDraft PendingHeaderComposite { get; set; } = new();

    [JsonIgnore]
    public string? Error { get; set; }
}
