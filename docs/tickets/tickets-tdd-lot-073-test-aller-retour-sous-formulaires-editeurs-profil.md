# Lot 073 — Garde-fou aller-retour contre la perte de champ à la reconstruction (éditeurs de profil)

## Contexte

Décision actée avec Simon le 16/09 (session d'architecture sur le patron d'enregistrement des éditeurs
de profil) : les pertes de données silencieuses des éditeurs relèvent de **deux défauts distincts**, à
traiter séparément.

| Défaut | Mécanisme | Incidents |
| :--- | :--- | :--- |
| **A — brouillon non validé** | Une saisie reste dans l'état local d'un sous-formulaire tant que son propre bouton n'a pas été cliqué ; une sauvegarde plus haut ne la lit pas. | Lot 056 ; commit `0cbac22` |
| **B — champ perdu à la reconstruction** | Un formulaire reconstruit l'objet métier à partir des seuls champs qu'il connaît ; ce qu'il n'expose pas (ou oublie de reporter) disparaît. | Lot 048.1 (`HeaderFields`/`HeaderComposites`) ; commit `1736886` (PLATINES, `FieldPresencePointRules`) ; commit `0cbac22` (`ConstantColumnDefinitions` des feuilles `TM_PROC_*`) |

Ce lot traite **B uniquement**, en préalable au lot 074. B s'est produit trois fois, chaque fois
corrigé champ par champ, et chaque fois découvert en production ou par le client.

**Place de ce lot dans la décision retenue (P3, brouillon unique possédé par la racine,
`tickets-tdd-lot-074-pilote-brouillon-editeur-profil-export.md`)** : à terme, P3 rend B impossible par
construction et le garde par un test **unitaire** aller-retour sur ses conversions. Ce lot reste
nécessaire avant : ses tests pilotent l'interface par ses identifiants et ses gestes, sans rien savoir
de l'état interne — c'est exactement le filet qu'exige la réécriture interne des lots 074 et suivants.
Ils doivent rester verts, **sans modifier leurs assertions**, pendant toute la migration.

## Constats (code au commit `8c05bc4`)

1. **Aucun test générique n'existe.** Le seul test de la famille est
   `ImportProfileEditorTests.EditingSheetRule_WithoutChanges_PreservesHeaderFieldsAndComposites`
   (`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileEditorTests.cs:2783`), écrit pour le
   lot 048.1 sur une fixture construite à la main qui ne couvre que les en-têtes. Les tests
   `*NestedEditFlushTests.cs` (commit `8c05bc4`) vérifient qu'une **modification** est conservée (défaut
   A), pas qu'un champ **non touché** survit (défaut B).
2. **Les formulaires qui reconstruisent un objet métier** : `SheetRuleForm` (`SheetExtractionRule`),
   `SheetGenerationRuleForm` (`SheetGenerationRule`), et les 7 sous-formulaires imbriqués
   (`BlockFieldForm`, `HeaderFieldRuleForm`, `HeaderCompositeRuleForm`, `FieldPresencePointRuleForm`,
   `ColumnDefinitionForm`, `PointColumnDefinitionForm`, `ApplicationColumnDefinitionForm`). S'y
   ajoutent les deux racines (`ImportProfileEditor`/`ExportProfileEditor`), qui reconstruisent le
   profil lui-même. Les éditeurs de ligne en place (colonnes inconditionnelles, règles de Point
   conditionnelles, Tableaux, Applications, libellés de types de tâches multiples) reconstruisent
   aussi leurs valeurs.
3. **Deux transformations volontaires, à ne pas confondre avec B** :
   - `HeaderFieldRuleForm` construit un `DirectCell("__pending__", range)` ; c'est `SheetRuleForm` qui
     réaffecte `Cell.Sheet` depuis le nom de feuille courant (décision 2 du lot 048). Un aller-retour
     testé **sur le sous-formulaire seul** échouerait à tort : le test se fait au niveau du profil.
   - `SheetRuleForm` nomme toujours la cellule de couleur d'étiquette `"CouleurEtiquette"`
     (`CouleurEtiquetteCellName`, `SheetRuleForm.razor:671`) et `FieldPresencePointRuleForm` garde le
     `Cell.Name` d'origine en édition (`FieldPresencePointRuleForm.razor:142`). Le profil par défaut
     utilise déjà ces noms : l'aller-retour doit être exact, sans exclusion.
