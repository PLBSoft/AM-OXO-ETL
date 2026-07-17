using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IUnconditionalIsolementSheetExtractionService
{
    IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule);
}
