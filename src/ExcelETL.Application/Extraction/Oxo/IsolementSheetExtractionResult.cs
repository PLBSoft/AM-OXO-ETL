using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// Shared return shape for every isolement-style sheet's extraction service (ISOLEMENT, PLATINES,
// ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS) -- purpose-built DTO, same spirit as
// RepeatingBlockReadResult, not a reuse of ImportResult: ImportResult's null Equipement specifically
// means "whole-file rejection" (see its own comment), which doesn't apply to these sheets at all
// (they never produce an Equipement), so reusing it here would blur that meaning for Lot D's
// orchestrator merge logic.
public sealed record IsolementSheetExtractionResult(
    IReadOnlyList<IsolementPivot> Isolements,
    IReadOnlyList<PointPivot> Points,
    IReadOnlyList<ExtractionError> Errors);
