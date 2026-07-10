namespace ExcelETL.Application.Extraction;

public interface IExcelGeneratorService
{
    Stream Generate(ExtractionResult result);
}
