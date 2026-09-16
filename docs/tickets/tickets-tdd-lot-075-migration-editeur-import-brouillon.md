# Lot 075 — Migration de l'éditeur de profil d'import sur le brouillon unique possédé par la racine (P3)

**Prérequis** : lot 073 (tests aller-retour, filet boîte noire) et lot 074 (pilote P3 sur l'export,
décision de poursuite confirmée avec Simon le 16/09) livrés.

## Contexte

Le lot 074 a migré l'éditeur d'export sur l'architecture « brouillon unique possédé par la racine »
(P3) : 0 assertion de test existant modifiée, solde de lignes négatif sur les composants migrés,
outils génériques sans code par type, relecture jugée plus simple par Simon. Les 4 critères de
poursuite étant remplis, ce lot applique le même modèle à l'éditeur d'import, là où les défauts A
(« brouillon non validé ») et B (« champ perdu à la reconstruction ») se sont produits le plus souvent
(lot 048.1, commits `1736886`/`0cbac22`, incident `FieldPresencePointRule`/PLATINES du 16/09).

## Décision d'architecture

**Aucune nouvelle discussion d'architecture** : P3 est retenu (voir le ticket 074, sections
« Décision d'architecture » et « Écarté, avec la raison »). Ce lot reproduit le patron du pilote :

- un seul brouillon (`ImportProfileDraft`) possédé par `ImportProfileEditor.razor` ;
- des classes brouillon mutables sans comportement sous `Editing/Import/`, qui reproduisent la forme
  du profil ;
- deux conversions centralisées (`ImportProfileDraftMapper.FromDomain`/`ToDomain`) et des conversions
  unitaires réutilisées par chaque bouton de ligne — **un seul chemin de validation**, qui appelle les
  constructeurs du domaine sans dupliquer de règle ;
- des sous-formulaires qui deviennent de purs lieurs de champs ;
- aucun changement visible (mêmes identifiants, gestes, messages), sauf le message global de ligne en
  attente invalide (même ajout que `ExportProfileEditor_PendingRowInvalidError`).

Les outils génériques du lot 074 (`DraftJson`, `ConversionResult<T>`, `DraftConversionError`,
`IDraftWithError`) sont réutilisés **sans modification**.

## Constats (code au commit `e69c911`)

Aucun commit n'a touché les 6 composants d'import depuis le commit du constat 074 (`2608120`) ; les
chiffres ci-dessous sont néanmoins recomptés, le constat 074 ne donnait que les champs d'état.

1. **Tailles réelles** :

   | Composant | Lignes | Champs d'état | `@ref` de composant | `TryCommitAsync`/`ResetForm`/`OnInitialized` |
   | :--- | ---: | ---: | ---: | :--- |
   | `ImportProfileEditor.razor` | 1 134 | 35 | 2 | — (appelle ceux de `SheetRuleForm`) |
   | `SheetRuleForm.razor` | 1 145 | 39 | 4 | oui / oui / oui, plus `IsBlank` |
   | `BlockFieldForm.razor` | 187 | 5 | 0 | oui / oui / oui |
   | `HeaderFieldRuleForm.razor` | 156 | 5 | 0 | oui / oui / oui |
   | `HeaderCompositeRuleForm.razor` | 151 | 4 | 0 | oui / oui / oui |
   | `FieldPresencePointRuleForm.razor` | 196 | 6 | 0 | oui / oui / oui |
   | **Total** | **2 969** | **94** | **6** | |

   À comparer au pilote export : 1 374 lignes, 5 composants.

