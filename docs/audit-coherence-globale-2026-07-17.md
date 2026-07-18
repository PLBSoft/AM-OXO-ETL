# Audit de cohérence globale — documentation ↔ code (2026-07-17)

*Basé sur une lecture directe du code sur `main` (dernier commit `6ee034f`) au 2026-07-17, en
réponse à la demande d'audit de cohérence documentation/code. Périmètre : pipeline d'extraction
OXO (Lots A→E), écran Blazor (Lot F), cohérence des 5 documents "vivants" listés en contexte, et
sujets hors périmètre (POC legacy, écriture cible, logs, sécurité).*

---

## 1. Statut complet des Lots A-E (extraction)

| Lot/Ticket | Statut | Preuve code |
|---|---|---|
| A1 Primitives | ✅ Présent | `src/ExcelETL.Domain/Extraction/Primitives/` — 8 fichiers, conforme au catalogue §1 de `modele-domaine-import-profile-2026-07-16.md` |
| A2 Modèle pivot | ✅ Présent | `src/ExcelETL.Domain/Extraction/Pivot/` — `EquipementPivot`, `IsolementPivot`, `PointPivot`, `TacheMultiplePivot`, `ImportResult`, `ExtractionError(Code)` |
| A3 `ImportProfile`/`SheetExtractionRule` | ✅ Présent | `src/ExcelETL.Domain/Extraction/Profile/` ; `EquipementTypeElementNom` validé non-blanc |
| B1 `IWorkbookReader` | ✅ Présent | `src/ExcelETL.Application/Extraction/Oxo/IWorkbookReader.cs` |
| B2 `TextTransformEvaluator` | ✅ Présent | idem dossier |
| B3 `RepeatingBlockLocator`/moteur | ✅ Présent | `RepeatingBlockReader.cs` + `BlockFieldRangeCalculator.cs` |
| B4 `ConditionalPointRuleEvaluator` | ✅ Présent | comparaison `.Trim()` + insensible à la casse confirmée (`ConditionalPointRuleEvaluatorTests.cs`) |
| C1 PROCEDURE | ✅ Présent | `Extraction/Oxo/Procedure/ProcedureExtractionService.cs` |
| C2 ISOLEMENT | ✅ Présent | `Extraction/Oxo/Isolement/IsolementExtractionService.cs` |
| C3 PLATINES | ✅ Présent | `Extraction/Oxo/UnconditionalIsolementSheetExtractionService.cs` (renommé depuis `PlatinesExtractionService`, voir §3) |
| C4 ORIFICES CAPACITES | ✅ Présent | même service que C3, appelé une 2e fois avec un `SheetExtractionRule` différent |
| C5 AUTRES JOINTS TOUCHES | ✅ Présent | `Extraction/Oxo/AutresJointsTouches/` |
| C6 DIVERS | ✅ Présent | `Extraction/Oxo/Divers/` |
| D1 `ImportPipelineOrchestrator` | ✅ Présent | `Extraction/Oxo/ImportPipelineOrchestrator.cs` |
| D2 Tests d'intégration 3 fixtures | ✅ Présent | `tests/ExcelETL.Infrastructure.Tests/Excel/ImportPipelineOrchestratorIntegrationTests.cs` |
| E1 `ClosedXmlWorkbookReader` | ✅ Présent | `src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookReader.cs` |
| E2 Persistance EF `ImportProfile` | ✅ Présent | `IImportProfileStore`/`EfImportProfileStore` + migration `20260717113850_AddImportProfile` |

**Important** : `docs/etat-avancement-pipeline-extraction-2026-07-17.md` (document déjà présent dans
le repo) affirme que Lot C n'est qu'à moitié fait (3/6 feuilles) et que Lot D/E n'existent pas.
**C'est un instantané volontairement daté d'avant le démarrage du Lot F** (il le dit lui-même en
introduction) — les commits `a281083`→`a7a8587` (C4, C5, C6, D1, D2, E2) sont tous postérieurs à
cet instantané. Ne pas s'y fier pour l'état actuel ; le tableau ci-dessus reflète l'état réel au
2026-07-17 après lecture directe du code.

