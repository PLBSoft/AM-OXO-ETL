using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.Divers;

public interface IDiversExtractionService
{
    DiversSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule);
}