4. **Le profil par défaut couvre presque tous les champs optionnels** : en-têtes et composites
   (PROCEDURE, AUTRES JOINTS TOUCHES, DIVERS), `ZeroEnergieExpectedValue` (ISOLEMENT),
   `FieldPresencePointRules` avec `ExpectedValue`, `CouleurEtiquetteCell` et `AllowedCouleursEtiquette`
   (PLATINES, ORIFICES CAPACITES), `DefaultCouleurEtiquette` (AUTRES JOINTS TOUCHES),
   `TacheMultipleTypeLabels`, `DefaultTableaux`/`DefaultApplicationNames`, et côté export colonnes à
   source `null`, colonnes Point, colonnes Application, colonnes constantes. **Seul trou connu** : une
   `FieldPresencePointRule` sans `ExpectedValue` (`null`), que le profil par défaut n'utilise plus
   depuis le 16/09.
5. **Les constructeurs de profil par défaut sont privés** (`DefaultProfileSeeder.BuildDefaultImportProfile`/
   `BuildDefaultExportProfile`). Le moyen déjà utilisé pour les obtenir en test : exécuter
   `DefaultProfileSeeder.SeedAsync()` sur le fournisseur EF Core InMemory, puis relire par
   `DefaultProfileSeeder.ImportProfileId`/`ExportProfileId` — voir
   `tests/ExcelETL.BlazorAdmin.Tests/Formatting/BlockFieldRangeFormatterTests.cs`.
6. **Piège d'ordre** : le fournisseur InMemory ne garantit pas l'ordre des collections possédées après
   la suppression-réinsertion que fait `SaveAsync` (noté dans `DefaultProfileSeederTests`). Comparer ce
   qui revient du magasin mélangerait ce bruit au défaut testé.

## Décisions

| Sujet | Décision |
| :--- | :--- |
| Référence de comparaison | Le profil par défaut **tel que semé puis relu**, pas une fixture écrite à la main : c'est la configuration réelle, et elle s'enrichit automatiquement quand le semeur évolue. |
| Point d'observation | **L'argument passé à `SaveAsync`**, capturé par un magasin simulé (Moq, déjà référencé par le projet de tests) qui renvoie le profil par défaut à `GetByIdAsync`. On teste l'interface, pas la persistance (déjà couverte par `EfImportProfileStoreTests`/`EfExportProfileStoreTests`). Ordre strict exigé. |
| Niveau de test | Profil complet (les deux racines), pas sous-formulaire isolé — voir constat 3. |
| Comparaison | `BeEquivalentTo(..., o => o.WithStrictOrdering())`. `ImportProfile` hérite d'`Entity` (égalité par identité) : l'égalité `==` ne prouve rien. |
| Trou de couverture (constat 4) | Un cas supplémentaire construit à la main pour la `FieldPresencePointRule` à `ExpectedValue` nulle, dans le même test paramétré. |
| Rouge | Ces tests peuvent passer au vert dès leur écriture (les trois incidents connus sont corrigés). C'est attendu. La preuve qu'ils détectent le défaut se fait **en réintroduisant temporairement** chacun des trois bogues connus (voir 073.4) — même méthode que le commentaire du test du lot 048.1. |

## Étapes TDD

### 073.1 — Aller-retour « règle de feuille ouverte puis revalidée sans modification » (import)

Pour **chaque** règle de feuille du profil d'import par défaut (`[Theory]` indexé sur la position) :
ouvrir l'éditeur sur ce profil, cliquer `#modify-sheet-rule-button-{i}`, cliquer
`#save-sheet-rule-button-{i}` sans rien toucher, cliquer `#save-profile-button`. Le profil capturé
doit être équivalent, ordre strict, au profil de départ.

Couvre `SheetRuleForm` et la racine `ImportProfileEditor`.

