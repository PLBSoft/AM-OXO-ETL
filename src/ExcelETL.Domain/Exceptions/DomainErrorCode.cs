namespace ExcelETL.Domain.Exceptions;

// One member per distinct validation/business-rule failure raised by the Domain layer. Each name
// doubles as the resource key that ExcelETL.Application.Resources.DomainErrorMessages uses to
// translate it at the WebAPI/BlazorAdmin boundary -- see the DomainValidationException family.
public enum DomainErrorCode
{
    CellMapping_InvalidSourceCell,
    CellMapping_EmptyTargetPropertyName,
    SheetConfig_EmptySheetName,
    SheetConfig_NegativeSheetIndex,
    SheetConfig_DuplicateCellMapping,
    ExtractionConfig_EmptyName,
    ExtractionConfig_TooManySheets,
    ExtractionConfig_DuplicateSheetIndex,
    ExtractionHistory_EmptySourceFileName,
    ExtractionHistory_EmptyStoredFilePath,
    ExtractionHistory_CannotCompleteFromStatus,
    ExtractionHistory_CannotFailFromStatus
}
