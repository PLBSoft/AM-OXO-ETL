using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo.Divers;

// DIVERS is the one sheet that produces the "loc1" broadcast value (B6:E6, "valeur brute, pas de
// transformation" per spec §6) -- a dedicated result type rather than reusing
// IsolementSheetExtractionResult, since Loc1 is meaningless for the other 4 isolement-style sheets
// and doesn't belong on their shared shape. Lot D's orchestrator reads Loc1 from here and broadcasts
// it onto the Equipement and every Isolement from the whole run (this service does not apply it to
// its own Isolements -- that would only cover DIVERS's own rows, not the other 4 sheets').
public sealed record DiversSheetExtractionResult(
    string Loc1,
    IReadOnlyList<IsolementPivot> Isolements,
    IReadOnlyList<PointPivot> Points,
    IReadOnlyList<ExtractionError> Errors);
