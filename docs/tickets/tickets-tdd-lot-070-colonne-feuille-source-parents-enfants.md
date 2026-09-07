# Tickets TDD — Lot 070 : colonne "Feuille" (onglet source) sur `Parents`/`Enfants`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : à l'import, un `EquipementPivot` (parent) et plusieurs `IsolementPivot` (enfants) sont
extraits du classeur source vers le modèle pivot ; les enfants peuvent provenir de différentes feuilles
du classeur source (`ISOLEMENT`, `PLATINES`, `ORIFICES CAPACITES`, `AUTRES JOINTS TOUCHES`, `DIVERS`).
À l'export, `SheetGenerationEngine` écrit le pivot vers un classeur cible, notamment les feuilles
`"Parents"`/`"Enfants"` du profil d'export standard. Demande de Simon : ajouter une colonne `"Feuille"`
en position colonne **B** de `Parents` et `Enfants`, contenant le nom de l'onglet Excel source depuis
lequel chaque ligne (l'Équipement, ou l'Isolement) a été lue.

---

## Décisions actées (confirmées avec Simon avant rédaction de ce ticket)

- **Périmètre** : uniquement les feuilles `"Parents"`/`"Enfants"` du profil d'export standard. Les
  feuilles générées dynamiquement `TM_PROC_MAD`/`TM_PROC_REL` (tâches multiples, `PivotSource.TacheMultiple`)
  sont **hors périmètre** de ce lot (leur source est de toute façon toujours la seule feuille PROCEDURE).
- **Contenu de la cellule** : le nom d'onglet Excel tel que **configuré dans le profil** (`SheetExtractionRule.SheetName`
  de la règle qui a produit cette ligne), pas un libellé métier distinct — reflète fidèlement la
  provenance réelle, y compris pour un profil client qui aurait renommé un onglet.
- **Position "B"** : `"Feuille"` devient la 2ᵉ colonne des deux feuilles (juste après `"Repère"`/`"Numéro"`,
  colonne A) ; toutes les colonnes existantes décalent d'un cran vers la droite. Aucune autre colonne
  n'est renommée/retirée.

---

## Hors périmètre explicite de ce lot (ne pas rouvrir)

- `TM_PROC_MAD`/`TM_PROC_REL` (tâches multiples) — confirmé hors périmètre avec Simon.
- Exposition d'un mécanisme générique de "colonne système" dans `ExportProfileEditor.razor` — la colonne
  est ajoutée via `DefaultProfileSeeder.cs` comme toute autre colonne du profil standard ; elle apparaît
  automatiquement dans le sélecteur de colonnes existant (filtré par `PivotSource`, aucun travail Blazor
  dédié requis) une fois `PivotFieldRef.EquipementSourceSheet`/`IsolementSourceSheet` ajoutés au Domain —
  voir 070.1.
- Migration de données pour un profil déjà seedé dans un environnement existant — même raisonnement que
  les lots récents (Lots 066/067/068/069) : base de données jetable en pré-production, un reseed suffit.
- Renommage/repositionnement de toute colonne autre que l'insertion de `"Feuille"` en position B.
- Toute modification du comportement d'extraction lui-même (quelle feuille produit quel Isolement) —
  ce lot ne fait qu'enregistrer, sur le pivot, une information déjà connue de chaque service d'extraction
  (`sheetRule.SheetName`, déjà reçu en paramètre par les 5 services), jamais la recalculer.

---

## 070.0. Investigation préalable (obligatoire avant tout code)

- [x] Confirmer que les 5 services d'extraction (`ProcedureExtractionService`, `IsolementExtractionService`,
  `UnconditionalIsolementSheetExtractionService` — PLATINES/ORIFICES CAPACITES —, `AutresJointsTouchesExtractionService`,
  `DiversExtractionService`) reçoivent tous `SheetExtractionRule sheetRule` en paramètre de leur méthode
  `Extract(...)` — confirmé par lecture directe des 4 signatures + `ProcedureExtractionService` (qui
  calcule déjà `var sheet = sheetRule.SheetName;`) : `sheetRule.SheetName` est disponible partout où un
  `EquipementPivot`/`IsolementPivot` est construit, aucune signature de méthode publique à changer.
- [x] Compter les sites d'appel de production (pas les tests) de `new EquipementPivot(...)`/
  `new IsolementPivot(...)` — exactement 5, un par service d'extraction (`grep -n`) :
  `ProcedureExtractionService.cs:68`, `IsolementExtractionService.cs:126` (appel **positionnel**,
  `hasZeroEnergie` en 6ᵉ argument — **aucun nouveau paramètre ne doit s'insérer avant lui**, seulement en
  toute fin de liste, pour ne pas décaler silencieusement cet appel positionnel existant),
  `UnconditionalIsolementSheetExtractionService.cs:51`, `AutresJointsTouchesExtractionService.cs:55`,
  `DiversExtractionService.cs:61`.
