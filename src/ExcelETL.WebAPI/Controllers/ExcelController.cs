using ExcelETL.Application.Extraction;
using ExcelETL.Application.Resources;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/excel")]
public class ExcelController(
    IProcessExcelFileService processExcelFileService, IStringLocalizer<ApplicationMessages> localizer)
    : ControllerBase
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
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = localizer["EmptyFileUploadRequired"]
            });
        }

        await using var fileStream = request.File.OpenReadStream();
        var command = new ProcessExcelFileCommand(request.ExtractionConfigId, fileStream, request.File.FileName);

        // ExtractionConfigNotFoundException and any other business exception are not caught here:
        // GlobalExceptionHandler translates them into a localized ProblemDetails response.
        var result = await processExcelFileService.ProcessAsync(command, cancellationToken);

        return new FileStreamResult(result.GeneratedFileStream, GeneratedFileContentType)
        {
            FileDownloadName = result.GeneratedFileName
        };
    }
}
