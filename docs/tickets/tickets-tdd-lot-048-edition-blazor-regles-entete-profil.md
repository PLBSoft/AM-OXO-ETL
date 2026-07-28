# Tickets TDD — Lot 048 : édition Blazor des règles d'en-tête du profil d'import

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Après le lot 047
(`tickets-tdd-lot-047-extraction-entetes-profile-driven-directcell.md`), **livré et mergé**. Dérivé
de la spec `spec-migration-entetes-profile-driven-directcell.md` (§2.4, §4).*

> **Version réconciliée avec le code réel du 047** (lecture du dépôt le 27/07). Le premier jet de ce
> ticket raisonnait sur les types *prévus* par le 047 ; cette version raisonne sur les types
> *livrés*. L'étape « 48.0 investigation » du brouillon est donc **close** : ses conclusions sont
> figées ci-dessous (§ « État réel du code livré par le 047 » et § « Divergences brouillon ↔ code
> réel »). Ne pas la rejouer.

**Objet** : rendre les règles d'en-tête (`HeaderFieldRule` directs + `HeaderCompositeRule` gabarits),
introduites et persistées en headless par le lot 047, **éditables depuis l'éditeur de profil d'import
Blazor**. C'est le livrable qui concrétise la demande client « configurer l'outil sans faire appel à
un développeur » (spec §2.4) : le 047 seed un modèle de départ, le 048 permet de le modifier.

**Rattachement UI** : les règles d'en-tête vivent sur `SheetExtractionRule` (par feuille, spec §2.2).
Leur édition se place donc **dans `SheetRuleForm.razor`**, à côté des sous-listes déjà présentes
(champs de bloc, colonnes inconditionnelles, point rules) — pas une nouvelle page, pas une section
transverse. Côté export : **aucune parité**, `ExportProfile` n'a pas de règles d'en-tête — ce lot est
strictement import.

---

## État réel du code livré par le 047 (constat verrouillé, ne pas ré-investiguer)

### Domaine

| Élément | Forme réelle |
|---|---|
| `HeaderFieldRule` | `sealed record`, ctor `(string name, DirectCell cell, bool stripReperePrefix = false, string? dateFormat = null)`, propriétés `Name` / `Cell` / `StripReperePrefix` / `DateFormat` |
| `HeaderCompositeRule` | `sealed partial record`, ctor `(string name, string template)`, propriétés `Name` / `Template`, **plus** une méthode publique `IReadOnlyList<string> PlaceholderNames()` |
| `DirectCell` | `sealed partial record (string sheet, string range)`, `Range` validé par `^[A-Z]{1,3}[1-9][0-9]*(:[A-Z]{1,3}[1-9][0-9]*)?$` — **majuscules obligatoires**, pas de `$`, pas de minuscules |
| Collections | `SheetExtractionRule.HeaderFields` et `.HeaderComposites`, toutes deux `IReadOnlyList<…>` adossées à des champs privés — **aucune méthode `Add`/`Remove` publique** |
| Ctor `SheetExtractionRule` | `(sheetName, locator, pointRules, unconditionalColonneNames, headerFields, headerComposites)` — **6 paramètres**, les deux derniers obligatoires (`ArgumentNullException.ThrowIfNull`), listes vides acceptées |

Le brouillon laissait ouvert le **lieu de la validation croisée des placeholders**. Réponse figée :
elle vit **dans le constructeur de `SheetExtractionRule`**, pas sur `HeaderCompositeRule`. Elle lève
une **`DomainRuleViolationException`** (et non `DomainValidationException`) portant
`DomainErrorCode.SheetExtractionRule_HeaderCompositeReferencesUnknownField`.

**Conséquence UI directe** : un gabarit à placeholder inconnu est **indétectable par le
sous-formulaire de règle composée isolé**. L'erreur ne peut apparaître qu'au moment où
`SheetRuleForm.Submit()` reconstruit la `SheetExtractionRule`. C'est ce que 48.4 doit couvrir, pas
48.3.

Validations levées par règle isolée (donc affichables dans les sous-formulaires) :
`HeaderFieldRule_EmptyName`, `HeaderFieldRule_BlankDateFormat` (un `DateFormat` non nul mais blanc),
`HeaderCompositeRule_EmptyName`, `HeaderCompositeRule_EmptyTemplate`, plus `DirectCell_EmptySheet` et
`DirectCell_InvalidRange` remontées par la construction du `DirectCell`.

### Persistance (aucune action pour ce lot, contexte seulement)

