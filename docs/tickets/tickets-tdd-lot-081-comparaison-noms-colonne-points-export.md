# Tickets TDD — Lot 081 : cocher une colonne de point de la même façon sur toutes les feuilles

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suite directe du
lot 079 (`tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md`, §1 « Écart »), qui a
**signalé** sans la corriger une incohérence du moteur de génération : la même paire « nom de Colonne
du profil d'import / `ColonneNom` du profil d'export » peut cocher la feuille « Parents » sans cocher
la feuille « Enfants ».*

**Objet** : une seule règle de comparaison des noms de Colonne et d'application, partout où le moteur
coche une colonne, et la règle d'unicité du profil d'export alignée dessus.

**À réaliser après le lot 080** si les deux sont menés à la suite : ils modifient les mêmes fichiers
(`SheetGenerationRule.cs`, `ExportProfileDescriptionBuilder.cs`, ticket 079, `CLAUDE.md`).

**Hors périmètre explicite de ce lot** :
- Les repères (`ParentRepere`, `RepereParent`, `Repere`) : produits par le pipeline, jamais saisis,
  toujours comparés à l'identique (D3).
- Le profil d'import : aucun nettoyage ni contrôle d'unicité des noms de Colonne saisis
  (`UnconditionalColonneNames`, `ConditionalPointRule.ColonneName`, `FieldPresencePointRule.ColonneName`).
- L'unicité des titres de colonne (`ValidateNoDuplicateHeaders`) : ils sont écrits dans le fichier,
  pas comparés.
- La comparaison des conditions de points à l'import (`ConditionalPointRuleEvaluator`, déjà tolérante).
- Les noms de feuilles (lot 080).

---

## Constats (relus dans le code au commit `1ea79d3`, non supposés)

### 1. Trois comparaisons différentes dans `SheetGenerationEngine.cs`

| Méthode | Lignes | Comparaison | Utilisée pour |
| :--- | :--- | :--- | :--- |
| `HasPoint` | L142-143 | `ParentRepere` et `ColonneNom` à l'identique (`==`) | feuille Isolement (L137) ; voie directe de la feuille Equipement (L156) |
| `HasPointForEquipement`, voie agrégée | L161-169 | `RepereParent` à l'identique ; `ColonneNom` après `Trim()`, `OrdinalIgnoreCase` | feuille Equipement (L123), lot 066 |
| `HasApplication` | L175-176 | `Trim()` + `OrdinalIgnoreCase` | feuilles Equipement (L121) et Isolement (L135) |

Le commentaire L145-153 présente l'exactitude de la voie directe comme un choix du lot 066 (non-régression),
pas comme une règle métier.

### 2. Les noms comparés sont des saisies d'administrateur, jamais nettoyées

- `PointPivot` (`Domain/Extraction/Pivot/PointPivot.cs` L12-28) ne vérifie que le non-vide et stocke
  `ColonneNom` tel quel.
- Création des points : `ProcedureExtractionService` L69 (`DefaultTableaux`, déjà rognés par
  `ImportProfile` L128/L150), `IsolementExtractionService` L131/L143, `AutresJointsTouchesExtractionService`
  L68/L76, `DiversExtractionService` L71, `UnconditionalIsolementSheetExtractionService` L65/L78.
  Les noms viennent du profil d'import sans nettoyage.
- `ConditionalPointRule` et `FieldPresencePointRule` ne rognent pas ; `SheetExtractionRule` ne contrôle
  aucun doublon de nom de Colonne.
- Côté export, `PointColumnDefinition` et `ApplicationColumnDefinition` stockent `ColonneNom`/
  `ApplicationNom` tels quels. Le commentaire d'`ApplicationColumnDefinition` (L9-10) annonce déjà une
  comparaison rognée et insensible à la casse.

### 3. L'unicité côté export est stricte

`SheetGenerationRule.ValidateNoDuplicateColonneNom` (L151-164) et `ValidateNoDuplicateApplicationNom`
(L166-180) groupent avec le comparateur par défaut, sans `Trim` ni casse. `Parents` peut donc déclarer
deux colonnes `ZÉRO ENERGIE` et `zéro energie ` : avec une comparaison tolérante, elles seraient cochées
ensemble. Le problème existe **déjà** pour les applications, dont la comparaison est tolérante.

### 4. Aucun test ne fige l'exactitude

`SheetGenerationEngineTests` : la tolérance est testée pour l'agrégation (L442) et les applications
(L355). **Aucun test n'affirme** que la feuille Isolement ou la voie directe est sensible à la casse ou
aux espaces. Tests de non-régression à garder verts sans changement : L70, L87, L373-480, L496.

