# Tickets TDD — Lot J : écran Blazor de profil d'export

*Document vivant (pas de suffixe de date). Symétrique du Lot F côté import
(`ImportProfileEditor.razor`/`ImportProfiles.razor`/`ImportProfileTest.razor`), appliqué au
modèle `ExportProfile` du Lot I. Dépend entièrement du Lot I (Domain/Application/Infrastructure)
— ne peut pas démarrer avant que `ExportProfile`/`SheetGenerationRule`/`IExportProfileStore`
existent réellement dans le dépôt.*

**Rappel important, pour ne pas répéter l'écart découvert par l'audit du 17/07** : ce document
*est* le cahier des charges TDD du Lot J — contrairement à `tickets-tdd-blazor-profil-import.md`
qui n'a jamais existé côté Lot F (référencé dans `CLAUDE.md` mais introuvable dans le dépôt, voir
`audit-coherence-globale-2026-07-17.md` §3 et ticket de correction H2). Une fois ce lot livré,
vérifier que ce fichier reste présent et à jour dans le dépôt (`docs/`), pas seulement dans
l'historique de conversation.

**Conventions Blazor déjà en place à respecter** (voir `etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`) :
- IDs HTML stables sur chaque élément interactif (`#xxx-input`, `#xxx-button`), jamais de
  sélection par texte ou position dans les tests bUnit.
- Construction directe de l'objet Domain réel dans un `try/catch`, erreurs localisées via
  `BusinessExceptionLocalizer.TryLocalize(ex)` — aucune duplication de règle de validation côté
  client.
- `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` sur les 3 pages, sans test bUnit dédié pour
  cet attribut (cohérent avec le reste du projet — aucune page admin n'en a).
- Liste simple sans pagination ni tri (échelle actuelle limitée), à surveiller si le nombre de
  profils grandit.

**Écart volontaire par rapport au Lot F** : contrairement à `ImportProfileEditor.razor` qui n'a
pas d'édition d'un profil existant, `ExportProfileEditor.razor` **doit** la supporter dès ce lot.
Un profil d'export sera modifié régulièrement au fil des ajustements du format cible (colonnes qui
passent de "non mappée" à mappée, ajout de feuilles) — imposer de dupliquer puis renommer
manuellement pour chaque petit ajustement serait une gêne d'usage immédiate, pas une simplification
d'architecture légitime. Cette limitation du Lot F n'est donc **pas** reconduite ici : ce n'était
qu'un choix pragmatique propre à ce lot-là, pas un principe à copier par défaut.

**Conventions de test générales** (voir `etat-des-lieux-technique.md`) : xUnit 2.9.3 +
FluentAssertions 7.0.0 + Moq, bUnit pour les composants Razor, miroir dossier-par-dossier.

**Hors périmètre explicite** : génération de la feuille Tâches Multiples (cohérent avec le Lot I
qui ne la couvre pas encore) ; exposition Web API/téléchargement M2M (dépend du Lot G).

---

## J1. `ExportProfiles.razor` — liste + navigation création/duplication

**Route** : `/export-profiles`.

**Comportement attendu**, miroir exact de `ImportProfiles.razor` :
- Liste : tableau `Name` / résumé des `SheetGenerationRule` (ex. nombre de feuilles configurées),
  chargé via `IExportProfileStore.GetAllAsync()`.
- Bouton `#create-export-profile-button` → navigation vers `/export-profiles/new` (pas de
  formulaire inline).
