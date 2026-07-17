# Glossaire technique — AMAR.ModelCF (legacy EF6)

> Extrait de l'audit du modèle EF6 legacy (`audit-ef6-legacy-AMAR-ModelCF-2026-07-15.md`,
> lecture directe du code au 2026-07-15). Document vivant : à compléter au fil des prochains
> échanges (nouvelles feuilles Excel analysées, nouveaux termes client rencontrés), sans avoir
> à retoucher l'audit lui-même.

**Mise à jour 2026-07-16 (v6)** — suite aux réponses du client sur les priorités 1/2/3 :
- **Table complète des `TypeElement` (Code;Nom) extraite de la base OXO au 16/07/2026** ajoutée
  ci-dessous (19 valeurs), remplace les extraits partiels des versions précédentes.
- **Correction de la v5** : `TypeElement.Nom` de l'Équipement parent MAD n'est **pas** `"MAD"`
  (hypothèse dégénérée Code=Nom, infirmée) mais **`"MAD TRAVAUX"`** (`Code = MAD`,
  `Nom = "MAD TRAVAUX"`) — confirmé par la liste base OXO. **Précision** : ce n'est pas un point
  de correction pour AM-OXO-ETL — cette valeur est déjà paramétrable dans le profil d'import
  (`ImportProfile.EquipementTypeElementNom`, voir `modele-domaine-import-profile-2026-07-16.md`
  v2). C'est au client de configurer son profil avec la bonne valeur ; le moteur d'extraction ne
  la code pas en dur, il se contente d'écrire ce que le profil lui indique.
- **`"VANNE"`** (observé feuille ISOLEMENT, fichier D8570) : confirmé **absent** de la base
  OXO (probable typo/confusion avec `VM`/`VANNE MANUELLE`, ou donnée à créer/corriger côté
  client). Traité par la politique d'erreur non bloquante déjà actée — pas de règle de Point
  spécifique à créer, pas d'action côté ETL.
- **`PROLOCK VANNES`/`DEPROLOCK VANNES`** : confirmé **inconditionnels** pour tout Isolement
  de la feuille ISOLEMENT (pas de condition sur `TypeElement = "PROLOCK"`).
- **Variantes DEB/FIN de PLATINES** : on revient à la spécification initiale (`DEBUT`
  uniquement) — l'écart observé dans le fichier cible réel reste sans explication et n'est
  pas traité comme fiable.
- **Nouveau mapping documenté** : `Isolement` (source, feuille ISOLEMENT, `H20:O21`,
  "Position MAD") alimente la colonne cible **`"POSITION A LA POSE"`** — confirme que le
  champ visé côté legacy est `Isolement.PositionALaPose`, pas `PositionALaMiseADisposition`.
- **`ZONE`** (colonne cible, feuilles `Parents`/`Enfants`) = `loc1` (confirmé).
- L'écart `ZONE 4`/`ZONE 3` (parent vs enfants) observé dans `OXO_TRAME_IMPORT_MAD.xlsx` est
  jugé non fiable (fichier de test déconnecté des fichiers source) — la portée globale de
  `loc1` est **conservée sans exception**.

**Mise à jour 2026-07-16 (v5)** : `loc1` est résolu (feuille DIVERS, `B6:E6`, portée globale) ;
source et cible utilisent `TypeElement.Nom` partout (annule la v3 qui posait `TypeElement.Code
= "MAD"`). Confirmé en base OXO (`Categorie = ISOLEMENTS` pour les 4) : `TP`↔`TAMPON PLEIN`,
`PT`↔`PLATINE`, `TH`↔`TROU D'HOMME`, `PF`↔`POINT FEU`. Nouveau type `"VANNE"` observé en
feuille ISOLEMENT (fichier réel D8570) — aucune règle de Point définie pour ce cas (voir v6,
tranché). Valeur réelle par défaut d'AUTRES JOINTS TOUCHES : `"TUYAUTERIE"` (pas `"JOINT"`),
exclusion confirmée sur `"TUBING"`. Risque de non-correspondance texte relevé : `"POINT DE
FEU"` (avec "DE") trouvé littéralement dans une cellule réelle (G6306B) alors que le Nom
confirmé en base est `"POINT FEU"`. La question `FIN`/`DEBUT` de PLATINES **n'est en fait pas
tranchée à cette date** — voir v6 pour le tranchage final.

