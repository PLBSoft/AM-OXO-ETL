# Tickets TDD — Lot 084 : moteur générique des feuilles d'éléments (ticket client J2M76)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 21/09 après
le ticket client J2M76 et le premier correctif du même jour (commit `b645159` : l'éditeur et la vue
Détails signalent une section sans effet sur la feuille). Investigation menée sans rien implémenter ;
décisions prises avec Simon le 21/09. Une première version de ce lot (garde-fous, sections masquées) a été
abandonnée le même jour au profit d'une remise à plat : un seul fonctionnement pour les 5 feuilles
d'éléments.*

**Ticket client J2M76 (résumé)** : sur PLATINES, le client a perdu ses 4 règles « Colonnes cochées si une
cellule est renseignée ». Il a voulu les refaire en copiant le modèle d'ISOLEMENT : 2 champs du bloc
`HasDebMad` (H19:N19) et `HasDebRel` (H20:N20), puis 2 règles conditionnelles `HasDebMad Equals DEBUT MAD`
et `HasDebRel Equals DEBUT REL`. Il signale une erreur à l'import.

---

## 1. Constat (vérifié dans le code et par une sonde jetable sur les 14 fixtures, supprimée ensuite)

### 1.1 Qui lit quoi

| Feuille | Service | Lecture des blocs | Règles de point lues | Champs visibles par une `ConditionalPointRule` |
| :--- | :--- | :--- | :--- | :--- |
| PROCEDURE | `ProcedureExtractionService` | parcours propre, tolérant | inconditionnelles + `PointRules` (au moins une vraie tâche, lot 083) | les 6 champs fixes d'une tâche (`ProcedureFieldNames`) |
| ISOLEMENT | `IsolementExtractionService` | parcours propre ; `HasZeroEnergie` optionnel (`FindOptionalField`) | inconditionnelles + `PointRules` | `TypeElement`, `HasZeroEnergie` (`"true"`/`"false"` calculé à partir de la cellule et de `ZeroEnergieExpectedValue`) |
| AUTRES JOINTS TOUCHES | `AutresJointsTouchesExtractionService` | `RepeatingBlockReader` | inconditionnelles + `PointRules` | `TypeElement` |
| DIVERS | `DiversExtractionService` | `RepeatingBlockReader` | inconditionnelles + `PointRules` | `TypeElement` |
| PLATINES, ORIFICES CAPACITES | `UnconditionalIsolementSheetExtractionService` | `RepeatingBlockReader` | inconditionnelles + `FieldPresencePointRules` (`PointRules` jamais lu) | aucun |

**Une `ConditionalPointRule` ne lit pas un champ quelconque du bloc.** Chaque service construit à la main
le dictionnaire passé à `ConditionalPointRuleEvaluator` ; un `SourceFieldName` absent de ce dictionnaire
lève `UnknownFieldReferenceException`. L'import entier échoue (500 sur `POST /api/oxo/process`, erreur
technique sur les pages de test ; la clé `UnknownFieldReference` n'a toujours pas d'entrée dans
`ApplicationMessages.resx`, cf. lot 065). La sonde l'a reproduit sur AUTRES JOINTS TOUCHES (D8570) avec un
champ `Extra` ajouté au bloc et référencé par une règle.

Le « modèle ISOLEMENT » copié par le client n'est donc pas un mécanisme général : `HasZeroEnergie` est un
nom codé en dur, lu et converti par le service (lot 063).

### 1.2 Différences de comportement entre les mécanismes

| | `ConditionalPointRule` | `FieldPresencePointRule` | Zéro énergie (ISOLEMENT) |
| :--- | :--- | :--- | :--- |
| Source | valeur déjà extraite (liste fixe par feuille) | cellule quelconque, relative au bloc | cellule V, nom `HasZeroEnergie` fixe |
| Présence seule (sans valeur) | non (`ComparisonValue` obligatoire) | oui (`ExpectedValue` nulle) | non |
| `NotEquals` | oui | non | non |
| OU pour une même colonne | oui (`Any` sur le groupe) | oui (plusieurs règles, point dédoublonné par (colonne, bloc)) | — |
| Comparaison | rognée, casse ignorée | rognée, casse ignorée | rognée, casse ignorée |
| Avertissement si rien ne correspond | `NoConditionalPointCreated`, agrégé par élément (sauf PROCEDURE, lot 083) | aucun (voulu, 16/09) | seulement pour une valeur inattendue |

