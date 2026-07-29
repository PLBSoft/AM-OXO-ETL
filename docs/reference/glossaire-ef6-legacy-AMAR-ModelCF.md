# Glossaire technique — AMAR.ModelCF (legacy EF6)

> Document vivant, état courant des correspondances terme métier ↔ code, à jour des décisions
> prises avec le client. Basé sur l'audit du modèle EF6 legacy (lecture directe du code) et sur
> les échanges successifs de clarification. L'historique des décisions (qui a tranché quoi et
> quand) vit dans les échanges avec l'assistant et dans git, pas dans ce document — ce fichier
> ne reflète que l'état actuel.

---

## Table complète des `TypeElement` — base OXO (source de vérité)

| `Code` | `Nom` | Statut / usage connu dans le pipeline AM-OXO-ETL |
|---|---|---|
| `MAD` | **MAD TRAVAUX** | `TypeElement.Nom` de l'Équipement parent pour un dossier MAD (confirmé). Paramétrable dans le profil d'import (`ImportProfile.EquipementTypeElementNom`), jamais codé en dur. C'est la seule valeur utilisée pour ce champ — il n'y a pas de dossier REL distinct (les tâches REL sont extraites du même fichier MAD, voir plus bas). |
| `PROLOCK` | PROLOCK | Isolement, feuille ISOLEMENT. Cas dégénéré Code=Nom. Déclenche `PROLOCK VANNES`/`DEPROLOCK VANNES` — **inconditionnel pour tout isolement de la feuille**, pas seulement `TypeElement = "PROLOCK"`. |
| `TP` | TAMPON PLEIN | Isolement, feuille PLATINES. `Categorie = ISOLEMENTS`. |
| `PT` | PLATINE | Isolement, feuille PLATINES. `Categorie = ISOLEMENTS`. |
| `TH` | TROU D'HOMME | Isolement, feuille ORIFICES CAPACITES. `Categorie = ISOLEMENTS`. |
| `J` | JOINT | Existe en base — non observé dans les 3 fichiers source réels (valeur par défaut confirmée = `TUYAUTERIE`, pas `JOINT`). |
| `TB` | TUBING | Isolement, feuille AUTRES JOINTS TOUCHES — seul cas d'exclusion de `"Pose étiquettes"`. |
| `TUY` | TUYAUTERIE | Isolement, feuille AUTRES JOINTS TOUCHES — valeur par défaut confirmée sur les 3 fichiers réels. |
| `VM` | VANNE MANUELLE | Existe en base. Probable confusion avec le `"VANNE"` observé (typo) dans le fichier D8570, feuille ISOLEMENT. |
| `INSTRUMENTATION` | INSTRUMENTATION | Isolement, feuille DIVERS. Déclenche `"SYNCHRONISATION INSTRUMENTATION"`. |
| `ZERO ENERGIE` | ZERO ENERGIE | Isolement, feuilles ISOLEMENT et DIVERS. Déclenche `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` (ISOLEMENT, seule condition de cette feuille) et `"ZÉRO ENERGIE EN PRESENCE EE"` (DIVERS). |
| `SOUPAPE` | SOUPAPE | Isolement, feuille DIVERS. Déclenche 2 Points (`"CONSTAT ENCRASSEMENT"`, `"RÉCEPTION REPOSE..."`). |
| `PF` | POINT FEU | Isolement, feuille DIVERS. Déclenche 3 Points (`"PF : ..."`). Valeur retenue malgré l'écart `"POINT DE FEU"` observé en cellule réelle. |
| `CIRC` | Circuit | Hors périmètre ETL. |
| `SEQUENCE_SECURITE` | Séquence de sécurité | Hors périmètre ETL. |
| `Item LUT` | ItemLUT | Hors périmètre ETL. |
| `Item LUT Induit` | ItemLUTInduit | Hors périmètre ETL. |
| `Modele_TM` | ModeleTM | Hors périmètre ETL (lié à `TypeTacheMultiple`, à ne pas confondre). |
| `TRANSMETTEUR` | TRANSMETTEUR | Existe en base — non observé dans les 3 fichiers source réels, non couvert par la spec actuelle. |

**Note** : cette table est la référence unique pour les valeurs `TypeElement.Nom`. `"VANNE"`
(observé dans le fichier D8570) **n'y figure volontairement pas** : confirmé absent du
référentiel OXO, probable typo utilisateur ou confusion avec `VM`/`VANNE MANUELLE`. Traité par
la politique d'erreur non bloquante (Isolement extrait normalement, aucun Point créé,
avertissement non bloquant) — aucune action corrective côté ETL.

---

## Table principale (correspondance terme métier ↔ code)

