# Tickets TDD — Lot 069 : complétion des colonnes des feuilles `TM_PROC_MAD`/`TM_PROC_REL`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : comparaison directe (ClosedXML, hors suite de tests) entre la trame de référence client
`tests/Fixtures/OXO.TRAME.IMPORT.MAD.xlsx` (23 colonnes, identiques sur les deux feuilles) et un export
réel généré avec le profil standard seedé (`Dossier.de.MaD.IDL.-.C7401_export_20260904-134946.xlsx`,
8 colonnes). 7 colonnes correspondent déjà (`REPERE TM`↔`Repère TM`, `TYPE ELEMENT CODE`, `Ordre`,
`Action`, `Acteur`, `Risques`, `COLONNE TRX`↔`Colonne Travaux`) ; `Date de validation` est en plus dans
l'export (pas de contrepartie dans la trame — conservée, personne n'a demandé de la retirer). Les 16
colonnes restantes de la trame sont traitées ici, sur indications explicites de Simon (voir
conversation) : mapping, valeur toujours vide, valeur constante, ou colonne non reportée.

---

## Décisions actées (résumé, ne pas rouvrir)

Traitement des 16 colonnes manquantes, par indication client :

| Colonne (trame) | Traitement |
|---|---|
| `GUID` | pas de correspondance — colonne présente, cellule toujours vide |
| `TYPE TACHE` | mappée → code de la feuille (`"TM_PROC_MAD"`/`"TM_PROC_REL"`), déjà porté par `TacheMultiplePivot.TypeTacheMultipleCode` |
| `ZONE` | mappée → zone de l'équipement (`EquipementPivot.Localisation`), diffusée sur chaque `TacheMultiplePivot` |
| `LOC2` | colonne présente, toujours vide pour le moment (même traitement que `LOC2` sur `Parents`) |
| `LOC3` | colonne présente, toujours vide pour le moment (même traitement que `LOC3` sur `Parents`) |
| `LOT` | colonne présente, toujours vide pour le moment |
| `Ressource` | colonne présente, toujours vide pour le moment |
| `Ligne` | mappée → numéro de ligne dans le fichier Excel source — nouveau champ sur le pivot |
| `Type` | **non reportée** (colonne absente de l'export — client : "j'ignore à quoi elle correspond") |
| `CRITERE` | colonne présente, valeur constante `"A faire"` sur toutes les lignes |
| `AVANCEMENT` | colonne présente, valeur constante `"0"` sur toutes les lignes |
| `AVANCEMENT POINT` | **non reportée** |
| `SIGNATURE` | **non reportée** |
| `DERNIERE MODIF` | **non reportée** |
| `UTILISATEUR` | **non reportée** |
| `SUPPRESSION` | colonne présente, valeur constante `"N"` sur toutes les lignes |

**Distinction retenue** (déduite du vocabulaire employé par le client, pas une extrapolation) : "toujours
vide pour le moment" = colonne conservée dans le schéma, cellule vide (même famille que `GUID`, même
traitement que `LOC2`/`LOC3` sur `Parents`, cf. `DefaultProfileSeeder.BuildDefaultExportProfile`) ; "on
ignore"/"on ne reporte pas cette colonne" = colonne absente de la feuille générée (`Type`, `AVANCEMENT
POINT`, `SIGNATURE`, `DERNIERE MODIF`, `UTILISATEUR`).

**Nouveau mécanisme requis, absent du modèle actuel** : aucun moyen d'exprimer "cette colonne porte
toujours la même valeur littérale, indépendamment de la ligne" (`ColumnDefinition.Source` est soit un
`PivotFieldRef` réel, soit `null` → cellule vide — pas de troisième état "valeur fixe"). Nouveau type
`ConstantColumnDefinition(Header, Value)` (Domain), sur le même modèle que `PointColumnDefinition`/
`ApplicationColumnDefinition` — voir 069.1. Volontairement **indépendant de tout `PivotSource`** :
contrairement à `PointColumnDefinition`/`ApplicationColumnDefinition` (interdits pour
`PivotSource.TacheMultiple`, aucune notion de Point/Application sur une tâche multiple), une colonne
constante ne dépend d'aucune donnée pivot — elle est valide pour n'importe quel `PivotSource`, y compris
`TacheMultiple`.

**Ordre des colonnes retenu** (guidage non-bloquant, même statut que les décisions de positionnement des
Lots 066/067 — les 8 colonnes déjà mappées gardent leur ordre relatif actuel, les nouvelles sont
intercalées en suivant les regroupements de la trame de référence) :

`GUID`, `TYPE TACHE`, `Repère TM`, `ZONE`, `LOC2`, `LOC3`, `TYPE ELEMENT CODE`, `LOT`, `Ressource`,
`Ligne`, `Ordre`, `Action`, `Acteur`, `Risques`, `Date de validation`, `Colonne Travaux`, `CRITERE`,
`AVANCEMENT`, `SUPPRESSION`.

---

## Hors périmètre explicite de ce lot (ne pas rouvrir)

- La colonne `Type` et son sens métier (client : "je ne sais pas à quoi elle correspond") — non reportée,
  à traiter dans un lot séparé une fois clarifiée.
- `AVANCEMENT POINT`/`SIGNATURE`/`DERNIERE MODIF`/`UTILISATEUR` — non reportées par décision explicite.
- Exposition de `ConstantColumnDefinition` dans `ExportProfileEditor.razor`/`SheetGenerationRuleForm.razor`
  — même déférence que `HeaderFieldRule`/`FieldPresencePointRule` (Lot 047→048) et `CouleurEtiquetteCell`
  (Lot 068) : seulement modifiable via `DefaultProfileSeeder.cs` pour l'instant, UI à ajouter dans un lot
  dédié si le besoin se confirme.
- Migration de données pour un profil déjà seedé dans un environnement existant — même raisonnement que
  les Lots 066/067 : base de données jetable en pré-production, un reseed suffit.
- Toute modification des feuilles `Parents`/`Enfants` (hors périmètre : ce lot ne touche que la règle
  `"Tâches multiples"`).
- Renommage ou repositionnement des 7 colonnes déjà mappées (`Repère TM`, `TYPE ELEMENT CODE`, `Ordre`,
  `Action`, `Acteur`, `Risques`, `Colonne Travaux`) — personne n'a demandé de les modifier, seulement
  d'ajouter les colonnes manquantes.
- Retrait de `Date de validation` (aucune contrepartie dans la trame, mais personne n'a demandé sa
  suppression — signalé à Simon comme point ouvert, pas tranché unilatéralement ici).

---

## 069.0. Investigation préalable (obligatoire avant tout code)

- [x] Confirmer qu'aucun mécanisme de "colonne à valeur constante" n'existe dans le modèle de génération
  actuel — confirmé, `ColumnDefinition.Source` (`PivotFieldRef?`) est le seul point d'entrée, `null`
  signifiant "cellule vide", pas "valeur fixe".
- [x] Confirmer que `TacheMultiplePivot.TypeTacheMultipleCode` porte déjà exactement la valeur attendue
  pour `TYPE TACHE` (`"TM_PROC_MAD"`/`"TM_PROC_REL"`) — confirmé
  (`src/ExcelETL.Domain/Extraction/Pivot/TacheMultiplePivot.cs`), aucune transformation nécessaire, juste
  un nouveau membre `PivotFieldRef` + branche de résolution.
- [x] Confirmer le mécanisme de diffusion existant pour `Repere`/`TypeElementNom`/`ColonneTravaux`
  (`ImportPipelineOrchestrator.BroadcastTachesMultiplesContext`, `with { ... }` après construction de
  `equipement`) — à réutiliser à l'identique pour `Localisation` (déjà disponible sur `equipement` à cet
  endroit, puisque diffusée juste avant).
- [x] Confirmer que `blockStartRow`, dans `ProcedureExtractionService.ReadTachesMultiples`, est bien le
  numéro de ligne réel de la feuille PROCEDURE pour chaque tâche (pas un index 0-based dans la liste) —
  confirmé par lecture directe de la boucle (`locator.FirstBlockStartRow + blockIndex * locator.Step`).
- [x] Confirmer que `LOC2`/`LOC3` existent déjà sur `Parents` comme colonnes non mappées (`Source: null`)
  — confirmé (`DefaultProfileSeeder.BuildDefaultExportProfile`), même traitement à reproduire pour
  `TM_PROC_MAD`/`TM_PROC_REL`.
- [x] Compter les sites d'appel directs de `new TacheMultiplePivot(...)` (7 fichiers, `grep -rl`) et de
  `new SheetGenerationRule(...)` (20 fichiers) — confirme le choix : `LigneSource` en paramètre de
  constructeur **requis** (valeur réelle systématiquement connue à la construction, contrairement à
  `Repere`/`TypeElementNom`/`ColonneTravaux`/`Localisation` qui restent diffusés après coup) ; en
  revanche `ConstantColumnDefinitions` sur `SheetGenerationRule` en **dernier paramètre optionnel**
  (`= null`, normalisé en `[]`) pour ne pas forcer la mise à jour des 20 sites d'appel existants — la
  plupart des règles (`Parents`/`Enfants`) n'en ont pas besoin.
- [x] Confirmer le fichier de test d'intégration réel pour `ProcedureExtractionService`
  (`tests/ExcelETL.Infrastructure.Tests/Excel/ProcedureExtractionServiceIntegrationTests.cs`, pas un
  chemin `.../Procedure/...` supposé) — chemin exact confirmé par recherche.

---

## 069.1. Domain — `ConstantColumnDefinition` + `SheetGenerationRule.ConstantColumnDefinitions`

**Comportement attendu** :
- Nouveau `src/ExcelETL.Domain/Generation/Profile/ConstantColumnDefinition.cs` : `sealed record
  ConstantColumnDefinition(string Header, string Value)`, même forme que
  `PointColumnDefinition`/`ApplicationColumnDefinition` — constructeur validant `Header`/`Value`
  non-vides (`DomainValidationException`, nouveaux `DomainErrorCode.ConstantColumnDefinition_EmptyHeader`/
  `_EmptyValue` — même rationale que `PointColumnDefinition.MarkValue` : une valeur constante vide serait
  visuellement indiscernable d'une cellule non écrite).
- `SheetGenerationRule` gagne une 4ᵉ collection `ConstantColumnDefinitions` (`IReadOnlyList<ConstantColumnDefinition>`,
  backing field privé mutable + ctor EF sans validation — même pattern que `_columnDefinitions`/
  `_pointColumnDefinitions`/`_applicationColumnDefinitions`) — **dernier paramètre optionnel**
  (`= null`, normalisé en `[]`) sur le constructeur public à 5 paramètres, qui en compte alors 6.
  **Aucune** restriction par `PivotSource` (contrairement à `PointColumnDefinitions`/
  `ApplicationColumnDefinitions`, interdits pour `TacheMultiple`) : une colonne constante ne référence
  aucune donnée pivot, elle est valide pour n'importe quel `PivotSource`.
- `ValidateNoDuplicateHeaders` étendue pour inclure les headers de `ConstantColumnDefinitions` dans la
  détection de doublon (même liste combinée que Column/Point/Application aujourd'hui).
- Pas de détection de doublon de `Value` (rien n'empêche deux colonnes constantes différentes de partager
  la même valeur littérale — contrairement à `ColonneNom`/`ApplicationNom`, qui identifient une donnée
  réelle du pivot).
- `Equals`/`GetHashCode` étendus (`SequenceEqual`/boucle `hash.Add`, même mécanique que les 3 autres
  collections).
- EF Core (`ExportProfileConfiguration.cs`) : `rules.OwnsMany(r => r.ConstantColumnDefinitions, ...)` —
  table `ExportProfileSheetRuleConstantColumnDefinitions`, FK `SheetGenerationRuleId`, clé fantôme
  `int Id`, `Header`/`Value` `IsRequired().HasMaxLength(200)` (même longueur que `Header` sur les 3
  autres types de colonne). Nouvelle migration EF (générée via `dotnet ef migrations add`, jamais écrite
  à la main).
- `.resx` : `DomainErrorMessages.resx`/`.fr.resx` gagnent les 2 nouvelles clés
  (`ConstantColumnDefinition_EmptyHeader`/`_EmptyValue`).

**Tests** (Domain + Infrastructure) :
- `ConstantColumnDefinitionTests` (nouveau fichier) : Header/Value vide rejeté, cas nominal, record
  equality structurelle.
- `SheetGenerationRuleTests` (extension) : paramètre omis → `ConstantColumnDefinitions` vide ; liste
  fournie → conservée ; header en double entre une colonne constante et une colonne descriptive/Point/
  Application → rejeté ; colonne constante **acceptée** pour `PivotSource.TacheMultiple` (contrairement
  aux colonnes Point/Application, non-régression explicite de cette différence de traitement) ; `Equals`/
  `GetHashCode` incluent les colonnes constantes.
- `EfExportProfileStoreTests` : round-trip complet d'une règle avec des `ConstantColumnDefinitions`
  (plusieurs entrées, round-trip d'une liste vide, round-trip sur une règle `PivotSource.TacheMultiple`).

**Dossier** : `src/ExcelETL.Domain/Generation/Profile/`,
`src/ExcelETL.Infrastructure/Persistence/Configurations/ExportProfileConfiguration.cs`.

---

## 069.2. Domain — `TacheMultiplePivot.LigneSource`/`Localisation` + `PivotFieldRef`

**Comportement attendu** :
- `TacheMultiplePivot` gagne **un nouveau paramètre de constructeur requis**, `ligneSource` (`int`),
  exposé en propriété `LigneSource` — **pas** un `init` diffusé après coup comme
  `Repere`/`TypeElementNom`/`ColonneTravaux` : la valeur est réellement connue au moment de la
  construction de chaque tâche (le numéro de ligne dans la feuille source), contrairement aux 3 autres
  champs qui dépendent de l'Équipement du run entier, connu seulement après coup. Aucune validation
  (n'importe quel entier positif est légitime ; pas de contrainte "doit être positif" demandée, la valeur
  vient toujours d'un calcul interne fiable, jamais d'une saisie utilisateur).
- `TacheMultiplePivot` gagne aussi `Localisation` (`init` property, `string`, défaut `""`) — même
  mécanisme de diffusion après coup que `Repere`/`TypeElementNom`/`ColonneTravaux` (Lot 067), puisque la
  zone de l'équipement n'est connue elle aussi qu'après résolution complète de l'Équipement (broadcast du
  `loc1` de DIVERS, `ImportPipelineOrchestrator`).
- `PivotFieldRef` gagne `TacheMultipleTypeTacheMultipleCode` (lit `TypeTacheMultipleCode` directement,
  aucune transformation), `TacheMultipleLocalisation` (lit `Localisation`), `TacheMultipleLigneSource`
  (lit `LigneSource`, formaté `CultureInfo.InvariantCulture` comme `TacheMultipleOrdre` — mais sans le
  `?`/repli `""`, puisque `LigneSource` n'est jamais nul).
- `PivotFieldResolver.GetPivotSource`/`Resolve(TacheMultiplePivot, PivotFieldRef)` gagnent les 3
  nouvelles branches.

**Tests** (Domain) :
- `TacheMultiplePivotTests` (extension) : `LigneSource` round-trip via le constructeur (paramètre requis
  — tous les sites d'appel existants du fichier de test doivent être mis à jour, pas de valeur par
  défaut) ; `Localisation` : défaut `""`, `with` fonctionne.
- `PivotFieldResolverTests` (extension) : les 3 nouveaux membres résolvent la bonne propriété,
  `GetPivotSource` les rattache à `PivotSource.TacheMultiple`.

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/TacheMultiplePivot.cs`,
`src/ExcelETL.Domain/Generation/Fields/`.

