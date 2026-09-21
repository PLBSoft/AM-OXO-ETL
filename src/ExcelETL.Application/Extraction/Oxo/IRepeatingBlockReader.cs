using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IRepeatingBlockReader
{
    // stopFieldName: the block field whose blank value ends the reading (lot 084, G12).
    RepeatingBlockReadResult Read(RepeatingBlockLocator locator, string stopFieldName, IWorkbookReader workbookReader);
}
