using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.Elements;

public interface IElementSheetExtractionService
{
    ElementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix);
}
