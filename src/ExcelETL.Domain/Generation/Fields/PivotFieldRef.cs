namespace ExcelETL.Domain.Generation.Fields;

// Typed selector for a field exposed by EquipementPivot or IsolementPivot -- no reflection, extended
// one member at a time as real generation needs surface a field to expose, same philosophy as
// ExtractionErrorCode on the import side. Compatibility with a given PivotSource (Equipement vs
// Isolement) is cross-validated at profile-construction time -- see PivotFieldResolver.
public enum PivotFieldRef
{
    EquipementRepere,
    EquipementDesignation,
    EquipementTypeElementNom,
    EquipementLocalisation,
    EquipementTableaux,
    IsolementRepere,
    IsolementDesignation,
    IsolementTypeElementNom,
    IsolementPositionALaPose,
    IsolementLocalisation,
    IsolementTableaux,
    IsolementRepereParent,
    TacheMultipleOrdre,
    TacheMultipleAction,
    TacheMultipleActeur,
    TacheMultipleRisques,
    TacheMultipleDateValidation
}
