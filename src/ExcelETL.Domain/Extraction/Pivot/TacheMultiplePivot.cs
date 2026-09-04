using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A row of the PROCEDURE sheet's TacheMultiple block. Action is the sheet's stop-condition field
// (column C:L), so it is always non-empty -- including for a "ligne de mise en page" (EstFactice)
// row, which by definition has a blank Ordre but non-blank Action. Acteur/Risques/TypeTacheMultipleCode
// are left unvalidated because a factice row can legitimately leave them blank -- see
// docs/spec-extraction-fichier-source-oxo-2026-07-16_4.md §1.2.
public sealed record TacheMultiplePivot
{
    public int? Ordre { get; }
    public string Action { get; }
    public string Acteur { get; }
    public string Risques { get; }
    public string TypeTacheMultipleCode { get; }
    public DateOnly? DateValidation { get; }
    public bool EstFactice { get; }

    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md): the
    // row number of this tache in the source PROCEDURE sheet -- unlike Repere/TypeElementNom/
    // ColonneTravaux/Localisation below, this is genuinely known at construction time (it's the same
    // blockStartRow ProcedureExtractionService already computes for the block walk), so it's a required
    // constructor parameter, not a `with`-broadcast init property.
    public int LigneSource { get; }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md): known
    // only after construction (there is exactly one EquipementPivot per run, but this pivot has no
    // notion of it at read time) -- diffused by ImportPipelineOrchestrator via `with { ... }`, same
    // broadcast mechanism as IsolementPivot.Localisation/Tableaux/Applications/RepereParent. All
    // default to "" (a legitimate state: ColonneTravaux stays blank when TypeTacheMultipleCode matches
    // no configured ImportProfile.TacheMultipleTypeLabels entry -- client-confirmed, no error).
    //
    // Localisation (Lot 069) follows the exact same broadcast mechanism -- the Equipement's own zone,
    // known only once the whole Equipement is resolved (DIVERS' loc1 broadcast).
    public string Repere { get; init; }
    public string TypeElementNom { get; init; }
    public string ColonneTravaux { get; init; }
    public string Localisation { get; init; }

    public TacheMultiplePivot(
        int? ordre, string action, string acteur, string risques, string typeTacheMultipleCode,
        DateOnly? dateValidation, bool estFactice, int ligneSource)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainValidationException(
                "Action must not be empty.", nameof(action), DomainErrorCode.TacheMultiplePivot_EmptyAction);
        }

        Ordre = ordre;
        Action = action;
        Acteur = acteur;
        Risques = risques;
        TypeTacheMultipleCode = typeTacheMultipleCode;
        LigneSource = ligneSource;
        Repere = "";
        TypeElementNom = "";
        ColonneTravaux = "";
        Localisation = "";
        DateValidation = dateValidation;
        EstFactice = estFactice;
    }
}
