# Tickets TDD — Lot K : exposition Web API du pipeline OXO, migration et retrait du POC legacy

*Document vivant (pas de suffixe de date). Fait suite à la décision du 21/07 : ne **pas**
supprimer `Mappings.razor`/`ExtractionConfig*` tant que le pipeline OXO n'est pas le pipeline
réellement branché en production côté legacy → Web API (voir échange assistant du 21/07,
confirmé par `audit-coherence-globale-2026-07-19.md` §6 — POC toujours actif, pipeline OXO
toujours sans route HTTP). Toutes les décisions ouvertes de la version précédente de ce document
ont été tranchées le 21/07 — voir résumé ci-dessous, ne plus les rouvrir sans nouvelle demande
explicite.

**Dépend entièrement des Lots A→J**, tous terminés à ce jour (Domain/Application/Infrastructure
du pipeline d'import OXO, écriture du fichier cible, écrans Blazor de profil import/export). Ce
lot ne construit aucune nouvelle logique métier — il **expose** ce qui existe déjà en process
côté BlazorAdmin, au même endroit (Web API) que le POC.

**Décisions actées le 21/07** (ne plus re-trancher), **complétées le 21/07 (session suivante)
après vérification de code — voir les 3 amendements ci-dessous, intégrés directement dans K2/K3/K4** :
- **Sélection de profil côté M2M** : `ImportProfileId` et `ExportProfileId` sont des paramètres
  **requis, explicites** de la requête `POST /api/oxo/process`. Pas de profil implicite/unique
  déduit d'une configuration — l'appelant (legacy) doit toujours préciser les deux identifiants,
  y compris s'il n'existe qu'un seul profil de chaque à un instant donné.
- **Pas de feature flag, pas de bascule progressive** : `ExcelProcessingClientService` (legacy)
  bascule directement et entièrement vers `POST /api/oxo/process` avec les nouveaux paramètres,
  en un seul changement. Aucune configuration de routage parallèle à construire.
- **Nom de route définitif** : `POST /api/oxo/process`.
- **Logs upload/égress** : réutilisation de la table `SystemLogs` existante (sink Serilog
  `MSSqlServer` déjà en place côté WebAPI, voir audit du 17/07) — pas de nouveau mécanisme de
  persistance dédié.
- **Pas de cohabitation, aucune période de validation en production entre K3 et K4** : la
  migration du client legacy (K3) et le retrait du POC (K4) se font **dans le même cycle de
  livraison**, à la suite immédiate l'un de l'autre. Priorité explicite donnée à la rapidité de
  mise en œuvre et à la sobriété d'exécution (minimiser le travail/les tokens consommés par
  Claude Code pour livrer ce lot) — voir note d'efficacité en fin de document.

**Rappel de l'état de départ** (vérifié dans les audits du 17/07 et 19/07, à ne pas re-vérifier
avant K1) :
- `POST /api/excel/process` (`ExcelController`) reste le seul point d'entrée HTTP, câblé
  uniquement sur l'ancien pipeline (`IExcelExtractionService`/`ClosedXmlExtractionService`,
  `IExcelGeneratorService`/`ClosedXmlGeneratorService`), protégé par `ApiKeyAuthenticationHandler`.
- Le pipeline OXO (`ImportPipelineOrchestrator`, `SheetGenerationEngine` + `ClosedXmlWorkbookWriter`)
  n'est exposé **que** en process depuis `ImportProfileTest.razor`/`ExportProfileTest.razor` dans
  BlazorAdmin — aucune route Web API, aucune sécurité M2M pour ce chemin.
- Logs applicatifs : présents et conformes côté import OXO (`ImportPipelineOrchestrator` + 5
  services par feuille, `ILogger<T>`, voir Lot G) ; **statut non vérifié côté génération**
  (`SheetGenerationEngine`/`ClosedXmlWorkbookWriter`) — à confirmer en K0 avant d'exposer quoi que
  ce soit en production, pas supposé absent ou présent sans lecture du code.
- Legacy (`legacy/ExcelProcessingClientService`) appelle aujourd'hui exclusivement
  `POST /api/excel/process` avec un `ExtractionConfigId`.

**Conventions générales** (voir `etat-des-lieux-technique.md`) : xUnit 2.9.3 + FluentAssertions
7.0.0 + Moq, `WebApplicationFactory` pour les tests d'intégration Web API, ClosedXML uniquement,
aucune dépendance commerciale.

