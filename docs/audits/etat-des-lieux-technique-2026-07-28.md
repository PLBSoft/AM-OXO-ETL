# État des lieux technique — AM-OXO-ETL

**Branche** : `main`
**Commit `HEAD`** : `7bba21e73b5813425dd73ad91df15824ad799738` (2026-07-28 14:47:10 +0200)
**Message du commit** : `feat(lot-052): acces des comptes non-Admin et page Acces refuse`
**Date de génération de ce document** : 2026-07-28
**Arbre de travail** : propre (`git status --porcelain` vide) au moment de la génération. Aucun
fichier du repository n'a été modifié pour produire ce document ; seule la suite de tests a été
exécutée (opération non destructive).

Ce document est un **instantané daté** : il décrit l'état du code au commit ci-dessus et ne sera
pas mis à jour après coup (voir `docs/conventions/convention-nommage-documents.md`). Il est écrit
pour être lu par une session Claude AI **sans accès au code** — tout ce qui n'y figure pas lui est
invisible.

---

## 1. Structure de la solution / des projets

### Arborescence (`ExcelETL.slnx`)

```
/legacy/
  ExcelProcessingClientService (.NET Framework 4.8) + .Tests
  NewApiPingService (.NET Framework 4.8) + .Tests
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
```

Le fichier solution référence bien les 6 projets `src/`, leurs 6 projets de test miroir, et les
2 projets `legacy/` (+ leurs 2 projets de test). Aucun projet du disque n'est absent du fichier
solution — vérifié par lecture directe de `ExcelETL.slnx`.

### Dépendances entre projets (`<ProjectReference>` de chaque `.csproj`)

| Projet | Références vers |
|---|---|
| `ExcelETL.Domain` | **aucune** |
| `ExcelETL.Application` | `ExcelETL.Domain` |
| `ExcelETL.Infrastructure` | `ExcelETL.Domain`, `ExcelETL.Application` |
| `ExcelETL.Hosting` | **aucune** (référencé uniquement par les deux hôtes) |
| `ExcelETL.WebAPI` | `ExcelETL.Application`, `ExcelETL.Hosting`, `ExcelETL.Infrastructure` |
| `ExcelETL.BlazorAdmin` | `ExcelETL.Application`, `ExcelETL.Hosting`, `ExcelETL.Infrastructure` |

`ExcelETL.Domain` a bien **zéro** `PackageReference` et **zéro** `ProjectReference` — l'invariant
« Domain sans aucune dépendance » est tenu. Aucune référence circulaire, aucun projet Infrastructure
référencé directement par un composant Blazor ou un contrôleur (vérifié aussi via
`docs/conventions/instructions-systeme-claude-code.md`, cohérent avec le code).

