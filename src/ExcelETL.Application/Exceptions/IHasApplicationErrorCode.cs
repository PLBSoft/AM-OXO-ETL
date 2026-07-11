namespace ExcelETL.Application.Exceptions;

// Same purpose as ExcelETL.Domain.Exceptions.IHasDomainErrorCode, for exceptions carrying an
// ApplicationErrorCode instead. Resolved by WebAPI's GlobalExceptionHandler via
// ExcelETL.Application.Resources.ApplicationMessages.
public interface IHasApplicationErrorCode
{
    string ResourceKey { get; }

    IReadOnlyList<object?> Args { get; }
}
