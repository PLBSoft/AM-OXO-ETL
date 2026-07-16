using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IRepeatingBlockReader
{
    RepeatingBlockReadResult Read(RepeatingBlockLocator locator, IWorkbookReader workbookReader);
}
