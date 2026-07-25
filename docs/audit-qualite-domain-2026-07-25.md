# Audit qualité — ExcelETL.Domain

**Commit réellement audité** : `8119f78` (2026-07-25, "Ajout des audits qualite par couche et de
l'etat d'avancement global"), branche `main`. Le commit de référence indiqué dans la demande
(`d018a90`, 2026-07-24) est un ancêtre direct de celui-ci ; `HEAD` était plus récent au moment de
l'audit, donc c'est `8119f78` qui a réellement été lu.

**Méthode** : lecture intégrale des 30 fichiers source de `src/ExcelETL.Domain` (hors
`obj/`/`bin/`) et des 24 fichiers de `tests/ExcelETL.Domain.Tests`, plus quelques `grep` ciblés
sur `src/ExcelETL.Application` (et uniquement là) pour vérifier si un type Domain donné est
réellement construit hors du Domain — jamais pour lire de la logique métier Application. Aucune
exécution de tests n'a été effectuée dans le cadre de cet audit (le confort factuel repose sur la
lecture du code, pas sur une ré-exécution de la suite).

**Confirmation périmètre (critère 1)** : `src/ExcelETL.Domain/ExcelETL.Domain.csproj` ne
contient ni `PackageReference` ni `ProjectReference` — confirmé en tête de fichier, RAS.

---

## 1. Respect de Clean Architecture / Onion

**RAS sur l'isolation du projet.** Zéro dépendance externe, zéro référence à ClosedXML, EF Core,
ou tout autre détail d'infrastructure. Les commentaires du code documentent explicitement, à
plusieurs endroits, les contraintes EF Core (impossibilité de constructor-binder une navigation
de collection) sans jamais y référencer le framework directement — la connaissance de la
contrainte est présente, mais aucune dépendance n'est introduite pour autant. C'est le pattern
attendu et il est appliqué de façon cohérente sur `RepeatingBlockLocator.Fields`,
`SheetExtractionRule.PointRules`, `ImportProfile.SheetRules`, `SheetGenerationRule` (3
collections), `ExportProfile.SheetRules`.

- **Localisation** : `Extraction/Primitives/RepeatingBlockLocator.cs`, `Extraction/Profile/ImportProfile.cs`,
  `Extraction/Profile/SheetExtractionRule.cs`, `Generation/Profile/SheetGenerationRule.cs`,
  `Generation/Profile/ExportProfile.cs`.
- **Constat factuel** : chacun de ces 5 types expose un constructeur privé sans validation
  (« EF Core materialization only ») en plus de son constructeur public validant, et bascule sa
  collection derrière un champ privé `List<T>` + une propriété `IReadOnlyList<T>` en lecture
  seule. C'est un pattern répété à l'identique 5 fois, avec le même commentaire copié-collé
  (« See RepeatingBlockLocator.Fields... »).
- **Impact estimé** : cosmétique. Le pattern est correct et bien documenté (chaque occurrence
  renvoie vers l'original par commentaire plutôt que de dupliquer l'explication en clair), mais
  c'est un boilerplate mécanique qui grandit à chaque nouveau type à collection.
- **Refacto envisageable** : aucune action nécessaire à ce stade — une factorisation
  générique (ex. une classe de base `EntityCollectionBackedRecord<T>`) ajouterait de
  l'indirection pour un gain marginal sur seulement 5 occurrences. À reconsidérer seulement si un
  6e/7e cas apparaît.

**Aucune fuite de détail Excel/ClosedXML dans le Domain.** Les seules références au format Excel
sont des règles de validation de forme (regex sur lettres de colonnes/lignes dans `DirectCell` et
`BlockFieldDefinition`) — c'est un vocabulaire métier légitime du domaine (le modèle pivote
justement autour de coordonnées de classeur), pas une fuite d'infrastructure.

---

## 2. Règles métier câblées en dur vs profile-driven

**RAS pour tout ce qui est structurellement représentable dans `ImportProfile`/`ExportProfile`.**
`ReperePrefix`, `EquipementTypeElementNom`, `DefaultTableaux`, `DefaultApplicationNames`, la liste
de `SheetExtractionRule` (avec `UnconditionalColonneNames`/`PointRules` par feuille) sont tous
portés par le profil, jamais par une constante Domain. `ImportProfile.DefaultReperePrefix =
"MAD-OXO-"` est la seule constante métier du Domain, et elle n'est qu'une valeur *par défaut* pour
un des constructeurs publics (le profil reste libre de la surcharger) — conforme à l'intention
documentée ("paramétrable, défaut MAD-OXO-").

