# Glossaire technique — AMAR.ModelCF (legacy EF6)
 
> Extrait de l'audit du modèle EF6 legacy (`audit-ef6-legacy-AMAR-ModelCF-2026-07-15.md`,
> lecture directe du code au 2026-07-15). Document vivant : à compléter au fil des prochains
> échanges (nouvelles feuilles Excel analysées, nouveaux termes client rencontrés), sans avoir
> à retoucher l'audit lui-même.
>
> **Mise à jour 2026-07-16** : ajout des entrées issues de la relecture de la spec client
> retravaillée (`spec-extraction-fichier-source-oxo-2026-07-16.md`). Les nouvelles lignes sont
> marquées **[NOUVEAU 2026-07-16]** — statut non confirmé sauf mention contraire.
 
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
| type élément | `TypeElement` (classe + table, `Code`/`Nom` uniques) | Valeurs = données, non codées en dur |
| type de point | `TypePoint` (classe + table, `Abreviation`/`Nom` uniques) | Idem — données |
| application (module) | `Application` (classe + table) | N-N avec `BaseElement`, `TypeElement`, `TypePoint` |
| visible | `BaseElement.Visible`, `TypeElement.Visible`, `Categorie.Visible`, `Colonne.Visible` | bool, défaut `true` sur `BaseElement` |
| Groupe de BE | `BaseElementCommun` (classe + table, `Nom` unique) | 1-N vers `BaseElement`, flag `IsGroupSignature` |
| Profil d'import Excel | `ProfilXLSX` (classe + table) | Existant, entité/feuille/lignes d'en-tête |
| Colonne de mapping d'import | `ColonneXLSX` (classe + table) | Position/titre/propriété/type/clé/requis/unique |
| TRAVAUX COMPLET / TRAVAUX DETAIL | `Tableau.Nom` (valeurs de données, projet OXO) | **Confirmé par le client (2026-07-15)** — ce sont des noms de `Tableau`, pas de code |
| Prolock vannes | `Colonne.Nom` = "Prolock vannes" | **Confirmé** — correspondance exacte avec le PPT |
| Déprolock vannes | `Colonne.Nom` = "Déprolock vannes" | **Confirmé** — PPT disait "DEPROLOCK VANNES" |
| Zéro énergie en présence EE (PS941) | `Colonne.Nom` = "Zéro énergie en présence EE (PS941)" | **Confirmé** |
| Pose étiquettes | `Colonne.Nom` = "Pose étiquettes" | **Confirmé** |
| Réceptions Assemblages : boulonnés (PS938) ou tubings | `Colonne.Nom` = idem | **Confirmé** |
| Contrôle Etanchéités | `Colonne.Nom` = "Contrôle Etanchéités" | **Confirmé** |
| Réceptions Platines/Tampons pleins (4 variantes DEB/FIN × MAD/REL) | `Colonne.Nom` : "FIN REL Platines / Tampons pleins", "DEB REL Platines / Tampons pleins", "FIN MAD Réception Platines/Tampons pleins" (×2 dans la liste fournie — doublon probable, un `DEB MAD ...` manque vraisemblablement) | ⚠️ **Toujours non résolu au 2026-07-16** — la spec client retravaillée (voir fichier du 2026-07-16) ne liste que les variantes `"DEBUT"` (MAD/REL), sans les variantes `FIN` pourtant présentes en base réelle. Laquelle des 4 valeurs réelles correspond à quelle règle reste à trancher avec le client |
| INSTRUMENTATION | `TypeElement.Nom` = "INSTRUMENTATION" | **Confirmé** |
| ZERO ENERGIE | `TypeElement.Nom` = "ZERO ENERGIE" | **Confirmé** |
| SOUPAPE | `TypeElement.Nom` = "SOUPAPE" | **Confirmé** |
| POINT FEU (PPT) / POINT DE FEU (BDD) | `TypeElement.Nom` = "POINT DE FEU" | ⚠️ **Divergence mineure, toujours présente au 2026-07-16** — la spec client retravaillée du 2026-07-16 utilise encore l'orthographe PPT "POINT FEU" dans la feuille DIVERS. Utiliser la valeur BDD (`"POINT DE FEU"`) dans le moteur d'extraction |
| MAD (comme `TypeElement.Nom` de l'Équipement parent) **[NOUVEAU 2026-07-16]** | `TypeElement.Nom` = "MAD" ? | ⚠️ **Non confirmé** — n'apparaît pas dans les valeurs de `TypeElement.Nom` déjà validées par l'audit EF6. Introduit par la spec client retravaillée du 2026-07-16 ("Equipement : BaseElement de TypeElement.Nom «MAD»"). À vérifier en base réelle avant de figer le profil d'import |
| TM_PROC_MAD **[NOUVEAU 2026-07-16]** | `TypeTacheMultiple.Code` = "TM_PROC_MAD" ? | ⚠️ **Non confirmé** — l'audit EF6 n'avait trouvé aucune trace littérale de "TM PROCEDURE MAD". Alias introduit par la spec du 2026-07-16 pour les tâches multiples de la feuille PROCEDURE liées à l'alias "MAD". À créer ou confirmer en base |
| TM_PROC_REL **[NOUVEAU 2026-07-16]** | `TypeTacheMultiple.Code` = "TM_PROC_REL" ? | ⚠️ **Non confirmé** — idem, pour l'alias "REL" |
| loc1 (statut) **[NOUVEAU 2026-07-16]** | — | ⚠️ L'anomalie de la synthèse §5.4 (`DIVERS!C6` vide dans les 3 fichiers réels) restait à trancher avec le client. La spec client retravaillée du 2026-07-16 ne mentionne plus `loc1` du tout — à confirmer explicitement si le concept est abandonné ou simplement omis dans la reformulation |