- Bouton par ligne `#edit-export-profile-button-{id}` → navigation vers
  `/export-profiles/{id}/edit` — charge le profil existant dans `ExportProfileEditor.razor`
  (voir J2), modifications sauvegardées sur le **même** profil (pas de création d'un nouveau).
- Bouton par ligne `#duplicate-export-profile-button-{id}` : reconstruit un **nouvel**
  `ExportProfile` à partir du profil source (toutes les `SheetGenerationRule` copiées telles
  quelles), suffixe le nom, sauvegarde et recharge la liste sans navigation serveur — reste utile
  pour partir d'un profil proche sans toucher l'original, en complément de l'édition directe.

**Tests** (bUnit) :
- Rendu de la liste à partir d'un `Mock<IExportProfileStore>` retournant N profils — assertions
  sur le contenu du tableau via IDs stables.
- Clic sur `#create-export-profile-button` déclenche la navigation attendue (`NavigationManager`).
- Clic sur `#edit-export-profile-button-{id}` navigue vers `/export-profiles/{id}/edit` avec le
  bon identifiant.
- Duplication : `SaveAsync` appelé avec un `ExportProfile` dont le nom porte le suffixe attendu et
  dont les `SheetGenerationRule` sont structurellement identiques à celles du profil source, et
  dont l'identifiant diffère du profil source (nouvel enregistrement, pas une modification).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfiles.razor`
(+ miroir `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfilesTests.cs`).

---

## J2. `ExportProfileEditor.razor` — construction ET édition du profil

**Deux routes sur le même composant** :
- `/export-profiles/new` — formulaire vierge, `SaveAsync` crée un nouveau profil.
- `/export-profiles/{id}/edit` — charge le profil existant via
  `IExportProfileStore.GetByIdAsync(id)` au chargement du composant (`OnInitializedAsync`),
  pré-remplit `_name` et reconstruit la liste des `SheetGenerationRule`/`ColumnDefinition`/
  `PointColumnDefinition` déjà configurées ; `SaveAsync` sauvegarde sur le **même** identifiant
  (mise à jour), pas de nouvel enregistrement. Si `IExportProfileStore.SaveAsync` fait déjà de
  l'upsert (comme `IImportProfileStore.SaveAsync`, voir I6), aucune signature supplémentaire
  n'est nécessaire côté store — juste s'assurer que l'entité rechargée conserve son identifiant
  d'origine avant modification.
- Si `id` ne correspond à aucun profil existant (route `/edit` avec identifiant invalide) :
  message d'erreur explicite, pas d'exception non gérée.

**Champ racine** : `_name` (`#export-profile-name-input`) — `ExportProfile` n'a qu'un nom en
champ racine (contrairement à `ImportProfile` qui porte aussi `ReperePrefix`/
`EquipementTypeElementNom`), donc pas de pré-remplissage par défaut à gérer ici (en mode création) ;
un seul test de validation "nom vide" suffit à ce niveau, pas quatre comme côté
`ImportProfileEditor`. En mode édition, `_name` est en revanche pré-rempli avec la valeur du
profil chargé (voir tests ci-dessous).

**Sous-formulaire "Ajouter une feuille" (`SheetGenerationRule`)**, un par un via
`#add-sheet-generation-rule-button` :
- `SheetName` (`#sheet-generation-rule-name-input`)
- `PivotSource` — select `Equipement`/`Isolement` (`#sheet-generation-rule-pivot-source-select`)
- Liste de `ColumnDefinition`, ajout un par un (`#add-column-definition-button`) :
  - `Header` (`#column-header-input`)
  - `Source` — select des valeurs de `PivotFieldRef` **filtrées selon le `PivotSource` choisi**
    pour la feuille (ne proposer que les champs `Equipement*` si `PivotSource = Equipement`, et
    inversement) — évite à l'UI de permettre la construction d'un profil invalide que le Domain
    rejetterait de toute façon (voir I2, validation croisée `PivotSource`/`PivotFieldRef`).
  - Option explicite **"Non mappée pour l'instant"** dans le select, qui correspond à
    `Source = null` — pas une case à cocher séparée, une valeur du même select pour rester simple.
- Liste de `PointColumnDefinition`, ajout un par un (`#add-point-column-definition-button`) :
  - `ColonneNom` (`#point-column-nom-input`)
  - `Header` (`#point-column-header-input`)
  - `MarkValue` pré-rempli à `"X"` (`#point-column-mark-value-input`), modifiable.

**Validation** : chaque ajout construit directement l'objet Domain réel dans un `try/catch`,
erreurs localisées via `BusinessExceptionLocalizer` — mêmes invariants qu'en I1 (nom de feuille
non vide, en-têtes non vides et uniques par feuille, pas de doublon de `ColonneNom`).

**Tests** (bUnit) :
- Validation avant sauvegarde : nom de profil vide, aucune `SheetGenerationRule` ajoutée — 2 tests
  dédiés (moins que les 4 côté `ImportProfileEditor`, cohérent avec le nombre réduit de champs
  racine).
- Ajout de feuille complet en un round-trip (`SheetName` + `PivotSource` + colonnes descriptives +
  colonnes Points), sauvegarde bout-en-bout, navigation vers `/export-profiles`.
- Violation d'invariant Domain (ex. en-tête dupliqué au sein d'une même feuille) → message localisé
  affiché, `SaveAsync` jamais appelé.
