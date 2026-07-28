# État d'avancement — Pipeline d'extraction OXO (2026-07-17)

Document complémentaire à `docs/etat-des-lieux-technique.md` (14/07). Basé sur une lecture
directe du code au 17/07, avant démarrage du Lot F (écran Blazor de construction/test de
profil d'import).

## 1. Statut des Lots A-D

### `src/ExcelETL.Domain/Extraction/Primitives/` — **présent**, conforme
`DirectCell`, `RepeatingBlockLocator`, `BlockFieldDefinition`, `TextTransform` (+ `RawValue`,
`SubstringAfter`, `Concat`, `ConcatPart`, `Literal`, `FieldRef`), `ConditionOperator`,
`ConditionalPointRule`. Aucun écart de nommage ou de structure repéré vs. le modèle de domaine.

### `src/ExcelETL.Domain/Extraction/Pivot/` — **présent**, conforme
`EquipementPivot`, `IsolementPivot`, `PointPivot`, `TacheMultiplePivot`, `ImportResult`,
`ExtractionError`, `ExtractionErrorCode`. Un fichier additionnel non listé dans la question mais
faisant partie du même dossier : `ExtractionErrorCode` a actuellement 3 membres seulement
(`RequiredFieldMissing`, `UnparsableValue`, `UnrecognizedTypeElement`) — volontaire, d'autres
seront ajoutés au fil des Lots C4-C6 selon leurs besoins réels, pas anticipés.

### `src/ExcelETL.Domain/Extraction/Profile/` — **présent**, avec le champ demandé
`ImportProfile` (aggregate root, `Entity`) et `SheetExtractionRule` sont tous les deux là.

**`ImportProfile.EquipementTypeElementNom` existe déjà** ([ImportProfile.cs:21](src/ExcelETL.Domain/Extraction/Profile/ImportProfile.cs:21)),
`string`, requis non-blanc (`DomainValidationException` /
`DomainErrorCode.ImportProfile_EmptyEquipementTypeElementNom` sinon). Pas de valeur par défaut —
contrairement à `ReperePrefix` qui a un défaut `"MAD-OXO-"` via un constructeur surchargé,
`EquipementTypeElementNom` doit toujours être fourni explicitement (le doc de modèle confirme
que cette valeur est spécifique au profil, ex. `"MAD TRAVAUX"` pour un dossier MAD).

`SheetExtractionRule` porte aussi `UnconditionalColonneNames` (`IReadOnlyList<string>`, requis
non-null mais peut être vide), en plus de `SheetName`/`Locator`/`PointRules` — résout la question
de groupement des `PointRules` documentée dans le CLAUDE.md du projet (Colonnes créées sans
condition, ex. `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"` d'ISOLEMENT).

### Moteur Application — **présent**, conforme
`src/ExcelETL.Application/Extraction/Oxo/` (sous-dossier dédié, distinct de l'ancien pipeline
POC — voir §2) : `IWorkbookReader`, `TextTransformEvaluator`, `RepeatingBlockReader` (+
`IRepeatingBlockReader`, `RepeatingBlockReadResult`), `ConditionalPointRuleEvaluator` (+
`IConditionalPointRuleEvaluator`), `BlockFieldRangeCalculator`. Implémentation `IWorkbookReader`
réelle : `ClosedXmlWorkbookReader` dans `src/ExcelETL.Infrastructure/Excel/`.

### Services par feuille et orchestrateur — **partiellement présent**
Seulement 3 des 6 feuilles ont un service dédié :

| Feuille | Dossier | Statut |
|---|---|---|
| PROCEDURE | `Extraction/Oxo/Procedure/` | ✅ présent (`ProcedureExtractionService`) |
| ISOLEMENT | `Extraction/Oxo/Isolement/` | ✅ présent (`IsolementExtractionService`) |
| PLATINES | `Extraction/Oxo/Platines/` | ✅ présent (`PlatinesExtractionService`) |
| ORIFICES CAPACITES | — | ❌ absent (Lot C4) |
| AUTRES JOINTS TOUCHES | — | ❌ absent (Lot C5) |
| DIVERS | — | ❌ absent (Lot C6) |

**Aucun orchestrateur** (Lot D) — recherche de `*Orchestrator*` dans `src/` : aucun résultat.
Le point d'entrée unique fusionnant les 6 feuilles en un seul `ImportResult` n'existe pas encore.

**Conclusion Lots A-D** : Lot A (Domain) et Lot B (moteur Application générique) sont **complets**.
Lot C (services par feuille) est **à moitié fait** — 3/6 feuilles (PROCEDURE, ISOLEMENT,
PLATINES). Lot D (orchestrateur + intégration bout-en-bout sur les 3 fixtures réelles)
**n'a pas commencé**.

---

## 2. Statut du POC legacy (`ExtractionConfig`/`SheetConfig`/`CellMapping`)

**Toujours présent, coexiste avec le nouveau modèle pivot** — n'a pas été retiré malgré la
décision actée le 16/07. Fichiers concernés :

- `src/ExcelETL.Domain/Entities/ExtractionConfig.cs`
- `src/ExcelETL.Domain/Entities/SheetConfig.cs`
- `src/ExcelETL.Domain/Entities/CellMapping.cs`
- `src/ExcelETL.Infrastructure/Persistence/Configurations/SheetConfigConfiguration.cs`
- `src/ExcelETL.Infrastructure/Persistence/Configurations/CellMappingConfiguration.cs`

Le POC n'est pas mort : il est **activement utilisé** par deux pages BlazorAdmin en prod
(`Mappings.razor` et `UploadTest.razor`, voir §4) via `IExtractionConfigRepository`, et par le
Web API (`POST /api/excel/process`, `ExcelController`). Les deux pipelines (POC `ExtractionConfig`
et nouveau `ImportProfile`/pivot) restent des dossiers `Extraction/` séparés, comme documenté
dans `docs/etat-des-lieux-technique.md`.

---

## 3. Abstractions de persistance existantes

### `IExtractionConfigRepository` — **existe encore**, actif
`src/ExcelETL.Application/Extraction/IExtractionConfigRepository.cs`. Signature :
```csharp
Task<ExtractionConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<ExtractionConfig>> GetAllAsync(CancellationToken ct = default);
Task AddAsync(ExtractionConfig config, CancellationToken ct = default);
Task AddSheetAsync(Guid configId, SheetConfig sheet, CancellationToken ct = default);
Task AddCellMappingAsync(Guid configId, Guid sheetId, CellMapping mapping, CancellationToken ct = default);
```
Implémentation : `ExtractionConfigRepository` dans `src/ExcelETL.Infrastructure/Persistence/Repositories/`.

### Équivalent `IImportProfileStore` — **n'existe pas**
Recherche exhaustive (`ImportProfile` dans `src/ExcelETL.Infrastructure/`, `IImportProfileStore`,
`IImportProfileRepository`, `ImportProfileRepository`, `ImportProfileStore` dans tout `src/`) :
aucune correspondance. Confirme le point déjà noté dans le CLAUDE.md du projet — la persistance
EF Core de `ImportProfile` est **délibérément différée**, les Lots A-D sont validés contre un
profil hardcodé en mémoire. **Il n'y a aujourd'hui aucune abstraction de stockage pour
`ImportProfile`**, ni interface ni implémentation — à construire de zéro pour le futur écran
Blazor (Lot F), et c'est probablement le premier vrai sujet de conception de ce lot.

---

## 4. Conventions BlazorAdmin existantes

### Pages admin déjà en place
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` : `Dashboard.razor`, `History.razor`,
`Logs.razor`, `Mappings.razor`, `Profile.razor`, `UploadTest.razor`, `Users.razor`.

**`Mappings.razor`** est la page la plus proche structurellement d'un futur écran de profil
d'import (liste + formulaire de construction imbriqué) :
- `@attribute` d'autorisation absent sur cette page précise (à vérifier/aligner si le nouvel écran
  doit être admin-only comme `Profile.razor`/`UploadTest.razor` qui portent
  `[Authorize(Roles = IdentitySeeder.AdminRoleName)]`)
- Layout deux colonnes (`col-md-4` liste + sélection / `col-md-8` détail + formulaire d'ajout)
- Accès direct au repository injecté (`IExtractionConfigRepository`), pas de couche service
  intermédiaire côté Blazor — cohérent avec la règle CLAUDE.md (accès via Application layer, pas
  de `DbContext` direct)
- Erreurs métier affichées via `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message` dans un
  `<div class="alert alert-danger">`, sur un `catch` typé listant précisément les exceptions
  attendues (`ArgumentException`, `InvalidOperationException`, `*NotFoundException`)
- i18n via `IStringLocalizer<BlazorAdminMessages>`, clés `Mappings_*`
- IDs HTML stables sur les éléments interactifs (`new-config-name-input`, `create-config-button`,
  `add-sheet-button`, classes `mapping-source-cell-input` etc.) — convention utilisée pour le
  ciblage bUnit

### Upload de fichier — **déjà utilisé**, un seul endroit
`UploadTest.razor` est le seul consommateur de `InputFile` dans BlazorAdmin (recherche
`InputFile` dans `src/ExcelETL.BlazorAdmin/` : 1 seul fichier). Pattern :
- `<InputFile accept=".xlsx" OnChange="OnInputFileChangeAsync" disabled="@uploading" />`
- Taille max lue via `e.File.OpenReadStream(MaxFileSizeBytes)` (10 MB, constante locale à la page,
  alignée sur `UploadLimits.MaxExcelFileSizeBytes` du Web API — voir commentaire en tête de
  `@code` block)
- État de la page piloté par une `enum UploadState { Idle, Uploading, Success, Error }` locale
- Résultat traité par un client HTTP typé dédié (`ExcelProcessingClient`, exception documentée à
  l'appel direct du Web API — voir CLAUDE.md), pas via un repository/service Application
- Le flux (upload → appel HTTP → téléchargement du résultat) est un aller-retour complet vers le
  Web API existant, ce qui ne correspond **pas** au besoin du Lot F (construire/tester un profil
  d'import en local, sans round-trip HTTP vers `/api/excel/process`) — à traiter comme un nouveau
  pattern plutôt que copier celui-ci tel quel

### Pattern de test bUnit (formulaire + upload) — `UploadTestTests.cs`
`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/UploadTestTests.cs` — référence directement
applicable :
- `class UploadTestTests : BunitContext`, DI via `Services.AddSingleton<...>` dans le constructeur
  (dbContextFactory de test EF InMemory, repository réel, un `FakeExcelDownloadInterop` maison
  pour intercepter le flux de sortie sans JS interop réel)
- Simulation d'upload : `cut.FindComponent<InputFile>()` puis
  `InputFileContent.CreateFromText("dummy content", "invoice.xlsx")` +
  `inputFileComponent.UploadFiles(file)`
- HTTP simulé via `FakeHttpMessageHandler` maison (pas RichardSzalay.MockHttp), cohérent avec la
  convention notée dans le CLAUDE.md du projet
- Un helper `WithCulture(string, Action)` pour tester les deux langues sans dépendance externe

`MappingsTests.cs` existe aussi (`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/MappingsTests.cs`)
comme second exemple de référence pour un écran liste + formulaire imbriqué sans upload.

---

## Écarts avec la conception documentée

1. **`ExtractionErrorCode` n'a que 3 membres** (`RequiredFieldMissing`, `UnparsableValue`,
   `UnrecognizedTypeElement`) au lieu de couvrir par avance tous les cas d'erreur envisagés par le
   modèle de domaine — décision délibérée notée dans le code/CLAUDE.md, pas un oubli, mais à
   garder en tête : les Lots C4-C6 ajouteront probablement de nouveaux membres.
2. **`SheetExtractionRule.UnconditionalColonneNames`** n'apparaît pas nommément dans le doc de
   modèle d'origine tel que cité dans la question (qui ne liste que `SheetName`/`Locator` pour ce
   type) — c'est une extension du modèle actée en cours de Lot C2 pour résoudre le problème du
   groupement des `PointRules` sans condition. Present et fonctionnel, mais représente un
   enrichissement du modèle vs. la conception documentée initiale.
3. **Lot C incomplet** : 3 des 6 feuilles seulement (PROCEDURE, ISOLEMENT, PLATINES). ORIFICES
   CAPACITES, AUTRES JOINTS TOUCHES et DIVERS n'ont aucun code — à ne pas supposer présents avant
   de câbler un écran qui dépendrait de leur `SheetExtractionRule`.
4. **Lot D (orchestrateur) n'existe pas du tout** — aucune classe fusionnant les 6
   `SheetExtractionRule` en un seul `ImportResult` cohérent, aucun test d'intégration bout-en-bout
   sur les 3 fixtures réelles au niveau profil complet. Le futur écran Blazor de test de profil ne
   pourra pas s'appuyer sur un point d'entrée unique tant que ce lot n'est pas fait.
5. **Aucune persistance pour `ImportProfile`** (ni interface `IImportProfileStore` ni
   implémentation EF Core) — c'est un vrai trou à combler pour le Lot F, pas juste un détail
   d'infrastructure à brancher : il faudra concevoir cette abstraction depuis zéro, en Application
   layer (interface) + Infrastructure (implémentation EF Core), pour rester cohérent avec la règle
   architecturale du projet (accès données via repository, jamais `DbContext` direct dans
   WebAPI/BlazorAdmin).
6. **Le POC (`ExtractionConfig`/`SheetConfig`/`CellMapping`) n'a pas été retiré** malgré la
   décision actée le 16/07 — il reste en usage actif (2 pages BlazorAdmin + le Web API). Toute
   décision de dépréciation/retrait reste à exécuter, ce n'est pas fait.