- **Localisation** : `Extraction/Profile/ImportProfile.cs:12` (`DefaultReperePrefix`).
- **Constat factuel** : usage conforme, RAS.

**Hors périmètre — observé en passant (une ligne, non approfondi)** : le doc `CLAUDE.md` indique
que les coordonnées d'en-tête de PROCEDURE (`M2:O2`, `P2:Q2`, `R2:T2`) et l'écho de repère `N6`
d'AUTRES JOINTS TOUCHES/DIVERS sont câblés en dur dans les services Application
(`ProcedureExtractionService`, etc.) plutôt que portés par `SheetExtractionRule`/`DirectCell` —
c'est exactement le type d'écart que ce critère demande de signaler, mais le Domain expose déjà
le type `DirectCell` qui aurait pu porter cette configuration (voir §3, `DirectCell` est
d'ailleurs mort côté Domain lui-même) ; le fait que le pipeline ne l'utilise pas est un choix
Application non audité ici en détail.

**Cas déjà actés comme exceptions volontaires (PROCEDURE, DIVERS) : pas de pendant Domain
correspondant à signaler.** Le Domain ne porte aucune règle PROCEDURE/DIVERS-spécifique en dur —
`TacheMultiplePivot` et `EquipementPivot`/`IsolementPivot` restent génériques, ce qui est cohérent
avec le fait que ces exceptions sont assumées comme relevant de l'Application (RAS, rien à
signaler comme défaut).

---

## 3. Duplication

### 3.1 — `DirectCell` : primitive du modèle jamais utilisée en dehors de ses propres tests

- **Localisation** : `Extraction/Primitives/DirectCell.cs` (classe entière, 34 lignes).
- **Constat factuel** : `grep -r "new DirectCell"` sur l'ensemble du repository (tous projets,
  fichiers `.cs` uniquement) ne renvoie que des occurrences dans
  `tests/ExcelETL.Domain.Tests/Extraction/Primitives/DirectCellTests.cs`. Aucun type Domain
  (`SheetExtractionRule`, `ImportProfile`, etc.) n'a de propriété de type `DirectCell`, et aucun
  service Application n'en construit une — les lectures de cellule d'en-tête (`M2:O2` etc., voir
  §2) sont faites par lecture directe de plage codée en dur dans les services, sans jamais passer
  par ce type. `DirectCell_EmptySheet`/`DirectCell_InvalidRange` dans `DomainErrorCode` ne sont
  donc eux non plus jamais levés en dehors des 10 tests unitaires dédiés.
- **Impact estimé** : dette légère, pas cosmétique — ce n'est pas juste un nommage à revoir,
  c'est un type public entièrement construit (validation, égalité structurelle, 2 codes
  d'erreur dédiés, 10 cas de test) qui ne sert à rien dans le pipeline réel actuel. Un lecteur du
  Domain qui cherche à comprendre "comment une cellule directe est modélisée" trouvera ce type et
  conclura à tort qu'il est le mécanisme utilisé.
- **Refacto envisageable** : soit le supprimer complètement (avec ses 2 `DomainErrorCode` et ses
  tests) si aucun usage n'est prévu à court terme, soit — si l'intention reste de migrer un jour
  les coordonnées d'en-tête PROCEDURE/AJT/DIVERS vers `SheetExtractionRule` (cf. §2) — le garder
  mais le documenter explicitement comme "prêt mais pas encore câblé", ce qu'aucun commentaire ne
  dit actuellement (le commentaire d'en-tête du fichier ne mentionne aucun statut d'usage).

### 3.2 — 6 implémentations manuelles quasi identiques d'`Equals`/`GetHashCode` pour égalité structurelle de listes

- **Localisation** : `Extraction/Pivot/EquipementPivot.cs`, `Extraction/Pivot/IsolementPivot.cs`,
  `Extraction/Primitives/RepeatingBlockLocator.cs`, `Extraction/Primitives/Concat.cs`,
  `Generation/Profile/SheetGenerationRule.cs`, `Generation/Profile/ExportProfile.cs`.