**Exception documentée et vérifiée dans le code** : `ExcelETL.BlazorAdmin` ne référence jamais
`ExcelETL.WebAPI` par `ProjectReference`, mais `ApiTest.razor` (route `/api-test`) appelle le Web
API **en HTTP réel** via un `HttpClient` typé (`IOxoApiTestClient`/`OxoApiTestClient`,
`src/ExcelETL.BlazorAdmin/Services/`), configuré par `OxoApiTestClientOptions` (`BaseUrl`/`ApiKey`
lus côté serveur, jamais saisis par l'utilisateur). C'est la seule voie HTTP BlazorAdmin → WebAPI
de toute la solution.

### Points d'entrée et DI

**`ExcelETL.WebAPI/Program.cs`** :
- Kestrel configuré (taille max de requête, timeouts) via `UploadLimits`.
- Connexion SQL via `AddDbContextFactory<ExcelEtlDbContext>` (pattern « factory », pas
  `AddDbContext` direct — permet un `DbContext` court par opération dans les repositories).
- Authentification par clé API (`ApiKeyAuthenticationHandler`, schéma `X-Api-Key`) — `FallbackPolicy`
  globale = authentification requise, aucun rôle.
- Lève `InvalidOperationException` au démarrage si `ApiKeyAuthentication:ApiKey` est absent
  (fail-fast).
- Enregistre le pipeline OXO (extraction + génération, tous singletons sauf les stores de profils),
  `IGeneratedFileWriter`/`IGeneratedFileArchiveStore` (archivage lot 034), `IProcessOxoFileService`.
- Applique les migrations EF (`MigrateIfEnabledAsync<ExcelEtlDbContext>`) juste avant `app.Run()`,
  gaté par `Database:AutoMigrate` (défaut `true`).

**`ExcelETL.BlazorAdmin/Program.cs`** :
- Même connexion SQL pour `ExcelEtlDbContext` (via factory) **et** `ApplicationIdentityDbContext`
  (double enregistrement : `AddDbContext` scoped pour `AddEntityFrameworkStores`, **et**
  `AddDbContextFactory` scoped pour `IUserRepository` — la double lifetime `Scoped` est
  intentionnelle, documentée en commentaire, nécessaire pour que la factory partage la même
  configuration scoped que `AddDbContext`).
- `AddDbContextFactory<SystemLogsDbContext>` — lecture seule, sans migration propre (schéma possédé
  par Serilog).
- Enregistre le même pipeline OXO qu'au WebAPI, plus le pipeline de génération, plus
  `IUserManagementService`, plus `IOxoApiTestClient` (typed `HttpClient`, avec
  `OxoApiTestClientOptionsValidator.ValidateOrThrow` appelé **avant** l'enregistrement DI — échoue au
  démarrage si `BaseUrl`/`ApiKey` sont absents).
- `AddIdentity<ApplicationUser, IdentityRole>` avec :
  - `options.User.AllowedUserNameCharacters` restreint (lettres/chiffres/`_`/`.`, sans `-@+`) —
    lot 050.
  - `options.User.RequireUniqueEmail = true` — lot 050.
  - `.AddErrorDescriber<LocalizedIdentityErrorDescriber>()`
  - `.AddUserValidator<ApplicationUserValidator>()` — lot 050.
  - `.AddClaimsPrincipalFactory<RequirePasswordChangeClaimsPrincipalFactory>()` — lot 045.
- `AddAuthorization` : `FallbackPolicy` = authentification requise, **aucun rôle** au niveau global
  (les rôles sont posés page par page, voir section 4).
- Pipeline HTTP : `UseStatusCodePagesWithReExecute("/not-found")`, `UseHttpsRedirection`,
  `UseRequestLocalization`, `UseAuthentication`, `UseAuthorization`, `UseAntiforgery`,
  `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
- Migrations des deux contextes appliquées avant le seeding (`MigrateIfEnabledAsync` × 2), puis
  `IdentitySeeder.SeedAsync()` (gaté `IdentitySeeding:Enabled`, défaut `true`), puis
  `DefaultProfileSeeder.SeedAsync()` (gaté `ProfileSeeding:Enabled`, défaut `true`).

Aucune contradiction avec la règle Clean Architecture constatée dans ces deux fichiers.

---

## 2. Conventions déjà adoptées

- **Nommage** : `PascalCase` pour les types, dossiers organisés par couche puis par domaine
  fonctionnel (`Extraction/Oxo/...`, `Generation/...`, `Identity/...`, `Persistence/...`).
- **Accès aux données** : exclusivement via des interfaces de repository définies dans
  `Application` et implémentées dans `Infrastructure` (`IImportProfileStore`,
  `IExportProfileStore`, `IUserRepository`, `IGeneratedFileArchiveStore`, `ISystemLogRepository`).
  Aucun composant Razor ni contrôleur n'injecte un `DbContext` directement — confirmé par lecture
  des fichiers `Program.cs` (aucun `AddDbContext` scoped n'est exposé hors des repositories) et par
  grep sur les composants de pages (`ExcelETL.EtlDbContext` n'apparaît jamais dans
  `Components/Pages/`).
- **Gestion des erreurs** :
  - Domain lève `DomainValidationException`/`DomainArgumentOutOfRangeException`/
    `DomainRuleViolationException`, chacune portant un `DomainErrorCode`.
  - Application lève des exceptions dédiées portant un `ApplicationErrorCode`
    (`ImportProfileNotFoundException`, `ProfileNameAlreadyExistsException`, etc.).
  - Le Web API traduit ces exceptions via `GlobalExceptionHandler` +
    `BusinessExceptionLocalizer` en réponses HTTP localisées ; les exceptions BCL non typées
    (`FileFormatException`) sont interceptées **explicitement dans le contrôleur**, pas dans le
    handler global, car `BusinessExceptionLocalizer.TryLocalize` retourne `null` pour tout type
    n'implémentant pas `IHasDomainErrorCode`/`IHasApplicationErrorCode` — vérifié dans
    `src/ExcelETL.WebAPI/Controllers/OxoController.cs`.
- **Validation — où vivent les règles, et absence de duplication côté client** :
  - Les règles de format d'un compte utilisateur (nom d'utilisateur 3-30 caractères,
    prénom/nom 2-50 caractères) sont posées dans
    `src/ExcelETL.Infrastructure/Identity/ApplicationUserValidator.cs`
    (`IUserValidator<ApplicationUser>`, ajouté au pipeline Identity, en plus du validateur par
    défaut). Le jeu de caractères autorisé pour le nom d'utilisateur est une option Identity native
    distincte (`IdentityOptions.User.AllowedUserNameCharacters`, posée dans `Program.cs`).
  - `Users.razor` (formulaires de création/édition) ne reproduit **aucune** de ces règles côté
    client : vérifié par grep, aucune constante de longueur ni de jeu de caractères n'apparaît dans
    le composant. Seule une aide textuelle informative (`Users_UserNameHelp`) est affichée ; la
    validation réelle passe entièrement par le serveur (`IUserManagementService` →
    `UserManager.CreateAsync`/`UpdateAsync`, qui invoque `ApplicationUserValidator`).
  - Même principe pour les profils d'import/export : les contraintes du domaine
    (`SheetExtractionRule`, `HeaderFieldRule`, etc.) sont vérifiées dans les constructeurs Domain ;
    les formulaires Blazor n'en dupliquent pas la logique, ils affichent le message localisé
    renvoyé par l'exception via `BusinessExceptionLocalizer`.
- **Mapping** : pas de bibliothèque de mapping (AutoMapper etc.) — projections manuelles explicites
  (ex. `UserRepository.GetAllAsync` projette directement vers `UserSummary`).
- **Documents de décision transverses** (au lieu d'ADR formels, voir section 6) : plusieurs
  documents « vivants » sous `docs/conventions/` (mis à jour en place, jamais dupliqués par date)
  font office de référence unique pour une convention transverse, notamment :
  `convention-autorisation-pages-blazoradmin.md` (posture d'autorisation, lot 052),
  `convention-secrets-production.md` (secrets par variable d'environnement, jamais de fichier
  `appsettings.Production.json`), `convention-ui-blazor-alignement-boutons.md`,
  `convention-ui-blazor-icones-boutons.md`.

---

## 3. Modèle EF Core existant

Deux `DbContext` distincts, chacun avec sa propre table d'historique de migrations
(`__EFMigrationsHistory_ExcelEtl` / `__EFMigrationsHistory_Identity`) et son propre dossier de
migrations.

### `ExcelEtlDbContext` (`src/ExcelETL.Infrastructure/Persistence/`)

`DbSet` exposés : `ImportProfiles`, `ExportProfiles`, `GeneratedFileRecords`.

Migrations, dans l'ordre chronologique (fichier réel présent sous
`src/ExcelETL.Infrastructure/Persistence/Migrations/`) :

| # | Migration | Contenu (résumé) |
|---|---|---|
| 1 | `20260710140017_InitialCreate` | Schéma initial (pipeline POC, retiré depuis) |
| 2 | `20260710174749_AddCompletedAtUtcToExtractionHistories` | (table depuis retirée) |
| 3 | `20260717113850_AddImportProfile` | `ImportProfile` + règles de feuille (types owned) |
| 4 | `20260718092214_AddExportProfile` | `ExportProfile` + règles de feuille |
| 5 | `20260721095640_RemoveExtractionConfigPoc` | Suppression du pipeline POC |
| 6 | `20260724005133_AddTableauxApplicationsToProfiles` | Colonnes Tableaux/Applications |
| 7 | `20260724115715_AddProfileNameUniqueIndexAndMaxLength` | Index unique sur `Name`, longueur max |
| 8 | `20260725010636_AddGeneratedFileRecord` | Table `GeneratedFileRecords` (archivage) |
| 9 | `20260727215239_AddHeaderRulesToImportProfile` | `HeaderFieldRule`/`HeaderCompositeRule` (lot 047) |

### `ApplicationIdentityDbContext` (`src/ExcelETL.Infrastructure/Identity/`)

Migrations :

| # | Migration | Contenu (résumé) |
|---|---|---|
| 1 | `20260710140119_InitialIdentityCreate` | Schéma Identity standard |
| 2 | `20260711090054_AddFirstNameLastNameToApplicationUser` | Colonnes `FirstName`/`LastName` |
| 3 | `20260727130533_AddRequirePasswordChangeOnFirstLoginToApplicationUser` | Colonne booléenne (lot 044) |
| 4 | `20260728004819_AddUniqueEmailIndexAndNameLengthsToApplicationUser` | Index unique **filtré** sur `NormalizedEmail`, longueur max `FirstName`/`LastName` (lot 050) |

`ApplicationIdentityDbContext.OnModelCreating` (lu directement) configure, **après**
`base.OnModelCreating` (pour gagner sur le mapping par défaut d'Identity) :
```csharp
user.HasIndex(u => u.NormalizedEmail)
    .IsUnique()
    .HasDatabaseName("EmailIndex")
    .HasFilter("[NormalizedEmail] IS NOT NULL");
user.Property(u => u.FirstName).HasMaxLength(50);
user.Property(u => u.LastName).HasMaxLength(50);
```

### Application automatique au démarrage

Les deux hôtes appellent `IServiceProvider.MigrateIfEnabledAsync<TContext>(configuration)`
(`src/ExcelETL.Hosting/DatabaseMigrationHostExtensions.cs`) juste avant/pendant le démarrage,
gaté par la clé de configuration `Database:AutoMigrate` (défaut `true`) et par
`Database.IsRelational()` (aucun effet sous le provider InMemory). C'est la seule mécanique
d'application des migrations — il n'existe pas de script SQL ni de procédure manuelle séparée dans
le repository.

### Ce qui n'a jamais été exercé contre un **vrai** SQL Server

Un rapport de vérification existe :
`docs/audits/audit-verification-base-de-donnees-2026-07-27.md`, daté du 27/07/2026, exécuté contre
une instance SQL Server Express réelle. Il confirme **8/8** migrations `ExcelEtl` et **2/2**
migrations `Identity` appliquées avec succès à cette date-là (`20260725010636_AddGeneratedFileRecord`
étant la plus récente couverte).

**Ce rapport est antérieur aux 3 migrations les plus récentes du dépôt**, qui n'y figurent donc pas
et n'ont, à la connaissance de ce document, **jamais été appliquées à une vraie instance
SQL Server** :
- `20260727130533_AddRequirePasswordChangeOnFirstLoginToApplicationUser`
- `20260727215239_AddHeaderRulesToImportProfile`
- `20260728004819_AddUniqueEmailIndexAndNameLengthsToApplicationUser`

Cette dernière est la plus sensible : elle pose l'**index unique filtré** sur `NormalizedEmail`,
une contrainte de schéma que le provider InMemory utilisé par tous les tests ignore
silencieusement (voir ci-dessous) — elle n'a donc, à ce stade, **jamais été prouvée fonctionnelle
nulle part**, ni par les tests, ni contre un vrai serveur.

### Contraintes non vérifiables avec le provider InMemory

Tous les tests EF Core du dépôt utilisent le provider `Microsoft.EntityFrameworkCore.InMemory`
(jamais un vrai SQL Server, jamais SQLite). Ce provider **ignore silencieusement** :
- Les index uniques (y compris filtrés) — `IX_ImportProfiles_Name`, `IX_ExportProfiles_Name`,
  `EmailIndex` (filtré sur `NormalizedEmail IS NOT NULL`).
- Les contraintes `HasMaxLength` — une chaîne plus longue que la limite déclarée est acceptée sans
  erreur.

Un test dédié existe (`tests/ExcelETL.Infrastructure.Tests/.../ApplicationIdentityDbContextModelTests`,
lot 050) qui vérifie que l'index/le filtre/les longueurs sont bien **déclarés** dans le modèle EF —
il ne prouve pas leur **effet réel** en base, ce que le fichier de test lui-même documente
explicitement en commentaire (vérifié par lecture du fichier).

---

## 4. Authentification / autorisation

### Mécanisme

- **Web API** (`ExcelETL.WebAPI`) : authentification par **clé API** uniquement
  (`ApiKeyAuthenticationHandler`, en-tête `X-Api-Key`, comparaison en temps constant), aucun compte
  utilisateur impliqué. `FallbackPolicy` = authentification requise pour tout endpoint.
- **BlazorAdmin** (`ExcelETL.BlazorAdmin`) : ASP.NET Core Identity (cookie), un seul rôle existant :
  `Admin` (`IdentitySeeder.AdminRoleName`). Aucun autre rôle n'est défini nulle part dans le code.
  `FallbackPolicy` globale = authentification requise, **sans rôle** — chaque page pose
  explicitement son propre niveau via son attribut `@attribute`.

### Tableau exhaustif — une ligne par route de `BlazorAdmin`

Vérifié par lecture directe de chaque fichier `.razor` (attribut `@attribute` réellement présent en
tête de fichier) et de `Components/Layout/NavMenu.razor` (bloc `<AuthorizeView>` encadrant chaque
lien).

| Route | Fichier | Attribut de page réel | Bloc `<AuthorizeView>` du lien NavMenu |
|---|---|---|---|
| `/` et `/import-profiles` | `ImportProfiles.razor` | `[Authorize]` | `<AuthorizeView>` (aucun rôle) |
| `/import-profiles/new`, `/import-profiles/{Id:guid}/edit` | `ImportProfileEditor.razor` | `[Authorize]` | *(pas de lien NavMenu dédié — atteint depuis la liste)* |
| `/export-profiles` | `ExportProfiles.razor` | `[Authorize]` | `<AuthorizeView>` (aucun rôle) |
| `/export-profiles/new`, `/export-profiles/{Id:guid}/edit` | `ExportProfileEditor.razor` | `[Authorize]` | *(idem)* |
| `/import-profiles/test` | `ImportProfileTest.razor` | `[Authorize]` | *(pas de lien NavMenu — bouton sur la page liste, lot S1)* |
| `/export-profiles/test` | `ExportProfileTest.razor` | `[Authorize]` | *(idem)* |
| `/api-test` | `ApiTest.razor` | `[Authorize]` | `<AuthorizeView>` (aucun rôle) |
| `/generated-files` | `GeneratedFiles.razor` | `[Authorize]` | `<AuthorizeView>` (aucun rôle) |
| `/profile` | `Profile.razor` | `[Authorize]` | *(lien fusionné avec le nom d'utilisateur, `<AuthorizeView>` sans rôle)* |
| `/users` | `Users.razor` | `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` | `<AuthorizeView Roles="Admin">` |
| `/logs` | `Logs.razor` | `[Authorize(Roles = IdentitySeeder.AdminRoleName)]` | `<AuthorizeView Roles="Admin">` |
| `/Account/Login` | `Login.razor` | `[AllowAnonymous]` | *(lien conditionnel, bloc `<NotAuthorized>`)* |
| `/Account/AccessDenied` | `AccessDenied.razor` | `[AllowAnonymous]` | *(aucun lien — page de destination, jamais un lien direct)* |
| `/Account/ForcePasswordChange` | `ForcePasswordChange.razor` | `[Authorize]` | *(aucun lien — étape forcée, jamais un lien direct)* |
| `/Error` | `Error.razor` | **aucun** | *(aucun lien)* |
| `/not-found` | `NotFound.razor` | **aucun** | *(aucun lien)* |

Toutes les routes métier (import/export/test/api-test/generated-files/profile) sont
**Authentifié sans rôle** ; seules `/users` et `/logs` exigent le rôle `Admin`. Chaque page métier
listée ci-dessus déclare son attribut explicitement — aucune ne repose silencieusement sur la
`FallbackPolicy` par omission (règle posée par
`docs/conventions/convention-autorisation-pages-blazoradmin.md` §4, vérifiée tenue dans le code).

`/Error` et `/not-found` n'ont aucun attribut d'autorisation propre ; ils sont atteints par
ré-exécution du pipeline (`UseExceptionHandler("/Error")`,
`UseStatusCodePagesWithReExecute("/not-found")`) après qu'une requête a déjà traversé
l'authentification/autorisation normale de la route d'origine.

### Règles de format et d'unicité sur les comptes

Posées dans `src/ExcelETL.Infrastructure/Identity/ApplicationUserValidator.cs` et dans les options
Identity de `Program.cs` :
- Nom d'utilisateur : 3 à 30 caractères, jeu restreint (lettres, chiffres, `_`, `.` — pas de `@`).
- Prénom / Nom : 2 à 50 caractères, aucune restriction de jeu de caractères.
- E-mail : unicité complète (`RequireUniqueEmail = true`), doublée d'un index unique filtré en
  base (voir section 3) — non vérifié contre un vrai serveur à ce jour.
- Le nom d'utilisateur, jamais l'e-mail, est l'identifiant de connexion (`Login.razor` appelle
  `SignInManager.PasswordSignInAsync(Input.UserName, ...)`).

### Parcours complet du premier accès

1. Un compte est créé par un Admin via `/users` (`IUserManagementService.CreateUserAsync`), avec un
   mot de passe temporaire généré aléatoirement (`TemporaryPasswordGenerator`,
   `RandomNumberGenerator`, 12 caractères) et affiché **une seule fois** à l'écran ; le champ
   `RequirePasswordChangeOnFirstLogin` est mis à `true`.
2. À la connexion (`Login.razor.LoginUser`), après succès de `PasswordSignInAsync`, le code relit
   l'utilisateur et redirige explicitement vers `Account/ForcePasswordChange` si le drapeau est
   vrai — **avant** toute prise en compte de `ReturnUrl`.
3. `ForcePasswordChange.razor` (page SSR statique, `[ExcludeFromInteractiveRouting]` via
   `Components/Account/_Imports.razor`) impose la saisie de l'ancien + nouveau mot de passe, appelle
   `IUserRepository.ChangePasswordAsync`, qui lève le drapeau à `false` **uniquement en cas de
   succès**. La page réécrit ensuite le cookie d'authentification en place
   (`SignInManager.RefreshSignInAsync`) — possible uniquement parce que cette page reste en rendu
   SSR statique (pas de circuit interactif déjà ouvert).
4. `RequirePasswordChangeClaimsPrincipalFactory` (`AddClaimsPrincipalFactory`) ajoute une claim
   `RequirePasswordChangeOnFirstLogin=True` sur tout principal construit tant que le drapeau base de
   données est vrai ; elle disparaît automatiquement à la prochaine reconstruction du principal
   (connexion ou `RefreshSignInAsync`) une fois le drapeau à `false`.
5. `PasswordChangeGuard.razor` (rendu inconditionnellement en tête de `MainLayout.razor`) lit cette
   claim depuis l'`AuthenticationState` en cascade et redirige **toute** navigation — hors
   `Account/ForcePasswordChange`, `Account/Logout`, `not-found`, `error` — vers la page de
   changement forcé, à la fois pour une navigation fraîche (`OnInitializedAsync`, couvre aussi la
   SSR statique) et pour une navigation interne en circuit déjà ouvert
   (`<NavigationLock OnBeforeInternalNavigation>`, rendu uniquement si `RendererInfo.IsInteractive`).
6. Après connexion (sans drapeau) ou après changement de mot de passe réussi, la redirection cible
   est `""` (chaîne vide), résolue par `IdentityRedirectManager.RedirectTo` vers la racine `/`, qui
   pointe vers `ImportProfiles.razor` — accessible à tout compte authentifié sans rôle (voir
   tableau ci-dessus). C'est une dépendance explicite documentée dans
   `convention-autorisation-pages-blazoladmin.md` §5 : si `/` devenait un jour réservé Admin, la
   cible de redirection post-connexion devrait changer dans le même lot.

### Les trois sorties d'échec

Vérifié par lecture de `Routes.razor` (`<NotAuthorized><RedirectToLogin /></NotAuthorized>`) et,
plus fiable, par les tests HTTP réels du lot 052 (`BusinessPageAuthorizationHttpTests`,
`AccessDeniedHttpTests`) qui font une vraie requête contre un `WebApplicationFactory<Program>` — la
distinction entre « non authentifié » et « authentifié sans droits » n'est **pas** visible dans le
rendu Blazor côté client (`RedirectToLogin` redirige toujours vers `Account/Login`, sans
distinction) : elle est en réalité tranchée **au niveau du middleware ASP.NET Core**
(`app.UseAuthorization()`, `FallbackPolicy`), avant même que le routeur Blazor n'intervienne, pour
la première requête HTTP de chaque page.

| Situation | Sortie observée (code HTTP + destination) |
|---|---|
| Non authentifié, route quelconque protégée | `302` → `/Account/Login` (challenge, `FallbackPolicy`) |
| Authentifié, rôle insuffisant (`/users` ou `/logs` sans rôle `Admin`) | `302` → `/Account/AccessDenied` (forbid) |
| Authentifié, route réellement inexistante | `200`, rendu de `NotFound.razor` (`/not-found` via ré-exécution) |

`AccessDenied.razor` (`[AllowAnonymous]`) affiche `#access-denied-message` et un lien de retour
`#access-denied-back-link` pointant vers `/` — vérifié pointer vers une route accessible à tout
compte authentifié sans rôle, pour éviter une boucle de refus (test
`ReturnLink_PointsToARouteAccessibleToAnAuthenticatedAccountWithoutARole`).

### Comportement du seeder au démarrage (`IdentitySeeder`)

Lu directement dans `src/ExcelETL.Infrastructure/Identity/IdentitySeeder.cs` :
- Crée le rôle `Admin` s'il n'existe pas (log `Error` en cas d'échec, ne fait pas échouer le
  démarrage).
- Pour chaque compte de `AdminSeedUsers` (configuration) :
  - S'il n'existe pas déjà (recherche par `UserName`), lit le mot de passe à
    `AdminSeedPasswords:{UserName}` (jamais dans le même fichier que l'identité du compte). **Si
    absent ou vide, le compte est silencieusement ignoré avec un `LogWarning`** — le démarrage ne
    plante pas.
  - Si la création échoue (`IdentityResult` en échec, ex. violation d'unicité e-mail avec les
    nouvelles règles du lot 050), un `LogError` est émis et la méthode retourne — le compte n'est
    pas créé, mais **le démarrage continue** pour les comptes suivants et pour le reste de
    l'application.
  - Si le compte existe déjà et a déjà le rôle `Admin`, ne fait rien (idempotent).
- 3 comptes seedés en configuration (`appsettings.json` de `BlazorAdmin`) : `SLB`, `J2M`, `JPN`,
  chacun avec un e-mail dans `appsettings.json` — un test dédié
  (`ApplicationUserValidatorTests.ValidateAsync_SeedUserValues_SatisfyTheValidator`) relit ces
  valeurs réelles et vérifie qu'elles satisfont `ApplicationUserValidator`, empêchant une régression
  qui verrouillerait l'admin lui-même hors d'une base fraîche.

---

## 5. Conventions de tests

### Structure et frameworks

- xUnit + FluentAssertions **7.0.0** (épinglé — v8+ est sous licence commerciale, incompatible avec
  la contrainte OSS du projet) + Moq 4.20.72 pour les tests .NET 10.
- bUnit 2.7.2 pour les composants Blazor (`class FooTests : BunitContext`, `Render<Foo>()`).
- `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) pour les tests HTTP réels,
  disponible dans `ExcelETL.WebAPI.Tests` **et**, depuis le lot 049, dans
  `ExcelETL.BlazorAdmin.Tests` (premier harnais HTTP réel pour ce projet — il n'en existait aucun
  avant).
- `Microsoft.EntityFrameworkCore.InMemory` pour tous les tests de repository EF Core (jamais SQLite,
  jamais un vrai SQL Server).
- Pas de bibliothèque HTTP-mock (type MockHttp) : un `FakeHttpMessageHandler` maison, colocalisé par
  projet de test qui en a besoin.

### Comptage réel (exécution complète, ce commit)

Suite complète exécutée via `dotnet test --verbosity quiet` sur ce commit — **tout est vert**,
aucun test ignoré :

| Projet de test | Résultat |
|---|---|
| `ExcelETL.Domain.Tests` | 300 / 300 |
| `ExcelETL.Application.Tests` | 180 / 180 |
| `ExcelETL.Infrastructure.Tests` | 212 / 212 |
| `ExcelETL.WebAPI.Tests` | 25 / 25 |
| `ExcelETL.BlazorAdmin.Tests` | 663 / 663 |
| `ExcelETL.Hosting.Tests` | 6 / 6 |
| `legacy/NewApiPingService.Tests` (.NET Framework 4.8) | 9 / 9 |
| `legacy/ExcelProcessingClientService.Tests` (.NET Framework 4.8) | 15 / 15 |
| **Total** | **1410 / 1410** |

### Ce qui est prouvé par une vraie requête HTTP vs. par bUnit seul

C'est un point de vigilance explicitement documenté dans le dépôt lui-même
(`docs/conventions/recommandations-tickets-tdd.md` §6, « Ce qu'un test bUnit ne prouve jamais »),
suite à deux régressions réelles (lots 049 et 051) où une page passait tous ses tests bUnit tout en
étant **inaccessible en pratique** (page « Introuvable » en navigateur réel malgré un rendu bUnit
correct).

- **bUnit** : rend un composant directement, en court-circuitant le routage, le mode de rendu
  (SSR statique vs interactif) et tout le pipeline HTTP/middleware. Il prouve le contenu et le
  comportement d'un composant **une fois atteint**, jamais qu'il est réellement atteignable.
- **`WebApplicationFactory<Program>`** : seule preuve retenue dans ce dépôt pour tout ce qui touche
  au routage, à la redirection ou à l'autorisation. Fichiers réels concernés dans
  `ExcelETL.BlazorAdmin.Tests` :
  - `Account/ForcePasswordChangeHttpTests.cs`
  - `Account/RegisterRemovalHttpTests.cs`
  - `Account/UserNameLoginIdentityHttpTests.cs`
  - `Account/AccessDeniedHttpTests.cs`
  - `Authorization/BusinessPageAuthorizationHttpTests.cs`
  - `Components/AppRenderModeHttpTests.cs`
  - Dans `ExcelETL.WebAPI.Tests` : `Oxo/OxoProcessEndpointTests.cs`,
    `Authentication/ApiKeyAuthenticationTests.cs`, `Health/HealthPingTests.cs`,
    `Configuration/ConnectionStringConfigurationTests.cs`.
- Le document interne va plus loin : une requête HTTP seule ne suffit pas non plus pour un défaut
  qui n'apparaît qu'une fois le circuit SignalR ouvert (un `HttpClient` n'ouvre jamais de circuit) —
  la seule garantie retenue est une assertion sur le **mode de rendu réellement servi** (présence ou
  absence du marqueur `"type":"server"` dans le corps de la réponse), utilisée explicitement dans
  `AppRenderModeHttpTests.cs`.
- Chaque harnais `WebApplicationFactory` de `ExcelETL.BlazorAdmin.Tests` bascule
  `ExcelEtlDbContext`/`ApplicationIdentityDbContext` (et, pour les tests touchant `/logs`,
  `SystemLogsDbContext`) vers le provider EF Core InMemory, et désactive explicitement
  `Database:AutoMigrate`, `IdentitySeeding:Enabled`, `ProfileSeeding:Enabled`,
  `Serilog:EnableMsSqlServerSink` — vérifié identique dans les 4 fichiers concernés.

---

## 6. ADR (Architecture Decision Records)

**Aucun répertoire ni fichier `ADR`/`adr` n'existe dans le dépôt** (recherche exhaustive par nom de
fichier — le seul faux positif est `RepeatingBlockReadResult.cs`, sans rapport). Il n'y a pas de
convention de type ADR formalisée (pas de numérotation, pas de statut proposé/accepté/rejeté).

Les décisions d'architecture et de convention transverse sont à la place consignées dans des
**documents vivants** sous `docs/conventions/` — mis à jour en place, sans empilement de version,
selon `docs/conventions/convention-nommage-documents.md`. Les plus significatifs pour cet état des
lieux :
- `convention-autorisation-pages-blazoradmin.md` — modèle d'autorisation à deux niveaux (section 4).
- `convention-secrets-production.md` — secrets de production exclusivement par variable
  d'environnement, jamais de fichier `appsettings.Production.json`.
- `convention-ui-blazor-alignement-boutons.md`, `convention-ui-blazor-icones-boutons.md`,
  `convention-ui-blazor-tableaux-generes-lisibilite.md` — conventions UI transverses.
- `recommandations-tickets-tdd.md` — méthodologie TDD du projet, y compris la leçon bUnit/HTTP
  (section 5).

Le fichier `CLAUDE.md` à la racine du dépôt contient par ailleurs un historique chronologique très
détaillé, lot par lot, de chaque décision technique prise — plus proche d'un journal de bord
exhaustif que d'un ADR par sujet, mais c'est la source la plus complète de justification « pourquoi »
pour toute décision passée. Aucun de ces documents n'est un ADR au sens strict du terme.

---

## 7. Écarts et points de vigilance avant mise en service

- **Migrations non vérifiées contre un vrai SQL Server** (détail section 3) : les 3 migrations les
  plus récentes, dont l'index unique filtré sur l'e-mail, n'ont jamais été exercées contre une
  vraie instance. Le rapport `audit-verification-base-de-donnees-2026-07-27.md` existant est
  antérieur à ces 3 migrations et ne peut donc pas être invoqué comme preuve les concernant.
- **Secrets de production jamais positionnés** : `ApiKeyAuthentication:ApiKey` (WebAPI),
  `OxoApiTestClient:BaseUrl`/`ApiKey` (BlazorAdmin) et `AdminSeedPasswords:{UserName}`
  (BlazorAdmin) sont tous absents de `appsettings.json`/`appsettings.Production.json` par
  conception (voir `convention-secrets-production.md`) — le démarrage de chaque hôte échoue tant
  que les variables d'environnement correspondantes ne sont pas positionnées sur le serveur cible.
  Aucun fichier `appsettings.Production.json` n'existe ni ne doit exister dans le dépôt.
- **Hébergement IIS jamais exercé en dehors du guide** : `docs/conventions/guide-deploiement-am-oxo-etl-windows-server.md`
  décrit un déploiement IIS avec deux sites distincts (WebAPI/BlazorAdmin), un certificat
  auto-signé par site, une instance SQL Server nommée. Le document lui-même indique une
  vérification EF Core « déjà faite en local » et « à refaire sur le serveur cible avant le
  go-live » — c'est-à-dire que la vérification en conditions réelles de déploiement (IIS + serveur
  cible, pas un poste de développement) n'a, à la connaissance de ce document, pas encore eu lieu.
- **Code mort confirmé absent** (points explicitement soulevés dans les lots précédents,
  revérifiés au code à ce commit) :
  - `IFileStorageService` (ancien mécanisme d'archivage, lot K) : entièrement supprimé — la seule
    occurrence restante du nom est un commentaire dans `ProcessOxoFileService.cs` expliquant son
    retrait au lot 046.
  - `IWorkbookReader.SheetExists` : n'existe plus dans l'interface ni dans son implémentation.
  - `DirectCell` (Domain) : n'est plus du code mort — utilisé par `HeaderFieldRule.Cell` depuis le
    lot 047 (extraction d'en-tête pilotée par profil).
- **`Register.razor` — confirmé totalement absent** (question ouverte 3, détaillée section 8).
- **Divergence documentaire trouvée et déjà résolue dans le code, mais qui affecte la lecture d'un
  document antérieur** : `docs/audits/audit-design-blazoradmin-2026-07-27.md` (antérieur à ce
  commit) et `etat-des-lieux-technique-2026-07-27.md` (également antérieur) se contredisaient sur
  la protection du lien « Journaux ». Le code à ce commit (lot 052) confirme que la **vraie**
  divergence pré-lot-052 était que `/logs` ne portait qu'un `[Authorize]` sans rôle au niveau de la
  page, alors que son lien dans `NavMenu.razor` était bien dans un bloc `<AuthorizeView Roles="Admin">` —
  c'est-à-dire un lien correctement masqué protégeant une page qui, elle, restait accessible par
  URL directe à tout compte authentifié. C'est exactement le piège que
  `convention-autorisation-pages-blazoradmin.md` §3 nomme : « masquer un lien n'est pas une
  autorisation ». **Corrigé au commit courant** (`Logs.razor` porte désormais
  `[Authorize(Roles = IdentitySeeder.AdminRoleName)]`, vérifié par lecture directe et par les tests
  HTTP du lot 052). Le document fautif pour la période qu'il couvre n'est ni l'un ni l'autre des
  deux documents cités : les deux décrivaient une facette réelle mais partielle d'un état
  effectivement incohérent — c'est cette incohérence elle-même, présente dans le code de l'époque,
  qui a été corrigée.
- **`AllowedHosts` production** : les deux `appsettings.json` de production portent
  `"AllowedHosts": "*"` — aucune restriction de nom d'hôte au niveau applicatif ; la restriction
  réelle dépendra entièrement de la configuration IIS/DNS au moment du déploiement, non vérifiable
  depuis le code.
- **`AllowedUserNameCharacters` et provider InMemory** : la restriction du jeu de caractères du nom
  d'utilisateur est une option Identity native testée avec succès contre un vrai
  `UserManager` + EF Core InMemory dans `UserNameCharacterSetIdentityIntegrationTests` (vérifié)
  — cette option agit au niveau applicatif Identity, pas au niveau schéma SQL, donc son
  comportement n'est **pas** affecté par la limite InMemory qui touche les migrations (section 3).

---

## 8. Questions ouvertes à trancher

### 1. Invariant des deux gabarits sur `/users` — étendu, doublé, ou inchangé ?

**Étendu**, pas doublé. Le test qui verrouille l'identité de contenu entre le tableau desktop et les
cartes mobiles préexistait (convention « V2 » du projet) et a été **directement modifié en place**
pour couvrir Prénom/Nom au lot 050 :

- `Users_CardTemplate_DisplaysSameContentAsTable`
  (`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/UsersTests.cs`) — le commentaire au-dessus du test
  dit explicitement : *« Lot 050 (50.7, D6) : FirstName/LastName rendered in both templates; extends
  the existing V2 content-identity coverage rather than duplicating it in a new test »*. Le corps
  du test vérifie `alice@example.com`, `alice`, `Alice`, `Smith` à la fois dans la ligne du tableau
  (`table tbody tr`) et dans la carte mobile (`div.d-md-none .card`).

Aucun second test parallèle n'a été créé pour ces deux colonnes. Le rôle (badge) est couvert par un
test séparé et distinct, `Users_AdminRow_ShowsAdminBadge` (attendu, puisque le badge est un élément
d'affichage différent, pas une simple donnée textuelle).

### 2. Règle d'auto-suppression (lot 44.3) — toujours effective après le restylage 50.9 ?

**Oui, confirmée dans le code et par 3 tests dédiés.** La logique `CanDelete`/`IsSoleRemainingAdmin`
n'a pas changé de comportement au restylage — seul le rendu visuel du bouton (icône seule au lieu
de texte, `AdminIconMarkup.Trash`) a changé.

Emplacement de la règle (`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/Users.razor`) :
```csharp
private bool CanDelete(UserSummary user) => user.Id != _currentUserId && !IsSoleRemainingAdmin(user);
private bool IsSoleRemainingAdmin(UserSummary user) => _adminUserIds.Count == 1 && _adminUserIds[0] == user.Id;
```
Le rendu conditionne bien la présence de l'attribut HTML `disabled` sur le bouton (pas seulement une
classe CSS visuelle) :
```razor
@if (CanDelete(user)) { <button id="...">...</button> }
else { <button id="..." disabled aria-label="@DeleteDisabledReason(user)">...</button> }
```
Dupliqué à l'identique pour le tableau desktop et la carte mobile (2 occurrences, mêmes conditions).

Tests qui verrouillent ce comportement, tous relus et confirmés présents :
- `CurrentUserRow_DeleteButtonIsDisabled`
- `SoleRemainingAdminRow_DeleteButtonIsDisabled`
- `NonAdminNonCurrentUserRow_DeleteButtonIsEnabled`

Les trois vérifient `HasAttribute("disabled")` sur le vrai bouton HTML, pas une apparence. Le doute
relevé le 28/07 sur une capture d'écran n'est donc pas corroboré par le code à ce commit ; il est
possible que la capture concernée datait d'un état intermédiaire du lot 052/050, ou concernait un
autre aspect visuel (couleur, contraste) que ce document ne peut pas trancher sans accès à
l'image elle-même.

### 3. Page `/Account/Register` — confirmation de la disparition complète

**Confirmée, sur les 5 axes.**
- **Fichier** : `Register.razor` n'existe plus sous
  `src/ExcelETL.BlazorAdmin/Components/Account/Pages/` (recherche par nom de fichier, résultat
  vide).
- **Route** : aucune occurrence de `@page "/Account/Register"` dans tout `src/`.
- **Références** : une recherche texte du mot « Register » dans tout le dépôt (hors `bin`/`obj`)
  ne retourne plus qu'**un seul fichier**, `Components/Layout/PasswordChangeGuard.razor`, et
  uniquement dans un commentaire historique expliquant que la page « existait au moment où ce
  commentaire a été écrit mais a été retirée au lot 051 ».
- **Clés de ressources** : `NavMenu_Register`, `Login_RegisterLink`, `Register_Title`,
  `Register_Subtitle`, `Register_ConfirmPasswordLabel`, `Register_Submit` — aucune de ces clés
  n'existe plus dans `Resources/BlazorAdminMessages.resx`/`.fr.resx` (grep confirmé vide).
- **Tests** : `RegisterTests.cs` (bUnit) et `RegisterFormFloatingAuditTests` ont été supprimés.
  Un test HTTP réel dédié existe désormais, `RegisterRemovalHttpTests.cs`, qui vérifie qu'une
  requête vers `/Account/Register` ne mène nulle part d'utilisable (voir aussi section 7 pour la
  nuance sur le code HTTP réellement observé : `302` vers `/Account/Login` pour un visiteur non
  authentifié, la route étant absorbée par la `FallbackPolicy` avant même d'atteindre le routeur
  Blazor).
- `NavMenu.razor` ne contient plus aucun lien vers `Account/Register` — le bloc
  `<NotAuthorized>` n'offre plus que la connexion (`#nav-login-link`).

### 4. Redirection post-connexion pour un compte sans rôle

Un compte authentifié **sans rôle** est envoyé, après connexion normale (pas de mot de passe
temporaire en attente), vers `ReturnUrl` s'il était présent, sinon vers `""` — résolu par
`IdentityRedirectManager.RedirectTo` en `/` (racine de l'application).

Après un changement de mot de passe forcé (premier accès), la cible est également `""` → `/`,
donc la **même route**.

`/` est mappée par `ImportProfiles.razor` (`@page "/"`, en plus de `@page "/import-profiles"`), dont
l'attribut réel est `[Authorize]` **sans rôle** — cette route est donc bien accessible à un compte
authentifié sans rôle, ce que confirme aussi directement le test HTTP
`NonAdminAccount_CanReachEveryBusinessRoute` (paramétré sur `"/"` parmi les routes vérifiées) et le
test `Get_WithoutAuthentication_ReturnsOkAndTheAccessDeniedMessage`/
`ReturnLink_PointsToARouteAccessibleToAnAuthenticatedAccountWithoutARole` côté page de refus.

Ce point est documenté comme une dépendance structurelle explicite dans
`convention-autorisation-pages-blazoradmin.md` §5 : si `/` devenait un jour réservée aux comptes
`Admin`, la cible de redirection post-connexion devrait être changée dans le même lot, faute de
quoi tout compte non-Admin se retrouverait immédiatement renvoyé vers `/Account/AccessDenied` après
chaque connexion.

---

## Non couvert / incertain

- **Vérification réelle en base de production** : ce document ne peut pas confirmer, à partir du
  seul code, si les 3 migrations les plus récentes (section 3) ont été appliquées à une instance
  SQL Server réelle depuis le rapport du 27/07 — seule une inspection directe d'une base réelle
  pourrait le confirmer ou l'infirmer. Traité comme non vérifié, pas comme absent.
- **Déploiement IIS réel** : le guide de déploiement décrit une procédure et des décisions actées,
  mais ce document ne peut pas attester qu'un déploiement IIS réel (avec les deux sites distincts,
  le certificat auto-signé, les hostnames DNS) a effectivement eu lieu sur le serveur cible.
- **Doute visuel du 28/07 sur le bouton de suppression (question 2)** : le code et les tests ne
  corroborent aucune régression fonctionnelle de la règle `CanDelete`. Sans accès à la capture
  d'écran mentionnée, ce document ne peut pas exclure un défaut purement visuel (contraste,
  positionnement) distinct du comportement `disabled` lui-même.
- **Contenu réel des secrets de production** (`ApiKeyAuthentication__ApiKey`,
  `OxoApiTestClient__ApiKey`, `AdminSeedPasswords__*`) : par construction, ces valeurs ne sont
  jamais présentes dans le dépôt — ce document ne peut donc ni les confirmer ni les infirmer, et ce
  n'est pas un défaut à signaler mais la conséquence voulue de
  `convention-secrets-production.md`.
- **Couverture exacte des scénarios de contraste/accessibilité visuelle réels** (rendu navigateur) :
  plusieurs lots antérieurs au commit courant (039 à 042) notent explicitement l'absence de
  vérification en navigateur réel pour cause d'indisponibilité d'un environnement de prévisualisation
  au moment où ils ont été livrés. Ce document, purement basé sur la lecture du code et l'exécution
  de la suite de tests automatisés, ne referme pas cet écart.
