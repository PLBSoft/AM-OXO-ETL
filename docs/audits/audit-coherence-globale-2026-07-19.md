# Audit de cohérence globale — documentation ↔ code (2026-07-19)

*Basé sur une lecture directe du code sur `main` (dernier commit `e378c7e`, 2026-07-19 10:40:49
+0200) au 2026-07-19, en réponse à la demande de second audit de cohérence globale. Périmètre :
tout ce qui a été livré depuis le premier audit (`docs/audit-coherence-globale-2026-07-17.md`,
commit `6ee034f`) — Lot I (écriture fichier cible), Lot J (écran Blazor export), F3 (édition
profil import), Lot G1/G2/G3 (logging OXO + config Serilog partagée), plus une recherche de
nouveaux écarts et un nouveau contrôle des points encore ouverts. Les Lots A-E et F1/F2, déjà
vérifiés conformes le 17/07 et non retouchés depuis (confirmé par `git log` sur leurs fichiers),
**ne sont pas rouverts ici**.

**Méthode** : lecture directe du code source (Domain/Application/Infrastructure/BlazorAdmin) pour
chaque lot, comparaison ligne à ligne avec le ticket correspondant, exécution réelle de la suite
de tests complète (`dotnet test ExcelETL.slnx`, pas seulement relecture des rapports de session
précédents), et recherche indépendante de nouveaux écarts. Les instantanés de session déjà produits
(`etat-avancement-lot-j-...`, `etat-avancement-lot-g-...`, section "Preuve" de F3) ont été
spot-vérifiés contre le code réel plutôt que recopiés tels quels — chaque citation de ligne
contrôlée s'est révélée exacte.

---

## 1. Lot I — écriture du fichier Excel cible (`docs/tickets-tdd-ecriture-fichier-cible.md`)

**Aucun instantané de session n'existait pour ce lot avant cet audit** (contrairement à J/F3/G) —
c'est donc le lot vérifié le plus en profondeur ici, code lu intégralement plutôt que spot-vérifié.

