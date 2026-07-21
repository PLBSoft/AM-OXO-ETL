namespace ExcelETL.WebAPI;

internal static class UploadLimits
{
    // 10 MB is comfortably above any realistic form-style Excel workbook and matches the client-side
    // cap BlazorAdmin's ImportProfileTest.razor/ExportProfileTest.razor pages enforce (MaxFileSizeBytes)
    // -- kept identical so those pages exercise the server's real limit, not a looser one.
    public const long MaxExcelFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public const string ExcelProcessingTimeoutPolicy = "ExcelProcessing";
    public static readonly TimeSpan ExcelProcessingTimeout = TimeSpan.FromMinutes(5);
}
