using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IRepeatingBlockReader
{
    // sheetName: the sheet of the rule that owns the locator (lot 084, G13); stopFieldName: the block
    // field whose blank value ends the reading (G12).
    RepeatingBlockReadResult Read(
        RepeatingBlockLocator locator, string sheetName, string stopFieldName, IWorkbookReader workbookReader);
}