`ImportProfileConfiguration` mappe `HeaderFields` / `HeaderComposites` en `OwnsMany` sur
`ImportProfileSheetRuleHeaderFields` / `ImportProfileSheetRuleHeaderComposites`, `DirectCell` en
`OwnsOne` table-splitté (`CellSheet` / `CellRange`). Longueurs de colonnes utiles à l'UI :
`Name` 200, `CellRange` 20, `DateFormat` 50, `Template` **500**. Migration
`20260727215239_AddHeaderRulesToImportProfile`.

### Modèle-exemple seedé (`DefaultProfileSeeder`) — matière première des textes d'aide UI

- **PROCEDURE** — champs directs : `nomMAD` (`M2:O2`, `StripReperePrefix = true`), `revision`
  (`P2:Q2`), `dateRev` (`R2:T2`, `DateFormat = "dd/MM/yyyy"`) ; composé : `Designation` =
  `Rév {revision} du {dateRev}`.
- **AUTRES JOINTS TOUCHES** — champ direct `repereEcho` (`N6`).
- **DIVERS** — champ direct `repereEcho` (`N6`).
- ISOLEMENT / PLATINES / ORIFICES CAPACITES — aucune règle d'en-tête (`[], []`).
- Dans **100 %** des règles seedées, `Cell.Sheet` est identique au `SheetName` de la règle de
  feuille. Le domaine ne l'impose pas, mais c'est l'usage réel — voir décision §UI ci-dessous.

### Noms d'en-tête attendus par les services (piège structurel)

`ProcedureExtractionService` et `AutresJointsTouchesExtractionService` / `DiversExtractionService`
récupèrent leurs valeurs **par nom, via un indexeur** :

```csharp
header.Fields[ProcedureHeaderFieldNames.NomMad]      // "nomMAD"
header.Composites[ProcedureHeaderFieldNames.Designation]!   // "Designation"
header.Fields[SharedHeaderFieldNames.RepereEcho].Value      // "repereEcho"
```

Renommer ou supprimer un de ces champs depuis l'UI produit une **`KeyNotFoundException` non
gérée à l'extraction**, pas une erreur métier propre. Les constantes existent
(`ProcedureHeaderFieldNames`, `SharedHeaderFieldNames`, Application, déjà référencée par BlazorAdmin)
mais les **noms de feuilles** sont des `private const` de `ImportPipelineOrchestrator`, inaccessibles
depuis l'UI. Traitement retenu : **avertissement non bloquant** (48.5).

### UI existante — patron à calquer

`BlockFieldForm.razor` est le patron de référence du sous-formulaire :

- paramètres `IdPrefix`, `Initial…`, `SubmitButtonId`, `CancelButtonId`, `SubmitLabel`, `ShowCancel`,
  `OnCancel` (`EventCallback`), `OnSubmit` (`EventCallback<T>`) ;
- **tous les IDs sont préfixés par `IdPrefix`** — `@($"{IdPrefix}name-input")`. Aucun ID absolu du
  type `#header-field-name-input` : le parent instancie le même composant en mode ajout
  (`{IdPrefix}block-field-`) et en mode édition (`{IdPrefix}block-field-{index}-`) ;
- `OnInitialized` hydrate depuis `Initial…` (le parent donne une identité de composant fraîche en
  mode édition) ;
- erreurs : `try/catch (DomainValidationException ex)` →
  `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message` → `<div class="alert alert-danger"
  role="alert">` ;
- actions dans `.right-aligned-actions`, `AdminIconMarkup.Check` sur le bouton de soumission en mode
  édition.

`SheetRuleForm.razor` porte trois sous-listes (champs de bloc, colonnes inconditionnelles, point
rules) dont le comportement réel est : **suppression immédiate, sans confirmation**, boutons icône
`aria-label` + `title`, classes `block-field-list` / `block-field-item` / `block-field-info` /
`block-field-actions` / `block-field-icon-btn`, section dans `<div class="card bg-light mb-3">` +
`<div class="card-body">` + `<h3 class="h5">`.

`_hasUnsavedChanges` (Lot 043) vit **dans `ImportProfileEditor`**, pas dans `SheetRuleForm`. Il est
positionné par `HandleAddSheetRule` / `HandleSaveSheetRule`, c'est-à-dire **à la soumission de la
règle de feuille entière**. Aucune mutation de sous-liste interne à `SheetRuleForm` ne le touche
aujourd'hui.

Garde-fous transverses que ce lot doit satisfaire sans les modifier :
`FormFloatingStructureAuditTests` (tout `div.form-floating` doit contenir un `input`/`select` **avant**
son `<label>`, et tout `INPUT` doit porter un `placeholder` non vide),
`HeadingHierarchyAssertions` (pas de saut de niveau de titre),
`ProfileEditorParityTests` (chaînes de classes CSS comparées caractère par caractère entre éditeurs
import et export).

