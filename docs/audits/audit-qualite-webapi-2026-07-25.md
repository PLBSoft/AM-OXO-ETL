# Audit qualité — ExcelETL.WebAPI

## Métadonnées de l'audit

- **Date d'exécution** : 2026-07-25
- **Périmètre** : `src/ExcelETL.WebAPI` et `tests/ExcelETL.WebAPI.Tests` uniquement. Domain,
  Application, Infrastructure et BlazorAdmin n'ont pas été lus au-delà de ce qui était
  strictement nécessaire pour vérifier les signatures utilisées par ce projet (ex. le contenu
  de `ProcessOxoFileResult`).
- **Commit réellement audité** : `8119f78` (HEAD de `main` au moment de l'exécution).
- **⚠️ Écart avec la demande** : la demande d'audit référence le commit `d018a90` (2026-07-24,
  812/812 tests) en affirmant que `ExcelETL.WebAPI` n'a reçu aucun changement depuis le Lot K et
  que son compteur de tests est resté à 13/13 depuis le 22/07. **C'est inexact au moment de
  l'exécution de cet audit** : le Lot 034 (« archivage des fichiers source/cible générés »,
  commits `14efa51`…`5246b9d`, 2026-07-25) a modifié `OxoController.cs` et `Program.cs` pour
  câbler un archivage best-effort, et `tests/ExcelETL.WebAPI.Tests` compte désormais **19 tests**
  (répartis en `ApiKeyAuthenticationTests` ×3, `HealthPingTests` ×3,
  `ConnectionStringConfigurationTests` ×2, `OxoProcessEndpointTests` ×11), pas 13. Cet audit
  porte donc sur l'état réel du code à HEAD, qui inclut le Lot 034 — pas sur l'état figé décrit
  par la demande. Les constats ci-dessous en tiennent compte.

---

## 1. Respect de Clean Architecture / Onion

**RAS.** `ExcelETL.WebAPI.csproj` référence exactement trois projets : `ExcelETL.Application`,
`ExcelETL.Hosting`, `ExcelETL.Infrastructure` (aucune référence à `ExcelETL.BlazorAdmin`, aucune
référence directe à `ExcelETL.Domain` en tant que `ProjectReference` — les types Domain utilisés
dans `GlobalExceptionHandler.cs` (`DomainValidationException`, `DomainArgumentOutOfRangeException`,
`DomainRuleViolationException`) arrivent transitivement via `Application`/`Infrastructure`, ce qui
est la relation attendue puisque Domain est la couche la plus interne).