### 5. Le profil semé correspond déjà caractère pour caractère

`DefaultProfileSeeder` construit les colonnes de points d'export avec les mêmes littéraux et constantes
que les règles d'import (L43-44, L385-402). `DefaultProfileSeederTests` L412-430 le vérifie en
comparaison exacte pour « Enfants », et L408 impose `Parents` = `Enfants`. Le changement ne doit donc
modifier **aucune cellule** générée avec le profil semé.

### 6. La page Détails décrit l'écart

- `ExportProfileDetails_PointIsolement` (`BlazorAdminMessages.resx` L1936) et
  `ExportProfileDetails_PointGroupIsolement` (L1942) contiennent « (nom identique, majuscules
  comprises) ». Les phrases Equipement (L1933, L1939) ne disent rien de la casse ni des espaces. Même
  texte dans `.fr.resx`.
- Utilisées par `ExportProfileDescriptionBuilder` L157 et L204.
- Tests : `ExportProfileDescriptionBuilderPointTests` (`Enfants_ApplicationThenGroupedPoints_WithTheExactNameNote`,
  L42 ; autres cas L30-85) ; `ExportProfileDescriptionBuilderSeededProfileTests` L41 et L66.
- Ticket 079 : §1 L63-72 (dont « Écart » L71-72), catalogue §5 L200 et L224, 79.5 L447-448, hors
  périmètre L572.

### 7. Fixtures disponibles

`tests/Fixtures` : C7401, D8570, G6306B, et 11 dossiers « Dossier de MaD IDL - … » (C8503, E3201B, E6423,
E6431A, E8201B, E8582, G4010A, LRS4504, PSA B, PSA E, RANGEE N°1). Génération avec le profil semé dans
`DefaultProfileSeederPipelineIntegrationTests` (L185-466), sur C7401, D8570 et G6306B seulement.

---

## Décisions à valider avant 81.1 (effort élevé)

| # | Question | Recommandation | Raison |
| :--- | :--- | :--- | :--- |
| D1 | Sens de l'alignement | **Tolérant partout** : `Trim` puis `OrdinalIgnoreCase`, pour les points (Isolement, voie directe et voie agrégée Equipement) et les applications. | Convention du pipeline (spec §7, lot 055), déjà appliquée à deux des trois comparaisons. Aligner sur le strict casserait l'agrégation du lot 066 et les applications. |
| D2 | Où vit la règle ? | Un `StringComparer` du domaine, `ColonneNameComparer.Instance` (`Domain/Generation/Profile/`) : `Equals` et `GetHashCode` sur la valeur rognée, insensibles à la casse. | Utilisable à la fois par le moteur (Application) et par la validation du profil (Domain), et directement dans un `GroupBy`. Un seul endroit. |
| D3 | Les repères | **Inchangés**, comparés à l'identique. | Produits par le pipeline, jamais saisis : aucune divergence de casse possible. Les normaliser masquerait un vrai défaut. |
| D4 | Unicité côté export | `ValidateNoDuplicateColonneNom` et `ValidateNoDuplicateApplicationNom` utilisent `ColonneNameComparer`. Codes et messages existants inchangés. | Deux colonnes égales pour le moteur seraient toujours cochées ensemble : c'est un doublon. |
| D5 | Profils déjà en base avec deux noms désormais en doublon | **Rien** : ils se chargent (constructeur EF, lot 080 constat 2), sont cochés ensemble, et l'éditeur refuse leur prochain enregistrement. | Cas improbable (aucun dans le profil semé), l'utilisateur est prévenu à la correction suivante. Pas de migration pour une base de dev jetable. |
| D6 | Phrases de la page Détails | Retirer « (nom identique, majuscules comprises) » des phrases Isolement, et ajouter aux quatre phrases (Equipement et Isolement, seule et groupée) : « (sans tenir compte des majuscules ni des espaces en début ou fin) ». | La page décrit le comportement réel (lots 078/079). Même règle partout, même mention partout. |
| D7 | Non-régression sur les fixtures | Sonde jetable : générer les 14 dossiers de `tests/Fixtures` avec les profils semés avant et après, comparer cellule par cellule, consigner le résultat ici, supprimer la sonde. | Seules 3 fixtures sont couvertes par les tests permanents. Le constat 5 prévoit zéro différence : le confirmer sur toutes. |

---

## 81.0. Relevé avant modification (pas de code de production)