2. **Listes éditées, 9 au total** (contre 4 côté export) :
   - **racine, en ligne dans la page** (sans composant enfant) : `DefaultTableaux` (`string`),
     `DefaultApplicationNames` (`string`), `TacheMultipleTypeLabels` (`TacheMultipleTypeLabel`,
     `Code`/`Label`). Chacune a une ligne d'ajout, une édition en ligne avec tampon séparé
     (`_editingXxxValue`) et deux messages d'erreur (ajout, édition). Validation par
     `ImportProfile.ValidateDefaultTableauName`/`ValidateDefaultApplicationName`/
     `ValidateTacheMultipleTypeLabelCode` (l'élément en cours d'édition est exclu de la comparaison
     des doublons) ;
   - **dans `SheetRuleForm`, via 4 composants feuilles** : `Fields` (`BlockFieldForm`),
     `HeaderFields` (`HeaderFieldRuleForm`), `HeaderComposites` (`HeaderCompositeRuleForm`),
     `FieldPresencePointRules` (`FieldPresencePointRuleForm`) ;
   - **dans `SheetRuleForm`, en ligne** (lot W) : `UnconditionalColonneNames` (`string`, ligne
     d'ajout d'un champ + édition compacte d'un champ) et `PointRules` (`ConditionalPointRule`, ligne
     d'ajout de 4 champs pleine largeur + édition compacte de 4 champs sur une ligne).

3. **Champs scalaires de `SheetRuleForm` sans composant dédié** : `SheetName`, `FirstBlockStartRow`,
   `Step`, `StopFieldName`, `ZeroEnergieExpectedValue` (vide ⇒ `null`), `DefaultCouleurEtiquette`
   (vide ⇒ `null`), `CouleurEtiquetteCell` (plage Excel absolue saisie en texte, vide ⇒ `null`, nom
   figé `"CouleurEtiquette"` à la reconstruction), `AllowedCouleursEtiquette` (un seul champ texte
   séparé par des virgules, vide ⇒ `null`).

4. **Plages Excel absolues** : `BlockFieldForm`, `FieldPresencePointRuleForm` et `CouleurEtiquetteCell`
   saisissent une plage absolue, convertie en décalages relatifs via `FirstBlockStartRow`
   (`BlockFieldRangeFormatter`). Aujourd'hui les éléments refermés stockent les **décalages** (le
   domaine) ; l'affichage absolu est recalculé à chaque rendu avec le `FirstBlockStartRow` courant.
   Aucun test ne modifie `FirstBlockStartRow` après avoir ajouté des champs (vérifié par recherche).

5. **Fuites du défaut A côté import, relevées à la lecture** (non reproduites, à confirmer en rouge
   en 075.3) — le mécanisme actuel ne vide que les éditions **ouvertes** des 4 composants feuilles et
   des 2 listes en ligne de `SheetRuleForm`, puis le formulaire de règle ouvert :
   - **L1** — racine : ligne d'ajout `Tableau`/`Application` remplie mais pas ajoutée ⇒ perdue ;
   - **L2** — racine : ligne d'ajout `TacheMultipleTypeLabel` remplie ⇒ perdue ;
   - **L3** — racine : édition en ligne ouverte d'un Tableau/Application/libellé de tâche multiple
     (tampon modifié, bouton ✓ non cliqué) ⇒ perdue ;
   - **L4** — `SheetRuleForm` : lignes d'ajout des 4 composants feuilles remplies mais pas ajoutées
     ⇒ perdues (la validation à la sortie du dernier champ, lot 056.4, l'atténue pour 3 d'entre eux
     mais pas via `Ctrl+Entrée`, et pas du tout pour `HeaderFieldRuleForm`) ;
   - **L5** — `SheetRuleForm` : ligne d'ajout de règle de Point conditionnelle (4 champs, aucune
     validation à la sortie) ⇒ perdue ; ligne d'ajout de colonne inconditionnelle ⇒ perdue via
     `Ctrl+Entrée` ;
   - **L6** — `IsBlank` de `SheetRuleForm` ignore toutes les lignes d'ajout : un formulaire d'ajout de
     règle où seule une ligne en attente est saisie est jugé vide et ignoré en silence.

