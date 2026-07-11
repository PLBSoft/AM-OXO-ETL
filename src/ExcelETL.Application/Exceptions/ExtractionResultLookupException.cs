namespace ExcelETL.Application.Exceptions;

// Args preserves the values interpolated into Message so WebAPI/BlazorAdmin can re-format the
// localized ApplicationMessages resource string using ErrorCode instead of parsing Message --
// same convention as the Domain layer's DomainRuleViolationException.
public sealed class ExtractionResultLookupException(string message, ApplicationErrorCode errorCode, params object?[] args)
    : KeyNotFoundException(message), IHasApplicationErrorCode
{
    public ApplicationErrorCode ErrorCode { get; } = errorCode;

    public IReadOnlyList<object?> Args { get; } = args;

    public string ResourceKey => ErrorCode.ToString();
}