---

## Divergences entre le brouillon 048 et le code réel

| # | Brouillon | Code réel | Traitement |
|---|---|---|---|
| D1 | « le lot 048 est UI-only, rien à corriger côté 047 » | **`SheetRuleForm.Submit()` passe `[], []`** au constructeur de `SheetExtractionRule` : toute édition d'une règle de feuille depuis l'admin **efface silencieusement ses règles d'en-tête**. Régression livrée par le 047, non couverte par un test. | **48.1**, en tête de lot, test rouge d'abord |
| D2 | Validation des placeholders « au niveau de la règle isolée ou de `SheetExtractionRule`, à décider » | Tranché : `SheetExtractionRule`, `DomainRuleViolationException` | 48.3 ne teste **pas** le placeholder inconnu ; 48.4 le fait |
| D3 | IDs absolus (`#header-field-name-input`, `#save-header-field-button`) | Le patron réel préfixe **tout** par `IdPrefix` | IDs redéfinis en `@($"{IdPrefix}…")` dans 48.2/48.3/48.4 |
| D4 | « confirmation de suppression inline, un seul bloc actif à la fois, patron déjà validé pour les champs de bloc » | **Faux** : aucune sous-liste de `SheetRuleForm` ne confirme. La confirmation inline n'existe qu'au niveau carte de feuille (`ImportProfileEditor`, `_pendingDeleteIndex`) | **Décision Simon** : parité avec les listes sœurs → **suppression immédiate**, pas de confirmation |
| D5 | « toute mutation positionne `_hasUnsavedChanges = true` » | Les mutations de sous-listes ne le font pas ; seul l'`OnSubmit` de la règle de feuille le fait | 48.4 passe par le chemin existant, **sans** nouveau mécanisme — et 48.4 le teste explicitement |
| D6 | Tests « sur le modèle de `SheetRuleFormTests` / `BlockFieldFormTests` » | Ces fichiers **n'existent pas** : tous les tests bUnit de l'éditeur vivent dans `ImportProfileEditorTests.cs` (124 `[Fact]`), qui monte `ImportProfileEditor` et atteint les sous-formulaires par leurs IDs préfixés | Les tests de ce lot vont dans `ImportProfileEditorTests.cs` |
| D7 | Champ « Feuille » saisi dans le sous-formulaire | `Cell.Sheet` vaut toujours le `SheetName` de la règle dans le seed, et c'est `Cell.Sheet` que lit le résolveur | **Décision Simon** : la feuille est **dérivée** du nom de la règle de feuille, jamais saisie |
| D8 | « la validation du 047 est déclenchée et affichée » | `DomainErrorMessages.resx` (EN/FR) **ne contient aucune** des clés `HeaderFieldRule_*`, `HeaderCompositeRule_*`, `SheetExtractionRule_HeaderCompositeReferencesUnknownField`, `DirectCell_*`. `TryLocalize` renverrait le **nom brut de la clé** à l'écran | **48.7** ajoute les 7 clés manquantes EN/FR |
| D9 | Rien sur la visibilité en lecture seule | La carte de règle de feuille et son `<details>` n'affichent **pas** les règles d'en-tête : invisibles hors mode édition | **48.6** (ajout au périmètre, justifié ci-dessous) |
| D10 | « réutiliser `AdminIconMarkup` plutôt que des SVG dupliqués » | Correct comme direction, mais `SheetRuleForm` **duplique encore** les SVG Pencil/Trash/Check/X dans ses trois sous-listes | Le markup **neuf** utilise `AdminIconMarkup` ; le nettoyage de l'existant reste **hors périmètre** (dette, audit design 27/07 §2.4) |

---

## Décisions figées pour ce lot (arbitrage Simon, 27/07)

1. **Suppression sans confirmation** dans les deux nouvelles listes — parité stricte avec les
   sous-listes sœurs de `SheetRuleForm`. La confirmation inline reste le patron de la carte de
   règle de feuille uniquement.
2. **Feuille dérivée, non saisie** : le sous-formulaire de champ direct n'expose que la **plage**.
   `Cell.Sheet` est rempli, à la soumission de la règle de feuille, avec le `SheetName` courant du
   formulaire. Conséquence assumée : l'UI ne permet pas d'exprimer une lecture d'en-tête vers une
   autre feuille (que le domaine autorise) — aucun besoin réel, aucun cas seedé, et cela supprime
   une classe entière d'erreurs de saisie. Aucune règle domaine ajoutée : c'est un choix de saisie.
