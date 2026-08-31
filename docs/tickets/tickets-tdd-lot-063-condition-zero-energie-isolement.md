# Tickets TDD — Lot 063 : condition « Zéro énergie » pilotée par une cellule dédiée (ISOLEMENT)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Après le lot 062
(`tickets-tdd-lot-062-version-application-sidebar.md`). Décisions figées avec Simon début août.*

**Objet** : corriger la règle de point conditionnel PS941 de la feuille ISOLEMENT — elle teste
aujourd'hui `TypeElement Equals "ZERO ENERGIE"`, alors que le signal réel « zéro énergie » vit dans
une cellule dédiée du bloc (colonne V, `V18:V19` pour le premier bloc), totalement indépendante du
`TypeElement`. Ce lot introduit une propriété `HasZeroEnergie` sur `IsolementPivot`, alimentée depuis
cette cellule, et reconstruit la règle PS941 pour la tester à la place du `TypeElement`.

**Origine** : signalé par Simon à partir du fichier Excel de test client. Investigation manuelle
confirmée sur les 3 fixtures réelles (voir §63.0) : seul `Dossier.de.MaD.IDL.-.C7401.xlsx` porte une
valeur non vide dans cette colonne, sur le bloc `Identification = "V4"` (`TypeElement = "PROLOCK"`,
`V32 = "ZERO ENERGIE"`, la cellule étant fusionnée `V32:V33`). `D8570`/`G6306B` sont vides sur toute
la feuille ISOLEMENT pour cette colonne.

**Note pour Claude Code** : une première tentative d'implémentation de ce lot a été faite dans un
environnement sans accès à `dotnet`/`dotnet ef` (SDK absent), ce qui a conduit à une migration EF Core
écrite à la main puis intégralement annulée (`git restore`). Ce ticket repart de zéro sur la base du
code actuellement dans le dépôt — ne pas chercher de trace de cette tentative, il n'en reste rien
(vérifié via `git status`).

---

## Décisions actées avec Simon (non négociables, à respecter telles quelles)

- La propriété vit sur **`IsolementPivot`** (pas sur `EquipementPivot`) : d'après les données du
  fichier Excel client, cette notion n'existe que pour les entités Isolement (qui dérivent de
  `BaseElement`), jamais pour les équipements.
- Modélisée en **`bool HasZeroEnergie`**.
- Convention de mapping : cellule vide → `false` ; cellule = valeur attendue → `true`.
- La règle PS941 devient : **`HasZeroEnergie Equals true`** (au lieu de `TypeElement Equals "ZERO ENERGIE"`).
- Le seed du profil d'import par défaut doit être mis à jour en conséquence.
- **Pas de migration de données** pour les profils déjà seedés en base : Simon n'est pas en
  production, la base de dev peut être supprimée/recréée sans ménagement.
- **Si la cellule contient autre chose que vide ou la valeur attendue, un warning doit être loggé**
  (non bloquant — l'isolement doit continuer à être extrait normalement, comme pour tout le reste du
  pipeline OXO).
- Le pas du bloc est **7**, premier bloc en ligne **19** (déjà le cas dans le `RepeatingBlockLocator`
  ISOLEMENT existant — rien à changer ici).
- **Critique — pas de valeur en dur dans le code** : le texte attendu (`"ZERO ENERGIE"`) doit être
  configurable au niveau du profil d'import. Citation verbatim de Simon : *« Je ne suis pas à l'abris
  que demain, le client final se décide à la renommer 'ZERO ENER.' ou '0 ENERGIE', par exemple. »*
- Ce champ configurable, **`ZeroEnergieExpectedValue`**, est un **champ dédié directement sur la
  règle d'extraction de ce bloc** (`SheetExtractionRule` d'ISOLEMENT) — pas un mécanisme générique
  réutilisable pour d'autres champs de bloc, pas porté par une autre entité.
- **`ZeroEnergieExpectedValue` doit être exposé et éditable dans l'UI Blazor de l'éditeur de profil
  d'import dès ce lot** (`ImportProfileEditor.razor`/`SheetRuleForm.razor`) — pas un champ invisible
  laissé pour un lot ultérieur (confirmé par Simon, contrairement au précédent Lot 047→048 où l'UI avait
  été volontairement différée). Voir §63.7.