**Sites d'appel à mettre à jour mécaniquement** (7 fichiers, confirmés par grep en 069.0) :
`tests/ExcelETL.Domain.Tests/Generation/Fields/PivotFieldResolverTests.cs`,
`tests/ExcelETL.Application.Tests/Extraction/Oxo/ImportPipelineOrchestratorTests.cs`,
`tests/ExcelETL.Domain.Tests/Extraction/Pivot/TacheMultiplePivotTests.cs`,
`tests/ExcelETL.Application.Tests/Generation/SheetGenerationEngineTests.cs`,
`tests/ExcelETL.Application.Tests/Extraction/Oxo/Procedure/ProcedureExtractionServiceTests.cs`,
`src/ExcelETL.Application/Extraction/Oxo/Procedure/ProcedureExtractionService.cs`,
`tests/ExcelETL.Application.Tests/Extraction/Oxo/Procedure/TacheMultipleSectionGrouperTests.cs`.

---

## 069.3. Application — `ProcedureExtractionService` (renseigne `LigneSource`) et `ImportPipelineOrchestrator` (diffuse `Localisation`)

**Comportement attendu** :
- `ProcedureExtractionService.ReadTachesMultiples` : le `new TacheMultiplePivot(...)` construit dans la
  boucle passe désormais `ligneSource: blockStartRow` (la variable existe déjà dans la boucle, aucun
  calcul nouveau).
