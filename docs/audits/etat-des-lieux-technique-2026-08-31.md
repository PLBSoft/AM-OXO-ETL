# État des lieux technique — AM-OXO-ETL

**Branche** : `main`
**Commit `HEAD`** : `29aa161c48bc102d55a1ea1f83a0ec2fb0fd50e7` (2026-08-31 12:10:57 +0200)
**Message du commit** : `feat(oxo): pilote la condition Zero energie ISOLEMENT via une cellule dediee (Lot 063)`
**Date de génération de ce document** : 2026-08-31
**Arbre de travail** : propre au moment de la génération.

Ce document est un **instantané daté** (voir `docs/conventions/convention-nommage-documents.md`) :
il décrit l'état du code au commit ci-dessus et ne sera pas mis à jour après coup. Il fait suite à
`docs/audits/etat-des-lieux-technique-2026-07-28.md` (commit `7bba21e`, lot 052) — les écarts avec
ce document précédent sont signalés explicitement partout où ils existent. Écrit pour être lu par
une session Claude AI **sans accès au code** — tout ce qui n'y figure pas lui est invisible.

---

## 1. Structure de la solution / des projets

### Arborescence (`ExcelETL.slnx`)

```
/legacy/                                  (.NET Framework 4.8)
  ExcelProcessingClientService + .Tests
  NewApiPingService + .Tests
/src/
  ExcelETL.Domain
  ExcelETL.Application
  ExcelETL.Infrastructure
  ExcelETL.Hosting
  ExcelETL.WebAPI
  ExcelETL.BlazorAdmin
/tests/
  ExcelETL.Domain.Tests
  ExcelETL.Application.Tests
  ExcelETL.Infrastructure.Tests
  ExcelETL.Hosting.Tests
  ExcelETL.WebAPI.Tests
  ExcelETL.BlazorAdmin.Tests
  Fixtures/
/docs/
  audits/  conventions/  reference/  tickets/
```

Inchangée depuis le 28/07 (aucun nouveau projet créé).

### Dépendances entre projets (`<ProjectReference>` de chaque `.csproj`)

| Projet | Références vers |
|---|---|
| `ExcelETL.Domain` | **aucune** |
| `ExcelETL.Application` | `ExcelETL.Domain` |
| `ExcelETL.Infrastructure` | `ExcelETL.Domain`, `ExcelETL.Application` |
| `ExcelETL.Hosting` | **aucune** (référencé uniquement par les deux hôtes) |
| `ExcelETL.WebAPI` | `ExcelETL.Application`, `ExcelETL.Hosting`, `ExcelETL.Infrastructure` |
| `ExcelETL.BlazorAdmin` | `ExcelETL.Application`, `ExcelETL.Hosting`, `ExcelETL.Infrastructure` |

`ExcelETL.Domain` a zéro `PackageReference` et zéro `ProjectReference` (vérifié dans son `.csproj`)
— l'invariant Clean Architecture « Domain sans dépendance » est tenu. Les dépendances pointent
toutes vers l'intérieur (WebAPI/BlazorAdmin → Infrastructure/Application → Domain), aucune
référence circulaire, aucun `DbContext` injecté hors des repositories.

**Exception documentée** : `ExcelETL.BlazorAdmin` ne référence jamais `ExcelETL.WebAPI` par
`ProjectReference`, mais `ApiTest.razor` (`/api-test`) appelle le Web API en HTTP réel via un
`HttpClient` typé (`IOxoApiTestClient`/`OxoApiTestClient`), configuré uniquement côté serveur
(`OxoApiTestClientOptions`). C'est la seule voie HTTP BlazorAdmin → WebAPI de toute la solution.

### Points d'entrée et DI

**`ExcelETL.WebAPI/Program.cs`** — authentification par clé API uniquement (voir section 4),
Kestrel configuré (taille max requête, timeouts, via `UploadLimits`), connexion SQL via
`AddDbContextFactory<ExcelEtlDbContext>`, échec fail-fast si `ApiKeyAuthentication:ApiKey` absent,
enregistre le pipeline OXO complet (extraction + génération) et `IProcessOxoFileService`, applique
les migrations EF (`MigrateIfEnabledAsync<ExcelEtlDbContext>`) juste avant `app.Run()`.

**`ExcelETL.BlazorAdmin/Program.cs`** — ASP.NET Core Identity (cookie) sur `ApplicationIdentityDbContext`
(double enregistrement scoped : `AddDbContext` pour `AddEntityFrameworkStores` + `AddDbContextFactory`
pour `IUserRepository`), `AddDbContextFactory<SystemLogsDbContext>` (lecture seule), enregistre le
même pipeline OXO que le WebAPI plus `IUserManagementService`/`IOxoApiTestClient`/`IHomeIndicatorsService`,
applique les migrations des deux `DbContext` puis exécute `IdentitySeeder`/`DefaultProfileSeeder`
(tous deux gatés par des clés de config, défaut `true`).

Registrations DI complètes des deux hôtes (interface → implémentation, durée de vie) :

*WebAPI* : `IGeneratedFileWriter`→`FileSystemGeneratedFileWriter` (Singleton), `IGeneratedFileArchiveStore`→`EfGeneratedFileArchiveStore` (Scoped), `IImportProfileStore`→`EfImportProfileStore` (Scoped), `IExportProfileStore`→`EfExportProfileStore` (Scoped), `ITextTransformEvaluator`/`IConditionalPointRuleEvaluator`/`IRepeatingBlockReader`/`IHeaderRuleResolver`/`IProcedureExtractionService`/`IIsolementExtractionService`/`IUnconditionalIsolementSheetExtractionService`/`IAutresJointsTouchesExtractionService`/`IDiversExtractionService`/`IImportPipelineOrchestrator`/`ISheetGenerationEngine`/`IWorkbookWriter` (tous Singleton, stateless), `IProcessOxoFileService`→`ProcessOxoFileService` (Scoped), `BusinessExceptionLocalizer` (Singleton), `IExceptionHandler`→`GlobalExceptionHandler`.