- [x] Confirmer le nombre de fichiers construisant `new EquipementPivot(...)`/`new IsolementPivot(...)`
  au global (production + tests) — 7 et 8 fichiers respectivement (`grep -rl`) — cohérent avec le choix
  d'un **paramètre optionnel en toute fin de constructeur** (comme `hasZeroEnergie`/`couleurEtiquette`,
  Lots 063/068), pas d'un paramètre requis inséré au milieu (qui casserait l'appel positionnel identifié
  ci-dessus) ni en toute fin sans défaut (qui forcerait la mise à jour mécanique de tous les sites
  existants pour une information qui, dans les tests, est souvent sans intérêt pour l'assertion en cours).
- [x] Confirmer la position exacte de la colonne A actuelle sur `"Parents"`/`"Enfants"`
  (`DefaultProfileSeeder.BuildDefaultExportProfile`) — `"Repère"` (`PivotFieldRef.EquipementRepere`) pour
  `Parents`, `"Numéro"` (`PivotFieldRef.IsolementRepere`) pour `Enfants` — confirmé par lecture directe,
  `"Feuille"` s'insère donc en position d'index 1 (juste après) dans les deux listes `ColumnDefinitions`.
- [x] Confirmer que `SheetGenerationEngine`/`PivotFieldResolver` n'ont besoin d'aucune modification
  structurelle : une nouvelle colonne descriptive standard (comme `EquipementLocalisation`/
  `IsolementDesignation`) se branche entièrement via `PivotFieldRef` + une branche de résolution — le
  moteur de génération (ordre des colonnes, écriture des cellules) reste inchangé, seule
  `PivotFieldResolver.Resolve` gagne 2 nouvelles branches triviales (lecture directe d'une propriété
  `string`, aucune transformation).

---

## 070.1. Domain — `EquipementPivot.SourceSheetName`/`IsolementPivot.SourceSheetName` + `PivotFieldRef`

**Comportement attendu** :
- `EquipementPivot` gagne un nouveau paramètre de constructeur **optionnel, en toute dernière position**,
  `sourceSheetName = ""`, exposé en propriété `string SourceSheetName { get; }` (pas `init` — c'est une
  information connue et fixée dès la construction par `ProcedureExtractionService`, pas diffusée après
  coup comme `Localisation`/`Tableaux`/`Applications`). Aucune validation dédiée (la valeur vient toujours
  de `SheetExtractionRule.SheetName`, déjà non-vide par construction du profil — jamais une saisie
  utilisateur directe sur ce champ). Inclus dans `Equals`/`GetHashCode`.
- `IsolementPivot` gagne le même paramètre optionnel, `sourceSheetName = ""`, **après** `couleurEtiquette`
  (dernière position du constructeur actuel) — jamais avant `hasZeroEnergie`, pour ne pas décaler
  silencieusement l'appel positionnel de `IsolementExtractionService.cs:126` identifié en 070.0. Même
  traitement : propriété simple (pas `init`), pas de validation dédiée, inclus dans `Equals`/`GetHashCode`.
- `PivotFieldRef` gagne 2 nouveaux membres : `EquipementSourceSheet`, `IsolementSourceSheet`.
- `PivotFieldResolver.GetPivotSource` : les 2 nouveaux membres rattachés respectivement à
  `PivotSource.Equipement`/`PivotSource.Isolement`.
- `PivotFieldResolver.Resolve(EquipementPivot, PivotFieldRef)`/`Resolve(IsolementPivot, PivotFieldRef)` :
  2 nouvelles branches, lecture directe (`equipement.SourceSheetName`/`isolement.SourceSheetName`), aucune
  transformation.

**Tests** (Domain) :
- `EquipementPivotTests`/`IsolementPivotTests` (extension) : `SourceSheetName` — défaut `""` quand omis,
  valeur conservée telle quelle quand fournie, incluse dans `Equals`/`GetHashCode` (deux instances
  identiques sauf `SourceSheetName` diffèrent).
- `PivotFieldResolverTests` (extension) : les 2 nouveaux membres résolvent la bonne propriété,
  `GetPivotSource` les rattache au bon `PivotSource`.

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/EquipementPivot.cs`,
`src/ExcelETL.Domain/Extraction/Pivot/IsolementPivot.cs`, `src/ExcelETL.Domain/Generation/Fields/`.

---

## 070.2. Application — les 5 services d'extraction renseignent `SourceSheetName`

**Comportement attendu** : chaque site de construction identifié en 070.0 passe désormais explicitement
`sourceSheetName: sheetRule.SheetName` (argument nommé, pas positionnel — y compris sur l'appel déjà
positionnel de `IsolementExtractionService.cs:126`, qui devient un mélange positionnel + nommé pour ce
dernier argument, seule façon sûre d'ajouter un argument nommé après des arguments positionnels sans
tout réécrire) :
- `ProcedureExtractionService.cs:68` → `new EquipementPivot(repere, designation, equipementTypeElementNom, sourceSheetName: sheet)`
  (`sheet` = variable déjà existante, `sheetRule.SheetName`).
- `IsolementExtractionService.cs:126` → ajoute `sourceSheetName: sheetRule.SheetName`.
- `UnconditionalIsolementSheetExtractionService.cs:51`, `AutresJointsTouchesExtractionService.cs:55`,
  `DiversExtractionService.cs:61` → idem, `sourceSheetName: sheetRule.SheetName`.

**Tests** (Application) : pour chacun des 5 services, un test dédié confirme que le pivot construit porte
`SourceSheetName` égal au `SheetName` du `SheetExtractionRule` passé en paramètre (pas une constante en
dur — même garde-fou "anti-hardcoding" que `EquipementTypeElementNom`, Lot C1 : deux profils avec des
`SheetName` différents pour le même rôle logique doivent chacun se retrouver sur leur propre pivot).

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/Procedure/ProcedureExtractionService.cs`,
`src/ExcelETL.Application/Extraction/Oxo/Isolement/IsolementExtractionService.cs`,
`src/ExcelETL.Application/Extraction/Oxo/UnconditionalIsolementSheetExtractionService.cs`,
`src/ExcelETL.Application/Extraction/Oxo/AutresJointsTouches/AutresJointsTouchesExtractionService.cs`,
`src/ExcelETL.Application/Extraction/Oxo/Divers/DiversExtractionService.cs`.

---

## 070.3. Infrastructure — `DefaultProfileSeeder` : colonne `"Feuille"` en position B

**Comportement attendu** : `BuildDefaultExportProfile` insère, dans les deux règles `"Parents"`/`"Enfants"`,
une nouvelle `ColumnDefinition("Feuille", PivotFieldRef.EquipementSourceSheet)`/
`ColumnDefinition("Feuille", PivotFieldRef.IsolementSourceSheet)` en **index 1** de leur liste
`ColumnDefinitions` respective (juste après `"Repère"`/`"Numéro"`) :

```csharp
// Parents
new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
new ColumnDefinition("Feuille", PivotFieldRef.EquipementSourceSheet),
new ColumnDefinition("Type Elément", PivotFieldRef.EquipementTypeElementNom),
// ... reste inchangé, décalé d'un cran

// Enfants
new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
new ColumnDefinition("Feuille", PivotFieldRef.IsolementSourceSheet),
new ColumnDefinition("Type Elément", PivotFieldRef.IsolementTypeElementNom),
// ... reste inchangé, décalé d'un cran
```

**Tests** (Infrastructure) :
- `DefaultProfileSeederTests` : les règles `"Parents"`/`"Enfants"` du profil d'export seedé contiennent
  `"Feuille"` en 2ᵉ position (`Header`+`Source` — même style de comparaison ordre-indépendante/positionnelle
  que les assertions existantes de ce fichier), le reste des colonnes inchangé en contenu et en ordre
  relatif.
- `DefaultProfileSeederPipelineIntegrationTests` (fixtures réelles, profil tel que seedé et rechargé via
  `IImportProfileStore`/`IExportProfileStore` — jamais reconstruit à la main) : pour C7401 (et au moins un
  autre fixture couvrant plusieurs feuilles sources d'Isolements, ex. D8570 ou G6306B) —
  - `Parents` : la seule ligne (l'Équipement) porte `"Feuille" == "PROCEDURE"` (ou le nom d'onglet réel
    configuré dans le profil de test, si différent).
  - `Enfants` : au moins une ligne par feuille source réellement représentée dans le fixture porte la
    bonne valeur (`"ISOLEMENT"`, `"PLATINES"`, `"ORIFICES CAPACITES"` si non vide pour ce fixture,
    `"AUTRES JOINTS TOUCHES"` si non vide, `"DIVERS"` si non vide) — résolu dynamiquement depuis les
    autres colonnes déjà présentes sur la ligne (ex. `"Type Elément"`/valeurs connues des Lots 055/066)
    plutôt que par position de ligne en dur, pour rester robuste à un futur ajout de colonne.
  - Non-régression : l'en-tête complet des deux feuilles (résolu dynamiquement, même convention que les
    tests de clôture des Lots 066/067/068/069) place bien `"Feuille"` en position B, tout le reste décalé
    d'un cran mais dans le même ordre relatif qu'avant ce lot.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs`.

---

## Ordre d'implémentation recommandé

070.0 → 070.1 → 070.2 → 070.3 (chaîne strictement séquentielle : 070.2 a besoin du nouveau paramètre de
070.1 pour compiler, 070.3 a besoin des 2 nouveaux `PivotFieldRef` de 070.1 et du comportement de 070.2
pour que l'assertion d'intégration ait un sens).
