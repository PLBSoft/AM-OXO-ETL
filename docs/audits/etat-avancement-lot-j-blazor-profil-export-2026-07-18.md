# État d'avancement — Lot J, écran Blazor de profil d'export

**Commit de référence** : `f614aa9` ("Update living doc: Lot J (Blazor export-profile screens)
complete"), HEAD de `main` au 2026-07-18.

**Méthode** : chaque affirmation ci-dessous est vérifiée par lecture directe du fichier cité
(chemin + ligne), pas par rappel de mémoire de session. Les 20 tests bUnit du Lot J ont été
exécutés au moment de la rédaction de ce document :

```
dotnet test tests/ExcelETL.BlazorAdmin.Tests/ExcelETL.BlazorAdmin.Tests.csproj --filter "FullyQualifiedName~ExportProfile"
Réussi! - échec : 0, réussite : 20, ignorée(s) : 0, total : 20
```

---

## 1. Statut ticket par ticket (J1 à J4)

### J1 — `ExportProfiles.razor` (liste + navigation) : **terminé**

- Fichier : [`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor)
- Route `/export-profiles` : ligne 1.
- Liste via `IExportProfileStore.GetAllAsync()` : ligne 66.
- Bouton `#create-export-profile-button` → `export-profiles/new` : lignes 17-19, 68.
- Bouton par ligne `#edit-export-profile-button-@profile.Id` → `export-profiles/{id}/edit` :
  lignes 46-49, 70.
- Bouton par ligne `#duplicate-export-profile-button-@profile.Id` : lignes 50-53 ; construit un
  **nouvel** `ExportProfile` (nouveau `Guid`, via le constructeur 2-arg `ExportProfile(name,
  sheetRules)`) avec nom suffixé (`ExportProfiles_DuplicateSuffix`), sauvegarde et recharge la
  liste sans navigation : lignes 72-80.
- Tests : [`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfilesTests.cs`](../tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfilesTests.cs)
  — rendu liste + compteur de feuilles (lignes 73-95), bouton créer (105-114), bouton éditer avec
  bon id (116-129), duplication avec nouvel id + `SheetRules` structurellement identiques
  (131-152, assertion explicite `p.Id != original.Id` ligne 150 et `BeEquivalentTo` ligne 151).

### J2 — `ExportProfileEditor.razor` (construction ET édition) : **terminé**

Voir détail complet en section 2 ci-dessous (point de vérification prioritaire du ticket).

### J3 — `ExportProfileTest.razor` (génération en mémoire, sans HTTP) : **terminé**

Voir détail complet en section 2 ci-dessous.

### J4 — Câblage DI (`Program.cs`) : **terminé**

- Fichier : [`src/ExcelETL.BlazorAdmin/Program.cs`](../src/ExcelETL.BlazorAdmin/Program.cs)
- Lignes 110-115 :
  ```csharp
  // Lot J: the target-workbook generation pipeline (Lot I), wired here so /export-profiles/test can
  // run it in process. IExportProfileStore is Scoped to match IImportProfileStore's lifetime; the
  // generation engine and writer are stateless, so Singleton, matching the OXO pipeline services above.
  builder.Services.AddScoped<IExportProfileStore, EfExportProfileStore>();
  builder.Services.AddSingleton<ISheetGenerationEngine, SheetGenerationEngine>();
  builder.Services.AddSingleton<IWorkbookWriter, ClosedXmlWorkbookWriter>();
  ```
- Conforme au ticket : `IExportProfileStore` en `AddScoped` (miroir `IImportProfileStore`), moteur
  + writer en `AddSingleton` (miroir des 9 services OXO déjà enregistrés lignes 100-108).
- Comme prévu par le ticket J4 ("lecture de `Program.cs` si le repo n'a pas ce type de test
  ailleurs"), il n'y a pas de test d'intégration DI dédié — aucun précédent de ce type dans
  `BlazorAdmin.Tests` (vérifié : ni pour le Lot F, ni ailleurs). La preuve indirecte est que
  `ExportProfileTestTests.cs` résout `ISheetGenerationEngine`/`IWorkbookWriter` via son propre
  conteneur bUnit-local (lignes 67-68) et exécute le pipeline de bout en bout avec succès.
- Nav-menu : deux nouveaux liens dans [`NavMenu.razor`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor)
  lignes 65-66 (`bi-file-earmark-spreadsheet-nav-menu`, `/export-profiles`) et lignes 70-71
  (`bi-download-nav-menu`, `/export-profiles/test`), avec leurs règles CSS correspondantes dans
  [`NavMenu.razor.css`](../src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor.css) lignes
  101-106 — la paire fichier `.razor`/`.razor.css` est bien à jour (pas d'icône orpheline, cf.
  règle de maintenance documentée dans `CLAUDE.md`).

**Lot J complet, les 4 tickets sont terminés.**

---

## 2. Points de vérification prioritaires

### 2.1 Édition d'un profil existant (J2)

- **Les 3 routes existent** :
  [`ExportProfiles.razor:1`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor#L1)
  (`/export-profiles`),
  [`ExportProfileEditor.razor:1-2`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileEditor.razor#L1-L2) :
  ```csharp
  @page "/export-profiles/new"
  @page "/export-profiles/{Id:guid}/edit"
  ```
  Un seul composant, deux routes — conforme au ticket.

- **Le mode édition charge et pré-remplit** : `OnInitializedAsync`
  (`ExportProfileEditor.razor:176-193`) — si `Id` a une valeur, appelle
  `ExportProfileStore.GetByIdAsync(Id.Value)` (ligne 183), puis pré-remplit `_editingId` (190),
  `_name` (191), et `_sheetRules.AddRange(profile.SheetRules)` (192) — donc bien toute la liste
  imbriquée `SheetGenerationRule`/`ColumnDefinition`/`PointColumnDefinition` d'un coup (ces deux
  derniers sont des propriétés de `SheetGenerationRule`, aucune reconstruction champ-par-champ
  n'est nécessaire côté composant).
  Test : `EditRoute_WithExistingProfile_PrefillsNameAndSheetRules`
  (`ExportProfileEditorTests.cs:223-236`) — vérifie `_name` pré-rempli (ligne 232) et présence de
  `"Parents"`/`"Repère"`/`"TRAVAUX COMPLET"` dans le markup (233-235), preuve que les 3 niveaux
  imbriqués sont bien rendus.

- **La sauvegarde en édition modifie le même identifiant** : `SaveProfileAsync`
  (`ExportProfileEditor.razor:256-273`) :
  ```csharp
  var profile = _editingId.HasValue
      ? new ExportProfile(_editingId.Value, _name, _sheetRules)
      : new ExportProfile(_name, _sheetRules);
  ```
  (lignes 262-264) — utilise le constructeur 3-arg `ExportProfile(Guid id, ...)`
  ([`ExportProfile.cs:30`](../src/ExcelETL.Domain/Generation/Profile/ExportProfile.cs#L30)) qui
  reconstruit sous le même `Id`, jamais un nouveau `Guid.NewGuid()`. Test dédié :
  `EditRoute_SaveAfterModification_UsesSameProfileId`
  (`ExportProfileEditorTests.cs:238-255`) — assertion explicite `saved.Id.Should().Be(profile.Id)`
  (ligne 253).

- **Identifiant invalide sur `/edit`** : comportement réel = **message d'erreur, pas
  d'exception**. `OnInitializedAsync` (`ExportProfileEditor.razor:184-188`) : si
  `GetByIdAsync` retourne `null`, positionne `_notFound = true` et retourne immédiatement (pas de
  `NullReferenceException`, pas de throw). Le markup (lignes 22-25) affiche alors uniquement
  `<div id="export-profile-not-found" class="alert alert-danger">@Loc["ExportProfileEditor_ProfileNotFound"]</div>`
  et **aucun** des éléments de formulaire (`#export-profile-name-input`,
  `#save-export-profile-button`, etc. — tout le `else` du bloc `@if (_notFound)`, lignes 26-159 —
  n'est rendu). Message localisé confirmé dans le `.resx`
  ([`BlazorAdminMessages.resx:697-698`](../src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx#L697-L698)) :
  `"Export profile not found."`. Test :
  `EditRoute_WithUnknownId_DisplaysErrorAndDoesNotRenderForm`
  (`ExportProfileEditorTests.cs:257-265`) — vérifie le message (262) **et** l'absence du formulaire
  via `FindAll("#export-profile-name-input").Should().BeEmpty()` (263) et
  `FindAll("#save-export-profile-button").Should().BeEmpty()` (264) — donc bien "formulaire non
  rendu", pas seulement "rendu mais vide".

**Conclusion 2.1** : les 4 exigences du ticket sont satisfaites, avec preuve code et test pour
chacune.

### 2.2 Filtrage du select `PivotFieldRef` selon `PivotSource` (J2)

- `AvailableFieldRefs()` (`ExportProfileEditor.razor:195-196`) :
  ```csharp
  private IEnumerable<PivotFieldRef> AvailableFieldRefs() =>
      Enum.GetValues<PivotFieldRef>().Where(fieldRef => PivotFieldResolver.GetPivotSource(fieldRef) == _newSheetRule.PivotSource);
  ```
  Rendu dans le select `#column-source-select` (lignes 105-111), boucle sur `AvailableFieldRefs()`
  ligne 107 — recalculé à chaque `@bind` de `_newSheetRule.PivotSource` (ligne 82), donc réactif.
- Test dédié : `PivotSourceSelect_WhenChanged_FiltersColumnSourceOptions`
  (`ExportProfileEditorTests.cs:180-194`) — change le select `PivotSource` vers `Equipement`,
  vérifie que les options contiennent `EquipementRepere` et pas `IsolementRepere` (186-188), puis
  bascule vers `Isolement` et vérifie l'inverse (190-193). **Confirmé : oui, le filtrage change
  réellement les options proposées.**

### 2.3 Colonne "non mappée" (J2)

- Option dans le markup (`ExportProfileEditor.razor:106`) : `<option value="">@Loc["ExportProfileEditor_SourceNotMapped"]</option>`
  — valeur vide, pas de sentinelle textuelle du type `"NotMapped"`.
- `AddColumnDefinition()` (lignes 198-215) :
  ```csharp
  PivotFieldRef? source = string.IsNullOrEmpty(_newColumn.SourceValue)
      ? null
      : Enum.Parse<PivotFieldRef>(_newColumn.SourceValue);
  ```
  (lignes 204-206) — chaîne vide ⇒ `source = null` (le vrai `null` du type `PivotFieldRef?`), pas
  une valeur d'enum par défaut. `ColumnDefinition` est ensuite construit avec ce `source` (ligne
  208).
- Test : `AddColumnDefinition_WithSourceNotMapped_BuildsColumnWithNullSource_SavedWithoutError`
  (`ExportProfileEditorTests.cs:151-178`) — sauvegarde bout en bout puis
  `column.Source.Should().BeNull()` (ligne 177), donc confirmé aussi après le round-trip EF Core
  (pas juste en mémoire côté composant) : `ColumnDefinition.Source` (Domain,
  `HasConversion<string>()` sans `IsRequired()` d'après la doc vivante Lot I6) recharge bien
  `null`, pas une valeur sentinelle.

**Conclusion 2.3** : `Source = null` confirmé, pas de valeur de repli différente.

### 2.4 Aucun appel HTTP dans `ExportProfileTest.razor` (J3)

- Recherche explicite via test :
  `Component_NeverReferencesHttpClientOrExcelProcessingClient`
  (`ExportProfileTestTests.cs:363-371`) — lit le fichier source brut du composant
  (`ComponentSourcePath()`, lignes 138-153, résout `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileTest.razor`)
  et vérifie l'absence de `"HttpClient"` (368), `"ExcelProcessingClient"` (369), **et**
  `"IExcelDownloadInterop"` (370) — ce dernier check va au-delà de ce que F2 vérifiait
  explicitement dans son propre test (la doc F2 documentait déjà l'absence d'interop, ce test la
  vérifie mécaniquement pour J3).
- Confirmé par lecture directe du composant : `ExportProfileTest.razor` n'injecte que
  `IImportProfileStore`, `IImportPipelineOrchestrator`, `IExportProfileStore`,
  `ISheetGenerationEngine`, `IWorkbookWriter`, `BusinessExceptionLocalizer`, `IStringLocalizer`
  (lignes 15-21) — aucun `HttpClient`. Le téléchargement se fait via une URL `data:` construite en
  mémoire (`_downloadDataUrl`, lignes 244-245), pas de JS interop de stream (contrairement à
  `UploadTest.razor`).

### 2.5 Blocage sur `ImportResult.Equipement is null` (J3)

**Choix réellement fait : bloquer, conformément à la recommandation du ticket.**

- Rendu (`ExportProfileTest.razor:69-80`) : si `_importResult.Equipement is null`, affiche
  l'alerte `"File rejected"` (`ExportProfileTest_RejectedFileHeading`, confirmé
  [`BlazorAdminMessages.resx:778-779`](../src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx#L778-L779))
  listant les erreurs — et le bloc `else` (lignes 81-143) qui contient le select `ExportProfile`
  et le bouton `#generate-workbook-button` **n'est tout simplement pas dans l'arbre de rendu**
  dans ce cas (pas de `disabled`, l'élément n'existe pas du tout).
- Défense supplémentaire côté logique : `GenerateWorkbook()` (lignes 218-227) revérifie
  `if (_importResult?.Equipement is null) { return; }` avant même de regarder le profil
  d'export sélectionné — double garde (UI + logique), pas seulement un blocage visuel.
- Test structurel :
  `SelectingFile_ThatFailsProcedureValidation_BlocksGeneration_AndNeverCallsGenerationEngine`
  (`ExportProfileTestTests.cs:312-338`) — injecte un `Mock<ISheetGenerationEngine>` (316-317),
  soumet un classeur synthétique avec `PROCEDURE` vide (326), vérifie l'affichage `"File
  rejected"` (333), l'absence de `#generate-workbook-button`/`#export-test-export-profile-select`
  dans le DOM (334-335), et surtout `mockEngine.Verify(..., Times.Never)` (337) — donc le moteur
  de génération n'est **jamais invoqué**, pas seulement son résultat ignoré.

### 2.6 Cas D8570/`"VANNE"` (J3)

- Test : `Run_D8570Fixture_GeneratesWorkbook_DespiteNonBlockingVanneWarning`
  (`ExportProfileTestTests.cs:340-361`) — upload de la fixture réelle
  `Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx` (351), vérifie que le markup ne contient
  **pas** `"File rejected"` (354, donc l'avertissement non bloquant n'empêche pas la suite),
  sélectionne un profil d'export réel, clique `#generate-workbook-button` (357), puis :
  ```csharp
  cut.Find("#generated-sheet-Enfants-table").InnerHtml.Should().Contain("VANNE");
  ```
  (ligne 360) — **confirmé : la ligne `Enfants` correspondant à l'isolement `"VANNE"` (TypeElement
  absent du référentiel OXO, avertissement `UnrecognizedTypeElement` non bloquant côté Lot C2)
  apparaît bien dans l'aperçu généré.**

### 2.7 Câblage DI (J4)

Déjà couvert en section 1 (`Program.cs:113-115`). `IExportProfileStore` en `AddScoped`,
`ISheetGenerationEngine`/`IWorkbookWriter` en `AddSingleton` — conforme au ticket sur les deux
points.

---

## 3. Écarts avec la conception documentée

Ces éléments divergent du ticket `tickets-tdd-blazor-profil-export.md` ou nécessitent une
décision non explicitée dans celui-ci :

1. **`GetPivotSource` n'est pas nouveau pour ce lot** — le ticket J2 (ligne 106) le mentionne comme
   "voir I2, validation croisée `PivotSource`/`PivotFieldRef`" et suppose son existence ; c'est
   bien le cas (`PivotFieldResolver.GetPivotSource`, déjà livré au Lot I2, réutilisé tel quel
   ligne 196 de `ExportProfileEditor.razor`). Pas un écart en soi, mais à noter : aucune nouvelle
   API Domain/Application n'a dû être créée pour le filtrage du select — tout existait déjà.

2. **Comportement de sélection `PivotSource` par défaut** : `_newSheetRule.PivotSource` a pour
   valeur initiale `PivotSource.Equipement`
   (`ExportProfileEditor.razor:278`, `NewSheetGenerationRuleModel`), non spécifié explicitement
   dans le ticket — décision d'implémentation raisonnable (premier membre de l'énumération), à
   signaler car elle affecte les options initialement proposées dans `#column-source-select`
   avant toute interaction utilisateur.

3. **`ExportProfiles.razor` n'a pas de compteur de règles dans le ticket avec un libellé précis** —
   le ticket (ligne 48) demande juste "résumé des `SheetGenerationRule` (ex. nombre de feuilles
   configurées)" sans fixer le texte exact ; l'implémentation choisit
   `"{N} sheet rule(s)"` / `"{N} règle(s) de feuille"`
   (`ExportProfiles_SheetCount`, testé lignes 82/94 de `ExportProfilesTests.cs`) — un choix de
   formulation, pas une divergence de fond.

4. **Aucune nouvelle propriété/concept Domain découvert en cours de route** (contrairement à
   `UnconditionalColonneNames` pour le Lot C2) — le Lot J est purement une couche UI au-dessus du
   Lot I déjà stabilisé ; aucun changement Domain/Application n'a été nécessaire pendant J1-J4.
   Ceci est cohérent avec le fait que le Lot I (et son ticket propre) avait déjà fixé
   `ExportProfile`/`SheetGenerationRule`/`ColumnDefinition`/`PointColumnDefinition`/
   `PivotFieldRef`/`PivotFieldResolver` avant que J ne démarre.

5. **Pas de renommage de route ou de composant par rapport au ticket** — contrairement à certains
   lots précédents (ex. Lot F1.1 qui avait dévié de la route suggérée par son ticket), ici les 3
   routes (`/export-profiles`, `/export-profiles/new`, `/export-profiles/{id}/edit`) et les 3 noms
   de fichiers (`ExportProfiles.razor`, `ExportProfileEditor.razor`, `ExportProfileTest.razor`)
   correspondent exactement à ce que le ticket demandait.

6. **Test de non-régression HTTP plus strict que celui documenté pour F2** : le test
   `Component_NeverReferencesHttpClientOrExcelProcessingClient` (`ExportProfileTestTests.cs:363-371`)
   vérifie aussi l'absence de `"IExcelDownloadInterop"`, un troisième terme non mentionné par le
   ticket J3 (ligne 177, qui ne cite que `HttpClient`/`ExcelProcessingClient`) — ajout
   raisonnable puisque le téléchargement en J3 se fait via une URL `data:` en mémoire, donc
   l'absence de tout interop de streaming est une garantie supplémentaire cohérente avec le
   design, pas requise littéralement par le ticket.

---

## 4. Hors périmètre / non traité

- **F3 (édition de profil d'import, `tickets-tdd-blazor-profil-import.md`) n'a pas été touché par
  le Lot J.** Confirmé par grep : aucune référence à `tickets-tdd-blazor-profil-import.md` dans
  aucun des 3 fichiers `.razor` du Lot J ni dans leurs tests, et `ImportProfileEditor.razor`
  (Lot F) n'a reçu aucune modification dans les commits `843e6d4`→`f614aa9` (Lot J) — seul son
  homologue `ExportProfileEditor.razor` a été créé. Par ailleurs, ce document même
  (`tickets-tdd-blazor-profil-export.md`, ligne 9-14) rappelle explicitement que
  `tickets-tdd-blazor-profil-import.md` n'a jamais existé dans le dépôt (écart déjà noté par
  l'audit du 17/07, hors du périmètre du Lot J de le corriger).
- **Le Lot I était entièrement terminé avant que J ne démarre.** Le journal Git montre
  `ee92fc4` ("Add Lot I6: EF Core persistence of ExportProfile -- Lot I complete") puis
  `9ba8e17` ("Update living doc: Lot I ... complete") **avant** `a007590` ("Add ticket docs for
  Blazor export/import profile screens") et les commits `843e6d4`→`a755b5e` du Lot J — l'ordre
  chronologique confirme que J n'a démarré qu'après la clôture complète de I, conformément à la
  dépendance déclarée en tête du ticket J (lignes 5-7).
- **Feuille Tâches Multiples** : le ticket (ligne 38-39) exclut explicitement sa génération de ce
  lot, cohérent avec le Lot I qui ne la couvre pas non plus — confirmé, aucune trace de
  `TacheMultiple`/`TachesMultiples` dans le moteur de génération ni dans les 3 pages Razor du Lot
  J.
- **Exposition Web API / téléchargement M2M** : également explicitement hors périmètre (ticket,
  ligne 39) — confirmé, `ExcelETL.WebAPI` n'a reçu aucune modification dans les commits du Lot J.

---

## 5. Non couvert / incertain

- **Aucune vérification manuelle dans un navigateur réel n'a été effectuée pour ce document** —
  toutes les preuves reposent sur la lecture du code source et l'exécution de la suite bUnit
  (20/20 verts). Le rendu visuel réel (CSS, alignement, responsive) n'a pas été inspecté.
  L'attribut `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` est présent sur les 3 pages
  (`ExportProfiles.razor:2`, `ExportProfileEditor.razor:3`, `ExportProfileTest.razor:2`) mais,
  comme documenté dans le ticket lui-même (ligne 22-23) et cohérent avec le reste du projet,
  aucun test bUnit ne l'exerce directement — son application réelle en production (redirection
  effective d'un utilisateur non-admin) n'est donc vérifiée nulle part dans la suite de tests.
- **Le champ `_downloadFileName`** (`ExportProfileTest.razor:243`, via
  `TargetWorkbookFileNameBuilder.Build`) n'est pas explicitement asserté par les tests J3 —
  les tests vérifient la présence du lien et de son `href` (data URL), pas la valeur exacte de
  l'attribut `download`. Comportement probablement correct (le builder est déjà testé
  indépendamment au Lot I4) mais non re-vérifié au niveau UI.
- **Comportement si `IExportProfileStore.GetAllAsync()` est vide sur `/export-profiles/test`** :
  le composant gère le cas `_importProfiles.Count == 0` (ligne 45-48) et
  `_exportProfiles.Count == 0` (lignes 92-95) avec un message `form-text`, mais aucun test bUnit
  dédié ne couvre ce chemin précis (liste de profils d'export vide) — déduit par lecture du
  markup, pas confirmé par un test exécuté.
- **Traductions FR complètes** : `ExportProfiles_*`/`ExportProfileEditor_*`/`ExportProfileTest_*`
  ont été vérifiées présentes en anglais (`.resx`) et un sous-ensemble en français (`.fr.resx`,
  confirmé pour `ExportProfiles_PageTitle`/`ExportProfiles_SheetCount` via le test
  `ExportProfiles_WithExistingProfile_AndFrenchCulture_DisplaysFrenchLabels`,
  lignes 85-95 de `ExportProfilesTests.cs`) mais la couverture FR exhaustive de **toutes** les
  clés `ExportProfileEditor_*`/`ExportProfileTest_*` (une quinzaine de clés au total) n'a pas été
  vérifiée entrée par entrée dans ce document — seul un échantillon a été contrôlé.