*BlazorAdmin* : le même bloc pipeline OXO que WebAPI, plus `ISystemLogRepository`→`SystemLogRepository` (Scoped), `IUserRepository`→`UserRepository` (Scoped), `IUserManagementService`→`UserManagementService` (Scoped), `ApplicationBuildInfo` (Singleton), `IHomeIndicatorsService`→`HomeIndicatorsService` (Scoped), `IOxoApiTestClient`→`OxoApiTestClient` (typed `HttpClient`, `BaseAddress`/timeout 6 min depuis `OxoApiTestClientOptions`), `IdentitySeeder`/`DefaultProfileSeeder` (Scoped), `AuthenticationStateProvider`→`IdentityRevalidatingAuthenticationStateProvider`.

`ExcelETL.Hosting` (référencé par les deux hôtes seulement, jamais par Domain/Application/Infrastructure) expose deux extensions statiques :
- `IHostBuilder.AddOxoHostLogging(applicationName, connectionString)` — Serilog (console + sink `MSSqlServer` table `SystemLogs`), gaté `Serilog:EnableMsSqlServerSink` (défaut `true`).
- `IServiceProvider.MigrateIfEnabledAsync<TContext>(configuration)` — gaté `Database:AutoMigrate` (défaut `true`) et `Database.IsRelational()` (aucun effet sous InMemory).

Aucun changement architectural par rapport au 28/07 sur ces deux points d'entrée, hormis
l'enregistrement d'`IHomeIndicatorsService` (lot 054, page `/`) et de `IHeaderRuleResolver`
(déjà présent au 28/07, lot 047).

---

## 2. Conventions déjà adoptées

- **Nommage** : `PascalCase`, dossiers organisés par couche puis par domaine fonctionnel
  (`Extraction/Oxo/...`, `Generation/...`, `Identity/...`, `Persistence/...`).
- **Accès aux données** : exclusivement via des interfaces définies dans `Application` et
  implémentées dans `Infrastructure` — un repository/store par agrégat, **pas de générique**
  `IRepository<T>`. Exemple concret (`IUserRepository`) :
  ```csharp
  public interface IUserRepository
  {
      Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default);
      Task<UserProfile?> GetByIdAsync(string id, CancellationToken ct = default);
      Task<IdentityOperationResult> UpdateProfileAsync(string id, string firstName, string lastName, string email, CancellationToken ct = default);
      Task<IdentityOperationResult> ChangePasswordAsync(string id, string currentPassword, string newPassword, CancellationToken ct = default);
  }
  ```
  Cinq repositories/stores existent : `IImportProfileStore`, `IExportProfileStore`,
  `IGeneratedFileArchiveStore`, `IUserRepository` (hybride EF direct + `UserManager`),
  `ISystemLogRepository`. Toutes les implémentations Infrastructure ouvrent un `DbContext`
  court par opération via `IDbContextFactory<T>`, jamais un `DbContext` partagé injecté.
- **Gestion des erreurs** :
  - Domain lève `DomainValidationException`/`DomainArgumentOutOfRangeException`/
    `DomainRuleViolationException`, chacune porteuse d'un `DomainErrorCode`.
  - Application lève des exceptions dédiées porteuses d'un `ApplicationErrorCode`
    (`ImportProfileNotFoundException`, `ProfileNameAlreadyExistsException`, etc.).
  - Le Web API traduit ces exceptions via `GlobalExceptionHandler` (`IExceptionHandler`) +
    `BusinessExceptionLocalizer` en `ProblemDetails` localisés :
    ```csharp
    private static int StatusCodeFor(Exception exception) => exception switch
    {
        ImportProfileNotFoundException or ExportProfileNotFoundException => 404,
        DomainValidationException or DomainArgumentOutOfRangeException
            or WorksheetNotFoundInWorkbookException => 400,
        DomainRuleViolationException => 409,
        _ => 500
    };
    ```
    Les exceptions BCL non typées (`FileFormatException` pour un fichier non-Excel) sont
    interceptées **explicitement dans `OxoController`**, pas dans le handler global, car
    `BusinessExceptionLocalizer.TryLocalize` retourne `null` pour tout type n'implémentant pas
    `IHasDomainErrorCode`/`IHasApplicationErrorCode`.
  - **Result pattern** : rejeté par principe (« pas de pattern Result générique »), avec
    **deux exceptions délibérées et documentées** : `ImportResult` (`sealed class`, accumule
    des `ExtractionError` pendant un traitement qui continue malgré des erreurs partielles) et
    `IdentityOperationResult` (`sealed record(bool Succeeded, IReadOnlyList<string> Errors)`,
    pour les échecs Identity localisés sans exposer `Microsoft.AspNetCore.Identity` à
    Application).
