# Tickets TDD — Lot 055 : sémantique et volume des avertissements d'extraction

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Lot numérique
suivant le lot 054 (`tickets-tdd-lot-054-page-accueil-indicateurs.md`).*

**Contexte** : un import réel du fichier `Dossier_de_MaD_IDL_-_C7401.xlsx` produit 9 avertissements
non bloquants, dont 8 lignes strictement identiques au repère près :

```
ISOLEMENT  C7401-V1  UnrecognizedTypeElement  No configured condition on 'TypeElement' matched
                                              for Colonne 'ZÉRO ENERGIE EN PRESENCE EE (PS941)';
                                              extracted value was 'PROLOCK'.
```

Trois défauts distincts partagent une racine unique — l'absence de spécification précise de ce que
cet avertissement signifie :

1. **Le code d'erreur ne décrit pas le fait constaté.** `UnrecognizedTypeElement` affirme une
   propriété du *référentiel OXO* (« cette valeur n'existe pas »). Le message affirme une propriété
   du *profil d'import* (« aucune condition configurée n'a matché »). Ce sont deux propositions
   différentes. Le moteur d'extraction ne connaît que le profil ; il n'a aucun moyen de juger le
   référentiel, et ne doit donc rien affirmer à son sujet.
2. **L'émission est par règle, pas par élément.** Sur une feuille portant plusieurs
   `ConditionalPointRule`, un seul élément produit autant d'avertissements que de règles non
   satisfaites — y compris pour des valeurs parfaitement légitimes. En feuille DIVERS (7 règles sur
   4 valeurs distinctes), un élément `SOUPAPE` produit 5 avertissements tout en ayant correctement
   généré ses 2 Points.
3. **Le volume noie le signal.** Un utilisateur confronté à 8 lignes identiques cesse de les lire,
   et manque l'avertissement réellement singulier (`VANNE` sur D8570, incohérence de type sur
   PROCEDURE).

S'y ajoute une incohérence de langue déjà relevée par `audit-qualite-domain-2026-07-25.md` (§4.2) :
`ExtractionErrorCode.TacheMultipleTypeIncoherence` est le seul membre d'énumération francophone de
tout le Domain, et il apparaît dans la même liste d'avertissements qu'un message rédigé en anglais,
dans une interface francophone.

---

## Relevé factuel préalable (déjà établi, ne pas ré-instruire)

Inspection des trois fixtures réelles (openpyxl, résolution des cellules fusionnées) :

| Feuille | C7401 | D8570 | G6306B |
| :--- | :--- | :--- | :--- |
| ISOLEMENT (`B22:E23`, pas 7) | 8 × `PROLOCK` | 14 × `PROLOCK`, 1 × `VANNE` | 3 × `PROLOCK` |
| AUTRES JOINTS TOUCHES (pas 7) | aucun élément | 13 × `TUYAUTERIE` | 2 × `TUYAUTERIE`, 2 × `TUBING` |
| DIVERS (pas 3) | aucun élément | `ZERO ENERGIE` uniquement | `INSTRUMENTATION`, `ZERO ENERGIE`, `SOUPAPE`, `POINT DE FEU` |
| PLATINES / ORIFICES CAPACITES | — | — | — (aucune `ConditionalPointRule` au profil) |

Trois constats qui conditionnent la conception de ce lot :

- **`ZERO ENERGIE` n'apparaît jamais en feuille ISOLEMENT**, dans aucun des trois fichiers. La
  `ConditionalPointRule` `(TypeElement, Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE
  (PS941)")` de cette feuille est conforme à `spec-extraction-fichier-source-oxo.md` §2 — cette
  décision n'est pas rouverte — mais aucun fichier connu ne la déclenche. Le chemin « condition
  vraie » de ISOLEMENT n'est donc couvrable qu'en test unitaire, jamais en intégration sur les
  fixtures actuelles.
- **`PROLOCK` est le contenu normal de la feuille ISOLEMENT**, pas une anomalie de saisie : 26
  éléments sur 26, trois équipements différents. À distinguer de `VANNE`, occurrence unique et
  isolée.
