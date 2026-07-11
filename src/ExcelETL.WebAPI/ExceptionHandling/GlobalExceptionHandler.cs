using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.ExceptionHandling;

// Translates the business exceptions localized in Milestones 1-3 (Domain rule/validation
// violations, Application not-found/state errors) into a ProblemDetails response using the
// culture negotiated by RequestLocalizationOptions (Accept-Language -- see Program.cs). Anything
// else falls through (returns false) to the default developer/problem-details middleware: this
// handler is deliberately narrow to exception types that actually carry a resource key.
public sealed class GlobalExceptionHandler(
    BusinessExceptionLocalizer businessExceptionLocalizer,
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var detail = businessExceptionLocalizer.TryLocalize(exception);
        if (detail is null)
        {
            return false;
        }

        var statusCode = StatusCodeFor(exception);

        logger.LogWarning(exception, "Handled {ExceptionType}, mapped to HTTP {StatusCode}",
            exception.GetType().Name, statusCode);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = statusCode, Detail = detail }
        });
    }

    private static int StatusCodeFor(Exception exception) => exception switch
    {
        ExtractionConfigNotFoundException
            or SheetNotFoundInExtractionConfigException
            or ExtractionHistoryNotFoundException
            or ExtractionResultLookupException => StatusCodes.Status404NotFound,
        DomainValidationException
            or DomainArgumentOutOfRangeException
            or WorksheetNotFoundInWorkbookException => StatusCodes.Status400BadRequest,
        DomainRuleViolationException
            or InvalidGeneratedWorkbookSheetCountException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
