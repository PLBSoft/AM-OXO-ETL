using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IImportPipelineOrchestrator
{
    ImportResult Run(IWorkbookReader workbookReader, ImportProfile profile);
}
