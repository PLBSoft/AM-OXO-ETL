# État d'avancement — Lot G, logging pipeline OXO (2026-07-19)

*Instantané daté, produit par lecture directe du code sur `main`, commit de référence
`d7b9cf2` (2026-07-19 10:03:59 +0200). Le travail de logging lui-même a été livré par les
commits `54c6c53` (Lot G1, 2026-07-18 10:45:36) et `b549ae5` (Lot G2, 2026-07-18 10:53:32),
tous deux antérieurs et inchangés depuis (`git log --oneline -- <fichiers touchés>` ne montre
aucun commit postérieur sur ces fichiers). Aucun commit G3 n'existe dans l'historique.*

---

## 1. Statut ticket par ticket

### G3 — Persistance/sink des logs : **non fait, aucune décision actée dans le code**

Recherche exhaustive : aucune référence à `SystemLogs`, `ExtractionHistory` ou `Serilog` dans
`src/ExcelETL.Application/Extraction/Oxo/` ni `src/ExcelETL.Infrastructure/Excel/`. Aucune
migration EF postérieure à `20260718092214_AddExportProfile` (Lot I6) — la dernière migration
du dépôt reste celle-là ; rien de type `AddOxoExecutionLog`/`AddImportRunHistory` n'existe.

Le pipeline OXO reste donc, comme avant le Lot G, **sans aucune persistance de log** —
seule l'instrumentation `ILogger` (G1/G2, voir ci-dessous) existe, et elle transite par les
sinks déjà configurés au niveau de l'hôte (Serilog `Console` + `MSSqlServer`/`SystemLogs`)
**uniquement si l'hôte appelant est le WebAPI ou BlazorAdmin** — ce qui n'est toujours pas le
cas aujourd'hui : le seul consommateur du pipeline OXO reste `ImportProfileTest.razor` (Lot F2),
qui tourne dans BlazorAdmin et hérite donc de facto de la config Serilog déjà en place côté
BlazorAdmin ([CLAUDE.md, section "Web API surface"] — "BlazorAdmin wires the identical
configuration ... against the same `SystemLogs` table"). Mais ceci n'est **pas** un choix G3 :
c'est une conséquence mécanique du fait que `ILogger<T>` de n'importe quel service DI résolu par
BlazorAdmin passe par le pipeline Serilog déjà branché pour tout l'hôte — aucun code Lot G ne
route explicitement vers `SystemLogs`, aucune option `ExtractionHistory`-like n'a été évaluée ni
écartée dans le code.

**Conclusion** : G3 n'a pas été implémenté et aucune décision explicite (réutiliser
`SystemLogs` vs. persistance dédiée façon `ExtractionHistory`) n'a été actée — ni dans le code,
ni dans un commit, ni dans `CLAUDE.md`. Le seul document qui aborde le sujet est l'audit du
17/07 (`docs/audit-coherence-globale-2026-07-17.md`, §"Écarts à corriger bientôt", point 3), qui
liste le manque comme "pas urgent tant que le seul consommateur est la page de test Blazor en
mémoire" — ce constat reste valable telle quelle aujourd'hui, rien n'a changé sur ce point depuis
le 17/07.

### G1 — Instrumentation `ILogger` : **fait**