- **Validation — où vivent les règles** : posées dans les constructeurs Domain (profils
  d'import/export) ou dans un `IUserValidator<ApplicationUser>` dédié
  (`ApplicationUserValidator`, Infrastructure) pour les comptes. Les formulaires Blazor ne
  dupliquent jamais ces règles côté client — ils affichent le message localisé renvoyé par
  l'exception via `BusinessExceptionLocalizer`, éventuellement doublé d'un texte d'aide
  purement informatif (ex. `Users_UserNameHelp`).
- **Mapping** : pas de bibliothèque de mapping (AutoMapper/Mapster) — projections manuelles
  explicites (ex. `UserRepository.GetAllAsync` projette directement en LINQ vers `UserSummary`).
- **Documents de décision transverses** (au lieu d'ADR formels, voir section 6) : plusieurs
  documents « vivants » sous `docs/conventions/` (mis à jour en place, jamais dupliqués par
  date), notamment `convention-autorisation-pages-blazoradmin.md`,
  `convention-secrets-production.md` (⚠️ contredite en pratique, voir section 7),
  `convention-ui-blazor-alignement-boutons.md`, `convention-ui-blazor-icones-boutons.md`.

---

## 3. Modèle EF Core existant

Trois `DbContext`, chacun avec son propre historique de migrations
(`__EFMigrationsHistory_ExcelEtl` / `__EFMigrationsHistory_Identity`) ; le troisième n'a pas de
migrations propres.

### `ExcelEtlDbContext` (`src/ExcelETL.Infrastructure/Persistence/`)

`DbSet` exposés : `ImportProfiles`, `ExportProfiles`, `GeneratedFileRecords`.
`OnModelCreating` applique toutes les `IEntityTypeConfiguration<T>` de l'assembly automatiquement
(`modelBuilder.ApplyConfigurationsFromAssembly(...)`).

Migrations, ordre chronologique (**10**, +1 depuis le 28/07) :

| # | Migration | Contenu |
|---|---|---|
| 1 | `20260710140017_InitialCreate` | Schéma initial (pipeline POC, retiré depuis) |
| 2 | `20260710174749_AddCompletedAtUtcToExtractionHistories` | (table depuis retirée) |
| 3 | `20260717113850_AddImportProfile` | `ImportProfile` + règles de feuille |
| 4 | `20260718092214_AddExportProfile` | `ExportProfile` + règles de feuille |
| 5 | `20260721095640_RemoveExtractionConfigPoc` | Suppression du pipeline POC |
| 6 | `20260724005133_AddTableauxApplicationsToProfiles` | Colonnes Tableaux/Applications |
| 7 | `20260724115715_AddProfileNameUniqueIndexAndMaxLength` | Index unique sur `Name`, longueur max |
| 8 | `20260725010636_AddGeneratedFileRecord` | Table `GeneratedFileRecords` (archivage) |
| 9 | `20260727215239_AddHeaderRulesToImportProfile` | `HeaderFieldRule`/`HeaderCompositeRule` (lot 047) |
| **10** | **`20260831095729_AddZeroEnergieExpectedValueToImportProfileSheetRule`** | **Nouvelle depuis le 28/07 — colonne `ZeroEnergieExpectedValue` sur `ImportProfileSheetRules` (lot 063)** |

Mapping — types persistés et emplacement Fluent API :

| Entité | Forme Domain | Fichier de mapping | Types owned |
|---|---|---|---|
| `ImportProfile` | `sealed class : Entity` | `Persistence/Configurations/ImportProfileConfiguration.cs` | `SheetRules` (`OwnsMany`) → `Locator` (`OwnsOne`) → `Fields` (`OwnsMany`), `PointRules` (`OwnsMany`), `HeaderFields` (`OwnsMany`, `Cell` table-split via `OwnsOne` imbriqué), `HeaderComposites` (`OwnsMany`) ; `DefaultTableaux`/`DefaultApplicationNames` = collections primitives JSON |
| `ExportProfile` | `sealed record` (équité structurelle, pas `Entity`) | `ExportProfileConfiguration.cs` | `SheetRules` (`OwnsMany`) → `ColumnDefinitions`/`PointColumnDefinitions`/`ApplicationColumnDefinitions` (chacune `OwnsMany`) |
| `GeneratedFileRecord` | `sealed class`, plate, sans propriétaire | `GeneratedFileRecordConfiguration.cs` | aucun — `ImportProfileId`/`ExportProfileId` configurés explicitement en scalaires pour empêcher la découverte de FK par convention EF |
| `ApplicationUser : IdentityUser` | Identity standard + `FirstName`/`LastName`/`RequirePasswordChangeOnFirstLogin` | mapping inline dans `ApplicationIdentityDbContext.OnModelCreating` (pas de fichier `Configuration` dédié) | — |

**Fluent API exclusivement** — vérifié dans les 3 fichiers `*Configuration.cs` ci-dessus (builders
`EntityTypeBuilder<T>`, `OwnsMany`/`OwnsOne`, `HasIndex().IsUnique()`) ; aucune Data Annotation
(`[Required]`/`[MaxLength]`) trouvée sur un type persisté.

### `ApplicationIdentityDbContext` (`src/ExcelETL.Infrastructure/Identity/`)

Migrations (**4**, inchangé depuis le 28/07) :

| # | Migration | Contenu |
|---|---|---|
| 1 | `20260710140119_InitialIdentityCreate` | Schéma Identity standard |
| 2 | `20260711090054_AddFirstNameLastNameToApplicationUser` | Colonnes `FirstName`/`LastName` |
| 3 | `20260727130533_AddRequirePasswordChangeOnFirstLoginToApplicationUser` | Colonne booléenne (lot 044) |
| 4 | `20260728004819_AddUniqueEmailIndexAndNameLengthsToApplicationUser` | Index unique **filtré** sur `NormalizedEmail`, longueur max `FirstName`/`LastName` (lot 050) |

`OnModelCreating` appelle `base.OnModelCreating` puis pose, inline :
```csharp
user.HasIndex(u => u.NormalizedEmail).IsUnique()
    .HasDatabaseName("EmailIndex").HasFilter("[NormalizedEmail] IS NOT NULL");
user.Property(u => u.FirstName).HasMaxLength(50);
user.Property(u => u.LastName).HasMaxLength(50);
```

### `SystemLogsDbContext` (`src/ExcelETL.Infrastructure/Diagnostics/`)

`DbSet SystemLogs`, **lecture seule**, aucun dossier `Migrations/` — le schéma physique est
créé/possédé par le sink Serilog `MSSqlServer` (`AutoCreateSqlTable = true`), pas par EF Core.

### Application automatique au démarrage

`IServiceProvider.MigrateIfEnabledAsync<TContext>(configuration)` (`ExcelETL.Hosting`), appelée par
les deux hôtes juste avant/pendant le démarrage, gatée par `Database:AutoMigrate` (défaut `true`)
et `Database.IsRelational()`. Seule mécanique d'application — aucun script SQL séparé dans le
dépôt.

### Statut de vérification contre un vrai SQL Server — **toujours ouvert, aggravé**

Le rapport cité par le guide de déploiement (`audit-verification-base-de-donnees-2026-07-27.md`,
« 10/10 migrations, seeding OK ») **n'existe dans aucun sous-dossier de `docs/`** au commit courant
— recherche exhaustive infructueuse. Il est cité une seule fois, dans
`docs/conventions/guide-deploiement-am-oxo-etl-windows-server.md` §0, sans qu'aucune copie ne soit
présente dans le dépôt : citation pendante, invérifiable directement.

Même en admettant ce rapport comme véridique à sa date, il couvrirait au mieux les migrations
jusqu'à `20260725010636_AddGeneratedFileRecord` (8 migrations `ExcelEtl` + 2 `Identity` selon le
document du 28/07 qui, lui, cite le rapport en détail). **Trois migrations `ExcelEtl` de plus** ont
été ajoutées depuis, dont la plus récente (lot 063, colonne `ZeroEnergieExpectedValue`) :
- `20260727215239_AddHeaderRulesToImportProfile`
- `20260831095729_AddZeroEnergieExpectedValueToImportProfileSheetRule`

`CLAUDE.md` déclare explicitement, pour chacune de ces migrations et pour
`20260724115715_AddProfileNameUniqueIndexAndMaxLength`/`20260728004819_AddUniqueEmailIndexAndNameLengthsToApplicationUser` :
« not yet applied to any real SQL Server database as of this lot ». **Aucune preuve dans le dépôt
qu'une seule des migrations créées depuis le 25/07 ait jamais été appliquée à un vrai serveur SQL
Server**, y compris le serveur de déploiement cible.

### Contraintes non vérifiables avec le provider InMemory

Tous les tests EF Core utilisent `Microsoft.EntityFrameworkCore.InMemory` (jamais SQLite, jamais un
vrai SQL Server). Ce provider ignore silencieusement les index uniques (y compris filtrés :
`EmailIndex`, `IX_ImportProfiles_Name`, `IX_ExportProfiles_Name`) et les contraintes
`HasMaxLength`. Un test dédié (`ApplicationIdentityDbContextModelTests`) vérifie que
l'index/le filtre/les longueurs sont bien **déclarés** dans le modèle — pas leur effet réel en
base.

---

## 4. Authentification / autorisation

### Mécanisme

- **Web API** : authentification par **clé API** uniquement (`ApiKeyAuthenticationHandler`,
  schéma `ApiKeyAuthenticationDefaults.SchemeName`, en-tête `X-Api-Key`), comparaison en temps
  constant :
  ```csharp
  private bool IsValidKey(string providedKey) => CryptographicOperations.FixedTimeEquals(
      Encoding.UTF8.GetBytes(providedKey), Encoding.UTF8.GetBytes(Options.ApiKey));
  ```
  `FallbackPolicy` = authentification requise pour tout endpoint. Démarrage fail-fast
  (`InvalidOperationException`) si `ApiKeyAuthentication:ApiKey` absent. Aucun compte
  utilisateur, aucune notion d'Identity dans cet hôte.
- **BlazorAdmin** : ASP.NET Core Identity (cookie), un seul rôle existant : `Admin`
  (`IdentitySeeder.AdminRoleName`). `AddIdentity<ApplicationUser, IdentityRole>` configure :
  ```csharp
  options.User.AllowedUserNameCharacters = "...lettres/chiffres/_/. sans -@+...";
  options.User.RequireUniqueEmail = true;
  ```
  chaîné avec `.AddErrorDescriber<LocalizedIdentityErrorDescriber>()`,
  `.AddUserValidator<ApplicationUserValidator>()`,
  `.AddClaimsPrincipalFactory<RequirePasswordChangeClaimsPrincipalFactory>()`.
  `FallbackPolicy` globale = authentification requise, **sans rôle** — chaque page pose
  explicitement son propre niveau via son attribut `@attribute`.

### Tableau — une ligne par route de `BlazorAdmin` (revérifié au commit courant)

| Route | Attribut de page | Rôle exigé |
|---|---|---|
| `/` (`Home.razor`, lot 054) | `[Authorize]` | Authentifié, aucun rôle |
| `/import-profiles`, `/export-profiles`, éditeurs, pages de test, `/api-test`, `/generated-files`, `/profile` | `[Authorize]` | Authentifié, aucun rôle |
| `/users` | `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` | **Admin** |
| `/logs` | `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` | **Admin** |
| `/Account/Login` | `[AllowAnonymous]` | — |
| `/Account/AccessDenied` | `[AllowAnonymous]` | — |
| `/Account/ForcePasswordChange` | `[Authorize]` | Authentifié, aucun rôle |
| `/Error`, `/not-found` | aucun | atteint par ré-exécution du pipeline après authz déjà tranchée |

Seules `/users` et `/logs` exigent `Admin`. **Aucune page métier ne dépend de la
`FallbackPolicy` par omission** — chacune déclare son attribut explicitement (règle posée par
`docs/conventions/convention-autorisation-pages-blazoradmin.md` §4). Ce modèle à deux niveaux
(Admin = administration de l'app uniquement / Authentifié = toute la fonctionnalité métier) est
stable depuis le lot 052 et n'a pas évolué depuis.

`/Account/Register` **n'existe plus** (retiré au lot 051) — confirmé par recherche : aucun fichier
`Register.razor`, aucune route `@page "/Account/Register"`, aucune clé `Register_*`/`NavMenu_Register`
dans les `.resx`. Un compte ne peut être créé que par un Admin via `/users`.

### `ApiKeyAuthenticationHandler` / seeders — inchangés depuis le 28/07

- Clé API lue via `ApiKeyAuthentication:ApiKey` (dev : `appsettings.Development.json`, valeur
  placeholder `"dev-local-api-key-CHANGE-ME"` ; prod : voir section 7, écart réel constaté).
- `IdentitySeeder` (rôle `Admin` + comptes `AdminSeedUsers`, mots de passe séparés via
  `AdminSeedPasswords:{UserName}`, jamais commités) et `DefaultProfileSeeder` (profils
  import/export par défaut, recherchés par `Guid` fixe, jamais par nom) — tous deux idempotents,
  gatés par `IdentitySeeding:Enabled`/`ProfileSeeding:Enabled` (défaut `true`).

### Parcours de premier accès et verrou mot de passe temporaire (lots 044/045/049) — inchangé

`Users.razor` (Admin) crée un compte avec mot de passe temporaire aléatoire
(`TemporaryPasswordGenerator`) + `RequirePasswordChangeOnFirstLogin = true`. `Login.razor`
redirige vers `/Account/ForcePasswordChange` avant toute prise en compte de `ReturnUrl`.
`RequirePasswordChangeClaimsPrincipalFactory` porte une claim tant que le drapeau est vrai ;
`PasswordChangeGuard.razor` (rendu en tête de `MainLayout`) intercepte toute navigation (fraîche
ou en circuit déjà ouvert via `<NavigationLock>`) hors `Account/ForcePasswordChange`,
`Account/Logout`, `not-found`, `error`.

### `AccessDenied.razor` / trois sorties d'échec — inchangé

| Situation | Sortie |
|---|---|
| Non authentifié, route protégée | `302` → `/Account/Login` |
| Authentifié, rôle insuffisant (`/users`/`/logs`) | `302` → `/Account/AccessDenied` |
| Authentifié, route inexistante | `200`, `NotFound.razor` |

Cette distinction est tranchée au niveau du middleware ASP.NET Core (`FallbackPolicy`), avant que
le routeur Blazor n'intervienne — non observable en bUnit seul, prouvée uniquement par des tests
HTTP réels (voir section 5).

### Extension pour de nouveaux écrans/profils

Aucun mécanisme de policy nommée (`AddPolicy`) n'existe — uniquement `[Authorize]` /
`[Authorize(Roles = ...)]` littéral. Ajouter un nouvel écran métier accessible à tout compte
authentifié = `[Authorize]` seul, cohérent avec le modèle à deux niveaux ; un écran réservé à une
future catégorie intermédiaire nécessiterait une décision explicite (le modèle actuel est
volontairement **binaire**, voir `convention-autorisation-pages-blazoradmin.md`).

---

## 5. Conventions de tests

### Structure et frameworks

- xUnit 2.9.3 + **FluentAssertions épinglée à `7.0.0` exactement** (v8+ commercial, incompatible
  avec la contrainte OSS) + Moq 4.20.72.
- bUnit 2.7.2 pour les composants Blazor (`class FooTests : BunitContext`, `Render<Foo>()`).
- `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) pour les tests HTTP réels
  — présent dans `ExcelETL.WebAPI.Tests` **et**, depuis le lot 049, dans
  `ExcelETL.BlazorAdmin.Tests` (10 fichiers au total, listés ci-dessous).
- `Microsoft.EntityFrameworkCore.InMemory` pour tous les tests de repository.
- Pas de bibliothèque HTTP-mock : un `FakeHttpMessageHandler` maison, colocalisé par projet
  (`legacy/ExcelProcessingClientService.Tests/`, `tests/ExcelETL.BlazorAdmin.Tests/Services/`).
  Autres doubles maison : `CapturingLogger` (Infrastructure.Tests **et** WebAPI.Tests, deux
  copies indépendantes, convention « pas de helper de test partagé » du dépôt).

### Convention de nommage / structure

Un dossier de test miroir exact de son dossier `src/` (`tests/ExcelETL.Domain.Tests/Extraction/Profile/`
pour `src/ExcelETL.Domain/Extraction/Profile/`). Une classe `{Type}Tests` par type testé. Méthodes
`MethodeOuScenario_Condition_ResultatAttendu` (ex. `Constructor_WithInvalidName_ThrowsDomainValidationException`).
Assertions **exclusivement** FluentAssertions (`.Should().Be(...)`, `.Should().Throw<T>()`), jamais
`Assert.*` de xUnit. Pas de bibliothèque de builders : de petites méthodes factory privées
(`ValidRule(...)`) réutilisées dans la classe de test.

### Tests HTTP réels (`WebApplicationFactory<Program>`)

*WebAPI.Tests* (3) : `Authentication/ApiKeyAuthenticationTests.cs`, `Health/HealthPingTests.cs`,
`Oxo/OxoProcessEndpointTests.cs`.

*BlazorAdmin.Tests* (7) : `Account/AccessDeniedHttpTests.cs`, `Account/ForcePasswordChangeHttpTests.cs`,
`Account/RegisterRemovalHttpTests.cs`, `Account/UserNameLoginIdentityHttpTests.cs`,
`Authorization/BusinessPageAuthorizationHttpTests.cs`, `Components/AppRenderModeHttpTests.cs`,
`Pages/HomeHttpTests.cs` (**nouveau depuis le 28/07**, lot 054).

Convention DI-override commune aux fixtures BlazorAdmin (établie au lot 049) : `UseEnvironment("Development")`,
`Serilog:EnableMsSqlServerSink=false`, `Database:AutoMigrate=false`, `IdentitySeeding:Enabled=false`,
`ProfileSeeding:Enabled=false`, `OxoApiTestClient:BaseUrl`/`ApiKey` factices, `ExcelEtlDbContext` +
les deux registrations `ApplicationIdentityDbContext` (et `SystemLogsDbContext` pour les tests
touchant `/logs`) basculées sur EF Core InMemory.

**Leçon documentée dans le dépôt** (`docs/conventions/recommandations-tickets-tdd.md` §6) : un
test bUnit seul ne prouve jamais qu'une page est réellement atteignable — il court-circuite le
routage, le mode de rendu (SSR statique vs interactif) et tout le pipeline HTTP. Deux régressions
réelles (lots 049 et 051) sont passées inaperçues en bUnit tout en rendant une page « Introuvable »
en navigateur réel. Une requête `WebApplicationFactory` seule ne suffit pas non plus pour un défaut
qui n'apparaît qu'une fois le circuit SignalR ouvert — la garantie retenue est une assertion sur le
mode de rendu réellement servi (marqueur `"type":"server"` dans le corps de la réponse), utilisée
dans `AppRenderModeHttpTests.cs`.

### Comptage réel de la suite (ce commit, `dotnet test --verbosity quiet` par projet)

| Projet de test | Résultat |
|---|---|
| `ExcelETL.Domain.Tests` | **323 / 323** ✅ (exécuté ce commit) |
| `ExcelETL.Application.Tests` | **215 / 215** ✅ (exécuté ce commit) |
| `ExcelETL.Infrastructure.Tests` | **221 / 221** ✅ (exécuté ce commit) |
| `ExcelETL.WebAPI.Tests` | **25 / 25** ✅ (exécuté ce commit) |
| `ExcelETL.Hosting.Tests` | **6 / 6** ✅ (exécuté ce commit) |
| `ExcelETL.BlazorAdmin.Tests` | **non exécuté ce commit** (voir ci-dessous) — dernier chiffre connu **952 / 952** (`CLAUDE.md`, lot 063) |
| `legacy/NewApiPingService.Tests` (.NET Framework 4.8) | **9 / 9** ✅ (exécuté ce commit) |
| `legacy/ExcelProcessingClientService.Tests` (.NET Framework 4.8) | **15 / 15** ✅ (exécuté ce commit) |
| **Total exécuté ce commit** | **814 / 814**, 0 échec |
| **Total incluant le dernier chiffre connu pour BlazorAdmin.Tests** | **1766** |

**`ExcelETL.BlazorAdmin.Tests` n'a pas pu être compilé/exécuté pendant cette session** : `dotnet build`
échoue avec `MSB3027`/`MSB3021` (« Le fichier est verrouillé par : Microsoft Visual Studio (40724),
ExcelETL.BlazorAdmin (42244) ») — une instance Visual Studio ouverte sur ce poste verrouillait le
dossier de sortie `bin\Debug\net10.0\` au moment de l'exécution. Ce n'est pas un défaut de code,
mais ce document ne peut **pas** certifier à 100 % que la suite BlazorAdmin est verte à ce commit
précis — seul le dernier chiffre connu du journal `CLAUDE.md` (952/952, daté du lot 063, même
commit `HEAD`) est disponible. À reconfirmer par une exécution sans verrou de fichier.

---

## 6. ADR (Architecture Decision Records)

**Aucun répertoire ni fichier `ADR`/`adr` n'existe dans le dépôt** — recherche exhaustive par nom
de fichier infructueuse (inchangé depuis le 28/07). Pas de convention ADR formalisée (pas de
numérotation, pas de statut proposé/accepté/rejeté).

Les décisions d'architecture et de convention transverse sont consignées dans des **documents
vivants** sous `docs/conventions/` (10 fichiers, mis à jour en place selon
`convention-nommage-documents.md`) :

| Fichier | Objet |
|---|---|
| `convention-autorisation-pages-blazoradmin.md` | Modèle d'autorisation à deux niveaux (section 4) |
| `convention-nommage-documents.md` | Méta-convention : documents « vivants » vs instantanés datés |
| `convention-secrets-production.md` | Secrets de prod par variable d'environnement uniquement — **contredit en pratique, voir section 7** |
| `convention-ui-blazor-alignement-boutons.md` | Alignement à droite des boutons d'action de contenu |
| `convention-ui-blazor-icones-boutons.md` | Icônes SVG inline via `AdminIconMarkup`, jamais une police d'icônes |
| `convention-ui-blazor-tableaux-generes-lisibilite.md` | Lisibilité des tableaux à colonnes pilotées par profil/fichier |
| `guide-deploiement-am-oxo-etl-windows-server.md` | Runbook de déploiement IIS + SQL Server (section 7) |
| `instructions-systeme-claude-code.md` | Brief système utilisé pour amorcer les sessions Claude Code |
| `procedure-mise-a-jour-packages.md` | Procédure/prompt réutilisable pour la revue périodique des dépendances NuGet |
| `recommandations-tickets-tdd.md` | Méthodologie TDD, dont la leçon bUnit/HTTP (§6) |

Le fichier `CLAUDE.md` à la racine contient un historique chronologique très détaillé, lot par lot,
de chaque décision technique — plus proche d'un journal de bord exhaustif que d'un ADR par sujet,
mais c'est la source la plus complète de justification « pourquoi » pour toute décision passée.
Aucun de ces documents n'est un ADR au sens strict.

---

## 7. Écarts et points de vigilance avant mise en service

Reprise de chaque point du document du 28/07, plus un écart nouveau significatif.

### Statut des points déjà identifiés au 28/07

| Point (28/07) | Statut au 31/08 |
|---|---|
| Migrations non vérifiées contre un vrai SQL Server | **Toujours ouvert, aggravé** — 1 migration de plus depuis (lot 063), le rapport de vérification cité par le guide est introuvable dans le dépôt (voir section 3 et 6) |
| Secrets de production jamais positionnés (`ApiKeyAuthentication:ApiKey`, `OxoApiTestClient:*`, `AdminSeedPasswords:*`) | **Partiellement obsolète — remplacé par un écart plus grave** (voir ci-dessous) : une valeur ressemblant à une vraie clé API de production est désormais **committée en clair** dans le dépôt, exactement l'inverse de la convention documentée |
| Hébergement IIS jamais exercé en dehors du guide | **Toujours ouvert** — le guide de déploiement (`docs/conventions/guide-deploiement-am-oxo-etl-windows-server.md`) n'a reçu aucune mise à jour de statut depuis le 28/07 ; ses cases à cocher restent toutes non cochées pour le serveur cible |
| Code mort (`IFileStorageService`, `IWorkbookReader.SheetExists`) | **Toujours résolu** — confirmé absent, aucune réintroduction constatée |
| `AllowedHosts` = `"*"` en production, sans restriction applicative | **Toujours ouvert, inchangé** — vérifié dans `appsettings.json` **et** dans les nouveaux `appsettings.Production.json` (section ci-dessous) des deux hôtes |
| `Register.razor` confirmé absent | **Toujours vrai**, aucune réintroduction |

### ⚠️ Écart nouveau et significatif : `appsettings.Production.json` committé avec une clé API en clair

Deux fichiers **n'existaient pas** au 28/07 et existent désormais, **suivis par git** :
`src/ExcelETL.WebAPI/appsettings.Production.json` et `src/ExcelETL.BlazorAdmin/appsettings.Production.json`,
ajoutés par le commit `7716dc4` (« appsettings.production : new path »), **le même jour que le
document précédent mais 3 heures après lui** (28/07 17:46, contre 14:47 pour le commit de
référence du document précédent).

Faits vérifiés directement (sans reproduire la valeur du secret dans ce document) :
- `.gitignore` exclut explicitement `appsettings.*.json`, avec seulement deux exceptions
  nommées : `!appsettings.json` et `!appsettings.Development.json`. `appsettings.Production.json`
  n'est **pas** dans la liste d'exception — sa présence dans `git ls-files` signifie qu'il a été
  ajouté de force (`git add -f` ou équivalent), en contournement délibéré ou accidentel du
  `.gitignore`.
- Les deux fichiers contiennent une clé `ApiKey` (`ApiKeyAuthentication.ApiKey` côté WebAPI,
  `OxoApiTestClient.ApiKey` côté BlazorAdmin) portant **exactement la même valeur** dans les deux
  fichiers — cohérent avec l'exigence documentée par le guide de déploiement (« même clé des deux
  côtés »), ce qui suggère que cette valeur **n'est pas** un placeholder mais une vraie clé
  destinée à (ou déjà utilisée en) production.
- `GeneratedFilesArchive:RootPath` (`c:\inetpub\Alpha\AM-OXO-ETL\GeneratedFiles\`) et
  `OxoApiTestClient:BaseUrl` (`https://oxo-etl-api.alphamaintenance.fr`) dans ces fichiers
  correspondent aux chemins/hôtes réels du serveur cible décrits dans le guide de déploiement —
  renforce l'hypothèse d'une configuration quasi-réelle, pas d'un gabarit générique.
- La chaîne de connexion dans ces deux fichiers reste un placeholder (`Server=localhost;...`) —
  seule la clé API semble être une vraie valeur.

Ceci **contredit directement** `docs/conventions/convention-secrets-production.md`, dont la
« Décision actée » énonce explicitement : *« Aucun `appsettings.Production.json` n'existe ni ne
doit exister dans le dépôt … Ce n'est pas un oubli à corriger. »* Le document de convention n'a
reçu aucune mise à jour reflétant ce changement de pratique.

**Recommandation factuelle, sans action entreprise par ce document** : si la valeur committée est
réellement une clé de production (ou destinée à l'être), elle doit être considérée comme
compromise dès lors qu'elle a transité par l'historique git (même si le fichier est supprimé
ensuite, la valeur reste récupérable dans l'historique) — une rotation de clé serait la mesure de
remédiation standard, à trancher avec l'équipe avant toute mise en service. Ce point n'était **pas**
mentionné dans le document du 28/07 (les fichiers n'existaient pas encore à ce moment-là).

### Autre écart documentaire mineur constaté

- `CLAUDE.md` affirme que les deux `.csproj` hôtes (`ExcelETL.BlazorAdmin`/`ExcelETL.WebAPI`) ont
  « déjà un `UserSecretsId` ». Vérifié : **seul `ExcelETL.BlazorAdmin.csproj`** porte réellement un
  `<UserSecretsId>` (`8a20d217-0693-442e-a749-a8fdf4a56bd1`). `ExcelETL.WebAPI.csproj` n'en a
  **aucun** — son secret de développement (`ApiKeyAuthentication:ApiKey`) est actuellement un
  placeholder en clair dans `appsettings.Development.json` (`"dev-local-api-key-CHANGE-ME"`),
  pas géré via User Secrets malgré ce que documente `CLAUDE.md`.
- `docs/conventions/convention-secrets-production.md` présente encore le choix
  IIS-vs-service-Windows-autonome comme « non tranché », alors que
  `guide-deploiement-am-oxo-etl-windows-server.md` §0 l'a explicitement tranché en faveur d'IIS
  seul. Incohérence mineure entre deux documents vivants, sans impact fonctionnel.

### TODO/FIXME/HACK

Recherche exhaustive (`grep -rn "TODO\|FIXME\|HACK" src/`) : **zéro occurrence**. Aucun marqueur de
dette technique explicite dans le code source à ce commit.

---

## 8. Avancement réel des lots depuis le dernier état des lieux (28/07, lot 052)

Vérification croisée **git log** (message de commit réel, pas seulement `CLAUDE.md`) et
**présence effective du code** produit par chaque lot, pour chaque ticket `docs/tickets/tickets-tdd-lot-0[5-6]*`
postérieur au lot 052.

| Lot | Ticket (`docs/tickets/`) | Commit(s) réel(s) | Statut constaté |
|---|---|---|---|
| 053 | `lot-053-largeur-densite-formulaires-editeurs-profil.md` | `dc4e710` | **Implémenté et testé** |
| 054 | `lot-054-page-accueil-indicateurs.md` | `494880e` | **Implémenté et testé** — `Home.razor` présent, `[Authorize]`, `IHomeIndicatorsService` enregistré, `HomeHttpTests.cs` existe |
| 055 | `lot-055-avertissements-extraction-semantique-et-bruit.md` | `1915db6`, `10ee994`, `e504441`, `8435224` + 3 commits de doc post-lot | **Implémenté et testé** (déjà confirmé en mémoire de session avant ce document) |
| 056 | `lot-056-modele-enregistrement-editeurs-profil.md` | `ec471eb`, `f4e7d37`, `dbd6020`, `486b727`, `c5fbfbf`, `3c5aa04` | **Implémenté et testé** |
| 057 | `lot-057-exclusion-mutuelle-formulaires-feuille.md` | `d87bc16`, `0e5b42d`, `fa279a0`, `354dd98` | **Implémenté et testé** |
| 058 | `lot-058-finitions-boutons-ajout.md` | `79e605a`, `81b3f6a`, `4f55413`, `b4bab27`, `e40a38d` | **Implémenté et testé** |
| 059 | `lot-059-validation-noms-listes-et-finitions-editeurs.md` | `157dcfd` | **Implémenté et testé** |
| 060 | `lot-060-palette-m3-complete-suppression-dette-couleurs.md` | `49ca4e4` | **Implémenté et testé** |
| 061 | `lot-061-logo-client-bas-sidebar.md` | `1c2b1be` | **Implémenté et testé** |
| 062 | `lot-062-version-application-sidebar.md` | `46b4e62` | **Implémenté et testé** (+ un suivi non numéroté le même jour, logo/alignement footer sidebar, mentionné dans `CLAUDE.md` mais sans ticket dédié) |
| 063 | `lot-063-condition-zero-energie-isolement.md` | `29aa161` (HEAD) + `ea92081` (correctif CSS post-lot, non documenté sous un numéro de lot) | **Implémenté et testé** — vérifié directement : `HasZeroEnergie`/`ZeroEnergieExpectedValue` présents dans `IsolementPivot.cs`, `SheetExtractionRule.cs`, `IsolementExtractionService.cs`, migration `20260831095729_...` présente |

**Conclusion de la vérification** : les 11 lots documentés entre le 052 et le 063 ont chacun un ou
plusieurs commits réels correspondants dans l'historique git, et pour un échantillon direct
(054, 063) le code produit est physiquement présent et cohérent avec la description du ticket —
**aucun ticket de ce lot de la plage 053-063 n'est resté à l'état de simple rédaction sans
implémentation**. Un commit correctif non rattaché à un numéro de lot existe après le lot 063
(`ea92081`, chevauchement bouton/champ + z-index barre d'enregistrement) — mineur, cosmétique,
sans impact sur le statut « implémenté » du lot 063 lui-même.

Aucun fichier `tickets-tdd-lot-*` numéroté au-delà de 063 n'existe dans `docs/tickets/` à ce
commit — 063 est le dernier lot numéroté du dépôt.

---

## Non couvert / incertain

- **Suite `ExcelETL.BlazorAdmin.Tests` non ré-exécutée ce commit** (section 5) — verrou de
  fichier Visual Studio actif pendant cette session. Le chiffre 952/952 vient du journal
  `CLAUDE.md` daté du même commit `HEAD`, pas d'une exécution indépendante de cette session.
- **Nature exacte de la valeur commitée dans les deux `appsettings.Production.json`** (section 7) :
  ce document ne peut pas confirmer avec certitude absolue qu'il s'agit d'une clé réellement
  utilisée en production (vs. une valeur générée mais jamais déployée) — seule la cohérence
  interne (même valeur des deux côtés, chemins/hôtes réels autour) l'indique fortement.
- **Vérification réelle en base de production** : comme au 28/07, ce document ne peut pas
  confirmer depuis le seul code si une quelconque migration a été appliquée à une instance SQL
  Server réelle — le rapport qui l'attesterait pour une partie des migrations est introuvable
  dans le dépôt.
- **Déploiement IIS réel** : aucune preuve dans le dépôt qu'un déploiement réel (avec les deux
  sites distincts, le certificat auto-signé, les hostnames DNS) ait eu lieu sur le serveur cible.
  Les artefacts `obj/Release/net10.0/PubTmp/Out/web.config` trouvés confirment seulement qu'un
  `dotnet publish` **local** a été exécuté au moins une fois sur ce poste de développement, pas un
  déploiement serveur.
- **Contenu réel des variables d'environnement de production** (`ApiKeyAuthentication__ApiKey`
  côté serveur IIS, `AdminSeedPasswords__*`) : par construction, ces valeurs ne sont jamais
  présentes dans le dépôt sous cette forme — non vérifiable depuis le code, distinct de l'écart
  sur `appsettings.Production.json` relevé en section 7.
- **Contraste/accessibilité visuelle en navigateur réel** : comme au 28/07, ce document est
  fondé sur la lecture du code et l'exécution de tests automatisés — il ne referme aucun écart
  de vérification visuelle en navigateur réel signalé par les lots antérieurs.
- **Raison exacte de l'ajout forcé de `appsettings.Production.json` malgré le `.gitignore`** :
  ce document constate le fait (commit, contenu, contradiction avec la convention) mais ne peut
  pas déterminer l'intention (erreur, contournement volontaire ponctuel, ou changement de
  décision jamais documenté) sans interroger l'auteur du commit.
