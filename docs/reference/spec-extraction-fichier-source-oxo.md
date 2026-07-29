# Spécification d'extraction — Fichier source OXO (feuille par feuille)

*État courant de la spécification d'extraction, feuille par feuille. Basé sur la spec PPT
client d'origine, croisée avec les 3 fichiers source réels et les décisions prises avec le
client. Tous les points auparavant ouverts sont désormais tranchés.*

---

## 0. Contexte

L'application legacy **AvancementRecette** (MVC5, EF6, .NET Framework 4.8) pilote les opérations de **Mise à Disposition (MAD)** et **Remise en Ligne (REL)** des actifs d'OXO. L'extraction du fichier Excel source est confiée à **AM-OXO-ETL** (solution distincte, EF Core / .NET 10 / Web API + Blazor).

OXO fournit un fichier Excel source. AM-OXO-ETL en extrait les données pour produire un **fichier pivot** que AvancementRecette importera pour créer :
- des **Equipement** (`BaseElement`) — les "parents", `TypeElement.Nom` = **`"MAD TRAVAUX"`** (confirmé en base OXO). Cette valeur vient du profil d'import actif (`ImportProfile.EquipementTypeElementNom`), jamais codée en dur dans le moteur d'extraction. Il n'y a pas de dossier REL distinct — les tâches REL sont des `TacheMultiple` extraites du même fichier MAD (feuille PROCEDURE), l'Équipement parent reste toujours `"MAD TRAVAUX"`.
- des **Isolement** (`BaseElement`) — les "enfants", dont le type est lu sur `TypeElement.Nom` (valeurs confirmées en base OXO : `INSTRUMENTATION`, `ZERO ENERGIE`, `SOUPAPE`, `POINT FEU`, `PROLOCK`, `TAMPON PLEIN`, `PLATINE`, `TROU D'HOMME`, `TUYAUTERIE`, `TUBING`, `VANNE MANUELLE` — voir §6 et glossaire. `"VANNE"` observé dans un fichier source n'existe **pas** dans ce référentiel, voir §2).

**Fichier cible réel fourni par le client (`OXO_TRAME_IMPORT_MAD.xlsx`)** : contrairement aux 5 feuilles envisagées initialement, le client a produit un fichier à **2 feuilles seulement** : `Parents` (1 ligne = 1 Equipement) et `Enfants` (1 ligne = 1 Isolement). Chaque `Colonne.Nom` connue (Prolock vannes, Contrôle Etanchéités, PF : signature étiquette...) est une **colonne** du fichier — un `X` signifie "créer le Point pour ce BaseElement sur cette Colonne" — et non une feuille séparée. Les Tâches Multiples ne sont volontairement pas encore couvertes par ce fichier cible (le client n'a pas eu le temps de les préparer). Ces données sont des données de test, non liées aux 3 fichiers source réels — elles valident la **forme** du fichier cible, pas les règles d'extraction bout-en-bout.

**Convention de nommage du fichier cible** : `MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx`, stocké côté AM-OXO-ETL.

**Fichier cible — structure actuelle** :
1. `Parents` — 1 ligne par Equipement, colonnes descriptives (Repère, TypeElement.Nom, Zone/Loc2/Loc3, Designation...) + 1 colonne par `Colonne.Nom` connue (Points). Colonnes descriptives non encore mappées (FLUIDE, RECURRENT, PROGRESS, SUPPRESSION, ADR Email, COMMENTAIRES) conservées dans le schéma, sans règle d'extraction à ce jour.
2. `Enfants` — 1 ligne par Isolement, colonnes descriptives (Numéro, Type, Zone, Élément Parent, Designation, Position à la pose/dépose...) + 1 colonne par `Colonne.Nom` connue (Points). Colonne `"POSITION A LA POSE"` alimentée par `Isolement` source `H20:O21` (voir §2). Colonnes descriptives non encore mappées (PHASE PROCESS, REMARQUES, ETIQUETTE, DIAMETRE INCH, SERIE LBS, NATURE JOINT, BESOIN ECHAF) conservées dans le schéma, sans règle d'extraction à ce jour.
3. *(à venir, non couvert par le fichier réel actuel)* Tâches multiples

**Profil d'import** : l'utilisateur choisit un profil avant import. Le profil paramètre a minima : nom de chaque feuille source, pas de lecture des blocs répétitifs, `TypeElement.Nom` de l'Équipement parent, liste des Colonnes concernées par la création de Points (et leurs conditions éventuelles), et le `TypeValidationTacheMultiple` des tâches multiples à extraire.

---

## 1. Feuille PROCEDURE

*Nom de feuille paramétrable dans le profil d'import.*

### 1.1 En-tête (identification de l'Équipement parent)

| Plage source | Donnée | Type Excel | Règle d'extraction |
|---|---|---|---|
| `M2:O2` | Repère brut | Texte | Retirer un préfixe **paramétrable dans le profil d'import**, valeur par défaut `"MAD-OXO-"` (avec tiret final) |
| `P2:Q2` | Numéro de révision | Texte | |
| `R2:T2` | Date de révision | Date | Utilisée au format `dd/mm/aaaa` dans la composition de `Designation` |

### 1.2 Bloc répétitif — Tâches multiples (à partir de la ligne 9)

| Colonne | Donnée | Type Excel |
|---|---|---|
| `B` (à partir de B9) | `TacheMultiple.Ordre` (voir règle §1.2) | — |
| `C:L` | TacheMultipleAction | Texte |
| `M:N` | TacheMultipleActeur | Texte |
| `O:Q` | TacheMultipleRisques | Texte |
| `R` | Alias MAD/REL pour `TypeTacheMultiple.Code` | Texte |
| `T:U` | `TacheMultiple.DateValidation` | Date |

**Condition d'arrêt** : lecture tant que `C:L` n'est pas `string.IsNullOrWhiteSpace`.

**Règle `TacheMultiple.Ordre` / lignes de présentation** :
- Si la colonne `B` contient une valeur (probablement un entier, au format Excel Texte) → elle est affectée à `TacheMultiple.Ordre`, ligne normale.
- Si `B` est vide → la ligne est une **ligne de mise en page/présentation** du fichier source (pas une vraie tâche). Dans ce cas, une `TacheMultiple` **factice, déjà marquée validée**, est tout de même créée, pour respecter la structure du fichier d'entrée telle que voulue par le client.

*Note de conception : cette convention (ligne sans `Ordre` ⇒ tâche factice pré-validée plutôt que ligne simplement ignorée) est explicitement une demande client assumée, pas une déduction technique.*

### 1.3 Règles métier — Équipement parent

- `BaseElement.Designation` = `"Rév {Numéro de révision} du {Date de révision}"` (date au format `dd/mm/aaaa`)
- `BaseElement.TypeElement.Nom` = valeur du profil d'import actif (`ImportProfile.EquipementTypeElementNom`) — **`"MAD TRAVAUX"`**, toujours cette seule valeur (pas de dossier REL distinct, voir §0). Cette valeur **n'est jamais codée en dur** dans le service d'extraction.
- `BaseElement.Visible = true`
- Associé aux entités `Tableau` : `"TRAVAUX COMPLET"` et `"TRAVAUX DETAIL"`
- Associé à l'entité `Application` : `"AMProgress"`
- Alias `TypeTacheMultiple.Code` (lu en `R9`) : `"MAD"` → `TM_PROC_MAD`, `"REL"` → `TM_PROC_REL` — codes déjà existants côté legacy, pas de création nécessaire. **Valeur distincte** de `TypeElement.Nom` de l'Équipement parent (`"MAD TRAVAUX"`) : les deux champs source portent des valeurs voisines (`"MAD"` vs `"MAD TRAVAUX"`) mais alimentent des cibles différentes — ne pas les fusionner dans le moteur d'extraction. Les tâches `TM_PROC_REL` sont extraites de la même feuille PROCEDURE du même fichier MAD — pas un dossier ou fichier séparé.
- Des `Point` sont créés pour chaque `Colonne` associée aux Tableaux `"TRAVAUX COMPLET"` **et** `"TRAVAUX DETAIL"` (création de Points dans les deux cas, pas seulement une association d'entité).

---

## 2. Feuille ISOLEMENT

*Nom de feuille paramétrable. Contient des Isolements enfants de l'Équipement MAD identifié en PROCEDURE.*

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:T6` | Repère de l'Équipement parent (pour composition du Repère) | Texte |
| `B19:E20` (1er enregistrement) | Identification | Texte |
| `H18:U19` | Désignation | Texte |
| `H20:O21` | Position MAD | Texte — alimente la colonne cible **`"POSITION A LA POSE"`** (fichier `Enfants`), champ pivot `IsolementPivot.PositionALaPose` |
| `B22:E23` | Type d'élément (`TypeElement.Nom`) | Texte |

**Pas de lecture entre blocs : 7**

**Règles métier**
- Repère de l'isolement = `{K6:T6}-{Identification}`
- Arrêt de lecture dès que la cellule Identification (lue par pas de 7) est vide
- Points créés pour **tout** isolement extrait de cette feuille, sans condition sur `TypeElement` : `"PROLOCK VANNES"`, `"DEPROLOCK VANNES"`
- Point créé uniquement si type d'élément = `"ZERO ENERGIE"` : `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"`
- Liste de colonnes paramétrable dans le profil d'import

**Valeurs de `TypeElement` observées sur cette feuille** : les 3 fixtures contiennent 26 éléments,
dont 25 de type `"PROLOCK"` et 1 de type `"VANNE"` (D8570). La condition `"ZERO ENERGIE"` de cette
feuille **n'est déclenchée par aucun fichier connu** — elle reste néanmoins au profil, conformément
à la règle métier ci-dessus, et est couverte en test unitaire.

`"PROLOCK"` est une valeur confirmée en base OXO (voir §6 et glossaire). `"VANNE"` en est
**confirmée absente** — probable typo utilisateur ou confusion avec `VM`/`VANNE MANUELLE`, à la
charge du client de corriger sa saisie si besoin.

Dans les deux cas, le traitement est identique et relève de la politique d'erreur non bloquante
(§3.2 du modèle de domaine) : l'Isolement est extrait normalement, ses Points inconditionnels
(`"PROLOCK VANNES"`, `"DEPROLOCK VANNES"`) sont créés, le Point conditionnel ne l'est pas, et un
avertissement `NoConditionalPointCreated` est ajouté à `ImportResult.Errors` — **une seule entrée
par valeur distincte**, pas une par élément. Aucune règle spécifique à coder pour ces cas.

Le moteur ne distingue pas `"PROLOCK"` de `"VANNE"` et ne le peut pas : il ne connaît que le
profil, jamais le référentiel OXO. La distinction « valeur confirmée / valeur absente » relève de
la lecture humaine du glossaire, pas d'un comportement du code.

**Avertissements `NoConditionalPointCreated` attendus sur les 3 fixtures** (profil seedé, vérifiés
au lot 055) : C7401 → 1 (`ISOLEMENT` / `PROLOCK`) ; D8570 → 2 (`ISOLEMENT` / `PROLOCK`,
`ISOLEMENT` / `VANNE`) ; G6306B → 3 (`ISOLEMENT` / `PROLOCK`, `AUTRES JOINTS TOUCHES` / `TUBING`,
`DIVERS` / `POINT DE FEU`). Les feuilles `PLATINES` et `ORIFICES CAPACITES` n'en produisent jamais :
elles ne portent aucune `ConditionalPointRule`.

---

## 3. Feuille PLATINES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `H16:V17` | Désignation | Texte |
| `H18:N18` | Texte libre | Texte |
| `B20:E22` | Type d'élément | Texte |

**Pas de lecture entre blocs : 8**

*Valeurs réelles de Type d'élément observées dans les 3 fichiers source : `"PLATINE"` et `"TAMPON PLEIN"` (confirmées en base OXO, `Code` respectifs `PT`/`TP`, `Categorie = ISOLEMENTS`).*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés — **variantes `DEBUT` uniquement** : `"POSE ÉTIQUETTES"`, `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`, `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"`

**Variantes DEB/FIN** : la spécification initiale est retenue (`DEBUT` uniquement). Un écart avait été observé dans un fichier cible de test du client (qui ne cochait que `FIN`, jamais `DEB`) — resté sans explication logique, jugé non fiable, sans retour attendu dessus. Les variantes `FIN` restent volontairement exclues du profil.

**Point de conception** : le profil d'import définit, par feuille, la liste des `Colonne.Nom` pour lesquelles créer des Points sans condition (`UnconditionalColonneNames`), et éventuellement une condition par colonne (`ConditionalPointRule`) — voir modèle de domaine §1.4.

---

## 4. Feuille ORIFICES CAPACITES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `H16:V17` | Désignation | Texte |
| `B20:E22` | Type d'élément | Texte |

**Pas de lecture entre blocs : 8**

*Valeur réelle de Type d'élément observée dans les 3 fichiers source : `"TROU D'HOMME"` (seule valeur rencontrée ; confirmée en base OXO, `Code = TH`, `Categorie = ISOLEMENTS`).*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés : `"POSE ÉTIQUETTES"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"` (coquille "PLEIN" au singulier corrigée — même Colonne qu'en feuille PLATINES, pas une variante distincte), `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`

---

## 5. Feuille AUTRES JOINTS TOUCHES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `F16:Y17` | Désignation | Texte |
| `B20:E21` | Type d'élément | Texte |

**Pas de lecture entre blocs : 7** *(Identification lue en `B17:E18`, puis `B24:E25`, puis `B31:E32`, etc.)*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés : `"POSE ÉTIQUETTES"` (si type d'élément ≠ `"TUBING"`), `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`

*Valeurs réelles de Type d'élément observées dans les 3 fichiers source : `"TUYAUTERIE"` (valeur par défaut) et `"TUBING"` (seul cas d'exclusion de `"Pose étiquettes"`, confirmé sur fichier réel).*

---

## 6. Feuille DIVERS

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B6:E6` | Localisation → `BaseElement.Localisation.Loc1.Nom` (`loc1`) | Texte |
| `B9:G11` (1er enregistrement) | Type d'élément | Texte |
| `H9:K11` | Identification | Texte |
| `L9:V11` | Désignation | Texte |

**Pas de lecture entre blocs : 3** *(Identification lue en `H9:K11`, puis `H12:K14`, puis `H15:K17`, etc.)*

**`loc1`** : la valeur lue en `B6:E6` (`BaseElement.Localisation.Loc1.Nom`) est **applicable à tous les Equipement et Isolement extraits du fichier Excel** (portée globale/broadcast, colonne cible `"ZONE"` — confirmé sans exception, y compris malgré un écart `ZONE 4`/`ZONE 3` observé dans un fichier cible de test, jugé non fiable).

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés selon le type d'élément :
  - `"SYNCHRONISATION INSTRUMENTATION"` si type = `"INSTRUMENTATION"`
  - `"ZÉRO ENERGIE EN PRESENCE EE"` si type = `"ZERO ENERGIE"`
  - `"SOUPAPE : CONSTAT ENCRASSEMENT"` si type = `"SOUPAPE"`
  - `"SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"` si type = `"SOUPAPE"`
  - `"PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"` si type = `"POINT FEU"`
  - `"PF : VALIDATION CONSTAT ENCRASSEMENT"` si type = `"POINT FEU"`
  - `"PF : ACCORD TRAVAUX FEU"` si type = `"POINT FEU"`

**Risque de non-correspondance texte** : la cellule réelle du fichier G6306B contient littéralement `"POINT DE FEU"` (avec "DE"), alors que le `TypeElement.Nom` confirmé en base OXO est `"POINT FEU"` (sans "DE"). Valeur retenue par le client : `"POINT FEU"` — tranché, pas de retour en arrière. Si la comparaison est stricte, cet isolement ne déclenchera aucune des 3 conditions PF ci-dessus — pas une erreur bloquante (avertissement non bloquant, Isolement extrait quand même), juste un point de vigilance sur la saisie, sans qu'il soit nécessaire d'y revenir. Une espace en fin de cellule a aussi été observée (`"SOUPAPE "`) — recommandation : `.Trim()` systématique en plus de la comparaison insensible à la casse (§7).

---

## 7. Recommandations transverses (casse et correspondance de valeurs)

Plusieurs noms de `Colonne`/`TypeElement` sont écrits ici en MAJUSCULES, alors que les valeurs confirmées en base (glossaire) sont en casse mixte : `Prolock vannes`, `Contrôle Etanchéités`, `Pose étiquettes`... **Recommandation** : comparaison insensible à la casse **et aux espaces de début/fin (`.Trim()`)** lors de la résolution `Colonne.Nom`/`TypeElement.Nom` dans le moteur d'extraction — une espace de fin a été observée dans une cellule réelle (`"SOUPAPE "`, voir §6).

**Cadrage important** : si malgré tout une correspondance échoue (nom de Colonne/TypeElement introuvable côté legacy), **ce n'est pas un problème pour AM-OXO-ETL**. Le moteur d'import de l'application legacy affiche une erreur explicite dans son résultat d'import lorsqu'il ne trouve pas la Colonne/le TypeElement attendu — le client est alors responsable de corriger ses fichiers Excel source ou ses données de référence en base. Cela s'applique en particulier au type `"VANNE"` (§2), aux variantes `DEB/FIN` de PLATINES (§3), et au risque `"POINT FEU"`/`"POINT DE FEU"` (§6) : ce sont des points de vigilance sur la donnée, pas des points bloquants à résoudre côté ETL.

À ne pas confondre avec l'avertissement `NoConditionalPointCreated` du §3.2 du modèle de domaine :
celui-ci ne présume rien de l'existence d'une valeur en base legacy, il constate seulement qu'aucune
condition du profil n'a matché.

---

## 8. Mécanisme de liaison Équipement parent → Isolements enfants

Le Repère de l'Équipement parent (extrait une fois depuis PROCEDURE) est **porté en mémoire pendant tout le traitement du fichier** (variable de contexte partagée entre les 6 feuilles), et non recherché en base par une requête `Repere`. Cohérent avec le concept de "portée globale"/broadcast (`loc1` en est un autre exemple).

---

## 9. Non couvert / incertain

- Format exact de sortie pour les Tâches multiples (non couvertes par le fichier cible réel actuel) — non détaillé ici.
- Colonnes cibles descriptives non mappées à une règle d'extraction (ZONE hors `loc1`/LOC2/LOC3, FLUIDE, RECURRENT, PROGRESS, SUPPRESSION, ADR Email, COMMENTAIRES côté Parents ; PHASE PROCESS, REMARQUES, ETIQUETTE, DIAMETRE INCH, SERIE LBS, NATURE JOINT, BESOIN ECHAF côté Enfants) — conservées dans le schéma cible en anticipation de futures demandes client, à clarifier au fur et à mesure. Ne bloque que l'écriture du fichier cible (hors périmètre du pipeline d'extraction).
- Comportement attendu en cas d'erreur — précisé dans `modele-domaine-import-profile.md` §3.
