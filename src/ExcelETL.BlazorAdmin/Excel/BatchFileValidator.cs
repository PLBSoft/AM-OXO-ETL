using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

namespace ExcelETL.BlazorAdmin.Excel;

public enum BatchValidationFailureReason
{
    TooManyFiles,
    FileTooLarge,
    TotalSizeTooLarge
}

public sealed record BatchValidationFailure(
    BatchValidationFailureReason Reason,
    int SelectedFileCount,
    IReadOnlyList<IBrowserFile> OversizedFiles,
    long TotalSizeBytes);

// Lot 033: shared by ImportProfileTest.razor/ExportProfileTest.razor. Split in two because the file
// count has to be checked against InputFileChangeEventArgs.FileCount *before* the caller can safely
// call GetMultipleFiles(BatchUploadLimits.MaxFilesPerBatch) -- that method itself throws if the real
// count exceeds the maxAllowedFiles argument, so a page must reject on count first, then only obtain
// the file list (for the size checks) once the count is already known to be within bounds. Pure/no
// localization here: callers build the actual user-facing message via their own IStringLocalizer.
public static class BatchFileValidator
{
    public static BatchValidationFailure? ValidateCount(int fileCount) =>
        fileCount > BatchUploadLimits.MaxFilesPerBatch
            ? new BatchValidationFailure(BatchValidationFailureReason.TooManyFiles, fileCount, [], 0)
            : null;

    public static BatchValidationFailure? ValidateSizes(IReadOnlyList<IBrowserFile> files)
    {
        var oversizedFiles = files.Where(f => f.Size > BatchUploadLimits.MaxFileSizeBytes).ToList();
        if (oversizedFiles.Count > 0)
        {
            return new BatchValidationFailure(
                BatchValidationFailureReason.FileTooLarge, files.Count, oversizedFiles, 0);
        }

        var totalSize = files.Sum(f => f.Size);
        return totalSize > BatchUploadLimits.MaxTotalBatchSizeBytes
            ? new BatchValidationFailure(BatchValidationFailureReason.TotalSizeTooLarge, files.Count, [], totalSize)
            : null;
    }

    public static string FormatMegabytes(long bytes) =>
        (bytes / 1024.0 / 1024.0).ToString("0.##", CultureInfo.InvariantCulture);
}