**Hors périmètre explicite** : toute nouvelle règle métier d'extraction/génération (Lots A→I,
terminés) ; format exact du fichier `OXO_TRAME_IMPORT_MAD.xlsx` (non figé côté client, voir Lot I
et `tickets-tdd-ecriture-fichier-cible.md`) ; feuille Tâches Multiples (toujours non couverte) ;
persistance dédiée des logs (`G3`, jamais tranchée, reste hors périmètre) ; tout mécanisme de
feature flag ou de bascule progressive (explicitement écarté ci-dessus).

---

## K0. Audit préalable — logs du pipeline de génération (`SheetGenerationEngine`/`ClosedXmlWorkbookWriter`)

**But** : ne pas découvrir en production que le chemin export du nouveau pipeline n'a aucune
observabilité, alors que le POC en a (`ExtractionHistory` + `ILogger` sur `ProcessExcelFileService`).

**Tâche** (audit de code, pas de nouveau test à ce stade — délibérément gardé léger pour ne pas
consommer de travail inutile avant K1) :
- Recherche exhaustive de `ILogger` dans `src/ExcelETL.Application/Generation/` et
  `src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookWriter.cs`.
- Si absent : ouvrir K0bis (symétrique du Lot G, appliqué à `SheetGenerationEngine`/
  `ClosedXmlWorkbookWriter`) avant de poursuivre K1 — mêmes conventions que G1/G2 (`ILogger<T>`
  injecté, pas de port Application dédié, `NullLogger<T>.Instance` dans les tests, log
  démarrage/succès/échec avec durée écoulée). Rester au plus proche du pattern G1/G2 existant,
  ne pas en profiter pour reconcevoir la convention de logging.
- Si présent et conforme : documenter la preuve (fichier + ligne) dans ce document, ne pas
  rouvrir.

**Condition de sortie de K0** : ce point doit être tranché (fait ou explicitement différé avec
justification) avant K1, pas découvert après coup une fois la route exposée.

**K0 — résultat (21/07)** : recherche exhaustive confirmée, `ILogger` absent des deux fichiers.
**K0bis réalisé** : `ILogger<SheetGenerationEngine>`/`ILogger<ClosedXmlWorkbookWriter>` injectés
via primary constructor, log démarrage/succès (durée écoulée + compteurs) et log d'erreur avec
`throw` — mêmes conventions que `ImportPipelineOrchestrator` (`src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs`,
Lot G1/G2). Voir `src/ExcelETL.Application/Generation/SheetGenerationEngine.cs` et
`src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookWriter.cs`. Tests mis à jour avec
`NullLogger<T>.Instance` (`SheetGenerationEngineTests`, `ClosedXmlWorkbookWriterTests`,
`GenerationPipelineIntegrationTests`) — aucune assertion sur le logging lui-même, conforme à la
convention G1/G2. Suite complète vérifiée verte : Application.Tests 6/6 (filtre `Generation`),
Infrastructure.Tests 135/135 (filtre `Excel`), BlazorAdmin.Tests 4/4 (filtre `ExportProfileTest`,
DI inchangée dans `Program.cs`). **K0 clos, ne pas rouvrir.**

---

## K1. Nouvelle route Web API — pipeline OXO (import + génération)

**Route** : `POST /api/oxo/process` (définitif, confirmé le 21/07).

**Comportement attendu**, miroir fonctionnel de `ExcelController`/`ProcessExcelFileService` mais
sur le pipeline OXO :
1. Réception du fichier source en flux HTTP (même contrainte legacy : synchrone, pas de
   streaming asynchrone côté appelant).
2. `ImportProfileId` et `ExportProfileId` : **paramètres requis** de la requête (query string ou
   corps multipart selon convention déjà en place pour `POST /api/excel/process` — à aligner sur
   l'existant plutôt que réinventer un format). Résolution via `IImportProfileStore.GetByIdAsync`/
   `IExportProfileStore.GetByIdAsync` ; identifiant inconnu → réponse d'erreur explicite (voir
   tests), jamais d'exception non gérée.
3. `ClosedXmlWorkbookReader` → `ImportPipelineOrchestrator.Run(...)` → si `Equipement is null`,
   réponse HTTP d'erreur explicite (code + détail des `ExtractionError` bloquantes), **pas de
   génération tentée** — même garde que celle déjà en place dans `ExportProfileTest.razor` (Lot
   J3, "File rejected").
