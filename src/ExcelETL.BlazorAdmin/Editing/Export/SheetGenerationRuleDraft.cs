using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// Mutable, behavior-free mirror of SheetGenerationRule (Domain). ConstantColumns is a list of real,
// immutable Domain objects rather than a draft type of its own -- lot 069's ConstantColumnDefinition
// has no editing UI at all today (only DefaultProfileSeeder.cs sets it), so there is nothing to bind
// an input to; it must still be carried through unmodified on every conversion (the exact bug fixed
// the same day this lot was decided, commit 0cbac22).
//
// PendingColumn/PendingPointColumn/PendingApplicationColumn are the always-present "Add a ..." row's
// own draft, one per list -- see ExportProfileDraftMapper.ConvertSheetRule for how a still-unclicked,
// non-blank pending row is folded into the list at conversion time (defect A, "brouillon non validé",
// suppressed by construction).
public sealed class SheetGenerationRuleDraft : IDraftWithError
{
    public string SheetName { get; set; } = string.Empty;
    public PivotSource PivotSource { get; set; } = PivotSource.Equipement;
    public List<ColumnDefinitionDraft> Columns { get; set; } = [];
    public List<PointColumnDefinitionDraft> PointColumns { get; set; } = [];
    public List<ApplicationColumnDefinitionDraft> ApplicationColumns { get; set; } = [];
    public List<ConstantColumnDefinition> ConstantColumns { get; set; } = [];

    public ColumnDefinitionDraft PendingColumn { get; set; } = new();
    public PointColumnDefinitionDraft PendingPointColumn { get; set; } = new();
    public ApplicationColumnDefinitionDraft PendingApplicationColumn { get; set; } = new();

    [JsonIgnore]
    public string? Error { get; set; }
}