- **`POINT DE FEU` (G6306B, DIVERS) ne matche pas le littéral `"POINT FEU"`** retenu au profil.
  C'est l'échec de correspondance légitime déjà documenté (`modele-domaine-import-profile.md`
  §1.4) — différence de mot, non rattrapable par `Trim` ni par la casse. Aucune correction à
  apporter : c'est un cas de test, pas un défaut.

---

## Décisions actées avec Simon (29/07) — non négociables, ne pas rouvrir

**Sémantique recentrée.** Le moteur d'extraction ne juge que le profil, jamais le référentiel OXO.
Le seul fait qu'il peut honnêtement signaler est : *un élément n'a produit aucun Point conditionnel
alors que sa feuille en définit au moins un*.

**Règle d'émission.** Une entrée d'avertissement par couple `(feuille, valeur extraite normalisée)`
n'ayant produit aucun Point conditionnel. La déduplication a lieu **à l'émission, dans le moteur** —
ce n'est pas une agrégation de couche de présentation.

**Clé de déduplication.** La valeur normalisée (`Trim` + insensible à la casse), cohérente avec la
normalisation déjà appliquée à la comparaison `ComparisonValue`. La **première forme brute
rencontrée** est celle conservée pour l'affichage.

**Valeur vide ou nulle.** Traitée comme n'importe quelle autre valeur sans correspondance. Pas de
cas particulier, pas de code d'erreur distinct.

**Sévérité.** `Warning`. Le modèle de sévérité reste `Warning` / `Error` et rien d'autre : tout ce
qui est non bloquant est `Warning`. Aucun niveau `Information` n'est introduit.

**Renommages.** `UnrecognizedTypeElement` → `NoConditionalPointCreated`.
`TacheMultipleTypeIncoherence` → identifiant anglais (`TacheMultipleTypeMismatch`), conformément à
la recommandation de `audit-qualite-domain-2026-07-25.md` §4.2 — les identifiants C# sont en
anglais, le vocabulaire métier français ne vit que dans les chaînes de caractères.

**Langue des messages utilisateur.** Français, comme `TacheMultipleTypeIncoherence` le fait déjà.

**Champs structurés.** L'entrée d'erreur porte la valeur extraite comme donnée structurée, non
interpolée dans le message.

### Caractéristiques connues et acceptées de la règle retenue

Ces deux points sont des conséquences assumées, **pas des défauts à corriger** :

- **`TUBING` sur AUTRES JOINTS TOUCHES produira un avertissement.** La règle de cette feuille est
  `(TypeElement, NotEquals, "TUBING", "POSE ÉTIQUETTES")` : elle exclut délibérément ce type, donc
  zéro Point conditionnel pour `TB1`/`TB2` est exactement le comportement demandé par l'auteur du
  profil. L'avertissement est techniquement vrai et fonctionnellement inutile. Il est conservé
  plutôt que traité par un cas particulier sur l'opérateur, qui complexifierait le moteur pour un
  gain d'une ligne par fichier.
- **ISOLEMENT produira systématiquement un avertissement `PROLOCK`** sur tout dossier réel connu.
  C'est l'information vraie que la règle `ZERO ENERGIE` de cette feuille ne trouve jamais son cas.

---

## Hors périmètre explicite de ce lot

- **Interrogation du référentiel `TypeElement` de la base OXO.** Déterminer si `PROLOCK` est un type
  légitime supposerait un endpoint dédié côté `AvancementRecette` (ASP.NET MVC 5) et un appel
  sortant depuis AM-OXO-ETL vers le legacy. C'est précisément le couplage que ce microservice a été
  conçu pour éviter. **Décision : hors périmètre de ce lot et du projet en l'état.** Si l'import
  legacy rejette un `TypeElement.Nom` inconnu, c'est un problème pour l'utilisateur final, pas un
  défaut d'AM-OXO-ETL : l'extraction et la génération restent correctes, la donnée source est
  transportée telle quelle sans correction silencieuse. Ne pas rouvrir ce point, ne pas proposer de
  mécanisme de validation contre un référentiel.