- **Constat factuel** : chacun de ces 6 `record` a au moins une propriété `IReadOnlyList<T>` et
  réimplémente à la main `bool Equals(T? other)` (comparant chaque scalaire puis
  `.SequenceEqual(...)` par liste) et `override int GetHashCode()` (un `HashCode` accumulé
  scalaire par scalaire puis élément par élément par `foreach`). Le corps de ces 12 méthodes suit
  la même structure mécanique à chaque fois — seuls les noms de propriétés changent.
- **Impact estimé** : cosmétique à dette légère. Le pattern est correct (nécessaire car
  l'égalité de record générée par le compilateur compare les `IReadOnlyList<T>` par référence, pas
  par contenu — chaque commentaire le rappelle correctement), mais c'est ~150 lignes de
  boilerplate répété qui grandiront à chaque futur type à liste.
- **Refacto envisageable** : un helper statique générique dans `Common/`
  (ex. `SequenceEqualityHelpers.CombineHash(HashCode, IEnumerable<T>)` et une extension
  `.SequenceEqualOrdered(...)`) réduirait chaque implémentation à quelques lignes sans changer la
  sémantique. Non-urgent, à faire seulement si un 7e cas apparaît ou lors d'un prochain passage
  sur ces fichiers.

### 3.3 — Constantes dupliquées littéralement entre deux types frères

- **Localisation** : `Extraction/Profile/ImportProfile.cs:13` et
  `Generation/Profile/ExportProfile.cs:13` (`public const int MaxNameLength = 60;` dans les deux) ;
  `Generation/Profile/PointColumnDefinition.cs:12` et
  `Generation/Profile/ApplicationColumnDefinition.cs:12` (`public const string DefaultMarkValue =
  "X";` dans les deux).
- **Constat factuel** : même nom de constante, même valeur, déclarés indépendamment dans deux
  types structurellement proches mais sans relation d'héritage.
