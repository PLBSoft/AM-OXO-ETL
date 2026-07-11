namespace ExcelETL.Domain.Exceptions;

// Domain must not reference any localization framework (see CLAUDE.md's Clean Architecture rule),
// so this only carries the English message plus an ErrorCode and the raw Args that were
// interpolated into it. WebAPI/BlazorAdmin re-format the localized DomainErrorMessages resource
// string using ErrorCode and Args instead of parsing Message.
public class DomainValidationException(string message, string paramName, DomainErrorCode errorCode, params object?[] args)
    : ArgumentException(message, paramName), IHasDomainErrorCode
{
    public DomainErrorCode ErrorCode { get; } = errorCode;

    public IReadOnlyList<object?> Args { get; } = args;

    public string ResourceKey => ErrorCode.ToString();
}