`OxoController` ne contient pas de logique métier écrite en dur (pas de calcul, pas de règle
d'extraction/génération) : il valide la présence du fichier, journalise, tamponne le flux, construit
la commande, délègue à `IProcessOxoFileService`, puis traduit le résultat en réponse HTTP. Le seul
point limite est documenté au § 4 (interprétation de `ImportResult.Equipement is null`
directement dans le contrôleur).

`HealthController` est un simple endpoint de liveness, sans logique.

---

## 2. Résidus du pipeline POC retiré (Lot K4)

**RAS — aucun résidu trouvé.**

- Recherche exhaustive (`ExcelController`, `ClosedXmlExtractionService`, `CellMapping`,
  `ExtractionHistor*`, `SheetConfig`, `ExtractionConfig`) dans `src/ExcelETL.WebAPI` : un seul
  résultat, un commentaire historique dans `Program.cs:82` (« since Lot K4's removal of the old
  ExtractionConfig/ProcessExcelFile pipeline ») — c'est une note explicative sur le nettoyage déjà
  effectué, pas un résidu de code actif.
- `appsettings.json` / `appsettings.Development.json` : aucune section résiduelle
  (`ExtractionConfig`, ancien nom de table, ancienne chaîne de connexion `ExcelEtl` — déjà
  renommée en `AM-OXO-ETL-MAD-REL` par le Lot 029).
- `Program.cs` : aucune route, middleware, ou enregistrement DI ne référence l'ancien pipeline,
  même commenté/désactivé. Le seul contrôleur MVC en plus d'`OxoController` est `HealthController`
  (liveness), pas un vestige.
- `tests/ExcelETL.WebAPI.Tests` ne contient aucun fichier de test orphelin visant l'ancien
  pipeline ; les 4 fichiers présents (`ApiKeyAuthenticationTests`, `HealthPingTests`,
  `ConnectionStringConfigurationTests`, `OxoProcessEndpointTests`) sont tous rattachés au
  périmètre actuel et le projet est bien référencé dans `ExcelETL.slnx`.
- OpenAPI : `builder.Services.AddOpenApi()` génère le document dynamiquement depuis les
  contrôleurs actuels ; aucun fichier `swagger.json`/`openapi.json` statique n'est committé qui
  pourrait contenir une trace figée de l'ancien contrat.

---

## 3. Contrat de `POST /api/oxo/process`

- **`ImportProfileId`/`ExportProfileId`** (`Contracts/ProcessOxoFileRequest.cs`) sont bien deux
  propriétés `Guid` explicites du corps de requête (`[FromForm]`), sans valeur par défaut
  métier appliquée nulle part dans `OxoController` — aucun profil n'est déduit implicitement.
  **Nuance factuelle** : ce sont des `Guid` (value type) sans attribut `[Required]` ni validation
  de modèle explicite. Si le champ est absent du multipart, le model binding ASP.NET Core laisse
  silencieusement `Guid.Empty` plutôt que de faire échouer la validation de modèle (`[ApiController]`
  ne rejette pas un `Guid` manquant comme il rejetterait une chaîne requise absente). En pratique,
  la conséquence reste correcte côté métier : `Guid.Empty` ne correspondra à aucun profil persisté,
  donc `IImportProfileStore`/`IExportProfileStore` renverront `null` et
  `ImportProfileNotFoundException`/`ExportProfileNotFoundException` produiront un 404 — mais ce
  n'est pas un rejet *explicite* de la requête pour absence de paramètre, c'est un 404 qui ressemble
  à un « profil supprimé entretemps ». Aucun test ne couvre ce cas précis (requête sans le champ
  `ImportProfileId`/`ExportProfileId` du tout).
- **Comportement synchrone bout-en-bout** : confirmé. `Process` lit le flux entrant, le tamponne
  intégralement en mémoire (`MemoryStream`), construit le lecteur ClosedXML, appelle
  `ProcessAsync` de façon synchrone (attendue), puis retourne soit un `ProblemDetails` 422, soit
  un `FileStreamResult` directement dans la même requête HTTP — aucune file d'attente, aucun
  callback, aucun identifiant de job à interroger plus tard.
- **Archivage** : depuis le Lot 034 (2026-07-25, wiring en `ca870d5`), le contrôleur transmet
  `sourceFileContent` (le tableau d'octets déjà tamponné) à `ProcessOxoFileCommand`, et
  `IProcessOxoFileService` (Application) déclenche un archivage best-effort du fichier source et,
  le cas échéant, du fichier cible via `IGeneratedFileWriter`/`IGeneratedFileArchiveStore`
  (Infrastructure), documenté comme volontairement isolé dans son propre `try`/`catch` pour ne
  jamais faire échouer la réponse HTTP. **Ce mécanisme est bien couvert par un ticket explicite**
  (Lot 034, `docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md`) — ce n'est donc pas un
  archivage ajouté hors cadre, contrairement à ce que la grille de la demande semblait redouter
  (elle a été rédigée avant que le Lot 034 ne soit fusionné). À noter simplement : la demande
  d'audit, écrite en supposant qu'aucune persistance de ce type n'existait, est obsolète sur ce
  point précis — signalé ici plutôt que silencieusement contourné.

---

## 4. Duplication

- **Interprétation du rejet de fichier dupliquée entre couches** : `OxoController` teste lui-même
  `result.ImportResult.Equipement is null` (ligne 58) pour décider entre un 422 et un 200. Cette
  même interprétation (« `Equipement is null` ⇒ rejet fichier complet, modèle §3.1 ») est déjà
  répétée ailleurs dans la solution — `ImportProfileTest.razor`/`ExportProfileTest.razor` côté
  BlazorAdmin font exactement la même vérification sur le même champ (fait constaté en lisant
  `CLAUDE.md`, pas en relisant le code Blazor lui-même, conformément au périmètre de cet audit).
  `ProcessOxoFileResult` (Application) n'expose qu'`ImportResult`/`GeneratedFileStream`/
  `GeneratedFileName` — aucune propriété de statut explicite (`IsRejected`/`Status`) n'encapsule
  cette règle une seule fois. **Refacto envisageable (non implémentée)** : exposer un statut
  explicite sur `ProcessOxoFileResult` (ou un type dédié) que chaque consommateur HTTP/Blazor lit
  directement, au lieu de réinterpréter `Equipement is null` à chaque site d'appel — réduit le
  risque qu'un futur site d'appel oublie cette règle ou la formule différemment.
- Pas d'autre duplication observée dans `OxoController` : la construction du flux, du lecteur, de
  la commande et le mapping d'erreurs (`error.Sheet`/`BlockIdentifier`/`Code`/`Message`) ne sont
  présents qu'une fois.

---

## 5. Cohérence des conventions déjà actées

**RAS.** `ApiKeyAuthenticationHandler` est un unique handler enregistré une seule fois dans
`Program.cs` (`AddAuthentication(...).AddScheme<...>`), appliqué globalement via
`FallbackPolicy` (`RequireAuthenticatedUser()`, pas de policy par route) — `HealthController`
et `OxoController` sont protégés de façon identique, sans réimplémentation par route. La
comparaison de clé utilise `CryptographicOperations.FixedTimeEquals` (comparaison à temps
constant), cohérent avec l'intention de protection contre le timing attack documentée dans
CLAUDE.md.

**Note mineure, non demandée par la grille mais observée en lisant le handler** : quand
`providedKey`/`Options.ApiKey` ont des longueurs différentes, `FixedTimeEquals` retourne `false`
immédiatement (pas d'exception, comportement .NET documenté) — donc pas de bug, mais la
comparaison n'est à temps constant que pour des clés de même longueur ; une clé candidate plus
courte ou plus longue que la vraie clé est rejetée plus vite, fuitant un bit d'information sur la
longueur de la clé. Risque jugé négligeable dans ce contexte (un seul client legacy connu, clé
statique en configuration), mentionné pour complétude.

---

## 6. Dette de test

- **Chiffre réel à HEAD : 19/19 tests**, pas 13/13 (voir l'écart signalé en tête de document).
  `ApiKeyAuthenticationTests` (3), `HealthPingTests` (3), `ConnectionStringConfigurationTests`
  (2, Lot 029 — vérifie juste la chaîne de connexion dans les `appsettings*.json`),
  `OxoProcessEndpointTests` (11, Lot K + Lot 034).
- **Couverture réelle du contrat `/api/oxo/process`** (`OxoProcessEndpointTests`) :
  - Sans clé API → 401 ✅
  - Profil d'import inconnu → 404 ✅
  - Profil d'export inconnu → 404 ✅
  - Fichier rejeté (règle métier, PROCEDURE vide) → 422, pas de fichier généré ✅
  - Requête valide (fixture réelle C7401) → 200, contenu généré correct, archive locale (ancien
    mécanisme `IFileStorageService`) ✅
  - Fixture D8570 (cas `TypeElement` non reconnu) → toujours extrait normalement ✅
  - Lot 034 : enregistrement d'archive persisté pour succès/avertissement non bloquant/rejet, avec
    fichiers réellement écrits sur disque ✅, et un cas d'échec d'archivage isolé (mock
    `IGeneratedFileWriter` qui lève) prouvant que la réponse HTTP reste 200 malgré l'échec ✅
  - Log d'upload/egress capturé via `CapturingLogger` ✅
  - **Cas non couverts constatés** :
    - **Fichier « malformé »** au sens strict (octets non-xlsx, pas juste une règle métier
      violée) : aucun test n'envoie un contenu qui ferait échouer la construction de
      `ClosedXmlWorkbookReader`/`XLWorkbook` elle-même (ex. `new
      ClosedXmlWorkbookReader(new MemoryStream(sourceFileContent))` avec des octets aléatoires).
      Ce chemin n'est intercepté par aucun type explicite dans
      `GlobalExceptionHandler.StatusCodeFor` (switch fermé sur des types métier connus) — il
      tomberait donc sur le comportement par défaut d'`AddProblemDetails()`/`UseExceptionHandler()`
      (probable 500), jamais vérifié par un test à ce niveau.
    - **Fichier vide / absent** : `OxoController` a une branche dédiée (`request.File is null ||
      request.File.Length == 0` → 400), mais **aucun test HTTP n'exerce ce chemin** — le seul test
      « sans fichier » du fichier de tests est en réalité le test « sans clé API », qui échoue
      avant même d'atteindre cette validation.
    - **`ImportProfileId`/`ExportProfileId` absents du multipart** (plutôt qu'inconnus) — voir
      § 3, chemin non testé.
    - Pas de test vérifiant qu'un `DomainRuleViolationException` (409, cf.
      `GlobalExceptionHandler.StatusCodeFor`) est effectivement atteignable depuis cette route en
      pratique — peut-être qu'aucun scénario réel ne le déclenche depuis `/api/oxo/process`
      (question laissée à Claude AI / Simon plutôt que supposée ici).
  - **Conclusion sur ce point** : le chiffre 19/19 (comme le 13/13 supposé par la demande) reflète
    une couverture solide du **chemin nominal et des rejets métier attendus**, mais un vrai trou
    sur les **entrées structurellement invalides** (fichier vide, fichier corrompu, paramètres
    absents) — ce n'est pas un simple statu quo non retouché : le Lot 034 a ajouté des tests
    récents et de qualité, mais uniquement sur son propre périmètre (archivage), sans combler ces
    trous préexistants.
- **`legacy/ExcelProcessingClientService.Tests`** (15 tests, hors `ExcelETL.slnx`, confirmé par
  lecture de `ExcelETL.slnx` qui ne référence que les projets `src/`/`tests/` de la solution .NET
  10) : ce projet teste le client HTTP legacy (`ExcelProcessingClientService`, .NET Framework 4.8)
  qui **consomme** `POST /api/oxo/process` — lié fonctionnellement à ce contrat (Lot K3 a changé sa
  signature pour poster vers `api/oxo/process` avec les deux `Guid` de profil), mais c'est un
  projet distinct, hors solution .NET 10, dont le code n'a pas été lu dans cet audit (hors
  périmètre explicite de la demande). Signalé ici pour mémoire, conformément à la demande.

---

## 7. Gestion des erreurs et logs

- **Codes HTTP** : cohérents et centralisés dans `GlobalExceptionHandler.StatusCodeFor` —
  `ImportProfileNotFoundException`/`ExportProfileNotFoundException` → 404,
  `DomainValidationException`/`DomainArgumentOutOfRangeException`/
  `WorksheetNotFoundInWorkbookException` → 400, `DomainRuleViolationException` → 409, tout le
  reste → 500. Le rejet métier explicite (fichier complet rejeté, §3.1 du modèle) est traité à part
  directement dans `OxoController` avec un 422 dédié — cohérent avec la sémantique HTTP
  (« entité bien formée mais sémantiquement invalide »).
- **`ProblemDetails`** : utilisé de façon cohérente à deux endroits — `GlobalExceptionHandler`
  (exceptions typées) et directement dans `OxoController` (fichier vide → 400, fichier rejeté →
  422 avec `Extensions["errors"]`). `builder.Services.AddProblemDetails()` est enregistré une
  seule fois dans `Program.cs`, donc le format de réponse par défaut (exceptions non gérées) est
  également du `ProblemDetails`, cohérent avec le reste.
- **Localisation** : `GlobalExceptionHandler` résout le détail via `BusinessExceptionLocalizer`
  sur la base de la culture négociée par `RequestLocalizationOptions` (`Accept-Language`
  uniquement, cohérent avec le fait que c'est un endpoint M2M — commentaire explicite dans
  `Program.cs`). `OxoController` utilise directement `IStringLocalizer<ApplicationMessages>` pour
  ses deux messages HTTP ad hoc (`EmptyFileUploadRequired`, `OxoFileRejected`) — mêmes clés
  `ApplicationMessages`, pas de fichier de ressources dupliqué côté WebAPI.
- **Cohérence avec Serilog / `SystemLogs`** : `Program.cs` appelle
  `builder.Host.AddOxoHostLogging("ExcelETL.WebAPI", connectionString)` (mécanisme partagé défini
  une seule fois dans `ExcelETL.Hosting`, Lot G3) — pas de configuration Serilog dupliquée ou
  réinventée dans ce projet. Les logs applicatifs du contrôleur (`ILogger<OxoController>`,
  upload/egress) et ceux de `GlobalExceptionHandler` (`ILogger<GlobalExceptionHandler>`,
  `LogWarning` sur chaque exception métier traduite) passent tous par ce même canal
  `ILogger<T>` → sink SQL Server `SystemLogs`, cohérent avec le reste de la solution. Les tests
  qui vérifient le contenu des logs le font via un `CapturingLogger` en mémoire (convention déjà
  établie ailleurs dans le projet), jamais en inspectant `SystemLogs` directement — cohérent avec
  la note déjà actée dans le ticket K2.
- **Point non couvert par un test, constaté par lecture du code seul** : le chemin « exception non
  typée » (dernier `_ => StatusCodes.Status500InternalServerError` dans
  `GlobalExceptionHandler.StatusCodeFor`, ou le chemin encore plus générique où
  `BusinessExceptionLocalizer.TryLocalize` retourne `null` et l'exception tombe hors de
  `GlobalExceptionHandler` entièrement) n'est logué qu'implicitement par le middleware
  `UseExceptionHandler()` par défaut d'ASP.NET Core — aucun test de ce projet ne vérifie qu'un tel
  cas atteint effectivement `SystemLogs` avec un niveau `Error` exploitable.

---

## Hors périmètre — observé en passant

- La règle de rejet de fichier (`ImportResult.Equipement is null`) est interprétée indépendamment
  dans `ImportProfileTest.razor`/`ExportProfileTest.razor` (BlazorAdmin) — constaté via
  `CLAUDE.md`, pas via lecture du code Blazor. Lié au point 4 ci-dessus.
- `legacy/ExcelProcessingClientService`/`.Tests` consomme ce contrat HTTP (Lot K3) — code non lu,
  signalé au § 6.
- La cohérence globale de `ApplicationMessages`/localisation vue depuis Application n'a pas été
  auditée au-delà des deux clés directement utilisées par `OxoController`.

---

## Non couvert / incertain

- Comportement exact d'un upload avec des octets non-Excel (constaté comme non testé, mais le
  code réel de `GlobalExceptionHandler`/`AddProblemDetails()` n'a pas été exécuté pour confirmer
  le code HTTP précis retourné en pratique — seule la lecture statique du switch a été faite).
- Si un `DomainRuleViolationException` (409) est réellement atteignable depuis
  `/api/oxo/process` en pratique aujourd'hui, ou si ce mapping existe uniquement pour d'autres
  routes/futurs cas d'usage partageant le même `GlobalExceptionHandler`.
- Comportement du `RequestSizeLimit`/`RequestFormLimits` (10 Mo, `UploadLimits`) au-delà de la
  limite — aucun test HTTP de ce projet n'envoie un fichier dépassant la limite pour vérifier le
  code de retour réel.
- Impact précis (bit de timing exploitable ou non) de la comparaison à temps non constant entre
  clés de longueurs différentes dans `ApiKeyAuthenticationHandler` — signalé au § 5 mais non
  creusé plus loin (nécessiterait une analyse de sécurité dédiée, hors du cadre de cet audit de
  qualité de code).

---

## Ce que cet audit ne déclenche pas

Aucun refacto listé ci-dessus n'est engagé. Ce document est destiné à être relu et priorisé par
Claude AI dans une autre session, puis validé explicitement par Simon avant tout ticket TDD.
