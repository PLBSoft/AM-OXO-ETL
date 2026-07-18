namespace ExcelETL.Application.Generation;

// Abstraction over the target workbook, implemented with ClosedXML in Infrastructure (I4). Symmetric
// to IWorkbookReader on the import side: the engine (ISheetGenerationEngine) never needs anything
// beyond "write this already-built intermediate structure out".
public interface IWorkbookWriter
{
    void Write(GeneratedWorkbook workbook, Stream destination);
}
