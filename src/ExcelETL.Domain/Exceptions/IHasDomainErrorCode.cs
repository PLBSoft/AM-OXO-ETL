namespace ExcelETL.Domain.Exceptions;

// Framework-free contract (no dependency on any localization library) implemented by every
// Domain exception that carries a DomainErrorCode. Lets WebAPI's GlobalExceptionHandler resolve
// a localized message via ExcelETL.Application.Resources.DomainErrorMessages without the handler
// needing to know each concrete exception type.
public interface IHasDomainErrorCode
{
    string ResourceKey { get; }

    IReadOnlyList<object?> Args { get; }
}