- **Agrégation ou repliement côté couche de présentation.** La déduplication à l'émission suffit
  (KISS/YAGNI). Aucun accordéon, aucun compteur « N avertissements (M occurrences) », aucune
  structure de regroupement en UI.
- **Exposition des avertissements dans le contrat M2M `POST /api/oxo/process`.** Sujet légitime,
  identifié, mais traité ultérieurement dans un lot dédié. Si l'investigation 55.0 révèle que les
  avertissements transitent effectivement par la réponse HTTP, le **constater et le documenter**
  dans le compte rendu du lot — ne rien modifier au contrat.
- **Compatibilité des libellés historiques dans `SystemLogs`.** Sans objet : phase de développement
  et pré-production, aucune donnée réelle, base régulièrement supprimée et recréée. Aucune
  migration de données, aucun alias de compatibilité, aucune note de rétrocompatibilité.
- **Modification des `ConditionalPointRule` du profil par défaut** (`DefaultProfileSeeder`). Le
  contenu du profil seedé n'est pas touché par ce lot. En particulier, la règle `ZERO ENERGIE` de
  ISOLEMENT est conservée telle quelle malgré le constat qu'aucune fixture ne la déclenche.
- **Traitement particulier de l'opérateur `NotEquals`** dans la règle d'émission (voir
  « caractéristiques connues et acceptées » ci-dessus).
- **Renommage des autres membres de `ExtractionErrorCode`** (`RequiredFieldMissing`,
  `UnparsableValue`) : déjà en anglais, conformes, non touchés.

---

## 55.0. Investigation préalable — obligatoire avant toute ligne de code

Aucun test n'est écrit et aucun fichier de production n'est modifié avant que cette étape ne soit
close et que ses réponses ne soient consignées.

- [ ] Lire `ConditionalPointRuleEvaluator` et ses tests existants. Répondre par écrit : la méthode
  de correspondance est-elle appelée **par règle** dans une boucle du service appelant, ou le
  service délègue-t-il l'évaluation de l'ensemble des règles d'une feuille ? Où exactement l'entrée
  d'erreur `UnrecognizedTypeElement` est-elle construite ?
- [ ] Lire `IsolementExtractionService`, `DiversExtractionService`, `AutresJointsTouchesExtractionService`.
  Répondre : la logique d'émission de l'avertissement est-elle **factorisée** (méthode partagée,
  classe commune) ou **dupliquée** dans chacun des trois services ? Ce point détermine si 55.4 est
  un changement en un point ou en trois.
- [ ] Identifier le type exact porté par `ImportResult.Errors` (nom, forme : `record` ? champs ?
  message pré-formaté ou gabarit + arguments ?), ainsi que l'énumération de sévérité associée.
  Répondre : existe-t-il déjà un mécanisme d'arguments structurés comparable à
  `IHasDomainErrorCode.Args` du Domain, ou le message est-il une `string` construite par
  interpolation au point d'émission ?
- [ ] Lire `ExtractionErrorLogging`. Répondre : la correspondance code → sévérité y est-elle
  centralisée sous forme de `switch`/table, et faut-il l'y mettre à jour pour les deux renommages ?