### Tests explicitement demandés — tous confirmés présents

- **Test anti-hardcoding C1** (deux profils, `EquipementTypeElementNom` différents) : ✅
  `ProcedureExtractionServiceTests.Extract_UsesEquipementTypeElementNomFromProfile_NotAHardcodedConstant`
  — `[Theory]` avec `[InlineData("MAD TRAVAUX")]` et `[InlineData("REL TRAVAUX")]`
  ([ProcedureExtractionServiceTests.cs:72-86](../tests/ExcelETL.Application.Tests/Extraction/Oxo/Procedure/ProcedureExtractionServiceTests.cs)).
- **Test explicite `"VANNE"` (C2, fixture D8570)** : ✅ à deux niveaux —
  test unitaire contre `Mock<IWorkbookReader>` avec des cellules recopiées du fichier réel D8570
  ligne 117 ([IsolementExtractionServiceTests.cs:129-150](../tests/ExcelETL.Application.Tests/Extraction/Oxo/Isolement/IsolementExtractionServiceTests.cs))
  **et** test d'intégration contre le vrai fichier D8570
  (`IsolementExtractionServiceIntegrationTests.Extract_D8570Fixture_ExtractsVanneIsolementNormallyAlongsideProlockOnes`).
  Le ticket demandait explicitement d'utiliser la fixture réelle plutôt qu'une donnée synthétique
  — c'est fait pour le niveau intégration, en plus du niveau unitaire.
- **Test `TM_PROC_REL` (C1)** : les 3 fixtures réelles ne contiennent **aucune** ligne `R9 = "REL"`
  — vérifié : `ProcedureExtractionServiceIntegrationTests.cs` (Infrastructure.Tests, contre les 3
  vrais fichiers) ne fait aucune assertion sur `TypeTacheMultipleCode`/`R9`, cohérent avec le fait
  que les 3 fixtures disponibles sont toutes des dossiers MAD (aucun fichier REL n'existe à ce
  jour, point déjà noté dans le glossaire v4 et la spec §9). **Un test unitaire dédié existe bien**
  en remplacement : `ProcedureExtractionServiceTests` porte un `[Theory]` avec
  `[InlineData("REL", "TM_PROC_REL")]` et `[InlineData("REL ", "TM_PROC_REL")]`
  ([ProcedureExtractionServiceTests.cs:122-126](../tests/ExcelETL.Application.Tests/Extraction/Oxo/Procedure/ProcedureExtractionServiceTests.cs)),
  couvrant aussi la casse `.Trim()`. Conforme à l'exigence du ticket ("sinon, un test unitaire
  dédié existe-t-il ?").
- **Test `"POINT DE FEU"` (C6)** : ✅ à trois niveaux — `ConditionalPointRuleEvaluatorTests.cs`
  (évaluateur générique), `DiversExtractionServiceTests.cs:133` (service C6, cellules synthétiques
  reproduisant le cas), et `DiversExtractionServiceIntegrationTests.cs:84` contre le **vrai**
  fichier G6306B (`.And.Contain(i => i.TypeElementNom == "POINT DE FEU")`), qui est la fixture où
  cette variante a été observée. Conforme à l'exigence du ticket.

---

## 2. Statut complet du Lot F (Blazor)

**Confirmé F1 et F2 tous les deux terminés, aucune régression depuis le dernier instantané**
(`docs/etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`). Vérification indépendante :

- Aucun commit n'a touché `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfile*.razor`
  depuis `c61f6f8` (dernier commit de code du Lot F) — le seul commit postérieur, `6ee034f`, n'ajoute
  que le rapport d'état lui-même (`git log` vérifié).
- **`EquipementTypeElementNom` sans valeur par défaut codée en dur** : reconfirmé directement —
  [ImportProfileEditor.razor:205](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor:205)
  `private string _equipementTypeElementNom = string.Empty;` — aucune littérale `"MAD TRAVAUX"` ou
  équivalente dans le fichier. Garde-fou architecture toujours respecté.