### 073.2 — Aller-retour « élément imbriqué ouvert puis revalidé sans modification » (import)

Pour chaque règle de feuille, pour **chaque** élément de chacune de ses sous-listes (champs de bloc,
en-têtes, composites, règles de présence, colonnes inconditionnelles, règles de Point) : ouvrir la
règle, ouvrir l'élément en modification, cliquer son propre bouton d'enregistrement sans rien
toucher, enregistrer la règle, enregistrer le profil, comparer.

Idem pour les trois listes de premier niveau (Tableaux, Applications, libellés de types de tâches
multiples) : ouvrir chaque élément, le revalider, enregistrer le profil, comparer.

Ajouter le cas `ExpectedValue = null` (constat 4) : une règle PLATINES construite à la main avec une
`FieldPresencePointRule` sans valeur attendue.

Les identifiants de boutons sont déjà en place — les relever dans `SheetRuleForm.razor` et
`ImportProfileEditor.razor` plutôt que de les supposer (convention `{IdPrefix}modify-…-button-{j}`/
`{IdPrefix}save-…-button-{j}`, avec `IdPrefix = "edit-{i}-"` pour une règle en modification).

### 073.3 — Mêmes aller-retours côté export

Miroir de 073.1 et 073.2 sur le profil d'export par défaut : chaque `SheetGenerationRule` (y compris
la règle « Tâches multiples » et ses colonnes constantes), puis chaque colonne, colonne Point et
colonne Application. `SheetGenerationRuleForm` n'expose pas les colonnes constantes : c'est
précisément le cas du commit `0cbac22` — l'aller-retour de la règle « Tâches multiples » doit les
retrouver intactes.

### 073.4 — Preuve de détection (pas de commit de code de production)

Réintroduire successivement, **sans les committer**, les trois bogues connus et vérifier qu'au moins
un test du lot échoue pour chacun :

1. `SheetRuleForm` : passer `[]` au lieu de `[.. _headerFields]`/`[.. _headerComposites]` (lot 048.1).
2. `SheetRuleForm` : ne plus passer `fieldPresencePointRules` (commit `1736886`).
3. `SheetGenerationRuleForm` : ne plus reporter `ConstantColumnDefinitions` (commit `0cbac22`).

Consigner le résultat (nom du test qui échoue pour chaque bogue) dans la section « Résultat » de ce
document, en fin de lot.

### 073.5 — Convention

Ajouter à `docs/conventions/recommandations-tickets-tdd.md`, section 9, un paragraphe court : tout
nouveau champ ajouté à un type de profil (`ImportProfile`, `SheetExtractionRule`, `ExportProfile`,
`SheetGenerationRule` et leurs éléments) doit être **semé dans le profil par défaut** ou ajouté comme
cas manuel à ces tests aller-retour, faute de quoi le garde-fou ne le voit pas. Citer les fichiers de
test par leur nom, ne pas recopier leur contenu.

Ce paragraphe est provisoire : il sera remplacé, à la fin de la migration P3 de l'import, par la règle
propre aux brouillons (tout champ passe par les conversions, gardées par leur test unitaire
aller-retour).

## Refactor à considérer

- Un seul fichier de test par éditeur (`ImportProfileEditorRoundTripTests.cs`,
  `ExportProfileEditorRoundTripTests.cs`), avec une fonction d'aide locale qui parcourt les éléments —
  pas d'aide partagée entre fichiers, conformément à la convention de tests du dépôt.
- Si le parcours de 073.2 devient illisible (plusieurs niveaux de boucles sur des identifiants
  construits), préférer un `[Theory]` dont chaque cas est `(indexRègle, typeSousListe, indexÉlément)`,
  généré en `MemberData` à partir du profil par défaut : un échec nomme alors l'élément fautif.

## Hors périmètre explicite

- **Le défaut A** (brouillon non validé) : lot 074.
- **Toute modification du code de production**, sauf si 073.1–073.3 révèlent un **quatrième** cas réel
  de B. Dans ce cas : corriger dans ce lot (règle de la section 9 : couvrir toutes les instances
  trouvées), un commit par défaut corrigé, et le signaler en fin de lot.