- `ImportPipelineOrchestrator.BroadcastTachesMultiplesContext` : le `with { ... }` gagne
  `Localisation = equipement.Localisation` (disponible à cet endroit, `equipement` ayant déjà reçu son
  propre `Localisation` via le broadcast DIVERS quelques lignes plus haut dans `Run`).

**Tests** (Application) :
- `ProcedureExtractionServiceTests` : pour un bloc de tâches multiples à plusieurs lignes (y compris une
  ligne factice), `LigneSource` de chaque `TacheMultiplePivot` correspond exactement au numéro de ligne
  attendu (`FirstBlockStartRow + index * Step`), pas à l'index dans la liste retournée.
- `ImportPipelineOrchestratorTests` : `Localisation` de chaque `TacheMultiplePivot` diffusée = celle de
  l'Équipement du run (y compris le cas `loc1` vide — no-op, cohérent avec le traitement de
  `Repere`/`TypeElementNom`) ; non-régression des diffusions déjà en place (`Repere`, `TypeElementNom`,
  `ColonneTravaux`).

**Dossier** : `src/ExcelETL.Application/Extraction/Oxo/Procedure/ProcedureExtractionService.cs`,
`src/ExcelETL.Application/Extraction/Oxo/ImportPipelineOrchestrator.cs`.

