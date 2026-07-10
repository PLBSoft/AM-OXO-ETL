namespace ExcelETL.Application.Extraction;

public interface IProcessExcelFileService
{
    Task<ProcessExcelFileResult> ProcessAsync(ProcessExcelFileCommand command, CancellationToken cancellationToken = default);
}
