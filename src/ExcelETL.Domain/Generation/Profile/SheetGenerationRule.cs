using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Generation.Profile;

// One generated sheet's column layout within an ExportProfile. Modeled as a record (structural
// equality), unlike the import side's SheetExtractionRule (a plain class with no identity) -- this
// lot's ticket explicitly asks for record equality across all 4 new types. ColumnDefinitions and
// PointColumnDefinitions are both lists, so the default record-synthesized equality (reference
// equality on IReadOnlyList<T>) is overridden below with SequenceEqual, same pattern as
// RepeatingBlockLocator.Fields on the import side.
public sealed record SheetGenerationRule
{
    // See RepeatingBlockLocator.Fields (Extraction/Primitives) for why these need a backing field
    // instead of a plain auto-property: EF Core cannot constructor-bind an entity-collection
    // navigation.
    private readonly List<ColumnDefinition> _columnDefinitions = [];
    private readonly List<PointColumnDefinition> _pointColumnDefinitions = [];

    public string SheetName { get; }
    public PivotSource PivotSource { get; }
    public IReadOnlyList<ColumnDefinition> ColumnDefinitions => _columnDefinitions;
    public IReadOnlyList<PointColumnDefinition> PointColumnDefinitions => _pointColumnDefinitions;

    public SheetGenerationRule(
        string sheetName,
        PivotSource pivotSource,
        IReadOnlyList<ColumnDefinition> columnDefinitions,
        IReadOnlyList<PointColumnDefinition> pointColumnDefinitions)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new DomainValidationException(
                "Sheet name must not be empty.", nameof(sheetName), DomainErrorCode.SheetGenerationRule_EmptySheetName);
        }

        ArgumentNullException.ThrowIfNull(columnDefinitions);
        ArgumentNullException.ThrowIfNull(pointColumnDefinitions);

        var duplicateHeader = columnDefinitions.Select(c => c.Header)
            .Concat(pointColumnDefinitions.Select(p => p.Header))
            .GroupBy(header => header)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateHeader is not null)
        {
            throw new DomainValidationException(
                $"Header '{duplicateHeader.Key}' is used more than once in sheet '{sheetName}'.",
                nameof(columnDefinitions), DomainErrorCode.SheetGenerationRule_DuplicateHeader, duplicateHeader.Key);
        }

        var duplicateColonneNom = pointColumnDefinitions
            .GroupBy(point => point.ColonneNom)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateColonneNom is not null)
        {
            throw new DomainValidationException(
                $"Colonne nom '{duplicateColonneNom.Key}' is used more than once in sheet '{sheetName}'.",
                nameof(pointColumnDefinitions), DomainErrorCode.SheetGenerationRule_DuplicateColonneNom, duplicateColonneNom.Key);
        }

        SheetName = sheetName;
        PivotSource = pivotSource;
        _columnDefinitions = [.. columnDefinitions];
        _pointColumnDefinitions = [.. pointColumnDefinitions];
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private SheetGenerationRule()
    {
        SheetName = string.Empty;
    }

    public bool Equals(SheetGenerationRule? other) =>
        other is not null
        && SheetName == other.SheetName
        && PivotSource == other.PivotSource
        && ColumnDefinitions.SequenceEqual(other.ColumnDefinitions)
        && PointColumnDefinitions.SequenceEqual(other.PointColumnDefinitions);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SheetName);
        hash.Add(PivotSource);
        foreach (var column in ColumnDefinitions)
        {
            hash.Add(column);
        }

        foreach (var point in PointColumnDefinitions)
        {
            hash.Add(point);
        }

        return hash.ToHashCode();
    }
}
