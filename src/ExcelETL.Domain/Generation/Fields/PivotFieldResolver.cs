using System.Globalization;
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
            or PivotFieldRef.EquipementLocalisation
            or PivotFieldRef.EquipementTableaux => PivotSource.Equipement,
        PivotFieldRef.IsolementRepere
            or PivotFieldRef.IsolementDesignation
            or PivotFieldRef.IsolementTypeElementNom
            or PivotFieldRef.IsolementPositionALaPose
            or PivotFieldRef.IsolementLocalisation
            or PivotFieldRef.IsolementTableaux
            or PivotFieldRef.IsolementRepereParent => PivotSource.Isolement,
        PivotFieldRef.TacheMultipleOrdre
            or PivotFieldRef.TacheMultipleAction
            or PivotFieldRef.TacheMultipleActeur
            or PivotFieldRef.TacheMultipleRisques
            or PivotFieldRef.TacheMultipleDateValidation => PivotSource.TacheMultiple,
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
        PivotFieldRef.EquipementTableaux => string.Join(", ", equipement.Tableaux),
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
        PivotFieldRef.IsolementTableaux => string.Join(", ", isolement.Tableaux),
        PivotFieldRef.IsolementRepereParent => isolement.RepereParent,
        _ => throw new InvalidOperationException(
            $"Pivot field '{fieldRef}' is not valid for an Isolement row. This should have been rejected at profile construction.")
    };

    // DateValidation is formatted "dd/MM/yyyy" for consistency with ProcedureExtractionService's own
    // date rendering (see DateRevision) -- invariant culture, not the reader's/host's negotiated culture.
    public static string Resolve(TacheMultiplePivot tacheMultiple, PivotFieldRef fieldRef) => fieldRef switch
    {
        PivotFieldRef.TacheMultipleOrdre => tacheMultiple.Ordre?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        PivotFieldRef.TacheMultipleAction => tacheMultiple.Action,
        PivotFieldRef.TacheMultipleActeur => tacheMultiple.Acteur,
        PivotFieldRef.TacheMultipleRisques => tacheMultiple.Risques,
        PivotFieldRef.TacheMultipleDateValidation => tacheMultiple.DateValidation?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
        _ => throw new InvalidOperationException(
            $"Pivot field '{fieldRef}' is not valid for a TacheMultiple row. This should have been rejected at profile construction.")
    };
}
