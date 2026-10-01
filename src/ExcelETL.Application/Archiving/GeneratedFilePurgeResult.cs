namespace ExcelETL.Application.Archiving;

public sealed record GeneratedFilePurgeResult(int RecordsPurged, int FilesDeleted, long BytesFreed)
{
    public static GeneratedFilePurgeResult None { get; } = new(0, 0, 0);
}