3. **Avertissement non bloquant** sur les noms attendus par les services (48.5) : signale, n'empêche
   pas d'enregistrer, n'ajoute aucune règle métier.

---

## Hors périmètre explicite de ce lot

- Tout changement du **domaine, de la persistance EF, de la migration, du seed ou des services
  d'extraction** — livrés par le 047. La seule exception est **48.1**, qui corrige un appel de
  constructeur **côté Blazor** (aucun fichier hors `ExcelETL.BlazorAdmin`).
- **Parité export** — `ExportProfile` / `ExportProfileEditor.razor` n'ont pas de règles d'en-tête.
- Rendre les services d'extraction tolérants à un nom d'en-tête manquant (`TryGetValue` au lieu de
  l'indexeur, `ExtractionErrorCode` dédié) : c'est du **047-bis côté Application**, pas de l'UI.
  Ce lot se contente d'avertir (48.5).
- Édition des règles d'en-tête ailleurs que dans l'éditeur de profil d'import (ni pages de test, ni
  API).
- Toute règle métier nouvelle : la validation reste **exactement** celle du domaine 047.
- Nettoyage des SVG inline dupliqués dans les sous-listes existantes de `SheetRuleForm` /
  `ImportProfileEditor` (dette relevée le 27/07) — lot séparé.
- Éditeur assisté de gabarits (autocomplétion de placeholders, aperçu) : un simple texte d'aide
  suffit.
- Suppression des types `TextTransform` inutilisés — toujours hors périmètre, comme au 047.

---

## 48.1. Correction bloquante — `SheetRuleForm` perd les règles d'en-tête

**Constat** : `SheetRuleForm.Submit()` construit aujourd'hui

```csharp
var rule = new SheetExtractionRule(_sheetName, locator, _pointRules, [.. _unconditionalColonneNames], [], []);
```

Les deux `[]` finaux sont des littéraux. Modifier une règle de feuille depuis l'admin — même pour
changer une virgule dans un nom de colonne — **efface les règles d'en-tête de cette feuille**, et le
profil ainsi enregistré casse l'extraction (`KeyNotFoundException` sur `nomMAD`). C'est le premier
ticket du lot : tout le reste construit dessus.

**Comportement attendu** : `SheetRuleForm` conserve les règles d'en-tête de `InitialRule` et les
restitue à l'identique à la soumission, même si aucune UI d'édition n'est encore branchée.

**Tests** (bUnit, `ImportProfileEditorTests.cs`) — **rouges d'abord** :

- Profil dont la feuille porte 1 `HeaderFieldRule` + 1 `HeaderCompositeRule` → entrer en mode édition
  (`#modify-sheet-rule-button-0`), soumettre sans rien changer (`#save-sheet-rule-button-0`),
  enregistrer le profil → le profil relu depuis le store porte **les mêmes** `HeaderFields` /
  `HeaderComposites` (comparaison structurelle, `record` → égalité par valeur).
- Même scénario en modifiant un champ sans rapport (nom de feuille, step) → règles d'en-tête
  toujours intactes.
- Une feuille sans règle d'en-tête reste soumise avec deux listes vides (non-régression).

