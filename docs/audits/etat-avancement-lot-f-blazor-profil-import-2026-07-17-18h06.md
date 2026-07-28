# État d'avancement — Lot F, écran Blazor de profil d'import (2026-07-17, 18h06)

Document complémentaire à `docs/etat-avancement-pipeline-extraction-2026-07-17.md` (même jour,
avant démarrage du Lot F). Basé sur une lecture directe du code après développement du Lot F
(commits `be96830`, `ff9fd6d`, `c61f6f8`).

**Remarque préalable** : le ticket source cité dans le CLAUDE.md du projet,
`docs/tickets-tdd-blazor-profil-import-2026-07-17.md`, **n'existe pas** dans le dépôt (ni sur
disque, ni dans l'historique git — recherche `git log --all -- "*profil-import*"` et
`find`/`grep` sur tout le repo, aucune correspondance, seule la mention dans `CLAUDE.md` existe).
La comparaison ci-dessous se fait donc contre la description F1.1/F1.2/F1.3/F2 telle que
consignée dans `CLAUDE.md`, faute d'un document de tickets consultable.

---

## 1. Statut F1 (liste + construction du profil)

### `ImportProfiles.razor` — présent, route `/import-profiles`
[ImportProfiles.razor](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor) :
- **Liste** : tableau Name / EquipementTypeElementNom / nombre de règles de feuille, chargé via
  `IImportProfileStore.GetAllAsync()`.