Abstraction utilisée : **`ILogger<T>` de `Microsoft.Extensions.Logging` injecté directement**
dans chaque classe concernée — pas de port `Application`-owned dédié (l'alternative envisagée
par le ticket n'a pas été retenue). C'est cohérent avec le seul précédent déjà présent dans le
repo (`ProcessExcelFileService`, POC), qui fait de même.

Confirmé par lecture directe :

| Classe | Preuve |
|---|---|
| `ImportPipelineOrchestrator` | [`ImportPipelineOrchestrator.cs:29`](../src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs#L29) — `ILogger<ImportPipelineOrchestrator> logger` en paramètre de constructeur primaire |
| `ProcedureExtractionService` | [`ProcedureExtractionService.cs:22`](../src/ExcelETL.Application/Extraction/Oxo/Procedure/ProcedureExtractionService.cs#L22) — `ILogger<ProcedureExtractionService> logger` |
| `IsolementExtractionService` | [`IsolementExtractionService.cs:23`](../src/ExcelETL.Application/Extraction/Oxo/Isolement/IsolementExtractionService.cs#L23) — `ILogger<IsolementExtractionService> logger` |
| `UnconditionalIsolementSheetExtractionService` (PLATINES + ORIFICES CAPACITES) | [`UnconditionalIsolementSheetExtractionService.cs:20`](../src/ExcelETL.Application/Extraction/Oxo/UnconditionalIsolementSheetExtractionService.cs#L20) — `ILogger<UnconditionalIsolementSheetExtractionService> logger` |
| `AutresJointsTouchesExtractionService` | [`AutresJointsTouchesExtractionService.cs:23`](../src/ExcelETL.Application/Extraction/Oxo/AutresJointsTouches/AutresJointsTouchesExtractionService.cs#L23) — `ILogger<AutresJointsTouchesExtractionService> logger` |
| `DiversExtractionService` | [`DiversExtractionService.cs:25`](../src/ExcelETL.Application/Extraction/Oxo/Divers/DiversExtractionService.cs#L25) — `ILogger<DiversExtractionService> logger` |

Soit l'orchestrateur + les 5 services par feuille distincts (PLATINES et ORIFICES CAPACITES
partagent une seule classe, `UnconditionalIsolementSheetExtractionService`, déjà le cas depuis
le Lot C3/C4 — donc 6 appels de service pour 5 classes distinctes, cohérent avec "les 6 services
d'extraction par feuille" du ticket).

**Mapping erreur → niveau de log**, factorisé dans un helper statique unique
([`ExtractionErrorLogging.cs:11-21`](../src/ExcelETL.Application/Extraction/Oxo/ExtractionErrorLogging.cs#L11-L21)),
appelé par les 5 services (pas par l'orchestrateur, qui ne construit pas d'`ExtractionError`
lui-même) :
- `ExtractionErrorCode.UnrecognizedTypeElement` → `LogLevel.Warning` (non bloquant, §3.2 du
  modèle de domaine)
- `RequiredFieldMissing` / `UnparsableValue` → `LogLevel.Error` (bloc, ou pour PROCEDURE fichier
  entier, rejeté)

Points d'appel confirmés : `AutresJointsTouchesExtractionService.cs:37,70`,
`UnconditionalIsolementSheetExtractionService.cs:34`, `ProcedureExtractionService.cs:171`,
`IsolementExtractionService.cs:88,112`, `DiversExtractionService.cs:40,69`.

L'orchestrateur log lui-même 4 événements propres (démarrage, succès, rejet fichier entier,
échec inattendu) — voir §G2 ci-dessous pour leur contenu exact.

**Convention de test** : `NullLogger<T>.Instance` partout (pas de `Mock<ILogger<T>>` +
`Verify`), confirmé cohérent avec le reste du dépôt — aucune occurrence de
`Mock<ILogger` nulle part dans les tests avant ce lot. Le message de commit `54c6c53` note
explicitement que le ticket affirmait à tort qu'une convention Moq était déjà en place ; ce
point a été vérifié par grep exhaustif avant d'écrire le premier test, et la convention
existante (`NullLogger`) a été suivie sans introduire de nouvelle convention.

### G2 — Contenu structuré des logs : **fait**

Contenu confirmé par lecture directe de [`ImportPipelineOrchestrator.cs:45-123`](../src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs#L45-L123) :

- **Démarrage** (`LogInformation`, ligne 50) : nom du profil.
- **Rejet fichier entier** (`LogWarning`, lignes 60-63, quand `procedureResult.Equipement is
  null`) : nom du profil, **durée écoulée** (`stopwatch.ElapsedMilliseconds`), nombre d'erreurs
  bloquantes.
- **Succès** (`LogInformation`, lignes 107-112) : nom du profil, **durée écoulée**, **nombre de
  feuilles traitées** (`SheetsProcessedOnSuccess = 6`, constante littérale — voir "écarts"
  ci-dessous), **nombre total d'éléments extraits** (`isolements.Count + points.Count +
  procedureResult.TachesMultiples.Count`, ligne 105), plus le détail par catégorie
  (isolements/points/tâches multiples) et le nombre d'avertissements non bloquants.
- **Échec inattendu** (`LogError`, lignes 118-120, `catch (Exception ex)` englobant tout le
  corps après les gardes `ArgumentNullException`) : nom du profil, durée écoulée, exception.

Durée mesurée via `System.Diagnostics.Stopwatch`, démarré ligne 51 juste après le log de
démarrage — présent dans les 3 chemins de sortie (rejet/succès/échec).

**Coordonnée de cellule fusionnée en échec — pas d'enrichissement de `ExtractionError`** :
confirmé par lecture directe de [`ExtractionError.cs:9-42`](../src/ExcelETL.Domain/Extraction/Pivot/ExtractionError.cs#L9-L42) —
toujours exactement 4 champs (`Sheet`, `BlockIdentifier`, `Code`, `Message`), signature de
constructeur inchangée depuis avant le Lot G. `ExtractionErrorLogging.Log` (ligne 18) loggue
`BlockIdentifier` tel quel, sans transformation.

**Décision explicitement actée (pas seulement déduite du code)** : le message de commit
`b549ae5` documente qu'une re-discussion de ce point ("G2bis" — enrichir `ExtractionError` pour
porter une vraie coordonnée de cellule) a été proposée à l'utilisateur et **déclinée** — la
décision de conception déjà prise au niveau du modèle de domaine (`BlockIdentifier` généralisé
en "repère ou n° de ligne", pas une coordonnée de cellule garantie) a été confirmée comme
définitive. `CLAUDE.md` documente ce point (section "Lot G2") avec le même contenu. Le
`BlockIdentifier` réel varie selon l'appelant :
- `ProcedureExtractionService` (rejet fichier entier) : une vraie plage de cellules (ex.
  `"M2:O2"`)
- `RepeatingBlockReader` (générique) : un simple numéro de ligne
- `IsolementExtractionService` / `AutresJointsTouchesExtractionService` /
  `DiversExtractionService` : le repère métier de l'Isolement (ex. `"D8570-V4"`)

---

## 2. Points de vérification prioritaires

### Test contre les 3 fixtures réelles capturant les logs eux-mêmes (pas seulement `ImportResult.Errors`)

**Confirmé, avec un double `ILogger` de capture dédié** :
[`CapturingLogger.cs`](../tests/ExcelETL.Infrastructure.Tests/Excel/CapturingLogger.cs) — un
`ILogger<T>` fait main qui enregistre `(LogLevel, message formaté, Exception?)` dans un sink
partagé (`CapturedLogEntries`), explicitement choisi plutôt qu'un `Mock<ILogger<T>>` + `Verify`
(voir commentaire lignes 5-10 du fichier) pour capturer la sortie réelle formatée d'une
exécution complète, pas juste vérifier qu'un appel a eu lieu.

Utilisé dans
[`ImportPipelineOrchestratorLoggingIntegrationTests.cs`](../tests/ExcelETL.Infrastructure.Tests/Excel/ImportPipelineOrchestratorLoggingIntegrationTests.cs) —
seul le test G6306B (durée/nombre de feuilles/nombre d'éléments, lignes 53-66) et le test D8570
(avertissement `"VANNE"`, lignes 68-81) instrumentent un `CapturingLogger` réel ; C7401 n'a pas
de test de log dédié dans cette classe (les 3 fixtures restent couvertes côté
`ImportResult`/isolements par les tests d'intégration préexistants du Lot D2,
`ImportPipelineOrchestratorIntegrationTests.cs`, qui eux n'inspectent pas les logs).

### Cas D8570/`"VANNE"` — confirmé `Warning`, pas `Error`

Confirmé directement, [`ImportPipelineOrchestratorLoggingIntegrationTests.cs:69-81`](../tests/ExcelETL.Infrastructure.Tests/Excel/ImportPipelineOrchestratorLoggingIntegrationTests.cs#L69-L81) :

```csharp
_isolementLog.Entries.Should().Contain(e =>
    e.Level == LogLevel.Warning &&
    e.Message.Contains(nameof(ExtractionErrorCode.UnrecognizedTypeElement)) &&
    e.Message.Contains(vanne.Repere));
```

Le test résout d'abord l'isolement réel `"VANNE"` extrait du fichier D8570
(`result.Isolements.Should().ContainSingle(i => i.TypeElementNom == "VANNE")`, ligne 73), vérifie
qu'il produit bien une `ExtractionError` de code `UnrecognizedTypeElement` dans
`ImportResult.Errors` (lignes 74-75), **puis** vérifie indépendamment que ce même événement est
visible en log à `Warning` via le logger dédié d'`IsolementExtractionService` (pas le logger de
l'orchestrateur) — cohérent avec la politique du projet : seul l'échec de l'Équipement parent
(PROCEDURE) déclenche un rejet total (`Warning` au niveau orchestrateur, mais sur un événement
différent, "rejected the whole file"), un `TypeElement` non reconnu sur un Isolement individuel
reste non bloquant.

### Persistance dédiée si G3 avait choisi cette option

**Sans objet** — G3 n'a pas été implémenté (voir §1). Aucune migration, aucun script à vérifier.

---

## 3. Écarts avec la conception documentée

- **G3 non implémenté, aucune décision actée** — écart le plus significatif : le ticket
  attendait un choix explicite (réutilisation `SystemLogs` vs. persistance dédiée), rien de tel
  n'existe dans le code ni dans un commit dédié. Seul un renvoi vers l'audit du 17/07 (qui
  qualifiait déjà le manque de "pas urgent") documente l'état actuel.
- **`SheetsProcessedOnSuccess = 6` en constante littérale, pas dérivée d'une collection** —
  documenté explicitement en commentaire ([`ImportPipelineOrchestrator.cs:39-43`](../src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs#L39-L43)) :
  il n'existe pas de liste unique "les 6 feuilles" dans cette classe (PLATINES/ORIFICES
  CAPACITES partagent un seul appel de service répété deux fois), donc reconstruire une
  collection juste pour la compter aurait été un détour inutile par rapport à un littéral.
- **`ExtractionError.BlockIdentifier` non enrichi malgré une remise en question explicite
  ("G2bis")** — décision déjà couverte au modèle de domaine, reconfirmée pendant le Lot G2
  plutôt que rouverte ; documentée à la fois dans le message de commit `b549ae5` et dans
  `CLAUDE.md`.
- **Convention de test Moq supposée par le ticket, invalidée par grep** — le ticket affirmait
  qu'un pattern `Mock<ILogger<T>>` + `Verify` existait déjà dans le dépôt ; c'était faux (aucune
  occurrence trouvée), donc le Lot G1 a suivi la convention réellement en place
  (`NullLogger<T>.Instance`, pas d'assertion sur le logging) plutôt que celle décrite dans le
  ticket.
- **Aucun changement de `Program.cs` (WebAPI ni BlazorAdmin) pour le Lot G** — cohérent avec le
  fait que `ILogger<T>` se résout automatiquement via l'infrastructure de logging déjà
  enregistrée dans chaque hôte ; confirmé par `git show --stat` sur les 2 commits G1/G2 (aucun
  fichier `Program.cs` dans la liste des fichiers modifiés).

---

## 4. Non couvert / incertain

- **Comportement réel en charge/volumétrie** — aucun test de charge ou de volumétrie n'existe
  pour le logging du pipeline OXO ; les 3 fixtures réelles restent des fichiers unitaires de
  taille modeste (quelques dizaines de lignes par feuille au plus), rien ne permet de dire
  comment le volume de logs se comporterait sur un usage de production répété.
- **Vérification manuelle des logs en conditions réelles (table `SystemLogs` réelle, sink
  Serilog réellement actif)** — non vérifiable depuis le code seul : les tests utilisent soit
  `NullLogger` soit `CapturingLogger` (un double en mémoire), jamais le sink `MSSqlServer` réel.
  Rien ne confirme qu'un événement de log OXO émis via `ImportProfileTest.razor` en conditions
  réelles atterrit effectivement et lisiblement dans la table `SystemLogs` partagée avec le
  WebAPI — seule la configuration Serilog de l'hôte BlazorAdmin (déjà documentée dans
  `CLAUDE.md`, section "Web API surface") permet de le déduire par cohérence, pas de le prouver.
- **Etat de la décision G3 au-delà du code** — impossible de savoir depuis le dépôt seul si une
  discussion informelle (hors session Claude Code, hors commit) a eu lieu entre-temps sur le
  choix `SystemLogs` vs. persistance dédiée ; seul l'état du code fait foi ici, et il est neutre
  (aucune des deux options n'a été commencée).

---

## Résultat de l'exécution des tests

Commandes exécutées le 2026-07-19 contre le commit `d7b9cf2` :

```
dotnet test tests/ExcelETL.Application.Tests/ExcelETL.Application.Tests.csproj \
  --filter "FullyQualifiedName~Extraction.Oxo"
→ Réussi : 76/76, 0 échec, 0 ignoré (297 ms)

dotnet test tests/ExcelETL.Infrastructure.Tests/ExcelETL.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~Extraction.Oxo|FullyQualifiedName~Excel.ImportPipeline|FullyQualifiedName~Excel.Isolement|FullyQualifiedName~Excel.Procedure|FullyQualifiedName~Excel.Autres|FullyQualifiedName~Excel.Divers|FullyQualifiedName~Excel.Platines|FullyQualifiedName~Excel.Orifices"
→ Réussi : 23/23, 0 échec, 0 ignoré (2 s)
```

Couvre l'ensemble des tests unitaires (Application.Tests) et d'intégration (Infrastructure.Tests,
contre les 3 fixtures réelles C7401/D8570/G6306B) touchés par les commits `54c6c53` (Lot G1) et
`b549ae5` (Lot G2), y compris `ImportPipelineOrchestratorLoggingIntegrationTests` (5 tests, tous
verts) qui capture directement les logs émis via `CapturingLogger<T>`.
