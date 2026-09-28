using ExcelETL.Domain.Archiving;

namespace ExcelETL.Application.Archiving;

// Lot 088 (088.1): the two columns the home page's recent-activity tile and chart need -- read on
// their own, never the whole GeneratedFileRecord (see IGeneratedFileArchiveStore.GetActivitySinceAsync).
public sealed record GeneratedFileActivityEntry(DateTime GeneratedAtUtc, GeneratedFileArchiveStatus Status);
