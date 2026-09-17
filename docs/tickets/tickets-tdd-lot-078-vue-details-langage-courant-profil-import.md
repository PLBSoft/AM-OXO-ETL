# Tickets TDD — Lot 078 : vue "Détails" en langage courant du profil d'import

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Prolonge le
travail du lot 048 (règles d'en-tête) et de `tickets-tdd-blazor-profil-import-edition-listes-conditionnelles.md`
(édition des sous-listes `UnconditionalColonneNames`/`ConditionalPointRule`) : ces lots ont rendu
les règles éditables et visibles techniquement (`<details>` du 48.6), mais dans le vocabulaire du
domaine (`SheetExtractionRule`, `StripReperePrefix`, plages Excel brutes). Ce lot ajoute une
**deuxième vue, séparée**, destinée à un utilisateur qui n'est ni développeur ni expert de
l'application : une reformulation en langage courant des mêmes règles.*

**Objet** : page de lecture seule `/import-profiles/{id}/details`, accessible depuis
`ImportProfiles.razor` par un nouveau bouton `#import-profile-details-button-{id}`, qui traduit
paramètres généraux + règles de chaque feuille en phrases métier. Ne remplace **rien** d'existant
— le `<details>` technique du lot 048 (§48.6) reste en place tel quel.

**Hors périmètre explicite de ce lot** :
- `ExportProfile` — lot séparé, après validation de l'approche sur l'import (accord déjà donné).
- Toute modification du domaine (`ImportProfile`, `SheetExtractionRule`, etc.) — ce lot est
  strictement présentation, aucune règle n'est reconstruite ni revalidée différemment.
- Toute modification de l'éditeur (`ImportProfileEditor.razor`/`SheetRuleForm.razor`) ou du
  `<details>` du 48.6 — nouvelle page, nouveau composant, zéro retouche du code existant.

---

## 78.0. Investigation préalable (obligatoire avant tout code — pas de sous-ticket rouge/vert avant validation)

**Nature de ce ticket** : il ne produit pas de code de production. Il produit une **note de
conception** (ajoutée en conclusion de cette section, même format que les investigations
précédentes du dépôt — voir `tickets-tdd-blazor-profil-import-edition-listes-conditionnelles.md`
§"Conclusions de l'investigation") qui sera validée avant que les sous-tickets 78.1+ soient
rédigés. Ne pas anticiper l'implémentation.

**À confirmer par lecture directe du dépôt réel** (ne pas supposer que les descriptions
ci-dessous, issues des tickets historiques, sont encore exactes — le dépôt a probablement avancé
au-delà de ce que ce contexte reflète) :

1. **Inventaire exhaustif des champs à traduire**, avec leur forme réelle actuelle :
   - Racine `ImportProfile` : `Name`, `ReperePrefix`, `EquipementTypeElementNom`.
   - `SheetExtractionRule` : `UnconditionalColonneNames`, `ConditionalPointRule`
     (`SourceFieldName`, `Operator`, `ComparisonValue`, `ColonneName`), `HeaderFieldRule`
     (`Name`, `Cell` (`DirectCell.Sheet`/`.Range`), `StripReperePrefix`, `DateFormat`),
     `HeaderCompositeRule` (`Name`, `Template`, `PlaceholderNames()`).
   - **Décision à confirmer, pas à supposer** : `RepeatingBlockLocator` et `BlockFieldDefinition`
     (mécanique d'extraction des blocs répétitifs) entrent-ils dans le périmètre de la vue
     Détails, ou restent-ils un détail d'implémentation trop technique pour l'utilisateur cible
     (probable, à faire trancher explicitement plutôt que d'omettre silencieusement) ?
   - Lister les valeurs réelles de l'énumération `ConditionOperator` (le glossaire et les tickets
     historiques ne mentionnent que `Equals`/`NotEquals` — confirmer qu'il n'y en a pas d'autre
     avant de figer les gabarits de phrase).

2. **Conventions UI existantes à réutiliser** : lire `convention-ui-blazor-icones-boutons.md` et
   `convention-ui-blazor-alignement-boutons.md` pour le bouton `#import-profile-details-button-{id}`
   sur `ImportProfiles.razor` ; confirmer le style de routing des pages en lecture seule existantes
   (vérifier si une page 100 % lecture seule existe déjà ailleurs dans `BlazorAdmin`).

3. **Dictionnaire de phrases métier** : n'existe pas encore (confirmé). Proposer :
   - son emplacement (probablement `ExcelETL.BlazorAdmin`, pas `Application` — c'est de la
     présentation, pas une règle métier réutilisable côté API) ;
   - sa forme (classe statique de gabarits + clés `.resx` paramétrées façon
     `…_WellKnownHeaderNamesMissingWarning`, ou un service dédié type
     `ImportProfileDescriptionBuilder`) ;
   - comment il gère les deux ressources EN/FR (même contrainte que le 48.7).

4. **Algorithme de regroupement des `ConditionalPointRule`** : proposer la clé de regroupement
   (`SourceFieldName` + `Operator` + `ComparisonValue`) et le gabarit de phrase résultant pour un
   groupe à N colonnes (N=1 cas simple, N>1 cas SOUPAPE/POINT FEU) — avec un exemple construit sur
   les données réelles du profil OXO actuellement seedé, pas un exemple inventé.

5. **Catalogue de gabarits complet**, feuille par feuille du profil OXO seedé (PROCEDURE,
   ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS), pour validation
   humaine avant d'écrire le moindre test — chaque feuille rendue en phrases complètes, pas en
   pseudo-code de gabarit.

**Livrable attendu de 78.0** : la note de conception ci-dessus complétée avec les réponses
réelles, plus le catalogue de gabarits du point 5 rendu en clair sur les 6 feuilles du profil
OXO. Aucun sous-ticket 78.1+ n'est écrit avant validation explicite de cette note.
