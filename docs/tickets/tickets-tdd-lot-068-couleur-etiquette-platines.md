# Tickets TDD — Lot 068 : couleur d'étiquette (feuille PLATINES) exposée en export

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Après le lot 067
(`tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md`). Décision actée avec Simon le
2026-09-04.*

**Objet** : nouvelle remarque client (pas de spécification écrite) — la feuille source PLATINES porte,
pour chaque bloc, une cellule « couleur d'étiquette » (`ROUGE`/`BLEUE`/`JAUNE` observées dans les
fixtures réelles, texte libre a priori) qui n'est aujourd'hui lue par aucun service d'extraction. Ce
lot ajoute une propriété dédiée sur `IsolementPivot`, alimentée depuis cette cellule pour la feuille
PLATINES uniquement, et la relie à la colonne d'export `"ETIQUETTE"` déjà présente (mais non mappée,
`Source: null`, Lot 066) sur la feuille `Enfants` du profil d'export par défaut.

**Origine** : remarque orale de Simon, confirmée par investigation directe des fixtures réelles (voir
§68.0) — aucune spécification écrite n'existe pour ce champ.

---

## Décisions actées avec Simon (non négociables, à respecter telles quelles)

- La propriété vit sur **`IsolementPivot`** (comme `HasZeroEnergie`, Lot 063) — c'est une donnée par
  bloc PLATINES, jamais par équipement.
- Cellule à lire : **`H:N`, offset +1 par rapport au début du bloc** (même largeur de fusion que les
  autres cellules valeur du formulaire — `Designation`, `PoseeLe`/`DeposeeLe`). Le libellé de formulaire
  `"ÉTIQUETTE"` en colonne F (même offset +1) est ignoré, il ne fait que reproduire le nom du champ
  papier, comme convenu.
- **Feuille PLATINES uniquement** — confirmé par investigation (§68.0) : ORIFICES CAPACITES, qui
  partage le même service d'extraction (`UnconditionalIsolementSheetExtractionService`), n'a aucune
  ligne équivalente dans aucune des 4 fixtures réelles sur disque. Le champ reste donc `null` (non
  configuré) pour ORIFICES CAPACITES.
- **Cellule vide → valeur vide** (`""`), pas d'erreur ni d'avertissement — contrairement à
  `ZeroEnergieExpectedValue` (Lot 063), il n'y a ici aucune notion de valeur « attendue » à comparer :
  n'importe quel texte est une valeur légitime (confirmé : `ROUGE`/`BLEUE`/`JAUNE` observées, pas un
  jeu de valeurs fermé).
- Le profil d'export par défaut porte déjà une colonne `"ETIQUETTE"` non mappée
  (`ColumnDefinition("ETIQUETTE", null)`, feuille `Enfants`, ajoutée au Lot 066 comme colonne
  d'identité "sans règle d'extraction connue") — ce lot la remappe sur le nouveau champ pivot au lieu
  d'en créer une nouvelle.
- **Pas de migration de données** pour les profils déjà seedés en base : base de dev jetable, même
  convention que les lots précédents (063, 066, 067).

---

## Hors périmètre explicite de ce lot

- ORIFICES CAPACITES (pas de cellule équivalente dans les fixtures réelles — à revalider si un futur
  fichier client en présentait une).
- Toute validation/liste fermée de valeurs (`ROUGE`/`BLEUE`/`JAUNE`/...) — texte libre, aucune
  contrainte métier connue à ce jour.
- Exposition dans l'UI Blazor de l'éditeur de profil d'import (`ImportProfileEditor.razor`/
  `SheetRuleForm.razor`) — comme `HeaderFieldRule`/`FieldPresencePointRule` avant lui, ce champ reste
  configurable uniquement via `DefaultProfileSeeder.cs` pour ce lot ; à revisiter si le client a besoin
  de le reconfigurer depuis l'admin.
- `ExportProfileEditor.razor` — la colonne `"ETIQUETTE"` existe déjà dans le seed, seul son
  `Source` change (`null` → `PivotFieldRef.IsolementCouleurEtiquette`), aucun changement UI nécessaire.

---

## 68.0. Investigation préalable (déjà faite, résultats verrouillés)

Confirmé par dump direct des fixtures réelles (`ClosedXML`, feuille `PLATINES`, colonnes F et H,
lignes 15 à la dernière ligne utilisée) sur les 4 fichiers `tests/Fixtures/*.xlsx` :

