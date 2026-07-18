using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Domain.Generation.Fields;

// Typed field access for the generation engine (Application/Generation, I3) -- no reflection.
// GetPivotSource is the single source of truth for which PivotSource a PivotFieldRef belongs to (its
// own EquipementXxx/IsolementXxx name prefix); SheetGenerationRule (I1) calls it to reject an
// incompatible PivotSource/PivotFieldRef pairing at profile-construction time, before any file is
// ever generated -- see docs/tickets-tdd-ecriture-fichier-cible.md I2.
public static class PivotFieldResolver
{
    public static PivotSource GetPivotSource(PivotFieldRef fieldRef) => fieldRef switch
    {
        PivotFieldRef.EquipementRepere
            or PivotFieldRef.EquipementDesignation
            or PivotFieldRef.EquipementTypeElementNom
            or PivotFieldRef.EquipementLocalisation => PivotSource.Equipement,
        PivotFieldRef.IsolementRepere
            or PivotFieldRef.IsolementDesignation
            or PivotFieldRef.IsolementTypeElementNom
            or PivotFieldRef.IsolementPositionALaPose
            or PivotFieldRef.IsolementLocalisation => PivotSource.Isolement,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldRef), fieldRef, "Unknown pivot field reference.")
    };

    // The two Resolve overloads below should only ever be called with a fieldRef already confirmed
    // compatible by SheetGenerationRule's constructor-time check (GetPivotSource) -- the default arms
    // are a developer-invariant safety net (plain BCL exception, no DomainErrorCode, out of i18n scope
    // per CLAUDE.md), not a user-reachable failure path.
    public static string Resolve(EquipementPivot equipement, PivotFieldRef fieldRef) => fieldRef switch
    {
        PivotFieldRef.EquipementRepere => equipement.Repere,
        PivotFieldRef.EquipementDesignation => equipement.Designation,
        PivotFieldRef.EquipementTypeElementNom => equipement.TypeElementNom,
        PivotFieldRef.EquipementLocalisation => equipement.Localisation,
        _ => throw new InvalidOperationException(
            $"Pivot field '{fieldRef}' is not valid for an Equipement row. This should have been rejected at profile construction.")
    };

    public static string Resolve(IsolementPivot isolement, PivotFieldRef fieldRef) => fieldRef switch
    {
        PivotFieldRef.IsolementRepere => isolement.Repere,
        PivotFieldRef.IsolementDesignation => isolement.Designation,
        PivotFieldRef.IsolementTypeElementNom => isolement.TypeElementNom,
        PivotFieldRef.IsolementPositionALaPose => isolement.PositionALaPose,
        PivotFieldRef.IsolementLocalisation => isolement.Localisation,
        _ => throw new InvalidOperationException(
            $"Pivot field '{fieldRef}' is not valid for an Isolement row. This should have been rejected at profile construction.")
    };
}