- Sélection `Source = "Non mappée pour l'instant"` sur une colonne → `ColumnDefinition` construite
  avec `Source = null`, sauvegardée sans erreur (test dédié, symétrique du test H/I "colonne non
  mappée = cas valide").
- Le filtrage du select `PivotFieldRef` selon `PivotSource` choisi change bien les options
  proposées (test qui change le select `PivotSource` et vérifie que les options du select `Source`
  se mettent à jour en conséquence).
- **Mode édition** : rendu sur `/export-profiles/{id}/edit` avec un `Mock<IExportProfileStore>`
  qui retourne un profil existant → `_name` et toutes les `SheetGenerationRule`/
  `ColumnDefinition`/`PointColumnDefinition` sont bien pré-remplies et affichées.
- **Sauvegarde en édition** : modification d'un champ puis sauvegarde → `SaveAsync` appelé avec
  le **même** identifiant que le profil chargé, pas un nouveau `Guid`.
- **Identifiant invalide en édition** : `GetByIdAsync` retourne `null` → message d'erreur affiché,
  aucune exception non gérée, formulaire non rendu vide silencieusement.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileEditor.razor`
(+ miroir tests).

---

## J3. `ExportProfileTest.razor` — génération en mémoire, aucun appel HTTP (route `/export-profiles/test`)

**Comportement attendu**, miroir de `ImportProfileTest.razor`, mais couvrant les deux bouts du
pipeline (import **et** export) puisque la génération a besoin d'un `ImportResult` en entrée :

1. Upload d'un fichier Excel source (`e.File.OpenReadStream(...)`).
2. `new ClosedXmlWorkbookReader(fileStream)` → `ImportPipelineOrchestrator.Run(...)` (Lot D,
   inchangé) → obtention de l'`ImportResult` pivot, **en process, sans appel HTTP** — même
   contrainte que F2.
3. Sélection d'un `ExportProfile` existant (chargé via `IExportProfileStore.GetAllAsync()`).
4. Génération en mémoire via le moteur du Lot I3 (`SheetGenerationEngine`) +
   `ClosedXmlWorkbookWriter` (I4) — toujours sans appel HTTP.
5. Résultat affiché : aperçu tabulaire par feuille générée (`Parents`/`Enfants`, une table HTML
   par feuille) **et** bouton de téléchargement du fichier binaire généré, pour inspection
   manuelle dans Excel — pas de round-trip serveur, le fichier est déjà en mémoire côté composant.

**Décision à trancher pendant ce ticket** : si l'`ImportResult` a des erreurs bloquantes
(`Equipement is null`), doit-on encore proposer la génération (fichier vide/partiel) ou bloquer
l'étape 3-4 avec un message explicite ? Recommandation : bloquer, par cohérence avec
`ImportProfileTest.razor` qui affiche déjà une alerte "File rejected" dans ce cas — inutile de
proposer de générer un fichier cible à partir d'un import déjà rejeté.

**Tests** (bUnit) :
- Round-trip complet contre une des 3 fixtures réelles (ex. C7401) : upload → import → sélection
  d'un `ExportProfile` de test → génération → assertions sur le contenu de l'aperçu tabulaire
  (valeurs connues retrouvées).
- Cas `Equipement is null` (fichier synthétique invalide) : étape génération bloquée, message
  explicite affiché, aucun appel au moteur de génération (vérifiable via `Mock`/spy si le moteur
  est injecté en interface).
- Cas D8570/`"VANNE"` : génération non bloquée malgré l'avertissement non bloquant, la ligne
  `Enfants` correspondante apparaît bien dans l'aperçu (même exigence que I5, vérifiée maintenant
  aussi au niveau UI).
- Aucune référence à `HttpClient`/`ExcelProcessingClient` dans le composant (recherche explicite,
  même test que documenté pour F2 dans `etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileTest.razor`
(+ miroir tests).

---

## J4. Câblage DI (`Program.cs`, `ExcelETL.BlazorAdmin`)

**Tests** : vérifier (via `WebApplicationFactory`/test d'intégration DI léger, ou simple lecture
de `Program.cs` si le repo n'a pas ce type de test ailleurs — s'aligner sur la convention déjà en
place pour le Lot F) que sont bien enregistrés :
- `IExportProfileStore`/`EfExportProfileStore` (`AddScoped`, même style que `IImportProfileStore`)
- Le moteur de génération (`SheetGenerationEngine` ou équivalent I3) et le writer `I4`
  (`AddSingleton`, même style que les 9 services du pipeline OXO déjà enregistrés).

**Dossier** : `src/ExcelETL.BlazorAdmin/Program.cs`.

---

## Ordre recommandé

1. **J1** (liste, le plus simple, valide le câblage `IExportProfileStore` en premier)
2. **J2** (construction du profil — le plus gros morceau, dépend de J1 pour la navigation retour)
3. **J4** (DI — peut être fait dès que J1/J2 compilent, avant J3)
4. **J3** (écran de test, dépend de J2 pour avoir des profils à sélectionner, et du Lot I3/I4 déjà
   livrés)

## Non couvert / à trancher pendant le développement

- Format exact de l'aperçu tabulaire en J3 (une table HTML par feuille suffit a priori — pas
  besoin de reproduire visuellement un rendu Excel).
- Blocage ou non de la génération sur `ImportResult` en erreur bloquante (J3) — recommandation
  donnée ci-dessus, à confirmer à l'implémentation.
- Aucune fonctionnalité d'édition d'un profil d'export existant — même limitation que Lot F,
  non réouverte ici sans nouvelle demande explicite.
