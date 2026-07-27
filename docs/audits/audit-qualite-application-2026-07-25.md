# Audit qualité — ExcelETL.Application

- **Date de l'audit** : 2026-07-25
- **Commit réellement audité** : `8119f78` (2026-07-25, "Ajout des audits qualite par couche et de l'etat d'avancement global")
- **Périmètre couvert** : tous les fichiers `.cs` sous `src/ExcelETL.Application/` (hors `bin`/`obj`) et tous les fichiers sous `tests/ExcelETL.Application.Tests/` (hors `bin`/`obj`). Aucune lecture approfondie de Domain/Infrastructure/WebAPI/BlazorAdmin — uniquement grep ciblé ponctuel, signalé comme tel.
- **Méthode** : lecture réelle du code (pas de suppositions), pas d'exécution de `dotnet test`/coverlet — les observations de couverture reposent sur la lecture des fichiers de test, pas sur un rapport chiffré.

---

## 1. Respect de Clean Architecture / Onion

**RAS — aucun manquement détecté.**

- `src/ExcelETL.Application/ExcelETL.Application.csproj` ne référence que : `ProjectReference` → `ExcelETL.Domain` ; `PackageReference` → `Microsoft.Extensions.Localization.Abstractions` (10.0.0), `Microsoft.Extensions.Logging.Abstractions` (10.0.0). Aucune trace de `ClosedXML`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.*` dans le `.csproj` ni dans les `using` de tout le projet.
- Tout accès Excel passe par `IWorkbookReader` (`Extraction/Oxo/IWorkbookReader.cs`) / `IWorkbookWriter` (`Generation/IWorkbookWriter.cs`) — interfaces pures, aucun détail d'implémentation.
- Toute persistance passe par interface : `IImportProfileStore`, `IExportProfileStore`, `IUserRepository`, `IGeneratedFileArchiveStore`, `ISystemLogRepository` — aucune ne fuit de type EF Core.
- `Identity/IdentityOperationResult.cs` est un exemple positif documenté : le type existe précisément pour éviter que `Microsoft.AspNetCore.Identity.IdentityResult` (Infrastructure) ne remonte dans Application.
- Tous les DTOs de résultat (`ImportResult`, `IsolementSheetExtractionResult`, `DiversSheetExtractionResult`, `GeneratedWorkbook`/`GeneratedSheet`/`GeneratedRow`) sont des types Domain/Application purs — aucun type ClosedXML exposé.

**Impact estimé** : n/a (rien à corriger).
**Refacto envisageable** : aucune.

---

## 2. Règles métier câblées en dur vs profile-driven

Catalogue des constantes/règles en dur au-delà des exceptions déjà actées (PROCEDURE header cells, DIVERS `loc1`, echo N6 pour AUTRES JOINTS TOUCHES/DIVERS, `UnknownFieldReferenceException`, "no generic Result pattern") :

- **`ProcedureExtractionService.MapTypeTacheMultipleAlias`** (`Extraction/Oxo/Procedure/ProcedureExtractionService.cs`, lignes ~202-211) : mapping en dur `"MAD" → "TM_PROC_MAD"`, `"REL" → "TM_PROC_REL"`, sinon passthrough. Aucun commentaire dans le fichier n'explique pourquoi ce mapping n'est pas porté par `ImportProfile`, contrairement aux autres choix hardcodés du même fichier (qui sont, eux, justifiés en tête de fichier).
  - **Impact estimé** : faible à court terme (comportement de repli = passthrough du texte brut, pas de crash), mais un futur alias/renommage de code client nécessite une modification de code, pas juste du profil.
  - **Refacto envisageable** : soit documenter explicitement pourquoi ce mapping reste hors profil (dette de documentation), soit le porter dans `ImportProfile` (dette de code) — à trancher côté priorisation, pas à un endroit intermédiaire.
- `ProcedureExtractionService.DateFormats` (ligne ~25) : `["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"]` en dur, non profil-configurable, sans commentaire dédié justifiant l'absence de configurabilité (à la différence d'autres constantes du même fichier).
  - **Impact estimé** : faible — format documenté par le spec source, stable dans les 3 fixtures réelles connues.
  - **Refacto envisageable** : documentation d'intention (pourquoi pas dans le profil) plutôt qu'un changement de code.
- `TargetWorkbookFileNameBuilder` (`Generation/TargetWorkbookFileNameBuilder.cs`, ligne ~19) : préfixe `"MAD_"` en dur dans le format de nommage, non paramétrable par profil, alors que le codebase anticipe déjà ailleurs (commentaires `ProcedureExtractionService`) qu'un futur profil REL pourrait avoir une convention différente.
  - **Impact estimé** : moyen si/quand un profil REL est introduit — bloquant fonctionnellement, pas seulement esthétique.
  - **Refacto envisageable** : paramétrer le préfixe via `ExportProfile` le jour où un second cas d'usage REL apparaît réellement (YAGNI tant qu'aucun besoin concret n'est confirmé).

Le reste des constantes recensées (les 6 noms d'onglets dans `ImportPipelineOrchestrator`, `SheetsProcessedOnSuccess = 6`, `ExcelSheetNameSanitizer.MaxSheetNameLength = 31` + caractères interdits, `TacheMultipleTypeCoherenceAnalyzer`/Lot 032) sont déjà documentées en commentaire comme délibérées — **RAS**, conformes.

**Non couvert / incertain sur ce point** : je n'ai pas vérifié auprès de la documentation métier externe (tickets déjà validés) si le mapping d'alias MAD/REL a déjà été acté ailleurs comme délibéré sans que le commentaire correspondant ait été reporté dans le fichier lui-même.

---

## 3. Duplication

- **`ComposeRepere` dupliquée à l'identique dans 4 services** : `Extraction/Oxo/Isolement/IsolementExtractionService.cs` (lignes ~122-137), `Extraction/Oxo/UnconditionalIsolementSheetExtractionService.cs` (lignes ~56-71), `Extraction/Oxo/AutresJointsTouches/AutresJointsTouchesExtractionService.cs` (lignes ~78-93), `Extraction/Oxo/Divers/DiversExtractionService.cs` (lignes ~77-92). Même séquence de ~15 lignes (construction `Dictionary` + `Concat`/`FieldRef`/`Literal` + `textTransformEvaluator.Evaluate`) répétée sans facteur commun, et sans commentaire expliquant pourquoi cette duplication précise a été acceptée — contrairement à d'autres duplications volontaires documentées ailleurs dans le projet.
  - **Impact estimé** : moyen — une évolution de la règle de composition du repère (`{EquipementRepere}-{Identification}`) exige 4 modifications synchronisées ; risque de divergence silencieuse déjà réel si l'une des 4 copies est modifiée sans les 3 autres.
  - **Refacto envisageable** : extraire une méthode statique partagée (ex. `RepereComposer.Compose(ITextTransformEvaluator, string equipementRepere, string identification)`) dans `Extraction.Oxo`, réutilisée par les 4 services.
- **Squelette d'extraction similaire entre `AutresJointsTouchesExtractionService.Extract` et `DiversExtractionService.Extract`** : lecture du bloc via `IRepeatingBlockReader` → groupement `PointRules.GroupBy(r => r.ColonneName)` → boucle avec `ComposeRepere` → `ConditionalPointGroupEvaluator.Evaluate` → log warning. Diffère seulement dans l'agrégation finale des points (Divers fusionne `UnconditionalColonneNames`+colonnes conditionnelles en une boucle, AutresJointsTouches les sépare en deux).
  - **Impact estimé** : faible à moyen — squelette non factorisé, mais différences suffisamment réelles pour qu'une factorisation forcée risque de nuire à la lisibilité (`IsolementExtractionService` a une structure proche mais légitimement différente, déjà documentée).
  - **Refacto envisageable** : à évaluer seulement si un 3e service au même squelette apparaît — prématuré aujourd'hui avec seulement 2 occurrences très proches.
- **`SheetGenerationEngine.GenerateEquipementRows`/`GenerateIsolementRows`** (lignes ~103-119 / ~121-132) : construction quasi identique des 3 groupes de cellules (`descriptiveCells`/`applicationCells`/`pointCells`), seule la source de boucle change (entité unique vs liste).
  - **Impact estimé** : faible — code court des deux côtés, mais toute évolution des règles d'affichage Point/Application doit être répercutée aux deux endroits.
  - **Refacto envisageable** : méthode générique `BuildRow<T>(SheetGenerationRule, T entity, points, repere, applications, resolver)` — à envisager seulement si une 3e variante de ligne apparaît, sinon la duplication actuelle reste lisible.
- **Pas de duplication détectée** entre les branches `TacheMultiple` et `Equipement`/`Isolement` de `SheetGenerationEngine` — `GenerateTacheMultipleSheets` est structurellement différente et documentée comme délibérément spécifique.

---

## 4. Cohérence des conventions déjà actées

- **Nommage cohérent** dans l'ensemble : `{Sheet}ExtractionService`/`I{Sheet}ExtractionService`, `{Sheet}SheetExtractionResult` (sauf `ImportResult` pour PROCEDURE, motivé), classes `{Sheet}FieldNames`.
- **`UnconditionalIsolementSheetExtractionService`** casse le pattern `{Sheet}ExtractionService` (elle sert à la fois PLATINES et ORIFICES CAPACITES) — documenté et volontaire en tête de fichier, donc **pas une incohérence de fait**, mais un nom générique qui rompt la régularité visuelle de la liste de fichiers dans `Extraction/Oxo/`.
  - **Impact estimé** : cosmétique, aucune ambiguïté fonctionnelle.
  - **Refacto envisageable** : aucune — le nom reflète correctement la réalité (un seul service pour deux feuilles).
- **Couplage de nommage entre dossiers** : `IsolementFieldNames` vit dans le sous-dossier `Extraction/Oxo/Isolement/` mais est importée et réutilisée par 3 autres services hors de ce sous-dossier (`AutresJointsTouchesExtractionService`, `UnconditionalIsolementSheetExtractionService`, `DiversExtractionService`) — un type nominalement "propre à ISOLEMENT" est en réalité un vocabulaire transverse aux 5 feuilles isolement-like.
  - **Impact estimé** : faible — fonctionne correctement, mais surprenant pour un lecteur qui s'attend à ce que le contenu d'un sous-dossier par feuille reste local à cette feuille.
  - **Refacto envisageable** : déplacer `IsolementFieldNames` directement sous `Extraction/Oxo/` si une clarification de structure est jugée utile — non bloquant.
- `Generation/` reste plate (pas de sous-dossiers), cohérent avec un moteur de génération unique.

---

## 5. Dette de test

**Volume de tests par service (lignes de test)** — `ProcedureExtractionServiceTests.cs` 411, `ProcessOxoFileServiceTests.cs` 378, `SheetGenerationEngineTests.cs` 385, `ImportPipelineOrchestratorTests.cs` 268, `IsolementExtractionServiceTests.cs` 225, `TacheMultipleTypeCoherenceAnalyzerTests.cs` 173, `UnconditionalIsolementSheetExtractionServiceTests.cs` 159, `DiversExtractionServiceTests.cs` 155, `AutresJointsTouchesExtractionServiceTests.cs` 137, `ConditionalPointRuleEvaluatorTests.cs` 148, `RepeatingBlockReaderTests.cs` 133, `TacheMultipleSectionGrouperTests.cs` 115, `TextTransformEvaluatorTests.cs` 91, `BusinessExceptionLocalizerTests.cs` 60, `ExcelSheetNameSanitizerTests.cs` 36, `ProfileNameAlreadyExistsExceptionTests.cs` 26, `TargetWorkbookFileNameBuilderTests.cs` 29. Total projet Application au dernier commit connu (CLAUDE.md) : 152 tests.

**Trous de couverture identifiés :**

- **`Extraction/Oxo/ConditionalPointGroupEvaluator.cs`** : aucun fichier de test dédié. Sa logique d'agrégation ("le warning ne remonte que si *aucun* groupe conditionnel ne matche") n'est exercée qu'indirectement via `IsolementExtractionServiceTests`/`AutresJointsTouchesExtractionServiceTests`/`DiversExtractionServiceTests`.
  - **Impact estimé** : moyen — c'est précisément la classe dont le commentaire souligne qu'elle importe surtout quand une feuille a plusieurs types conditionnels mutuellement exclusifs (DIVERS) ; le cas limite "deux groupes matchent simultanément" n'est testé nulle part au niveau unitaire pur.
  - **Refacto envisageable** : créer `ConditionalPointGroupEvaluatorTests.cs` couvrant explicitement le cas multi-groupes.
- **`Extraction/Oxo/BlockFieldRangeCalculator.cs`** : pas de fichier de test dédié, exercé seulement indirectement via `RepeatingBlockReaderTests`/`ProcedureExtractionServiceTests`.
  - **Impact estimé** : faible (fonction pure simple), mais le cas mono-colonne isolé (`"A"` sans `:`) n'est jamais testé explicitement en isolation.
  - **Refacto envisageable** : test unitaire dédié si cette fonction évolue.
- **Incohérence de couverture entre exceptions similaires** : `ProfileNameAlreadyExistsExceptionTests.cs` teste explicitement les propriétés (`ErrorCode`, `Args`, `ResourceKey`) de l'exception, alors qu'`ImportProfileNotFoundException`/`ExportProfileNotFoundException`/`UnknownFieldReferenceException` n'ont pas d'équivalent — elles ne sont vérifiées qu'indirectement par leur levée dans `ProcessOxoFileServiceTests`/`ConditionalPointRuleEvaluatorTests`.
  - **Impact estimé** : faible — comportement fonctionnel déjà couvert, mais divergence de convention de test entre exceptions du même projet.
  - **Refacto envisageable** : aligner sur le modèle `ProfileNameAlreadyExistsExceptionTests` si une régularisation des tests d'exception est priorisée, sinon acceptable tel quel.
- **`IWorkbookReader.SheetExists`** : déclarée dans l'interface mais **aucun des 5 services d'extraction ne l'appelle** (vérifié par recherche exhaustive dans `Extraction/Oxo/*ExtractionService.cs`). Aucun test Application ne modélise le cas "feuille manquante" via `SheetExists`.
  - **Impact estimé** : incertain — `WorksheetNotFoundInWorkbookException` existe et suggère que ce cas est géré quelque part, mais son point de levée n'a pas été localisé dans le périmètre Application audité.
  - **Refacto envisageable** : à vérifier côté Infrastructure/WebAPI (hors périmètre de cet audit) avant toute action — reporté en "Non couvert/incertain".

**Qualité des doubles de test (mocks)** : le pattern dominant (`Mock<IWorkbookReader>` avec dictionnaire `(sheet, range) → string?`) est fidèle au contrat réel de l'interface (`ReadCellValue` nullable) — pas de sur-permissivité identifiée. Les mocks des 5 interfaces de service dans `ImportPipelineOrchestratorTests` correspondent exactement aux signatures réelles, sans `It.IsAny<>` généralisé masquant une régression de contrat. **Aucune zone où le double de test pourrait diverger silencieusement du comportement réel n'a été identifiée avec certitude** dans ce projet, à l'exception du point `SheetExists` ci-dessus qui reste non tranché.

---

## 6. Gestion des erreurs et logs

**RAS globalement — cohérence confirmée.**

- `ExtractionErrorCode` est utilisé de façon cohérente et centralisée via `Extraction/Oxo/ExtractionErrorLogging.Log` : `UnrecognizedTypeElement`/`TypeIncoherenceDansTacheMultiple` → `Warning`, `RequiredFieldMissing`/`UnparsableValue` → `Error`. Chaque service appelle systématiquement cette méthode ; `RepeatingBlockReader.Read` construit les erreurs sans les logger lui-même, délégué à l'appelant — cohérent et intentionnel (le composant générique ne connaît pas de logger).
- **Un seul `catch` "avale" une exception sans la relancer** : `ProcessOxoFileService.TryArchiveAsync` (lignes ~106-149), `catch (Exception ex)` qui logge en `Error` puis ne relance pas — **explicitement documenté** ("best-effort, ne doit jamais faire échouer la réponse HTTP") et couvert par 3 tests dédiés (`ProcessAsync_WhenArchiveStoreThrows_StillReturnsSuccessfulResult`, etc.). Ce n'est donc pas un avalage silencieux au sens négatif (loggé + testé), conforme à une décision déjà actée.
- Aucun autre `catch` vide ou sans log détecté ; les `catch (Exception ex)` de `ImportPipelineOrchestrator.Run` et `ProcessOxoFileService.ProcessAsync` logguent puis relancent (`throw;`).
- Aucun mécanisme de logging parallèle : `ILogger<T>` injecté par classe uniquement, aucune trace de `Console.WriteLine`/`Debug.WriteLine`/écriture fichier manuelle/second logger. `Diagnostics/ISystemLogRepository` est une lecture de logs déjà persistés (pas un canal d'écriture concurrent).
- `UnknownFieldReferenceException` levée de façon cohérente aux deux seuls points d'appel (`ConditionalPointRuleEvaluator.Matches`, `TextTransformEvaluator.EvaluateConcat`), conformément à la décision déjà actée (bug de profil, pas de donnée).

**Impact estimé** : n/a.
**Refacto envisageable** : aucune.

---

## 7. Lisibilité / complexité

- **`SheetGenerationEngine`** (142 lignes) : complexité raisonnable, bien découpée. Le `switch` sur `PivotSource` suit le pattern `ArgumentOutOfRangeException` standard. Rien d'anormal.
- **`ImportPipelineOrchestrator`** (141 lignes) : `Run` est longue (~90 lignes) mais linéaire, complexité cyclomatique faible malgré la longueur (agrégation répétitive `AddRange` + boucle de broadcast `Localisation`/`Tableaux`/`Applications`/`RepereParent`, lignes ~91-100).
  - **Impact estimé** : faible — lisibilité perfectible mais pas de risque fonctionnel.
  - **Refacto envisageable** : extraire la boucle de broadcast en méthode nommée (`BroadcastEquipementContext`) — amélioration cosmétique, pas urgente.
- **`ProcedureExtractionService`** (236 lignes, le plus complexe du projet) : complexité justifiée et documentée en tête de fichier (seule feuille sans `IRepeatingBlockReader`, + détection d'incohérence Lot 032) ; la complexité additionnelle est déléguée à des classes séparées (`TacheMultipleSectionGrouper`, `TacheMultipleTypeCoherenceAnalyzer`), donc contenue.
- **`ProcessOxoFileService`** (152 lignes) : complexité proportionnée à son rôle d'orchestrateur haut niveau. Le double mécanisme d'archivage (`IFileStorageService` pré-existant + `IGeneratedFileWriter`/`IGeneratedFileArchiveStore` du Lot 034) est **une duplication fonctionnelle réelle**, explicitement documentée comme voulue pour préserver un test d'intégration existant côté WebAPI (hors périmètre).
  - **Impact estimé** : moyen à terme — un futur changement de format d'archivage doit être fait à deux endroits distincts, avec des sémantiques de statut légèrement différentes (`IFileStorageService.SaveAsync` toujours appelé même si `HasErrors`, vs. le second mécanisme qui distingue `NonBlockingWarning`) ; risque de confusion si un consommateur mélange les deux notions de statut.
  - **Refacto envisageable** : unifier les deux mécanismes d'archivage dans un futur lot, une fois le test d'intégration WebAPI concerné ré-évalué — pas à faire à la légère, dépendance croisée hors périmètre.

Aucun service n'est disproportionné par rapport à sa charge fonctionnelle documentée ; toute la complexité "élevée" trouvée est expliquée par un commentaire en tête de fichier.

---

## Hors périmètre — observé en passant

- Le mécanisme réel de levée de `WorksheetNotFoundInWorkbookException` (cf. §5) n'a pas pu être localisé dans Application — probablement en Infrastructure (`ClosedXmlWorkbookReader`) ou WebAPI.
- Les invariants de construction des pivots/`ImportProfile`/`ExportProfile` (Domain) n'ont pas été vérifiés en détail ; certaines règles classées "en dur dans Application" (§2) pourraient recouper des invariants Domain non audités ici.

---

## Non couvert / incertain

- Aucune exécution de `dotnet test`/mesure de couverture chiffrée (coverlet) — les estimations de couverture reposent uniquement sur la lecture des fichiers de test.
- `Resources/ApplicationMessages.resx`/`.fr.resx` et `DomainErrorMessages.*.resx` n'ont pas été ouverts en détail — pas de vérification que chaque `ApplicationErrorCode` a bien une clé de ressource dans les deux langues.
- Le statut "délibéré ou non" du mapping d'alias `MapTypeTacheMultipleAlias` (§2) repose uniquement sur l'absence de commentaire dans le fichier lui-même, pas sur une vérification de la documentation métier externe (tickets déjà validés).
- Le point d'usage réel (ou l'absence d'usage) de `IWorkbookReader.SheetExists` en dehors du périmètre Application (§5) n'a pas été tranché.
- Les tests d'intégration réels contre fixtures Excel (`ProcedureExtractionServiceIntegrationTests` et équivalents) vivent dans `ExcelETL.Infrastructure.Tests`, hors périmètre — non lus, donc impossible de confirmer si le cas limite `ConditionalPointGroupEvaluator` multi-groupes (§5) y est couvert malgré l'absence de test unitaire pur.

## Ce que cet audit ne déclenche pas

Aucun refacto listé ci-dessus n'est engagé avant relecture/priorisation par Claude AI, validation explicite de Simon, puis ticket TDD dédié.
