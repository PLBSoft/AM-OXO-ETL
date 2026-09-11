# Tickets TDD — Lot 071 : compteurs d'éléments sur l'historique des fichiers générés (M2M)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : l'app legacy interroge `GET /api/generated-files`/`GET /api/generated-files/{id}` pour
afficher son propre historique (`/OXO/History`). Aujourd'hui, `GeneratedFileSummaryResponse` ne porte
que des métadonnées de *run* (repère équipement, statut, noms de fichiers, utilisateur, horodatage) —
aucune information sur le *contenu* de l'extraction. Demande de Simon (discussion du 11/09) : exposer,
sans modification lourde, le nombre d'Isolements, de Points et de Tâches Multiples extraits pour chaque
entrée de l'historique. Cette information existe déjà, le temps d'un appel, dans `ImportResult` au sein
de `ProcessOxoFileService.TryArchiveAsync` — elle n'est simplement jamais reportée sur
`GeneratedFileRecord` avant d'être archivée.

---

## Décisions actées (confirmées avec Simon avant rédaction de ce ticket)

- **Champs ajoutés** : uniquement des compteurs bruts — `IsolementCount`, `PointCount`,
  `TacheMultipleCount`. Pas de `WarningCount`/`ErrorCount` dans ce lot (écarté par Simon lors de la
  discussion préalable — resterait un ajout trivial du même type si demandé plus tard, mais non retenu
  ici).
- **Nommage** : `IsolementCount`/`PointCount`/`TacheMultipleCount` — reflète directement les 3
  collections d'`ImportResult` (`Isolements`/`Points`/`TachesMultiples`), cohérent avec le vocabulaire
  Domain déjà en place plutôt qu'un vocabulaire "métier legacy" (`ChildElementCount`, écarté).
- **Type** : `int` non-nullable (jamais `int?`) — ces compteurs sont **toujours** connus au moment de
  l'archivage, y compris sur un fichier rejeté (voir 071.0, `ImportResult` porte alors des listes vides,
  jamais `null`) — aucune ambiguïté "non calculé" à distinguer de "zéro", contrairement à
  `EquipementRepere`/`TargetFileName` qui sont légitimement `null` sur rejet.

---

## Hors périmètre explicite de ce lot (ne pas rouvrir)

- `WarningCount`/`ErrorCount` (`ImportResult.Errors.Count`) — écarté par Simon, voir ci-dessus.
- Toute consommation côté app legacy (`OXOGeneratedFileSummary`, colonnes de `/OXO/History`) — dépôt
  séparé, hors périmètre de ce ticket ; voir la mémoire `project_legacy_oxo_integration.md` une fois ce
  lot livré et déployé.
- Migration de données pour un enregistrement déjà archivé dans un environnement existant — même
  raisonnement que les lots récents (066/067/068/069/070) : base de données jetable en pré-production,
  un reseed/nouvel archivage suffit ; un enregistrement archivé avant ce lot affichera simplement `0`
  pour les 3 nouveaux compteurs après migration EF, jamais une erreur.
- Toute modification du comportement d'extraction/génération lui-même — ce lot ne fait que reporter, sur
  l'enregistrement d'archive, une information déjà présente dans `ImportResult` au moment de l'archivage,
  jamais la recalculer autrement.
- `/generated-files` (page Blazor admin, `GeneratedFiles.razor`, Lot 034 + QuickGrid) — hors périmètre
  explicite ; rien n'empêche un lot ultérieur d'y ajouter les mêmes colonnes une fois ce lot livré, mais
  ce n'est pas demandé ici.

---

**Statut : implémenté, 071.0 → 071.4, tous les tests verts (voir note de clôture en fin de document).**

## 071.0. Investigation préalable (obligatoire avant tout code)

- [x] Confirmer que `ProcessOxoFileService.TryArchiveAsync` a bien `ImportResult importResult` dans son
  scope au moment de construire `new GeneratedFileRecord(...)` (`ProcessOxoFileService.cs`, méthode
  privée `TryArchiveAsync`, paramètre `importResult` déjà reçu) — confirmé par lecture directe, aucun
  changement de signature de méthode publique nécessaire.
- [x] Confirmer la forme d'`ImportResult` (`src/ExcelETL.Domain/Extraction/Pivot/ImportResult.cs`) :
  `IReadOnlyList<IsolementPivot> Isolements`, `IReadOnlyList<PointPivot> Points`,
  `IReadOnlyList<TacheMultiplePivot> TachesMultiples` — toutes non-nullables par construction
  (`ArgumentNullException.ThrowIfNull` sur les 3 dans le constructeur), donc `.Count` est toujours
  valide, y compris sur le cas de rejet.