| Terme métier (client) | Nom réel dans le code (classe/table/champ) | Notes |
|---|---|---|
| BE (Base Élément) | `BaseElement` (classe + table `dbo.BaseElement`) | Une seule classe/table pour tous les BE (TPH), pas de distinction schéma parent/enfant |
| BE parent | *(aucun nom dédié — même classe `BaseElement`)* | Rôle porté par la collection `BaseElement.ParentSet` dans la relation N-N auto-référencée |
| BE enfant | *(aucun nom dédié — même classe `BaseElement`)* | Rôle porté par `BaseElement.ChildSet` ; table de jointure `dbo.BaseElementBaseElement` |
| MAD (Mise à Disposition) | `Isolement.PositionALaMiseADisposition` / `PositionALaMiseADispositionId` (FK vers `PositionPoseDepose`) | Non trouvé comme entité de tête ; existait comme booléen `BaseElement.CorrespondanceLOTOMAD` avant suppression en 2021 (migration `RemoveLOTOMADAndLOTOREL`) |
| REL (Remise en Ligne) | *(non trouvé dans le code actuel)* | Existait comme `BaseElement.CorrespondanceLOTOREL` (booléen), supprimé en 2021. Confirmé : il n'y a pas de fichier Excel de dossier REL distinct — les tâches REL sont des `TacheMultiple` extraites du même fichier MAD, feuille PROCEDURE (voir `TM_PROC_REL` ci-dessous) |
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
| Profil d'import Excel (legacy) | `ProfilXLSX` (classe + table) | POC legacy, distinct du nouveau `ImportProfile` (AM-OXO-ETL) |
| Colonne de mapping d'import (legacy) | `ColonneXLSX` (classe + table) | Position/titre/propriété/type/clé/requis/unique |
| TRAVAUX COMPLET / TRAVAUX DETAIL | `Tableau.Nom` (valeurs de données, projet OXO) | Confirmé par le client — ce sont des noms de `Tableau`, pas de code |
| Prolock vannes | `Colonne.Nom` = "Prolock vannes" | Créé pour **tout** isolement extrait de la feuille ISOLEMENT, sans condition sur `TypeElement` |
| Déprolock vannes | `Colonne.Nom` = "Déprolock vannes" | Idem, inconditionnel |
| Zéro énergie en présence EE (PS941) | `Colonne.Nom` = "Zéro énergie en présence EE (PS941)" | Seule condition de la feuille ISOLEMENT (`TypeElement = "ZERO ENERGIE"`) |
| Pose étiquettes | `Colonne.Nom` = "Pose étiquettes" | Confirmé |
| Réceptions Assemblages : boulonnés (PS938) ou tubings | `Colonne.Nom` = idem | Confirmé |
| Contrôle Etanchéités | `Colonne.Nom` = "Contrôle Etanchéités" | Confirmé |
| Réceptions Platines/Tampons pleins (variantes DEB/FIN × MAD/REL) | `Colonne.Nom` : `"RECEPTION DEBUT MAD"`, `"RÉCEPTION PLATINES/TAMPONS PLEINS"`, `"RECEPTION DEBUT REL"`, `"PLATINES / TAMPONS PLEINS"` | Tranché — variantes `DEBUT` uniquement (spécification initiale). L'écart observé dans un fichier cible de test (qui ne cochait que `FIN`) reste sans explication, jugé non fiable. Les variantes `FIN` restent volontairement exclues, sans retour attendu sur ce point. |
| INSTRUMENTATION | `TypeElement.Nom` = "INSTRUMENTATION" | Confirmé |
| ZERO ENERGIE | `TypeElement.Nom` = "ZERO ENERGIE" | Confirmé |
| SOUPAPE | `TypeElement.Nom` = "SOUPAPE" | Confirmé |
| POINT FEU (PPT et décision client) / POINT DE FEU (valeur trouvée dans l'audit EF6) | `TypeElement.Nom` = "POINT FEU" (valeur retenue par le client) | Tranché — le client utilise `"POINT FEU"` (orthographe PPT), pas `"POINT DE FEU"` (valeur relevée par l'audit EF6). Si cela ne correspond à rien en base réelle, le moteur d'import legacy affichera une erreur exploitable par le client — non bloquant pour AM-OXO-ETL, inutile d'y revenir. |
| MAD (comme `TypeElement.Nom` de l'Équipement parent) | `TypeElement.Nom = "MAD TRAVAUX"` (`Code = "MAD"`) | Confirmé en base OXO. Valeur paramétrable dans le profil d'import (`ImportProfile.EquipementTypeElementNom`), jamais codée en dur dans le moteur d'extraction. Seule valeur utilisée — il n'y a pas de valeur REL distincte à chercher pour ce champ (voir entrée REL ci-dessus). |
| VANNE (feuille ISOLEMENT, fichier D8570) | *(n'existe pas en base OXO)* | Absent de la liste `TypeElement` OXO (19 valeurs vérifiées, voir table complète). Probable typo utilisateur ou confusion avec `VM`/`VANNE MANUELLE`. Traité par la politique d'erreur non bloquante (§3.2 du modèle de domaine) : Isolement extrait, Points inconditionnels créés, Point conditionnel non créé, avertissement `NoConditionalPointCreated` — une seule entrée par valeur distincte. Pas d'action corrective côté ETL. **Le moteur ne détecte pas cette absence du référentiel** et ne l'affirme pas : il constate seulement qu'aucune condition du profil n'a matché. Le même avertissement est produit pour `PROLOCK`, valeur pourtant confirmée en base. |
| TM_PROC_MAD | `TypeTacheMultiple.Code` = "TM_PROC_MAD" | Confirmé, déjà existant côté legacy. Distinct de `TypeElement.Nom` de l'Équipement parent (`"MAD TRAVAUX"`) — ne pas confondre les deux champs source (`R9` vs `M2:O2`). |
| TM_PROC_REL | `TypeTacheMultiple.Code` = "TM_PROC_REL" | Confirmé, déjà existant côté legacy. Extrait de la même feuille PROCEDURE du même fichier MAD que TM_PROC_MAD — pas un fichier séparé. |
| loc1 | `BaseElement.Localisation.Loc1.Nom` | Lu en feuille `DIVERS`, cellule `B6:E6`. Portée globale confirmée : applicable à tous les Equipement et Isolement extraits du fichier, sans exception (malgré un écart `ZONE 4`/`ZONE 3` observé dans un fichier cible de test, jugé non fiable). |
| ZONE (colonne cible, `Parents`/`Enfants`) | = `loc1` | Confirmé. |
| Position MAD (source, Isolement `H20:O21`) → colonne cible | `"POSITION A LA POSE"` | Confirmé — alimente cette colonne cible dans `Parents`/`Enfants`. Le champ legacy visé est `Isolement.PositionALaPose`, pas `PositionALaMiseADisposition`. |
| TAMPON PLEIN | `TypeElement.Nom = "TAMPON PLEIN"`, `Code = "TP"`, `Categorie = ISOLEMENTS` | Confirmé en base OXO. Valeur réelle observée dans les 3 fichiers source (feuille PLATINES) |
| PLATINE | `TypeElement.Nom = "PLATINE"`, `Code = "PT"`, `Categorie = ISOLEMENTS` | Confirmé en base OXO. Valeur réelle observée dans les 3 fichiers source (feuille PLATINES) |
| TROU D'HOMME | `TypeElement.Nom = "TROU D'HOMME"`, `Code = "TH"`, `Categorie = ISOLEMENTS` | Confirmé en base OXO. Seule valeur observée dans les 3 fichiers source pour la feuille ORIFICES CAPACITES |
| VANNE (le vrai type existant, à ne pas confondre avec la typo `"VANNE"` d'ISOLEMENT) | `TypeElement.Nom = "VANNE MANUELLE"`, `Code = "VM"` | Existe en base OXO — probable origine de la confusion du fichier D8570. |
| TUYAUTERIE | `TypeElement.Nom = "TUYAUTERIE"`, `Code = "TUY"` | Confirmé par les 3 fichiers réels — valeur par défaut de la feuille AUTRES JOINTS TOUCHES. Déclenche `"Pose étiquettes"` (règle `≠ TUBING`) |
| TUBING (feuille AUTRES JOINTS TOUCHES) | `TypeElement.Nom = "TUBING"`, `Code = "TB"` | Confirmé par le fichier réel G6306B — seul cas où `"Pose étiquettes"` n'est pas créé |
| POINT DE FEU (cellule réelle) vs POINT FEU (base OXO confirmée) | Cellule réelle (G6306B, feuille DIVERS) = `"POINT DE FEU"` (avec "DE") ; `TypeElement.Nom` confirmé en base = `"POINT FEU"` (sans "DE") | ⚠️ Risque de non-correspondance texte/référentiel — si la comparaison est stricte, cet Isolement ne matchera aucune `ConditionalPointRule` (pas d'erreur bloquante, juste un avertissement et aucun Point PF créé). Signalé au client, non bloquant, pas de retour attendu dessus. |
| OXO_TRAME_IMPORT_MAD.xlsx (fichier cible réel fourni par le client) | 2 feuilles : `Parents` (1 ligne = 1 Equipement) et `Enfants` (nommée en interne `_x0009_Enfants`, avec un caractère tabulation parasite en préfixe — à corriger côté client) | Chaque `Colonne.Nom` connue est une **colonne** du fichier (`X` = créer le Point), pas une feuille séparée. `TABLEAUX` porte `Tableau.Code` (`TRX_COMP`/`TRX_DET`, ≤8 car., distinct de `Tableau.Nom`). Données de test non liées aux 3 fichiers source réels — ne permet pas de valider les règles d'extraction, seulement la forme du fichier cible |
| UnconditionalColonneNames (`SheetExtractionRule`) | `IReadOnlyList<string>`, requis non-null (peut être vide) | Champ du modèle Domain regroupant les `Colonne.Nom` créés sans condition pour toute feuille (ex. `"PROLOCK VANNES"`/`"DEPROLOCK VANNES"` d'ISOLEMENT), distinct des `ConditionalPointRule` qui portent une condition. Apparu pendant l'implémentation du Lot C, pas anticipé dans la conception initiale — confirmé cohérent avec le modèle. |