---

## Hors périmètre explicite de ce lot

- Migration/rattrapage des profils déjà en base (Simon confirme : base de dev jetable).
- Toute autre feuille que ISOLEMENT (PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS n'ont
  pas cette notion, confirmé par les données client — `HasZeroEnergie` reste `false` par défaut pour
  leurs `IsolementPivot`, jamais construit explicitement par leurs services).
- Généralisation du mécanisme de valeur attendue configurable à d'autres champs de bloc — YAGNI,
  aucune demande au-delà de ce cas précis.

---

## 63.0. Investigation préalable (obligatoire avant tout code)

- [ ] Relire `IsolementExtractionService.cs` (`src/ExcelETL.Application/Extraction/Oxo/Isolement/`) :
  comprendre pourquoi ce service ne délègue pas à `IRepeatingBlockReader` (politique de champs requis
  mixte — voir le commentaire en tête de fichier), et où s'insère la lecture d'un champ de bloc
  supplémentaire optionnel.
- [ ] Relire `SheetExtractionRule.cs` (Domain) : convention de validation au constructeur (voir
  `SheetExtractionRule_EmptySheetName`/`SheetExtractionRule_SheetNameLocatorMismatch`/
  `SheetExtractionRule_HeaderCompositeReferencesUnknownField` dans `DomainErrorCode.cs`) pour ajouter
  un nouveau champ optionnel `string? ZeroEnergieExpectedValue` dans le même style.
- [ ] Relire `IsolementPivot.cs` (Domain) : record scellé, égalité structurelle explicite (`Equals`/
  `GetHashCode`), constructeur à 5 paramètres obligatoires. Décider si `HasZeroEnergie` est un
  paramètre de constructeur optionnel (`= false`) plutôt qu'une propriété `init` façon
  `Localisation`/`Tableaux`/`Applications`/`RepereParent` — recommandation : paramètre de constructeur,
  car (contrairement à ces 4 propriétés) la donnée est connue au moment de la construction, lue sur la
  même feuille ISOLEMENT que `Identification`/`Designation`/`TypeElement`, pas diffusée après coup par
  l'orchestrateur.
- [ ] Relire `ExtractionErrorCode.cs`/`ExtractionErrorLogging.cs`/`ExtractionError.cs` : ajouter un
  nouveau code d'erreur non bloquant (mappé `Warning`), suivant exactement le patron de
  `NoConditionalPointCreated`/`TacheMultipleTypeMismatch`. `ExtractionError` porte déjà un champ
  optionnel `ExtractedValue` (Lot 055) — l'utiliser pour la valeur brute inattendue plutôt que de la
  concaténer uniquement dans `Message`.
- [ ] Relire `NoConditionalPointCreatedWarningTracker.cs` (`src/ExcelETL.Application/Extraction/Oxo/`) :
  décider si le nouveau warning « valeur inattendue » a besoin d'une déduplication similaire (probable :
  éviter un warning par bloc identique si plusieurs isolements partagent la même valeur inattendue dans
  la même feuille) ou si un warning par bloc suffit (plus simple, à trancher en 63.0 selon le volume
  réel attendu — le seul cas réel connu à ce jour, C7401/V4, est un événement unique par fixture).
- [ ] Relire `DefaultProfileSeeder.cs` : règle `SheetExtractionRule` d'ISOLEMENT actuelle (voir
  extrait ci-dessous) — `BlockFieldDefinition` à ajouter pour la colonne V, `ConditionalPointRule` à
  reconstruire, `ZeroEnergieExpectedValue` à renseigner.
- [ ] Relire `ImportProfileConfiguration.cs` (Infrastructure) et
  `ExcelEtlDbContextModelSnapshot.cs` : où rattacher la nouvelle colonne `ZeroEnergieExpectedValue` sur
  la table `ImportProfileSheetRules` (mapping `OwnsMany` existant de `SheetExtractionRule`).
  **Générer la migration exclusivement via `dotnet ef migrations add` — ne jamais l'écrire à la main.**