6. **Messages non issus du domaine** : `ImportProfileEditor_InvalidExcelRangeError` (plage non
   analysable) et `ImportProfileEditor_EmptyColonneNameError` (édition d'une colonne inconditionnelle
   vidée ; l'ajout d'une valeur vide est, lui, ignoré en silence). Le domaine ne valide pas
   `UnconditionalColonneNames`. Côté export, toutes les erreurs venaient du domaine : ce cas est nouveau.

7. **Avertissement non bloquant** « plage au-delà de la zone plausible » (`IsBeyondPracticalRange`)
   affiché en tête du formulaire feuille **après** un ajout réussi (`BlockFieldForm`,
   `FieldPresencePointRuleForm`) ; asserté par `AddBlockField_BeyondPracticalPlausibilityThreshold_...`
   et `BlockField_BeyondPracticalRange_Blur_AddsAndShowsNonBlockingWarning`. Le même avertissement
   posé par `SheetRuleForm` sur `CouleurEtiquetteCell` n'est jamais visible (le formulaire se referme
   au succès) — affichage mort préexistant, conservé tel quel.

8. **Validation à la sortie / Entrée et retour du focus** (lot 056.4) sur `BlockFieldForm`,
   `HeaderCompositeRuleForm`, `FieldPresencePointRuleForm` et la ligne d'ajout de colonne
   inconditionnelle : le focus revient au premier champ **seulement après un ajout réussi**. Un
   composant feuille sans validation propre doit donc savoir, après avoir relayé la soumission, si
   elle a réussi.

9. **Emplacement de l'erreur d'ajout d'une règle de Point** : affichée dans l'alerte **en tête de
   `SheetRuleForm`** (partagée avec les erreurs de la règle elle-même), pas près de la ligne.

10. **Points à préserver** : `HeaderFieldRule.Cell.Sheet` toujours re-dérivé du nom de feuille (lot 048,
    décision 2) ; `FieldPresencePointRule.Cell.Name` d'origine conservé à l'édition (`"FieldPresenceCell"`
    pour un nouveau) ; `ExpectedValue` vide ⇒ `null` ; date de format vide ⇒ `null` ; avertissement des
    noms d'en-tête attendus (`KnownHeaderFieldNames`, lot 048.5) ; lot 043 (confirmation de navigation),
    056 (indicateur, `Ctrl+Entrée`, barre collante), 057 (un seul formulaire de règle ouvert, bascule
    qui valide d'abord), 059 (bouton de sauvegarde désactivé sans modification, validations des noms de
    Tableaux/Applications), 063/067 et couleur d'étiquette.

