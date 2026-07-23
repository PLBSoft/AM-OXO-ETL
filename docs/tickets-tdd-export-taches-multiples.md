# Tickets TDD — Lot T : feuilles Tâches Multiples dynamiques dans le fichier cible

*Document vivant (pas de suffixe de date). Étend le modèle `ExportProfile`/`SheetGenerationRule`
du Lot I/J pour couvrir la génération des feuilles Tâches Multiples, explicitement laissée hors
périmètre par ces deux lots (`tickets-tdd-blazor-profil-export.md`, section "Hors périmètre
explicite"). S'appuie sur `TacheMultiplePivot` (`spec-extraction-fichier-source-oxo.md` §2.2),
déjà produit par le pipeline d'extraction — ce lot ne modifie rien côté extraction/import.*

**Décisions actées avec le client, remplaçant les points ouverts précédents** :
- **Généricité, pas de liste statique** : le nombre de `TypeTacheMultipleCode` distincts n'est
  **pas** figé à `TM_PROC_MAD`/`TM_PROC_REL`. Le client est susceptible d'introduire de nouveaux
  types à l'avenir ; une approche statique (une `SheetGenerationRule` par code connu) coûterait
  cher à chaque nouveau type. Le moteur doit donc **découvrir les codes présents à l'exécution**
  et générer une feuille par code distinct rencontré dans `ImportResult.TachesMultiples`, sans
  modification de profil ni de code à chaque nouveau type.
- **Nommage des feuilles** : le code technique brut (`TypeTacheMultipleCode`, ex. `"TM_PROC_MAD"`,
  `"TM_PROC_REL"`) est utilisé tel quel comme nom de feuille Excel. Pas de libellé métier
  convivial à ce stade.
- **Lignes factices** (`TacheMultiplePivot.EstFactice = true`, lignes de présentation sans
  `Ordre`) : incluses telles quelles dans le fichier cible, sans colonne ni marque distinctive —
  fidélité à la structure du fichier source, cohérent avec la règle métier déjà actée en
  extraction (`spec-extraction-fichier-source-oxo.md` §1.2).

**Conventions déjà en place à respecter** (Lots I/J) :
- `SheetGenerationRule` porte `PivotSource` et une liste de `ColumnDefinition`
  (`Header`/`Source: PivotFieldRef?`).
- Validation croisée `PivotSource`/`PivotFieldRef` existante (I2) : un `ColumnDefinition.Source`
  doit référencer un champ compatible avec le `PivotSource` de sa feuille.
- IDs HTML stables sur chaque élément interactif Blazor, jamais de sélection par texte/position.
- Construction directe de l'objet Domain réel dans un `try/catch`, erreurs localisées via
  `BusinessExceptionLocalizer`.
- xUnit 2.9.3 + FluentAssertions 7.x + Moq, bUnit pour les composants Razor.

**Hors périmètre explicite de ce lot** :
- Toute modification de l'extraction (feuille PROCEDURE, alias `R9`, règle `EstFactice`) —
  ce lot consomme `TacheMultiplePivot` tel qu'il existe déjà, sans y toucher.
- Exposition des champs `DataTacheMultiple`/`ItemTacheMultiple` (valeurs détaillées par type de
  tâche multiple côté legacy `AvancementRecette`) — hors périmètre du pipeline d'extraction
  pivot actuel, non couvert par `TacheMultiplePivot`.
- Tri configurable ou pagination des feuilles générées.
- Regroupement dynamique généralisé à `Equipement`/`Isolement` — cette mécanique de
  regroupement-par-valeur est **spécifique à `PivotSource = TacheMultiple`**, pas une primitive
  générique du moteur (voir T5, note de conception).

---

## T1. Domain — `PivotSource.TacheMultiple` + `PivotFieldRef` associés

**Comportement attendu** :
- Ajout de la valeur `TacheMultiple` à l'enum `PivotSource` (aux côtés de `Equipement`/`Isolement`).
- Ajout des valeurs `PivotFieldRef` correspondant aux champs de `TacheMultiplePivot` exposables en
  colonne : `TacheMultipleOrdre`, `TacheMultipleAction`, `TacheMultipleActeur`,
  `TacheMultipleRisques`, `TacheMultipleDateValidation`. **Pas** de `PivotFieldRef` pour
  `TypeTacheMultipleCode` (ce champ pilote le regroupement en feuilles, ce n'est pas une colonne
  du contenu — voir T5) ni pour `EstFactice` (jamais affiché, décision actée ci-dessus).

**Tests** (Domain) :
- Chaque nouvelle valeur `PivotFieldRef` est bien distincte des valeurs `Equipement*`/`Isolement*`
  existantes (pas de doublon de nom/valeur numérique).
- Construction d'un `ColumnDefinition(Source: PivotFieldRef.TacheMultipleAction)` valide sans
  exception.

---

## T2. Domain — invariants `SheetGenerationRule` pour `PivotSource = TacheMultiple`

**Comportement attendu**, extension de la validation croisée existante (I2) :
- Si `PivotSource = TacheMultiple` : rejet de toute `ColumnDefinition.Source` qui référence un
  `PivotFieldRef` `Equipement*`/`Isolement*` (symétrique de la règle déjà en place dans l'autre
  sens).
- Si `PivotSource = TacheMultiple` : rejet de toute `PointColumnDefinition` ajoutée à la règle —
  une tâche multiple n'a pas de `Point` associé (structurellement distinct, voir
  `modele-domaine-import-profile.md`), donc aucune colonne Point n'a de sens ici. Nouvelle
  exception métier dédiée (localisée), pas une simple validation UI.