| Fixture | Lignes `F = 'ÉTIQUETTE'` | Valeurs `H` observées |
|---|---|---|
| `Dossier.de.MaD.IDL.-.C7401.xlsx` | 18, 26, 34, ... (pas de 8) | `ROUGE` (toutes) |
| `Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx` | 18, 26, ..., 250 | `ROUGE` (blocs 1-13), `BLEUE` (blocs 14-21), `ROUGE` (blocs 22-30) |
| `Dossier.de.MaD.IDL.-.G6306B.REV.xlsx` | 18, 26, ..., 170 | `ROUGE` (toutes sauf bloc 5 = `JAUNE`) |
| `Dossier de MaD IDL -  G4010A.xlsx` | 18, 26, ..., 250 | `ROUGE`/`BLEUE` mélangées |

Chaque occurrence est à l'offset **+1** par rapport au début de bloc (`FirstBlockStartRow = 17`,
`Step = 8` — inchangés). Confirmé aussi : **`ORIFICES CAPACITES` ne contient aucune ligne `'ÉTIQUETTE'`
dans aucune des 4 fixtures** (même recherche, sheet différente) — la colonne V n'existe donc pas sur
cette feuille, cohérent avec le fait que PLATINES et ORIFICES CAPACITES représentent des éléments
différents (platines borgnes vs. orifices/capacités) malgré un formulaire source par ailleurs
identique.

Relecture de `UnconditionalIsolementSheetExtractionService.cs` : le service construit déjà
`IsolementPivot` avec `positionALaPose: ""` explicite (jamais lu pour ces 2 feuilles) et gère déjà un
mécanisme de lecture de cellule optionnelle en dehors de `RepeatingBlockLocator.Fields`
(`FieldPresencePointRules`, Lot 063 PLATINES) — le même patron (lecture via
`BlockFieldRangeCalculator.BuildRange` + `IWorkbookReader.ReadCellValue`, hors politique de champ
requis de `IRepeatingBlockReader`) s'applique ici.

Relecture de `SheetExtractionRule.cs` : `ZeroEnergieExpectedValue` (Lot 063, `string?`) est le
précédent direct pour un champ optionnel dédié à une seule feuille — ce lot ajoute de la même façon
`BlockFieldDefinition? CouleurEtiquetteCell`, dernier paramètre optionnel du constructeur (`= null`),
sans validation supplémentaire (la validation de `BlockFieldDefinition` lui-même, si non-null, est déjà
assurée par son propre constructeur).

Relecture de `ImportProfileConfiguration.cs` : le mapping `OwnsOne` de `HeaderFieldRule.Cell`/
`FieldPresencePointRule.Cell` (table-split, colonnes préfixées pour éviter toute collision de nom sur
la même ligne `ImportProfileSheetRules`) est le patron direct à reproduire pour
`CouleurEtiquetteCell`, en optionnel (pas de `.Navigation(...).IsRequired()`).

---

## 68.1. Domain — `CouleurEtiquette` sur `IsolementPivot`

