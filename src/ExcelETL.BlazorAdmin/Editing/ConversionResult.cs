namespace ExcelETL.BlazorAdmin.Editing;

// Result of converting one draft (or draft subtree) into its real Domain value. Deliberately never
// localizes anything (see the mapper types' own header comments) -- a caller with access to the
// page's injected BusinessExceptionLocalizer applies each Errors[i].Exception to its own
// Errors[i].Draft (an IDraftWithError) wherever it needs to be displayed.
public sealed record ConversionResult<T>(T? Value, IReadOnlyList<DraftConversionError> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static ConversionResult<T> Success(T value) => new(value, []);

    public static ConversionResult<T> Failure(IReadOnlyList<DraftConversionError> errors) => new(default, errors);

    public static ConversionResult<T> Failure(object draft, Exception exception) =>
        new(default, [new DraftConversionError(draft, exception)]);
}
