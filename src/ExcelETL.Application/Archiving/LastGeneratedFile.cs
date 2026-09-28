using ExcelETL.Domain.Archiving;

namespace ExcelETL.Application.Archiving;

// The most recent archived file, as the home page's "Dernière génération" tile shows it: only these
// four columns are read (see IGeneratedFileArchiveStore.GetSummaryAsync), never the whole
// GeneratedFileRecord. EquipementRepere is null for a rejected file, Username when the caller sent none.
public sealed record LastGeneratedFile(
    DateTime GeneratedAtUtc,
    string? EquipementRepere,
    string? Username,
    GeneratedFileArchiveStatus Status);
