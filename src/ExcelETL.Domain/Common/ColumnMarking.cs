namespace ExcelETL.Domain.Common;

// Shared by PointColumnDefinition and ApplicationColumnDefinition -- same default mark-cell value,
// no conceptual relation to ProfileNaming above (kept as a separate constant, not merged into a
// catch-all). See each type's own DefaultMarkValue for the redirect that keeps their existing
// public-const access pattern (incl. use as a C# default parameter value) working.
public static class ColumnMarking
{
    public const string DefaultMarkValue = "X";
}
