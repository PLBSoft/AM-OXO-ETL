using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo.Elements;

// Lot 084 (G11): the one result shape of the five element sheets. Zone is "" unless the sheet
// declares a "zone" header field (DIVERS in the standard profile).
public sealed record ElementSheetExtractionResult(
    IReadOnlyList<IsolementPivot> Elements,
    IReadOnlyList<PointPivot> Points,
    IReadOnlyList<ExtractionError> Errors,
    string Zone);