**Implémentation** : deux listes d'état `_headerFields` / `_headerComposites` hydratées dans
`OnInitialized` depuis `InitialRule`, passées au constructeur, vidées dans `ResetForm()`. Passer des
**snapshots** (`[.. _headerFields]`) au constructeur — même raison que le commentaire existant sur
`_unconditionalColonneNames` : `ResetForm()` ne doit pas pouvoir atteindre la règle déjà transmise.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/SheetRuleForm.razor`.

---

## 48.2. `HeaderFieldRuleForm.razor` — sous-formulaire d'un champ d'en-tête direct

**Comportement attendu** : sous-formulaire calqué sur `BlockFieldForm.razor` pour ajouter/éditer un
`HeaderFieldRule`.

Paramètres : `IdPrefix`, `InitialField` (`HeaderFieldRule?`), `SubmitButtonId`, `CancelButtonId`,
`SubmitLabel`, `ShowCancel`, `OnCancel` (`EventCallback`), `OnSubmit`
(`EventCallback<HeaderFieldRule>`).

> Pas de paramètre `SheetName` : la feuille n'est pas connue de ce composant. Il émet un
> `HeaderFieldRule` dont le `Cell.Sheet` est un **placeholder** que `SheetRuleForm` réécrit à la
> soumission (48.4). Alternative acceptable si elle simplifie l'implémentation : passer `SheetName`
> en paramètre et construire directement le bon `DirectCell` — dans ce cas 48.4 réécrit quand même
> la feuille à la soumission, pour couvrir le renommage de feuille après coup. **L'invariant à
> tester est le résultat, pas le chemin** : après soumission, `Cell.Sheet == SheetName` de la règle.

Champs :

| Champ | ID | Structure |
|---|---|---|
| Nom du champ | `@($"{IdPrefix}header-field-name-input")` | `form-floating` + `placeholder` + `<label for>` |
| Plage | `@($"{IdPrefix}header-field-range-input")` | `form-floating`, placeholder d'exemple `M2:O2` |
| Format de date (optionnel) | `@($"{IdPrefix}header-field-date-format-input")` | `form-floating`, placeholder `dd/MM/yyyy`, hint « ne s'applique qu'à un champ contenant une date » |
| Retirer le préfixe repère | `@($"{IdPrefix}header-field-strip-prefix-checkbox")` | **`form-check`, surtout pas `form-floating`** — `FormFloatingStructureAuditTests` exige un `placeholder` non vide sur tout `INPUT` d'un `div.form-floating`, ce qu'une case à cocher ne peut satisfaire |

Règles de conversion : un format de date **vide ou blanc doit être transmis en `null`**, jamais en
`""` — sinon `HeaderFieldRule_BlankDateFormat` est levée alors que l'utilisateur n'a simplement rien
saisi. La plage est transmise telle quelle : c'est `DirectCell` qui valide (majuscules obligatoires).

Erreurs : `try/catch (DomainValidationException ex)` autour de la construction du `DirectCell` **et**
du `HeaderFieldRule` → `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message` dans un
`<div class="alert alert-danger" role="alert">`, sous-formulaire maintenu ouvert, aucun callback émis.

**Tests** (bUnit, `ImportProfileEditorTests.cs`) :

- Saisie valide (nom + plage) → callback appelé avec un `HeaderFieldRule` portant les valeurs
  saisies, `StripReperePrefix == false`, `DateFormat == null`.
- Case cochée + format de date renseigné → `StripReperePrefix == true`, `DateFormat == "dd/MM/yyyy"`.
- Format de date laissé vide → `DateFormat` **null** (et non `""`), aucune exception.
- Nom vide → message d'erreur `role="alert"`, aucun callback.
- Plage invalide (`"m2:o2"` minuscule, `"ZZZZ1"`, `"foo"`) → message d'erreur `role="alert"`, aucun
  callback.
- Mode édition (`InitialField` fourni) → champs pré-remplis (y compris case et format), soumission
  renvoie la version modifiée.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/HeaderFieldRuleForm.razor`.

---

## 48.3. `HeaderCompositeRuleForm.razor` — sous-formulaire d'un champ composé

**Comportement attendu** : symétrique de 48.2, mêmes paramètres, `OnSubmit`
(`EventCallback<HeaderCompositeRule>`).

Champs :

| Champ | ID | Structure |
|---|---|---|
| Nom du champ | `@($"{IdPrefix}header-composite-name-input")` | `form-floating` + placeholder |
| Gabarit | `@($"{IdPrefix}header-composite-template-input")` | `form-floating`, placeholder `Rév {revision} du {dateRev}`, hint sur la syntaxe `{nomDuChamp}` |

**Validation possible ici, et elle seule** : nom vide, gabarit vide (`DomainValidationException`).
Le **placeholder inconnu n'est pas détectable à ce niveau** — la validation croisée vit sur
`SheetExtractionRule` (voir D2). Ne pas écrire de test « placeholder inconnu » sur ce composant : il
serait vert pour la mauvaise raison (aucune exception levée du tout).

**Tests** (bUnit) :

- Gabarit valide → callback avec le `HeaderCompositeRule` saisi.
- Nom vide → erreur `role="alert"`, pas de callback.
- Gabarit vide → erreur `role="alert"`, pas de callback.
- Un gabarit **sans aucun placeholder** (texte littéral) est accepté — le domaine l'autorise
  (`PlaceholderNames()` renvoie une liste vide, la validation croisée passe).