**Mise à jour 2026-07-16 (v4)** : le cas symétrique "REL" n'est pas hors scope — même
mécanisme que MAD (`TypeElement.Code`, révisé en `.Nom` par la v5), simplement aucun fichier
Excel exemple de dossier REL n'est disponible pour l'instant. Confirmé : création de Points
pour `"TRAVAUX DETAIL"` comme pour `"TRAVAUX COMPLET"`.

**Mise à jour 2026-07-16 (v3)** : `MAD` correspondait initialement au champ `TypeElement.Code`
(hypothèse annulée par la v5, qui standardise sur `.Nom`). Les codes `TM_PROC_MAD`/
`TM_PROC_REL` sont confirmés déjà existants côté legacy. Les variantes `FIN` de PLATINES sont
volontairement exclues (confirmé à nouveau en v6 après une réouverture en v5). `"POINT FEU"`
(pas `"POINT DE FEU"`) est la valeur retenue par le client.

**Mise à jour 2026-07-16 (v2)** : ajout des entrées issues de la relecture de la spec client
retravaillée (`spec-extraction-fichier-source-oxo-2026-07-16.md`).

---

## Table complète des `TypeElement` — base OXO au 2026-07-16 (v6, source de vérité)

| `Code` | `Nom` | Statut / usage connu dans le pipeline AM-OXO-ETL |
|---|---|---|
| `MAD` | **MAD TRAVAUX** | `TypeElement.Nom` de l'Équipement parent pour un dossier MAD (confirmé). Paramétrable dans le profil d'import (`ImportProfile.EquipementTypeElementNom`), jamais codé en dur. |
| `PROLOCK` | PROLOCK | Isolement, feuille ISOLEMENT. Cas dégénéré Code=Nom (celui-ci confirmé). Déclenche `PROLOCK VANNES`/`DEPROLOCK VANNES` — **inconditionnel pour tout isolement de la feuille**, pas seulement `TypeElement = "PROLOCK"`. |
| `TP` | TAMPON PLEIN | Isolement, feuille PLATINES. `Categorie = ISOLEMENTS`. |
| `PT` | PLATINE | Isolement, feuille PLATINES. `Categorie = ISOLEMENTS`. |
| `TH` | TROU D'HOMME | Isolement, feuille ORIFICES CAPACITES. `Categorie = ISOLEMENTS`. |
| `J` | JOINT | Existe en base — **non observé** dans les 3 fichiers source réels (valeur par défaut confirmée = `TUYAUTERIE`, pas `JOINT`). Feuille AUTRES JOINTS TOUCHES probable mais non vu en pratique. |
| `TB` | TUBING | Isolement, feuille AUTRES JOINTS TOUCHES — seul cas d'exclusion de `"Pose étiquettes"`. |
| `TUY` | TUYAUTERIE | Isolement, feuille AUTRES JOINTS TOUCHES — valeur par défaut confirmée sur les 3 fichiers réels. |
| `VM` | VANNE MANUELLE | Existe en base. Probable confusion avec le `"VANNE"` observé (typo) dans le fichier D8570, feuille ISOLEMENT — à vérifier au cas par cas si le client corrige ses fichiers source. |
| `INSTRUMENTATION` | INSTRUMENTATION | Isolement, feuille DIVERS. Déclenche `"SYNCHRONISATION INSTRUMENTATION"`. |
| `ZERO ENERGIE` | ZERO ENERGIE | Isolement, feuilles ISOLEMENT et DIVERS. Déclenche `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` (ISOLEMENT, seule condition de cette feuille) et `"ZÉRO ENERGIE EN PRESENCE EE"` (DIVERS). |
| `SOUPAPE` | SOUPAPE | Isolement, feuille DIVERS. Déclenche 2 Points (`"CONSTAT ENCRASSEMENT"`, `"RÉCEPTION REPOSE..."`). |
| `PF` | POINT FEU | Isolement, feuille DIVERS. Déclenche 3 Points (`"PF : ..."`). Valeur retenue malgré l'écart `"POINT DE FEU"` observé en cellule réelle. |
| `CIRC` | Circuit | Hors périmètre ETL — module non concerné. |
| `SEQUENCE_SECURITE` | Séquence de sécurité | Hors périmètre ETL. |
| `Item LUT` | ItemLUT | Hors périmètre ETL. |
| `Item LUT Induit` | ItemLUTInduit | Hors périmètre ETL. |
| `Modele_TM` | ModeleTM | Hors périmètre ETL (probablement lié à `TypeTacheMultiple`, à ne pas confondre). |
| `TRANSMETTEUR` | TRANSMETTEUR | Existe en base — non observé dans les 3 fichiers source réels, non couvert par la spec actuelle. |

