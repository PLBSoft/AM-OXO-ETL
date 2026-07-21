namespace ExcelETL.Application.Extraction.Oxo;

public interface IProcessOxoFileService
{
    Task<ProcessOxoFileResult> ProcessAsync(ProcessOxoFileCommand command, CancellationToken cancellationToken = default);
}
