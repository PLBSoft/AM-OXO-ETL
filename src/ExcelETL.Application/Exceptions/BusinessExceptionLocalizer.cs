using ExcelETL.Application.Resources;
using ExcelETL.Domain.Exceptions;
using Microsoft.Extensions.Localization;

namespace ExcelETL.Application.Exceptions;

// Shared by every host (WebAPI's GlobalExceptionHandler, BlazorAdmin's Razor components) so the
// "which resource table applies to this exception" dispatch logic lives in exactly one place.
public sealed class BusinessExceptionLocalizer(
    IStringLocalizer<DomainErrorMessages> domainLocalizer,
    IStringLocalizer<ApplicationMessages> applicationLocalizer)
{
    // Returns null when the exception isn't one of ours (IHasDomainErrorCode/IHasApplicationErrorCode) --
    // callers fall back to a generic message or let the exception propagate in that case.
    public string? TryLocalize(Exception exception)
    {
        var (resourceKey, args, localizer) = exception switch
        {
            IHasDomainErrorCode domainError =>
                (domainError.ResourceKey, domainError.Args, (IStringLocalizer)domainLocalizer),
            IHasApplicationErrorCode applicationError =>
                (applicationError.ResourceKey, applicationError.Args, (IStringLocalizer)applicationLocalizer),
            _ => (null, null, null)
        };

        return resourceKey is null ? null : localizer![resourceKey, (object[])args!.ToArray()].Value;
    }
}
