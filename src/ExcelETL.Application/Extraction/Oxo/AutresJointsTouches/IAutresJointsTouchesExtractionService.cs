using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;

public interface IAutresJointsTouchesExtractionService
{
    IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule);
}