| Ticket | Statut | Preuve |
|---|---|---|
| I1 Domain primitives | ✅ Conforme | [`ExportProfile.cs`](../src/ExcelETL.Domain/Generation/Profile/ExportProfile.cs), [`SheetGenerationRule.cs`](../src/ExcelETL.Domain/Generation/Profile/SheetGenerationRule.cs), [`ColumnDefinition.cs`](../src/ExcelETL.Domain/Generation/Profile/ColumnDefinition.cs), [`PointColumnDefinition.cs`](../src/ExcelETL.Domain/Generation/Profile/PointColumnDefinition.cs) — 4 `sealed record`, égalité structurelle, `SequenceEqual` overridé sur `ExportProfile`/`SheetGenerationRule` (listes) |
| I2 `PivotFieldRef`/`PivotFieldResolver` | ✅ Conforme | [`PivotFieldRef.cs`](../src/ExcelETL.Domain/Generation/Fields/PivotFieldRef.cs) (9 membres, `Equipement*`×4/`Isolement*`×5) ; [`PivotFieldResolver.cs`](../src/ExcelETL.Domain/Generation/Fields/PivotFieldResolver.cs) — `GetPivotSource` pas de réflexion |
| I2 validation croisée au chargement du profil | ✅ Conforme | [`SheetGenerationRule.cs:40-51`](../src/ExcelETL.Domain/Generation/Profile/SheetGenerationRule.cs#L40-L51) — `DomainRuleViolationException` levée **dans le constructeur**, avant toute génération de fichier |
| I3 moteur sans dépendance ClosedXML | ✅ Conforme | [`SheetGenerationEngine.cs`](../src/ExcelETL.Application/Generation/SheetGenerationEngine.cs) — aucun `using ClosedXML` ; `GeneratedRow`/`GeneratedSheet`/`GeneratedWorkbook` sont des `record` purs |
| I3 ordre des colonnes (descriptives puis Points) | ✅ Conforme | [`SheetGenerationEngine.cs:30-32`](../src/ExcelETL.Application/Generation/SheetGenerationEngine.cs#L30-L32) — `ColumnDefinitions.Select(...).Concat(PointColumnDefinitions.Select(...))` |
| I3 `Source = null` → cellule vide | ✅ Conforme | [`SheetGenerationEngine.cs:53,64`](../src/ExcelETL.Application/Generation/SheetGenerationEngine.cs#L53) — `column.Source is null ? string.Empty : ...` |
| I3 décision `Equipement is null` | ✅ Conforme, décision actée en commentaire | [`SheetGenerationEngine.cs:11-16,46-49`](../src/ExcelETL.Application/Generation/SheetGenerationEngine.cs#L11-L16) — feuille Équipement **présente avec en-têtes, 0 ligne**, jamais omise ; comportement documenté explicitement comme décision (pas un oubli) |
| I4 writer indépendant du POC | ✅ Conforme | [`ClosedXmlWorkbookWriter.cs`](../src/ExcelETL.Infrastructure/Excel/ClosedXmlWorkbookWriter.cs) — aucune référence à `ClosedXmlGeneratorService`, commentaire explicite ligne 9-10 |
| I4 nommage `MAD_{Repere}_{AAAAMMDDHHmmss}.xlsx` | ✅ Conforme | [`TargetWorkbookFileNameBuilder.cs`](../src/ExcelETL.Infrastructure/Excel/TargetWorkbookFileNameBuilder.cs) — fonction séparée, testée indépendamment |
| I5 tests bout-en-bout 3 fixtures + cas VANNE | ✅ Conforme | [`GenerationPipelineIntegrationTests.cs`](../tests/ExcelETL.Infrastructure.Tests/Excel/GenerationPipelineIntegrationTests.cs) — `GenerateFromD8570Fixture_KeepsUnrecognizedTypeElementIsolementAsNormalRow` (ligne 202-213) confirme la ligne `"VANNE"` présente dans la feuille `Enfants` générée |
| I6 EF Core, `Source = null` persiste comme `null` | ✅ Conforme | [`ExportProfileConfiguration.cs:51-53`](../src/ExcelETL.Infrastructure/Persistence/Configurations/ExportProfileConfiguration.cs#L51-L53) — `HasConversion<string>()` **sans** `IsRequired()` ; test dédié `SaveAsync_WithNullColumnSource_PersistsAndReloadsAsNull_NotADefaultValue` confirmé présent dans `EfExportProfileStoreTests.cs` |
| I6 upsert par Id, 2 `SaveChangesAsync` | ✅ Conforme | [`EfExportProfileStore.cs:26-38`](../src/ExcelETL.Infrastructure/Persistence/Repositories/EfExportProfileStore.cs#L26-L38) — même pattern remove-puis-add que `EfImportProfileStore` |

**Les 7 décisions d'architecture actées en amont sont toutes respectées** : séparation
colonnes descriptives/Points ✅, colonne non mappée = cellule vide jamais absente ✅, `PivotSource`
explicite validé à la construction ✅, ordre des colonnes Points figé par l'ordre du profil (pas
recalculé dynamiquement) ✅, pas de réutilisation de `ClosedXmlGeneratorService` ✅, feuille Tâches
Multiples absente (confirmé : aucune référence à `TacheMultiple` dans `Generation/`) ✅, écran
Blazor hors périmètre I (livré séparément au Lot J) ✅.

**Migration `20260718092214_AddExportProfile`** : postérieure et cohérente avec
`20260717113850_AddImportProfile`, pas de conflit de nom de table ni d'ordre — vérifié par lecture
directe des deux fichiers de migration et de `ExcelEtlDbContextModelSnapshot.cs`.

---

## 2. Lot J — écran Blazor de profil d'export (`docs/tickets-tdd-blazor-profil-export.md`)

**Confirmé conforme à l'instantané du 18/07** (`docs/etat-avancement-lot-j-blazor-profil-export-2026-07-18.md`) :
- `git log --oneline -- ExportProfiles.razor ExportProfileEditor.razor ExportProfileTest.razor` ne
  montre aucun commit postérieur à `a755b5e` (dernier commit de code du Lot J) — seul `70e8d63`
  (ajout du rapport d'état lui-même) et les 3 commits G3 (`e378c7e` et alentours, qui ne touchent
  que `Program.cs`/`Hosting`) sont postérieurs, aucun ne touche les fichiers `.razor` du Lot J.
- **Spot-vérification indépendante des citations de ligne** de l'instantané (pas une simple
  confiance dans le document) : [`ExportProfileEditor.razor:176-193`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ExportProfileEditor.razor#L176-L193)
  (chargement/pré-remplissage en édition), lignes 256-273 (sauvegarde avec `_editingId` préservant
  l'Id) — **les numéros de ligne et le contenu cités correspondent exactement au code réel**.
- Les 3 routes, le filtrage `PivotFieldRef`/`PivotSource`, le blocage de génération sur
  `Equipement is null`, et le câblage DI (`Program.cs:113-115`, `AddScoped`/`AddSingleton`)
  restent conformes — aucune raison de re-dérouler ce qui a déjà été vérifié ligne à ligne le
  18/07 sans qu'aucun commit n'ait touché ces fichiers depuis.

**Rien de nouveau à signaler pour le Lot J.**

---

## 3. F3 — édition d'un profil d'import existant (`docs/tickets-tdd-blazor-profil-import.md`)

**Confirmé conforme à sa propre section "Preuve"** :
- `git log --oneline -- ImportProfileEditor.razor ImportProfiles.razor` : dernier commit `d7b9cf2`
  (F3 lui-même) ; aucun commit postérieur (`c96a154`/`e0a967c`/`e378c7e` touchent
  `LoginTests.cs`/`MainLayoutTests.cs`/`CLAUDE.md`/`Hosting`, jamais ces deux fichiers Razor).
- Spot-vérification indépendante : [`ImportProfileEditor.razor:1-2`](../src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor#L1-L2)
  (2 routes confirmées), lignes 217-242 (`_editingId`/`_notFound`/`GetByIdAsync`), lignes 320-328
  (`SaveProfileAsync` — constructeur 5 arguments préservant l'Id en édition vs. constructeur 4
  arguments en création) — **exactement comme documenté dans la Preuve, aucun écart trouvé**.

**Rien de nouveau à signaler pour F3.**

---

## 4. Lot G1/G2/G3 — logging pipeline OXO + config Serilog partagée (`docs/tickets-tdd-corrections-audit-coherence.md`)

### G1/G2 : conformes à l'instantané du 19/07, aucune régression
`git log --oneline -- ImportPipelineOrchestrator.cs` : derniers commits touchant ce fichier sont
`54c6c53` (G1) et `b549ae5` (G2), tous deux antérieurs à `e378c7e` (G3, qui ne touche que
`Program.cs`×2 et le nouveau projet `Hosting`). Spot-vérification directe de
[`ImportPipelineOrchestrator.cs`](../src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs) :
`Stopwatch` (ligne 51), log de démarrage (50), rejet fichier entier avec durée (60-63), succès avec
durée/nombre de feuilles/nombre d'éléments (107-112), `SheetsProcessedOnSuccess = 6` en constante
littérale commentée (39-43), échec inattendu avec durée (118-120) — **identique à ce que
l'instantané du 19/07 décrit**, aucun écart.

### G3 : implémenté depuis le dernier instantané (qui le donnait "non fait") — vérifié conforme à sa propre section "Preuve"
- **Nouveau projet `src/ExcelETL.Hosting/`** confirmé : [`SerilogHostLoggingExtensions.cs`](../src/ExcelETL.Hosting/SerilogHostLoggingExtensions.cs)
  expose `AddOxoHostLogging(IHostBuilder, applicationName, connectionString)` + `Configure(...)`
  public/testable séparément. Référencé uniquement par les deux hôtes
  ([`ExcelETL.WebAPI.csproj:16`](../src/ExcelETL.WebAPI/ExcelETL.WebAPI.csproj#L16),
  [`ExcelETL.BlazorAdmin.csproj:5`](../src/ExcelETL.BlazorAdmin/ExcelETL.BlazorAdmin.csproj#L5)) —
  confirmé qu'**aucun** `.csproj` de `Application`/`Infrastructure`/`Domain` ne référence Serilog
  (recherche exhaustive, aucune correspondance).
- Les deux hôtes appellent bien `AddOxoHostLogging(...)` avec leur propre nom
  ([`WebAPI/Program.cs:42`](../src/ExcelETL.WebAPI/Program.cs#L42),
  [`BlazorAdmin/Program.cs:38`](../src/ExcelETL.BlazorAdmin/Program.cs#L38)) et ont bien perdu
  leur `PackageReference` directe à `Serilog.AspNetCore`/`Serilog.Sinks.MSSqlServer` (vérifié par
  lecture des deux `.csproj` : plus aucune trace).
- Test de non-duplication confirmé : [`SerilogHostLoggingExtensionsTests.cs`](../tests/ExcelETL.Hosting.Tests/SerilogHostLoggingExtensionsTests.cs) —
  3 `[Theory]` × 2 cas (`ExcelETL.WebAPI`/`ExcelETL.BlazorAdmin`) = 6 tests, correspond exactement
  au "6/6 verts" annoncé.
- `CLAUDE.md` mis à jour aux deux endroits attendus ("Projects" et "Web API surface") avec le
  texte de décision cité dans le ticket — confirmé par lecture directe des deux sections.

**G3 est donc bien terminé et conforme à sa propre Preuve.** Voir cependant §5.1 ci-dessous pour
un écart réel découvert en marge de cette vérification.

---

## 5. Nouveaux écarts trouvés

### 5.1 — `ExcelETL.Hosting` et `ExcelETL.Hosting.Tests` absents de `ExcelETL.slnx` (nouveau, non détecté par la session G3 elle-même)
**Confirmé par exécution réelle** : `dotnet test ExcelETL.slnx` à la racine du dépôt compile bien
`ExcelETL.Hosting` (référencé transitivement par les deux hôtes) mais **n'exécute aucun test du
projet `ExcelETL.Hosting.Tests`** — absent de la sortie de test, alors que les 5 autres projets de
test (`Domain.Tests` 255, `Application.Tests` 91, `Infrastructure.Tests` 135, `WebAPI.Tests` 14,
`BlazorAdmin.Tests` 112, plus `Legacy.NewApiPingService.Tests` 9) s'exécutent tous. Confirmé par
lecture directe d'[`ExcelETL.slnx`](../ExcelETL.slnx) : la liste `<Folder Name="/src/">` ne
contient pas `ExcelETL.Hosting.csproj` et `<Folder Name="/tests/">` ne contient pas
`ExcelETL.Hosting.Tests.csproj` — les deux projets existent bien sur disque et compilent (ils sont
référencés par les `.csproj` des deux hôtes), mais **n'ont jamais été ajoutés au fichier solution**.

**Conséquence concrète** : quiconque exécute `dotnet build`/`dotnet test` au niveau solution (la
commande naturelle pour une CI ou un `git clone` initial) ne compile ni n'exécute jamais les 6
tests `SerilogHostLoggingExtensionsTests` — la preuve "6/6 verts" du ticket G3 n'est reproductible
qu'en ciblant le `.csproj` directement (`dotnet test tests/ExcelETL.Hosting.Tests/...`, exactement
la commande documentée dans `tickets-tdd-corrections-audit-coherence.md`), jamais via la solution.
Il n'y a pas de pipeline CI dans ce dépôt aujourd'hui (`.github/workflows/` existe mais est vide),
donc l'impact réel est actuellement nul en pratique — mais le jour où une CI est mise en place sur
la base de `dotnet test ExcelETL.slnx` (l'approche la plus naturelle), `ExcelETL.Hosting.Tests`
serait silencieusement exclu sans aucune erreur ni avertissement.

### 5.2 — Aucun autre écart structurel trouvé
- **Duplication `ImportProfileEditor.razor`/`ExportProfileEditor.razor`** : les deux composants
  partagent la même forme (sous-formulaire "ajouter une règle de feuille", construction directe de
  l'objet Domain dans un `try/catch`, `BusinessExceptionLocalizer`) mais opèrent sur des modèles
  Domain distincts (`SheetExtractionRule` à 4 sous-concepts vs `SheetGenerationRule` à 2) avec des
  invariants différents — un examen des deux fichiers ne fait apparaître aucun bloc de code
  factorisable sans introduire une abstraction générique prématurée (violerait la consigne
  YAGNI du projet). Pas un écart, confirmé volontaire par les deux tickets eux-mêmes (J2 note
  explicitement que J n'a réutilisé aucune API Domain/Application nouvelle mais n'a pas non plus
  factorisé les deux pages Razor entre elles).
- **Migrations EF Core** : ordre chronologique cohérent (`InitialCreate` → `AddCompletedAtUtcToExtractionHistories`
  → `AddImportProfile` 2026-07-17 → `AddExportProfile` 2026-07-18), pas de conflit de nom de table,
  `ExcelEtlDbContextModelSnapshot.cs` à jour avec la dernière migration.

---

## 6. Statut des points encore ouverts (depuis le premier audit)

| Point | Statut au 17/07 | Statut au 19/07 |
|---|---|---|
| Format exact `OXO_TRAME_IMPORT_MAD.xlsx` | Non figé côté client | **Inchangé** — `tickets-tdd-ecriture-fichier-cible.md` reconfirme explicitement "format non figé côté client" (ligne 6-8), l'`ExportProfile` de test au Lot I5 reste une "approximation de travail" ; toujours pas de feuille Tâches Multiples (confirmé : aucune référence à `TacheMultiple` dans `Generation/`) |
| Retrait du POC legacy (`ExtractionConfig`/`Mappings.razor`/`UploadTest.razor`) | Toujours actif | **Inchangé** — `CellMapping.cs`/`ExtractionConfig.cs`/`ExtractionHistory.cs`/`SheetConfig.cs` toujours dans `Domain/Entities/` ; `Mappings.razor`/`UploadTest.razor` toujours dans `Components/Pages/Admin/` ; aucune dépréciation amorcée |
| Sécurité API Key / exposition M2M du pipeline OXO | Hors périmètre, aucune route HTTP | **Inchangé** — `ExcelETL.WebAPI/Controllers/` contient toujours seulement `ExcelController`/`HealthController` ; aucune référence source à `Extraction.Oxo`/`ImportPipelineOrchestrator`/`IImportProfileStore`/`IExportProfileStore` dans `ExcelETL.WebAPI` (recherche exhaustive, seuls les binaires compilés référencent `Application.dll`/`Infrastructure.dll` transitivement) |
| Migration `AddImportProfile` appliquée à une vraie base SQL Server | Non vérifiable depuis le dépôt | **Toujours non vérifiable** — s'applique désormais aussi à `AddExportProfile` (20260718092214), pour la même raison (nécessite un accès à l'environnement Windows Server 2022 cible, hors de portée d'un audit dépôt-seul) |

### Points résolus depuis le 17/07 (ne plus revérifier)
- **`CLAUDE.md` affichait `MaxRequestBodySize`=100 MB vs 10 MB réel** — **corrigé** : `CLAUDE.md`
  ligne 219 affiche désormais correctement 10 MB, cohérent avec `UploadLimits.MaxExcelFileSizeBytes`.
- **`docs/tickets-tdd-blazor-profil-import-2026-07-17.md` référencé mais inexistant** —
  **corrigé** : `CLAUDE.md` ne référence plus aucun nom de fichier daté pour ce document ; le
  document réel `docs/tickets-tdd-blazor-profil-import.md` (sans suffixe de date) existe désormais
  et fait foi, créé rétroactivement puis complété par F3.
- **`UnconditionalColonneNames` absent de `modele-domaine-import-profile.md`** — **corrigé** :
  document désormais une section dédiée §1.4 (lignes 56-107), concept et propriété documentés.
- **Logging Serilog/SystemLogs du WebAPI non documenté dans `CLAUDE.md` (H3)** — **corrigé** :
  la section "Web API surface" documente désormais explicitement `AddOxoHostLogging` et le
  mécanisme de sink partagé, mis à jour par le Lot G3 lui-même.

### Point non résolu, toujours cosmétique
- **Renommage `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService` non
  répercuté dans `tickets-tdd-extraction.md`** — confirmé toujours vrai : le document décrit
  encore C3 (PLATINES) et C4 (ORIFICES CAPACITES) comme deux sections séparées sans mentionner la
  fusion en un seul service partagé. Sans impact fonctionnel, `CLAUDE.md` documente déjà le
  renommage correctement.

---

## Écarts à corriger

### Bloquant
*Aucun.* Les Lots I, J, F3, G1/G2/G3 sont fonctionnellement complets, testés (613/613 tests
exécutés et confirmés verts indépendamment dans cet audit — voir §7), et cohérents avec leurs
tickets respectifs. Aucune incohérence trouvée ne bloque la suite du développement.

### À corriger bientôt
1. **`ExcelETL.Hosting`/`ExcelETL.Hosting.Tests` absents de `ExcelETL.slnx`** (§5.1) — ajouter les
   deux projets au fichier solution pour que `dotnet build`/`dotnet test ExcelETL.slnx` les inclue.
   Sans impact aujourd'hui (pas de CI en place), mais deviendrait un trou silencieux le jour où une
   CI est configurée sur la base de la solution plutôt que d'une liste de `.csproj` explicite.
2. **Renommage `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService` toujours
   non répercuté dans `tickets-tdd-extraction.md`** (hérité du 17/07, toujours vrai) — cosmétique,
   sans urgence.

### Cosmétique
*Aucun nouveau point cosmétique au-delà de celui listé ci-dessus (déjà hérité du 17/07).*

---

## Rien à signaler (vérifié conforme le 19/07, inutile de re-vérifier tant qu'aucun commit ne touche ces fichiers)

- **Lot I (I1-I6) intégralement conforme au ticket**, y compris les 7 décisions d'architecture
  actées en amont — vérifié par lecture complète du code (pas de spot-check, aucun instantané de
  session préexistant à recouper).
- **Lot J (J1-J4) conforme à l'instantané du 18/07**, aucune régression depuis (confirmé par
  `git log` sur les 3 fichiers `.razor` + spot-vérification indépendante des citations de ligne).
- **F3 conforme à sa propre section "Preuve"**, aucune régression depuis (confirmé par `git log` +
  spot-vérification indépendante).
- **G1/G2 conformes à l'instantané du 19/07**, aucune régression depuis le Lot G3 (qui n'a touché
  que `Program.cs`×2 et le nouveau projet `Hosting`, jamais les fichiers `Extraction/Oxo/`).
- **G3 terminé et conforme à sa propre section "Preuve"** — nouveau projet `ExcelETL.Hosting`,
  câblage des deux hôtes, suppression des `PackageReference` Serilog directes, `CLAUDE.md` à jour
  aux deux endroits attendus. Seul écart réel : absence du projet dans le fichier solution (§5.1,
  listé séparément car c'est un vrai trou, pas une confirmation).
- **H3 (logging WebAPI/`SystemLogs` non documenté)** : résolu par la mise à jour `CLAUDE.md` du
  Lot G3 — plus rien à signaler sur ce point.
- **`MaxRequestBodySize` CLAUDE.md vs code** : résolu, les deux affichent désormais 10 MB.
- **`tickets-tdd-blazor-profil-import.md` inexistant** : résolu, le document existe et fait foi
  pour F1/F2/F3.
- **`UnconditionalColonneNames` absent du modèle de domaine** : résolu, section §1.4 ajoutée.
- **Aucune duplication de code répréhensible trouvée** entre `ImportProfileEditor.razor`/
  `ExportProfileEditor.razor` ni entre les deux pipelines import/export (§5.2) — les similitudes
  structurelles reflètent une convention Blazor commune au projet, pas du code dupliqué qui aurait
  dû être factorisé.
- **Migrations EF Core** : ordre chronologique cohérent de `InitialCreate` à `AddExportProfile`,
  aucun conflit de nom de table, snapshot du modèle à jour.
- **Suite de tests complète exécutée réellement** (pas seulement relue dans les rapports de
  session) : 255+91+135+14+112 = 607 tests .NET 10 + 9 tests legacy .NET Framework 4.8 via
  `ExcelETL.slnx`, plus 6 tests `ExcelETL.Hosting.Tests` exécutés séparément en ciblant son
  `.csproj` (absent de la solution, voir §5.1) — **613/613 verts au total**, tous recomptés
  indépendamment dans cet audit, pas seulement relus dans un rapport de session.
- **POC legacy, sécurité API Key, absence d'exposition HTTP du pipeline OXO** : tous inchangés
  depuis le 17/07, reconfirmés indépendamment (§6).

## Non vérifiable depuis le code seul

- **Application effective des migrations `20260717113850_AddImportProfile` et
  `20260718092214_AddExportProfile` sur une base SQL Server réelle** — toujours non vérifiable
  depuis ce dépôt seul (nécessiterait un accès à l'environnement Windows Server 2022 cible), comme
  au 17/07.
- **Format définitif de `OXO_TRAME_IMPORT_MAD.xlsx`** — dépend d'une confirmation client externe au
  dépôt, toujours non figée.

## 7. Détail de l'exécution des tests (2026-07-19, cet audit)

```
dotnet test ExcelETL.slnx
→ ExcelETL.Domain.Tests.dll        : 255/255 réussis
→ ExcelETL.Application.Tests.dll   :  91/91  réussis
→ ExcelETL.Infrastructure.Tests.dll: 135/135 réussis
→ ExcelETL.WebAPI.Tests.dll        :  14/14  réussis
→ ExcelETL.BlazorAdmin.Tests.dll   : 112/112 réussis
→ Legacy.NewApiPingService.Tests   :   9/9   réussis (net48, hors périmètre OXO)
```

Total confirmé par exécution réelle dans cet audit (hors `ExcelETL.Hosting.Tests`, absent du
fichier solution — voir §5.1) : **607/607 tests verts** via `ExcelETL.slnx`.

`ExcelETL.Hosting.Tests` ne pouvant pas être atteint via la solution, il a été exécuté séparément
dans cet audit en ciblant directement son `.csproj` :

```
dotnet test tests/ExcelETL.Hosting.Tests/ExcelETL.Hosting.Tests.csproj
→ ExcelETL.Hosting.Tests.dll : 6/6 réussis
```

**Total recoupé indépendamment dans cet audit : 613/613 tests verts**, confirmant (sans se fier au
seul rapport de session G3) le chiffre qu'il annonçait.
