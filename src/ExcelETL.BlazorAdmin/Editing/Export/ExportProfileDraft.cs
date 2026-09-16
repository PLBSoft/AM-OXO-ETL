using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// Mutable, behavior-free mirror of ExportProfile (Domain) -- the one draft object owned by
// ExportProfileEditor.razor's root. Id is null for a brand new profile (/export-profiles/new);
// ExportProfileDraftMapper.ToDomain builds a fresh ExportProfile in that case, or reconstructs one
// under its original Id otherwise (see ExportProfile's own two constructors).
public sealed class ExportProfileDraft : IDraftWithError
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<SheetGenerationRuleDraft> SheetRules { get; set; } = [];
    public SheetGenerationRuleDraft PendingSheetRule { get; set; } = new();

    [JsonIgnore]
    public string? Error { get; set; }
}