- Mode édition → champs pré-remplis, renvoie la version modifiée.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/HeaderCompositeRuleForm.razor`.

---

## 48.4. Intégration dans `SheetRuleForm.razor` — deux listes éditables

**Comportement attendu** : deux nouvelles sections, chacune dans son
`<div class="card bg-light mb-3"><div class="card-body"><h3 class="h5">…`, après la section des
point rules — structure et classes strictement identiques aux sous-listes existantes.

**Liste « Champs d'en-tête »** (`HeaderFieldRule`) :

- résumé lecture seule par ligne : nom (`block-field-name`) + `plage` en
  `block-field-range text-muted font-monospace`, suffixé des indicateurs actifs (préfixe retiré /
  format de date) ;
- `@($"{IdPrefix}modify-header-field-button-{index}")` et
  `@($"{IdPrefix}delete-header-field-button-{index}")`, boutons icône seule,
  `aria-label` **et** `title`, markup via `AdminIconMarkup.Pencil` / `AdminIconMarkup.Trash` ;
- édition en ligne : la ligne est remplacée par un `HeaderFieldRuleForm` en mode édition
  (`IdPrefix = $"{IdPrefix}header-field-{index}-"`,
  `SubmitButtonId = $"{IdPrefix}save-header-field-button-{index}"`,
  `CancelButtonId = $"{IdPrefix}cancel-header-field-button-{index}"`) ;
- ajout : un `HeaderFieldRuleForm` permanent en bas de section
  (`IdPrefix = $"{IdPrefix}header-field-"`,
  `SubmitButtonId = $"{IdPrefix}add-header-field-button"`) ;
- **suppression immédiate**, sans confirmation (décision 1), avec la même remise à zéro de l'index
  d'édition que `DeleteBlockField`.

**Liste « Champs composés »** (`HeaderCompositeRule`) : strictement symétrique —
`add-header-composite-button`, `modify-header-composite-button-{index}`,
`delete-header-composite-button-{index}`, `save-header-composite-button-{index}`,
`cancel-header-composite-button-{index}`, `IdPrefix` de ligne
`$"{IdPrefix}header-composite-{index}-"`.

**Soumission** : `Submit()` reconstruit chaque `HeaderFieldRule` avec
`new DirectCell(_sheetName, <plage saisie>)` — la feuille est **toujours re-dérivée du nom de règle
courant** (décision 2), de sorte qu'un renommage de feuille après coup reste cohérent. Le
`try/catch` existant couvre déjà `DomainRuleViolationException` : c'est par lui que remonte le
placeholder inconnu, en `alert alert-danger role="alert"` en tête de formulaire, la soumission étant
abandonnée.

**Tests** (bUnit, `ImportProfileEditorTests.cs`) :

- Ajout d'un champ d'en-tête → nouvelle ligne rendue avec le résumé attendu ; après soumission de la
  règle de feuille, `HeaderFields` contient la règle, `Cell.Sheet == SheetName`.
- Édition d'une ligne → résumé mis à jour ; l'index édité est bien celui modifié (2 lignes, on édite
  la seconde).
- Suppression → la bonne ligne disparaît, **sans** bloc de confirmation intermédiaire (assertion
  explicite : aucun élément de confirmation rendu, la ligne est partie au premier clic).
- Idem pour la liste des champs composés (mêmes assertions transposées).
- **Renommage de feuille** : ajouter un champ d'en-tête, puis changer `sheet-rule-name-input`, puis
  soumettre → `Cell.Sheet` suit le nouveau nom (et `SheetExtractionRule` ne lève pas son
  `SheetNameLocatorMismatch`).
- **Placeholder inconnu** : composé `Rév {inconnu}` + soumission de la règle de feuille → alerte
  `role="alert"` affichée avec un message **localisé** (pas le nom brut de la clé — dépend de 48.7),
  et `OnSubmit` non invoqué (le nombre de règles de feuille de l'éditeur n'a pas bougé).
- **Lot 043** : après ajout puis soumission d'une règle de feuille portant une règle d'en-tête,
  `#unsaved-changes-indicator` est présent — via le chemin `HandleAddSheetRule`/`HandleSaveSheetRule`
  existant, sans nouveau mécanisme (D5).
- **Non-régression** : les tests existants de `ImportProfileEditorTests.cs` restent verts sans
  modification ; `FormFloatingStructureAuditTests.SheetRuleAndBlockFieldEditMode_…` reste vert avec
  les nouveaux champs rendus (c'est-à-dire : `form-floating` correct, `placeholder` non vide, case à
  cocher hors `form-floating`) ; `HeadingHierarchyAssertions` reste vert (les nouveaux titres sont
  des `h3`, comme leurs voisins) ; `ProfileEditorParityTests` reste vert (aucune chaîne de classe
  comparée n'est modifiée).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/SheetRuleForm.razor`.

---

## 48.5. Avertissement non bloquant sur les noms attendus par les services

**Comportement attendu** : quand la feuille éditée est l'une de celles dont un service d'extraction
lit un en-tête par nom, et qu'un de ces noms n'est **pas** présent dans les listes du formulaire, un
`<div class="alert alert-warning" role="alert">` (ID
`@($"{IdPrefix}header-well-known-names-warning")`) liste les noms manquants. **Non bloquant** :
l'utilisateur peut soumettre et enregistrer.

Table d'avertissement (UI seulement, jamais consultée par l'extraction) :

