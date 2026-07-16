namespace ExcelETL.Application.Exceptions;

// One member per distinct user-facing business error raised by the Application layer. Each name
// doubles as the resource key that ApplicationMessages.resx / .fr.resx uses to translate it at
// the WebAPI/BlazorAdmin boundary -- mirrors ExcelETL.Domain.Exceptions.DomainErrorCode.
public enum ApplicationErrorCode
{
    ExtractionConfigNotFound,
    ExtractionResult_SheetNotFound,
    ExtractionResult_PropertyNotFound,
    SheetNotFoundInExtractionConfig,
    ExtractionHistoryNotFound,
    WorksheetNotFoundInUploadedWorkbook,
    GeneratedWorkbookSheetCountOutOfRange,
    UnknownFieldReference
}