**Comportement attendu** : nouveau paramètre de constructeur `string couleurEtiquette = ""` (dernier
paramètre, préserve tous les appels existants — `IsolementExtractionService`,
`AutresJointsTouchesExtractionService`, `DiversExtractionService`, qui n'ont pas cette notion et
n'ont pas à être modifiés), nouvelle propriété `public string CouleurEtiquette { get; }`, incluse dans
`Equals`/`GetHashCode`. Aucune validation (comme `Designation`/`PositionALaPose` — une chaîne vide est
un état légitime).

**Tests** (xUnit, Domain, `IsolementPivotTests.cs`) :
- Valeur par défaut `""` quand omis.
- Construction explicite avec une valeur non vide → propriété assignée.
- Deux instances ne différant que par `CouleurEtiquette` → non égales structurellement.

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/IsolementPivot.cs` (+ miroir tests).

---

## 68.2. Domain — `CouleurEtiquetteCell` sur `SheetExtractionRule`

**Comportement attendu** : nouvelle propriété `public BlockFieldDefinition? CouleurEtiquetteCell { get; }`,
nouveau paramètre de constructeur optionnel `BlockFieldDefinition? couleurEtiquetteCell = null` (dernier
paramètre, préserve tous les appels existants). `null` = pas de cellule couleur d'étiquette configurée
pour cette feuille (comportement de toutes les feuilles à ce jour, y compris ORIFICES CAPACITES).

**Tests** (xUnit, Domain, `SheetExtractionRuleTests.cs`) :
- `rule.CouleurEtiquetteCell.Should().BeNull()` quand omis.
- Construction avec un `BlockFieldDefinition` valide → propriété assignée.

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/SheetExtractionRule.cs` (+ miroir tests).

---

## 68.3. Domain — `PivotFieldRef`/`PivotFieldResolver`

**Comportement attendu** : nouveau membre `PivotFieldRef.IsolementCouleurEtiquette`, `GetPivotSource`
le classe sous `PivotSource.Isolement` (même groupe que `IsolementDesignation` et consorts), `Resolve`
retourne `isolement.CouleurEtiquette` directement (pas de transformation, même patron que
`IsolementDesignation`).

**Tests** (xUnit, Domain, `PivotFieldResolverTests.cs`) :
- `GetPivotSource(PivotFieldRef.IsolementCouleurEtiquette) == PivotSource.Isolement`.
- `Resolve(isolementPivot, PivotFieldRef.IsolementCouleurEtiquette)` retourne la valeur exacte de
  `CouleurEtiquette` (cas non vide et cas `""`).

**Dossier** : `src/ExcelETL.Domain/Generation/Fields/PivotFieldRef.cs`,
`src/ExcelETL.Domain/Generation/Fields/PivotFieldResolver.cs` (+ miroir tests).

---

## 68.4. Application — `UnconditionalIsolementSheetExtractionService` : lecture de la cellule

**Comportement attendu** : pour chaque bloc extrait, si `sheetRule.CouleurEtiquetteCell` n'est pas
`null`, lire la cellule via `BlockFieldRangeCalculator.BuildRange(sheetRule.CouleurEtiquetteCell,
block.StartRow)` + `workbookReader.ReadCellValue(sheet, ...)` ; `null`/blanc → `""` ; sinon la valeur
brute (pas de trim/normalisation — texte affiché tel quel). Passer la valeur au constructeur
`IsolementPivot` (`couleurEtiquette: ...`). Quand `CouleurEtiquetteCell` est `null` (ORIFICES
CAPACITES, ou tout profil PLATINES antérieur à ce lot) : `couleurEtiquette: ""`, comportement
strictement inchangé.

**Tests** (xUnit, Application, `UnconditionalIsolementSheetExtractionServiceTests.cs`,
`Mock<IWorkbookReader>`) :
- `CouleurEtiquetteCell` configuré, cellule non vide → `IsolementPivot.CouleurEtiquette` porte la
  valeur lue.
- `CouleurEtiquetteCell` configuré, cellule vide/blanche → `CouleurEtiquette == ""`.
- `CouleurEtiquetteCell` non configuré (`null`) → `CouleurEtiquette == ""`, aucun appel
  `ReadCellValue` supplémentaire (garde-fou non-régression pour ORIFICES CAPACITES).
- **Garde-fou anti-hardcoding** (même patron que le test `EquipementTypeElementNom` du Lot C1) : deux
  profils avec des `CouleurEtiquetteCell`/plages différentes → chacun restitue sa propre valeur.

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/UnconditionalIsolementSheetExtractionService.cs`
(+ miroir tests).

---

## 68.5. Infrastructure — persistance EF Core + migration

**Comportement attendu** : nouvelle colonne owned-type optionnelle `CouleurEtiquetteCell` (4 colonnes
`CouleurEtiquetteCellName`/`CouleurEtiquetteCellColumnRange`/`CouleurEtiquetteCellRowOffsetStart`/
`CouleurEtiquetteCellRowOffsetEnd`, mêmes noms de colonnes physiques préfixés pour éviter toute
collision avec `Locator`/`HeaderFields`/`FieldPresencePointRules` déjà table-splittés sur la même ligne
`ImportProfileSheetRules`) sur la table `ImportProfileSheetRules`, configurée dans
`ImportProfileConfiguration.cs` via `rules.OwnsOne(r => r.CouleurEtiquetteCell, ...)`, **sans**
`.Navigation(...).IsRequired()` (optionnel). **Migration générée via `dotnet ef migrations add
AddCouleurEtiquetteCellToImportProfileSheetRule --project src/ExcelETL.Infrastructure
--startup-project src/ExcelETL.WebAPI` — jamais écrite à la main.**

**Tests** (xUnit, EF Core InMemory réel, `EfImportProfileStoreTests.cs`) :
- Round-trip d'une règle PLATINES avec `CouleurEtiquetteCell` renseigné → relu à l'identique.
- Round-trip d'une règle ORIFICES CAPACITES avec `CouleurEtiquetteCell = null` → relu comme `null`
  (pas un `BlockFieldDefinition` avec des colonnes vides par défaut).

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Configurations/ImportProfileConfiguration.cs`
+ nouvelle migration (générée par tooling).

---

## 68.6. Infrastructure — mise à jour du `DefaultProfileSeeder`

**Comportement attendu** :
- Règle PLATINES du profil d'import par défaut : ajouter
  `couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1)`.
- Règle ORIFICES CAPACITES : **inchangée** (pas de `couleurEtiquetteCell`).
- Feuille `Enfants` du profil d'export par défaut : remplacer
  `new ColumnDefinition("ETIQUETTE", null)` par
  `new ColumnDefinition("ETIQUETTE", PivotFieldRef.IsolementCouleurEtiquette)` — même position dans la
  liste, aucun autre changement d'ordre.

**Tests** (xUnit, `DefaultProfileSeederTests.cs`) :
- La règle PLATINES seedée porte le `CouleurEtiquetteCell` attendu (`"H:N"`, offsets 1/1).
- La règle ORIFICES CAPACITES seedée a toujours `CouleurEtiquetteCell == null`.
- La colonne `"ETIQUETTE"` de `Enfants` a désormais `Source == PivotFieldRef.IsolementCouleurEtiquette`
  (plus `null`).

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (+ miroir tests).

---

## 68.7. Non-régression d'intégration sur les fixtures réelles

**Comportement attendu** : avec le profil **seedé par défaut**, l'extraction + génération de chaque
fixture produit, sur la feuille `Enfants` générée, une valeur `"ETIQUETTE"` par ligne PLATINES
correspondant exactement à la couleur relevée en §68.0 pour le bloc correspondant (`ROUGE`/`BLEUE`/
`JAUNE` selon la fixture) ; les lignes issues des autres feuilles (ISOLEMENT, ORIFICES CAPACITES,
AUTRES JOINTS TOUCHES, DIVERS) ont `"ETIQUETTE" == ""`. Aucun autre comportement observable ne change
(nombre d'isolements, warnings, autres colonnes) — les suites d'intégration existantes
(`PlatinesExtractionServiceIntegrationTests`, `ImportPipelineOrchestratorIntegrationTests`,
`GenerationPipelineIntegrationTests`, `DefaultProfileSeederPipelineIntegrationTests`) restent vertes
**sans modification de leurs assertions existantes**, à l'exception du test de contenu de colonnes de
`DefaultProfileSeederPipelineIntegrationTests` qui doit être étendu (résolution dynamique de la colonne
par nom d'en-tête, patron déjà établi au Lot 066) pour vérifier la nouvelle valeur.

**Tests** (xUnit, intégration, fixtures réelles) :
- Nouveau test dédié (`PlatinesExtractionServiceIntegrationTests` ou
  `DefaultProfileSeederPipelineIntegrationTests`, à trancher selon où vivent déjà les assertions
  équivalentes de PLATINES) confirmant, pour au moins une fixture portant les deux couleurs (D8570 ou
  G4010A), que chaque `IsolementPivot` issu de PLATINES porte la bonne `CouleurEtiquette`.
- Test de génération confirmant la colonne `"ETIQUETTE"` de `Enfants` sur au moins une fixture réelle.

**Dossier** : `tests/ExcelETL.Infrastructure.Tests/Excel/` (fichiers existants, extension en place).

---

## Ordre recommandé

1. **68.0** (investigation — déjà fait, verrouillé ci-dessus)
2. **68.1** + **68.2** + **68.3** (Domain — indépendants entre eux)
3. **68.4** (Application — dépend de 68.1/68.2)
4. **68.5** (EF Core + migration générée par tooling) puis **68.6** (seeder — dépend de 68.2/68.3/68.5)
5. **68.7** (non-régression fixtures — dernier, valide l'invariant central)

## Note d'efficacité d'implémentation (Claude Code)

- **Ne jamais écrire de migration EF Core à la main** — toujours `dotnet ef migrations add`.
- **Invariant central** : seule la feuille PLATINES change de comportement observable ; ORIFICES
  CAPACITES/ISOLEMENT/AUTRES JOINTS TOUCHES/DIVERS restent strictement inchangées.
- La colonne d'export `"ETIQUETTE"` existe déjà (Lot 066) — ne pas en créer une deuxième, uniquement
  changer son `Source`.
- Strict Red-Green-Refactor : test qui échoue d'abord, à chaque étape.
