namespace ExcelETL.BlazorAdmin.Excel;

// Lot 033: shared by ImportProfileTest.razor/ExportProfileTest.razor -- MaxTotalBatchSizeBytes is
// deliberately derived from the other two, not an independent constant, so the two base limits can
// never drift out of sync with a separately-maintained total.
public static class BatchUploadLimits
{
    public const int MaxFilesPerBatch = 20;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    public const long MaxTotalBatchSizeBytes = MaxFilesPerBatch * MaxFileSizeBytes;
}
