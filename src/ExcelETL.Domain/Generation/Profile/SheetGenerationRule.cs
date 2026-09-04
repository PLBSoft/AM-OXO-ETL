using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;

namespace ExcelETL.Domain.Generation.Profile;

// One generated sheet's column layout within an ExportProfile. Modeled as a record (structural
// equality), unlike the import side's SheetExtractionRule (a plain class with no identity) -- this
// lot's ticket explicitly asks for record equality across all 4 new types. ColumnDefinitions and
// PointColumnDefinitions are both lists, so the default record-synthesized equality (reference
// equality on IReadOnlyList<T>) is overridden below with SequenceEqual, same pattern as
// RepeatingBlockLocator.Fields on the import side.
//
// For PivotSource.TacheMultiple (Lot T), SheetName changes role: it's an admin-facing internal label
// only (e.g. "Tâches multiples") -- the generated workbook never uses it as an actual sheet name for
// this PivotSource, since the engine discovers TypeTacheMultipleCode values at runtime and emits one
// physical sheet per distinct code instead (see SheetGenerationEngine). Still required non-blank, same
// validation as every other PivotSource. PointColumnDefinitions are rejected outright for this
// PivotSource (a TacheMultiple has no associated Point, structurally distinct from Equipement/Isolement).
public sealed record SheetGenerationRule
{
    // See RepeatingBlockLocator.Fields (Extraction/Primitives) for why these need a backing field
    // instead of a plain auto-property: EF Core cannot constructor-bind an entity-collection
    // navigation.
    private readonly List<ColumnDefinition> _columnDefinitions = [];
    private readonly List<PointColumnDefinition> _pointColumnDefinitions = [];
    private readonly List<ApplicationColumnDefinition> _applicationColumnDefinitions = [];
    private readonly List<ConstantColumnDefinition> _constantColumnDefinitions = [];

    public string SheetName { get; }
    public PivotSource PivotSource { get; }
    public IReadOnlyList<ColumnDefinition> ColumnDefinitions => _columnDefinitions;
    public IReadOnlyList<PointColumnDefinition> PointColumnDefinitions => _pointColumnDefinitions;
    public IReadOnlyList<ApplicationColumnDefinition> ApplicationColumnDefinitions => _applicationColumnDefinitions;
    public IReadOnlyList<ConstantColumnDefinition> ConstantColumnDefinitions => _constantColumnDefinitions;

    // constantColumnDefinitions is a last, optional parameter (Lot 069) -- unlike the 3 collections
    // above, most sheets (Parents/Enfants) never need a constant column, so making it required would
    // force a mechanical update of every one of the ~20 existing `new SheetGenerationRule(...)` call
    // sites for no benefit. Normalized to [] rather than left null.
    public SheetGenerationRule(
        string sheetName,
        PivotSource pivotSource,
        IReadOnlyList<ColumnDefinition> columnDefinitions,
        IReadOnlyList<PointColumnDefinition> pointColumnDefinitions,
        IReadOnlyList<ApplicationColumnDefinition> applicationColumnDefinitions,
        IReadOnlyList<ConstantColumnDefinition>? constantColumnDefinitions = null)
    {
        ValidateSheetNameNotEmpty(sheetName);

        ArgumentNullException.ThrowIfNull(columnDefinitions);
        ArgumentNullException.ThrowIfNull(pointColumnDefinitions);
        ArgumentNullException.ThrowIfNull(applicationColumnDefinitions);

        constantColumnDefinitions ??= [];

        ValidateColumnPivotSourceCompatibility(columnDefinitions, pivotSource);
        ValidateNoPointColumnsForTacheMultiple(sheetName, pivotSource, pointColumnDefinitions);
        ValidateNoApplicationColumnsForTacheMultiple(sheetName, pivotSource, applicationColumnDefinitions);
        ValidateNoDuplicateHeaders(
            sheetName, columnDefinitions, pointColumnDefinitions, applicationColumnDefinitions, constantColumnDefinitions);
        ValidateNoDuplicateColonneNom(sheetName, pointColumnDefinitions);
        ValidateNoDuplicateApplicationNom(sheetName, applicationColumnDefinitions);

        SheetName = sheetName;
        PivotSource = pivotSource;
        _columnDefinitions = [.. columnDefinitions];
        _pointColumnDefinitions = [.. pointColumnDefinitions];
        _applicationColumnDefinitions = [.. applicationColumnDefinitions];
        _constantColumnDefinitions = [.. constantColumnDefinitions];
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private SheetGenerationRule()
    {
        SheetName = string.Empty;
    }

    private static void ValidateSheetNameNotEmpty(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new DomainValidationException(
                "Sheet name must not be empty.", nameof(sheetName), DomainErrorCode.SheetGenerationRule_EmptySheetName);
        }
    }