11. **Tests exposés** (nombre d'attributs `[Fact]`/`[Theory]`) : `ImportProfileEditorTests.cs` (171),
    `ImportProfileEditorLot056Tests.cs` (31), `ImportProfileEditorLot057Tests.cs` (16),
    `ImportProfileEditorLot058Tests.cs` (5), `ImportProfileEditorLot059Tests.cs` (12),
    `ImportProfileEditorLot063Tests.cs` (6), `ImportProfileEditorLot067Tests.cs` (10),
    `ImportProfileEditorCouleurEtiquetteTests.cs` (14), `ImportProfileEditorFieldPresencePointRuleTests.cs`
    (14), `ImportProfileEditorNestedEditFlushTests.cs` (4), `ImportProfileEditorRoundTripTests.cs`
    (lot 073, 4 théories / 72 cas), et partagés avec l'export `ProfileEditorParityTests.cs` (23),
    `FormFloatingStructureAuditTests.cs` (6).

## Décisions de conception

| Sujet | Décision |
| :--- | :--- |
| Emplacement | `src/ExcelETL.BlazorAdmin/Editing/Import/` ; tests en miroir sous `tests/ExcelETL.BlazorAdmin.Tests/Editing/Import/`. |
| Classes brouillon | `ImportProfileDraft` (`Id`, `Name`, `ReperePrefix` = `ImportProfile.DefaultReperePrefix`, `EquipementTypeElementNom`, les 3 listes racine et une ligne en attente par liste, `SheetRules`, `PendingSheetRule`) ; `SheetExtractionRuleDraft` (les 8 scalaires du constat 3 en texte/entier, les 6 listes et une ligne en attente par liste) ; `BlockFieldDefinitionDraft` (`Name`, `AbsoluteRange`) ; `HeaderFieldRuleDraft` (`Name`, `Range`, `DateFormat`, `StripReperePrefix`) ; `HeaderCompositeRuleDraft` (`Name`, `Template`) ; `FieldPresencePointRuleDraft` (`ColonneName`, `AbsoluteRange`, `ExpectedValue`, `CellName`) ; `ConditionalPointRuleDraft` (`ColonneName`, `SourceFieldName`, `Operator` = `Equals`, `ComparisonValue`) ; `TacheMultipleTypeLabelDraft` (`Code`, `Label`). |
| Listes de chaînes nues (Q2) | **Tranché** : un seul type `StringItemDraft { Value }`, partagé par `DefaultTableaux`, `DefaultApplicationNames` et `UnconditionalColonneNames` — une classe au lieu de trois identiques. `DraftJson.IsPristine`/`Clone` (contrainte `new()`) vérifiés sur ce type en 075.1, pas supposés. |
| Listes éditées en ligne (Q1) | **Tranché** : balisage en ligne conservé (racine et lot W), simplement lié aux brouillons. La dette de ces listes est leur état (tampons, index, erreurs, validations recopiées), que P3 supprime de toute façon ; les extraire en composants ajouterait 5 fichiers à une dizaine de paramètres chacun pour reproduire des identifiants et mises en page déjà divergents — plus de code, pas moins. |
| Plages absolues (Q3) | **Tranché avec Simon** : le brouillon stocke la **plage absolue en texte**, source de vérité ; `FromDomain` la calcule avec le `FirstBlockStartRow` chargé, `ToDomain` la convertit avec le `FirstBlockStartRow` courant du brouillon. Modifier `FirstBlockStartRow` garde donc les plages affichées et déplace les décalages (aujourd'hui l'inverse, constat 4, non couvert par un test) — ce que l'administrateur voit correspond au fichier Excel. Seul changement de comportement assumé du lot, en plus du message global. |
| Noms figés | `CellName` de `FieldPresencePointRuleDraft` et `CouleurEtiquetteCellName` de la règle sont portés par le brouillon (champs non affichés) : `null` pour un nouvel élément ⇒ nom par défaut actuel. Supprime deux cas latents de défaut B sans changement visible. |
| Erreurs hors domaine | Nouveau type générique `Editing/DraftValidationException(string ResourceKey)` levé par les conversions pour les messages du constat 6. La page localise : `DraftValidationException` ⇒ `Loc[ResourceKey]`, sinon `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message`. Aucune localisation dans le mapper. |
| Colonne inconditionnelle vide | Conservé : une ligne d'ajout vide (ou blanche) est ignorée au clic « Ajouter » et à la sauvegarde ; une édition vidée échoue avec `ImportProfileEditor_EmptyColonneNameError`. |
| Doublons des listes racine | Conversions unitaires avec contexte : `ConvertDefaultTableau(brouillon, autres)` etc. appellent les validateurs statiques d'`ImportProfile`, l'élément converti exclu des « autres ». `ToDomain` compte en plus sur le constructeur d'`ImportProfile`, qui refait la vérification complète. |
| Avertissement de plage (constat 7) | Porté par une propriété d'affichage `Warning` (`[JsonIgnore]`) des brouillons à plage. Le propriétaire de la liste le calcule après une conversion réussie et le pose sur la nouvelle ligne en attente, que le composant feuille affiche comme aujourd'hui. `ConversionResult<T>` reste inchangé. |
| Validation à la sortie et focus (constat 8) | Le composant feuille relaie la soumission puis rend le focus au premier champ si `Draft.Error` est vide après l'appel (le parent pose l'erreur en cas d'échec). Aucune validation dans le composant feuille. |
| Erreur d'ajout de règle de Point (constat 9) | Portée par `PendingPointRule.Error`, affichée dans la même alerte de tête de `SheetRuleForm` qu'aujourd'hui. |
| Ligne en attente à la sauvegarde | Comme au lot 074 : vierge ⇒ ignorée ; non vierge et valide ⇒ ajoutée en fin de liste ; non vierge et invalide ⇒ sauvegarde bloquée, erreur sur la ligne, message global `ImportProfileEditor_PendingRowInvalidError` (EN/FR, **seul ajout visible**). S'applique aux 10 lignes en attente (3 racine, 1 règle, 6 dans chaque règle). |
| Édition ouverte à la sauvegarde | Liée directement à l'élément de la liste : sa valeur saisie est convertie par `ToDomain` comme tout élément. Invalide ⇒ sauvegarde bloquée, erreur sur l'élément. |
| « Annuler » d'un élément | Copie `DraftJson.Clone` prise à l'ouverture, remise en place à l'annulation. Neuf états d'édition (3 racine, 6 dans la règle) : un petit type générique `Editing/ListItemEditState<TDraft>` (index ouvert + copie ; `Open`, `Cancel`, `Close`) plutôt que neuf paires de champs écrites à la main. |
| Lot 057 | La bascule convertit la règle ouverte par `ImportProfileDraftMapper.ConvertSheetRule` (plus aucun `@ref`) ; échec ⇒ refus. Fermer le formulaire d'ajout par le bouton bascule remet `PendingSheetRule` à neuf (leçon du pilote). |
| Indicateur de modification | Conservé tel quel (`OnDirty`, `_hasUnsavedChanges`), comme au pilote. |
| Cartes de résumé | Rendues depuis le brouillon. Invariant « tout élément refermé est valide » : les formats d'affichage passent par les conversions unitaires (`BlockFieldRangeFormatter`, `FieldPresencePointRuleFormatter`) pour rester identiques à aujourd'hui (plage normalisée). |
| Identifiants HTML et paramètres | Strictement conservés (`IdPrefix`, `SubmitButtonId`, `ShowCancel`, `FirstBlockStartRow`, …). Seul le paramètre de pré-remplissage (`InitialRule`, `InitialField`, …) est remplacé par le brouillon lié. |
| Ce qui disparaît | Dans les 6 composants : les champs d'état de données, les tampons d'édition, les `OnInitialized`, les 5 `TryCommitAsync`, les 5 `ResetForm`, `IsBlank`, les 6 `@ref` de composants et la chaîne de vidage imbriquée. |

## Étapes TDD

### 075.1 — Compléments génériques (effort standard)

- `DraftValidationException` : porte sa clé de ressource.
- `ListItemEditState<TDraft>` : ouverture (copie indépendante), annulation (restaure la copie dans la
  liste à l'index ouvert), fermeture (garde la valeur), suppression d'un élément avant l'index ouvert.
- `DraftJson` sur les nouveaux cas, vérifiés et non supposés : type à une chaîne, énumération
  (`ConditionOperator`), booléen, entier à zéro, `Guid?`.

### 075.2 — Brouillons et `ImportProfileDraftMapper` (effort élevé)

Tests xUnit, sans bUnit :

- **aller-retour** : profil d'import standard semé ⇒ `FromDomain` ⇒ `ToDomain` ⇒ équivalent membre à
  membre, ordre strict, `Id` conservé ; plus le cas construit à la main `FieldPresencePointRule`
  (`ExpectedValue = null`) du lot 073 ;
- brouillon sans `Id` ⇒ nouveau profil ;
- erreur rattachée au bon brouillon : champ de bloc à plage invalide (`DraftValidationException`),
  en-tête vide, règle de Point incomplète, libellé de tâche multiple à code dupliqué, composite à
  espace réservé inconnu (rattaché à la règle) ; plusieurs erreurs toutes remontées ;
- les 10 lignes en attente : vierge ignorée, valide ajoutée en fin de liste, invalide en erreur ;
- `HeaderFieldRule.Cell.Sheet` re-dérivé du nom de feuille ; `CellName`/`CouleurEtiquetteCellName`
  conservés ; `AllowedCouleursEtiquette` découpée ; vide ⇒ `null` sur les 4 optionnels ;
- plages absolues (Q3) : `FirstBlockStartRow` modifié dans le brouillon ⇒ même texte de plage,
  décalages recalculés.

### 075.3 — Rouge : lignes en attente et éditions ouvertes (effort standard)

Nouveau fichier `ImportProfileEditorPendingRowTests.cs`, **sur l'interface actuelle**, un test par fuite
du constat 5 (L1 à L6), chacun via `#save-profile-button` et au moins un via `Ctrl+Entrée` ; plus :
ligne partielle ⇒ `SaveAsync` jamais appelé, erreur de ligne et message global, saisie conservée ;
toutes les lignes vides ⇒ sauvegarde normale ; élément existant vidé en édition ⇒ erreur, pas
d'ancienne valeur restaurée en silence ; modifié puis « Annuler » ⇒ ancienne valeur.

### 075.4 — Vert : migration des 6 composants (effort élevé)

Un seul commit vert (un état mi-brouillon mi-domaine n'est pas cohérent). **Tous les tests du constat 11
passent sans modifier leurs assertions.** Un test qui échoue : s'arrêter, lire le diff ; s'il teste un
détail interne plutôt qu'un comportement, le signaler dans « Résultat » avec la raison.

### 075.5 — Bilan (effort élevé)

Remplir « Résultat » : `git diff --stat` de 075.4, champs d'état/`@ref`/méthodes restants contre le
constat 1, assertions modifiées (et pourquoi), points à toucher pour ajouter un champ à
`FieldPresencePointRule` avant/après, difficultés rencontrées. Conclusion sur le bilan global P3
(import + export).

### 075.6 — Documentation

- Réécrire `docs/conventions/recommandations-tickets-tdd.md` §9 : la règle provisoire du lot 073 est
  remplacée par la règle des brouillons (tout champ passe par les conversions, gardées par leur test
  aller-retour ; plus de chaîne de vidage à brancher).
- Mettre à jour `CLAUDE.md` (« CURRENT SOLUTION STATE »).

## Refactor à considérer

- Adopter `ListItemEditState<TDraft>` et l'application générique des erreurs côté export, si le
  gain est net et que les tests export restent verts sans modification.
- Indicateur de modification par comparaison du brouillon sérialisé (voir le ticket 074), dans les
  mêmes conditions.

## Hors périmètre explicite

- Tout changement d'interface autre que le message global (et le comportement de Q3) : pas de nouveau
  composant pour les listes en ligne, pas de changement de libellé, classe, identifiant, emplacement
  d'erreur.
- Le domaine, la persistance (`IImportProfileStore`, EF Core), le seeder.
- Les pages de test d'import et la liste des profils d'import.
- L'éditeur d'export, hors « Refactor à considérer ».

## Notes d'exécution

- Tests filtrés pendant l'itération :
  `dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~Editing|FullyQualifiedName~ImportProfileEditor|FullyQualifiedName~ProfileEditorParity|FullyQualifiedName~FormFloatingStructureAudit" --verbosity quiet`.
  Clôture de 075.4 : `ExcelETL.BlazorAdmin.Tests` complet (seul projet touché).
- Un commit par sous-ticket ; 075.4 est volontairement un seul commit.

## Questions tranchées (16/09)

- **Q1** et **Q2** : Simon a demandé de retenir l'option la plus simple et qui réduit le plus la dette ;
  réponses et raisons dans le tableau des décisions.
- **Q3** : choix de Simon, « la plage saisie reste ».

## Résultat

*(à remplir en 075.5)*