- Routes confirmées : `/import-profiles` (liste), `/import-profiles/new` (construction),
  `/import-profiles/test` (test en mémoire) — 3 routes indépendantes, pas un écran à onglets.
- `IImportProfileStore`/`EfImportProfileStore` et les 9 services du pipeline OXO sont bien
  enregistrés dans `src/ExcelETL.BlazorAdmin/Program.cs` (`AddScoped`/`AddSingleton` respectivement).

Aucun écart supplémentaire trouvé par rapport au rapport du 17/07 18h06 — je n'ai pas cherché à
re-vérifier chaque détail déjà couvert par ce rapport (ex. IDs HTML, structure des sous-formulaires),
seulement l'absence de régression depuis, ce qui est confirmé par l'historique git.

---

## 3. Cohérence documentation ↔ code, écart par écart

### `UnconditionalColonneNames` — concept absent des documents de conception d'origine
Confirmé : **absent** de `modele-domaine-import-profile-2026-07-16.md` (§2.1 ne liste que
`SheetName`/`Locator`/`PointRules` pour `SheetExtractionRule`) et **absent** de
`tickets-tdd-extraction-2026-07-16.md` (le ticket C2 décrit le comportement — "sans condition sur
`TypeElement`" — mais ne nomme jamais la propriété qui le porte). C'est un concept apparu
**pendant** le développement du Lot C2 pour résoudre l'ambiguïté de groupement des `PointRules`
sans condition, comme documenté dans `CLAUDE.md` et déjà repéré dans
`etat-avancement-pipeline-extraction-2026-07-17.md` (§ "Écarts avec la conception documentée",
point 2). **Seul `CLAUDE.md` documente correctement cette propriété** aujourd'hui — les deux
documents de conception d'origine (modèle de domaine, tickets) restent silencieux sur son
existence. Ce n'est pas un défaut de mise en œuvre (le code est cohérent et testé), c'est un
écart de traçabilité documentaire qui persiste depuis le Lot C2 sans avoir été corrigé dans les
docs sources.

### Table des `TypeElement` OXO (glossaire) — cohérente avec le code
Scan de tous les littéraux `TypeElement`/`TypeElementNom` utilisés dans les tests Application et
Infrastructure (Lots C1-C6) : aucune valeur utilisée dans le code (`MAD TRAVAUX`, `REL TRAVAUX`,
`PROLOCK`, `TAMPON PLEIN`, `PLATINE`, `TROU D'HOMME`, `TUYAUTERIE`, `TUBING`, `INSTRUMENTATION`,
`ZERO ENERGIE`, `SOUPAPE`, `POINT FEU`, `POINT DE FEU`, `VANNE`) ne figure en dehors de la table
du glossaire (`VANNE` y est explicitement documenté comme absent de la base OXO, exactement comme
utilisé dans les tests). **Rien à signaler.**

### Routes/fichiers Blazor réels du Lot F vs `tickets-tdd-blazor-profil-import-2026-07-17.md`
**Ce document n'existe pas dans le dépôt** — ni sur disque, ni dans l'historique git. Reconfirmé
indépendamment (`git log --all --oneline -- "*tickets-tdd-blazor*"` et `find`/`grep` sur tout le
repo : aucune correspondance, seule la mention dans `CLAUDE.md` §"Lot F1.1..." existe). Ce
constat avait déjà été fait dans `docs/etat-avancement-lot-f-blazor-profil-import-2026-07-17-18h06.md`
(§1, remarque préalable) — confirmé toujours vrai. **Il est donc impossible de comparer le Lot F
ligne à ligne contre son cahier des charges TDD d'origine** ; la seule source de vérité disponible
pour le Lot F est la description synthétique de `CLAUDE.md`, elle-même vérifiée ligne à ligne
contre le code au §2 ci-dessus.

### Renommage `PlatinesExtractionService` → `UnconditionalIsolementSheetExtractionService`
`tickets-tdd-extraction-2026-07-16.md` (C3/C4) nomme encore implicitement un service "PLATINES"
dédié et un service ORIFICES CAPACITES potentiellement distinct. Le code a fusionné les deux
(commit `4de787c`, "Rename PlatinesExtractionService to shared
UnconditionalIsolementSheetExtractionService") une fois confirmé que les deux feuilles partagent
une structure byte-identique. **Documenté correctement dans `CLAUDE.md`** ("renamed from
`PlatinesExtractionService` once building C4 confirmed...") mais **non répercuté** dans le ticket
d'origine, qui reste écrit au singulier par feuille. Écart cosmétique — le ticket décrit
l'intention business (règles C3/C4), pas une contrainte d'implémentation de nommage, donc ce n'est
pas une violation du ticket, juste un nom de fichier qui a évolué sans que le ticket source soit
mis à jour.

### Kestrel `MaxRequestBodySize` — écart documentation ↔ code repéré dans `CLAUDE.md` lui-même
Hors périmètre strict des 5 documents cités, mais découvert en vérifiant la section "Web API
surface" de `CLAUDE.md` : celle-ci affirme `MaxRequestBodySize`=**100 MB**, alors que le code
([Program.cs:23](../src/ExcelETL.WebAPI/Program.cs:23)) fixe
`options.Limits.MaxRequestBodySize = UploadLimits.MaxExcelFileSizeBytes`, et
[UploadLimits.cs:9](../src/ExcelETL.WebAPI/UploadLimits.cs:9) définit cette constante à
**10 MB** (`10 * 1024 * 1024`) — cohérent avec le reste de la doc ("Max 10 MB" mentionné juste
avant dans le même paragraphe de `CLAUDE.md`, et avec `UploadTest.razor`'s côté client). La valeur
"100 MB" semble être une confusion résiduelle avec une ancienne valeur ou une coquille lors de la
rédaction de `CLAUDE.md` — **le code est cohérent en interne (Kestrel = client Blazor = 10 MB),
seul `CLAUDE.md` affiche un chiffre erroné.**

---

## 4. État du POC legacy et sujets hors périmètre

### `ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`/`IExtractionConfigRepository` — toujours actifs
Confirmé, aucune dépréciation amorcée :
- `src/ExcelETL.Domain/Entities/ExtractionConfig.cs`, `SheetConfig.cs`, `CellMapping.cs` toujours
  présents.
- `Mappings.razor`/`UploadTest.razor` toujours présents dans `Components/Pages/Admin/`.
- `IExtractionConfigRepository`/`ExtractionConfigRepository` toujours enregistrés dans **les deux**
  hosts (`ExcelETL.BlazorAdmin/Program.cs:90` et `ExcelETL.WebAPI/Program.cs:92`).
- Le Web API expose toujours uniquement l'ancien pipeline : `POST /api/excel/process`
  (`ExcelController`) reste câblé sur `IExcelExtractionService`/`IExcelGeneratorService`
  (`ClosedXmlExtractionService`/`ClosedXmlGeneratorService`), pas sur le pipeline OXO. Le nouveau
  pipeline `Extraction/Oxo/` n'est exposé **que** par le Lot F Blazor (`ImportProfileTest.razor`,
  en mémoire, sans host HTTP).

### Écriture du fichier `.xlsx` cible — toujours absente pour le pipeline OXO
Confirmé : aucune référence à `SaveAs`/`XLWorkbook` dans `src/ExcelETL.Application/Extraction/Oxo/`
ni dans `src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookReader.cs`. Un générateur
`.xlsx` existe bien dans le code (`ClosedXmlGeneratorService.cs`,
`src/ExcelETL.Infrastructure/Excel/`) mais **appartient exclusivement à l'ancien pipeline POC**
(il consomme `ExtractionResult`/`ExtractedSheet`, le modèle de `ExcelETL.Application.Extraction`,
pas `ImportResult`/le modèle pivot OXO). Le pipeline OXO reste donc **lecture seule** de bout en
bout, conforme à ce que documentent `tickets-tdd-extraction-2026-07-16.md` ("hors périmètre") et
`CLAUDE.md`.

### Logs applicatifs — présents pour le POC, **absents pour le pipeline OXO**
- **POC (`ProcessExcelFileService`, `ClosedXmlExtractionService`)** : `ILogger` injecté et utilisé
  (`LogInformation`/`LogDebug`/`LogError`), plus persistance via `ExtractionHistory` (table dédiée)
  — couvre upload, extraction et erreurs pour ce pipeline.
- **WebAPI** : Serilog configuré (`Program.cs:48-62`) avec sink `Console` + sink SQL Server
  (`WriteTo.MSSqlServer`, table `SystemLogs`, partagée avec BlazorAdmin via la propriété
  `Application`) — **ceci n'est documenté nulle part dans `CLAUDE.md`** ("Web API surface"
  n'en parle pas), découverte faite pendant cet audit.
- **Pipeline OXO (`Extraction/Oxo/`)** : recherche exhaustive (`ILogger` dans
  `src/ExcelETL.Application/Extraction/Oxo/` et `ClosedXmlWorkbookReader.cs`) : **aucune
  correspondance**. Aucun des 6 services par feuille, de l'orchestrateur, ni du lecteur ClosedXML
  du pipeline OXO n'émet le moindre log. Le point du cahier des charges initial "Errors (exception
  type, merged cell coordinate that failed)" (cité dans `modele-domaine-import-profile-2026-07-16.md`
  §3) est couvert **fonctionnellement** par `ExtractionError`/`ImportResult` (retourné à l'appelant),
  mais **pas par un mécanisme de log/persistance** — contrairement au POC qui a `ExtractionHistory`.
  À signaler comme un vrai trou si le pipeline OXO doit un jour être exposé en production hors du
  Lot F Blazor.

### Sécurité API Key — présente, inchangée, mais ne couvre que le POC
`ApiKeyAuthenticationHandler`/`ApiKeyAuthenticationDefaults`/`ApiKeyAuthenticationOptions`
toujours présents dans `src/ExcelETL.WebAPI/Authentication/`. Protège `POST /api/excel/process`
(l'ancien pipeline). Le pipeline OXO n'ayant aucune route HTTP, la question de sa sécurité M2M ne
se pose pas encore — à traiter le jour où (si) il sera exposé par le Web API.

---

## Écarts à corriger

### Bloquant
*Aucun.* Le pipeline OXO et le Lot F sont fonctionnellement complets et testés contre les 3
fixtures réelles ; aucune incohérence trouvée ne bloque la suite du développement.

### À corriger bientôt
1. **`CLAUDE.md` affiche `MaxRequestBodySize`=100 MB alors que le code applique 10 MB**
   (`UploadLimits.MaxExcelFileSizeBytes`, [Program.cs:23](../src/ExcelETL.WebAPI/Program.cs:23) /
   [UploadLimits.cs:9](../src/ExcelETL.WebAPI/UploadLimits.cs:9)) — corriger la ligne dans
   `CLAUDE.md` pour éviter qu'un futur lecteur suppose une limite 10× plus large que la réalité.
2. **`docs/tickets-tdd-blazor-profil-import-2026-07-17.md` référencé par `CLAUDE.md` n'existe pas**
   — soit le créer rétroactivement (a posteriori, à partir de ce qui a été livré) pour disposer
   d'un cahier des charges vérifiable pour le Lot F, soit retirer la référence de `CLAUDE.md` si
   aucun document de ce type n'a jamais été formellement rédigé côté client/produit.
3. **Aucun logging/persistance pour le pipeline OXO** (`Extraction/Oxo/`) — si ce pipeline doit
   un jour tourner en dehors du Lot F Blazor (ex. exposition Web API), il lui manquera l'équivalent
   de `ExtractionHistory`/`ILogger` que possède déjà le POC. Pas urgent tant que le seul
   consommateur est la page de test Blazor en mémoire, mais à anticiper avant toute mise en
   production du nouveau pipeline.
4. **Logging Serilog/SystemLogs du WebAPI non documenté dans `CLAUDE.md`** — section "Web API
   surface" à compléter pour éviter qu'un futur lecteur ignore ce mécanisme de log centralisé.

### Cosmétique
1. **`UnconditionalColonneNames` absent de `modele-domaine-import-profile-2026-07-16.md` et
   `tickets-tdd-extraction-2026-07-16.md`** — le concept existe et fonctionne, seule la
   traçabilité documentaire côté conception est en retard sur le code (déjà noté dans
   `etat-avancement-pipeline-extraction-2026-07-17.md`, toujours pas corrigé). Ajouter un
   paragraphe dans le modèle de domaine réglerait définitivement le sujet.
2. **Renommage `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService`
   non répercuté dans `tickets-tdd-extraction-2026-07-16.md`** (C3/C4 toujours écrits comme deux
   services distincts par feuille) — sans impact fonctionnel, `CLAUDE.md` documente déjà le
   renommage correctement.

---

## Rien à signaler (vérifié conforme, inutile de re-vérifier)

- Lots A, B, C (6/6 feuilles), D, E1, E2 : tous présents, structure conforme au modèle de domaine
  et aux tickets.
- Les 4 tests explicitement demandés par l'audit (anti-hardcoding C1, `"VANNE"` D8570 C2,
  `TM_PROC_REL` C1, `"POINT DE FEU"` C6) sont tous présents, certains à plusieurs niveaux
  (unitaire + intégration fixture réelle).
- Lot F (F1.1, F1.2/F1.3, F2) intégralement terminé, aucune régression depuis le dernier
  instantané du 17/07 18h06 (confirmé par `git log` : aucun commit de code postérieur).
- `_equipementTypeElementNom` dans `ImportProfileEditor.razor` reste sans valeur par défaut codée
  en dur — garde-fou architecture toujours respecté.
- Table des `TypeElement` du glossaire cohérente à 100 % avec les valeurs utilisées dans le code
  et les tests (aucune valeur orpheline des deux côtés).
- `ExtractionErrorCode` a toujours exactement 3 membres, conforme à la décision documentée
  (volontairement non exhaustif).
- i18n `DomainErrorMessages.resx`/`.fr.resx` : exactement les 17 clés `ImportProfile_*`/
  `SheetExtractionRule_*`/`RepeatingBlockLocator_*`/`BlockFieldDefinition_*`/
  `ConditionalPointRule_*` documentées dans `CLAUDE.md`, parité EN/FR confirmée, aucune des 10
  clés `DirectCell_*`/`SubstringAfter_*`/`Concat_*`/`FieldRef_*`/`EquipementPivot_*`/
  `IsolementPivot_*`/`PointPivot_*`/`TacheMultiplePivot_*`/`ExtractionError_*` présente (conforme
  au choix documenté de ne pas les traduire tant qu'aucun host ne les expose).
- POC (`ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`/`IExtractionConfigRepository`) :
  toujours actif dans les deux hosts, aucune dépréciation commencée.
- Écriture `.xlsx` cible : toujours absente côté pipeline OXO (le générateur existant appartient
  exclusivement à l'ancien pipeline POC).
- Sécurité API Key (`ApiKeyAuthenticationHandler`) : toujours en place, inchangée, protège
  uniquement l'ancien pipeline (seul consommateur HTTP existant).

## Non vérifiable depuis le code seul

- **Application effective de la migration `20260717113850_AddImportProfile` sur une base SQL
  Server réelle** — le fichier de migration existe dans le dépôt (code C#), mais rien dans le code
  ne permet de confirmer si elle a été appliquée à une base de données réelle en dehors des tests
  (InMemory provider). `CLAUDE.md` la documente déjà comme "not yet applied to any real SQL Server
  database" — cette affirmation n'a pas pu être vérifiée ni infirmée depuis ce dépôt (nécessiterait
  un accès à l'environnement Windows Server 2022 cible).