Dans le code actuel, les deux mécanismes répondent à deux questions distinctes : « la valeur déjà extraite
vaut-elle X ? » et « que contient telle cellule du bloc ? ». Mais dès que n'importe quel champ du bloc peut
servir de condition (§3, G2), la seconde devient un cas particulier de la première : un champ facultatif
et l'opérateur « renseigné » ou « égal à ». D'où une seule sorte de règle.

### 1.3 Le cas J2M76 sur les fixtures réelles

| | PLATINES extraites (14 fixtures) | Points DEBUT MAD / DEBUT REL |
| :--- | :--- | :--- |
| Profil standard | 74 | 16 / 5 |
| Profil du client | 19 (55 blocs rejetés, `RequiredFieldMissing`) | 0 / 0 |

Détail : C7401 15 → 2, D8570 21 → 0, G6306B 5 → 0, G4010A 4 → 0, RANGEE N°1 9 → 2, E6423 3 → 0, les deux
PSA 1 → 0. Seuls les blocs dont les deux cellules H sont remplies survivent (C8503, E3201B, E6431A,
E8582). **C'est l'erreur signalée** : `RepeatingBlockReader` exige tout champ déclaré dans le bloc, et les
cellules H+2/H+3 sont vides dans la plupart des blocs. Les éléments disparaissent de la feuille Enfants.

Même appliquée, la règle du client serait fausse : C8503 porte `DEBUT REL` en +2 et `DEBUT MAD` en +3 ;
E6431A (TP1-4) et E8582 portent `DEBUT MAD` en +3. Associer « POSÉE LE » à MAD est la lecture écartée le
16/09 ; il faut le OU entre les deux cellules, ce que font les 4 règles du profil standard.

### 1.4 Ce que le correctif `b645159` ne couvre pas

- Un **champ du bloc que le service ne lit pas** détruit des blocs sur les 4 feuilles lues par
  `RepeatingBlockReader` (il est sans effet sur PROCEDURE et ISOLEMENT). Aucun avertissement.
- Un **`SourceFieldName` hors de la liste de la feuille** fait échouer l'import. Aucun avertissement.

### 1.5 Pourquoi les feuilles ne lisent pas toutes les mêmes réglages

Ce n'est pas un choix métier : chaque feuille a son propre service, écrit au lot C d'après la spec de
l'époque, qui ne lit que ce dont la feuille avait besoin ce jour-là. Les différences entre les 4 services
d'éléments sont des accidents d'historique, pas des règles métier :

| Différence actuelle, codée en dur | Service(s) |
| :--- | :--- |
| Repère de l'équipement lu en `K6:T6` / `K6:U6` / `N6` (champ d'en-tête seulement sur AJT/DIVERS, lot 047) | tous |
| Désignation facultative sur ISOLEMENT, obligatoire ailleurs | ISOLEMENT |
| Position à la pose lue sur ISOLEMENT seulement | ISOLEMENT |
| Zone lue en `B6:E6` sur DIVERS seulement | DIVERS |
| Zéro énergie : champ `HasZeroEnergie`, `ZeroEnergieExpectedValue`, avertissement dédié | ISOLEMENT |
| Règles conditionnelles lues ou non selon la feuille ; champs utilisables codés en dur | tous |
| Règles « cellule renseignée » sur PLATINES/ORIFICES seulement | PLATINES, ORIFICES |
| Champ d'arrêt configuré ignoré | ISOLEMENT |

