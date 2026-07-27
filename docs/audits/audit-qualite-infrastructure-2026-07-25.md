# Audit qualité — ExcelETL.Infrastructure

- **Date de l'audit** : 2026-07-25
- **Commit réellement audité** : `8119f78` (2026-07-25, "Ajout des audits qualite par couche et de l'etat d'avancement global"). Le commit `d018a90` référencé par la demande n'est plus HEAD au moment de l'audit — pas d'écart fonctionnel identifié entre les deux sur `ExcelETL.Infrastructure`, mentionné par transparence.
- **Périmètre effectivement lu** : `src/ExcelETL.Infrastructure/` (repositories EF Core, `DbContext`, configurations Fluent API, `ClosedXmlWorkbookReader`/`ClosedXmlWorkbookWriter`, `IdentitySeeder`/`DefaultProfileSeeder`, migrations, `FileSystemGeneratedFileWriter`, `LocalFileStorageService`) + `tests/ExcelETL.Infrastructure.Tests/`. Les interfaces `Application` consommées ont été lues pour vérifier l'absence de fuite de type, jamais leur implémentation côté consommateur.
- **Point de vigilance migration** : la migration `20260724005133_AddTableauxApplicationsToProfiles` est bien présente dans `src/ExcelETL.Infrastructure/Persistence/Migrations/` et s'enchaîne correctement dans l'historique (voir §4) — confirmée présente et cohérente à ce jour.

---

## 1. Respect de Clean Architecture / Onion

**Constat factuel** : aucune fuite de type EF Core (`DbContext`, `DbSet<T>`, `IQueryable<T>`) ni ClosedXML (`XLWorkbook`, `IXLWorksheet`) n'a été trouvée à travers une interface `Application` consommée par une autre couche. Vérification exhaustive des 8 interfaces implémentées dans ce projet :

| Interface (Application) | Implémentation | Résultat |
|---|---|---|
| `IImportProfileStore` | `EfImportProfileStore` | RAS — `ImportProfile`/`IReadOnlyList<ImportProfile>` uniquement |
| `IExportProfileStore` | `EfExportProfileStore` | RAS |
| `IGeneratedFileArchiveStore` | `EfGeneratedFileArchiveStore` | RAS |
| `IWorkbookReader` | `ClosedXmlWorkbookReader` | RAS pour l'interface (`string?`/`bool`). Note : la classe implémente aussi `IDisposable` en plus de l'interface — le consommateur doit connaître `Dispose()`, asymétrie documentée en commentaire vs `IWorkbookWriter` (voir §7, pas une fuite de type mais un point de forme) |
| `IWorkbookWriter` | `ClosedXmlWorkbookWriter` | RAS |
| `IUserRepository` | `UserRepository` | RAS — jamais `ApplicationUser`/`IdentityResult` exposés, tout est mappé en `UserSummary`/`UserProfile`/`IdentityOperationResult` |
| `IFileStorageService` | `LocalFileStorageService` | RAS |
| `IGeneratedFileWriter` | `FileSystemGeneratedFileWriter` | RAS |
| `ISystemLogRepository` | `SystemLogRepository` | RAS |

Aucune méthode publique de repository ne retourne `IQueryable<T>` — toutes matérialisent via `ToListAsync`/`FirstOrDefaultAsync`/`SingleOrDefaultAsync` avant retour.

**Pattern `IDbContextFactory<T>` + DbContext court par méthode** : vérifié strictement sur les 7 classes concernées (`EfImportProfileStore`, `EfExportProfileStore`, `EfGeneratedFileArchiveStore`, `UserRepository`, `SystemLogRepository`). Chacune ouvre son `DbContext` via `CreateDbContextAsync` à l'intérieur de chaque méthode, jamais un `DbContext` injecté en scoped. Le rationale est documenté explicitement dans `EfImportProfileStore.cs:8-10` ("Blazor Server's long-lived circuits can invoke handlers concurrently, so a directly-injected scoped DbContext would be unsafe here") et repris par renvoi dans `EfExportProfileStore.cs:8-10`/`EfGeneratedFileArchiveStore.cs:7` plutôt que redupliqué texte pour texte.

**Exceptions au pattern identifiées et qualifiées** :
- `IdentitySeeder` consomme `UserManager<ApplicationUser>`/`RoleManager<IdentityRole>` (scoped par ASP.NET Identity lui-même), pas un `IDbContextFactory` direct.
- `DefaultProfileSeeder` ne touche ni `DbContext` ni factory — passe exclusivement par `IImportProfileStore`/`IExportProfileStore` (Application), conforme Onion et même plus strict que nécessaire.
- Aucune classe Unit-of-Work trouvée nulle part dans le projet.

