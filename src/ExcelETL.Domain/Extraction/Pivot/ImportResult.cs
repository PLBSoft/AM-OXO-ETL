namespace ExcelETL.Domain.Extraction.Pivot;

// The full result of extracting one source workbook -- decouples extraction from target-workbook
// writing (out of scope for now) and backs the future "test profile" screen. Equipement is null only
// for the whole-file rejection case (an invalid PROCEDURE header): see
// docs/modele-domaine-import-profile-2026-07-16.md §2.2, §3.1. Whether the other collections are
// actually empty in that case is enforced by the orchestrator (Lot D), not here.
public sealed class ImportResult
{
    public EquipementPivot? Equipement { get; }
    public IReadOnlyList<IsolementPivot> Isolements { get; }
    public IReadOnlyList<PointPivot> Points { get; }
    public IReadOnlyList<TacheMultiplePivot> TachesMultiples { get; }
    public IReadOnlyList<ExtractionError> Errors { get; }
    public bool HasErrors => Errors.Count > 0;

    public ImportResult(
        EquipementPivot? equipement,
        IReadOnlyList<IsolementPivot> isolements,
        IReadOnlyList<PointPivot> points,
        IReadOnlyList<TacheMultiplePivot> tachesMultiples,
        IReadOnlyList<ExtractionError> errors)
    {
        ArgumentNullException.ThrowIfNull(isolements);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(tachesMultiples);
        ArgumentNullException.ThrowIfNull(errors);

        Equipement = equipement;
        Isolements = isolements;
        Points = points;
        TachesMultiples = tachesMultiples;
        Errors = errors;
    }
}
