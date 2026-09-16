using System.Text.Json.Serialization;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of ImportProfile (Domain) -- the one draft object owned by
// ImportProfileEditor.razor's root (lot 075). Id is null for a brand new profile.
public sealed class ImportProfileDraft : IDraftWithError
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReperePrefix { get; set; } = ImportProfile.DefaultReperePrefix;
    public string EquipementTypeElementNom { get; set; } = string.Empty;

    public List<StringItemDraft> DefaultTableaux { get; set; } = [];
    public List<StringItemDraft> DefaultApplicationNames { get; set; } = [];
    public List<TacheMultipleTypeLabelDraft> TacheMultipleTypeLabels { get; set; } = [];
    public List<SheetExtractionRuleDraft> SheetRules { get; set; } = [];

    public StringItemDraft PendingDefaultTableau { get; set; } = new();
    public StringItemDraft PendingDefaultApplicationName { get; set; } = new();
    public TacheMultipleTypeLabelDraft PendingTacheMultipleTypeLabel { get; set; } = new();
    public SheetExtractionRuleDraft PendingSheetRule { get; set; } = new();

    [JsonIgnore]
    public string? Error { get; set; }
}
