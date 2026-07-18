# Tickets TDD — Lot F : écran Blazor de profil d'import

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). **Créé
rétroactivement** pour combler l'écart identifié par `audit-coherence-globale-2026-07-17.md` §3
et le ticket de correction H2 : `CLAUDE.md` référençait `docs/tickets-tdd-blazor-profil-import-2026-07-17.md`,
qui n'a jamais existé ni sur disque ni dans l'historique git. Ce fichier reconstruit F1/F2 à
partir de ce qui a été réellement livré (description synthétique de `CLAUDE.md`, vérifiée
ligne à ligne contre le code par l'audit du 17/07), et ajoute F3 comme complément non encore
livré. À partir de maintenant, ce document fait foi pour le Lot F — mettre à jour en place,
ne jamais empiler un nouveau fichier daté à côté pour une évolution du même lot.*

**Conventions respectées** (voir `etat-des-lieux-technique.md` et
`etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`) : xUnit 2.9.3 + FluentAssertions
7.0.0 + Moq + bUnit pour les composants Razor ; IDs HTML stables (`#xxx-input`, `#xxx-button`),
jamais de sélection par texte ou position ; construction directe de l'objet Domain réel dans un
`try/catch`, erreurs localisées via `BusinessExceptionLocalizer.TryLocalize(ex)` ; aucune
duplication de règle de validation côté client ; `[Authorize(Roles = IdentitySeeder.AdminRoleName)]`
sur les 3 pages, sans test bUnit dédié pour cet attribut (cohérent avec le reste du projet).

---

## F1. `ImportProfiles.razor` + `ImportProfileEditor.razor` — liste et construction ✅ terminé

**Statut** : livré et vérifié conforme par l'audit du 17/07 (commits `be96830`, `ff9fd6d`,
`c61f6f8`). Documenté ici pour mémoire, non réouvert.

### F1.1 `ImportProfiles.razor` — route `/import-profiles`
- Liste : tableau Name / `EquipementTypeElementNom` / nombre de règles de feuille, chargé via
  `IImportProfileStore.GetAllAsync()`.
- Bouton `#create-profile-button` → navigation vers `/import-profiles/new` (pas de formulaire
  inline).