- **L'ordre des collections après persistance réelle** (constat 6) : sujet de persistance, pas
  d'interface.
- **Les pages de test** (`ImportProfileTest.razor`/`ExportProfileTest.razor`) et les listes de profils.

## Notes d'exécution

- Effort standard partout ; seul 073.2 (conception du parcours paramétré) mérite un effort élevé.
- Tests filtrés : `dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter "FullyQualifiedName~RoundTrip" --verbosity quiet`.
  Clôture : `ExcelETL.BlazorAdmin.Tests` complet uniquement (aucun autre projet touché).
- Un commit par sous-ticket (073.1+073.2 peuvent être groupés s'ils partagent la fonction d'aide).

## Résultat

### 073.1–073.3 — tests livrés

- `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ImportProfileEditorRoundTripTests.cs` : 72 cas —
  `SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile` (7 : les 6 règles du profil
  par défaut + la règle PLATINES construite à la main), `NestedItem_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile`
  (un cas par élément imbriqué, `MemberData` nommant `fixture`/`ruleIndex`/`subList`/`itemIndex` et un
  libellé `FEUILLE/sous-liste[j]`), `TopLevelItem_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile`
  (Tableaux, Applications, libellés de types de tâches multiples), plus un `[Fact]` qui vérifie que
  les fixtures couvrent toujours les champs optionnels visés (y compris `ExpectedValue = null`).
- `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ExportProfileEditorRoundTripTests.cs` : 93 cas, même
  structure (règles, colonnes, colonnes Point, colonnes Application, `[Fact]` de couverture).
- Tous verts dès l'écriture, comme prévu. **Aucun quatrième cas réel de B trouvé** : aucun code de
  production modifié.
- Comparaison : `BeEquivalentTo(original, o => o.ComparingByMembers<ImportProfile>().ComparingRecordsByMembers().WithStrictOrdering())`
  (côté export, sans `ComparingByMembers<ImportProfile>`). Sans ces deux options, FluentAssertions
  compare par `Equals` tout type qui le redéfinit : identité pour `ImportProfile` (`Entity`), et
  égalité de référence sur les listes pour un `record` qui ne la redéfinit pas.

### 073.4 — preuve de détection (bogues réintroduits sans commit, puis `git checkout`)

| Bogue réintroduit | Tests en échec | Exemple |
| :--- | :--- | :--- |
| 1. `SheetRuleForm` passe `[], []` pour `HeaderFields`/`HeaderComposites` (lot 048.1) | 31 / 165 | `ImportProfileEditorRoundTripTests.SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(fixture: "default", ruleIndex: 0, sheetName: "PROCEDURE")` (aussi AUTRES JOINTS TOUCHES, DIVERS et tous leurs éléments imbriqués) |
| 2. `SheetRuleForm` ne passe plus `fieldPresencePointRules` (commit `1736886`) | 17 / 165 | `ImportProfileEditorRoundTripTests.SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(fixture: "default", ruleIndex: 2, sheetName: "PLATINES")` et le même cas sur la fixture `field-presence-null-expected-value` |
| 3. `SheetGenerationRuleForm` ne reporte plus `ConstantColumnDefinitions` (commit `0cbac22`) | 18 / 165 | `ExportProfileEditorRoundTripTests.SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(ruleIndex: 2, sheetName: "Tâches multiples")` et les 17 colonnes de cette règle |

Après restauration : 165/165 verts, `git status` propre.

### 073.5 — convention

Paragraphe « Nouveau champ sur un type de profil » ajouté en fin de section 9 de
`docs/conventions/recommandations-tickets-tdd.md` (provisoire, à remplacer à la fin de la migration P3).

### Clôture

`ExcelETL.BlazorAdmin.Tests` complet : 1210/1211 verts. Le seul échec,
`ExportProfileTestTests.ClickingGenerate_WithoutExportProfileSelected_ShowsError_WithRoleAlert`, est le
défaut préexistant déjà signalé (fixture `new ExportProfile(..., [])` rejetée par le constructeur),
sans lien avec ce lot.
