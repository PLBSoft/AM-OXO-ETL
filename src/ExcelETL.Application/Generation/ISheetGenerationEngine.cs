using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Application.Generation;

public interface ISheetGenerationEngine
{
    GeneratedWorkbook Generate(ImportResult importResult, ExportProfile profile);
}