**Impact estimé** : nul — le point le plus sensible de la grille (fuite de type + pattern DbContext) est intégralement respecté, y compris dans les 2 seeders qui auraient pu s'en écarter facilement.

**Refacto envisageable** : aucune. Signaler éventuellement en documentation (pas en code) que `IWorkbookReader` + `IDisposable` est un couplage à connaître pour tout futur consommateur, déjà fait en commentaire.

---

## 2. Règles métier câblées en dur vs profile-driven

**Constat factuel** :
- `DefaultProfileSeeder.cs` contient un volume important de données métier littérales (noms de Colonnes OXO, coordonnées de blocs, règles conditionnelles — lignes ~107-213 pour l'import, ~236-291 pour l'export). Ce n'est pas une réimplémentation d'algorithme d'extraction/génération : ce sont des **données de seed** construites via les constructeurs Domain (`ImportProfile`, `SheetExtractionRule`, etc.), documentées comme transcrites et vérifiées mot pour mot contre les 5 services d'extraction réels (commentaire `DefaultProfileSeeder.cs:104-106`).
- `ClosedXmlWorkbookReader.ReadCellValue` applique un rendu culture-invariant (`dd/MM/yyyy HH:mm:ss`) pour les cellules de type date — c'est un contournement technique documenté (dérive de `CultureInfo.CurrentCulture` sous ASP.NET Core, bug réel trouvé et corrigé au Lot K1) et non une règle métier d'extraction déplacée depuis Application.
- Aucun autre moteur d'extraction/génération (algorithmes eux-mêmes, ex. `ProcedureExtractionService`, `ISheetGenerationEngine`) ne vit dans ce projet — confirmé, ils restent dans `ExcelETL.Application`.

**Impact estimé** : nul — aucune règle métier n'est dupliquée dans Infrastructure ; le seeder reste un jeu de données initial, pas de la logique.

**Refacto envisageable** : aucune.

---

## 3. Duplication

**Constat factuel** : duplication réelle et **documentée** (pas silencieuse) entre `EfImportProfileStore` et `EfExportProfileStore` :
- Vérification d'unicité de nom (Trim + `OrdinalIgnoreCase`, exclusion de l'Id propre) : blocs quasi-identiques `EfImportProfileStore.cs:44-52` / `EfExportProfileStore.cs:33-41`.
- Upsert "delete-then-insert" en 2 `SaveChangesAsync` : `EfImportProfileStore.cs:54-62` / `EfExportProfileStore.cs:43-51`.
- `GetAllAsync`/`GetByIdAsync`/`DeleteAsync` structurellement identiques entre les deux stores.
- `EfExportProfileStore.cs:8-10,31-32` renvoie explicitement vers `EfImportProfileStore` en commentaire ("Symmetric to EfImportProfileStore -- same short-lived-DbContext-per-method pattern, same two-round-trip upsert") plutôt que de réexpliquer — la duplication est assumée consciemment, pas un oubli.
- Aucune factorisation (pas de `EfProfileStoreBase<T>` générique) n'existe.

