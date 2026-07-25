namespace ExcelETL.Application.Archiving;

// Archives a raw file stream (source or generated target workbook) to disk, distinct from
// IWorkbookReader/IWorkbookWriter which read/write ClosedXML workbooks in memory -- this interface
// never touches Excel structure, it only persists bytes. Returns the path written, relative to the
// implementation's own configured root (never an absolute path -- see GeneratedFileRecord.
// SourceFilePath/TargetFilePath, which must stay portable if the root ever changes).
public interface IGeneratedFileWriter
{
    Task<string> WriteSourceAsync(
        Stream content, string originalFileName, DateTime timestampUtc, CancellationToken cancellationToken = default);

    Task<string> WriteTargetAsync(
        Stream content, string originalFileName, DateTime timestampUtc, CancellationToken cancellationToken = default);
}
