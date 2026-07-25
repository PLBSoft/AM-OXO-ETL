namespace ExcelETL.Domain.Common;

// Shared by ImportProfile and ExportProfile -- same name-length limit, no relation between the
// two aggregates otherwise. See each type's own MaxNameLength for the redirect that keeps their
// existing public-const access pattern working.
public static class ProfileNaming
{
    public const int MaxNameLength = 60;
}