- [ ] Confirmer sur les 3 fixtures réelles (déjà fait pour ce ticket, voir tableau ci-dessous) qu'aucune
  autre feuille isolement-style (PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS) ne porte de
  valeur dans une colonne équivalente — sinon remonter le point avant d'implémenter.

**Extrait actuel de `DefaultProfileSeeder.cs` (règle ISOLEMENT, à faire évoluer)** :
```csharp
new SheetExtractionRule(
    "ISOLEMENT",
    new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
    [
        new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
        new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
        new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
        new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
    ]),
    [
        new ConditionalPointRule(
            IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", IsolementZeroEnergieColonneName)
    ],
    ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []),
```
où `IsolementZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)"`.

**Constat fixtures réelles (à valider par Claude Code, valeurs relevées à la main via openpyxl,
`data_only=True`, feuille ISOLEMENT, colonne V, offset -1 par rapport au début de chaque bloc)** :

| Fixture | Bloc(s) avec une valeur non vide en colonne V | Valeur |
|---|---|---|
| `Dossier.de.MaD.IDL.-.C7401.xlsx` | `Identification = "V4"` (ligne de bloc 33, `TypeElement = "PROLOCK"`) | `"ZERO ENERGIE"` (cellule fusionnée `V32:V33`) |
| `Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx` | aucun | — |
| `Dossier.de.MaD.IDL.-.G6306B.REV.xlsx` | aucun | — |

Ce cas C7401/V4 est le cas de non-régression concret à utiliser en 63.8 : avant ce lot, `V4` (TypeElement
"PROLOCK") ne matche aucune condition PS941 (puisque testée sur `TypeElement`) → aucun Point PS941.
Après ce lot, `V4` doit produire le Point PS941 (puisque sa cellule V vaut la valeur attendue), sans
aucun warning pour ce bloc précis.

---

## 63.1. Domain — `HasZeroEnergie` sur `IsolementPivot`

**Comportement attendu** : nouveau paramètre de constructeur `bool hasZeroEnergie = false` (valeur par
défaut préservant les 4 autres services isolement-style qui construisent `IsolementPivot` sans jamais
connaître cette notion — `UnconditionalIsolementSheetExtractionService`, `AutresJointsTouchesExtractionService`,
`DiversExtractionService`, à ne pas modifier), nouvelle propriété `public bool HasZeroEnergie { get; }`,
incluse dans `Equals`/`GetHashCode`.

**Tests** (xUnit, Domain, `IsolementPivotTests.cs`) :
- Test de base existant : `isolement.HasZeroEnergie.Should().BeFalse()` par défaut quand omis.
- Construction explicite avec `hasZeroEnergie: true` → propriété assignée.
- Deux instances ne différant que par `HasZeroEnergie` → non égales structurellement (`Equals`/`GetHashCode`).

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/IsolementPivot.cs` (+ miroir tests).

---

## 63.2. Domain — `ZeroEnergieExpectedValue` sur `SheetExtractionRule`

**Comportement attendu** : nouvelle propriété `public string? ZeroEnergieExpectedValue { get; }`,
nouveau paramètre de constructeur optionnel `string? zeroEnergieExpectedValue = null` (préserve tous
les appels existants — PROCEDURE, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS n'ont
pas cette notion). `null` = pas de champ « zéro énergie » configuré pour cette feuille (comportement
actuel de toutes les feuilles hors ISOLEMENT). Validation : si renseigné, ne doit pas être blanc
(`string.IsNullOrWhiteSpace` → `DomainValidationException` avec un nouveau `DomainErrorCode` dédié,
même patron que les validations existantes de ce type — voir `SheetExtractionRule_EmptySheetName`).

**Tests** (xUnit, Domain, `SheetExtractionRuleTests.cs`) :
- Test de base existant : `rule.ZeroEnergieExpectedValue.Should().BeNull()` quand omis.
- Construction avec une valeur non blanche → propriété assignée.
- `[Theory]` avec `""` et `" "` → `DomainValidationException` + code dédié.

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/SheetExtractionRule.cs` (+ miroir tests) +
`src/ExcelETL.Domain/Exceptions/DomainErrorCode.cs` (nouveau membre, ex.
`SheetExtractionRule_BlankZeroEnergieExpectedValue`) + `DomainErrorMessages.resx`/`.fr.resx`
(entrées EN/FR correspondantes, vérifiées non orphelines via le test de localisation existant si le
projet en a un pour cette resx — voir `DomainErrorMessagesHeaderRuleLocalizationTests.cs`/
`DomainErrorMessagesImportProfileListItemLocalizationTests.cs` comme précédent direct à reproduire).

