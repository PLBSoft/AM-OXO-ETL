namespace ExcelETL.Application.Generation;

// Builds the target workbook's file name per the naming convention documented in
// spec-extraction-fichier-source-oxo.md: "MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx". The pivot
// model has no literal "Equipement.Nom" property (see EquipementPivot: Repere/Designation/
// TypeElementNom/Localisation) -- Repere is used here, being the unique "tag" field the glossary
// documents as BaseElement's business search key (glossaire-ef6-legacy-AMAR-ModelCF.md), the closest
// real equivalent to what the spec doc informally calls "Nom".
//
// Moved here from ExcelETL.Infrastructure.Excel at Lot K1: it's pure string logic with no ClosedXML
// dependency, and ProcessOxoFileService (Application) needs to build the archived/downloaded file
// name itself -- Application cannot reference Infrastructure to reuse the original location.
public static class TargetWorkbookFileNameBuilder
{
    public static string Build(string equipementRepere, DateTime generatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipementRepere);

        return $"MAD_{equipementRepere}_{generatedAt:yyyyMMddHHmmss}.xlsx";
    }
}
