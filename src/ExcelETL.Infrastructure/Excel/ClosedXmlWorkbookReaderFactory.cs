using ExcelETL.Application.Extraction.Oxo;

namespace ExcelETL.Infrastructure.Excel;

public sealed class ClosedXmlWorkbookReaderFactory : IWorkbookReaderFactory
{
    // The returned reader is IDisposable; ProcessOxoFileService disposes it once processing is done.
    public IWorkbookReader Open(byte[] content) => new ClosedXmlWorkbookReader(new MemoryStream(content));
}
