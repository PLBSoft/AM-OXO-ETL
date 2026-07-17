using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// The primitive shared by all repeating-block sheets (ISOLEMENT, PLATINES, ORIFICES CAPACITES,
// AUTRES JOINTS TOUCHES, DIVERS, PROCEDURE) -- see docs/modele-domaine-import-profile-2026-07-16.md §1.2.
// Fields is a list, so the default record-synthesized equality (reference equality on the list) is
// overridden below with SequenceEqual to give true structural equality.
public sealed record RepeatingBlockLocator
{
    // Backing field rather than a plain auto-property: EF Core's constructor-binding materialization
    // cannot bind a constructor parameter to an entity-collection navigation (confirmed empirically --
    // "Navigations to related entities... cannot be bound" -- so Fields must be settable via
    // reflection post-construction instead, using the private parameterless constructor below. Same
    // technique as ExtractionConfig._sheets/SheetConfig._cellMappings, minus a public mutation method
    // since RepeatingBlockLocator is meant to stay fully immutable after construction.
    private readonly List<BlockFieldDefinition> _fields = [];

    public string Sheet { get; }
    public int FirstBlockStartRow { get; }
    public int Step { get; }
    public string StopFieldName { get; }
    public IReadOnlyList<BlockFieldDefinition> Fields => _fields;

    public RepeatingBlockLocator(
        string sheet, int firstBlockStartRow, int step, string stopFieldName, IReadOnlyList<BlockFieldDefinition> fields)
    {
        if (string.IsNullOrWhiteSpace(sheet))
        {
            throw new DomainValidationException(
                "Sheet must not be empty.", nameof(sheet), DomainErrorCode.RepeatingBlockLocator_EmptySheet);
        }

        if (firstBlockStartRow <= 0)
        {
            throw new DomainArgumentOutOfRangeException(
                nameof(firstBlockStartRow), firstBlockStartRow, "First block start row must be positive.",
                DomainErrorCode.RepeatingBlockLocator_NonPositiveFirstBlockStartRow);
        }

        if (step <= 0)
        {
            throw new DomainArgumentOutOfRangeException(
                nameof(step), step, "Step must be positive.",
                DomainErrorCode.RepeatingBlockLocator_NonPositiveStep);
        }

        if (string.IsNullOrWhiteSpace(stopFieldName))
        {
            throw new DomainValidationException(
                "Stop field name must not be empty.", nameof(stopFieldName),
                DomainErrorCode.RepeatingBlockLocator_EmptyStopFieldName);
        }

        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0)
        {
            throw new DomainValidationException(
                "Fields must contain at least one field definition.", nameof(fields),
                DomainErrorCode.RepeatingBlockLocator_EmptyFields);
        }

        Sheet = sheet;
        FirstBlockStartRow = firstBlockStartRow;
        Step = step;
        StopFieldName = stopFieldName;
        _fields = [.. fields];
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private RepeatingBlockLocator()
    {
        Sheet = string.Empty;
        StopFieldName = string.Empty;
    }

    public bool Equals(RepeatingBlockLocator? other) =>
        other is not null
        && Sheet == other.Sheet
        && FirstBlockStartRow == other.FirstBlockStartRow
        && Step == other.Step
        && StopFieldName == other.StopFieldName
        && Fields.SequenceEqual(other.Fields);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sheet);
        hash.Add(FirstBlockStartRow);
        hash.Add(Step);
        hash.Add(StopFieldName);
        foreach (var field in Fields)
        {
            hash.Add(field);
        }

        return hash.ToHashCode();
    }
}
