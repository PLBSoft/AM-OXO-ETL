using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.ExceptionHandling;

// Translates the business exceptions localized in Milestones 1-3 (Domain rule/validation
// violations, Application not-found/state errors) into a ProblemDetails response using the
// culture negotiated by RequestLocalizationOptions (Accept-Language -- see Program.cs).
// Lot 065: every other exception reaching this handler (no resource key, would otherwise fall
// through to the framework's own default ProblemDetails response) is also handled here now,
// still as a 500, but with the exception's short type name and message surfaced in the response
// body (Extensions, never Detail -- that property stays reserved for a localized business
// message) so a caller (e.g. /api-test) doesn't have to go spelunking through SystemLogs for a
// technical failure it can't self-diagnose from an empty response. The stack trace is never
// included, by construction: only exception.GetType().Name and exception.Message are read.
// Correctif (2026-09-01): a *mapped* business exception (has a resource key, IHasApplicationErrorCode/
// IHasDomainErrorCode) can still resolve to 500 when StatusCodeFor below has no explicit case for
// it -- e.g. UnknownFieldReferenceException, the real exception that motivated this lot, which had
// no ApplicationMessages resx entry at all, so its Detail was just the raw, unhelpful resource key
// string. The exceptionType/exceptionMessage extensions are therefore attached whenever the final
// status is 500, regardless of which branch below produced it -- not only for a fully unmapped
// exception.
public sealed class GlobalExceptionHandler(
    BusinessExceptionLocalizer businessExceptionLocalizer,
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var detail = businessExceptionLocalizer.TryLocalize(exception);
        var problemDetails = new ProblemDetails();

        if (detail is not null)
        {
            problemDetails.Status = StatusCodeFor(exception);
            problemDetails.Detail = detail;

            logger.LogWarning(exception, "Handled {ExceptionType}, mapped to HTTP {StatusCode}",
                exception.GetType().Name, problemDetails.Status);
        }
        else
        {
            problemDetails.Status = StatusCodes.Status500InternalServerError;

            logger.LogError(exception,
                "Unhandled {ExceptionType} reached the global exception handler, mapped to HTTP {StatusCode}",
                exception.GetType().Name, problemDetails.Status);
        }

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
            problemDetails.Extensions["exceptionMessage"] = exception.Message;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static int StatusCodeFor(Exception exception) => exception switch
    {
        ImportProfileNotFoundException or ExportProfileNotFoundException => StatusCodes.Status404NotFound,
        DomainValidationException
            or DomainArgumentOutOfRangeException
            or WorksheetNotFoundInWorkbookException => StatusCodes.Status400BadRequest,
        DomainRuleViolationException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
