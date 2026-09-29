namespace ExcelETL.Application.Extraction.Oxo;

// Opens the uploaded bytes as an IWorkbookReader (implemented in Infrastructure). Lets
// ProcessOxoFileService open the file itself, so a load failure can be reported and archived like
// any other rejection. Throws System.IO.FileFormatException when the bytes aren't an Excel package,
// UnreadableWorkbookException when they are one the library can't load.
public interface IWorkbookReaderFactory
{
    IWorkbookReader Open(byte[] content);
}
