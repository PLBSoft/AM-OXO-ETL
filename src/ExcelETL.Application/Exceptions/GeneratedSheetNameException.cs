namespace ExcelETL.Application.Exceptions;

// Lot 080.5 (docs/tickets/tickets-tdd-lot-080-validation-noms-feuilles-profil-export.md, D4/D5): a sheet name
// produced by SheetGenerationEngine that ClosedXML would refuse -- a task code clashing with another sheet, or a
// profile saved before lot 080 that the domain would now reject. Mapped to 422 by the WebAPI: the imported file
// and the export profile are incompatible, neither a malformed request nor a server failure.
public sealed class GeneratedSheetNameException : Exception, IHasApplicationErrorCode
{
    private GeneratedSheetNameException(ApplicationErrorCode errorCode, string sheetName, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        SheetName = sheetName;
    }

    public ApplicationErrorCode ErrorCode { get; }

    public string SheetName { get; }

    public IReadOnlyList<object?> Args => [SheetName];

    public string ResourceKey => ErrorCode.ToString();

    public static GeneratedSheetNameException Invalid(string sheetName) =>
        new(ApplicationErrorCode.GeneratedSheetNameInvalid, sheetName, $"Generated sheet name '{sheetName}' is refused by Excel.");

    public static GeneratedSheetNameException Conflict(string sheetName) =>
        new(ApplicationErrorCode.GeneratedSheetNameConflict, sheetName,
            $"Generated sheet name '{sheetName}' is used by more than one sheet (case ignored).");
}