Conséquence : l'éditeur propose partout les mêmes sections, alors que chaque feuille n'en lit qu'une
partie. Il a fallu une table recopiée côté BlazorAdmin (`ImportSheetUsage`), gardée par des tests contre le
vrai pipeline, puis des avertissements (`b645159`), pour que l'utilisateur comprenne ce qui marche où. Le
ticket J2M76 montre que ça ne suffit pas : le client a fait le geste le plus naturel (déclarer un champ du
bloc, puis s'en servir dans une condition) et c'est le code qui ne suivait pas.

**Hors de cette remise à plat : PROCEDURE.** Ce sont des tâches, pas des éléments, avec un en-tête qui peut
rejeter tout le fichier et des points posés sur l'équipement (lots 082-083) : une vraie différence de
nature.

## 2. Options comparées

| | Principe | Verdict |
| :--- | :--- | :--- |
| A | PLATINES/ORIFICES lisent aussi les `PointRules`, champs du bloc optionnels | écartée : deux mécanismes qui se recouvrent sur la même feuille |
| B1 | règles « sur cellule » acceptées sur toutes les feuilles d'éléments | écartée : garde deux façons de faire la même chose |
| C / C+ | avertissements, puis garde-fous et sections masquées (première version de ce lot, 21/09) | écartée : rend les cas particuliers visibles sans les supprimer |
| **Moteur générique** | un seul service pour les 5 feuilles d'éléments, une seule sorte de règle de point, tout réglage lu sur toutes ces feuilles | **retenue** |

Le moteur générique remplace environ 500 lignes réparties dans 4 services par un seul service, et supprime
la table `ImportSheetUsage` (pour les feuilles d'éléments), `SheetRuleIgnoredSettings` (pour l'essentiel)
et les avertissements de `b645159`. Il est faisable maintenant parce qu'une référence fiable existe :
14 fichiers clients réels, et la méthode du lot 081 (sortie complète avant/après, différence vide exigée).

## 3. Décisions (Simon, 21/09)

| # | Sujet | Décision |
| :- | :--- | :--- |
| G1 | Direction | Un seul service pour ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS. PROCEDURE reste à part. Le lot remplace la première version « garde-fous » de ce document. |
| G2 | Règles de point | Une seule sorte : un champ du bloc + un opérateur (`Equals`, `NotEquals`, nouveau `IsNotBlank`) + une valeur (absente pour `IsNotBlank`). La « cellule renseignée » devient un champ du bloc facultatif ; `FieldPresencePointRule` disparaît. |
| G3 | Avertissement « aucune colonne conditionnelle cochée » | Réglage par règle de feuille (case). Semé : oui sur ISOLEMENT, AJT, DIVERS (comportement actuel) ; non sur PLATINES, ORIFICES. |
| G4 | Champs du bloc obligatoires | Case « obligatoire » par champ. Semée pour reproduire exactement le comportement actuel. |
| G5 | Zéro énergie (annule la règle dédiée du lot 063) | Devient une règle ordinaire : champ du bloc facultatif `ZeroEnergie` (colonne V), règle `ZeroEnergie Equals ZERO ENERGIE` → PS941. `HasZeroEnergie` et `ZeroEnergieExpectedValue` disparaissent. |
| G6 | Repère de l'équipement | Champ d'en-tête `repereEcho` sur les 5 feuilles (semé `K6:T6`, `K6:U6`, `K6:U6`, `N6`, `N6`). |
| G7 | Avertissement sur PLATINES quand aucune règle n'est satisfaite | Non : case de G3 décochée. |
| G8 | Profils existants | Pas de migration de données (D7 du lot 082) : réinitialisation, voir §5. |
| G9 | Arbre `TextTransform` | Supprimé (`TextTransform`, `RawValue`, `SubstringAfter`, `Concat`, `ConcatPart`, `FieldRef`, `Literal`, `ITextTransformEvaluator`/`TextTransformEvaluator`). Le repère devient une concaténation ; le retrait du préfixe repère dans `HeaderRuleResolver`, une opération de chaîne (mêmes messages d'erreur). |
| G10 | Couleur d'étiquette | La cellule dédiée (`CouleurEtiquetteCell`) devient un champ du bloc facultatif au nom connu `CouleurEtiquette`, comme la position à la pose. `DefaultCouleurEtiquette` et `AllowedCouleursEtiquette` restent. |
| G11 | Avertissements et orchestrateur | Un seul suivi d'avertissements dédoublonnés (paramétré par code et message) remplace les classes par avertissement. L'orchestrateur boucle sur les 5 feuilles d'éléments avec un seul type de résultat. |
| G12 | Champ d'arrêt | Réglage supprimé : les éléments s'arrêtent toujours sur `Identification`, PROCEDURE sur `Action` (déjà le cas). |
| G13 | Nom de feuille unique | Le nom de feuille n'est plus répété : `RepeatingBlockLocator.Sheet` supprimé, et un champ d'en-tête ne porte plus qu'une plage (`DirectCell` supprimé). Seul `SheetExtractionRule.SheetName` reste. |

| G14 | Case « obligatoire » d'un champ ajouté dans l'éditeur | Décochée par défaut : le cas J2M76 fonctionne tel que le client l'a saisi. Côté domaine et base, la valeur par défaut reste « obligatoire » (profils déjà enregistrés inchangés jusqu'à la réinitialisation). |
| G15 | Avertissement « valeur zéro énergie inattendue » (lot 063) | Supprimé : une autre valeur en colonne V ne coche simplement pas PS941, comme un `FIN MAD` sur PLATINES. Aucune fixture ne le déclenche. |
| G16 | Zone (loc1) | Champ d'en-tête au nom connu `zone`, semé sur DIVERS en `B6:E6` ; l'orchestrateur prend celui de DIVERS, comme aujourd'hui. |

Autres choix pris pendant la rédaction (sans enjeu métier fort) :
- **Le repère d'un élément** reste `{repereEcho}-{Identification}`, et le pivot est rempli à partir de noms
  de champs connus (`Identification`, `Designation`, `TypeElement`, `PositionALaPose`, `IsolementFieldNames`).
  `Identification` et `TypeElement` doivent être déclarés dans le bloc ; sinon erreur de paramétrage.
- **Validation dans le domaine** : `SheetExtractionRule` refuse une règle de point dont le champ source
  n'est pas un champ de son bloc (même principe que les champs composés du lot 047). Un profil enregistré
  avant le lot peut encore contenir un nom inconnu : l'import répond alors **422** avec un message localisé
  au lieu d'un 500.
- **L'éditeur garde deux formes** : PROCEDURE (tâches) et feuilles d'éléments. Toutes les sections sont
  affichées pour une feuille d'éléments, puisque toutes sont lues.

## 4. Sous-tickets

Ordre imposé : 84.0 d'abord (filet de sécurité), puis ajout du nouveau (84.1 → 84.5), bascule (84.6),
suppression de l'ancien (84.7 → 84.8), simplifications du modèle (84.9 → 84.10, faites après la bascule
pour ne toucher qu'un seul service), puis API et documentation. Chaque commit reste vert et 84.0 reste
vert sans régénération. Tests ciblés par `--filter` pendant l'itération ; suite complète à la fin de 84.8,
84.10 et du lot (changements transversaux).

### 84.0 — Filet de sécurité : sortie complète des 14 fixtures figée

- Nouveau test d'intégration (Infrastructure.Tests) : pour chaque fixture de `tests/Fixtures`, profils
  standard semés puis relus par les stores, écrit une représentation texte stable de l'`ImportResult`
  (équipement, éléments dans l'ordre, points, tâches, erreurs) et du classeur généré (toutes les cellules
  de toutes les feuilles), et la compare à un fichier de référence commité
  (`tests/ExcelETL.Infrastructure.Tests/Snapshots/{fixture}.txt`). Régénération explicite par variable
  d'environnement, jamais automatique.
- Erreurs : comparées sur code, feuille et valeur extraite, **triées** (l'ordre change forcément :
  ISOLEMENT mêle aujourd'hui erreurs et avertissements bloc par bloc, `RepeatingBlockReader` rend d'abord
  toutes les erreurs de lecture). L'identifiant de bloc et le texte du message (qui diffèrent aujourd'hui
  entre ISOLEMENT et `RepeatingBlockReader`) sont dans une section séparée du fichier, dont chaque
  changement devra être relu et justifié dans le commit.