- **Impact estimé** : cosmétique. Un changement de règle métier (ex. "60" → "80" caractères pour
  le nom d'un profil) devrait être répercuté manuellement aux deux endroits ; le risque de dérive
  silencieuse est réel mais faible (deux fichiers, proches, déjà repérés côté tests par
  `ProfileEditorParityTests.cs` selon `CLAUDE.md`).
- **Refacto envisageable** : déplacer les 2 constantes vers une classe statique partagée
  (ex. `Common/ProfileNaming.cs` avec `MaxNameLength`) et une pour `DefaultMarkValue`
  (`Common/ColumnDefaults.cs` ou similaire) référencée par les 2 types Point/Application. Gain
  réel mais mineur — à faire opportunément, pas prioritaire seul.

### 3.4 — `ConditionalPointRule` vs `UnconditionalColonneNames` : duplication conceptuelle, pas mécanique

- **Localisation** : `Extraction/Primitives/ConditionalPointRule.cs` (record à 4 champs) vs
  `Extraction/Profile/SheetExtractionRule.UnconditionalColonneNames` (`IReadOnlyList<string>`
  brute, sans type dédié).
- **Constat factuel** : le commentaire de `SheetExtractionRule` explique lui-même pourquoi les
  deux coexistent (`ConditionalPointRule` "carries a real SourceFieldName/ComparisonValue,
  there's no 'no condition' sentinel"). Il ne s'agit donc pas d'un doublon de code — aucune
  logique n'est dupliquée entre les deux — mais d'une asymétrie de modélisation : une Colonne
  inconditionnelle est une simple `string`, tandis qu'une Colonne conditionnelle est un objet à 4
  champs. Un lecteur découvrant le modèle doit connaître cette distinction pour comprendre
  pourquoi il existe deux mécanismes séparés au lieu d'un seul type avec condition optionnelle
  (ex. `ConditionalPointRule` avec `SourceFieldName`/`ComparisonValue` nullable).
- **Impact estimé** : cosmétique — la modélisation actuelle fonctionne et est déjà bien
  documentée in situ.
- **Refacto envisageable** : aucune action recommandée. Fusionner les deux forcerait
  `ConditionalPointRule` à accepter un état "sans condition" que son constructeur rejette
  aujourd'hui explicitement (`ConditionalPointRule_EmptySourceFieldName` etc.) — ce serait une
  perte de clarté métier pour gagner un seul type au lieu de deux, l'inverse de ce que demande la
  question posée par le critère. Signalé ici pour mémoire, pas comme un défaut.

### 3.5 — Pas de duplication problématique entre `EquipementPivot`/`IsolementPivot`/`TacheMultiplePivot`

Les 3 pivots partagent le même mécanisme de "broadcast" différé (`Localisation`/`Tableaux`/
`Applications` en `init`, remplis a posteriori via `with`), mais chacun a un jeu de champs requis
distinct (`EquipementPivot` exige `Repere`+`Designation`+`TypeElementNom` ; `IsolementPivot`
n'exige que `Repere`+`TypeElementNom` ; `TacheMultiplePivot` n'exige qu'`Action`). C'est une
duplication de *forme* (le pattern broadcast répété, déjà couvert en §3.2) mais pas de *règle
métier* — chaque type encode une invariant différente et légitime. RAS au-delà de ce qui est déjà
signalé en §3.2.

---

## 4. Cohérence des conventions déjà actées

### 4.1 — Trois patterns d'identité différents pour 3 types porteurs d'un `Guid Id`, un seul documenté comme délibéré

- **Localisation** : `Extraction/Profile/ImportProfile.cs` (hérite de `Common/Entity.cs`,
  égalité par identité) ; `Generation/Profile/ExportProfile.cs` (record, `Guid Id` en propriété
  plate, égalité structurelle — **explicitement documenté** comme un choix délibéré : "this lot's
  ticket explicitly asks for record (structural) equality... which would conflict with Entity's
  identity-only Equals") ; `Archiving/GeneratedFileRecord.cs` (`sealed class` avec son propre
  `Guid Id` en propriété plate, **aucun** `Equals`/`GetHashCode` réécrit — égalité par défaut,
  c'est-à-dire égalité par référence).
- **Constat factuel** : `GeneratedFileRecord` a la même forme qu'`ExportProfile` (Guid Id
  indépendant d'`Entity`) mais, contrairement à celui-ci, ne documente aucune raison de ne pas
  hériter d'`Entity`, et n'implémente pas non plus une égalité structurelle comme `ExportProfile`
  le fait. Deux instances de `GeneratedFileRecord` représentant la même ligne (même `Id`, mêmes
  valeurs) rechargées séparément depuis le store ne seraient jamais `Equals` l'une de l'autre.
- **Impact estimé** : dette légère. Aucun bug connu n'en découle aujourd'hui (les tests
  `GeneratedFileRecordTests.cs` ne testent d'ailleurs pas l'égalité — cf. §5), mais c'est un
  troisième pattern d'identité sans justification écrite, alors que ce projet a pris l'habitude de
  documenter explicitement ce genre d'écart (voir la justification donnée pour `ExportProfile`).
- **Refacto envisageable** : soit faire hériter `GeneratedFileRecord` de `Entity` (le cas d'usage
  — un enregistrement d'archive avec identité stable chargé depuis un store — correspond
  exactement à ce pour quoi `Entity` existe), soit ajouter un commentaire équivalent à celui
  d'`ExportProfile` expliquant pourquoi ce n'est délibérément pas le cas.

### 4.2 — Nommage des membres d'énumération : anglais partout, sauf un membre récent en français

- **Localisation** : `Extraction/Pivot/ExtractionErrorCode.cs`.
- **Constat factuel** : `RequiredFieldMissing`, `UnparsableValue`, `UnrecognizedTypeElement`
  (anglais) cohabitent avec `TypeIncoherenceDansTacheMultiple` (français, ajouté au Lot 032 selon
  `CLAUDE.md`). C'est la seule enum de tout le Domain dont un membre n'est pas en anglais — tous
  les autres noms de types/membres du Domain (`DomainErrorCode` compris, y compris ses entrées
  ajoutées le même jour pour le même lot) restent en anglais, le vocabulaire métier français
  n'apparaissant que dans les chaînes de caractères (`Message`, noms de colonnes, etc.), jamais
  dans les identifiants C#.
- **Impact estimé** : cosmétique — aucun impact fonctionnel, mais c'est une incohérence de
  convention détectable en une lecture, sur un point que le projet a été rigoureux à maintenir
  partout ailleurs (voir aussi le glossaire EF6 legacy évoqué dans `CLAUDE.md`, qui documente
  justement les correspondances de vocabulaire pour éviter ce genre de dérive).
  `DomainErrorCode` n'a pas ce problème : ses membres restent tous en anglais
  (`SheetGenerationRule_DuplicateApplicationNom`, etc.), y compris ceux liés au même Lot 032/034.
- **Refacto envisageable** : renommer en `TacheMultipleTypeIncoherence` ou
  `TacheMultipleTypeMismatch` (nécessite une passe Application pour les points de construction —
  `TacheMultipleTypeCoherenceAnalyzer` selon `CLAUDE.md` — donc hors du seul Domain, à faire dans
  un ticket dédié touchant les deux couches).

### 4.3 — RAS ailleurs sur le nommage

Le reste du vocabulaire (`Repere`, `Localisation`, `TypeElementNom`, `ColonneNom`, `Designation`,
`PositionALaPose`) est cohérent avec le vocabulaire métier documenté ailleurs dans le repo
(glossaire EF6 legacy cité dans `CLAUDE.md`) et strictement stable dans le temps (aucune
incohérence de casse ou de synonyme trouvée entre pivots — ex. `TypeElementNom` est identique sur
`EquipementPivot` et `IsolementPivot`, jamais `TypeElementCode` malgré le nom historique évoqué en
commentaire).

---

## 5. Dette de test

**Volume global** : 175 méthodes `[Fact]`/`[Theory]` (dont 146 cas `[InlineData]` supplémentaires
sur les `[Theory]`), soit un total cohérent avec les ~264-278 tests annoncés dans `CLAUDE.md` pour
ce projet aux lots récents. Pas de mesure de couverture ligne/branche disponible (aucun outil de
coverage exécuté dans le cadre de cet audit) — l'estimation ci-dessous est faite par comptage de
cas de test rapportés à la complexité du type testé, pas par un outil.

### 5.1 — Écart net de couverture entre types triviaux et types métier riches

| Fichier testé | Cas de test | Constat |
|---|---|---|
| `RawValueTests.cs` | 1 | Type marqueur vide (`sealed record RawValue : TextTransform;`), 1 cas suffit — RAS. |
| `LiteralTests.cs` | 1 | Idem, `record Literal(string Text)` — RAS. |
| `DirectCellTests.cs` | 10 (4 méthodes + InlineData) | Voir §3.1 — bien testé pour un type mort. |
| `ImportResultTests.cs` | 3 | Voir §5.2 ci-dessous — sous-couvert par rapport à sa complexité réelle. |
| `PivotFieldResolverTests.cs` | 25 méthodes | Le fichier le mieux couvert du projet — cohérent avec le fait que `Resolve`/`GetPivotSource` a 3 `switch` à 5-7 branches chacun, tous couverts individuellement. |
| `SheetGenerationRuleTests.cs` | 21 méthodes | Deuxième fichier le mieux couvert — cohérent avec la richesse réelle du constructeur (5 règles de validation croisées, voir §7.1). |
| `ImportProfileTests.cs` | 16 méthodes | Bien couvert. |

Le classement global des fichiers de test suit assez fidèlement la complexité réelle des types
qu'ils couvrent (`PivotFieldResolver`/`SheetGenerationRule`/`ImportProfile` en tête, primitives
triviales en bas) — **RAS sur la tendance générale**, pas d'entité manifestement sous-testée par
rapport à ses voisines de complexité comparable, à l'exception du cas ci-dessous.

### 5.2 — `ImportResultTests.cs` ne couvre pas l'invariant documenté comme volontairement absent

- **Localisation** : `tests/ExcelETL.Domain.Tests/Extraction/Pivot/ImportResultTests.cs` (3
  tests : succès, erreurs, collections nulles).
- **Constat factuel** : le commentaire de `ImportResult.cs` dit explicitement "Whether the other
  collections are actually empty in that case [Equipement null] is enforced by the orchestrator
  (Lot D), not here." Aucun test de `ImportResultTests.cs` ne construit le cas `Equipement: null`
  avec des collections *non vides* pour vérifier que le constructeur l'accepte effectivement sans
  lever d'exception (ce qui est le comportement voulu, mais non testé positivement — seul le cas
  `null` + collections vides est couvert, ligne 32).
- **Impact estimé** : dette légère. Ce n'est pas un bug (le comportement actuel est conforme à
  l'intention documentée), mais l'absence de ce cas de test rend l'invariant "le Domain n'impose
  rien ici, c'est un choix" difficile à distinguer d'un simple oubli pour un lecteur qui ne lirait
  que les tests sans le commentaire du modèle. C'est aussi exactement le genre de garde qui, si un
  jour quelqu'un décide de la faire remonter dans le Domain (cf. §6.1), cassera silencieusement ce
  test-ci s'il n'existe pas déjà pour documenter le comportement actuel.
- **Refacto envisageable** : ajouter un test
  `Constructor_WithNullEquipementAndNonEmptyCollections_DoesNotThrow` avec un commentaire renvoyant
  vers le doc de modèle, pour documenter explicitement le choix au niveau test et pas seulement au
  niveau commentaire de production.

### 5.3 — Tests qui testent l'implémentation plutôt que l'invariant métier

Aucun cas trouvé de test couplé à un détail d'implémentation interne (pas de test sur un champ
privé, pas de test sur l'ordre d'évaluation des validations au-delà de ce qui est observable via
l'exception levée). Les tests lus (`DirectCellTests`, `ImportResultTests`,
`BlockFieldDefinitionTests` par échantillonnage) vérifient systématiquement soit une propriété
publique, soit un `DomainErrorCode`/type d'exception observable — RAS sur ce point précis.

---

## 6. Gestion des erreurs

**RAS sur la cohérence globale du système d'erreurs.** `IHasDomainErrorCode` +
`DomainValidationException`/`DomainArgumentOutOfRangeException`/`DomainRuleViolationException`
forment un triptyque cohérent, chaque exception portant `ErrorCode` + `Args` sans jamais dépendre
d'un framework de localisation (conforme à la règle Clean Architecture documentée dans
`CLAUDE.md`). Les erreurs de garde développeur (`ArgumentNullException.ThrowIfNull`,
`ArgumentException.ThrowIfNullOrWhiteSpace` sur `GeneratedFileRecord`) restent cohérentes avec la
règle de périmètre i18n documentée (pas de code métier localisable pour des invariants
non-utilisateur).