- [x] Confirmer le comportement sur rejet (`Equipement is null`) : `ProcedureExtractionService.Extract`
  retourne, sur chaque branche de rejet, `Rejected(...)` — qui construit un `ImportResult` avec
  `Isolements`/`Points`/`TachesMultiples` **vides** (le bloc PROCEDURE n'a jamais été parcouru,
  `ImportPipelineOrchestrator` ne lance les 5 autres services que si `procedureResult.Equipement` est
  non-null, `Mock.Verify(..., Times.Never)` déjà garanti par les tests existants de l'orchestrateur) —
  donc les 3 compteurs valent `0` sur un enregistrement `Rejected`, sans branche `if` dédiée à écrire :
  une simple lecture de `.Count` produit déjà le bon résultat dans les 3 statuts
  (`Success`/`NonBlockingWarning`/`Rejected`).
- [x] Compter les sites de construction de `new GeneratedFileRecord(...)` — 13 au total (`grep -rn`),
  **1 seul en production** (`ProcessOxoFileService.cs`), les 12 autres dans 3 fichiers de test
  (`GeneratedFileRecordTests.cs`, `GeneratedFilesEndpointTests.cs`, `GeneratedFilesTests.cs`) — même
  ordre de grandeur que `Username` (Lot ajouté le 10/09), qui a choisi 3 paramètres **optionnels, en
  toute fin de constructeur, défaut `null`/`0`** précisément pour ne forcer la mise à jour d'aucun de ces
  sites de test existants. Même choix retenu ici : `isolementCount = 0, pointCount = 0,
  tacheMultipleCount = 0`, après `username` (dernière position actuelle).
- [x] Confirmer que `GeneratedFileRecordConfiguration` n'a besoin d'aucune configuration EF Core
  explicite pour un `int` non-nullable simple (contrairement à `Username`, `string?` qui nécessite
  `HasMaxLength`) — EF Core mappe par convention un `int` du CLR en colonne `NOT NULL`, aucune ligne à
  ajouter dans `Configure(...)` au-delà de la migration elle-même.
- [x] Confirmer que `GeneratedFilesController.ToSummary` est le seul point de construction de
  `GeneratedFileSummaryResponse` (utilisé par `Search` et `GetById`) — les 3 nouveaux champs y sont donc
  ajoutés une seule fois, appliqués automatiquement aux deux routes.

---

## 071.1. Domain — `GeneratedFileRecord.IsolementCount`/`PointCount`/`TacheMultipleCount`

**Comportement attendu** : `GeneratedFileRecord` gagne 3 nouveaux paramètres de constructeur
**optionnels, en toute dernière position** (après `username`), défaut `0` :

```csharp
public GeneratedFileRecord(
    Guid id,
    DateTime generatedAtUtc,
    string? equipementRepere,
    string sourceFileName,
    string sourceFilePath,
    string? targetFileName,
    string? targetFilePath,
    Guid importProfileId,
    Guid? exportProfileId,
    GeneratedFileArchiveStatus status,
    string? username = null,
    int isolementCount = 0,
    int pointCount = 0,
    int tacheMultipleCount = 0)
```

Exposés en propriétés simples `int IsolementCount/PointCount/TacheMultipleCount { get; }` (pas de
validation dédiée — un compteur négatif n'a aucun sens métier mais ne peut jamais survenir : la seule
source de ces valeurs est `IReadOnlyList<T>.Count`, toujours `>= 0`, jamais une saisie utilisateur).
Documenter en commentaire, à côté du commentaire déjà présent sur `Username`, le même raisonnement
"toujours connu au moment de l'archivage, y compris sur rejet — voir 071.0" pour qu'un futur lecteur
comprenne pourquoi ce ne sont pas des `int?`.

**Tests** (Domain, `GeneratedFileRecordTests.cs`) :
- Construction sans les 3 nouveaux paramètres → les 3 valent `0`.
- Construction avec des valeurs explicites → conservées telles quelles.

**Dossier** : `src/ExcelETL.Domain/Archiving/GeneratedFileRecord.cs`.

---

## 071.2. Application — `ProcessOxoFileService` reporte les 3 compteurs à l'archivage

**Comportement attendu** : `TryArchiveAsync` passe, à la construction de `new GeneratedFileRecord(...)`,
`isolementCount: importResult.Isolements.Count`, `pointCount: importResult.Points.Count`,
`tacheMultipleCount: importResult.TachesMultiples.Count` (arguments nommés) — **aucune branche
conditionnelle sur `status`/`Equipement is null`** : une simple lecture de `.Count` suffit dans les 3 cas
(voir 071.0, le rejet produit déjà des listes vides côté `ImportResult`).

**Tests** (Application, `ProcessOxoFileServiceTests.cs`) :
- Cas succès : l'`ImportResult` stubbé sur `Mock<IImportPipelineOrchestrator>` porte des collections non
  triviales (ex. 3 Isolements, 5 Points, 2 TachesMultiples) → le `GeneratedFileRecord` passé à
  `IGeneratedFileArchiveStore.SaveAsync` (capturé via `Mock.Setup(...).Callback<GeneratedFileRecord>(...)`
  ou équivalent, même mécanisme que les tests existants qui inspectent le record archivé) porte les
  mêmes 3 valeurs.
- **Garde-fou anti-hardcoding** (même principe que `EquipementTypeElementNom`, Lot C1) : un `ImportResult`
  stubbé avec `Equipement = null` (rejet) **mais des collections `Isolements`/`Points`/`TachesMultiples`
  volontairement non vides** (scénario artificiel, impossible en pratique per 071.0, mais qui prouve que
  le code lit vraiment `.Count` plutôt que de court-circuiter à `0` sur détection de rejet) → le record
  archivé porte quand même les comptes réels, pas `0` — ce test échouerait si `TryArchiveAsync` était
  réécrit avec une branche `status == Rejected ? 0 : importResult.X.Count`.
- Non-régression : un test existant de ce fichier (succès nominal) étendu avec une assertion sur les 3
  nouveaux champs plutôt que dupliqué — pas de nouveau test pour le cas déjà couvert.

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/ProcessOxoFileService.cs`.

---

## 071.3. Infrastructure — mapping EF + migration

**Comportement attendu** : aucune ligne à ajouter dans `GeneratedFileRecordConfiguration.Configure(...)`
au-delà de la migration elle-même (071.0 : un `int` non-nullable est déjà mappé `NOT NULL` par
convention EF Core). Générer la migration via `dotnet ef migrations add
AddElementCountsToGeneratedFileRecord --project src/ExcelETL.Infrastructure --startup-project
src/ExcelETL.WebAPI --context ExcelEtlDbContext` (jamais écrite à la main, même convention que tous les
lots précédents) — 3 colonnes `int NOT NULL` (défaut implicite `0` côté EF/SQL Server au niveau
applicatif, pas de `DEFAULT` SQL nécessaire puisque toute ligne est désormais insérée avec les 3 valeurs
explicites). Non appliquée à une base SQL Server réelle dans le cadre de ce ticket (même convention que
tous les lots récents).

**Tests** (Infrastructure, `EfGeneratedFileArchiveStoreTests.cs`) :
- Round-trip : un `GeneratedFileRecord` sauvegardé avec des compteurs non nuls, puis relu via
  `GetByIdAsync`/`SearchAsync`, restitue les mêmes 3 valeurs.
- Round-trip du défaut `0` (un record construit sans préciser les 3 paramètres) — non-régression, pas de
  valeur fantôme introduite par le mapping EF.

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Configurations/GeneratedFileRecordConfiguration.cs`,
nouvelle migration sous `src/ExcelETL.Infrastructure/Migrations/`.

---

## 071.4. WebAPI — `GeneratedFileSummaryResponse` + `GeneratedFilesController`

**Comportement attendu** : `GeneratedFileSummaryResponse` gagne 3 nouveaux membres positionnels, à la
suite de `Username` (dernière position actuelle) :

```csharp
public sealed record GeneratedFileSummaryResponse(
    Guid Id,
    DateTime GeneratedAtUtc,
    string? EquipementRepere,
    string SourceFileName,
    string? TargetFileName,
    Guid ImportProfileId,
    Guid? ExportProfileId,
    string Status,
    string SourceDownloadUrl,
    string? TargetDownloadUrl,
    string? Username,
    int IsolementCount,
    int PointCount,
    int TacheMultipleCount);
```

`GeneratedFilesController.ToSummary` (méthode privée statique, seul site de construction) ajoute
`record.IsolementCount`, `record.PointCount`, `record.TacheMultipleCount` aux 3 nouvelles positions —
s'applique automatiquement à `Search` (`GET /api/generated-files`) et `GetById`
(`GET /api/generated-files/{id}`), aucune autre modification de contrôleur.

**Tests** (WebAPI, `GeneratedFilesEndpointTests.cs`, `WebApplicationFactory<Program>`, vraie requête
HTTP) :
- Un enregistrement archivé avec des compteurs connus → le corps JSON de `GET
  /api/generated-files/{id}` contient les 3 valeurs attendues.
- Non-régression : un enregistrement `Rejected` (compteurs à `0`) → les 3 champs valent `0` dans la
  réponse, pas absents/`null` (cohérent avec le type `int` non-nullable choisi en 071.1).

**Dossier** : `src/ExcelETL.WebAPI/Contracts/GeneratedFileSummaryResponse.cs`,
`src/ExcelETL.WebAPI/Controllers/GeneratedFilesController.cs`.

---

## Ordre d'implémentation recommandé

071.0 → 071.1 → 071.2 → 071.3 → 071.4 (chaîne strictement séquentielle : 071.2 a besoin des nouveaux
paramètres de 071.1 pour compiler, 071.3 a besoin du nouveau mapping pour que le round-trip ait un sens,
071.4 a besoin des 3 valeurs réellement persistées par 071.2/071.3 pour que le test HTTP de clôture soit
significatif — pas seulement d'une valeur câblée en dur côté réponse).

---

## Note de clôture

Implémenté dans cet ordre, chaque étape passée au rouge puis au vert avant la suivante.

- **071.0/071.1** (Domain) : `GeneratedFileRecord` gagne `IsolementCount`/`PointCount`/`TacheMultipleCount`
  (`int`, défaut `0`, en toute fin de constructeur) exactement comme prévu.
- **071.2** (Application) : `ProcessOxoFileService.TryArchiveAsync` passe
  `importResult.Isolements.Count`/`.Points.Count`/`.TachesMultiples.Count` sans branche conditionnelle,
  comme prévu — garde-fou anti-hardcoding ajouté (`ImportResult` artificiellement rejeté mais aux
  collections non vides → les vrais comptes sont quand même archivés, pas `0`).
- **071.3** (Infrastructure) : **l'hypothèse d'investigation "aucune configuration EF explicite
  nécessaire pour un `int` non-nullable" s'est révélée fausse** — constat réel, pas anticipé dans le
  ticket : EF Core a refusé de matérialiser les 3 nouveaux paramètres de constructeur
  (`InvalidOperationException`, "Cannot bind ... only mapped properties can be bound"). Cause :
  `GeneratedFileRecordConfiguration` ne mappe explicitement AUCUNE propriété de ce type par pure
  convention de découverte automatique — chaque propriété existante (`Id`, `GeneratedAtUtc`, etc.) est en
  réalité déjà référencée explicitement (`Property(...)` ou `HasIndex(...)`), ce qui suffisait à la faire
  entrer dans le modèle ; une propriété jamais référencée n'y entre pas, même scalaire. Corrigé en
  ajoutant `builder.Property(r => r.IsolementCount).IsRequired();` (×3) — commentaire explicatif laissé en
  place dans le fichier de configuration pour la prochaine fois qu'un nouveau champ scalaire est ajouté à
  ce type. Migration générée via `dotnet ef migrations add AddElementCountsToGeneratedFileRecord
  --project src/ExcelETL.Infrastructure --startup-project src/ExcelETL.WebAPI --context
  ExcelEtlDbContext` (jamais écrite à la main) — 3 colonnes `int NOT NULL DEFAULT 0`, non appliquée à une
  base SQL Server réelle dans le cadre de ce ticket.
- **071.4** (WebAPI) : `GeneratedFileSummaryResponse` + `GeneratedFilesController.ToSummary` étendus
  exactement comme prévu, test HTTP réel de clôture vert (compteurs non nuls transmis, `Rejected` → `0`
  jamais `null`).

**Tests** : Domain +2, Application +3 (nominal, rejet, garde-fou anti-hardcoding), Infrastructure +2
(round-trip valeurs non nulles, round-trip défaut), WebAPI +2 (`GetById` valeurs non nulles, `Rejected` →
zéros). Solution complète recompilée et testée (`Domain.Tests` 399/399, `Application.Tests` 264/264,
`Infrastructure.Tests` 246/246, `WebAPI.Tests` 66/66, `BlazorAdmin.Tests` — fichier
`GeneratedFilesTests.cs` seul, non touché par ce ticket, toujours vert 16/16) — tous verts, aucune
régression.

**Non fait, conforme au hors périmètre** : aucune consommation côté app legacy, aucune migration de
données, aucun champ `WarningCount`, aucune modification de `/generated-files` (Blazor).