- Éléments et points : comparés **dans l'ordre** (ils alimentent l'ordre des lignes du classeur généré).
- Ce test est vert sur le code actuel et **doit rester vert sans régénération** jusqu'à la fin du lot,
  sauf pour les écarts listés au §6.
- Points DEBUT MAD/REL attendus (relevé du 21/09, à retrouver dans les références) :

  | Fixture | Blocs cochés DEBUT MAD | Blocs cochés DEBUT REL |
  | :--- | :--- | :--- |
  | C8503 PORTE FILTRE | TP3, TP6, TP9 | TP3, TP6, TP9 |
  | E3201B condensats | TP7, TP8 | — |
  | E6431A Dépose cellule | P1-P4, TP1-TP4 | — |
  | E8582 | TP1 | — |
  | RANGEE N°1 | 1er bloc PT5 | 2e bloc PT5 |
  | C7401 | PT15A | PT15B |
  | les 8 autres | — | — |

  Totaux : 74 PLATINES, 16 DEBUT MAD, 5 DEBUT REL.

### 84.1 — Domaine : les nouveaux réglages (sans rien retirer)

- `ConditionOperator.IsNotBlank` ; `ConditionalPointRule.ComparisonValue` devient nullable : obligatoire
  (non blanc) pour `Equals`/`NotEquals`, interdit pour `IsNotBlank`. Nouveaux `DomainErrorCode` + `.resx`.
