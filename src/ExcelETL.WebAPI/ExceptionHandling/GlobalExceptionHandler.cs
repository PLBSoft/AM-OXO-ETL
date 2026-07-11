using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Resources;
using ExcelETL.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ExcelETL.WebAPI.ExceptionHandling;

// Translates the business exceptions localized in Milestones 1-3 (Domain rule/validation
// violations, Application not-found/state errors) into a ProblemDetails response using the
// culture negotiated by RequestLocalizationOptions (Accept-Language -- see Program.cs). Anything
// else falls through (returns false) to the default developer/problem-details middleware: this
// handler is deliberately narrow to exception types that actually carry a resource key.
public sealed class GlobalExceptionHandler(
    IStringLocalizer<DomainErrorMessages> domainLocalizer,
    IStringLocalizer<ApplicationMessages> applicationLocalizer,
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (resourceKey, args, localizer) = exception switch
        {
            IHasDomainErrorCode domainError =>
                (domainError.ResourceKey, domainError.Args, (IStringLocalizer)domainLocalizer),
            IHasApplicationErrorCode applicationError =>
                (applicationError.ResourceKey, applicationError.Args, (IStringLocalizer)applicationLocalizer),
            _ => (null, null, null)
        };

        if (resourceKey is null)
        {
            return false;
        }

        var statusCode = StatusCodeFor(exception);
        var detail = localizer![resourceKey, (object[])args!.ToArray()].Value;

        logger.LogWarning(exception, "Handled {ExceptionType} ({ResourceKey}), mapped to HTTP {StatusCode}",
            exception.GetType().Name, resourceKey, statusCode);

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
