# Spécification d'extraction — Fichier source OXO (feuille par feuille)

*Reformatage et relecture du 2026-07-16 d'une spec PPT client retravaillée par l'utilisateur. Basé sur une relecture croisée avec `etat-des-lieux-technique.md`, `ALPHA-OXO-ETL-EXCEL-synthese.md` (notamment l'analyse structurelle des 3 fichiers réels, section 5) et `audit-ef6-legacy-AMAR-ModelCF-2026-07-15.md` / son glossaire.*

*Statut : la plupart des points ⚠️ sont désormais tranchés (voir v6 ci-dessous) — seul le cas REL (aucun fichier exemple disponible) reste en attente, sans bloquer le développement.*

**Mise à jour 2026-07-16 (v6)** — réponses du client sur les priorités 1 et 2 :
- **Type `"VANNE"`** (feuille ISOLEMENT) : confirmé absent de la base OXO. Traité par la politique d'erreur non bloquante déjà actée — pas de règle de Point spécifique, pas d'action côté ETL.
- **`"PROLOCK VANNES"`/`"DEPROLOCK VANNES"`** : confirmé **inconditionnels** pour tout Isolement de la feuille ISOLEMENT (pas de condition sur `TypeElement = "PROLOCK"`).
- **Variantes DEB/FIN de PLATINES** : tranché — retour à la spécification initiale (`DEBUT` uniquement). L'écart du fichier cible réel reste sans explication et n'est pas traité comme fiable.
- **`TypeElement.Nom` de l'Équipement parent MAD** : confirmé en base OXO = **`"MAD TRAVAUX"`** (`Code = "MAD"`), pas `"MAD"` comme précédemment supposé. Cette valeur est paramétrable dans le profil d'import (`ImportProfile.EquipementTypeElementNom`, voir `modele-domaine-import-profile-2026-07-16.md` v2) — ce n'est pas une constante à coder en dur dans le moteur.
- **Nouveau mapping documenté** : `Isolement` (source, feuille ISOLEMENT, `H20:O21`, "Position MAD") alimente la colonne cible **`"POSITION A LA POSE"`** du fichier `Enfants` — confirme que le champ legacy visé est `Isolement.PositionALaPose`.
- **`ZONE`** (colonne cible, feuilles `Parents`/`Enfants`) = `loc1`, confirmé.
- L'écart `ZONE 4`/`ZONE 3` (parent vs enfants) observé dans `OXO_TRAME_IMPORT_MAD.xlsx` est jugé non fiable (fichier de test déconnecté des fichiers source) — la portée globale de `loc1` est conservée **sans exception**.
- **Colonnes cibles non mappées** (ZONE/LOC2/LOC3 hors ZONE, FLUIDE, RECURRENT, PROGRESS, SUPPRESSION, ADR Email, COMMENTAIRES côté Parents ; PHASE PROCESS, REMARQUES, ETIQUETTE, DIAMETRE INCH, SERIE LBS, NATURE JOINT, BESOIN ECHAF côté Enfants) : conservées dans le schéma cible, sans règle d'extraction à ce jour — le client anticipe des extensions futures non encore spécifiées.

**Mise à jour 2026-07-16 (v5)** — suite à l'inspection du fichier cible réel `OXO_TRAME_IMPORT_MAD.xlsx` fourni par le client, croisée avec les 3 fichiers source réels :
- **Décision de simplification qui annule la v3** : source et cible utilisent finalement `TypeElement.Nom` **partout**, y compris pour l'Équipement parent (pas `Code`). Une table de traduction Code↔Nom pourra être ajoutée plus tard si le legacy l'exige (Code et Nom sont tous deux uniques côté OXO), sans impact sur le moteur d'extraction.
- Valeur réelle par défaut d'AUTRES JOINTS TOUCHES : `"TUYAUTERIE"` (pas `"JOINT"`), voir §5.
- Fichier cible réel : 2 feuilles seulement (`Parents`/`Enfants`), les Points sont des **colonnes** (pas des feuilles séparées) — voir §0.

**Mise à jour 2026-07-16 (v2)** — confirmations et corrections apportées par l'utilisateur suite à la première relecture :
- Pas de lecture AUTRES JOINTS TOUCHES (`7`) et DIVERS (`3`) : confirmés, avec exemples concrets.
- `loc1` : réintroduit (feuille DIVERS, `B6:E6`), portée globale confirmée.
- Préfixe Repère : paramétrable, valeur par défaut `"MAD-OXO-"`.
- `TacheMultiple.Ordre` : règle métier précisée (voir 1.2).
- Doublon `"PLATINES / TAMPONS PLEIN(S)"` : confirmé comme coquille, pas une variante distincte en base.

---

## 0. Contexte

L'application legacy **AvancementRecette** (MVC5, EF6, .NET Framework 4.8) pilote les opérations de **Mise à Disposition (MAD)** et **Remise en Ligne (REL)** des actifs d'OXO. L'extraction du fichier Excel source est confiée à **AM-OXO-ETL** (solution distincte, EF Core / .NET 10 / Web API + Blazor).

OXO fournit un fichier Excel source. AM-OXO-ETL en extrait les données pour produire un **fichier pivot** que AvancementRecette importera pour créer :
- des **Equipement** (`BaseElement`) — les "parents", `TypeElement.Nom` = **`"MAD TRAVAUX"`** pour un dossier MAD (confirmé en base OXO, 2026-07-16 — remplace l'hypothèse `"MAD"` posée en v5). Cette valeur vient du profil d'import actif (`ImportProfile.EquipementTypeElementNom`), jamais codée en dur dans le moteur d'extraction. Pour un dossier REL, la valeur correspondante reste à confirmer (aucun fichier REL disponible à ce jour).
- des **Isolement** (`BaseElement`) — les "enfants", dont le type est lu sur `TypeElement.Nom` (valeurs confirmées en base OXO : `INSTRUMENTATION`, `ZERO ENERGIE`, `SOUPAPE`, `POINT FEU`, `PROLOCK`, `TAMPON PLEIN`, `PLATINE`, `TROU D'HOMME`, `TUYAUTERIE`, `TUBING`, `VANNE MANUELLE` — voir §6 et glossaire. `"VANNE"` observé dans un fichier source n'existe **pas** dans ce référentiel, voir §2).

**Fichier cible réel fourni par le client (`OXO_TRAME_IMPORT_MAD.xlsx`, 2026-07-16)** : contrairement aux 5 feuilles envisagées initialement, le client a produit un fichier à **2 feuilles seulement** : `Parents` (1 ligne = 1 Equipement) et `Enfants` (1 ligne = 1 Isolement). Chaque `Colonne.Nom` connue (Prolock vannes, Contrôle Etanchéités, PF : signature étiquette...) est une **colonne** du fichier — un `X` signifie "créer le Point pour ce BaseElement sur cette Colonne" — et non une feuille séparée. Les Tâches Multiples ne sont volontairement pas encore couvertes (le client n'a pas eu le temps de les préparer). Ces données sont des données de test, non liées aux 3 fichiers source réels — elles valident la **forme** du fichier cible, pas les règles d'extraction bout-en-bout.

**Convention de nommage du fichier cible** : `MAD_{Equipement.Nom}_{AAAAMMDDHHmmss}.xlsx`, stocké côté AM-OXO-ETL.

**Fichier cible — structure à date (2026-07-16, mise à jour selon le fichier réel)** :
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

*Note de conception : cette convention (ligne sans `Ordre` ⇒ tâche factice pré-validée plutôt que ligne simplement ignorée) est explicitement une demande client assumée, pas une déduction technique — à documenter comme telle dans le profil d'import pour que la prochaine personne qui lit le code ne la prenne pas pour un bug.*

### 1.3 Règles métier — Équipement parent

- `BaseElement.Designation` = `"Rév {Numéro de révision} du {Date de révision}"` (date au format `dd/mm/aaaa`)
- `BaseElement.TypeElement.Nom` = valeur du profil d'import actif (`ImportProfile.EquipementTypeElementNom`) — **`"MAD TRAVAUX"`** pour un dossier MAD (confirmé en base OXO), valeur REL encore à confirmer. Cette valeur **n'est jamais codée en dur** dans le service d'extraction.
- `BaseElement.Visible = true`
- Associé aux entités `Tableau` : `"TRAVAUX COMPLET"` et `"TRAVAUX DETAIL"`
- Associé à l'entité `Application` : `"AMProgress"`
- Alias `TypeTacheMultiple.Code` (lu en `R9`) : `"MAD"` → `TM_PROC_MAD`, `"REL"` → `TM_PROC_REL` — codes déjà existants côté legacy (confirmé, pas de création nécessaire). **Valeur distincte** de `TypeElement.Nom` de l'Équipement parent (`"MAD TRAVAUX"`) : les deux champs source portent des valeurs voisines (`"MAD"` vs `"MAD TRAVAUX"`) mais alimentent des cibles différentes — ne pas les fusionner dans le moteur d'extraction.
- Des `Point` sont créés pour chaque `Colonne` associée aux Tableaux `"TRAVAUX COMPLET"` **et** `"TRAVAUX DETAIL"` (confirmé — création de Points dans les deux cas, pas seulement une association d'entité).

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

**Pas de lecture entre blocs : 7** ✓ *cohérent avec l'analyse structurelle réelle (synthèse §5.1 : blocs de 7 lignes pour ISOLEMENT).*

**Règles métier**
- Repère de l'isolement = `{K6:T6}-{Identification}`
- Arrêt de lecture dès que la cellule Identification (lue par pas de 7) est vide
- Points créés pour **tout** isolement extrait de cette feuille, sans condition sur `TypeElement` (confirmé 2026-07-16) : `"PROLOCK VANNES"`, `"DEPROLOCK VANNES"`
- Point créé uniquement si type d'élément = `"ZERO ENERGIE"` : `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"`
- Liste de colonnes paramétrable dans le profil d'import

**Type `"VANNE"` — tranché (2026-07-16)** : le fichier réel D8570 contient des isolements de type `"VANNE"`, une valeur **confirmée absente** du référentiel `TypeElement` de la base OXO (voir liste complète, glossaire). Probable typo utilisateur ou confusion avec `VM`/`VANNE MANUELLE` — à la charge du client de corriger sa saisie si besoin. Traité par la politique d'erreur non bloquante déjà actée (§3.2 du modèle de domaine) : l'Isolement est extrait normalement, aucun Point n'est créé (aucune règle ne matche), un avertissement non bloquant est ajouté à `ImportResult.Errors`. Aucune règle spécifique à coder pour ce cas.

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

*Valeurs réelles de Type d'élément observées dans les 3 fichiers source : `"PLATINE"` et `"TAMPON PLEIN"` (confirmées en base OXO, `Code` respectifs `PT`/`TP`, `Categorie = ISOLEMENTS`).*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés — **variantes `DEBUT` uniquement, tranché (2026-07-16, retour à la spécification initiale)** : `"POSE ÉTIQUETTES"`, `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`, `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"`

**Variantes DEB/FIN — tranché (2026-07-16)** : par défaut, on conserve la spécification initiale (`DEBUT` uniquement). L'écart observé dans le fichier cible réel du client (qui ne coche que `FIN`, jamais `DEB`) reste sans explication logique — le client lui-même l'attribue possiblement à une erreur de saisie dans son fichier de test ou à une expression de besoin imprécise, sans certitude. Les variantes `FIN` restent volontairement exclues du profil. Si le fichier cible produit par AM-OXO-ETL ne correspond finalement pas à ce qu'attend réellement le legacy, ce sera visible via une erreur d'import legacy (non bloquant pour AM-OXO-ETL, cf. §7).

**Point de conception confirmé à cette occasion** : le profil d'import définit, par feuille, la liste des `Colonne.Nom` pour lesquelles créer des Points, et éventuellement une condition par colonne (`ConditionalPointRule`) — ce mécanisme, déjà modélisé dans `modele-domaine-import-profile-2026-07-16.md` (§1.4, §2.1), couvre exactement ce besoin sans évolution du catalogue de primitives.

---

## 4. Feuille ORIFICES CAPACITES

| Plage source | Donnée | Type Excel |
|---|---|---|
| `K6:U6` | Repère de l'Équipement parent | Texte |
| `B17:E18` (1er enregistrement) | Identification | Texte |
| `H16:V17` | Désignation | Texte |
| `B20:E22` | Type d'élément | Texte |

**Pas de lecture entre blocs : 8** ✓ *cohérent (synthèse §5.1 : 8 lignes pour ORIFICES CAPACITES).*

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

**Pas de lecture entre blocs : 7** ✓ *confirmé par le client — Identification lue en `B17:E18`, puis `B24:E25`, puis `B31:E32`, etc. (contredit l'analyse structurelle de la synthèse §5.1 qui avait estimé 3 lignes/bloc à partir des 3 fichiers échantillon fournis — le pas réel de 7 fait foi ici et corrige cette estimation antérieure.)*

**Règles métier**
- Repère de l'isolement = `{Equipement.Repere}-{Identification}`
- Arrêt dès que Identification est vide
- Points créés : `"POSE ÉTIQUETTES"` (si type d'élément ≠ `"TUBING"`), `"RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"`, `"CONTRÔLE ETANCHÉITÉS"`

*Valeurs réelles de Type d'élément observées dans les 3 fichiers source : `"TUYAUTERIE"` (valeur par défaut — corrige une hypothèse antérieure erronée `"JOINT"`, jamais observée) et `"TUBING"` (seul cas d'exclusion de `"Pose étiquettes"`, confirmé sur fichier réel).*

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

**`loc1`** : la valeur lue en `B6:E6` (`BaseElement.Localisation.Loc1.Nom`) est **applicable à tous les Equipement et Isolement extraits du fichier Excel** (portée globale/broadcast, colonne cible `"ZONE"` — confirmé sans exception, y compris malgré l'écart `ZONE 4`/`ZONE 3` observé dans le fichier cible de test, jugé non fiable).

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

**Risque de non-correspondance texte** : la cellule réelle du fichier G6306B contient littéralement `"POINT DE FEU"` (avec "DE"), alors que le `TypeElement.Nom` confirmé en base OXO est `"POINT FEU"` (sans "DE"). Valeur retenue par le client : `"POINT FEU"` — tranché, pas de retour en arrière. Si la comparaison est stricte, cet isolement ne déclenchera aucune des 3 conditions PF ci-dessus — pas une erreur bloquante (politique §3.2 du modèle de domaine : avertissement non bloquant, Isolement extrait quand même), juste un point de vigilance sur la saisie, à signaler au client à l'occasion, sans qu'il soit nécessaire d'y revenir. Une espace en fin de cellule a aussi été observée (`"SOUPAPE "`) — recommandation : `.Trim()` systématique en plus de la comparaison insensible à la casse déjà recommandée (§7).

---

## 7. Recommandations transverses (casse et correspondance de valeurs)

Plusieurs noms de `Colonne`/`TypeElement` sont écrits ici en MAJUSCULES, alors que les valeurs confirmées en base (glossaire) sont en casse mixte : `Prolock vannes`, `Contrôle Etanchéités`, `Pose étiquettes`... **Recommandation** : comparaison insensible à la casse **et aux espaces de début/fin (`.Trim()`)** lors de la résolution `Colonne.Nom`/`TypeElement.Nom` dans le moteur d'extraction — une espace de fin a été observée dans une cellule réelle (`"SOUPAPE "`, voir §6).

**Cadrage important (2026-07-16)** : si malgré tout une correspondance échoue (nom de Colonne/TypeElement introuvable côté legacy), **ce n'est pas un problème pour AM-OXO-ETL**. Le moteur d'import de l'application legacy affiche une erreur explicite dans son résultat d'import lorsqu'il ne trouve pas la Colonne/le TypeElement attendu — le client est alors responsable de corriger ses fichiers Excel source ou ses données de référence en base. Cela s'applique en particulier au type `"VANNE"` (§2), aux variantes `DEB/FIN` de PLATINES (§3), et au risque `"POINT FEU"`/`"POINT DE FEU"` (§6) : ce sont des points de vigilance sur la donnée, pas des points bloquants à résoudre côté ETL.

---

## 8. Mécanisme de liaison Équipement parent → Isolements enfants

Non explicité dans la spec d'origine : à préciser que le Repère de l'Équipement parent (extrait une fois depuis PROCEDURE) est **porté en mémoire pendant tout le traitement du fichier** (variable de contexte partagée entre les 6 feuilles), et non recherché en base par une requête `Repere`. C'est cohérent avec le concept de "portée globale"/broadcast déjà identifié dans la synthèse ALPHA-OXO-ETL (§4, `loc1` en était l'exemple).

---

## 9. Questions ouvertes — statut au 2026-07-16 (v6)

1. ~~Variantes DEB/FIN de PLATINES~~ — **Tranché (v6)** : retour à `DEBUT` uniquement, voir §3.
2. ~~Type `"VANNE"`~~ — **Tranché (v6)** : absent de la base OXO, traité par la politique d'erreur non bloquante ; `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"` confirmés inconditionnels.
3. ~~Valeur `TypeElement.Nom` de l'Équipement parent pour un dossier MAD~~ — **Tranché (v6)** : `"MAD TRAVAUX"` (confirmé en base OXO), remplace l'hypothèse `"MAD"`. Paramétrable dans le profil (`ImportProfile.EquipementTypeElementNom`), jamais codé en dur.
4. **Risque `"POINT FEU"` vs `"POINT DE FEU"`** : non bloquant, `"POINT FEU"` conservé — inutile de revenir dessus (voir §6).
5. ~~Mapping "Position MAD" (source) → colonne cible~~ — **Tranché (v6)** : `H20:O21` alimente `"POSITION A LA POSE"`, voir §2.

Point de vigilance résiduel, non bloquant : le jour où un premier fichier Excel de dossier REL sera disponible, il faudra vérifier que sa structure (feuilles, plages de cellules, pas de lecture) suit bien les mêmes conventions que les fichiers MAD analysés — rien ne le garantit a priori tant qu'aucun exemple n'a été inspecté. Mis en veille tant que non demandé explicitement par le client.

---

## 10. Non couvert / incertain

- Format exact de sortie pour les Tâches multiples (non couvertes par le fichier cible réel actuel) — non détaillé ici.
- Colonnes cibles descriptives non mappées à une règle d'extraction (ZONE hors `loc1`/LOC2/LOC3, FLUIDE, RECURRENT, PROGRESS, SUPPRESSION, ADR Email, COMMENTAIRES côté Parents ; PHASE PROCESS, REMARQUES, ETIQUETTE, DIAMETRE INCH, SERIE LBS, NATURE JOINT, BESOIN ECHAF côté Enfants) — conservées dans le schéma cible en anticipation de futures demandes client, à clarifier au fur et à mesure. Ne bloque que le lot E (écriture cible).
- Comportement attendu en cas d'erreur (type d'élément non reconnu par aucune condition de Points, date illisible, cellule requise vide en dehors du cas d'arrêt de bloc) — largement précisé depuis dans `modele-domaine-import-profile-2026-07-16.md` §3.
- Correspondance exacte entre le catalogue de primitives du modèle pivot (`ImportProfile`/`SheetExtractionRule`, voir `modele-domaine-import-profile-2026-07-16.md`) et les règles décrites ici — à faire lors du découpage en tickets (voir `tickets-tdd-extraction-2026-07-16.md`).
- Structure d'un fichier Excel de dossier REL — aucun exemple disponible à ce jour, mis en veille.
