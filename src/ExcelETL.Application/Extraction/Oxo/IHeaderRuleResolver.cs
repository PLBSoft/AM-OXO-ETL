using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

// Resolves every HeaderFieldRule/HeaderCompositeRule (Lot 047,
// docs/reference/spec-migration-entetes-profile-driven-directcell.md §3) declared on a
// SheetExtractionRule against a real workbook -- the profile-driven replacement for the coordinates
// (M2:O2, P2:Q2, R2:T2, N6) previously hardcoded in ProcedureExtractionService/
// AutresJointsTouchesExtractionService/DiversExtractionService.
public interface IHeaderRuleResolver
{
    HeaderResolutionResult Resolve(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix);
}
