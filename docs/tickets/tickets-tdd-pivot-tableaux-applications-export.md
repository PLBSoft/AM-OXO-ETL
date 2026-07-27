# Tickets TDD — Lot U : Tableaux/Applications dans le pivot, enrichissement du profil d'export par défaut

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à un
échange avec le client sur la structure du fichier Excel cible : retire le hardcode
`"TRAVAUX COMPLET"`/`"TRAVAUX DETAIL"` de `ProcedureExtractionService` pour le faire porter par le
profil d'import, introduit la notion d'Applications (liaison many-to-many `BaseElement`↔
`Application` côté legacy EF6 `AMProgress`), et met à jour le profil d'export seedé par défaut
(`tickets-tdd-seed-profils-defaut.md`) avec plusieurs nouvelles colonnes.*

**Vérificateur de lettre de lot** : à confirmer avant implémentation que "Lot U" n'entre pas en
collision avec un lot déjà attribué entre-temps (dernier lot connu au moment de la rédaction :
T — feuilles Tâches Multiples).

**Ne fait pas partie de ce lot** (parqué, à reprendre une fois U livré — voir échange du 23/07) :
- Colonne **ETIQUETTE** (Enfants) = "Isolement TexteLibre". Point non tranché : ce champ ne
  semble aujourd'hui extrait sur aucune feuille ISOLEMENT (seul un champ "Texte libre" existe en
  `H18:N18` côté PLATINES, feuille distincte). Nécessite une clarification du besoin réel avant
  de pouvoir écrire ce ticket — probablement une extension du pipeline d'extraction, pas
  seulement du profil d'export.

---

## Contexte et décisions actées avec le client (23/07)

1. **Tableaux** — l'association `BaseElement`↔`Tableau` ("TRAVAUX COMPLET"/"TRAVAUX DETAIL" pour
   l'Équipement parent, aujourd'hui hardcodée dans `ProcedureExtractionService`) devient un **vrai
   champ du pivot**, alimenté par le profil d'import plutôt que codé en dur. Décision explicite :
   ce n'est pas une feature spéculative, c'est un besoin réel identifié en cours de projet (le
   client pourrait vouloir un jour lire cette information depuis son fichier source via un profil
   configuré différemment).
2. **Applications** — connaissance métier nouvelle : côté legacy EF6 `AMProgress`, il existe une
   liaison many-to-many entre `BaseElement` et `Application`. Décision actée : **on reste simple**
   — pas de nouvelle entité `Application` dans notre modèle EF Core, juste une liste de noms
   (`string`) portée par le profil d'import, configurée par l'utilisateur qui connaît les noms
   attendus côté `AMProgress`. Aucune autre donnée à porter pour une Application dans notre
   contexte (juste son nom).
3. **Diffusion (broadcast)** — Tableaux et Applications suivent tous les deux la **même règle de
   diffusion que `loc1`** : une seule liste par profil, appliquée à l'Équipement parent **et** à
   tous les Isolements enfants du run. Pas de liste différente par Isolement.
4. **Nom de l'Application à seeder par défaut** : littéralement `"PROGRESS"` (remplace l'ancienne
   idée de colonne `PROGRESS` à valeur constante — c'est en réalité une colonne d'Application au
   sens du point 2).
5. **Type Elément (Enfants)** ne nécessite aucune extension du pivot :
   `IsolementPivot.TypeElementNom` existe déjà.
6. **ELEMENT PARENT (Enfants)** = `EquipementPivot.Repere` (confirmé) — **décision d'implémentation
   actée le 23/07** : nouveau champ `IsolementPivot.RepereParent`, diffusé (broadcast) au même
   titre que `loc1`/Tableaux/Applications, plutôt qu'une lecture parallèle d'`ImportResult.Equipement`
   pendant la génération de la feuille Enfants. Cohérent avec le fait que `SheetGenerationEngine`
   ne lit aujourd'hui que les pivots eux-mêmes, jamais `ImportResult.Equipement` en parallèle.

---

## U1. Domain — `ImportProfile.DefaultTableaux` / `DefaultApplicationNames`

**Comportement attendu** :
- Deux nouveaux champs sur `ImportProfile` : `IReadOnlyList<string> DefaultTableaux` et
  `IReadOnlyList<string> DefaultApplicationNames`, au même niveau que `RepereePrefix`/
  `EquipementTypeElementNom`.