### 6.1 — Un seul cas de "logique de validation qui devrait être une garde de constructeur mais ne l'est pas", déjà connu et documenté comme un choix

- **Localisation** : `Extraction/Pivot/ImportResult.cs` (constructeur).
- **Constat factuel** : voir §5.2 — l'invariant "si `Equipement` est `null`, les autres
  collections doivent être vides" (modèle doc §3.1, cité dans le commentaire du fichier) n'est pas
  une garde de constructeur ici ; il est vérifié seulement au niveau de l'orchestrateur
  Application (`ImportPipelineOrchestrator`, hors périmètre de cet audit). C'est le seul candidat
  trouvé dans tout le Domain à ce type d'écart critère-6.
- **Impact estimé** : dette légère, pas un risque réel aujourd'hui — le seul point de
  construction d'`ImportResult` en dehors des tests est l'orchestrateur lui-même (non audité ici),
  qui respecte déjà la règle par construction de son propre flux de contrôle. Le risque
  n'existerait que si un futur second appelant construisait un `ImportResult` sans passer par cet
  orchestrateur.
- **Refacto envisageable** : si l'invariant doit un jour devenir une vraie garantie plutôt qu'une
  discipline d'appelant, ajouter dans le constructeur : `if (equipement is null && (isolements.Count
  > 0 || points.Count > 0 || tachesMultiples.Count > 0)) throw new DomainRuleViolationException(...)`
  avec un nouveau `DomainErrorCode.ImportResult_...`. Non recommandé tant qu'un seul appelant
  existe et que Simon n'a pas explicitement demandé à durcir cette règle — signalé pour
  priorisation, pas implémenté.

### 6.2 — Cohérence `ExtractionErrorCode`/`DomainErrorCode` : RAS sauf le point de nommage déjà noté en §4.2

Les deux enums sont bien séparées par rôle : `DomainErrorCode` = échecs de construction d'objets
Domain (un par invariant de constructeur) ; `ExtractionErrorCode` = échecs de règle métier
*pendant* l'extraction, portés dans `ImportResult.Errors` sans jamais lever d'exception (design
volontaire déjà documenté — "no generic Result pattern... this doesn't fit 'throw and stop'"). Pas
de chevauchement de responsabilité trouvé entre les deux.

---

## 7. Lisibilité / complexité

### 7.1 — `SheetGenerationRule`, constructeur nettement plus complexe que ses équivalents, mais justifié par une règle métier réelle

- **Localisation** : `Generation/Profile/SheetGenerationRule.cs:34-121` (constructeur, ~90
  lignes).
- **Constat factuel** : c'est le constructeur le plus long du Domain — 6 blocs de validation
  distincts (nom de feuille vide, compatibilité `PivotSource`/`ColumnDefinition.Source`,
  `PointColumnDefinitions` interdites pour `TacheMultiple`, `ApplicationColumnDefinitions`
  interdites pour `TacheMultiple`, en-têtes dupliqués sur 3 collections combinées, `ColonneNom`
  dupliqué, `ApplicationNom` dupliqué). À titre de comparaison, `ImportProfile` (le second type le
  plus complexe du Domain) n'a que 5 gardes simples, toutes indépendantes, sans logique de
  recherche de doublon (`GroupBy`/`FirstOrDefault`).
- **Impact estimé** : cosmétique à dette légère — pas un risque réel, chaque règle est déjà
  individuellement couverte par un test dédié (voir les 21 méthodes de
  `SheetGenerationRuleTests.cs`, §5.1) et chaque bloc est commenté. Mais la méthode reste
  significativement plus dense à lire d'une traite que le reste du Domain, sans qu'aucun
  commentaire de tête n'explique *pourquoi* cette classe concentre autant de règles (le lecteur
  doit inférer que c'est parce qu'elle est le seul point de validation croisée entre colonnes
  d'un même type).
- **Refacto envisageable** : extraire chacun des 6 blocs en méthode privée nommée
  (`ValidateColumnPivotSourceCompatibility(...)`, `ValidateNoPointColumnsForTacheMultiple(...)`,
  etc.), appelées séquentiellement depuis le constructeur — réduit la longueur visuelle du
  constructeur sans changer le comportement ni l'ordre de levée des exceptions (les tests
  existants resteraient valides tels quels, car ils testent le comportement observable, pas
  l'implémentation — cf. §5.3).

### 7.2 — RAS ailleurs

Aucune autre entité/value object du Domain ne se distingue par une complexité disproportionnée par
rapport à ses pairs de même rôle. Les primitives (`DirectCell`, `BlockFieldDefinition`,
`SubstringAfter`, `FieldRef`, etc.) restent toutes à un seul niveau de validation simple ; les
pivots (`PointPivot`, `TacheMultiplePivot`, `ExtractionError`) restent des records à validation
plate ; `ImportProfile`/`ExportProfile` sont d'une complexité comparable et attendue pour des
racines d'agrégat portant plusieurs champs requis.

---

## Non couvert / incertain

- **Impact réel du `DirectCell` mort (§3.1) sur une éventuelle migration future** : le fait que
  ce type existe mais ne soit jamais construit peut aussi bien signifier "code mort à supprimer"
  que "brique préparée en avance pour une migration prévue mais pas encore planifiée" — seul Simon
  peut trancher, l'audit ne peut pas le déduire du code seul.
- **Opportunité réelle de durcir l'invariant `ImportResult` (§6.1)** : dépend de si un second
  appelant est prévu à court terme (hors périmètre Domain, information Application/roadmap non
  disponible depuis ce seul projet).
- **Mesure de couverture ligne/branche réelle** : cet audit estime la densité de test par
  comptage de cas rapporté à la complexité perçue du type, faute d'avoir exécuté un outil de
  coverage (`dotnet test` avec collecteur de couverture) dans le cadre de cette passe — une
  mesure chiffrée confirmerait ou infirmerait le classement du §5.1 plus rigoureusement.
- **Whether `GeneratedFileRecord`'s absence of Entity inheritance (§4.1) is a genuine oversight
  vs. an unwritten deliberate choice** : aucun commentaire ne tranche, contrairement à
  `ExportProfile` qui documente explicitement son écart au même pattern — seule Simon (ou une
  relecture du ticket Lot 034 correspondant) peut confirmer l'intention.
- **Le reste des projets de la solution (Application/Infrastructure/WebAPI/BlazorAdmin)** n'a pas
  été audité ici — chaque écart potentiel signalé en "Hors périmètre — observé en passant"
  (§2) est un simple pointeur, pas une évaluation, conformément au périmètre demandé.
