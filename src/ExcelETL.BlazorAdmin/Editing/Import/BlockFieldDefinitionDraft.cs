using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of BlockFieldDefinition (Domain). AbsoluteRange is the Excel range as
// shown/typed (e.g. "B19:E20") and is the source of truth: the relative offsets the Domain stores are
// recomputed from it with the owning rule's current FirstBlockStartRow at conversion time (lot 075,
// decision Q3 -- changing the start row keeps the ranges the admin sees).
public sealed class BlockFieldDefinitionDraft : IDraftWithError
{
    public string Name { get; set; } = string.Empty;
    public string AbsoluteRange { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Error { get; set; }

    // Display-only, non-blocking "range beyond the plausible zone" notice
    // (BlockFieldRangeParseResult.IsBeyondPracticalRange), set by the list owner after a successful add.
    [JsonIgnore]
    public string? Warning { get; set; }
}