---

## 63.3. Domain — nouveau code d'erreur non bloquant

**Comportement attendu** : nouveau membre `ExtractionErrorCode.UnexpectedZeroEnergieValue` (ou nom
équivalent choisi en 63.0), mappé `Warning` dans `ExtractionErrorLogging.Log` aux côtés de
`NoConditionalPointCreated`/`TacheMultipleTypeMismatch`.

**Tests** (xUnit, Application, `ExtractionErrorLoggingTests.cs` si ce fichier existe déjà, sinon
compléter les tests existants de la classe qui couvre ce mapping) :
- Une `ExtractionError` avec ce nouveau code loggue au niveau `Warning`, pas `Error`.

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/ExtractionErrorCode.cs`,
`src/ExcelETL.Application/Extraction/Oxo/ExtractionErrorLogging.cs`.

---

## 63.4. Application — `IsolementExtractionService` : lecture et mapping de `HasZeroEnergie`

**Comportement attendu** :
1. Un nouveau nom de champ, ex. `IsolementFieldNames.HasZeroEnergie`, à ajouter dans
   `IsolementFieldNames.cs`.
2. Le `BlockFieldDefinition` correspondant (`"V"`, `-1`, `0` — reproduisant `V18:V19` pour le premier
   bloc en ligne 19, confirmé par le calcul `BlockFieldRangeCalculator`) est **optionnel** dans le
   `RepeatingBlockLocator` de la feuille : rechercher le champ par nom sans lever d'exception s'il est
   absent (`FirstOrDefault`, pas `First`/`FindField` strict) — permet à un profil ne le configurant pas
   de continuer à fonctionner comme avant ce lot (`HasZeroEnergie` reste `false`).
3. Pour chaque bloc extrait : si le champ est configuré, lire la cellule ; cellule vide/blanche →
   `HasZeroEnergie = false`, aucun warning ; cellule = `sheetRule.ZeroEnergieExpectedValue` (comparaison
   trim + insensible à la casse, cohérent avec `ConditionalPointRuleEvaluator` — spec §7 déjà en place
   ailleurs dans le pipeline) → `HasZeroEnergie = true` ; toute autre valeur non blanche → `HasZeroEnergie
   = false` **et** un `ExtractionError` de code `UnexpectedZeroEnergieValue` est loggué et ajouté aux
   erreurs du résultat (non bloquant, l'isolement continue d'être extrait normalement).
4. `HasZeroEnergie` (sous forme `"true"`/`"false"`) est ajouté au dictionnaire de champs déjà extraits
   passé à `ConditionalPointGroupEvaluator.Evaluate`, aux côtés de `TypeElement`, pour que la règle
   PS941 puisse le référencer comme `SourceFieldName`.
5. `new IsolementPivot(repere, designation, typeElement!, positionALaPose!, "", hasZeroEnergie)`.

**Tests** (xUnit, Application, `IsolementExtractionServiceTests.cs`, `Mock<IWorkbookReader>`) :
- Cellule V égale à `ZeroEnergieExpectedValue` (trim/casse variables) → `IsolementPivot.HasZeroEnergie
  == true`, Point PS941 créé, aucun warning.
- Cellule V vide/blanche → `HasZeroEnergie == false`, aucun warning.
- Cellule V renseignée avec une valeur différente de `ZeroEnergieExpectedValue` → `HasZeroEnergie ==
  false`, un `ExtractionError` de code `UnexpectedZeroEnergieValue` ajouté (vérifier `ExtractedValue`
  porte bien la valeur brute lue).
- `ZeroEnergieExpectedValue` non configuré (`null`) sur le profil → comportement inchangé (pas de
  levée d'exception, `HasZeroEnergie` reste `false` même si la cellule contient du texte — documenter
  ce choix explicitement dans le test).
- Champ `BlockFieldDefinition` de la colonne V absent du `RepeatingBlockLocator` → aucune exception,
  `HasZeroEnergie` reste `false` pour tous les blocs (rétrocompatibilité d'un profil non mis à jour).
- **Garde-fou anti-hardcoding** (même patron que le test `EquipementTypeElementNom` du Lot C1) : deux
  profils avec des `ZeroEnergieExpectedValue` différents (`"ZERO ENERGIE"` vs `"0 ENERGIE"`) sur la même
  cellule → chaque profil restitue son propre résultat, jamais une constante de service.
- Non-régression : les tests existants de ce fichier (notamment ceux couvrant la règle PS941 actuelle
  testée sur `TypeElement`) sont réécrits pour construire leur `SheetExtractionRule` de test avec le
  nouveau champ `HasZeroEnergie` et la nouvelle règle conditionnelle — pas de doublon, réécriture en
  place puisqu'il s'agit du même comportement observable (un Point PS941 créé ou non), juste piloté par
  une source différente.

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/Isolement/` (`IsolementFieldNames.cs`,
`IsolementExtractionService.cs`) + miroir tests.