- Le champ `SheetName` de `SheetGenerationRule` change de rôle pour ce `PivotSource` : il devient
  un **libellé interne** (affiché dans la liste des règles en admin, ex. "Tâches multiples"), et
  n'est **jamais** utilisé comme nom de feuille dans le fichier généré — voir T5 pour le nommage
  réel. Toujours requis non-vide (même règle de validation qu'aujourd'hui), juste réinterprété.

**Tests** (Domain) :
- `PivotSource = TacheMultiple` + `ColumnDefinition.Source = PivotFieldRef.EquipementRepere` →
  exception de validation croisée, message localisé distinct du message symétrique existant.
- `PivotSource = TacheMultiple` + ajout d'une `PointColumnDefinition` → exception dédiée levée,
  règle non construite.
- `PivotSource = TacheMultiple` + `ColumnDefinition.Source = PivotFieldRef.TacheMultipleAction` →
  construction valide (cas nominal).
- Non-régression : les règles `PivotSource = Equipement`/`Isolement` existantes ne sont pas
  affectées par les nouvelles validations (tests déjà existants toujours verts).

---

## T3. Application — `SheetGenerationEngine` : génération dynamique par code

**Comportement attendu** :
- Pour chaque `SheetGenerationRule` avec `PivotSource = TacheMultiple` dans le profil d'export :
  1. Grouper `ImportResult.TachesMultiples` par `TypeTacheMultipleCode` (égalité de chaîne
     stricte, pas de normalisation — les codes sont des constantes internes déjà propres, pas de
     saisie utilisateur).
  2. Pour chaque groupe (code distinct), générer **une feuille physique** nommée avec ce code
     brut, contenant une ligne par `TacheMultiplePivot` du groupe (y compris les lignes
     `EstFactice = true`, sans filtrage ni marquage), colonnes = celles définies par la
     `ColumnDefinition` de la règle, dans l'ordre de définition.
  3. **Ordre des lignes à l'intérieur d'une feuille** : ordre d'apparition dans
     `ImportResult.TachesMultiples` (ordre d'extraction), **pas** un tri par `Ordre` — cohérent
     avec la décision "fidélité à la structure source" (une ligne factice sans `Ordre` reste à sa
     position d'origine, pas reléguée en fin de liste).
  4. **Ordre des feuilles générées entre elles** : tri alphabétique du code (déterministe, pour
     des tests et une sortie reproductibles — pas d'ordre "d'apparition" qui dépendrait de l'ordre
     interne d'un `Dictionary`).
  5. Si `ImportResult.TachesMultiples` est vide : **aucune feuille Tâches Multiples générée**
     (pas de feuille vide).
- Les feuilles Tâches Multiples sont générées **après** `Parents`/`Enfants` dans le classeur final
  (ordre des règles telles que définies dans le profil, `Parents`/`Enfants` en premier par
  convention actuelle du profil par défaut — voir T7).

*Note de conception* : ce regroupement dynamique est **spécifique au `PivotSource =
TacheMultiple`**. Ce n'est pas une primitive générique de regroupement offerte à
`Equipement`/`Isolement` (qui restent 1 feuille = 1 `SheetGenerationRule`, sans groupement) — pas
de sur-ingénierie non justifiée par le besoin actuel.

**Tests** (Application) :
- `ImportResult` avec des `TacheMultiplePivot` de codes `"TM_PROC_MAD"` et `"TM_PROC_REL"` +
  profil avec une seule règle `PivotSource = TacheMultiple` → génère bien 2 feuilles physiques
  nommées exactement `"TM_PROC_MAD"` et `"TM_PROC_REL"`.
- Contenu de chaque feuille généré : bon nombre de lignes par code, bonnes valeurs de colonnes
  (`Ordre`/`Action`/`Acteur`/`Risques`/`DateValidation`), lignes `EstFactice = true` présentes
  telles quelles (pas de colonne supplémentaire, `Ordre` vide/null affiché tel quel).
- Ordre des lignes : test dédié avec une ligne factice intercalée entre deux lignes réelles →
  position préservée dans la feuille générée (pas de tri qui la déplacerait).
- Ordre des feuilles : codes ajoutés dans un ordre arbitraire au niveau du pivot → feuilles
  produites triées alphabétiquement dans le classeur final.
- `TachesMultiples` vide → aucune feuille Tâches Multiples dans le classeur (feuilles
  `Parents`/`Enfants` seules, non-régression).
- Non-régression complète des tests `SheetGenerationEngine` existants pour
  `Equipement`/`Isolement` (aucune règle `PivotSource = TacheMultiple` dans le profil → même
  comportement qu'avant ce lot).

---

## T4. Application/Infrastructure — nommage de feuille défensif

**Comportement attendu** : avant d'écrire une feuille via `ClosedXmlWorkbookWriter`, le nom de
feuille dérivé d'un `TypeTacheMultipleCode` est validé/assaini pour rester compatible avec les
contraintes Excel (max 31 caractères, caractères interdits `\ / ? * [ ] :`). Défensif — aucun code
connu aujourd'hui (`TM_PROC_MAD`, `TM_PROC_REL`) ne déclenche cette règle, mais un futur code
fourni par le client pourrait la violer sans que ce lot soit revisité.
- Règle : remplacement de tout caractère interdit par `_`, troncature à 31 caractères si
  nécessaire.

**Tests** (Application ou Infrastructure selon où vit la responsabilité) :
- Code contenant un caractère interdit (ex. `"TM/PROC:MAD"` fictif) → nom de feuille assaini
  (`"TM_PROC_MAD"`), pas d'exception `ClosedXML`.
- Code de plus de 31 caractères (fictif) → nom de feuille tronqué à 31 caractères, pas
  d'exception.
- Codes réels actuels (`TM_PROC_MAD`, `TM_PROC_REL`) → non modifiés par l'assainissement (test de
  non-régression explicite, pour garantir que la règle défensive ne casse pas le cas nominal).

---

## T5. Infrastructure — `DefaultProfileSeeder` : ajout de la règle Tâches Multiples au profil par défaut

**Comportement attendu** : le profil d'export par défaut seedé (`M3`,
`tickets-tdd-seed-profils-defaut.md`) gagne une troisième `SheetGenerationRule` :
- `SheetName` (libellé interne) : `"Tâches multiples"`.
- `PivotSource = TacheMultiple`.
- Colonnes : `Ordre` → `PivotFieldRef.TacheMultipleOrdre`, `Action` → `TacheMultipleAction`,
  `Acteur` → `TacheMultipleActeur`, `Risques` → `TacheMultipleRisques`, `Date de validation` →
  `TacheMultipleDateValidation`.
- Aucune `PointColumnDefinition` (rejetée par construction, voir T2).
- Guid constant stable dédié pour cette règle, cohérent avec l'idempotence déjà en place pour le
  reste du profil seedé (pas de recréation/duplication à chaque redémarrage).

**Tests** (Infrastructure, contre les 3 fixtures réelles) :
- Non-régression : le profil seedé reste valide et les tests d'intégration existants
  (`Parents`/`Enfants`) restent verts.
- Nouveau test contre au moins une fixture réelle contenant des `TacheMultiplePivot` (vérifier
  laquelle des 3 fixtures — C7401/D8570/G6306B — en produit, potentiellement plusieurs) : le
  fichier généré via le profil seedé contient bien une ou plusieurs feuilles `TM_PROC_*`, avec un
  nombre de lignes cohérent avec l'`ImportResult` de cette fixture.

---

## T6. Blazor — `ExportProfileEditor.razor` : support de `PivotSource = TacheMultiple`

**Comportement attendu** :
- Le select `#sheet-generation-rule-pivot-source-select` propose une troisième option
  `TacheMultiple`.
- Quand `TacheMultiple` est sélectionné pour une feuille :
  - Le select `#column-header-input`/`Source` (`#column-...-select` selon l'ID existant) ne
    propose que les `PivotFieldRef` `TacheMultiple*` (même mécanisme de filtrage que
    `Equipement`/`Isolement`, voir Lot J).
  - Le sous-formulaire d'ajout de `PointColumnDefinition` (`#add-point-column-definition-button`)
    est **masqué**, pas seulement désactivé — cohérent avec l'invariant Domain T2 (aucune colonne
    Point possible pour ce `PivotSource`), évite à l'utilisateur de construire une combinaison que
    le Domain rejetterait de toute façon.
  - Le champ `SheetName` reste affiché mais son label/aide contextuelle précise son nouveau rôle
    ("libellé interne, le fichier généré nomme chaque feuille d'après le code de type de tâche
    multiple rencontré") — nouvelle clé resx dédiée EN/FR.

**Tests** (bUnit) :
- Sélection de `TacheMultiple` dans le select `PivotSource` → les options du select `Source`
  changent pour ne proposer que les champs `TacheMultiple*` (même principe de test que J2 pour
  `Equipement`/`Isolement`).
- Sélection de `TacheMultiple` → le bouton/sous-formulaire `PointColumnDefinition` disparaît du
  DOM (`FindAll` vide, pas un test de style caché).
- Retour à `Equipement`/`Isolement` après avoir sélectionné `TacheMultiple` → le sous-formulaire
  `PointColumnDefinition` réapparaît (pas de perte définitive de fonctionnalité).
- Ajout complet d'une règle `PivotSource = TacheMultiple` avec colonnes → sauvegarde bout-en-bout,
  navigation vers `/export-profiles` (miroir du test J2 existant pour les autres `PivotSource`).
- Tentative d'ajout d'une `PointColumnDefinition` alors que `PivotSource = TacheMultiple` est
  sélectionné (si l'UI la masque, ce test peut devenir un test de garde-fou Domain direct plutôt
  que bUnit — à ajuster selon ce que révèle l'implémentation réelle du masquage).

---

## T7. Blazor — `ExportProfileTest.razor` : aperçu des feuilles Tâches Multiples

**Comportement attendu** : l'aperçu tabulaire par feuille générée (J3, "une table HTML par
feuille") couvre désormais aussi les feuilles Tâches Multiples générées dynamiquement — une table
HTML par feuille physique réellement produite (donc potentiellement plusieurs tables Tâches
Multiples selon le nombre de codes rencontrés dans le fichier source testé), pas une table statique
figée par règle de profil.

**Tests** (bUnit) :
- Upload d'une fixture réelle contenant des tâches multiples de plusieurs codes + profil avec la
  règle `PivotSource = TacheMultiple` → autant de tables HTML rendues que de codes distincts
  détectés, avec les bonnes valeurs de colonnes.
- Fixture sans tâches multiples (si un tel cas existe/est simulable) → aucune table Tâches
  Multiples rendue, seules `Parents`/`Enfants` apparaissent (non-régression).

---

## Note d'efficacité d'implémentation

Ordre recommandé pour limiter les allers-retours : **T1 → T2 → T3 → T4 → T5 → T6 → T7**. T1-T4
(Domain/Application) sont indépendants de toute UI et peuvent être validés intégralement par tests
unitaires/intégration avant de toucher à Blazor (T6/T7), qui ne fait que refléter un modèle déjà
stable. Avant de commencer T5, vérifier concrètement (via les 3 fichiers fixtures réels ou les
tests d'intégration existants du pipeline d'extraction) quelles fixtures produisent effectivement
des `TacheMultiplePivot` et avec quels codes — évite d'écrire un test T5 contre une fixture qui ne
contient en réalité aucune tâche multiple.