- [ ] Identifier le composant Blazor qui rend le tableau « Avertissements non bloquants (N) » de la
  page de test d'import, et le nom exact de l'en-tête de sa deuxième colonne (celle qui affiche
  aujourd'hui `C7401-V1`). Répondre : cet en-tête provient-il d'une clé resx, et laquelle ?
- [ ] Confirmer contre le profil seedé par `DefaultProfileSeeder` quelles feuilles portent au moins
  une `ConditionalPointRule` (attendu : ISOLEMENT, AUTRES JOINTS TOUCHES, DIVERS — et **pas**
  PLATINES ni ORIFICES CAPACITES). Ce point conditionne les assertions de 55.8.
- [ ] Vérifier si les avertissements sont exposés dans la réponse de `POST /api/oxo/process`
  (constat seulement, aucune modification — voir « hors périmètre »).

**Effort** : élevé. C'est la seule étape du lot où la compréhension de l'existant est en jeu.

---

## 55.1. Renommage `UnrecognizedTypeElement` → `NoConditionalPointCreated`

**Comportement** : le code d'erreur émis lorsqu'un élément ne produit aucun Point conditionnel
s'appelle `NoConditionalPointCreated`. L'identifiant `UnrecognizedTypeElement` n'existe plus nulle
part dans la solution.

**Rouge** : adapter les tests existants qui référencent `ExtractionErrorCode.UnrecognizedTypeElement`
pour attendre `ExtractionErrorCode.NoConditionalPointCreated` — ils échouent à la compilation.

**Vert** : renommer le membre de l'énumération et propager mécaniquement. Vérifier
`ExtractionErrorLogging` (correspondance code → `Warning`) et toute clé resx dérivant du nom du
code.

**Refacto** : néant, renommage pur.

**Vérification de clôture** : une recherche plein texte de `UnrecognizedTypeElement` sur toute la
solution ne retourne aucune occurrence.

**Effort** : standard.

---

## 55.2. Renommage `TacheMultipleTypeIncoherence` → `TacheMultipleTypeMismatch`

**Comportement** : tous les membres de `ExtractionErrorCode` portent un identifiant anglais. Le
message utilisateur associé reste en français, inchangé sur le fond.

**Rouge** : adapter les tests existants référençant l'ancien identifiant — échec à la compilation.

**Vert** : renommer le membre, propager aux points de construction (`TacheMultipleTypeCoherenceAnalyzer`,
`ProcedureExtractionService`) et à `ExtractionErrorLogging`.

**Refacto** : néant.

**Vérification de clôture** : aucun membre de `ExtractionErrorCode` ne contient de mot français ;
une recherche plein texte de `TacheMultipleTypeIncoherence` ne retourne aucune occurrence.

**Effort** : standard.

**Note** : ce sous-ticket est totalement indépendant de 55.1 et de tout le reste du lot. Il ne
partage avec eux que le fichier d'énumération.

---

## 55.3. Champs structurés sur l'entrée d'erreur

**Comportement** : la valeur extraite est portée par l'entrée d'erreur comme **donnée structurée**,
et non plus uniquement interpolée dans une chaîne de message déjà formatée.

**Conséquence de la sémantique recentrée** : l'avertissement ne se rapporte plus à une `Colonne`
particulière — il n'y a plus de Colonne unique à désigner, puisqu'aucune des règles de la feuille
n'a produit de Point. **Le nom de Colonne disparaît donc de cette entrée d'erreur**, du message
comme des champs.

**Rouge** : écrire un test asservissant la présence et la valeur du champ structuré sur l'entrée
d'erreur produite (assertion sur le champ, pas sur le texte du message).

**Vert** : ajouter le champ. Si l'investigation 55.0 a révélé un mécanisme d'arguments structurés
déjà existant sur ce type, **le réutiliser** plutôt que d'ajouter une propriété dédiée ; sinon,
propriété dédiée nullable, non renseignée par les autres codes d'erreur.

**Refacto** : vérifier que les autres codes d'erreur (`RequiredFieldMissing`, `UnparsableValue`,
`TacheMultipleTypeMismatch`) ne sont pas dégradés par le changement de forme du type.

**Effort** : standard ; élevé au refacto si un mécanisme d'arguments doit être introduit.

---

## 55.4. Règle d'émission : par élément, non par règle

**Comportement** : pour chaque élément extrait d'une feuille portant au moins une
`ConditionalPointRule`, si **aucune** de ces règles n'a produit de Point, une entrée d'avertissement
`NoConditionalPointCreated` est produite. Si au moins une règle a produit un Point, **aucune** entrée
n'est produite, quel que soit le nombre de règles non satisfaites. Une feuille sans aucune
`ConditionalPointRule` ne produit jamais cet avertissement.

**Rouge** — cas unitaires à couvrir, tous sur profils construits inline :

