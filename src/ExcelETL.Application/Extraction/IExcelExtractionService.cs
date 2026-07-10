using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public interface IExcelExtractionService
{
    ExtractionResult Extract(Stream excelFileStream, ExtractionConfig config);
}
