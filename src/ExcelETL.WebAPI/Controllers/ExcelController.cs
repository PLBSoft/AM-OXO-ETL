using ExcelETL.Application.Extraction;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/excel")]
public class ExcelController(IProcessExcelFileService processExcelFileService) : ControllerBase
{
    private const string GeneratedFileContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpPost("process")]
    [RequestSizeLimit(UploadLimits.MaxExcelFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxExcelFileSizeBytes)]
    [RequestTimeout(UploadLimits.ExcelProcessingTimeoutPolicy)]
    public async Task<IActionResult> Process(
        [FromForm] ProcessExcelFileRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A non-empty .xlsx file must be uploaded.");
        }

        await using var fileStream = request.File.OpenReadStream();
        var command = new ProcessExcelFileCommand(request.ExtractionConfigId, fileStream, request.File.FileName);

        try
        {
            var result = await processExcelFileService.ProcessAsync(command, cancellationToken);

            return new FileStreamResult(result.GeneratedFileStream, GeneratedFileContentType)
            {
                FileDownloadName = result.GeneratedFileName
            };
        }
        catch (ExtractionConfigNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