Première moitié de D7 : générer les 14 dossiers avec les profils semés au commit de départ et garder le
résultat (hors dépôt, dossier temporaire de session). Noter les dossiers qui échouent à l'import pour
une raison sans rapport, pour ne pas les confondre avec une régression.

---

## 81.1. Comparateur des noms de Colonne

**Rouge** (`ColonneNameComparerTests`, Domain.Tests) :
- `ZÉRO ENERGIE` égal à `zéro energie`, à ` ZÉRO ENERGIE `, et même `GetHashCode` ;
- `ZÉRO ENERGIE` différent de `ZERO ENERGIE` (accent : aucune normalisation au-delà de la casse) ;
- `POINT DE FEU` différent de `POINT FEU` (même garde-fou que le lot 055) ;
- `null` égal à `null`, différent d'une chaîne.

**Vert** : `ColonneNameComparer : StringComparer`.

## 81.2. Moteur : points des feuilles Isolement et voie directe Equipement

**Rouge** (`SheetGenerationEngineTests`) :
- feuille Isolement : point `zéro energie ` sur l'élément, colonne `ZÉRO ENERGIE` → cochée ;
- feuille Equipement, voie directe : point `travaux complet` sur l'équipement, colonne `TRAVAUX COMPLET`
  → cochée ;
- **non-généralisation** : feuille Isolement, point porté par un autre élément (repère différent) →
  non cochée ; point `POINT DE FEU`, colonne `POINT FEU` → non cochée.

**Vert** : `HasPoint`, la voie agrégée et `HasApplication` passent par `ColonneNameComparer.Instance`.
Plus aucun `Trim()`/`OrdinalIgnoreCase` en ligne dans le moteur. Mettre à jour le commentaire L145-153.

**Contrôle** : vérifier que les deux nouveaux tests positifs ne sont pas vides de sens en remettant
`==` dans `HasPoint`.

## 81.3. Unicité des noms dans une règle d'export

**Rouge** (`SheetGenerationRuleTests`) :
- colonnes de points `ZÉRO ENERGIE` et `zéro energie ` → `DomainValidationException`,
  `SheetGenerationRule_DuplicateColonneNom` ;
- applications `PROGRESS` et `progress` → `SheetGenerationRule_DuplicateApplicationNom` ;
- **non-généralisation** : deux titres `Repère` et `repère` restent acceptés (D4 ne touche pas
  `ValidateNoDuplicateHeaders`).

**Vert** : `GroupBy(..., ColonneNameComparer.Instance)` dans les deux méthodes.

**Contrôle** : `grep` des tests (éditeur d'export, mapper, aller-retour) qui construiraient deux noms
ne différant que par la casse ou les espaces. Attendu : aucun. S'il y en a, s'arrêter et le signaler.

## 81.4. Page Détails

**Rouge** :
- `ExportProfileDescriptionBuilderPointTests.Enfants_ApplicationThenGroupedPoints_WithTheExactNameNote`
  devient `..._WithTheToleranceNote` (intention changée, corrigé sur place et signalé dans le commit) ;
- nouveau cas : la phrase Equipement mentionne aussi la tolérance ;
- `ExportProfileDescriptionBuilderSeededProfileTests` L41 et L66 : phrases attendues mises à jour.

**Vert** : les 4 clés de D6, même texte dans `.resx` et `.fr.resx` (décision D7 du lot 079).

**Document** : catalogue §5 du ticket 079 (L200, L224) remis égal à la sortie réelle ; §1 L63-72, 79.5
L447-448 et hors périmètre L572 mis à jour en place, avec renvoi à ce lot.

## 81.5. Non-régression et documents vivants

- Seconde moitié de D7 : générer à nouveau les 14 dossiers, comparer au relevé 81.0, consigner ici le
  nombre de cellules différentes (attendu : 0).
- `CLAUDE.md` : puce du lot 079 (« Second finding, not fixed ») mise à jour, nouvelle puce lot 081.

---

## Portée des tests

- Itérations : `--filter` sur la classe travaillée.
- Clôture : Domain.Tests, Application.Tests, Infrastructure.Tests, puis `ExcelETL.BlazorAdmin.Tests`
  filtré sur `Formatting` et `ExportProfile`.
- Doivent rester verts **sans changement d'assertion** : `SheetGenerationEngineTests` existants,
  `DefaultProfileSeederTests`, `DefaultProfileSeederPipelineIntegrationTests`,
  `GenerationPipelineIntegrationTests`, tests aller-retour des lots 073-076.
- Seules assertions modifiées : celles de 81.4, listées dans le commit.
- Pas de navigateur : Simon vérifie.
