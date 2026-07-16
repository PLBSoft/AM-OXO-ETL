# Spécification d'extraction — Fichier source OXO (feuille par feuille)

*Reformatage et relecture du 2026-07-16 d'une spec PPT client retravaillée par l'utilisateur. Basé sur une relecture croisée avec `etat-des-lieux-technique.md`, `ALPHA-OXO-ETL-EXCEL-synthese.md` (notamment l'analyse structurelle des 3 fichiers réels, section 5) et `audit-ef6-legacy-AMAR-ModelCF-2026-07-15.md` / son glossaire.*

*Statut : brouillon de travail — plusieurs points ⚠️ restent à trancher avant transmission à Claude Code (voir section 9).*

**Mise à jour 2026-07-16 (v2)** — confirmations et corrections apportées par l'utilisateur suite à la première relecture :
- Pas de lecture AUTRES JOINTS TOUCHES (`7`) et DIVERS (`3`) : confirmés, avec exemples concrets.
- `loc1` : réintroduit (feuille DIVERS, `B6:E6`), portée globale confirmée.
- Préfixe Repère : paramétrable, valeur par défaut `"MAD-OXO-"`.
- `TypeElement.Nom` "MAD" de l'Équipement parent : ~~alias paramétrable~~ **correction v3 : c'est `TypeElement.Code` (valeur littérale, pas d'alias) — voir mise à jour v3.**
- `TacheMultiple.Ordre` : règle métier précisée (voir 1.2).
- Doublon `"PLATINES / TAMPONS PLEIN(S)"` : confirmé comme coquille, pas une variante distincte en base.

---

## 0. Contexte

L'application legacy **AvancementRecette** (MVC5, EF6, .NET Framework 4.8) pilote les opérations de **Mise à Disposition (MAD)** et **Remise en Ligne (REL)** des actifs d'OXO. L'extraction du fichier Excel source est confiée à **AM-OXO-ETL** (solution distincte, EF Core / .NET 10 / Web API + Blazor).

OXO fournit un fichier Excel source. AM-OXO-ETL en extrait les données pour produire un **fichier pivot** que AvancementRecette importera pour créer :
- des **Equipement** (`BaseElement`) — les "parents", `TypeElement.Code` = valeur littérale (`"MAD"` ou `"REL"` selon le dossier traité — même mécanisme dans les deux cas, ce n'est pas un cas particulier). *Aucun fichier Excel exemple de dossier REL n'est disponible pour l'instant, mais le principe retenu est qu'on utilisera toujours `TypeElement.Code` (jamais `Nom`) pour cette valeur, quel que soit MAD ou REL.*
- des **Isolement** (`BaseElement`) — les "enfants", dont le type est lu sur `TypeElement.Nom` (valeurs données : `INSTRUMENTATION`, `ZERO ENERGIE`, `SOUPAPE`, `POINT FEU`... — voir §6). *Noter que ce n'est pas le même champ que pour l'Équipement parent (`Code` vs `Nom`) — à ne pas confondre lors de l'implémentation.*

**Convention de nommage du fichier cible** : `MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx`, stocké côté AM-OXO-ETL.

**Fichier cible — 5 feuilles connues** :
1. Equipement MAD Parent
2. Isolements enfants
3. Points parent
4. Points enfants
5. Tâches multiples

**Profil d'import** : l'utilisateur choisit un profil avant import. Le profil paramètre a minima : nom de chaque feuille source, pas de lecture des blocs répétitifs, liste des Colonnes concernées par la création de Points, et le `TypeValidationTacheMultiple` des tâches multiples à extraire.

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

*Note de conception : cette convention (ligne sans `Ordre` ⇒ tâche factice pré-validée plutôt que ligne simplement ignorée) est explicitement une demande client assumée, pas une déduction technique — à documenter comme telle dans le profil d'import pour que la prochaine personne qui lit le code ne la prenne pas pour un bug.*

### 1.3 Règles métier — Équipement parent

- `BaseElement.Designation` = `"Rév {Numéro de révision} du {Date de révision}"` (date au format `dd/mm/aaaa`)
- `BaseElement.Visible = true`
- Associé aux entités `Tableau` : `"TRAVAUX COMPLET"` et `"TRAVAUX DETAIL"`
- Associé à l'entité `Application` : `"AMProgress"`
- Alias `TypeTacheMultiple.Code` : `"MAD"` → `TM_PROC_MAD`, `"REL"` → `TM_PROC_REL` — **codes déjà existants côté legacy** (confirmé, pas de création nécessaire).
- Des `Point` sont créés pour chaque `Colonne` associée aux Tableaux `"TRAVAUX COMPLET"` **et** `"TRAVAUX DETAIL"` (confirmé — création de Points dans les deux cas, pas seulement une association d'entité).

---

## 2. Feuille ISOLEMENT

*Nom de feuille paramétrable. Contient des Isolements enfants de l'Équipement MAD identifié en PROCEDURE.*

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:T6` | Repère de l'Équipement parent (pour composition du Repère) | Texte |
| `B19:E20` (1er enregistrement) | Identification | Texte |
| `H18:U19` | Désignation | Texte |
| `H20:O21` | Position MAD | Texte |
| `B22:E23` | Type d'élément (`TypeElement.Nom`) | Texte |

**Pas de lecture entre blocs : 7** ✓ *cohérent avec l'analyse structurelle réelle (synthèse §5.1 : blocs de 7 lignes pour ISOLEMENT).*

**Règles métier**
- Repère de l'isolement = `{K6:T6}-{Identification}`
- Arrêt de lecture dès que la cellule Identification (lue par pas de 7) est vide
- Points créés pour chaque isolement extrait, sur les Colonnes : `"PROLOCK VANNES"`, `"DEPROLOCK VANNES"`, `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` (si type d'élément = `"ZERO ENERGIE"`)
- Liste de colonnes paramétrable dans le profil d'import

---

## 3. Feuille PLATINES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `H16:V17` | Désignation | Texte |
| `H18:N18` | Texte libre | Texte |
| `B20:E22` | Type d'élément | Texte |

**Pas de lecture entre blocs : 8** ✓ *cohérent (synthèse §5.1 : 8 lignes pour PLATINES).*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés : `"POSE ÉTIQUETTES"`, `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`, `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"`

✓ **Variantes DEB/FIN — clarifié (2026-07-16)** : les variantes `FIN` (`"FIN REL Platines / Tampons pleins"`, `"FIN MAD Réception Platines/Tampons pleins"`...) sont **volontairement exclues** de cette spec, seules les variantes `"DEBUT"` sont couvertes. Si le moteur d'import legacy ne trouve pas une Colonne par son nom, il affiche une erreur dans son résultat d'import — c'est un problème côté client (à lui de corriger ses fichiers ou sa base), pas un problème pour AM-OXO-ETL.

---

## 4. Feuille ORIFICES CAPACITES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `H16:V17` | Désignation | Texte |
| `B20:E22` | Type d'élément | Texte |

**Pas de lecture entre blocs : 8** ✓ *cohérent (synthèse §5.1 : 8 lignes pour ORIFICES CAPACITES).*

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

**Pas de lecture entre blocs : 7** ✓ *confirmé par le client — Identification lue en `B17:E18`, puis `B24:E25`, puis `B31:E32`, etc. (contredit l'analyse structurelle de la synthèse §5.1 qui avait estimé 3 lignes/bloc à partir des 3 fichiers échantillon fournis — le pas réel de 7 fait foi ici et corrige cette estimation antérieure.)*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés : `"POSE ÉTIQUETTES"` (si type d'élément ≠ `"TUBING"`), `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`

---

## 6. Feuille DIVERS

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B6:E6` | Localisation → `BaseElement.Localisation.Loc1.Nom` (`loc1`) | Texte |
| `B9:G11` (1er enregistrement) | Type d'élément | Texte |
| `H9:K11` | Identification | Texte |
| `L9:V11` | Désignation | Texte |

**Pas de lecture entre blocs : 3** ✓ *confirmé par le client — Identification lue en `H9:K11`, puis `H12:K14`, puis `H15:K17`, etc.*

**`loc1`** : réintroduit dans cette version. La valeur lue en `B6:E6` (`BaseElement.Localisation.Loc1.Nom`) est **applicable à tous les Equipement et Isolement extraits du fichier Excel** (portée globale/broadcast — cohérent avec le concept déjà identifié dans la synthèse ALPHA-OXO-ETL §4).

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

---

## 7. Recommandations transverses (casse et correspondance de valeurs)

Plusieurs noms de `Colonne`/`TypeElement` sont écrits ici en MAJUSCULES, alors que les valeurs confirmées en base (glossaire) sont en casse mixte : `Prolock vannes`, `Contrôle Etanchéités`, `Pose étiquettes`... **Recommandation** : comparaison insensible à la casse lors de la résolution `Colonne.Nom`/`TypeElement.Nom` dans le moteur d'extraction, pour limiter les échecs de correspondance évitables.

**Cadrage important (2026-07-16)** : si malgré tout une correspondance échoue (nom de Colonne/TypeElement introuvable côté legacy), **ce n'est pas un problème pour AM-OXO-ETL**. Le moteur d'import de l'application legacy affiche une erreur explicite dans son résultat d'import lorsqu'il ne trouve pas la Colonne/le TypeElement attendu — le client est alors responsable de corriger ses fichiers Excel source ou ses données de référence en base. Cela s'applique en particulier à `"POINT FEU"` (voir ci-dessous) et aux variantes `DEB/FIN` de PLATINES (§3) : ce sont des décisions client assumées, pas des points bloquants à résoudre côté ETL.

`"POINT FEU"` (et non `"POINT DE FEU"`) est la valeur retenue par le client — voir glossaire mis à jour.

---

## 8. Mécanisme de liaison Équipement parent → Isolements enfants

Non explicité dans la spec d'origine : à préciser que le Repère de l'Équipement parent (extrait une fois depuis PROCEDURE) est **porté en mémoire pendant tout le traitement du fichier** (variable de contexte partagée entre les 6 feuilles), et non recherché en base par une requête `Repere`. C'est cohérent avec le concept de "portée globale"/broadcast déjà identifié dans la synthèse ALPHA-OXO-ETL (§4, `loc1` en était l'exemple).

---

## 9. Questions ouvertes à trancher avec le client

*Mise à jour 2026-07-16 (v4) : toutes les questions précédemment ouvertes sont désormais résolues.*
- **Cas symétrique "REL"** : pas hors scope — même mécanisme que MAD (`TypeElement.Code`), pas de fichier exemple disponible pour l'instant mais le principe est acquis (voir §0).
- **Points TRAVAUX DETAIL** : confirmé, création de Points comme pour TRAVAUX COMPLET (voir §1.3).

Aucune question bloquante ne subsiste à ce stade sur la base des éléments fournis. Point de vigilance résiduel, non bloquant : le jour où un premier fichier Excel de dossier REL sera disponible, il faudra vérifier que sa structure (feuilles, plages de cellules, pas de lecture) suit bien les mêmes conventions que les fichiers MAD analysés — rien ne le garantit a priori tant qu'aucun exemple n'a été inspecté.

---

## 10. Non couvert / incertain

- Format exact de sortie pour les feuilles "Points parent", "Points enfants" et "Tâches multiples" du fichier pivot (structure de colonnes cible) — non détaillé ici.
- Comportement attendu en cas d'erreur (type d'élément non reconnu par aucune condition de Points, date illisible, cellule requise vide en dehors du cas d'arrêt de bloc) — à spécifier.
- Correspondance exacte entre le catalogue de primitives du modèle pivot (`ImportProfile`/`SheetExtractionRule`, voir `ALPHA-OXO-ETL-EXCEL-synthese.md` §4) et les règles décrites ici — à faire lors de la prochaine étape de modélisation.