| `SheetName` | Champs directs attendus | Champs composés attendus |
|---|---|---|
| `PROCEDURE` | `nomMAD`, `revision`, `dateRev` | `Designation` |
| `AUTRES JOINTS TOUCHES` | `repereEcho` | — |
| `DIVERS` | `repereEcho` | — |

**Implémentation** : un statique `src/ExcelETL.BlazorAdmin/Shared/KnownHeaderFieldNames.cs`
réutilisant `ProcedureHeaderFieldNames` / `SharedHeaderFieldNames` (Application est déjà référencée
par BlazorAdmin) et ne redéclarant que les 3 littéraux de noms de feuille — les constantes
correspondantes de `ImportPipelineOrchestrator` sont `private`. Commentaire obligatoire dans le
fichier : **table consultative d'UI, aucune autorité sur l'extraction**, et pourquoi les noms de
feuille y sont dupliqués.

**Tests** (bUnit + un test unitaire simple sur la table) :

- Feuille `PROCEDURE` sans aucune règle d'en-tête → avertissement affiché, listant les 4 noms.
- Feuille `PROCEDURE` complète (les 3 directs + le composé) → **aucun** avertissement.
- Renommer `nomMAD` en `nomMad` → avertissement listant `nomMAD` (comparaison **ordinale**, sensible
  à la casse : c'est ce que fait le `Dictionary` du résolveur).
- Feuille `ISOLEMENT` sans règle d'en-tête → aucun avertissement (aucune attente pour cette feuille).
- L'avertissement **n'empêche pas** la soumission : soumettre malgré lui appelle bien `OnSubmit`.

**Dossier** : `src/ExcelETL.BlazorAdmin/Shared/KnownHeaderFieldNames.cs` +
`SheetRuleForm.razor`.

---

## 48.6. Visibilité en lecture seule dans la carte de règle de feuille

*Ajout au périmètre par rapport au brouillon (D9) : sans cela, les règles d'en-tête n'existent
visuellement que dans le mode édition — un admin ne peut pas vérifier d'un coup d'œil ce que porte
son profil, ce qui vide de son sens la promesse « configurable sans développeur ».*

**Comportement attendu** : dans `ImportProfileEditor.razor`, le `<details>` de la carte de règle de
feuille (déjà utilisé pour les colonnes inconditionnelles et les point rules) gagne deux blocs :
les champs d'en-tête (nom + plage + indicateurs) et les champs composés (nom + gabarit). Le libellé
du `<summary>` (`ImportProfileEditor_SheetRuleDetailsSummary`, aujourd'hui 2 arguments) intègre les
deux nouveaux compteurs.

**Tests** (bUnit) :

- Profil avec règles d'en-tête → après ouverture du `<details>`
  (`#sheet-rule-details-toggle-0`), le contenu (`#sheet-rule-details-content-0`) affiche les noms et
  plages attendus.
- Feuille sans aucune sous-liste → le message `ImportProfileEditor_NoSheetRuleSublistItems` reste
  affiché (il ne doit pas disparaître à cause des nouveaux compteurs).
- `ProfileEditorParityTests.SheetRuleSublistDetails_…` reste vert : seule la structure/les classes
  sont comparées entre import et export, pas le texte du résumé.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor`.

---

## 48.7. Ressources de localisation (EN/FR)

**Deux jeux distincts, les deux obligatoires.**

**a) `BlazorAdminMessages.resx` / `.fr.resx`** — nouvelles clés UI, sur la convention de nommage
existante `ImportProfileEditor_*` :

- titres de sections : `ImportProfileEditor_HeaderFieldsHeading`,
  `ImportProfileEditor_HeaderCompositesHeading` ;
- libellés + placeholders : `…_HeaderFieldNameLabel`/`Placeholder`, `…_HeaderFieldRangeLabel`/
  `Placeholder`, `…_HeaderFieldDateFormatLabel`/`Placeholder`, `…_HeaderFieldStripPrefixLabel`,
  `…_HeaderCompositeNameLabel`/`Placeholder`, `…_HeaderCompositeTemplateLabel`/`Placeholder` ;
- hints : `…_HeaderFieldDateFormatHint`, `…_HeaderCompositeTemplateHint` (mentionnant l'exemple seedé
  `Rév {revision} du {dateRev}`) ;
- boutons d'ajout : `…_AddHeaderFieldButton`, `…_AddHeaderCompositeButton` ;
- avertissement 48.5 : `…_WellKnownHeaderNamesMissingWarning` (paramétrée par la liste des noms) ;
- résumé 48.6 : mise à jour de `…_SheetRuleDetailsSummary`.

**Réutiliser sans dupliquer** : `ImportProfileEditor_ModifyButton`, `…_DeleteButton`,
`…_SaveChangesButton`, `…_CancelButton`, `…_InvalidExcelRangeError`.

**b) `DomainErrorMessages.resx` / `.fr.resx` (projet Application)** — **clés manquantes depuis le
047** (D8). Sans elles, `IStringLocalizer` renvoie le nom de la clé et l'utilisateur voit
littéralement `HeaderFieldRule_EmptyName` à l'écran :

- `HeaderFieldRule_EmptyName`
- `HeaderFieldRule_BlankDateFormat`
- `HeaderCompositeRule_EmptyName`
- `HeaderCompositeRule_EmptyTemplate`
- `SheetExtractionRule_HeaderCompositeReferencesUnknownField` (2 arguments : nom du composé, nom du
  placeholder — respecter l'ordre passé à la `DomainRuleViolationException`)
- `DirectCell_EmptySheet`
- `DirectCell_InvalidRange`

**Tests** : un test de localisation ciblé sur les clés du groupe (b) — une règle invalide construite
en test, passée à `BusinessExceptionLocalizer.TryLocalize`, doit renvoyer un message **différent du
nom de la clé**, en `en` et en `fr`. C'est la seule garantie qu'une clé oubliée sera vue.

---

## Ordre recommandé

1. **48.1** — la correction de la perte de données, test rouge d'abord. Rien d'autre n'a de sens
   avant : les sous-formulaires alimenteraient un état que la soumission jette.
2. **48.7 (b)** — les clés `DomainErrorMessages` manquantes, tout de suite après. Elles conditionnent
   la lisibilité de chaque assertion d'erreur écrite ensuite.
3. **48.2** — `HeaderFieldRuleForm`, le sous-formulaire de référence.
4. **48.3** — `HeaderCompositeRuleForm`, duplication stricte du patron de 48.2.
5. **48.4** — intégration des deux listes dans `SheetRuleForm` (dépend de 48.2 et 48.3).
6. **48.5** — avertissement noms attendus (dépend de l'état de listes introduit en 48.4).
7. **48.6** — résumé lecture seule dans `ImportProfileEditor`.
8. **48.7 (a)** — clés UI, une fois les textes définitifs connus.

---

## Note d'efficacité d'implémentation (Claude Code)

- **48.1 n'est pas un préambule, c'est un bug de perte de données en production potentielle.** Le
  test doit être écrit rouge, constaté rouge, puis vert. Ne pas le fusionner avec 48.4 : sa valeur
  de régression tient à ce qu'il passe **sans** aucune UI d'édition branchée.
- **La réconciliation avec le 047 est faite** (§ « État réel » et § « Divergences »). Ne pas rejouer
  une investigation : lire directement les fichiers cités si un doute subsiste, mais les décisions
  D1-D10 sont figées.
- **Le placeholder inconnu se teste en 48.4, jamais en 48.3.** Un test « placeholder inconnu » sur le
  sous-formulaire isolé passerait au vert sans rien prouver.
- **La case à cocher ne va pas dans un `form-floating`** — `FormFloatingStructureAuditTests` casse
  sinon, et le message d'échec parlera de `placeholder`, pas de case à cocher : piège coûteux en
  temps.
- **Tous les IDs sont préfixés par `IdPrefix`.** Un ID absolu marchera en mode ajout et cassera en
  mode édition (deux instances du même composant montées simultanément → IDs dupliqués, `Find`
  ambigu).
- **`_hasUnsavedChanges` n'est pas à câbler** : le chemin `OnSubmit` → `HandleAddSheetRule` /
  `HandleSaveSheetRule` existe déjà et suffit. Ajouter un mécanisme par mutation serait une
  divergence avec les sous-listes sœurs, pas une amélioration.
- **Markup neuf → `AdminIconMarkup`.** Ne pas en profiter pour nettoyer les SVG dupliqués voisins :
  hors périmètre, et cela mêlerait une dette ancienne aux tests de ce lot.
- **Rester import-only** : ne pas ouvrir `ExportProfileEditor.razor`.