---

## 069.4. Application — `SheetGenerationEngine` écrit les colonnes constantes

**Comportement attendu** :
- `GenerateSheet` (chemin Équipement/Isolement) : l'en-tête devient `ColumnDefinitions` →
  `ConstantColumnDefinitions` → `ApplicationColumnDefinitions` → `PointColumnDefinitions` (nouvel ordre
  fixe — `Parents`/`Enfants` n'ont aujourd'hui aucune colonne constante, donc leur en-tête généré ne
  change pas ; seul l'emplacement où une future colonne constante s'insérerait est désormais défini).
  `GenerateEquipementRows`/`GenerateIsolementRows` gagnent chacune une troisième séquence de cellules,
  `constantCells = rule.ConstantColumnDefinitions.Select(c => c.Value)`, insérée entre les cellules
  descriptives et les cellules Application dans le `GeneratedRow` final.
- `GenerateTacheMultipleSheets` : l'en-tête devient `ColumnDefinitions` → `ConstantColumnDefinitions`
  (pas d'Application/Point ici, toujours interdits pour ce `PivotSource`). Chaque `GeneratedRow` gagne les
  cellules constantes (valeur `c.Value`, identique pour toutes les lignes, y compris les lignes
  factices — une colonne constante ne connaît aucune notion de ligne factice, elle écrit
  inconditionnellement).

**Tests** (Application) :
- `SheetGenerationEngineTests` : une règle Équipement/Isolement/TacheMultiple avec des
  `ConstantColumnDefinitions` produit la valeur attendue sur toutes les lignes, y compris zéro ligne
  (feuille Équipement rejetée en amont → toujours zéro ligne, pas d'exception) ; non-régression : une
  règle sans colonne constante produit exactement le même en-tête/lignes qu'avant ce lot.

**Dossier** : `src/ExcelETL.Application/Generation/SheetGenerationEngine.cs`.

---

## 069.5. Infrastructure — `DefaultProfileSeeder` : nouvelles colonnes sur `BuildTacheMultipleSheetRule`

**Comportement attendu** : `BuildTacheMultipleSheetRule` (méthode partagée entre le chemin de seed
nominal et la migration `MigrateTacheMultipleSheetRuleIfMissingAsync`, Lot T8 — donc les deux chemins
héritent automatiquement des nouvelles colonnes) devient :

```csharp
new SheetGenerationRule(
    "Tâches multiples",
    PivotSource.TacheMultiple,
    [
        new ColumnDefinition("GUID", null),
        new ColumnDefinition("TYPE TACHE", PivotFieldRef.TacheMultipleTypeTacheMultipleCode),
        new ColumnDefinition("Repère TM", PivotFieldRef.TacheMultipleRepere),
        new ColumnDefinition("ZONE", PivotFieldRef.TacheMultipleLocalisation),
        new ColumnDefinition("LOC2", null),
        new ColumnDefinition("LOC3", null),
        new ColumnDefinition("TYPE ELEMENT CODE", PivotFieldRef.TacheMultipleTypeElementNom),
        new ColumnDefinition("LOT", null),
        new ColumnDefinition("Ressource", null),
        new ColumnDefinition("Ligne", PivotFieldRef.TacheMultipleLigneSource),
        new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
        new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
        new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
        new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
        new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation),
        new ColumnDefinition("Colonne Travaux", PivotFieldRef.TacheMultipleColonneTravaux)
    ],
    [],
    [],
    [
        new ConstantColumnDefinition("CRITERE", "A faire"),
        new ConstantColumnDefinition("AVANCEMENT", "0"),
        new ConstantColumnDefinition("SUPPRESSION", "N")
    ]);
```

**Tests** (Infrastructure) :
- `DefaultProfileSeederTests` : profil d'export seedé — la règle `"Tâches multiples"` contient les 19
  colonnes attendues (mêmes `Header`/`Source`/valeur constante que ci-dessus), dans cet ordre.
- `DefaultProfileSeederPipelineIntegrationTests` (fixture C7401, déjà connue pour produire
  `TM_PROC_MAD`/`TM_PROC_REL`) : pour une ligne prise sur chaque feuille dynamique — `GUID`/`LOC2`/`LOC3`/
  `LOT`/`Ressource` vides ; `TYPE TACHE` = le code de la feuille (`"TM_PROC_MAD"` ou `"TM_PROC_REL"`) ;
  `ZONE` = la zone de l'Équipement (même valeur que `Parents`/`Enfants`' colonne `Zone` pour ce run) ;
  `Ligne` = un entier correspondant à une ligne réelle de la feuille PROCEDURE (au minimum : non vide,
  parseable, cohérent entre deux tâches consécutives d'un même bloc — pas nécessairement une valeur
  exacte câblée en dur, sensible au moindre décalage de plage) ; `CRITERE` = `"A faire"` ;
  `AVANCEMENT` = `"0"` ; `SUPPRESSION` = `"N"` sur toutes les lignes des deux feuilles.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs`.

---

## Ordre d'implémentation recommandé

069.0 → 069.1 → 069.2 → 069.3 → 069.4 → 069.5. 069.1 et 069.2 sont indépendants entre eux (deux
sous-systèmes distincts — colonnes constantes vs. nouveaux champs du pivot) et peuvent être menés dans
n'importe quel ordre ou en parallèle ; 069.3/069.4 en dépendent tous les deux ; 069.5 dépend de
l'ensemble (sans lui, les nouvelles colonnes seedées produiraient un résultat silencieusement vide/faux).