4. Sinon, `SheetGenerationEngine` + `ClosedXmlWorkbookWriter` → fichier binaire généré, retourné
   synchrone dans le corps de la réponse (même contrat qu'aujourd'hui côté legacy : pas de
   changement du contrat HTTP externe au-delà des deux nouveaux paramètres de profil).
5. Sécurité : même `ApiKeyAuthenticationHandler` que la route existante, réutilisé tel quel — pas
   de nouveau mécanisme d'authentification.

**Tests** (xUnit + `WebApplicationFactory`, miroir des tests existants sur `ExcelController` si
présents — vérifier convention avant d'écrire, ne pas supposer) :
- Round-trip complet contre une des 3 fixtures réelles (ex. C7401) : requête avec fichier +
  `ImportProfileId`/`ExportProfileId` valides → réponse 200, fichier binaire non vide, feuilles
  attendues présentes (mêmes assertions que `GenerationPipelineIntegrationTests.cs`, Lot I5,
  reflétées ici au niveau HTTP).
- Fichier rejeté (`Equipement is null`, classeur synthétique invalide) → réponse d'erreur
  explicite (code HTTP à définir, ex. 422), **aucun fichier généré dans le corps de la réponse**.
- Cas D8570/`"VANNE"` : avertissement non bloquant → réponse 200 quand même, fichier généré
  contient la ligne `Enfants` correspondante (même exigence qu'I5/J3, vérifiée maintenant aussi au
  niveau HTTP).
- Absence/invalidité de la clé API → 401/403, même comportement que la route existante (test par
  réutilisation du handler, pas de nouvelle logique à tester si le handler est bien partagé).
- `ImportProfileId` ou `ExportProfileId` inconnu (l'un ou l'autre, tester les deux cas
  indépendamment) → réponse d'erreur explicite, pas d'exception non gérée remontant en 500.

**Dossier** : `src/ExcelETL.WebAPI/Controllers/OxoController.cs` (nom à confirmer au moment de
l'implémentation, pas bloquant) + service d'orchestration HTTP dédié dans `ExcelETL.Application`
si la logique de résolution des profils + appel orchestrateur + génération dépasse ce qu'un
controller devrait porter directement (cohérent avec la séparation déjà en place : `ExcelController`
délègue à `ProcessExcelFileService`, ne pas dupliquer cette logique dans le controller ici non plus).

**K1 — réalisé (21/07)** :
- `ProcessOxoFileService` (`src/ExcelETL.Application/Extraction/Oxo/`) est le service d'orchestration
  HTTP évoqué ci-dessus : résout les 2 profils (404 explicite via `ImportProfileNotFoundException`/
  `ExportProfileNotFoundException`, nouveaux types + `ApplicationErrorCode` + entrées EN/FR dans
  `ApplicationMessages.resx`/`.fr.resx`, mappés dans `GlobalExceptionHandler`), lance
  `ImportPipelineOrchestrator.Run`, distingue le rejet (`Equipement is null`) du succès, génère +
  archive (réutilise `IFileStorageService`, déjà branché — la contrainte "persistance sur disque"
  du haut de `CLAUDE.md` s'applique à ce pipeline aussi, pas seulement à l'ancien). `ProcessOxoFileCommand`
  transporte un `IWorkbookReader` déjà construit (pas un `Stream` brut) : construire un
  `ClosedXmlWorkbookReader` nécessite Infrastructure, qu'Application ne peut pas référencer — c'est
  le contrôleur (host, référence déjà Infrastructure) qui le construit, exactement comme le fait déjà
  chaque page Blazor OXO.
- **Déplacement architectural mineur** : `TargetWorkbookFileNameBuilder` (pure logique de chaîne, zéro
  dépendance ClosedXML) vivait dans `ExcelETL.Infrastructure.Excel` alors qu'`Application` en a besoin
  pour nommer le fichier archivé/généré — déplacé vers `ExcelETL.Application.Generation` (et son test
  associé vers `ExcelETL.Application.Tests`). Aucun appelant cassé (`ExportProfileTest.razor` importait
  déjà les deux namespaces).
- Rejet : `422 Unprocessable Entity`, `ProblemDetails.Detail` localisé (`OxoFileRejected`) +
  `Extensions["errors"]` (liste `Sheet`/`BlockIdentifier`/`Code`/`Message`).
- **Bug réel découvert et corrigé pendant l'écriture des tests HTTP** (`ClosedXmlWorkbookReader.ReadCellValue`) :
  `GetString()` de ClosedXML formate une cellule de type date selon `CultureInfo.CurrentCulture` ;
  sous le host Web API, `RequestLocalizationOptions` fixe `en-US` par défaut par requête, ce qui
  produit `"9/11/2025 12:00:00 AM"` au lieu du format `dd/MM/yyyy HH:mm:ss` que
  `ProcedureExtractionService.TryParseDate` attend — **chaque fixture réelle était rejetée à 100%**
  dès qu'appelée derrière une requête HTTP négociant la culture (jamais démasqué avant : aucun host
  antérieur ne faisait tourner ce pipeline sous négociation de culture ASP.NET Core). Corrigé en
  détectant `cell.DataType == XLDataType.DateTime` et en formatant nous-mêmes en
  `CultureInfo.InvariantCulture` plutôt que de faire confiance à `GetString()`. Aucune régression :
  suite complète revérifiée verte (Domain 255, Application 100, Infrastructure 131, WebAPI 21,
  BlazorAdmin 112).
- Tests : `tests/ExcelETL.WebAPI.Tests/Oxo/OxoProcessEndpointTests.cs` (7 cas : sans clé API,
  `ImportProfileId`/`ExportProfileId` inconnus indépendamment, fichier rejeté, round-trip C7401 réel,
  cas D8570/`"VANNE"`, logs upload/egress) + `ProcessOxoFileServiceTests.cs` (Application.Tests, 5 cas,
  `Mock` sur toutes les dépendances).

**K2 — réalisé (21/07, même commit que K1)** :
- DI (`ExcelETL.WebAPI/Program.cs`) : les 9 services OXO déjà utilisés par BlazorAdmin + `IProcessOxoFileService`
  enregistrés avec les mêmes durées de vie (`Scoped` pour les 2 profile stores, `Singleton` pour le
  reste). `IWorkbookReader` **n'est pas** enregistré en DI — ni ici ni dans BlazorAdmin, il est
  construit directement depuis le flux uploadé par le host (contrôleur ou composant Blazor).
- Logs upload/egress ajoutés dans `OxoController` (nom fichier, taille, IP source pour l'upload ; nom
  fichier généré + code HTTP pour l'égress) en plus des logs métier déjà posés dans
  `ProcessOxoFileService` (Lot K0bis' convention). **Écart documenté avec le texte du ticket** :
  `ProcessExcelFileService` (l'ancien pipeline) ne logue en réalité ni taille de fichier, ni IP, ni
  hash — vérifié dans le code, contrairement à la description du ticket. Le hash de fichier n'a été
  reproduit nulle part (aucun précédent dans ce dépôt) ; les autres champs (taille, IP, code réponse)
  ont été ajoutés.
- Test de logging : `Process_WithValidRequest_LogsUploadAndEgress` réutilise `CapturingLogger<T>`
  (dupliqué dans `tests/ExcelETL.WebAPI.Tests/Oxo/CapturingLogger.cs`, même convention que
  `tests/ExcelETL.Infrastructure.Tests/Excel/CapturingLogger.cs`, Lot G2) via une substitution DI de
  `ILogger<OxoController>` sur une factory dédiée à ce test.
- Pas de test DI dédié (registration-only) : même précédent que Lot J4 (BlazorAdmin), vérifié par
  lecture de `Program.cs` plutôt que par un test dédié — aucun test de ce type n'existe ailleurs
  dans ce dépôt.

---

## K2. Câblage DI et logs — route Web API OXO

**Tâches** :
- Enregistrer les services du pipeline OXO déjà utilisés par BlazorAdmin (`IWorkbookReader`,
  `ImportPipelineOrchestrator`, les 5 services d'extraction, `ISheetGenerationEngine`,
  `IWorkbookWriter`, `IImportProfileStore`, `IExportProfileStore`) dans
  `src/ExcelETL.WebAPI/Program.cs` — actuellement absents de ce host (confirmé par recherche
  exhaustive dans l'audit du 19/07, seul `ExcelETL.BlazorAdmin/Program.cs` les enregistre).
- Ajouter, au niveau du nouveau controller/service HTTP, les logs équivalents à ceux déjà présents
  côté legacy (`ProcessExcelFileService` : upload — taille, timestamp, IP source via clé API ;
  égress — hash fichier, code réponse, timestamp), **persistés dans `SystemLogs` via le sink
  Serilog `MSSqlServer` déjà configuré** (`Program.cs:48-62` de `ExcelETL.WebAPI`, voir audit du
  17/07) — même mécanisme que le reste du Web API, pas de nouvelle table ni de nouveau sink.

**Tests** :
- Test DI léger (ou lecture de `Program.cs`, selon convention déjà en place pour BlazorAdmin —
  voir Lot J4) confirmant l'enregistrement des services listés ci-dessus dans `ExcelETL.WebAPI`.
- **Amendement (21/07, session suivante)** : recherche exhaustive confirmée — aucun sink de test
  Serilog inspectable (`InMemorySink`, `TestCorrelator`, etc.) n'existe dans ce dépôt, et tous les
  hosts de test désactivent délibérément le vrai sink `MSSqlServer`
  (`Serilog:EnableMsSqlServerSink=false`) pour ne jamais dépendre d'un SQL Server atteignable
  (voir `HealthPingTests`/`ApiKeyAuthenticationTests`/`ExcelProcessEndpointTests`). Le test
  d'intégration vérifiant qu'un appel réussi à la route K1 produit une entrée de log upload +
  une entrée de log égress se fait donc **à la frontière `ILogger<T>`, pas au niveau des lignes
  réelles de `SystemLogs`** : réutiliser `CapturingLogger<T>` (`tests/ExcelETL.Infrastructure.Tests/Excel/CapturingLogger.cs`,
  introduit au Lot G2, même pattern que `ImportPipelineOrchestratorLoggingIntegrationTests`) pour
  capturer les appels de log du nouveau controller/service HTTP et asserter sur leurs champs
  structurés (taille fichier, timestamp, IP source, hash fichier, code réponse). Ne pas introduire
  de nouvelle convention de test Serilog pour ce lot.

**Dossier** : `src/ExcelETL.WebAPI/Program.cs`.

---

## K3. Migration de `ExcelProcessingClientService` (legacy) vers la nouvelle route — bascule directe, sans feature flag

**Amendement (21/07, session suivante) — périmètre réel confirmé** : `docs/etat-des-lieux-technique.md`
indique explicitement que `legacy/ExcelProcessingClientService` (+ `.Tests`) "ne fait pas partie
de la solution .NET 10" — c'est une lib de référence/style pour le code HTTP M2M, pas
l'application ASP.NET MVC 5 réelle. Le contrôleur MVC qui appelle
`ExcelProcessingClientService.ProcessAsync(extractionConfigId, file)` en production et décide de
la valeur à passer **vit en dehors de ce dépôt**. En conséquence, **K3 se limite strictement à**
`legacy/ExcelProcessingClientService`/`legacy/ExcelProcessingClientService.Tests` dans ce dépôt :
signature de `ProcessAsync` (nouveaux paramètres `importProfileId`/`exportProfileId`), corps de
requête, tests colocalisés. La mise à jour du vrai site d'appel MVC 5 en production reste
**explicitement hors périmètre** de cette session/ce dépôt — c'est un suivi manuel du porteur du
projet dans l'autre dépôt, pas une tâche de ce ticket.

**Contrainte rappelée par le contexte du projet** : aucune modification de logique métier côté
legacy — ce ticket touche uniquement l'appel HTTP sortant (URL, corps de requête), pas le
comportement fonctionnel de l'application legacy.

**Comportement attendu** :
- `ExcelProcessingClientService` appelle `POST /api/oxo/process` au lieu de
  `POST /api/excel/process`. **Précision (21/07)** : `ProcessAsync` prend aujourd'hui
  `extractionConfigId` en paramètre (pas lu depuis une config interne à la lib — confirmé par
  lecture du code) ; le même principe s'applique, `ProcessAsync` gagne deux paramètres
  `Guid importProfileId`/`Guid exportProfileId` en plus de `file`, mirroring exact du paramètre
  existant. D'où ces deux valeurs proviennent réellement côté appelant (config, valeur fixe,
  etc.) est décidé par le vrai site d'appel MVC 5 — hors périmètre de ce dépôt, voir amendement
  ci-dessus.
- **Bascule directe, en un seul changement** : pas de feature flag, pas de configuration de
  routage parallèle vers l'ancienne route. Le code appelant l'ancienne route
  (`ExtractionConfigId`) est remplacé, pas dupliqué à côté.

**Tests** (côté legacy, `ASP.NET MVC 5`/.NET Framework 4.8 — conventions de test existantes du
projet legacy à respecter, hors périmètre des conventions xUnit/FluentAssertions ci-dessus) :
- `ExcelProcessingClientService` appelle bien la nouvelle URL avec le nouveau contrat de requête
  (`ImportProfileId`/`ExportProfileId`), test au niveau du client HTTP, mock du endpoint.
- Comportement de fallback/erreur inchangé du point de vue de l'application legacy (le
  changement de pipeline côté serveur ne doit rien changer à la gestion d'erreur déjà en place
  côté legacy).

**Dossier** : côté `legacy/` (chemin exact à confirmer, hors du dépôt `ExcelETL.*` audité jusqu'ici).

**K3 — réalisé (21/07)** : `ExcelProcessingClientService.ProcessAsync` prend désormais
`(Guid importProfileId, Guid exportProfileId, HttpPostedFileBase file)` au lieu de
`(Guid extractionConfigId, HttpPostedFileBase file)`, poste sur `api/oxo/process` (constante
`ProcessRelativeUrl` mise à jour), avec 2 champs multipart (`ImportProfileId`/`ExportProfileId`) au
lieu d'un seul (`ExtractionConfigId`) — bascule directe, aucune logique de fallback/routage
parallèle. Tests mis à jour à l'identique (mêmes 15 cas, seuls le contrat de requête et l'URL
attendue changent) : `Legacy.ExcelProcessingClientService.Tests` 15/15 vert
(`dotnet test legacy/ExcelProcessingClientService.Tests/Legacy.ExcelProcessingClientService.Tests.csproj`).
Conforme à l'amendement de périmètre ci-dessus : seul `legacy/ExcelProcessingClientService*` a été
touché, le vrai site d'appel MVC 5 en production reste hors dépôt/hors périmètre.

---

## K4. Retrait du POC (`Mappings.razor`, `UploadTest.razor`, `ExtractionConfig*`, ancien pipeline Web API)

**Enchaîné immédiatement après K3, dans le même cycle de livraison — pas de période de
validation en production entre les deux** (décision actée le 21/07 : priorité à la rapidité et à
la sobriété d'exécution plutôt qu'à une bascule prudente par étapes).

**Amendement (21/07, session suivante) — périmètre étendu à `ExtractionHistory` et ses 3 lecteurs** :
vérification de code : `ExtractionHistory` n'est **écrite que par `ProcessExcelFileService`**
(donc 100% données POC), mais elle est **lue** par `Dashboard.razor`, `History.razor` et le
endpoint de téléchargement d'historique (`AdminEndpointRouteBuilderExtensions.cs`) — aucun des
trois n'était dans le périmètre de suppression initial, alors que `Dashboard.razor` est la page
d'accueil de l'app (`@page "/"`). Décision : **étendre K4** pour tout supprimer plutôt que
laisser ces écrans figés à afficher indéfiniment de vieilles données POC après la bascule
(cohérent avec la décision G3 de ne pas doter le pipeline OXO d'une persistance de log dédiée, et
avec la priorité « sobriété d'exécution » du présent document).

**Périmètre de suppression** :
- `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/Mappings.razor` et `UploadTest.razor` (+ tests
  bUnit associés).
- **(Amendement 21/07)** `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/Dashboard.razor` et
  `History.razor` (+ tests bUnit associés) ; `src/ExcelETL.BlazorAdmin/Components/Admin/AdminEndpointRouteBuilderExtensions.cs`
  (endpoint de téléchargement d'historique).
- `src/ExcelETL.Domain/Entities/ExtractionConfig.cs`, `SheetConfig.cs`, `CellMapping.cs`.
- **(Amendement 21/07)** `src/ExcelETL.Domain/Entities/ExtractionHistory.cs`,
  `ExtractionHistoryStatistics`, `ExtractionHistoryNotFoundException`.
- `IExtractionConfigRepository`/`ExtractionConfigRepository` + configurations EF Core associées
  (`SheetConfigConfiguration`, `CellMappingConfiguration`).
- **(Amendement 21/07)** `IExtractionHistoryRepository`/`ExtractionHistoryRepository` +
  `ExtractionHistoryConfiguration`.
- `src/ExcelETL.WebAPI/Controllers/ExcelController.cs`, `ProcessExcelFileService`,
  `ClosedXmlExtractionService`, `ClosedXmlGeneratorService` (ancien générateur, distinct du writer
  OXO de Lot I4 — vérifier qu'aucune autre classe n'en dépend encore avant suppression, devrait
  être trivial puisque K3 vient de retirer le seul appelant HTTP).
- Route `POST /api/excel/process` retirée du Web API.
- Migration EF Core de suppression des tables `ExtractionConfig*` (`SheetConfig`, `CellMapping`)
  **et `ExtractionHistory`** (tranché — plus de condition « si elle n'est pas réutilisée
  ailleurs », voir amendement ci-dessus).
- **(Amendement 21/07) Route de repli pour `/`** : `Dashboard.razor` porte `@page "/"` (landing
  page de l'app). Ajouter `@page "/"` à `ImportProfiles.razor` en plus de sa route existante
  `/import-profiles`, pour qu'un utilisateur atterrissant sur `/` après connexion tombe sur
  l'écran des profils d'import plutôt qu'un 404 — sauf préférence contraire exprimée avant
  l'implémentation de K4.

**Tests** :
- Recherche exhaustive post-suppression (`ExtractionConfig`, `ExtractionHistory`,
  `Mappings.razor`, `Dashboard.razor`, `History.razor`, `IExtractionConfigRepository`,
  `IExtractionHistoryRepository`) confirmant zéro référence restante dans `src/` et `tests/`.
- Suite de tests complète (BlazorAdmin + WebAPI + Infrastructure) toujours au vert après
  suppression — aucune régression sur le pipeline OXO qui reste, lui, inchangé par ce ticket.
- Nav-menu BlazorAdmin : liens vers `Mappings.razor`, `Dashboard.razor` (« Tableau de bord ») et
  `History.razor` (« Historique d'extraction ») retirés, pas seulement les pages (même règle de
  maintenance déjà appliquée pour les paires `.razor`/`.razor.css` orphelines, voir `CLAUDE.md`).
- Vérifier qu'un utilisateur connecté atterrissant sur `/` obtient bien `ImportProfiles.razor`,
  pas un 404 (voir route de repli ci-dessus).

**Dossier** : suppression répartie sur `ExcelETL.Domain`, `ExcelETL.Application`,
`ExcelETL.Infrastructure`, `ExcelETL.WebAPI`, `ExcelETL.BlazorAdmin` — un seul cycle de revue,
mais un commit par projet reste recommandé pour faciliter la lecture du diff (pas pour ralentir
la livraison, juste pour la lisibilité).

---

## Note d'efficacité d'implémentation (Claude Code)

Priorité explicite donnée par le porteur du projet à la rapidité et à la sobriété d'exécution.
Recommandations pour limiter le travail (et les tokens) sans sacrifier le TDD :
- **K0 doit rester un audit court** : une recherche `grep`/lecture ciblée, pas une relecture
  intégrale des dossiers `Generation/`. S'arrêter dès que la présence/absence de `ILogger` est
  confirmée.
- **K1 et K2 peuvent être livrés dans le même commit/PR** : les deux touchent
  `ExcelETL.WebAPI`, K2 dépend directement de K1, inutile de les séparer en deux cycles de revue.
- **K3 et K4 peuvent être livrés dans le même cycle de livraison**, comme décidé — mais rester
  deux commits/PR distincts (un côté legacy `.NET Framework`, un côté `ExcelETL.*`) plutôt qu'un
  seul, pour ne pas mélanger deux bases de code et deux conventions de test différentes dans une
  même revue.
- **Ne pas réouvrir de décision déjà actée** (voir section "Décisions actées le 21/07") — si
  Claude Code identifie un doute résiduel pendant l'implémentation, le signaler explicitement
  plutôt que de re-proposer plusieurs options déjà tranchées.
- Aucune section "Non couvert" ne subsiste dans ce document — toutes les décisions nécessaires
  au démarrage de K0 ont été prises le 21/07.

## Ordre recommandé

1. **K0** (audit préalable des logs génération — bloquant avant toute exposition HTTP, à garder
   court)
2. **K1 + K2** (route Web API OXO + DI/logs — même PR)
3. **K3 + K4** (migration legacy + retrait du POC — même cycle de livraison, deux PR distincts)
