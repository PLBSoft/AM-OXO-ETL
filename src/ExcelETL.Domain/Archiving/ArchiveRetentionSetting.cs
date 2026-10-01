using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Archiving;

// The one configurable retention duration for archived files (Lot 090), editable from BlazorAdmin
// and read by the Web API when it purges. A single row (SingletonId), created lazily by the store:
// when nothing was ever saved, DefaultRetentionDays applies. 0 disables the purge.
public sealed class ArchiveRetentionSetting
{
    public const int SingletonId = 1;
    public const int DefaultRetentionDays = 90;
    public const int MaxRetentionDays = 3650;

    public int Id { get; private set; } = SingletonId;
    public int RetentionDays { get; private set; }

    public ArchiveRetentionSetting(int retentionDays)
    {
        Validate(retentionDays);
        RetentionDays = retentionDays;
    }

    // EF Core materialization only.
    private ArchiveRetentionSetting()
    {
    }

    public void ChangeRetentionDays(int retentionDays)
    {
        Validate(retentionDays);
        RetentionDays = retentionDays;
    }

    public static void Validate(int retentionDays)
    {
        if (retentionDays is < 0 or > MaxRetentionDays)
        {
            throw new DomainArgumentOutOfRangeException(
                nameof(retentionDays), retentionDays,
                $"Retention days must be between 0 and {MaxRetentionDays}.",
                DomainErrorCode.ArchiveRetentionSetting_DaysOutOfRange, 0, MaxRetentionDays);
        }
    }
}
