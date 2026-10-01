namespace ExcelETL.Application.Archiving;

// Counterpart of IGeneratedFileWriter: removes an archived file given the path the writer returned
// (relative to the archive root). Returns the bytes freed, 0 when the file was already missing.
public interface IGeneratedFileDeleter
{
    long Delete(string relativePath);
}
