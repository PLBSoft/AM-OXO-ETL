using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Resources;
using ExcelETL.Infrastructure.Excel;
using ExcelETL.WebAPI.Contracts;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/oxo")]
public class OxoController(
    IProcessOxoFileService processOxoFileService,
    IStringLocalizer<ApplicationMessages> localizer,
    ILogger<OxoController> logger)
    : ControllerBase
{
    private const string GeneratedFileContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpPost("process")]
    [RequestSizeLimit(UploadLimits.MaxExcelFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxExcelFileSizeBytes)]
    [RequestTimeout(UploadLimits.ExcelProcessingTimeoutPolicy)]
    public async Task<IActionResult> Process(
        [FromForm] ProcessOxoFileRequest request, CancellationToken cancellationToken)
    {
        // Lot 036.1: checked before anything else, including the file check below -- a
        // structurally-missing identifier is a malformed request, not the same condition as a
        // syntactically-valid Guid matching no profile (404, via ImportProfileNotFoundException/
        // ExportProfileNotFoundException further down). Each gets its own message rather than a
        // merged "missing parameters" one, since either can be missing independently; when both are
        // missing, only the first one checked (ImportProfileId) is reported.
        if (request.ImportProfileId is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = localizer["ImportProfileIdRequired"]
            });
        }

        if (request.ExportProfileId is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = localizer["ExportProfileIdRequired"]
            });
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = localizer["EmptyFileUploadRequired"]
            });
        }

        // Upload log: file name/size and the caller's source IP. Reaches SystemLogs via the same
        // ILogger<T> -> Serilog MSSqlServer sink pipeline as every other log call in this host (see Lot G3).
        logger.LogInformation(
            "OXO upload: {SourceFileName} ({FileSizeBytes} bytes) from {RemoteIpAddress}",
            request.File.FileName, request.File.Length, HttpContext.Connection.RemoteIpAddress);

        await using var fileStream = request.File.OpenReadStream();
        using var bufferedContent = new MemoryStream();
        await fileStream.CopyToAsync(bufferedContent, cancellationToken);
        var sourceFileContent = bufferedContent.ToArray();

        // Lot 036.2: a non-Excel/corrupted byte stream makes XLWorkbook's own constructor throw
        // System.IO.FileFormatException -- a bare BCL type, not an IHasDomainErrorCode/
        // IHasApplicationErrorCode exception, so GlobalExceptionHandler's BusinessExceptionLocalizer
        // would never pick it up (TryLocalize returns null for it, same as any unrecognized
        // exception, falling through to the framework's default 500). Caught explicitly here
        // instead, at the same controller level as the pre-existing empty-file check, and
        // translated into a 400 rather than an unqualified 500.
        ClosedXmlWorkbookReader workbookReader;
        try
        {
            workbookReader = new ClosedXmlWorkbookReader(new MemoryStream(sourceFileContent));
        }
        catch (FileFormatException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = localizer["InvalidExcelFileFormat"]
            });
        }

        using var _ = workbookReader;
        var command = new ProcessOxoFileCommand(
            request.ImportProfileId.Value, request.ExportProfileId.Value, workbookReader, request.File.FileName,
            sourceFileContent);

        // ImportProfileNotFoundException/ExportProfileNotFoundException and any other business
        // exception are not caught here: GlobalExceptionHandler translates them into a localized
        // ProblemDetails response.
        var result = await processOxoFileService.ProcessAsync(command, cancellationToken);

        if (result.ImportResult.Equipement is null)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = localizer["OxoFileRejected"]
            };
            problemDetails.Extensions["errors"] = result.ImportResult.Errors
                .Select(error => new { error.Sheet, error.BlockIdentifier, Code = error.Code.ToString(), error.Message })
                .ToList();

            logger.LogInformation(
                "OXO egress: {SourceFileName} rejected, HTTP {StatusCode}",
                request.File.FileName, StatusCodes.Status422UnprocessableEntity);

            return UnprocessableEntity(problemDetails);
        }

        logger.LogInformation(
            "OXO egress: {SourceFileName} -> {GeneratedFileName}, HTTP {StatusCode}",
            request.File.FileName, result.GeneratedFileName, StatusCodes.Status200OK);

        return new FileStreamResult(result.GeneratedFileStream!, GeneratedFileContentType)
        {
            FileDownloadName = result.GeneratedFileName
        };
    }
}