**Impact estimé** : faible à moyen. Les deux fichiers font 66-77 lignes chacun — la duplication est petite en volume absolu mais porte une logique sensible (upsert + contrainte d'unicité) répétée à l'identique ; un futur 3ᵉ type de profil dupliquerait une 3ᵉ fois le même bloc.

**Refacto envisageable** (non implémentée) : un repository générique `EfNamedProfileStore<TProfile>` paramétré par le `DbSet` et le type d'exception serait possible mais le projet documente déjà (via CLAUDE.md et les commentaires en question) une préférence pour la duplication explicite plutôt qu'une abstraction générique risquant de perdre en clarté si les deux types divergent un jour (ex. `GeneratedFileRecord` n'a délibérément pas suivi ce pattern owned-type, voir §4). À documenter comme arbitrage déjà tranché plutôt que comme dette si aucune 3ᵉ occurrence n'apparaît.

---

## 4. Cohérence des conventions déjà actées

**Migrations — nommage** (vérifié directement sur disque, 8 fichiers `ExcelEtlDbContext` + 2 fichiers `ApplicationIdentityDbContext`) :

`ExcelEtlDbContext` (`Persistence/Migrations/`) : `20260710140017_InitialCreate`, `20260710174749_AddCompletedAtUtcToExtractionHistories`, `20260717113850_AddImportProfile`, `20260718092214_AddExportProfile`, `20260721095640_RemoveExtractionConfigPoc`, `20260724005133_AddTableauxApplicationsToProfiles`, `20260724115715_AddProfileNameUniqueIndexAndMaxLength`, `20260725010636_AddGeneratedFileRecord`.

`ApplicationIdentityDbContext` (`Identity/Migrations/`) : `20260710140119_InitialIdentityCreate`, `20260711090054_AddFirstNameLastNameToApplicationUser`.

Convention uniforme : préfixe timestamp `yyyyMMddHHmmss` + description PascalCase, historique séparé par table nommée explicitement (`__EFMigrationsHistory_ExcelEtl` / `__EFMigrationsHistory_Identity`, configuré dans les deux `IDesignTimeDbContextFactory`). `20260724005133_AddTableauxApplicationsToProfiles` est bien présente et s'enchaîne sans trou apparent dans la séquence — point de vigilance de la demande levé.

**Fluent API — cohérence entre entités** :
- Shadow `int Id` + `HasKey("Id")` pour toute collection `OwnsMany` sans identité propre : appliqué systématiquement dans `ImportProfileConfiguration` et `ExportProfileConfiguration`.
- `OwnsOne` utilisé une seule fois (`ImportProfileConfiguration`, pour `Locator`) — cohérent, c'est la seule relation un-à-un possédée du modèle.
- `HasConversion<string>()` pour tout enum persisté (`Operator`, `PivotSource`, `Source`, `Status`) — appliqué de façon homogène dans les 3 configurations concernées.
- Index unique sur `Name` présent sur `ImportProfile` et `ExportProfile` (ajouté Lot 027), absent sur `GeneratedFileRecord` (normal, pas de contrainte de nom sur ce type).
- `GeneratedFileRecordConfiguration` diverge délibérément des deux autres (pas d'`OwnsMany`, `ImportProfileId`/`ExportProfileId` en `Guid` dénormalisés sans FK) — divergence documentée en commentaire pour éviter qu'EF Core ne déduise une relation shadow par convention de nommage (piège déjà rencontré et documenté au Lot 034 dans CLAUDE.md). Ce n'est pas une incohérence mais une divergence volontaire et justifiée.

**Idempotence des seeders** :
- `IdentitySeeder.SeedUserAsync` : lookup par **nom** (`FindByNameAsync`), pas de Guid stable. Si l'utilisateur existe déjà, aucune donnée (`FirstName`/`LastName`/`Email`) n'est réécrite — seul le rôle Admin est revérifié/ajouté si manquant. Mot de passe seed manquant → utilisateur ignoré avec `LogWarning`, pas d'échec de démarrage.
- `DefaultProfileSeeder` : lookup par **Guid stable hardcodé** (`ImportProfileId`/`ExportProfileId`), jamais par nom — explicitement justifié en commentaire ("an admin can rename a seeded profile ... Once a profile with that Id exists, it is never touched again"). Une seule exception ciblée et documentée : `MigrateTacheMultipleSheetRuleIfMissingAsync`, qui ajoute une règle manquante à un profil déjà seedé sans toucher au reste — migration additive "narrow, one-time" reconnue comme telle en commentaire.
- Les deux seeders utilisent une stratégie de lookup différente (nom vs Guid) mais c'est un choix assumé (documenté dans les deux fichiers et dans CLAUDE.md), pas une incohérence accidentelle — les deux respectent strictement "ne jamais écraser une donnée existante/modifiée par un admin".

**Impact estimé** : nul sur ces 4 sous-points — toutes les conventions actées sont respectées et les seules divergences trouvées sont documentées comme volontaires.

**Refacto envisageable** : aucune.

---

## 5. Dette de test

**Constat factuel** — inventaire de `tests/ExcelETL.Infrastructure.Tests/` (127 `[Fact]`/`[Theory]` au total, `dotnet test --list-tests` + grep concordants ; la demande mentionnait 121 tests au 24/07, +6 depuis, cohérent avec le Lot 034 mergé le 25/07) :

| Dossier | Fichier | Tests (approx.) |
|---|---|---|
| Archiving | `FileSystemGeneratedFileWriterTests.cs` | 7 |
| Archiving | `GeneratedFileNameSanitizerTests.cs` | 3 (1 `[Theory]` à 3 cas) |
| Diagnostics | `SystemLogRepositoryTests.cs` | 4 |
| Excel | 10 fichiers (extraction/génération intégration) | ~39 au total |
| Identity | `ApplicationIdentityDbContextFactoryTests.cs`, `IdentitySeederTests.cs`, `LocalizedIdentityErrorDescriberTests.cs`, `UserRepositoryTests.cs` | 1 / 7 / 1 / 12 |
| Persistence | `ExcelEtlDbContextFactoryTests.cs` | 1 |
| Persistence/Repositories | `EfExportProfileStoreTests.cs`, `EfGeneratedFileArchiveStoreTests.cs`, `EfImportProfileStoreTests.cs` | 13 / 6 / 14 |
| Seeding | `DefaultProfileSeederPipelineIntegrationTests.cs`, `DefaultProfileSeederTests.cs` | 6 / 13 |
| Storage | `LocalFileStorageServiceTests.cs` | 2 |

**Zones avec couverture plus faible que la moyenne** :
- `LocalizedIdentityErrorDescriber.cs` (63 lignes, override d'une vingtaine de méthodes de description d'erreur Identity) n'a qu'1 test — le fichier ne teste vraisemblablement qu'un sous-ensemble des overrides, pas chacun individuellement.
- Aucun fichier de test dédié aux `DbContext` eux-mêmes (`ExcelEtlDbContext`, `ApplicationIdentityDbContext`, `SystemLogsDbContext`) — seules leurs *factories* de design-time sont testées. Cohérent dans la mesure où un `DbContext` sans logique propre (juste `OnModelCreating` délégué aux `IEntityTypeConfiguration`) n'a rien à tester isolément ; le comportement réel est couvert indirectement par les tests de repository.

**Fournisseur EF Core utilisé** : confirmé InMemory réel partout (`TestDbContextFactory`, `TestExcelEtlDbContextFactory`, `TestSystemLogsDbContextFactory`, `TestApplicationIdentityDbContextFactory`, tous `UseInMemoryDatabase`). Aucun mock de `DbContext`/`DbSet` trouvé. Seule exception : `IdentityManagerMocks` mocke `UserManager<ApplicationUser>`/`RoleManager<IdentityRole>` via Moq (classes Identity de haut niveau, pas des DbContext) — conforme à la règle CLAUDE.md documentant explicitement ce cas comme exception acceptée (repositories dépendant d'`UserManager`/`RoleManager` testés au mock plutôt qu'à l'InMemory).

**Projet `legacy/ExcelProcessingClientService.Tests`** : toujours référencé dans `ExcelETL.slnx` (`<Project Path="legacy/ExcelProcessingClientService.Tests/..." />`), donc **inclus dans la solution** — mais son inclusion dans le `.slnx` ne garantit pas son exécution en CI (hors périmètre de vérification directe ici, config CI non lue par cet audit).

**Impact estimé** : faible. La couverture globale du projet est dense (127 tests pour ~1000-1500 lignes de code source estimées) ; les seuls points bas identifiés (`LocalizedIdentityErrorDescriber`, DbContext non testés isolément) sont soit des wrappers mécaniques à faible risque, soit déjà couverts indirectement.

**Refacto envisageable** (non implémentée) : étoffer `LocalizedIdentityErrorDescriberTests.cs` en `[Theory]` couvrant chaque override si une régression de libellé y est déjà survenue ou jugée probable — pas engagé ici.

**Non couvert / incertain** : le statut CI réel (exécution effective) de `legacy/ExcelProcessingClientService.Tests` n'a pas été vérifié — seule sa présence dans `ExcelETL.slnx` a été confirmée par lecture du fichier ; vérifier la configuration CI/pipeline (hors périmètre de ce projet Infrastructure) pour confirmer si c'est toujours un trou de couverture CI comme identifié au 24/07.

---

## 6. Gestion des erreurs et logs

**Constat factuel** :
- Aucune configuration de sink Serilog dans Infrastructure (recherche `Serilog`/`UseSerilog` sans résultat de configuration réelle). Seule occurrence : un commentaire dans `Diagnostics/SystemLogsDbContext.cs:6-8` précisant explicitement que "Serilog.Sinks.MSSqlServer owns and auto-creates the physical schema of the SystemLogs table (see the UseSerilog configuration in both hosts' Program.cs)" — confirme que la configuration Serilog vit bien exclusivement dans `ExcelETL.Hosting`/les `Program.cs` des deux hosts (hors périmètre de ce projet), Infrastructure se contentant de **lire** `SystemLogs` via `SystemLogRepository`.
- Aucun `Console.WriteLine`/`Console.Write` trouvé dans `src/ExcelETL.Infrastructure`.
- Aucun `try/catch` avalant silencieusement une exception. Le seul bloc `try/catch` du projet (`ClosedXmlWorkbookWriter.cs:23-61`) logge via `logger.LogError` puis re-throw — pas d'avalage.
- Aucun mécanisme de persistance de log parallèle non documenté — le seul mécanisme d'écriture disque hors base de données est `FileSystemGeneratedFileWriter` (archivage physique des fichiers source/cible du Lot 034), qui est un composant métier explicitement documenté (`IGeneratedFileWriter`) et non un mécanisme de log caché.

**Impact estimé** : nul.

**Refacto envisageable** : aucune.

---

## 7. Lisibilité / complexité

**Constat factuel** — tailles comparées (hors migrations/snapshots auto-générés) :

| Fichier | Lignes |
|---|---|
| `Seeding/DefaultProfileSeeder.cs` | 308 |
| `Persistence/Configurations/ImportProfileConfiguration.cs` | 140 |
| `Identity/IdentitySeeder.cs` | 102 |
| `Persistence/Configurations/ExportProfileConfiguration.cs` | 103 |
| `Persistence/Repositories/EfImportProfileStore.cs` | 77 |
| `Identity/UserRepository.cs` | 74 |
| `Persistence/Repositories/EfExportProfileStore.cs` | 66 |
| `Identity/LocalizedIdentityErrorDescriber.cs` | 63 |
| `Excel/ClosedXmlWorkbookWriter.cs` | 63 |
| `Excel/ClosedXmlWorkbookReader.cs` | 58 |
| `Persistence/Repositories/EfGeneratedFileArchiveStore.cs` | 47 |
| `Archiving/FileSystemGeneratedFileWriter.cs` | 46 |

`DefaultProfileSeeder.cs` (308 lignes) est nettement le fichier le plus long du projet, ~3× `IdentitySeeder.cs` (102 lignes). La quasi-totalité de l'écart (~200 lignes, 107-307) est constituée de données de seed OXO littérales (noms de Colonnes, définitions de blocs/règles), pas de logique de contrôle — la logique proprement dite (`SeedAsync`, `SeedImportProfileAsync`, `SeedExportProfileAsync`, `MigrateTacheMultipleSheetRuleIfMissingAsync`, lignes 50-102) reste comparable en complexité à `IdentitySeeder`. Justification documentée en commentaire (référence à la spec métier vérifiée champ par champ).

Second point noté au §1 : `ClosedXmlWorkbookReader` implémente `IWorkbookReader` **et** `IDisposable`, alors que `ClosedXmlWorkbookWriter` n'a pas cette double responsabilité vis-à-vis de son interface — asymétrie de forme entre les deux classes symétriques du pipeline (lecture vs écriture), déjà documentée en commentaire dans le code mais qui vaut la peine d'être notée ici comme point de lisibilité pour un futur lecteur qui s'attendrait à une symétrie complète.

**Impact estimé** : faible. Le volume de `DefaultProfileSeeder.cs` est un volume de données, pas de complexité cyclomatique — un futur ajout de sheet/colonne OXO fera encore grossir ce fichier de façon linéaire et attendue, pas un signal de mauvaise conception.

**Refacto envisageable** (non implémentée) : si `DefaultProfileSeeder.cs` continue de grossir avec de futurs lots (nouvelles colonnes/règles OXO), envisager d'extraire les blocs de données de seed (listes de `SheetExtractionRule`/`ColumnDefinition` littérales) dans des méthodes privées statiques dédiées par feuille (`BuildProcedureSheetRule()`, `BuildIsolementSheetRule()`, etc. — le fichier a déjà commencé ce découpage pour `BuildTacheMultipleSheetRule()` au Lot T) plutôt que de laisser croître une seule méthode ; non engagé ici faute de seuil de douleur atteint.

---

## Hors périmètre — observé en passant

- La configuration Serilog réelle (sinks, enrichissement) vit dans `ExcelETL.Hosting`, pas lue en détail ici (voir §6) — cohérent avec le périmètre du projet.
- Le statut d'exécution CI effective de `legacy/ExcelProcessingClientService.Tests` (au-delà de sa présence dans `ExcelETL.slnx`) n'a pas été vérifié, la configuration CI n'étant pas dans `ExcelETL.Infrastructure` (voir §5, "Non couvert / incertain").

## Non couvert / incertain

- Statut CI réel (exécution effective, pas seulement présence dans le `.slnx`) de `legacy/ExcelProcessingClientService.Tests` — nécessite de lire la config CI, hors périmètre de ce projet.
- Aucun autre point de la grille n'est resté sans réponse factuelle.