- `BlockFieldDefinition.IsRequired` (paramètre optionnel, défaut `true`).
- `SheetExtractionRule.WarnWhenNoConditionalPoint` (paramètre optionnel, défaut `false`).
- `SheetExtractionRule` refuse une `ConditionalPointRule` dont `SourceFieldName` n'est pas un nom de champ
  de son bloc (`SheetExtractionRule_PointRuleReferencesUnknownBlockField`). Vérifier que les règles de
  PROCEDURE du profil standard passent (elles portent sur `TypeTacheMultipleAlias`, champ du bloc).
- Tests Domain ; tests de localisation réelle des nouveaux codes (EN/FR différents).

### 84.2 — Infrastructure : persistance des nouveaux réglages

- Mapping EF (`IsRequired`, `WarnWhenNoConditionalPoint`, `ComparisonValue` nullable ; `Operator` est déjà
  stocké en texte, `IsNotBlank` ne demande rien). Migration générée par `dotnet ef migrations add`.
- `EfImportProfileStoreTests` : aller-retour des trois réglages, dont `ComparisonValue` nul relu nul.

### 84.3 — Application : `RepeatingBlockReader` respecte « obligatoire »

- **Rouge** (Moq) : champ facultatif vide → bloc conservé, valeur `""` dans le dictionnaire, aucune erreur ;
  champ obligatoire vide → `RequiredFieldMissing` (non-régression) ; le champ d'arrêt reste lu en premier.
- Les services actuels ne changent pas (tous les champs semés sont encore obligatoires à ce stade).

### 84.4 — Application : `ElementSheetExtractionService`

- Un seul service, une seule interface, un seul type de résultat (`ElementSheetExtractionResult` : éléments,
  points, erreurs, zone) pour les 5 feuilles d'éléments. Pour chaque bloc lu par `RepeatingBlockReader` :
  repère `{repereEcho}-{Identification}` par simple concaténation (pas de `TextTransform`, G9) ;
  `IsolementPivot` à partir des champs connus (`Designation`/`PositionALaPose` vides si absents ou vides) ;
  couleur à partir du champ du bloc `CouleurEtiquette` s'il est déclaré, sinon `DefaultCouleurEtiquette`,
  filtrée par `AllowedCouleursEtiquette` (même règle que `CouleurEtiquetteResolver`, G10) ; colonnes
  inconditionnelles ; règles de point évaluées sur
  **tous** les champs du bloc (groupées par colonne, OU dans le groupe, une seule fois par colonne et par
  bloc) ; avertissement `NoConditionalPointCreated` (valeur rapportée : `TypeElement`) seulement si
  `WarnWhenNoConditionalPoint`, qu'il existe des règles et qu'aucune colonne conditionnelle n'est cochée ;
  zone lue dans le champ d'en-tête `zone` s'il est déclaré.