    private static void ValidateColumnPivotSourceCompatibility(
        IReadOnlyList<ColumnDefinition> columnDefinitions, PivotSource pivotSource)
    {
        var incompatibleColumn = columnDefinitions.FirstOrDefault(
            column => column.Source is not null && PivotFieldResolver.GetPivotSource(column.Source.Value) != pivotSource);

        if (incompatibleColumn is not null)
        {
            var incompatibleFieldRef = incompatibleColumn.Source!.Value;
            throw new DomainRuleViolationException(
                $"Column '{incompatibleColumn.Header}' references field '{incompatibleFieldRef}', which belongs to " +
                $"{PivotFieldResolver.GetPivotSource(incompatibleFieldRef)}, not this sheet's PivotSource ({pivotSource}).",
                DomainErrorCode.SheetGenerationRule_ColumnSourceIncompatibleWithPivotSource,
                incompatibleColumn.Header, incompatibleFieldRef, pivotSource);
        }
    }

    private static void ValidateNoPointColumnsForTacheMultiple(
        string sheetName, PivotSource pivotSource, IReadOnlyList<PointColumnDefinition> pointColumnDefinitions)
    {
        if (pivotSource == PivotSource.TacheMultiple && pointColumnDefinitions.Count > 0)
        {
            throw new DomainRuleViolationException(
                $"Sheet '{sheetName}' has PivotSource TacheMultiple, which has no associated Point -- " +
                "PointColumnDefinitions are not allowed for this pivot source.",
                DomainErrorCode.SheetGenerationRule_PointColumnDefinitionsNotAllowedForTacheMultiple, sheetName);
        }
    }

    private static void ValidateNoApplicationColumnsForTacheMultiple(
        string sheetName, PivotSource pivotSource, IReadOnlyList<ApplicationColumnDefinition> applicationColumnDefinitions)
    {
        if (pivotSource == PivotSource.TacheMultiple && applicationColumnDefinitions.Count > 0)
        {
            throw new DomainRuleViolationException(
                $"Sheet '{sheetName}' has PivotSource TacheMultiple, which has no associated Application -- " +
                "ApplicationColumnDefinitions are not allowed for this pivot source.",
                DomainErrorCode.SheetGenerationRule_ApplicationColumnDefinitionsNotAllowedForTacheMultiple, sheetName);
        }
    }

    private static void ValidateNoDuplicateHeaders(
        string sheetName,
        IReadOnlyList<ColumnDefinition> columnDefinitions,
        IReadOnlyList<PointColumnDefinition> pointColumnDefinitions,
        IReadOnlyList<ApplicationColumnDefinition> applicationColumnDefinitions,
        IReadOnlyList<ConstantColumnDefinition> constantColumnDefinitions)
    {
        var duplicateHeader = columnDefinitions.Select(c => c.Header)
            .Concat(pointColumnDefinitions.Select(p => p.Header))
            .Concat(applicationColumnDefinitions.Select(a => a.Header))
            .Concat(constantColumnDefinitions.Select(c => c.Header))
            .GroupBy(header => header)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateHeader is not null)
        {
            throw new DomainValidationException(
                $"Header '{duplicateHeader.Key}' is used more than once in sheet '{sheetName}'.",
                nameof(columnDefinitions), DomainErrorCode.SheetGenerationRule_DuplicateHeader, duplicateHeader.Key);
        }
    }

    private static void ValidateNoDuplicateColonneNom(
        string sheetName, IReadOnlyList<PointColumnDefinition> pointColumnDefinitions)
    {
        var duplicateColonneNom = pointColumnDefinitions
            .GroupBy(point => point.ColonneNom)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateColonneNom is not null)
        {
            throw new DomainValidationException(
                $"Colonne nom '{duplicateColonneNom.Key}' is used more than once in sheet '{sheetName}'.",
                nameof(pointColumnDefinitions), DomainErrorCode.SheetGenerationRule_DuplicateColonneNom, duplicateColonneNom.Key);
        }
    }

    private static void ValidateNoDuplicateApplicationNom(
        string sheetName, IReadOnlyList<ApplicationColumnDefinition> applicationColumnDefinitions)
    {
        var duplicateApplicationNom = applicationColumnDefinitions
            .GroupBy(application => application.ApplicationNom)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateApplicationNom is not null)
        {
            throw new DomainValidationException(
                $"Application nom '{duplicateApplicationNom.Key}' is used more than once in sheet '{sheetName}'.",
                nameof(applicationColumnDefinitions), DomainErrorCode.SheetGenerationRule_DuplicateApplicationNom,
                duplicateApplicationNom.Key);
        }
    }

    public bool Equals(SheetGenerationRule? other) =>
        other is not null
        && SheetName == other.SheetName
        && PivotSource == other.PivotSource
        && ColumnDefinitions.SequenceEqual(other.ColumnDefinitions)
        && PointColumnDefinitions.SequenceEqual(other.PointColumnDefinitions)
        && ApplicationColumnDefinitions.SequenceEqual(other.ApplicationColumnDefinitions)
        && ConstantColumnDefinitions.SequenceEqual(other.ConstantColumnDefinitions);

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

        foreach (var application in ApplicationColumnDefinitions)
        {
            hash.Add(application);
        }

        foreach (var constant in ConstantColumnDefinitions)
        {
            hash.Add(constant);
        }

        return hash.ToHashCode();
    }
}
