namespace ExcelETL.WebAPI;

internal static class UploadLimits
{
    // Excel workbooks with many merged-cell forms can be large, and processing is synchronous;
    // limits are set generously so legitimate uploads are not dropped mid-transfer.
    public const long MaxExcelFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    public const string ExcelProcessingTimeoutPolicy = "ExcelProcessing";
    public static readonly TimeSpan ExcelProcessingTimeout = TimeSpan.FromMinutes(5);
}