- Requis non-null (peuvent être vides), même style de validation que
  `UnconditionalColonneNames` (pas de contrainte de non-vacuité — un profil pourrait
  légitimement n'avoir ni Tableau ni Application à propager).
- Pas de valeur par défaut "magique" dans le constructeur Domain (cohérent avec
  `EquipementTypeElementNom` : la valeur vient uniquement du profil actif, jamais d'une constante
  de code) — le seeder (U6) est le seul endroit où `["TRAVAUX COMPLET", "TRAVAUX DETAIL"]` et
  `["PROGRESS"]` apparaissent en dur.

**Tests** (Domain) :
- Construction d'un `ImportProfile` avec des listes non vides pour les deux champs → accessibles
  telles quelles.
- Listes vides acceptées sans exception (cas nominal, pas un cas d'erreur).
- Non-régression : les tests de construction `ImportProfile` déjà existants (sans ces deux
  champs) continuent de passer si un constructeur de compatibilité est conservé, ou sont mis à
  jour si le constructeur principal change de signature — à trancher selon l'implémentation
  réelle du constructeur actuel (voir garde-fou anti-breaking-change de l'équipe).

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/`.

---

## U2. Domain — extension du modèle pivot (`Tableaux`, `Applications`)

**Comportement attendu** :
```csharp
public sealed record EquipementPivot(
    string Repere, string Designation, string TypeElementNom,
    IReadOnlyList<string> Tableaux, IReadOnlyList<string> Applications);

public sealed record IsolementPivot(
    string Repere, string Designation, string TypeElementNom, string PositionALaPose,
    /* ... champs existants ... */ string Localisation,
    IReadOnlyList<string> Tableaux, IReadOnlyList<string> Applications,
    string RepereParent);
```
- Pas de nouveau record de jonction (pas d'`ApplicationPivot`/`ParentRepere` façon `PointPivot`)
  pour Tableaux/Applications : diffusés de façon identique à `loc1`, donc portés directement
  comme liste sur chaque pivot plutôt que reconstruits par jointure — plus simple, cohérent avec
  le mécanisme de broadcast déjà en place.
- `RepereParent` suit le même principe de diffusion directe (pas de jointure a posteriori) :
  c'est une simple `string`, valeur = `EquipementPivot.Repere` du run, copiée sur chaque
  `IsolementPivot` au moment du broadcast (U3) — pas une référence/navigation vers l'objet
  `EquipementPivot` lui-même (cohérent avec le style "records immuables, pas de graphe d'objets"
  déjà en place pour le pivot).

**Tests** (Domain) :
- Construction de `EquipementPivot`/`IsolementPivot` avec les trois nouveaux champs
  (`Tableaux`, `Applications`, `RepereParent`).
- Égalité structurelle (records) inchangée sur les autres champs.

**Dossier** : `src/ExcelETL.Domain/Extraction/Pivot/`.

---

## U3. Application/Infrastructure — retrait du hardcode PROCEDURE, application du broadcast

**Comportement attendu** :
- `ProcedureExtractionService` ne construit plus `EquipementPivot.Tableaux` à partir des deux
  `private const string` actuelles — la valeur vient de `profile.DefaultTableaux`, jamais d'une
  constante du service (même garde-fou anti-hardcoding que pour `EquipementTypeElementNom`).
- `ImportPipelineOrchestrator` applique `profile.DefaultTableaux` et
  `profile.DefaultApplicationNames` à l'`EquipementPivot` **et** à tous les `IsolementPivot` du
  run, au même endroit et selon le même principe que le broadcast `loc1` déjà en place (D1,
  étape 3).
- Le même orchestrateur diffuse également `EquipementPivot.Repere` vers
  `IsolementPivot.RepereParent` sur tous les `IsolementPivot` du run — même étape, même principe
  (un `EquipementPivot` par run, valeur constante pour tous les Isolements).

**Tests** :
- Test explicite avec deux profils portant des `DefaultTableaux`/`DefaultApplicationNames`
  différents → le service/l'orchestrateur restitue bien les valeurs du profil, pas une constante
  (même garde-fou explicite que celui déjà écrit pour `EquipementTypeElementNom`, voir C1).
- **Non-régression contre les 3 fixtures réelles** (C7401, D8570, G6306B) : en configurant le
  profil d'import avec `DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"]`, les 3 fichiers
  produisent exactement ces deux valeurs sur l'Équipement et sur tous les Isolements extraits —
  comportement observable identique à avant ce lot.
- Profil avec `DefaultApplicationNames = ["PROGRESS"]` → même valeur présente sur
  `EquipementPivot.Applications` et sur chaque `IsolementPivot.Applications` du run.
- Profil avec listes vides → `Tableaux`/`Applications` vides sur tous les pivots produits (pas
  d'exception, pas de valeur par défaut injectée silencieusement).
- `IsolementPivot.RepereParent` = `EquipementPivot.Repere` sur chaque Isolement extrait, vérifié
  sur les 3 fixtures réelles (même valeur que celle qui compose déjà `IsolementPivot.Repere` en
  préfixe, `{RepereParent}-{Identification}` — test de cohérence entre les deux champs).

---

## U4. Domain — `ApplicationColumnDefinition` + invariants `SheetGenerationRule`

**Comportement attendu** :
- Nouveau type `ApplicationColumnDefinition(string ApplicationNom, string Header, string MarkValue)`
  sur le modèle du `PointColumnDefinition` existant (`ColonneNom`/`Header`/`MarkValue`), mais
  distinct — une Application n'est pas un `Point`/une `Colonne` au sens legacy.
- `SheetGenerationRule` porte une nouvelle liste `IReadOnlyList<ApplicationColumnDefinition>`, au
  même niveau que `ColumnDefinition`/`PointColumnDefinition`.
- **Validation croisée symétrique à T2** : si `PivotSource = TacheMultiple`, rejet de toute
  `ApplicationColumnDefinition` ajoutée à la règle (une tâche multiple n'a pas d'Application
  associée) — même exception métier dédiée que pour `PointColumnDefinition`, ou extension de la
  même exception si elle couvre déjà plusieurs types de colonnes incompatibles.
- Pas de doublon d'`ApplicationNom` au sein d'une même règle (même style de garde-fou que pour
  `ColonneNom`/en-têtes dupliqués).

**Tests** (Domain) :
- Construction d'une `SheetGenerationRule` avec des `ApplicationColumnDefinition` valides,
  `PivotSource = Equipement` ou `Isolement`.
- `PivotSource = TacheMultiple` + ajout d'une `ApplicationColumnDefinition` → exception dédiée,
  règle non construite.
- Deux `ApplicationColumnDefinition` avec le même `ApplicationNom` dans la même règle → exception
  de validation (doublon).
- Non-régression : constructions existantes de `SheetGenerationRule` sans
  `ApplicationColumnDefinition` toujours valides.

---

## U5. Application — `SheetGenerationEngine` : rendu Tableaux (concaténation) et Applications (colonnes dynamiques)

**Comportement attendu** :
- **Colonne "Tableaux"** (une seule colonne, `ColumnDefinition` classique avec un nouveau
  `PivotFieldRef.EquipementTableaux`/`PivotFieldRef.IsolementTableaux`, ou équivalent) : rendu =
  `string.Join(", ", pivot.Tableaux)` — première fois que le moteur doit joindre une liste dans
  une cellule plutôt qu'afficher une valeur scalaire ou marquer une correspondance Point. Ordre de
  la liste = ordre de définition dans le profil (`DefaultTableaux`), pas de tri alphabétique
  additionnel.
- **Colonnes Applications** (une colonne par `ApplicationColumnDefinition` de la règle) : pour
  chaque ligne générée (Équipement ou Isolement selon `PivotSource`), cellule =
  `MarkValue` si `pivot.Applications` contient `ApplicationNom` (comparaison insensible à la
  casse et `.Trim()`, même recommandation transverse que pour les `TypeElement`/`Colonne.Nom` —
  voir spec §7), sinon cellule vide. Comportement structurellement identique à celui déjà en place
  pour les colonnes Points, mais sans passer par un `PointPivot`/`ParentRepere` puisque
  `Applications` est déjà porté directement par le pivot de la ligne.

**Tests** (Application) :
- `EquipementPivot.Tableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"]` + règle avec une colonne
  Tableaux → cellule générée = `"TRAVAUX COMPLET, TRAVAUX DETAIL"`.
- Liste vide → cellule vide (pas d'exception, pas de virgule orpheline).
- `IsolementPivot.Applications = ["PROGRESS"]` + `ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")`
  → cellule = `"O"`.
- `ApplicationColumnDefinition("AUTRE_APP", ...)` alors que le pivot ne contient que `"PROGRESS"`
  → cellule vide pour la colonne `"AUTRE_APP"`.
- Variante de casse/espace (`"progress "` côté pivot vs `"PROGRESS"` côté profil) → correspondance
  trouvée quand même (test dédié, même recommandation `.Trim()`+insensible à la casse que le
  reste du moteur).
- Non-régression complète des tests `SheetGenerationEngine` existants (Points, colonnes
  descriptives, Tâches Multiples du Lot T) sans règle Tableaux/Applications dans le profil.

---

## U6. Infrastructure — mise à jour du profil d'export par défaut (`DefaultProfileSeeder`)

**Comportement attendu**, extension de M3 (`tickets-tdd-seed-profils-defaut.md`) :

**Profil d'import seedé** :
- `DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"]` — reproduit exactement le
  comportement hardcodé retiré en U3, non-régression garantie par les tests U3.
- `DefaultApplicationNames = ["PROGRESS"]`.

**Profil d'export seedé — feuille `"Parents"`** (ordre acté le 23/07) :
- ... colonnes descriptives existantes, dans leur ordre actuel, jusqu'à **"Désignation"** incluse ...
- Nouvelle colonne **"Tableaux"** (`Source = EquipementTableaux`, rendu joint par virgule — U5),
  positionnée **juste après "Désignation"**, donc avant les colonnes Points existantes.
- Nouvelle colonne **"PROGRESS"** (`ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")`),
  juste après "Tableaux" (donc également avant les colonnes Points).
- ... colonnes Points existantes, inchangées, à la suite ...

**Profil d'export seedé — feuille `"Enfants"`** (ordre acté le 23/07) :
- **"Numéro"** (existante, 1re colonne).
- Nouvelle colonne **"Type Elément"** (`Source = IsolementTypeElementNom`), 2e colonne, juste
  après "Numéro".
- ... colonnes descriptives existantes ("Zone", etc.) ...
- Nouvelle colonne **"ELEMENT PARENT"** (`Source = IsolementRepereParent`, nouvelle valeur
  `PivotFieldRef` lisant `IsolementPivot.RepereParent` — voir U2/U3), positionnée entre "Zone" et
  "Désignation".
- **"Désignation"** (existante) ... reste des colonnes descriptives existantes ...
- Nouvelle colonne **"Tableaux"** (`Source = IsolementTableaux`, même rendu joint que Parents) et
  nouvelle colonne **"PROGRESS"** (même `ApplicationColumnDefinition` que Parents, juste après
  "Tableaux") : positionnées **juste avant les colonnes Points** de la feuille Enfants — donc
  après toutes les colonnes descriptives (y compris "POSITION A LA POSE"), mais avant
  `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"`/etc.

**Tests** (Infrastructure, non-régression contre les 3 fixtures réelles) :
- `SheetGenerationEngine` + profil d'export seedé contre un `ImportResult` réel → en-têtes de
  colonnes générés correspondent exactement à l'ordre attendu ci-dessus (Parents et Enfants).
- Contenu généré pour les 3 fixtures : colonne "Tableaux" = `"TRAVAUX COMPLET, TRAVAUX DETAIL"`
  sur chaque ligne (Parents et Enfants) ; colonne "PROGRESS" = `"O"` sur chaque ligne ; colonne
  "ELEMENT PARENT" = repère de l'Équipement, identique sur toutes les lignes Enfants d'un même
  fichier ; colonne "Type Elément" (Enfants) = valeur déjà connue par isolement (mêmes valeurs que
  celles déjà vérifiées côté extraction, C2-C6).
- Cohérence croisée import/export (même principe que M3) : les `PivotFieldRef` référencés par le
  profil d'export seedé (`IsolementTypeElementNom`, colonnes Tableaux, `ApplicationColumnDefinition`
  `"PROGRESS"`) trouvent bien une contrepartie cohérente côté profil d'import seedé
  (`DefaultTableaux`/`DefaultApplicationNames` non vides).

---

## Hors périmètre explicite de ce lot

- Colonne **ETIQUETTE**/Isolement TexteLibre (parqué, voir en-tête de ce document).
- **Migration des profils déjà seedés sur un environnement existant** : comme `DefaultProfileSeeder`
  ne modifie jamais un profil déjà présent en base (principe acté au Lot M), un environnement où
  le profil par défaut a déjà été seedé avant ce lot **ne recevra pas automatiquement** ces
  nouvelles colonnes/valeurs. Si un environnement de ce type existe déjà, un ticket de migration
  idempotente sera nécessaire, sur le modèle de **T8** (`tickets-tdd-export-taches-multiples.md`)
  — non inclus ici tant qu'un tel environnement n'est pas confirmé exister.
- Toute modification de l'écran Blazor `ExportProfileEditor.razor` au-delà de l'ajout des champs
  de formulaire strictement nécessaires pour saisir `ApplicationColumnDefinition`
  (`#add-application-column-definition-button`, `#application-column-nom-input`,
  `#application-column-header-input`, `#application-column-mark-value-input` pré-rempli à `"O"`)
  et `DefaultTableaux`/`DefaultApplicationNames` côté `ImportProfileEditor.razor` — l'ergonomie
  fine (réordonnancement, groupes visuels) n'est pas demandée, à traiter séparément si besoin une
  fois la fonctionnalité livrée.