---

## 63.5. Infrastructure — persistance EF Core + migration

**Comportement attendu** : nouvelle colonne `ZeroEnergieExpectedValue` (nullable, `nvarchar`, longueur
raisonnable — ex. 200, cohérent avec les autres colonnes texte courtes du même mapping `OwnsMany` de
`SheetExtractionRule`) sur la table `ImportProfileSheetRules`, configurée dans
`ImportProfileConfiguration.cs`. **Migration générée via `dotnet ef migrations add
AddZeroEnergieExpectedValueToImportProfileSheetRule --project src/ExcelETL.Infrastructure --startup-project
src/ExcelETL.WebAPI` (ou le projet de démarrage équivalent déjà utilisé pour les migrations
précédentes de ce contexte) — jamais écrite à la main.**

**Tests** (xUnit, EF Core InMemory réel, `EfImportProfileStoreTests.cs`) :
- Round-trip d'un `SheetExtractionRule` avec `ZeroEnergieExpectedValue` renseigné → relu à l'identique.
- Round-trip avec `ZeroEnergieExpectedValue = null` → relu comme `null` (pas une chaîne vide par
  défaut de valeur — même exigence que le test dédié `SaveAsync_WithNullColumnSourceForZeroEnergieExpectedValue_PersistsAndReloadsAsNull`
  déjà écrit une première fois pour ce lot, à reproduire).

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Configurations/ImportProfileConfiguration.cs`
+ nouvelle migration (générée par tooling).

---

## 63.6. Infrastructure — mise à jour du `DefaultProfileSeeder`

**Comportement attendu** : la règle ISOLEMENT seedée par défaut est mise à jour pour :
- ajouter le `BlockFieldDefinition` de la colonne V (`IsolementFieldNames.HasZeroEnergie`, `"V"`, `-1`, `0`) ;
- remplacer la `ConditionalPointRule` PS941 par `new ConditionalPointRule(IsolementFieldNames.HasZeroEnergie,
  ConditionOperator.Equals, "true", IsolementZeroEnergieColonneName)` ;
- renseigner `zeroEnergieExpectedValue: "ZERO ENERGIE"` (reproduit la valeur actuellement en dur, la
  seule connue à ce jour — pas de valeur inventée).

**Tests** (xUnit, `DefaultProfileSeederTests.cs`) :
- Le profil seedé par défaut porte la règle ISOLEMENT attendue : champ `HasZeroEnergie` présent avec
  la bonne plage colonne, `ZeroEnergieExpectedValue == "ZERO ENERGIE"`, `PointRules` contenant la règle
  PS941 testant `SourceFieldName == IsolementFieldNames.HasZeroEnergie`/`ComparisonValue == "true"`.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (+ miroir tests).

---

## 63.7. BlazorAdmin — exposition de `ZeroEnergieExpectedValue` dans l'éditeur de profil

**Comportement attendu** : `ZeroEnergieExpectedValue` est un champ optionnel de niveau feuille (comme
`StopFieldName`), pas un élément d'une sous-liste répétable — même traitement que `_stopFieldName`
dans `SheetRuleForm.razor` (champ `form-floating` simple, `@bind:after="MarkDirty"`).

1. **`SheetRuleForm.razor`** : nouveau champ d'état `private string _zeroEnergieExpectedValue =
   string.Empty;`, initialisé dans `OnInitialized()` depuis `InitialRule.ZeroEnergieExpectedValue`
   (vide si `null`), remis à `string.Empty` dans `ResetForm()`, inclus dans le test `IsBlank()` (une
   valeur renseignée doit empêcher le formulaire d'être considéré comme vierge). Un nouvel `<input>`
   `form-floating` (id `@($"{IdPrefix}sheet-rule-zero-energie-expected-value-input")`) est ajouté à la
   suite du champ `StopFieldName` — nouvelles clés resx
   `ImportProfileEditor_ZeroEnergieExpectedValueLabel`/`...Placeholder` (EN/FR). Champ **facultatif** :
   une valeur vide est envoyée comme `null` au constructeur (`string.IsNullOrWhiteSpace(_zeroEnergieExpectedValue)
   ? null : _zeroEnergieExpectedValue`), jamais une chaîne vide (le constructeur Domain rejette une
   chaîne blanche non-null — voir 63.2).
2. **`TryCommitAsync()`** : le 7ᵉ argument du constructeur `SheetExtractionRule` (voir extrait de code
   actuel en 63.0/§ordre) devient ce champ converti en `null`/valeur, à la suite de `headerComposites`.
3. **`ImportProfileEditor.razor`** (résumé en lecture seule de la carte de règle) : la valeur, quand
   elle est renseignée, est affichée dans `.sheet-rule-card-meta` à la suite du texte existant
   (`ImportProfileEditor_SheetMetadata`) — **conditionnellement** (span séparé affiché uniquement si
   `rule.ZeroEnergieExpectedValue is not null`), pour ne rien afficher sur les feuilles qui n'ont pas
   cette notion (PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS/PROCEDURE). Nouvelle clé resx
   `ImportProfileEditor_ZeroEnergieExpectedValueSummary` (ex. `"Zero energie expected value: {0}"` /
   `"Valeur « zéro énergie » attendue : {0}"`).

**Tests** (bUnit, `ImportProfileEditorTests.cs`/fichiers de test dédiés du composant `SheetRuleForm` si
séparés — suivre le patron déjà en place pour `StopFieldName`) :
- Le champ a un `<label>` associé (patron `FormFloatingStructureAssertions`, déjà utilisé partout
  ailleurs sur cette page — l'étendre à ce nouveau champ, pas de nouvelle exception).
- Ajout d'une règle de feuille avec `ZeroEnergieExpectedValue` renseigné → round-trip via
  `IImportProfileStore` (sauvegarde puis relecture) → valeur identique.
- Champ laissé vide → la règle construite a `ZeroEnergieExpectedValue == null` (pas une chaîne vide).
- Édition d'une règle existante (bouton Modifier) → le champ est pré-rempli avec la valeur actuelle ;
  Annuler restaure la valeur d'origine sans la persister.
- Le résumé en lecture seule d'une carte de règle affiche la valeur uniquement quand elle est
  renseignée (ex. ISOLEMENT du profil seedé par défaut) et ne l'affiche pas pour une feuille qui n'a
  pas cette notion (ex. PROCEDURE/PLATINES) — non-régression sur les cartes déjà couvertes par les
  tests de parité existants (`ProfileEditorParityTests.cs`).
- Modifier/renseigner ce champ seul (sans toucher à un autre champ) déclenche bien l'indicateur de
  modifications non enregistrées (`MarkDirty`/`_hasUnsavedChanges`, Lot 056/059).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/SheetRuleForm.razor`,
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor`,
`src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx`/`.fr.resx` (+ miroir tests).

**Hors périmètre pour ce sous-lot** : `ExportProfileEditor.razor` n'a pas d'équivalent
(`ZeroEnergieExpectedValue` n'existe que côté import) — aucun changement à y apporter.

---

## 63.8. Non-régression d'intégration sur les 3 fixtures réelles

**Comportement attendu** : avec le profil **seedé par défaut** (63.6), l'extraction des 3 fixtures
produit le résultat suivant pour la feuille ISOLEMENT :
- **C7401** : l'isolement `Identification = "V4"` a désormais `HasZeroEnergie == true` et le Point
  PS941 (`"ZÉRO ENERGIE EN PRESENCE EE (PS941)"`) est créé pour son repère composé, sans aucun warning
  `UnexpectedZeroEnergieValue` pour ce bloc. Le nombre total d'isolements extraits pour cette feuille
  reste inchangé (23, cf. changelog `ImportPipelineOrchestratorIntegrationTests`) — seul le
  comportement du Point PS941 change pour ce bloc précis.
- **D8570** : aucun changement observable (aucune valeur non vide en colonne V sur cette fixture) —
  la suite d'intégration existante reste verte sans modification de ses assertions.
- **G6306B** : aucun changement observable, même raison que D8570.

**Tests** (xUnit, intégration, fixtures réelles — patron `ExcelETL.Infrastructure.Tests`,
`IsolementExtractionServiceIntegrationTests.cs` et/ou `DefaultProfileSeederPipelineIntegrationTests.cs`
selon où vivent déjà les assertions équivalentes) :
- Nouveau test dédié C7401 : l'isolement `"C7401-V4"` (ou le repère composé réel) a `HasZeroEnergie ==
  true`, le Point PS941 existe pour ce repère, et aucune `ExtractionError` de code
  `UnexpectedZeroEnergieValue` n'est présente pour ce bloc.
- Les tests d'intégration existants (comptage d'isolements par fixture, absence de `RequiredFieldMissing`
  ailleurs) restent verts **sans modification de leurs assertions**, à l'exception du(des) test(s)
  couvrant explicitement l'ancien comportement de la règle PS941 sur `TypeElement` — ceux-là sont
  réécrits en place (même raison qu'en 63.4 : comportement observable équivalent, source différente),
  pas dupliqués.

---

## Ordre recommandé

1. **63.0** (investigation — confirme la nature du champ, verrouille les valeurs de non-régression)
2. **63.1** + **63.2** + **63.3** (Domain — indépendants entre eux, peuvent avancer en parallèle)
3. **63.4** (Application — dépend de 63.1/63.2/63.3)
4. **63.5** (EF Core + migration générée par tooling) puis **63.6** (seeder — dépend de 63.2/63.5)
5. **63.7** (UI Blazor — dépend de 63.2 pour le champ Domain, peut avancer dès que 63.5/63.6 compilent
   pour disposer d'un profil seedé de référence à éditer)
6. **63.8** (non-régression fixtures — dernier, valide l'invariant central)

## Note d'efficacité d'implémentation (Claude Code)

- **Ne jamais écrire de migration EF Core à la main** — toujours `dotnet ef migrations add`, avec le
  bon `--project`/`--startup-project` (vérifier la commande exacte utilisée pour les migrations
  précédentes de `ExcelEtlDbContext`, ex. Lot 047/`AddHeaderRulesToImportProfile`, comme référence).
- **Invariant central** : seul le bloc C7401/V4 doit changer de comportement observable dans les 3
  fixtures réelles. Toute autre divergence (nombre d'isolements, autres Points, autres warnings) est un
  échec du lot, pas un ajustement.
- Réutiliser explicitement le patron déjà établi de `NoConditionalPointCreatedWarningTracker` (Lot 055)
  pour la question de déduplication du nouveau warning — ne pas réinventer un mécanisme parallèle sans
  raison.
- `HasZeroEnergie` doit rester strictement scopé à ISOLEMENT : ne pas ajouter de champ, de constante ou
  de logique dans `UnconditionalIsolementSheetExtractionService`/`AutresJointsTouchesExtractionService`/
  `DiversExtractionService` — ces trois services restent inchangés par ce lot.
- Pour l'UI (63.7) : réutiliser le patron exact de `_stopFieldName` dans `SheetRuleForm.razor` (champ
  `form-floating` de niveau feuille, pas une sous-liste) — ne pas inventer un nouveau composant dédié
  pour un champ texte optionnel unique.
- Strict Red-Green-Refactor : test qui échoue d'abord, à chaque étape.