**Note transverse** : cette table est la référence unique pour les valeurs `TypeElement.Nom` — elle remplace les mentions éparses dans l'audit EF6 et les versions précédentes de ce glossaire/de la spec en cas de divergence. `"VANNE"` (observé dans le fichier D8570) **n'apparaît volontairement pas** dans cette table : il est confirmé absent du référentiel OXO.

---

## Table principale (correspondance terme métier ↔ code)

| Terme métier (client) | Nom réel dans le code (classe/table/champ) | Notes |
|---|---|---|
| BE (Base Élément) | `BaseElement` (classe + table `dbo.BaseElement`) | Une seule classe/table pour tous les BE (TPH), pas de distinction schéma parent/enfant |
| BE parent | *(aucun nom dédié — même classe `BaseElement`)* | Rôle porté par la collection `BaseElement.ParentSet` dans la relation N-N auto-référencée |
| BE enfant | *(aucun nom dédié — même classe `BaseElement`)* | Rôle porté par `BaseElement.ChildSet` ; table de jointure `dbo.BaseElementBaseElement` |
| MAD (Mise à Disposition) | `Isolement.PositionALaMiseADisposition` / `PositionALaMiseADispositionId` (FK vers `PositionPoseDepose`) | **Non trouvé** comme entité de tête ; existait comme booléen `BaseElement.CorrespondanceLOTOMAD` avant suppression en 2021 (migration `RemoveLOTOMADAndLOTOREL`) |
| REL (Remise en Ligne) | *(non trouvé dans le code actuel)* | Existait comme `BaseElement.CorrespondanceLOTOREL` (booléen), supprimé en 2021 ; aucun équivalent actuel identifié |
| TM (Tâche Multiple) | `TacheMultiple` (classe + table) | Instance rattachée à un `BaseElement` et à un `TypeTacheMultiple` |
| PTM (type de tâche multiple) | `TypeTacheMultiple` (classe + table) | Porte `Code`, `Nom`, `TypeValidation` (enum) |
| Item de TM (donnée d'une TM) | `ItemTacheMultiple` | Définit un champ (`Type`, `Cle`, `Position`...) d'un `TypeTacheMultiple` |
| Valeur d'un item de TM | `DataTacheMultiple` (+ 6 sous-classes TPH) | Une ligne par (TacheMultiple, ItemTacheMultiple) |
| Point (au sens "cellule de suivi") | `Point` (classe + table) | Couple unique (BaseElementId, ColonneId) |
| PointSingulier | `PointSingulier` (sous-classe de `BaseElement`, PAS de `Point`) | ⚠️ Faux-ami : c'est un type de BE (contrôle non destructif/soudure), pas une instance de `Point` |
| Colonne (étape de workflow) | `Colonne` (classe + table) | À ne pas confondre avec `ColonneXLSX` (mapping Excel) ni `ColonneTableau` |
| Tableau (plan de suivi) | `Tableau` (classe + table) | `Code` (≤8 car.), `Nom`, tous deux uniques |
| repère | `BaseElement.Repere` (`[Required]`) + `NewRepere` (optionnel) | Champ "tag" ; utilisé comme clé de recherche métier dans le code d'import existant (`FindByRepere`) |
| désignation | `BaseElement.Designation` | string libre |
| type élément | `TypeElement` (classe + table, `Code`/`Nom` uniques) | Valeurs = données, non codées en dur. Voir table complète ci-dessus. |
| type de point | `TypePoint` (classe + table, `Abreviation`/`Nom` uniques) | Idem — données |
| application (module) | `Application` (classe + table) | N-N avec `BaseElement`, `TypeElement`, `TypePoint` |
| visible | `BaseElement.Visible`, `TypeElement.Visible`, `Categorie.Visible`, `Colonne.Visible` | bool, défaut `true` sur `BaseElement` |
| Groupe de BE | `BaseElementCommun` (classe + table, `Nom` unique) | 1-N vers `BaseElement`, flag `IsGroupSignature` |
| Profil d'import Excel | `ProfilXLSX` (classe + table) | Existant, entité/feuille/lignes d'en-tête |
| Colonne de mapping d'import | `ColonneXLSX` (classe + table) | Position/titre/propriété/type/clé/requis/unique |
| TRAVAUX COMPLET / TRAVAUX DETAIL | `Tableau.Nom` (valeurs de données, projet OXO) | **Confirmé par le client (2026-07-15)** — ce sont des noms de `Tableau`, pas de code |
| Prolock vannes | `Colonne.Nom` = "Prolock vannes" | **Confirmé** — correspondance exacte avec le PPT. Créé pour **tout** isolement extrait de la feuille ISOLEMENT, sans condition sur `TypeElement` (confirmé 2026-07-16, v6) |
| Déprolock vannes | `Colonne.Nom` = "Déprolock vannes" | **Confirmé** — PPT disait "DEPROLOCK VANNES". Idem, inconditionnel (v6) |
| Zéro énergie en présence EE (PS941) | `Colonne.Nom` = "Zéro énergie en présence EE (PS941)" | **Confirmé** — seule condition de la feuille ISOLEMENT (`TypeElement = "ZERO ENERGIE"`) |
| Pose étiquettes | `Colonne.Nom` = "Pose étiquettes" | **Confirmé** |
| Réceptions Assemblages : boulonnés (PS938) ou tubings | `Colonne.Nom` = idem | **Confirmé** |
| Contrôle Etanchéités | `Colonne.Nom` = "Contrôle Etanchéités" | **Confirmé** |
| Réceptions Platines/Tampons pleins (variantes DEB/FIN × MAD/REL) | `Colonne.Nom` : `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"` | ✅ **Tranché (2026-07-16, v6)** — retour à la spécification initiale (`DEBUT` uniquement). L'écart observé dans le fichier cible réel (qui ne coche que `FIN`) reste sans explication, jugé non fiable. Les variantes `FIN` restent volontairement exclues. |
| INSTRUMENTATION | `TypeElement.Nom` = "INSTRUMENTATION" | **Confirmé** |
| ZERO ENERGIE | `TypeElement.Nom` = "ZERO ENERGIE" | **Confirmé** |
| SOUPAPE | `TypeElement.Nom` = "SOUPAPE" | **Confirmé** |
| POINT FEU (PPT et décision client) / POINT DE FEU (valeur trouvée dans l'audit EF6) | `TypeElement.Nom` = "POINT FEU" (valeur retenue par le client) | **Tranché** — le client choisit d'utiliser `"POINT FEU"` (orthographe PPT), pas `"POINT DE FEU"` (valeur relevée par l'audit EF6). Si cela ne correspond à rien en base réelle, le moteur d'import legacy affichera une erreur exploitable par le client — ce n'est pas un point bloquant pour AM-OXO-ETL. **Inutile d'y revenir (confirmé v6).** |
| MAD (comme `TypeElement.Nom` de l'Équipement parent) | `TypeElement.Nom = "MAD TRAVAUX"` (`Code = "MAD"`) | ✅ **Confirmé (v6)** — remplace l'hypothèse `"MAD"` posée en v5. Valeur paramétrable dans le profil d'import (`ImportProfile.EquipementTypeElementNom`), jamais codée en dur dans le moteur d'extraction — c'est au client de configurer correctement son profil. |
| VANNE (feuille ISOLEMENT, fichier D8570) | *(n'existe pas en base OXO)* | ✅ **Tranché (v6)** — absent de la liste `TypeElement` OXO (19 valeurs vérifiées, voir table complète). Probable typo utilisateur ou confusion avec `VM`/`VANNE MANUELLE`. Traité par la politique d'erreur non bloquante déjà actée (§3.2 du modèle de domaine) : Isolement extrait, aucun Point créé, avertissement non bloquant. Pas d'action corrective côté ETL. |
| TM_PROC_MAD | `TypeTacheMultiple.Code` = "TM_PROC_MAD" | **Confirmé** — déjà existant côté legacy, pas de création nécessaire. Distinct de `TypeElement.Nom` de l'Équipement parent (`"MAD TRAVAUX"`) — ne pas confondre les deux champs source (`R9` vs `M2:O2`). |
| TM_PROC_REL | `TypeTacheMultiple.Code` = "TM_PROC_REL" | **Confirmé** — idem, déjà existant côté legacy |
| loc1 | `BaseElement.Localisation.Loc1.Nom` | **Résolu** — lu en feuille `DIVERS`, cellule `B6:E6`. Portée globale confirmée : applicable à tous les Equipement et Isolement extraits du fichier. **Confirmé sans exception (v6)** malgré l'écart `ZONE 4`/`ZONE 3` observé dans un fichier cible de test, jugé non fiable. |
| ZONE (colonne cible, `Parents`/`Enfants`) | = `loc1` | ✅ **Confirmé (v6)**. |
| Position MAD (source, Isolement `H20:O21`) → colonne cible | `"POSITION A LA POSE"` | ✅ **Confirmé (v6)** — la donnée source alimente cette colonne cible dans `Parents`/`Enfants`. Confirme que le champ legacy visé est `Isolement.PositionALaPose`, pas `PositionALaMiseADisposition`. |
| TAMPON PLEIN | `TypeElement.Nom = "TAMPON PLEIN"`, `Code = "TP"`, `Categorie = ISOLEMENTS` | **Confirmé en base OXO.** Valeur réelle observée dans les 3 fichiers source (feuille PLATINES) |
| PLATINE | `TypeElement.Nom = "PLATINE"`, `Code = "PT"`, `Categorie = ISOLEMENTS` | **Confirmé en base OXO.** Valeur réelle observée dans les 3 fichiers source (feuille PLATINES) |
| TROU D'HOMME | `TypeElement.Nom = "TROU D'HOMME"`, `Code = "TH"`, `Categorie = ISOLEMENTS` | **Confirmé en base OXO.** Seule valeur observée dans les 3 fichiers source pour la feuille ORIFICES CAPACITES |
| VANNE (le vrai type existant, à ne pas confondre avec la typo `"VANNE"` d'ISOLEMENT) | `TypeElement.Nom = "VANNE MANUELLE"`, `Code = "VM"` | Existe en base OXO (voir table complète) — probable origine de la confusion du fichier D8570. |
| TUYAUTERIE | `TypeElement.Nom = "TUYAUTERIE"`, `Code = "TUY"` | **Confirmé par les 3 fichiers réels** — c'est la valeur par défaut de la feuille AUTRES JOINTS TOUCHES (corrige l'hypothèse antérieure `"JOINT"`, jamais observée). Déclenche `"Pose étiquettes"` (règle `≠ TUBING`) |
| TUBING (feuille AUTRES JOINTS TOUCHES) | `TypeElement.Nom = "TUBING"`, `Code = "TB"` | **Confirmé par le fichier réel G6306B** — seul cas où `"Pose étiquettes"` n'est pas créé |
| POINT DE FEU (cellule réelle) vs POINT FEU (base OXO confirmée) | Cellule réelle (G6306B, feuille DIVERS) = `"POINT DE FEU"` (avec "DE") ; `TypeElement.Nom` confirmé en base = `"POINT FEU"` (sans "DE") | ⚠️ **Risque de non-correspondance texte/référentiel** — si la comparaison est stricte, cet Isolement ne matchera aucune `ConditionalPointRule` (pas d'erreur bloquante grâce à la politique déjà actée, juste un avertissement et aucun Point PF créé). Signalé au client, non bloquant, pas de retour attendu dessus. |
| OXO_TRAME_IMPORT_MAD.xlsx (fichier cible réel fourni par le client) | 2 feuilles : `Parents` (1 ligne = 1 Equipement) et `Enfants` (nommée en interne `_x0009_Enfants`, avec un caractère tabulation parasite en préfixe — à corriger côté client) | Chaque `Colonne.Nom` connue est une **colonne** du fichier (`X` = créer le Point), pas une feuille séparée. `TABLEAUX` porte `Tableau.Code` (`TRX_COMP`/`TRX_DET`, ≤8 car., distinct de `Tableau.Nom`). Données de test non liées aux 3 fichiers source réels — ne permet pas de valider les règles d'extraction, seulement la forme du fichier cible |