- **Création** : bouton "Créer" (`#create-profile-button`) qui **navigue** vers
  `/import-profiles/new` — pas de formulaire inline, conforme à la conception documentée
  (`ImportProfile` n'a pas de flux "créer avec juste un nom").
- **Duplication** : bouton par ligne (`#duplicate-profile-button-{id}`) qui reconstruit un
  `ImportProfile` via le constructeur 3 arguments (nom, `EquipementTypeElementNom`,
  `SheetRules` — `ReperePrefix` copié tel quel via l'autre constructeur), suffixe le nom
  (`ImportProfiles_DuplicateSuffix`), sauvegarde et recharge la liste sans navigation serveur.
- **Aucune action "Éditer"** un profil existant — confirmé absent du fichier, conforme à la note
  déjà présente dans `CLAUDE.md` ("Not yet built: editing an existing profile").

Aucun écart de nom de fichier ou de route par rapport à la description F1.1 du `CLAUDE.md`.

### Formulaire racine (`ImportProfileEditor.razor`, route `/import-profiles/new`)
[ImportProfileEditor.razor](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor) :
- Les 3 champs racine sont bien présents : `_name`, `_reperePrefix`, `_equipementTypeElementNom`
  (lignes 203-205), avec des `<input>` dédiés (`#profile-name-input`,
  `#profile-repere-prefix-input`, `#profile-equipement-type-element-nom-input`).
- **`_reperePrefix` est préinitialisé** à `ImportProfile.DefaultReperePrefix` (ligne 204) —
  attendu et documenté.
- **`_equipementTypeElementNom` est initialisé à `string.Empty`** (ligne 205), **aucune valeur
  codée en dur n'est pré-remplie** (pas de `"MAD TRAVAUX"` ni équivalent en dur dans le
  composant). Confirmé aussi par le test
  `NewProfile_PrefillsReperePrefixWithDefault_AndLeavesEquipementTypeElementNomEmpty`
  ([ImportProfileEditorTests.cs:74-81](tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileEditorTests.cs:74)),
  qui vérifie explicitement que ce champ est vide au chargement. Le garde-fou anti-hardcoding
  posé au Lot C1/E2 est donc respecté ici — aucune régression.

### Édition des `SheetExtractionRule` — tous les sous-éléments cités sont éditables
Sous-formulaire "Add sheet rule" ([ImportProfileEditor.razor:78-196](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor:78)) :
- **`RepeatingBlockLocator`** : `SheetName`, `FirstBlockStartRow`, `Step`, `StopFieldName` — 4
  champs éditables (`#sheet-rule-*-input`).
- **`BlockFieldDefinition`** : `Name`, `ColumnRange`, `RowOffsetStart`, `RowOffsetEnd` — liste
  construite champ par champ via "Add field" (`#block-field-*-input` + `#add-block-field-button`).
  Vérification faite sur le type Domain
  ([BlockFieldDefinition.cs:14](src/ExcelETL.Domain/Extraction/Primitives/BlockFieldDefinition.cs:14)) :
  son constructeur ne porte **que** ces 4 paramètres — pas de `TextTransform` associé à ce type,
  donc rien n'est omis côté UI par rapport à ce que `BlockFieldDefinition` expose réellement.
  (La hiérarchie `TextTransform`/`RawValue`/`SubstringAfter`/`Concat` du dossier `Primitives/`
  n'est référencée par aucun type de la chaîne `SheetExtractionRule → RepeatingBlockLocator →
  BlockFieldDefinition` — elle n'est donc pas un champ manquant de l'écran, simplement hors de la
  surface configurable par un `ImportProfile` aujourd'hui.)
- **`UnconditionalColonneNames`** : liste de chaînes libres, ajout un par un
  (`#unconditional-colonne-name-input` + `#add-unconditional-colonne-button`).
- **`ConditionalPointRule`** : `ColonneName`, `SourceFieldName`, `Operator` (select
  Equals/NotEquals), `ComparisonValue` — ajout un par un (`#point-rule-*` +
  `#add-point-rule-button`).

Validation : chaque ajout (`AddBlockField`/`AddPointRule`/`AddSheetRule`/`SaveProfileAsync`)
construit directement l'objet Domain réel dans un `try/catch` et localise l'exception via
`BusinessExceptionLocalizer` — aucune duplication de règle de validation côté client, conforme à
la convention déjà en place pour `Mappings.razor`.

### Tests bUnit associés
[ImportProfileEditorTests.cs](tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileEditorTests.cs)
(8 `[Fact]`) + [ImportProfilesTests.cs](tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfilesTests.cs)
(5 `[Fact]`) :
- **Validation avant sauvegarde** : couverte — nom vide, `ReperePrefix` vide,
  `EquipementTypeElementNom` vide, aucune règle de feuille ajoutée (4 tests dédiés, un par champ
  requis, chacun vérifiant le message localisé exact et l'absence de persistance).
- **IDs HTML stables** : couverte — tous les tests ciblent les éléments par `#id` CSS
  (`cut.Find("#...")`), jamais par texte ou position.
- **Autorisation `AdminRoleName`** : **non couverte**. Aucun des 13 tests des 3 fichiers Lot F ne
  configure `AddAuthorization()`/`SetAuthorized`/`SetNotAuthorized` (recherche `grep` sur tout
  `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`, aucune correspondance dans un fichier Lot F).
  Les pages sont rendues directement via `Render<T>()` sans passer par le routeur ni par un
  contexte d'autorisation bUnit ; le `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` présent
  sur les 3 pages n'est donc vérifié par aucun test bUnit. **Ce n'est pas une régression propre au
  Lot F** — le même constat s'applique à `Mappings.razor`/`UploadTest.razor`/`Users.razor`, aucune
  page admin n'a de test bUnit d'autorisation dans ce projet.
- Cas supplémentaires bien couverts : ajout de règle de feuille avec succès (round-trip complet
  Locator/Fields/UnconditionalColonneNames/PointRules), violation `Step <= 0` (miroir de
  l'invariant Domain), sauvegarde bout-en-bout avec navigation vers la liste.

---

## 2. Statut F2 (onglet Test)

### Composant — page séparée, pas un onglet
[ImportProfileTest.razor](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor),
route `/import-profiles/test` — **une page à part**, pas un onglet de `ImportProfiles.razor` ni de
l'éditeur. Le libellé "onglet Test" de la question ne correspond pas à l'implémentation réelle :
c'est un troisième item de menu/route indépendant.

### Exécution en process, aucun appel HTTP — confirmé
Recherche explicite dans le fichier : aucune référence à `ExcelProcessingClient`, `HttpClient`, ni
à `IExcelDownloadInterop`/JS interop de téléchargement (contrairement à `UploadTest.razor`). Le
flux réel ([ImportProfileTest.razor:243-256](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor:243)) :
`e.File.OpenReadStream(...)` → `new ClosedXmlWorkbookReader(fileStream)` (Infrastructure,
instancié directement dans le composant) → `ImportPipelineOrchestrator.Run(workbookReader,
profile)` (appel synchrone, injecté via `IImportPipelineOrchestrator`). Aucun round-trip vers
`/api/excel/process` — le point qu'on voulait explicitement éviter est bien respecté.

### Pipeline utilisé — orchestrateur du Lot D, 6 feuilles réellement couvertes
[ImportPipelineOrchestrator.cs](src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs),
classe `ImportPipelineOrchestrator` (`Extraction/Oxo/`), c'est bien le composant du Lot D. Lecture
du code (lignes 41-87) confirme les 6 appels de service : `procedureExtractionService` (PROCEDURE,
avec rejet immédiat du fichier si `Equipement is null`, les 5 autres services alors jamais
appelés), `isolementExtractionService` (ISOLEMENT),
`unconditionalIsolementSheetExtractionService` appelé **deux fois** (PLATINES puis ORIFICES
CAPACITES), `autresJointsTouchesExtractionService` (AUTRES JOINTS TOUCHES),
`diversExtractionService` (DIVERS, dont `Loc1` est diffusé sur l'Equipement et tous les
Isolements). Les 6 feuilles sont donc réellement couvertes à ce stade, pas un sous-ensemble.

Câblage DI ([Program.cs:98-106](src/ExcelETL.BlazorAdmin/Program.cs:98)) : premier host à
enregistrer ces services (`AddSingleton`), le Web API ne les expose toujours pas — conforme à ce
que documente déjà `CLAUDE.md`.

### Affichage bloquant / non-bloquant
Distinction lue directement sur `ImportResult.Equipement is null`
([ImportProfileTest.razor:64-75](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor:64)) :
si `null`, alerte rouge "File rejected" listant toutes les `Errors` ; sinon, tables de résultats
plus une table "Non-blocking warnings" distincte si `Errors.Count > 0`. Testée explicitement pour
le cas D8570/"VANNE" : `Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection`
([ImportProfileTestTests.cs:277-295](tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileTestTests.cs:277))
vérifie que le fichier n'est pas rejeté, que les 67 isolements sont bien rendus, et que le libellé
"Non-blocking warnings" + le code `UnrecognizedTypeElement` apparaissent dans le markup. Un second
test (`SelectingFile_ThatFailsProcedureValidation_ShowsRejectedFileSection_NotAsAWarning`) couvre
le chemin de rejet total avec un classeur synthétique invalide.

### Tests d'intégration F2.2 — 3 fixtures réelles, bien utilisées
[ImportProfileTestTests.cs](tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileTestTests.cs)
(6 `[Fact]`) charge les 3 fichiers réels via un helper de remontée de répertoire jusqu'à
`tests/Fixtures/` (`FixturePath`), avec un profil hardcodé (`CreateRealProfile`) qui reprend
**exactement** les plages de cellules déjà validées par
`ImportPipelineOrchestratorIntegrationTests` (Infrastructure.Tests, Lot D2). Assertions par
fixture :
- **C7401** : pas de "File rejected", repère `38-C7401` affiché, les 4 tables présentes, **23**
  lignes dans `#isolements-table` (= total attendu 8+15+0+0+0).
- **D8570** : repère `644-D8570`, **67** isolements (15+21+5+13+13, incluant la ligne "VANNE"),
  section "Non-blocking warnings" + code `UnrecognizedTypeElement` présents.
- **G6306B** : repère `602-G6306B`, **18** isolements (3+5+2+4+4), pas de rejet.

Ces 3 totaux correspondent exactement à ceux déjà documentés pour Lot D2 dans `CLAUDE.md` — le
même calcul, revalidé au niveau UI plutôt que directement au niveau orchestrateur.

**F1 et F2 sont donc tous les deux entièrement terminés** au moment de cette lecture — aucune
partie constatée manquante ou en cours dans le code présent sur `main`.

---

## 3. Écarts de conception apparus pendant le développement

- **Route `/import-profiles/test` en page séparée, pas en onglet** — la question posée supposait
  potentiellement un onglet unique avec F1 ; ce n'est pas ce qui a été construit (3 routes
  distinctes : liste, création, test). Différence structurelle à noter si un futur document de
  conception présente F1/F2 comme un seul écran à onglets.
- **`ImportProfiles.razor` — pas de bouton "Éditer"** : seules "Créer" (→ nouvelle page) et
  "Dupliquer" (in-place) existent. Éditer un profil existant nécessiterait aujourd'hui de le
  dupliquer puis de le renommer manuellement en base, ou d'attendre un futur lot dédié — déjà
  noté comme non fait dans `CLAUDE.md`, confirmé inchangé ici.
  `DuplicateProfileAsync` ([ImportProfiles.razor:68-78](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor:68))
  utilise le constructeur 4-arguments d'`ImportProfile` et recopie bien `profile.ReperePrefix`
  du profil source (pas de retour silencieux au défaut `"MAD-OXO-"`) — vérifié, aucun écart.
- **`ImportProfileEditor.razor` n'expose aucun champ pour un `TextTransform`** — confirmé être un
  non-écart : `BlockFieldDefinition` (le seul type qui pourrait théoriquement en porter un) n'a
  structurellement pas de propriété `TextTransform` dans le Domain actuel. Pas une omission côté
  Blazor, une caractéristique du modèle tel qu'il existe déjà avant le Lot F.
- **Pas de champ de recherche/tri sur `ImportProfiles.razor`** — la liste est un simple tableau
  sans pagination ni tri, cohérent avec l'échelle actuelle (profils en nombre restreint) mais à
  surveiller si le nombre de profils grandit.

Aucun renommage ni contournement technique significatif n'a été repéré au-delà de ces points —
contrairement à Lot C2 (`UnconditionalColonneNames`), le Lot F n'a pas fait émerger de nouveau
concept Domain : il consomme le modèle déjà stabilisé à l'issue du Lot E2.

---

## 4. Ce qui reste hors périmètre

- **POC (`ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`)** : **toujours actif**, non
  retiré. Confirmé : les trois fichiers existent toujours
  ([Mappings.razor](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/Mappings.razor),
  [UploadTest.razor](src/ExcelETL.BlazorAdmin/Components/Pages/Admin/UploadTest.razor)), et
  `IExtractionConfigRepository`/`ExtractionConfigRepository` restent enregistrés dans
  `Program.cs` aux côtés des nouveaux services Lot F, sans aucune dépréciation amorcée.
- **Écriture du fichier `.xlsx` cible** : **toujours hors périmètre**. Aucune trace de génération
  de classeur de sortie dans `src/ExcelETL.Application/Extraction/Oxo/` (recherche
  `SaveAs`/`XLWorkbook` : aucune correspondance dans ce dossier). Le pipeline OXO reste
  lecture-seule à ce stade.
- **Fichier Excel exemple d'un dossier REL** : **toujours aucun disponible**. `tests/Fixtures/`
  contient exactement les 3 mêmes fichiers déjà connus (C7401, D8570, "G6306B.REV" — un dossier
  MAD, pas un dossier REL malgré le suffixe "REV" dans son nom de fichier). Aucun quatrième
  fixture n'a été ajouté pendant le Lot F.

---

## Écarts avec la conception documentée

1. **Le document de tickets `docs/tickets-tdd-blazor-profil-import-2026-07-17.md`, référencé par
   `CLAUDE.md` comme base du découpage F1/F2, n'existe pas dans le dépôt.** Impossible de vérifier
   le Lot F ligne à ligne contre son cahier des charges d'origine ; cette analyse s'appuie
   uniquement sur la description synthétique déjà consignée dans `CLAUDE.md`.
2. **F2 est une route indépendante (`/import-profiles/test`), pas un onglet intégré à l'écran de
   liste ou de construction** — à corriger dans toute documentation qui présenterait F1/F2 comme
   un seul écran.
3. **Aucune fonctionnalité d'édition d'un profil existant** (`ImportProfiles.razor` n'a que
   Créer/Dupliquer) — écart déjà anticipé et documenté comme tel dans `CLAUDE.md`, confirmé
   toujours vrai après lecture du code.
4. **Aucun test bUnit ne vérifie l'attribut `[Authorize(Roles = AdminRoleName)]`** sur les 3 pages
   du Lot F — cohérent avec le reste du projet (aucune page admin n'a ce type de test), mais à
   noter explicitement puisque la question le demandait spécifiquement.
5. **Pas d'écart de modèle Domain découvert pendant le Lot F** (contrairement à
   `UnconditionalColonneNames` au Lot C2) — le Lot F consomme le modèle `ImportProfile` /
   `SheetExtractionRule` tel que stabilisé au Lot E2, sans extension ni contournement notable.