| Cas | Profil de feuille | Valeur de l'élément | Attendu |
| :--- | :--- | :--- | :--- |
| a | 1 règle `Equals "ZERO ENERGIE"` | `ZERO ENERGIE` | 1 Point, 0 avertissement |
| b | 1 règle `Equals "ZERO ENERGIE"` | `PROLOCK` | 0 Point, 1 avertissement |
| c | 7 règles sur 4 valeurs (réplique DIVERS) | `SOUPAPE` | 2 Points, **0 avertissement** |
| d | 7 règles sur 4 valeurs (réplique DIVERS) | `POINT DE FEU` | 0 Point, 1 avertissement |
| e | 1 règle `NotEquals "TUBING"` | `TUYAUTERIE` | 1 Point, 0 avertissement |
| f | 1 règle `NotEquals "TUBING"` | `TUBING` | 0 Point, 1 avertissement |
| g | 0 règle, ≥1 `UnconditionalColonneNames` | n'importe laquelle | Points inconditionnels créés, 0 avertissement |
| h | 1 règle quelconque | valeur vide ou `null` | 0 Point, 1 avertissement |

Le cas **c** est le cœur du sous-ticket : c'est celui qui échoue aujourd'hui (5 avertissements
attendus par l'implémentation actuelle, 0 attendus par la nouvelle règle).

Le cas **g** garantit qu'aucune régression n'affecte PLATINES et ORIFICES CAPACITES.

**Vert** : déplacer la décision d'émission du niveau « règle » au niveau « élément ». Si
l'investigation 55.0 a révélé une logique dupliquée dans les trois services, la factoriser à cette
occasion plutôt que de la corriger trois fois.

**Refacto** : le lieu naturel de cette logique est le composant qui connaît l'ensemble des règles de
la feuille et le résultat de leur évaluation pour un élément donné — pas
`ConditionalPointRuleEvaluator`, dont la responsabilité (évaluer *une* règle) ne doit pas être
élargie.

**Effort** : standard au rouge et au vert, **élevé au refacto** — c'est ici que se décide le
placement de la responsabilité.

---

## 55.5. Déduplication par valeur normalisée

**Comportement** : au sein d'une même feuille et d'un même import, plusieurs éléments partageant la
même valeur normalisée (`Trim` + insensible à la casse) et ne produisant aucun Point conditionnel
produisent **une seule** entrée d'avertissement. La valeur affichée est la **première forme brute
rencontrée** dans l'ordre de lecture.

**Rouge** — cas unitaires :

| Cas | Éléments d'une même feuille | Attendu |
| :--- | :--- | :--- |
| a | 8 × `PROLOCK` | 1 entrée, valeur `PROLOCK` |
| b | `PROLOCK`, `VANNE` | 2 entrées, une par valeur |
| c | `SOUPAPE `, `soupape` (dans cet ordre) | 1 entrée, valeur affichée `SOUPAPE ` (première forme brute) |
| d | 2 éléments à valeur vide | 1 entrée |
| e | même valeur sur deux feuilles différentes | 2 entrées, une par feuille |

Le cas **c** vérifie la cohérence avec la normalisation du matching : deux formes qui matcheraient
la même `ComparisonValue` ne doivent pas produire deux avertissements.

Le cas **e** vérifie que la clé de déduplication est bien `(feuille, valeur normalisée)` et non la
valeur seule.

**Vert** : accumuler les valeurs déjà signalées par feuille pendant l'extraction ; n'ajouter une
entrée qu'à la première rencontre.

**Refacto** : la portée de l'accumulateur est un point de vigilance — il doit être local à
l'extraction d'une feuille pour un fichier, jamais partagé entre deux exécutions (aucun état
statique, aucun champ d'instance sur un service enregistré en singleton).

**Effort** : standard ; élevé au refacto pour statuer sur la portée de l'accumulateur.

---

## 55.6. Message utilisateur en français

**Comportement** : le message de `NoConditionalPointCreated` est rédigé en français, ne mentionne
aucune `Colonne`, et n'affirme rien sur le référentiel OXO. Formulation retenue :

> Aucun Point conditionnel n'a été créé pour la valeur « {valeur} » : aucune condition du profil
> d'import ne correspond à cette valeur pour cette feuille.

Pour une valeur vide ou nulle, la formulation reflète l'absence de valeur plutôt que d'afficher des
guillemets vides.

**Rouge** : test asservissant le message produit, y compris le cas de la valeur absente.

**Vert** : suivre le mécanisme de localisation déjà en place pour `TacheMultipleTypeMismatch` — clé
resx s'il en utilise une, chaîne construite au même endroit sinon. **Ne pas introduire de mécanisme
de localisation parallèle.**

**Refacto** : néant.

**Effort** : standard.

**Point de vigilance** : ce message ne doit contenir aucun jugement du type « valeur inconnue »,
« type non reconnu » ou « erreur de saisie » — c'est exactement le glissement sémantique que ce lot
corrige.

---

## 55.7. Affichage Blazor : colonne de contexte

**Comportement** : dans le tableau des avertissements de la page de test d'import, la colonne qui
affichait le repère de l'élément (`C7401-V1`) affiche, pour les entrées `NoConditionalPointCreated`,
la **valeur extraite brute** (`PROLOCK`). Les autres codes d'erreur conservent leur contexte actuel
inchangé.

**Rouge** — tests bUnit, sélection par ID HTML stable uniquement (jamais par texte ni par position,
cf. `recommandations-tickets-tdd.md`) :
- un `ImportResult` contenant une entrée `NoConditionalPointCreated` rend la valeur extraite dans la
  cellule de contexte ;
- un `ImportResult` contenant une entrée `TacheMultipleTypeMismatch` rend son contexte d'origine,
  non modifié ;
- le compteur de l'en-tête (« Avertissements non bloquants (N) ») reflète le nombre réel d'entrées
  après déduplication.

**Vert** : si des IDs stables manquent sur les cellules concernées, les ajouter — c'est un
prérequis de la convention, pas une extension de périmètre.

**Refacto** : vérifier si l'intitulé de la colonne (issu d'une clé resx identifiée en 55.0) reste
juste maintenant qu'elle n'affiche plus systématiquement un repère. Si l'intitulé actuel est
spécifique au repère, proposer un intitulé neutre — sans redesign du tableau.

**Effort** : standard.

---

## 55.8. Tests d'intégration sur les trois fixtures réelles

**Comportement** : le pipeline complet, exécuté avec le **profil seedé** (récupéré via
`IImportProfileStore`, jamais construit inline dans le test — même convention que les tests de
non-régression de `tickets-tdd-seed-profils-defaut.md`), produit sur chaque fixture les entrées
`NoConditionalPointCreated` attendues, et seulement celles-là.

**Rouge** — assertions attendues :

| Fixture | Entrées `NoConditionalPointCreated` attendues |
| :--- | :--- |
| `Dossier_de_MaD_IDL_-_C7401.xlsx` | 1 : `ISOLEMENT` / `PROLOCK` |
| `D8570_chgt_plateaux` | 2 : `ISOLEMENT` / `PROLOCK` et `ISOLEMENT` / `VANNE` |
| `G6306B_REV` | 3 : `ISOLEMENT` / `PROLOCK`, `AUTRES JOINTS TOUCHES` / `TUBING`, `DIVERS` / `POINT DE FEU` |

Assertions complémentaires, sur les trois fixtures :
- **aucune** entrée `NoConditionalPointCreated` pour les feuilles `PLATINES` et `ORIFICES CAPACITES` ;
- **aucune** entrée pour les éléments `DIVERS` de type `ZERO ENERGIE` (D8570 et G6306B),
  `INSTRUMENTATION` et `SOUPAPE` (G6306B) — ils produisent leurs Points normalement ;
- **aucune** entrée pour les éléments `AUTRES JOINTS TOUCHES` de type `TUYAUTERIE` (D8570 et
  G6306B) — la règle `NotEquals "TUBING"` est satisfaite ;
- le nombre d'Isolements extraits par feuille reste **identique** aux assertions des
  `ImportPipelineOrchestratorIntegrationTests` existants — aucun élément ne doit être perdu par ce
  lot ; l'avertissement change, l'extraction non.

**Vert** : aucun code de production attendu ici si 55.4 et 55.5 sont corrects. Si une assertion
échoue, c'est un défaut de 55.4/55.5, pas un test à ajuster.

**Refacto** : néant.

**Effort** : standard.

**Points de vigilance** :
- Les volumes du tableau ci-dessus proviennent d'une inspection openpyxl avec résolution naïve des
  cellules fusionnées. **Les confirmer contre le comportement réel de `RowRangeLocator` avant de
  figer les assertions** — notamment la condition d'arrêt sur la valeur d'identification, qui peut
  faire diverger le nombre d'éléments lus.
- Les avertissements des **autres** codes (`TacheMultipleTypeMismatch` sur PROCEDURE de C7401,
  notamment) ne sont pas dans le périmètre de ces assertions : filtrer sur le code, ne pas asserter
  le total brut de `ImportResult.Errors`.

---

## Note d'efficacité d'implémentation (Claude Code)

- **55.0 conditionne tout le reste.** En particulier, la réponse à « la logique d'émission est-elle
  factorisée ou dupliquée dans les trois services ? » détermine si 55.4 est un changement en un
  point ou en trois. Ne pas commencer 55.4 sans cette réponse.
- **55.2 est totalement indépendant** du reste du lot (renommage pur, autre code d'erreur). Il peut
  être livré en premier, seul, dans son propre commit — c'est le moyen le plus rapide d'obtenir un
  premier cycle vert et de valider que la propagation d'un renommage d'énumération ne réserve pas de
  surprise avant d'attaquer 55.1.
- **55.1 et 55.3 doivent précéder 55.4 et 55.5** : inutile d'écrire les tests de la nouvelle règle
  d'émission contre un type d'erreur qui va changer de nom et de forme juste après.
- **55.4 et 55.5 se livrent dans le même cycle.** Les séparer ferait passer deux fois au même
  endroit, et l'état intermédiaire (nouvelle règle sans déduplication) n'a aucune valeur : il
  produit exactement le même volume d'avertissements qu'aujourd'hui sur ISOLEMENT.
- **55.8 ne doit pas être écrit avant que 55.4 et 55.5 ne soient verts en unitaire.** Un échec
  d'intégration sur fixture réelle est coûteux à diagnostiquer ; les cas unitaires du tableau de
  55.4 couvrent déjà toutes les combinaisons logiques.
- **Exécuter les tests avec `--verbosity quiet` et filtrer sur la classe en cours** plutôt que de
  relancer toute la suite à chaque itération (`recommandations-tickets-tdd.md` §3).
- **En cas d'échec inattendu sur bUnit ou sur fixture réelle**, interrompre et inspecter le diff
  plutôt que d'enchaîner les tentatives de correction (`recommandations-tickets-tdd.md` §5).

## Ordre recommandé

1. **55.0** — investigation (bloquant, effort élevé)
2. **55.2** — renommage indépendant, premier cycle vert rapide
3. **55.1** — renommage du code principal
4. **55.3** — champs structurés
5. **55.4 + 55.5** — même cycle : règle d'émission et déduplication
6. **55.6** — message français
7. **55.7** — affichage Blazor
8. **55.8** — intégration sur les trois fixtures, en clôture

---

## Critères de clôture du lot

- Les 613+ tests existants restent verts, sans assertion affaiblie ni test supprimé pour
  accommoder le changement.
- Une recherche plein texte de `UnrecognizedTypeElement` et de `TacheMultipleTypeIncoherence` sur
  toute la solution ne retourne aucune occurrence.
- Un import réel de `Dossier_de_MaD_IDL_-_C7401.xlsx` depuis la page de test Blazor affiche **2**
  avertissements non bloquants — l'incohérence de type sur PROCEDURE, et une seule ligne
  `ISOLEMENT` / `PROLOCK`.
- Aucun membre de `ExtractionErrorCode` ne porte d'identifiant francophone.
- Aucun message d'avertissement affiché à l'utilisateur n'est en anglais.