- `ConditionalPointRuleEvaluator` gère `IsNotBlank` (valeur rognée non vide).
- Avertissements : un seul `DeduplicatedWarningTracker` (feuille, code, construction du message), utilisé
  pour `NoConditionalPointCreated` et `UnexpectedCouleurEtiquetteValue` (G11). Messages et dédoublonnage
  identiques à l'existant (rogné, casse ignorée, première forme conservée).
- **Tests unitaires** (Moq), dont : le cas J2M76 tel que saisi par le client (champs facultatifs,
  `Equals DEBUT MAD`) coche les colonnes ; champ facultatif vide ≠ bloc rejeté ; `NotEquals` sur un champ
  vide ; `IsNotBlank` ; case d'avertissement cochée/décochée ; deux règles de la même colonne satisfaites →
  un seul point ; `Identification` ou `TypeElement` non déclaré → erreur de paramétrage ; anti-codage-en-dur
  du repère et de la zone (deux profils, deux cellules).
- Pas encore branché dans l'orchestrateur.

### 84.5 — BlazorAdmin : éditeur et brouillons

- Brouillons et mapper (§9 des recommandations) : `IsRequired` (case, décochée par défaut pour un nouveau
  champ), `WarnWhenNoConditionalPoint` (case au niveau de la règle de feuille), opérateur `IsNotBlank`
  (valeur masquée et vide), champ source en **liste fermée** alimentée par les noms des champs du bloc de
  la même règle (y compris la ligne en attente ; une valeur hors liste d'un ancien profil s'affiche
  « (inconnu) » et n'est pas perdue).
- Sur PROCEDURE, la case « obligatoire » et la case d'avertissement ne sont pas affichées : son service
  parcourt ses tâches lui-même, de façon tolérante, et n'émet pas cet avertissement (lot 083).
- Aller-retour du mapper : profil standard **et** profil construit à la main enrichi des trois nouveaux
  réglages (dont une règle `IsNotBlank` et un champ facultatif).
- Tests `…EditorPendingRowTests` pour une ligne en attente de chaque nouvelle forme.

### 84.6 — Bascule : orchestrateur et profil standard

- `ImportPipelineOrchestrator` appelle `ElementSheetExtractionService` pour les 5 feuilles, dans une boucle
  (ordre inchangé : ISOLEMENT, PLATINES, ORIFICES, AJT, DIVERS) ; la zone est celle de DIVERS (G11).
- `DefaultProfileSeeder` réécrit : `repereEcho` sur les 5 feuilles (G6) ; `zone` sur DIVERS ; champs
  obligatoires reproduisant l'actuel (Désignation facultative sur ISOLEMENT) ; ISOLEMENT : champ facultatif
  `ZeroEnergie` (V, -1..0) + règle `ZeroEnergie Equals ZERO ENERGIE` → PS941, case d'avertissement cochée ;
  PLATINES : champs facultatifs `PoseeLe` (H:N +2) et `DeposeeLe` (H:N +3) + 4 règles
  (`PoseeLe`/`DeposeeLe Equals DEBUT MAD` → RECEPTION DEBUT MAD, idem REL), case décochée ; ORIFICES : case
  décochée ; AJT, DIVERS : case cochée. Couleur : champ facultatif `CouleurEtiquette` (H:N +1) sur PLATINES
  et ORIFICES au lieu de la cellule dédiée ; AJT garde sa couleur par défaut `BLEUE` ; listes de couleurs
  autorisées inchangées.
- **Critère d'acceptation : le test 84.0 reste vert sans régénération.** Tout écart est analysé ; seuls ceux
  du §6 sont acceptables.
- `DefaultProfileSeederTests` mis à jour ; les tests d'intégration existants par feuille sont repointés sur
  le nouveau service, assertions inchangées.
- Dans le même commit : `ImportSheetUsage` déclare que les 5 feuilles lisent règles conditionnelles et
  en-têtes. Sinon ses garde-fous contre le vrai pipeline (`ImportSheetUsageTests`) cassent à la bascule
  (précédent du lot 083).

### 84.7 — BlazorAdmin : vue Détails et éditeur simplifiés

- `ImportSheetUsage` réduit à deux formes (PROCEDURE, feuille d'éléments) et à ce qui reste codé en dur :
  vocabulaire tâche/élément, comportements fixes de PROCEDURE, rôles des en-têtes (`repereEcho`, `zone`).
  `SheetRuleIgnoredSettings` réduit à : nom de feuille non traité, sections sans effet sur PROCEDURE,
  combinaisons de couleur. Les avertissements de `b645159` propres aux feuilles d'éléments disparaissent.
- Phrases de la vue Détails pour `IsNotBlank`, champ facultatif, case d'avertissement ; catalogue figé du
  ticket 078 §5 mis à jour avec le profil standard réécrit.
- Section zéro énergie, section « cellule renseignée » et saisie de la cellule couleur retirées de
  `SheetRuleForm` ; `FieldPresencePointRuleForm.razor` supprimé. Les combinaisons de couleur signalées
  (« couleur par défaut ignorée », « couleurs autorisées sans cellule ») portent désormais sur la présence du
  champ du bloc `CouleurEtiquette`.

### 84.8 — Suppression de l'ancien

- Supprimés : `IsolementExtractionService`, `UnconditionalIsolementSheetExtractionService`,
  `AutresJointsTouchesExtractionService`, `DiversExtractionService` (et interfaces, résultats dédiés,
  enregistrements DI des deux hôtes), `FieldPresencePointRule`, `SheetExtractionRule.FieldPresencePointRules`
  et `ZeroEnergieExpectedValue`, `UnexpectedZeroEnergieValueWarningTracker`, le code d'erreur
  `UnexpectedZeroEnergieValue`, `IsolementPivot.HasZeroEnergie` (aucun consommateur, à reconfirmer par
  recherche), `SheetExtractionRule.CouleurEtiquetteCell`, `CouleurEtiquetteResolver`,
  `NoConditionalPointCreatedWarningTracker` et `UnexpectedCouleurEtiquetteValueWarningTracker`,
  `IsolementSheetExtractionResult`, `DiversSheetExtractionResult`, tout l'arbre `TextTransform` (G9) et ses
  tests Domain/Application, avec leurs `DomainErrorCode`. Clés `.resx` orphelines retirées.
- Migration EF : suppression de la table `ImportProfileSheetRuleFieldPresencePointRules` et des colonnes
  `ZeroEnergieExpectedValue` et `CouleurEtiquetteCell*`. Deux des trois astuces de matérialisation EF
  « référence vers un type possédé » (`FieldPresencePointRule.Cell`, `CouleurEtiquetteCell`) disparaissent.
- 84.0 toujours vert ; suite complète verte.

### 84.9 — Modèle : suppression du réglage « champ d'arrêt » (G12)

- `RepeatingBlockLocator.StopFieldName` supprimé, avec sa validation (`RepeatingBlockLocator_EmptyStopFieldName`).
  `RepeatingBlockReader` reçoit le nom du champ d'arrêt de son appelant : `ElementSheetExtractionService`
  passe `Identification`. PROCEDURE s'arrêtait déjà sur `Action`.
- Migration EF (colonne supprimée) ; brouillon, mapper, `SheetRuleForm` (saisie retirée), vue Détails
  (phrase « champ d'arrêt configuré mais ignoré » retirée), `SheetRuleIgnoredSettings.IgnoredStopFieldFixedName`
  et `ImportSheetUsage.FixedStopFieldName` supprimés.
- Retouches mécaniques des constructions de `RepeatingBlockLocator` dans les tests (environ 150 dans
  50 fichiers). Aucune assertion modifiée ; 84.0 vert.

### 84.10 — Modèle : un seul nom de feuille par règle (G13)

- `RepeatingBlockLocator.Sheet` supprimé (avec `RepeatingBlockLocator_EmptySheet`) ; la validation « nom de feuille = feuille du localisateur »
  (`SheetExtractionRule_SheetNameLocatorMismatch`) disparaît avec lui. `RepeatingBlockReader` reçoit le nom
  de feuille de son appelant.
- `HeaderFieldRule.Cell` (`DirectCell`) devient `HeaderFieldRule.CellRange` (même validation de plage) ;
  `HeaderRuleResolver` lit la plage dans la feuille de la règle. `DirectCell` est supprimé, ainsi que la
  troisième astuce de matérialisation EF (`HeaderFieldRule.Cell`) et la recopie du nom de feuille faite par
  l'éditeur à la sauvegarde (lot 048, décision 2).
- Migration EF (colonnes de feuille du localisateur et des champs d'en-tête supprimées ; valeurs
  redondantes, aucune perte).
- Retouches mécaniques des tests (environ 120 `DirectCell` dans 27 fichiers, plus les localisateurs).
  Aucune assertion modifiée ; 84.0 vert ; aller-retour des brouillons vert.

### 84.11 — API : paramétrage invalide hérité

- Entrée `UnknownFieldReference` dans `ApplicationMessages.resx`/`.fr.resx`, `GlobalExceptionHandler` → 422.
  Le test du lot 065 qui s'appuie sur cette exception pour éprouver un 500 est réécrit avec une autre
  exception non mappée.

### 84.12 — Documentation et client

- `CLAUDE.md` (bullet du lot, mise à jour des bullets 047/048/063/068/PLATINES DEB-FIN et « Import editor
  warns… »), spec d'extraction (feuilles d'éléments : un seul fonctionnement), modèle de domaine
  (primitives supprimées, localisateur et champs d'en-tête simplifiés).
- Réponse au client J2M76 : après réinitialisation, sa méthode (champs du bloc + règles conditionnelles)
  est la méthode normale ; rappeler de lire les deux cellules H (`DEBUT MAD`/`DEBUT REL` peuvent apparaître
  dans l'une ou l'autre).

## 5. Mise en service

- **Tous les profils d'import doivent être réinitialisés ou refaits.** Les règles « cellule renseignée »,
  la valeur zéro énergie et la cellule couleur disparaissent (84.8) ; un profil copié qui ne déclare pas
  `repereEcho` sur ISOLEMENT, PLATINES ou ORIFICES fait échouer l'import. La vue Détails signale le champ
  manquant. Les suppressions de 84.9/84.10 ne perdent rien (valeurs redondantes ou ignorées).
- Déploiement : livrer le lot en une fois (quatre migrations EF, appliquées automatiquement au démarrage,
  lot G4), puis réinitialiser le profil standard.

## 6. Écarts acceptés sur les références de 84.0

- Identifiant de bloc et texte des messages d'erreur `RequiredFieldMissing` sur ISOLEMENT (un seul format
  désormais).
- Aucun autre écart n'est attendu. Un écart de points, d'éléments, de repères ou de cellules générées est
  un défaut à corriger, pas une référence à régénérer.

## 7. Hors périmètre

- Le service de PROCEDURE (tâches) : seuls son localisateur et ses champs d'en-tête suivent les simplifications de 84.9 et 84.10, son comportement ne change pas.
- Rendre la liste des feuilles traitées paramétrable (les 6 noms restent connus de l'orchestrateur).
- Repère d'un élément autrement qu'en `{repereEcho}-{Identification}`.
- Migration de données d'un profil existant (G8).
- Le bug de cohérence des types de tâches découvert au lot 083 (tâche séparée).
