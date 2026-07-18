namespace ExcelETL.Infrastructure.Excel;

// Builds the target workbook's file name per the naming convention documented in
// spec-extraction-fichier-source-oxo.md: "MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx". The pivot
// model has no literal "Equipement.Nom" property (see EquipementPivot: Repere/Designation/
// TypeElementNom/Localisation) -- Repere is used here, being the unique "tag" field the glossary
// documents as BaseElement's business search key (glossaire-ef6-legacy-AMAR-ModelCF.md), the closest
// real equivalent to what the spec doc informally calls "Nom".
public static class TargetWorkbookFileNameBuilder
{
    public static string Build(string equipementRepere, DateTime generatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipementRepere);

        return $"MAD_{equipementRepere}_{generatedAt:yyyyMMddHHmmss}.xlsx";
    }
}
