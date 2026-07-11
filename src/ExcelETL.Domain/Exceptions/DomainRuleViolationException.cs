namespace ExcelETL.Domain.Exceptions;

// See DomainValidationException for why this carries an ErrorCode/Args pair instead of depending
// on a localization framework directly.
public class DomainRuleViolationException(string message, DomainErrorCode errorCode, params object?[] args)
    : InvalidOperationException(message), IHasDomainErrorCode
{
    public DomainErrorCode ErrorCode { get; } = errorCode;

    public IReadOnlyList<object?> Args { get; } = args;

    public string ResourceKey => ErrorCode.ToString();
}
