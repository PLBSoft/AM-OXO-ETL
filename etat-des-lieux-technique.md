# État des lieux technique — AM-OXO-ETL

*Généré le 2026-07-14 par exploration directe du code (aucune donnée n'est tirée de la mémoire long-terme sans vérification).*

---

## 1. Structure du solution/projets et câblage des dépendances

Pas de fichier `.sln` au niveau racine — les projets se compilent/testent via les `.csproj` individuels ou `dotnet build`/`dotnet test` sur le dossier. Clean Architecture (Onion) stricte en 5 projets `src/` + leurs 5 miroirs `tests/`, plus un dossier `legacy/` isolé.

```
src/
├── ExcelETL.Domain            (net10.0, ZÉRO PackageReference, ZÉRO ProjectReference)
├── ExcelETL.Application        → réf. Domain
├── ExcelETL.Infrastructure     → réf. Domain, Application
├── ExcelETL.WebAPI (Sdk.Web)   → réf. Application, Infrastructure
└── ExcelETL.BlazorAdmin (Sdk.Web) → réf. Application, Infrastructure   [InternalsVisibleTo: ExcelETL.BlazorAdmin.Tests]

tests/  (1 projet de test par projet src/, structure interne miroir 1:1)
legacy/
├── NewApiPingService (+.Tests)              — .NET Framework 4.8
└── ExcelProcessingClientService (+.Tests)   — .NET Framework 4.8
```

**Règle de dépendances validée dans le code** : `Domain` ne référence rien (ni package NuGet ni projet). `Application` ne référence que `Domain` et deux packages *Abstractions* (`Microsoft.Extensions.Localization.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`) — donc framework-free au sens fort. `Infrastructure` est la seule couche qui touche EF Core, ClosedXML et ASP.NET Core Identity. `WebAPI` et `BlazorAdmin` référencent tous deux `Application` + `Infrastructure`, jamais l'un l'autre — **sauf une exception documentée et volontaire** : `BlazorAdmin` contient un `HttpClient` typé (`ExcelProcessingClient`) qui appelle `POST /api/excel/process` sur le WebAPI, réservé à la page admin `/upload-test` dont le rôle est de tester le vrai contrat M2M. C'est la seule communication HTTP inter-apps ; tout le reste passe par les couches Application/Infrastructure partagées en mémoire de processus.

`legacy/` (ASP.NET MVC 5, .NET Framework 4.8) est le style de référence pour tout futur code HTTP M2M côté client (construction multipart, traduction des timeouts, headers) mais ne fait pas partie de la solution .NET 10.

---

## 2. Conventions déjà adoptées

**Repository pattern (pas de Unit of Work).** Chaque repository (`ExtractionConfigRepository`, `UserRepository`, etc.) reçoit un `IDbContextFactory<TContext>` injecté et ouvre/ferme un `DbContext` court-vécu **par appel de méthode** — pas de `DbContext` scoped injecté directement, pas de classe `UnitOfWork`. Raison documentée en commentaire : Blazor Server (rendermode Interactive Server) a des circuits longue durée qui peuvent invoquer des handlers en concurrence ; un `DbContext` scoped partagé y serait unsafe. Le WebAPI aurait pu se contenter d'un scoped classique, mais le pattern est volontairement identique dans les deux hosts pour ne pas diverger.

Piège EF Core documenté : les entités du Domain assignent leur `Guid` côté client (constructeur), donc le change tracker ne peut pas distinguer « nouvelle entité à clé pré-assignée » de « entité existante modifiée » — les repositories doivent forcer `context.Entry(x).State = EntityState.Added` explicitement après un ajout par mutation de collection.

**Exceptions / pas de Result pattern générique.** Le Domain lève des exceptions typées (`DomainValidationException`, `DomainArgumentOutOfRangeException`, `DomainRuleViolationException`, dans `ExcelETL.Domain/Exceptions`), chacune portant un `DomainErrorCode` (enum) + les `Args` bruts interpolés dans un message anglais. L'Application fait de même avec `ApplicationErrorCode` (`ExtractionConfigNotFoundException`, etc.). Le WebAPI capte tout ça dans un seul `GlobalExceptionHandler : IExceptionHandler`, qui résout le code d'erreur via un `BusinessExceptionLocalizer` (ressources `.resx`) et mappe chaque type d'exception à un status HTTP (404/400/409/500) — voir `src/ExcelETL.WebAPI/ExceptionHandling/GlobalExceptionHandler.cs:43`.
Une exception au principe « pas de Result » existe côté Identity : `IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors)` — un record simple utilisé uniquement pour les opérations `UserManager` (mot de passe/profil), pour ne pas fuir de type ASP.NET Identity hors d'Infrastructure.

**Entités Domain riches, pas des DTO anémiques.** Constructeurs qui valident (ex. `CellMapping`, `SheetConfig`, `ExtractionConfig` limitent à 4-5 feuilles via `MaxSheets`), propriétés en lecture seule + méthodes métier (`AddSheet`, `AddCellMapping`, `MarkCompleted`, `MarkFailed`) qui portent les invariants (ex. on ne peut pas `MarkCompleted` une entrée déjà `Completed`/`Failed`).

**Organisation des dossiers : par feature, pas par type technique**, à l'intérieur de chaque couche (`Extraction/`, `Identity/`, `Diagnostics/` regroupent interface + implémentation + exceptions liées à ce sous-domaine, plutôt que des dossiers `Interfaces/`, `Services/`, `Exceptions/` génériques au niveau racine — seul `Exceptions/` et `Resources/` restent transverses dans Application).

**i18n** (en cours, voir mémoire projet [i18n Refactor Milestone Status]) : `DomainErrorCode`/`ApplicationErrorCode` servent de clé de ressource, tables `.resx`/`.fr.resx` centralisées dans `Application/Resources/`, partagées par WebAPI et BlazorAdmin pour ne jamais dupliquer le mapping.

---

## 3. Modèle EF Core existant

**Deux `DbContext` distincts, deux jeux de migrations séparés, deux bases logiques (même connexion SQL Server, tables séparées via une table d'historique de migration différente).**

### `ExcelEtlDbContext` (métier)
`src/ExcelETL.Infrastructure/Persistence/ExcelEtlDbContext.cs` — 2 `DbSet` : `ExtractionConfigs`, `ExtractionHistories`. Configuration Fluent API via `ApplyConfigurationsFromAssembly` (fichiers `*Configuration.cs` dans `Persistence/Configurations/`, un par entité : `CellMapping`, `ExtractionConfig`, `SheetConfig`, `ExtractionHistory`).

Entités actuelles (`src/ExcelETL.Domain/Entities/`) :
- `ExtractionConfig` — nom + collection de `SheetConfig` (max 5, index unique)
- `SheetConfig` — nom + index + collection de `CellMapping`
- `CellMapping` — cellule source (regex validée, ex. `B4` ou `B4:D4`), nom de propriété cible, `CellDataType`
- `ExtractionHistory` — timestamp, nom de fichier source, chemin de fichier stocké, `ExtractionStatus` (Pending/Completed/Failed), `CompletedAtUtc`, `Duration` calculée

Migrations (`Persistence/Migrations/`) : `20260710140017_InitialCreate`, `20260710174749_AddCompletedAtUtcToExtractionHistories`. Table d'historique dédiée : `__EFMigrationsHistory_ExcelEtl` (configurée explicitement dans les deux `Program.cs`, WebAPI et BlazorAdmin, pour éviter toute collision avec les migrations Identity sur la même base).

### `ApplicationIdentityDbContext` (Identity)
`src/ExcelETL.Infrastructure/Identity/ApplicationIdentityDbContext.cs` — hérite de `IdentityDbContext<ApplicationUser>`, aucun `DbSet` additionnel déclaré (tout vient d'ASP.NET Core Identity). `ApplicationUser : IdentityUser` ajoute seulement `FirstName`/`LastName`.
Migrations (`Identity/Migrations/`) : `20260710140119_InitialIdentityCreate`, `20260711090054_AddFirstNameLastNameToApplicationUser`. Table d'historique dédiée : `__EFMigrationsHistory_Identity`.

### Une troisième table hors migrations EF
`SystemLogs` (via `SystemLogsDbContext`, lecture seule côté BlazorAdmin pour le `/dashboard`) est **possédée et créée par Serilog** (`Serilog.Sinks.MSSqlServer`, `AutoCreateSqlTable = true`) — délibérément en dehors des migrations Code-First EF, car c'est une table de logs technique partagée par les deux hosts (WebAPI et BlazorAdmin écrivent tous deux dedans, avec une colonne `Application` pour distinguer la source).

### Comment les migrations sont gérées
`Microsoft.EntityFrameworkCore.Design` est référencé uniquement dans `Infrastructure`. Chaque `DbContext` métier a une `IDesignTimeDbContextFactory` dédiée (`ExcelEtlDbContextFactory.cs`, `ApplicationIdentityDbContextFactory.cs`) pour permettre `dotnet ef migrations add` sans dépendre du host au démarrage. Les deux hosts (WebAPI, BlazorAdmin) enregistrent leurs `DbContextFactory` avec `sql.MigrationsHistoryTable(...)` explicite — aucune migration n'est appliquée automatiquement au démarrage (`Migrate()` n'apparaît pas dans les `Program.cs`, contrairement au seed Identity qui lui est automatique).

**Accès EF Core exclusivement via `IDbContextFactory<T>`, jamais de `DbContext` scoped injecté directement dans une méthode métier** — sauf le cas particulier documenté : `ApplicationIdentityDbContext` scoped classique est enregistré *en plus* de sa factory, uniquement parce que `AddEntityFrameworkStores<ApplicationIdentityDbContext>()` (le store interne d'ASP.NET Identity, hors du contrôle du projet) l'exige.

---

## 4. Authentification / autorisation — câblage actuel

**Deux mécanismes d'auth totalement séparés, un par host, cohérents avec le rôle de chaque app :**

### WebAPI (M2M, `src/ExcelETL.WebAPI/Program.cs:78-88`)
- Scheme custom `ApiKeyAuthenticationDefaults.SchemeName`, handler `ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>`.
- Clé attendue dans le header `X-Api-Key`, comparaison en temps constant (`CryptographicOperations.FixedTimeEquals`) pour éviter le timing attack.
- Clé lue depuis `ApiKeyAuthentication:ApiKey` (config), avec `throw` fail-fast au démarrage si absente.
- `AuthorizationOptions.FallbackPolicy` = authentification requise globalement (pas d'attribut `[Authorize]` par contrôleur à gérer — tout est protégé par défaut).
- Pas de rôles/claims métier : le principal authentifié porte juste `ClaimTypes.Name = "LegacyApplication"` — un seul « client » possible, pas de notion multi-tenant côté WebAPI.

### BlazorAdmin (interactif, `src/ExcelETL.BlazorAdmin/Program.cs:113-132`)
- ASP.NET Core Identity complet (`AddIdentity<ApplicationUser, IdentityRole>`), store EF Core (`ApplicationIdentityDbContext`), `LocalizedIdentityErrorDescriber` custom pour les messages d'erreur i18n.
- `IdentitySeeder` (scoped, exécuté à chaque démarrage, idempotent) crée un jeu fixe de comptes admin — mots de passe **jamais** dans un fichier de config committé (voir `AdminSeedUser`).
- Autorisation : `FallbackPolicy` = authentifié requis par défaut (comme WebAPI) ; les pages admin ajoutent en plus `@attribute [Authorize(Roles = IdentitySeeder.AdminRoleName)]` au niveau composant pour restreindre par rôle (ex. `Users.razor`).
- `AddCascadingAuthenticationState()` + `AuthenticationStateProvider` custom (`IdentityRevalidatingAuthenticationStateProvider`) pour les composants Interactive Server.
- Contrainte connue et documentée : un circuit Blazor Server ne peut pas réécrire le cookie d'auth en cours de circuit (ex. après changement de mot de passe) — la solution en place est un vrai POST HTML (`<form action="Account/Logout">`) vers l'endpoint Identity classique, pas une tentative de re-sign-in in-process.

### Pour le futur écran de gestion des profils d'import
Il devra suivre le pattern déjà établi et normalisé en Milestone 11 : accès aux données **uniquement via un repository de l'Application layer** (`IExtractionConfigRepository` existe déjà et couvre CRUD config/sheet/cellmapping), jamais `DbContext` injecté dans le composant Razor, et protection par `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` comme les autres pages admin. Aucun câblage supplémentaire d'auth n'est nécessaire — le `FallbackPolicy` + l'attribut de rôle suffisent, à l'identique de `Users.razor`/`Mappings` existants.

---

## 5. Conventions de tests

**Structure** : un projet de test par projet source, miroir dossier-par-dossier (`tests/ExcelETL.Infrastructure.Tests/Identity/` teste `src/ExcelETL.Infrastructure/Identity/`, etc.) — pas d'organisation par feature transverse aux couches, chaque couche a ses propres tests dans son propre projet.

**Stack commune** : xUnit 2.9.3 + **FluentAssertions 7.0.0 épinglé** (v8+ est commercial, interdit par la contrainte OSS du projet) + Moq. Toutes les assertions passent par FluentAssertions (`.Should().Be(...)`), jamais `Assert.*` de xUnit.

**Pattern par type de dépendance testée** :
- **Repository adossé à un `DbContext` EF Core** (`ExtractionConfigRepository`, `ExtractionHistoryRepository`, lecture `UserRepository`) → testé contre le **vrai provider EF Core InMemory**, jamais mocké. Chaque classe de test a sa propre factory `internal sealed class Test{Nom}DbContextFactory(string databaseName) : IDbContextFactory<TContext>` colocalisée dans le dossier de test, nom de base suffixé par un GUID pour l'isolation. Il n'existe **pas** de helper générique partagé — chaque contexte a sa propre petite factory (ex. `TestDbContextFactory.cs` pour `ExcelEtlDbContext`, `TestApplicationIdentityDbContextFactory.cs` pour `ApplicationIdentityDbContext`, `TestSystemLogsDbContextFactory.cs` pour `SystemLogsDbContext`, dupliquée à l'identique dans BlazorAdmin.Tests et Infrastructure.Tests).
- **Repository adossé à `UserManager<T>`/`RoleManager<T>`** (écritures `UserRepository` : `UpdateProfileAsync`, `ChangePasswordAsync`) → testé avec **Moq**, via un helper `IdentityManagerMocks.CreateUserManagerMock()` (`tests/ExcelETL.Infrastructure.Tests/Identity/IdentityManagerMocks.cs`), car ces managers ne s'exercent pas de façon significative contre InMemory seul. Une même classe de test peut mélanger les deux approches (cas de `UserRepositoryTests`).
- **Endpoints WebAPI** → `WebApplicationFactory<Program>` (intégration réelle, pas de mock HTTP), ex. `ExcelProcessEndpointTests : IClassFixture<WebApplicationFactory<Program>>`. Le seeding Identity et le sink SQL Serilog sont désactivables via des flags de config (`IdentitySeeding:Enabled`, `Serilog:EnableMsSqlServerSink`) pour que ces tests n'aient pas besoin d'un vrai SQL Server.
- **Composants Blazor** → bUnit v2.7.2, `class FooTests : BunitContext`, `Render<Foo>()`. Auth simulée via `this.AddAuthorization().SetAuthorized(...)` + `.SetClaims(...)`.
- **Code HTTP client** (`ExcelProcessingClient`) → `FakeHttpMessageHandler` fait main (colocalisé dans le projet de test, même style que côté `legacy/`), pas de dépendance à une lib de mock HTTP tierce.

**Piège documenté (bUnit + InputFile)** : déclencher `InputFile.OnChange` en bUnit bloque de façon synchrone jusqu'à la fin de toute la chaîne async — une réponse HTTP fake gérée par `TaskCompletionSource` qu'on ne complète qu'*après* `UploadFiles` fait deadlocker le test ; il faut des réponses fake immédiatement résolues.

**Pas de builders/object-mothers génériques repérés** — les tests construisent les entités directement via leurs constructeurs (cohérent avec des entités riches qui valident déjà tout en entrée).

---

## 6. ADR (Architecture Decision Records)

**Aucun ADR formel n'existe dans le repo** — pas de dossier `docs/adr/`, pas de fichier contenant "ADR" dans le nom, nulle part dans `src/`, `tests/` ou ailleurs.

Les décisions d'architecture significatives sont en revanche **documentées inline, en commentaires C# au point de décision**, avec le "pourquoi" explicite — c'est le lieu de facto où l'historique de conception vit aujourd'hui. Exemples représentatifs déjà relevés ci-dessus : le choix `IDbContextFactory` par-appel plutôt que `DbContext` scoped (`ExtractionConfigRepository.cs:7-10`), le `EntityState.Added` forcé après mutation de collection (`ExtractionConfigRepository.cs:50-53`), l'exception `BlazorAdmin → WebAPI` en HTTP pour `/upload-test` (`Program.cs` BlazorAdmin), Serilog propriétaire de la table `SystemLogs` hors migrations EF (les deux `Program.cs`), l'ordre `UseRequestLocalization` avant `UseExceptionHandler` (WebAPI `Program.cs:131-137`).

**Recommandation** : si l'équipe veut un historique de décisions consultable sans lire le code (utile notamment pour la personne côté Claude AI qui prépare le futur écran de gestion des profils d'import), il vaudrait la peine d'extraire rétroactivement 4-5 ADR courts à partir de ces commentaires (repository pattern sans UoW, séparation des deux DbContext/tables de migration, exception HTTP BlazorAdmin→WebAPI, stratégie i18n Domain-free). Le skill `engineering:architecture` de cette session peut générer ce format si utile — à confirmer avant de le lancer, ce n'est pas fait automatiquement ici.