- Bouton par ligne `#duplicate-profile-button-{id}` : reconstruit un `ImportProfile` (constructeur
  3 arguments : nom, `EquipementTypeElementNom`, `SheetRules` ; `ReperePrefix` copié tel quel via
  l'autre constructeur), suffixe le nom (`ImportProfiles_DuplicateSuffix`), sauvegarde et recharge
  la liste sans navigation serveur.

### F1.2/F1.3 `ImportProfileEditor.razor` — route `/import-profiles/new`
- 3 champs racine : `_name`, `_reperePrefix` (préinitialisé à `ImportProfile.DefaultReperePrefix`),
  `_equipementTypeElementNom` (initialisé à `string.Empty`, **aucune valeur codée en dur** —
  garde-fou anti-hardcoding vérifié).
- Sous-formulaire "Add sheet rule" : `RepeatingBlockLocator` (`SheetName`, `FirstBlockStartRow`,
  `Step`, `StopFieldName`), `BlockFieldDefinition` (`Name`, `ColumnRange`, `RowOffsetStart`,
  `RowOffsetEnd`, ajout un par un), `UnconditionalColonneNames` (liste libre, ajout un par un),
  `ConditionalPointRule` (`ColonneName`, `SourceFieldName`, `Operator`, `ComparisonValue`, ajout un
  par un).
- Validation : construction directe de l'objet Domain réel dans un `try/catch`, erreurs localisées.

**Tests** : 8 `[Fact]` dans `ImportProfileEditorTests.cs`, 5 `[Fact]` dans `ImportProfilesTests.cs` —
validation avant sauvegarde (nom vide, `ReperePrefix` vide, `EquipementTypeElementNom` vide, aucune
règle de feuille), IDs HTML stables, ajout de règle de feuille en round-trip complet, violation
d'invariant Domain (`Step <= 0`), sauvegarde bout-en-bout avec navigation.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor` +
`ImportProfileEditor.razor` (+ miroir tests
`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`).

---

## F2. `ImportProfileTest.razor` — génération/import en mémoire, route `/import-profiles/test` ✅ terminé

**Statut** : livré et vérifié conforme par l'audit du 17/07. Documenté ici pour mémoire, non
réouvert.

- Page indépendante (pas un onglet de `ImportProfiles.razor` ni de l'éditeur).
- `e.File.OpenReadStream(...)` → `new ClosedXmlWorkbookReader(fileStream)` →
  `ImportPipelineOrchestrator.Run(...)`, appel synchrone, **aucun round-trip HTTP** (vérifié :
  aucune référence à `ExcelProcessingClient`/`HttpClient`/`IExcelDownloadInterop` dans le fichier).
- Affichage : `ImportResult.Equipement is null` → alerte rouge "File rejected" listant les
  `Errors` ; sinon, tables de résultats + table "Non-blocking warnings" distincte si
  `Errors.Count > 0`.

**Tests** : `Run_D8570Fixture_ShowsVanneAsNonBlockingWarning_NotAsFileRejection` (67 isolements
rendus, libellé "Non-blocking warnings" + code `UnrecognizedTypeElement` dans le markup),
`SelectingFile_ThatFailsProcedureValidation_ShowsRejectedFileSection_NotAsAWarning` (rejet total
avec classeur synthétique invalide).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileTest.razor`
(+ miroir tests).

---

## F3. Édition d'un profil d'import existant ⬜ à faire

**Nature du ticket** : complément à F1, pas une réouverture de F1/F2 déjà livrés. L'absence
d'édition (seulement Créer/Dupliquer) était un choix pragmatique de F1 à l'origine, mais elle
s'avère gênante à l'usage : modifier un profil existant oblige aujourd'hui à le dupliquer puis à
renommer/supprimer manuellement l'original en base — inutilement lourd pour un ajustement mineur
(ex. corriger un `ColumnRange`, ajouter une `ConditionalPointRule`). Même raisonnement que celui
qui a mené à spécifier l'édition dès le départ côté `ExportProfile` (Lot J) : rien dans le domaine
n'impose cette limitation, ce n'est pas à reconduire par défaut.

**Comportement attendu** :
- Nouvelle route `/import-profiles/{id}/edit`, sur le même composant `ImportProfileEditor.razor`
  que F1.2/F1.3 (pas un composant séparé).
- Chargement du profil existant via `IImportProfileStore.GetByIdAsync(id)` au chargement du
  composant (`OnInitializedAsync`) : pré-remplit `_name`, `_reperePrefix`,
  `_equipementTypeElementNom`, et reconstruit la liste des `SheetExtractionRule` (avec leurs
  `RepeatingBlockLocator`/`BlockFieldDefinition`/`UnconditionalColonneNames`/`ConditionalPointRule`
  déjà configurés) telles qu'elles existent en base.
- `SaveProfileAsync` sauvegarde sur le **même** identifiant (mise à jour), pas un nouvel
  enregistrement — à vérifier si `IImportProfileStore.SaveAsync` fait déjà de l'upsert (voir E2) ;
  si oui, aucune signature supplémentaire n'est nécessaire côté store.
- `ImportProfiles.razor` : ajout d'un bouton par ligne `#edit-profile-button-{id}` → navigation
  vers `/import-profiles/{id}/edit`, en plus de Créer/Dupliquer déjà existants.
- Identifiant invalide en édition (route `/edit` avec `id` inexistant) : message d'erreur
  explicite, pas d'exception non gérée.

**Tests** (bUnit) :
- `ImportProfiles.razor` : clic sur `#edit-profile-button-{id}` navigue vers
  `/import-profiles/{id}/edit` avec le bon identifiant.
- `ImportProfileEditor.razor` en mode édition : rendu avec un `Mock<IImportProfileStore>`
  retournant un profil existant → tous les champs racine et toutes les `SheetExtractionRule` sont
  bien pré-remplis et affichés (y compris les sous-listes imbriquées).
- Sauvegarde en édition : modification d'un champ (ex. `_reperePrefix`) puis sauvegarde →
  `SaveAsync` appelé avec le **même** identifiant que le profil chargé, pas un nouveau `Guid`.
- Identifiant invalide : `GetByIdAsync` retourne `null` → message d'erreur affiché, formulaire non
  rendu vide silencieusement.
- Non-régression : les tests F1.2/F1.3 existants (mode création) continuent de passer sans
  modification — le mode édition est un ajout au composant, pas une réécriture de son
  comportement en création.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor` +
`ImportProfiles.razor` (extension, pas nouveau fichier) — miroir tests existants, étendus.

---

## Ce que ce document ne couvre pas

- Écriture/lecture du fichier Excel cible côté écran — voir Lot J (`ExportProfile`), document
  séparé (`tickets-tdd-blazor-profil-export.md`).
- Pagination/tri sur `ImportProfiles.razor` — non nécessaire à l'échelle actuelle, à surveiller.
