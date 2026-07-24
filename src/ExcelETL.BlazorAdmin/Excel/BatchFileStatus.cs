namespace ExcelETL.BlazorAdmin.Excel;

// Lot 033: one of 4 mutually exclusive outcomes for a single file within a batch upload, shared by
// ImportProfileTest.razor/ExportProfileTest.razor. TechnicalError is distinct from Rejected: the
// latter is the pre-existing business rejection (ImportResult.Equipement is null), the former is an
// unhandled exception thrown while reading/processing the file itself (corrupt/non-Excel content).
public enum BatchFileStatus
{
    Ok,
    Warning,
    Rejected,
    TechnicalError
}
