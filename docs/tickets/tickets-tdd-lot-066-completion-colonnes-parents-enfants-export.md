# Tickets TDD — Lot 066 : complétion des colonnes cibles `Parents`/`Enfants` (profil d'export par défaut)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : comparaison du fichier d'exemple client `OXO_TRAME_IMPORT_MAD.xlsx` (inspiration
non littérale, cf. décision ci-dessous) avec un export réel produit par le pipeline actuel
(`Dossier_de_MaD_IDL_-_C7401_export.xlsx`) contre la fixture C7401. Plusieurs écarts de colonnes
ont été identifiés et tranchés avec le client/product owner. Ce lot les corrige tous, à
l'exception explicite de ceux mis hors périmètre ci-dessous.

**Rappel non-négociable** : `OXO_TRAME_IMPORT_MAD.xlsx` n'est **pas** une source de vérité — le
client l'a manipulé manuellement, il ne doit jamais être pris à la lettre. Chaque écart traité
dans ce lot a été explicitement validé, indépendamment de ce fichier. Ne rien déduire ou
généraliser depuis ce fichier au-delà de ce qui est explicitement décrit ci-dessous.

**Non-négociable général du projet** : ne pas rouvrir de décision déjà actée dans les documents
vivants existants. Si un point n'est pas explicitement listé comme ouvert dans ce ticket, le
traiter comme tranché.

---

**Correctif décidé en cours d'implémentation (66.0)** : la décision 4 ci-dessous ("`ZÉRO ENERGIE EN
PRESENCE EE` sans `(PS941)` = doublon accidentel, ne correspond à aucune extraction réelle") s'est
avérée factuellement fausse à l'investigation — la feuille DIVERS produit réellement cette Colonne
(sans suffixe) via son propre `ConditionalPointRule` (`TypeElement == "ZERO ENERGIE"`), distinct du
mécanisme d'ISOLEMENT (`HasZeroEnergie` → `"...(PS941)"`). Le fixture D8570 en produit réellement 13
occurrences (confirmé par `DiversExtractionServiceIntegrationTests`). Retirer purement et simplement
cette colonne du profil d'export aurait fait perdre silencieusement ces points sur `Enfants`. Soumis
au product owner, qui a tranché : **fusionner les deux colonnes**. Réalisé en retargetant la règle
DIVERS elle-même (`ConditionalPointRule` "ZERO ENERGIE") sur le nom de Colonne d'ISOLEMENT
(`"ZÉRO ENERGIE EN PRESENCE EE (PS941)"`) — les deux sheets convergent désormais vers la même
Colonne réelle, et un seul `PointColumnDefinition` (déjà existant) suffit côté export. Voir le
commentaire dans `DefaultProfileSeeder.BuildDefaultImportProfile` (feuille DIVERS).

---

## Décisions actées (résumé, ne pas rouvrir)

1. **Colonne `TABLEAUX`** : une seule colonne, valeurs jointes par `", "` (approche déjà retenue
   côté client, déjà livrée par le Lot U — voir 66.0 pour confirmation de l'état réel).
2. **Colonnes `TRAVAUX COMPLET` / `TRAVAUX DETAIL`** (`Parents`) : redondantes avec la colonne
   `TABLEAUX` (même information, sous forme éclatée). À **retirer** du profil d'export seedé.
3. **24 colonnes "points"** (`Prolock vannes`, `Contrôle Etanchéités`, ... `Réception finale
   chantier` — hors les 8 exclues, point 5) : doivent exister à l'identique sur `Parents` **et**
   `Enfants` (aujourd'hui présentes uniquement sur `Enfants`).
4. **Doublon `ZÉRO ENERGIE EN PRESENCE EE`** (`Enfants`) : `"ZÉRO ENERGIE EN PRESENCE EE"` (sans
   `(PS941)`) est un doublon accidentel qui ne correspond à aucun `Colonne.Nom` réellement produit
   par le moteur d'extraction (seul `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` existe côté
   `ConditionalPointRule` d'ISOLEMENT). À **retirer**.
5. **8 colonnes définitivement écartées** (`Visite préalable chantier`, `Procédure MAD`,
   `Autorisation travaux`, `Validation fin travaux`, `Autorisation déplatinages`, `Procédure REL`,
   `Autorisation de remise en service`, `Réception finale chantier`) : aucune règle d'extraction
   connue ne les alimente. **Ne pas les ajouter** dans ce lot. Si une spec client arrive un jour,
   ce sera un nouveau ticket — ne pas anticiper.
6. **Colonnes d'identité manquantes**, ajoutées **sans règle d'extraction** (`Source = null` /
   `ColumnDefinition` non mappée — cellule vide, cas déjà valide du domaine) :
   - `Parents` : `LOC2`, `LOC3`, `FLUIDE`, `RECURRENT`, `SUPPRESSION`, `ADR Email`,
     `COMMENTAIRES`.
   - `Enfants` : `LOC2`, `LOC3`, `PHASE PROCESS`, `REMARQUES`, `ETIQUETTE`, `DIAMETRE INCH`,
     `SERIE LBS`, `NATURE JOINT`, `BESOIN ECHAF`, `SUPPRESSION`, `POSITION A LA DEPOSE`.
7. **Agrégation des colonnes points sur `Parents`** (point de conception nouveau, voir 66.3) :
   aujourd'hui, un `PointPivot` créé sur les feuilles ISOLEMENT/PLATINES/ORIFICES
   CAPACITES/AUTRES JOINTS TOUCHES porte `ParentRepere = Isolement.Repere`, jamais
   `Equipement.Repere`. Le moteur ne marquerait donc **jamais** ces 24 colonnes sur une ligne
   `Parents` en l'état. Décision actée : **agrégation** — une colonne point est marquée sur la
   ligne `Parents` si **au moins un** `IsolementPivot` enfant de cet Équipement
   (`IsolementPivot.RepereParent == EquipementPivot.Repere`) porte ce `Point`. C'est une
   **nouvelle capacité du moteur**, pas un contournement côté profil.

---

## Hors périmètre explicite de ce lot (ne pas réouvrir)

- Les 8 colonnes de la décision 5 ci-dessus.
- `TM_PROC_MAD` / `TM_PROC_REL` : écart réel et important identifié (le profil actuel n'expose que
  5 colonnes low-level — `Ordre`, `Action`, `Acteur`, `Risques`, `Date de validation` — quand
  l'import côté legacy en attend potentiellement davantage), mais **volontairement différé** à un
  futur lot dédié. Ne rien modifier sur ces deux feuilles ici.
- Migration idempotente pour un environnement où le profil par défaut serait déjà seedé (pattern
  T8) : **non nécessaire ici** — la base de données peut être supprimée et recréée avant mise en
  service (l'outil n'est pas encore en usage réel), donc un simple reseed suffit. Ne pas écrire de
  migration de données pour ce lot.
- Écran Blazor `ExportProfileEditor.razor` : aucune modification d'UI n'est nécessaire pour ce
  lot — toutes les modifications sont des données de seed (`DefaultProfileSeeder`) et, pour 66.3,
  un changement de comportement du moteur (`SheetGenerationEngine`), pas du formulaire d'édition.
- Renommage, ré-ordonnancement esthétique de colonnes existantes non listées explicitement
  ci-dessus.

---

## 66.0. Investigation préalable (obligatoire avant tout code)

- [x] Lire l'état réel actuel du profil d'export seedé (`DefaultProfileSeeder`) pour les règles
  `Parents` et `Enfants` : confirmer si les colonnes `Tableaux`/`PROGRESS`/`ELEMENT PARENT`/
  `Type Elément` (Lot U, `tickets-tdd-pivot-tableaux-applications-export.md`) sont **déjà
  implémentées** — un export réel produit contre la fixture C7401 les contient déjà, donc ce lot
  ne doit **pas** les recréer, seulement vérifier la non-régression.
- [x] Confirmer dans le seed actuel la présence exacte des 2 `PointColumnDefinition`
  `"TRAVAUX COMPLET"` / `"TRAVAUX DETAIL"` sur `Parents`, à retirer (décision 2).
- [x] Confirmer dans le seed actuel la présence exacte du doublon `"ZÉRO ENERGIE EN PRESENCE EE"`
  (sans suffixe) sur `Enfants`, à retirer (décision 4) — relever son `ColonneNom`/`Header` exacts
  tels que seedés, pour ne retirer que la bonne entrée.
- [x] Relire `SheetGenerationEngine` (marquage des colonnes Points) pour confirmer le mécanisme
  exact de correspondance `PointPivot.ParentRepere` ↔ `Repere` de la ligne générée, point de
  départ de 66.3.
- [x] Relire `IsolementPivot.RepereParent` (Lot U) : confirmer qu'il est bien renseigné et fiable
  pour servir de clé d'agrégation Isolement → Équipement.
- [x] Relever l'ordre exact des 24 `PointColumnDefinition` déjà seedées sur `Enfants`, pour les
  reproduire à l'identique (même ordre, mêmes `ColonneNom`/`Header`/`MarkValue`) sur `Parents`.

---

## 66.1. Infrastructure — retrait des colonnes redondantes/erronées du profil d'export seedé

**Comportement attendu** (`DefaultProfileSeeder`, extension additive du seed existant — pas de
nouvelle capacité Domain) :
- `Parents` : retirer les `PointColumnDefinition` `"TRAVAUX COMPLET"` et `"TRAVAUX DETAIL"`
  (décision 2 — redondantes avec `TABLEAUX`).
- `Enfants` : retirer la `PointColumnDefinition` dupliquée `"ZÉRO ENERGIE EN PRESENCE EE"` (sans
  `(PS941)`) — ne conserver que `"ZÉRO ENERGIE EN PRESENCE EE (PS941)"` (décision 4).

**Tests** (Infrastructure, contre le profil seedé réel) :
- Le profil d'export seedé pour `Parents` ne contient plus de `PointColumnDefinition` dont le
  `ColonneNom` est `"TRAVAUX COMPLET"` ou `"TRAVAUX DETAIL"`.
- Le profil d'export seedé pour `Enfants` ne contient plus qu'une seule `PointColumnDefinition`
  dont le `Header` correspond à la variante `ZÉRO ENERGIE...` — celle avec `(PS941)`.
- Non-régression contre les 3 fixtures réelles (C7401, D8570, G6306B) : génération toujours
  fonctionnelle après retrait, aucune colonne orpheline dans l'en-tête généré.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (+ tests associés).

---

## 66.2. Infrastructure — ajout des colonnes d'identité non mappées

**Comportement attendu** : ajout, dans le profil d'export seedé, de `ColumnDefinition(Header,
Source: null)` pour chacune des colonnes suivantes (cas déjà valide du domaine — voir I1,
`ColumnDefinition.Source = null` écrit une cellule vide sans erreur) :

- `Parents` : `LOC2`, `LOC3`, `FLUIDE`, `RECURRENT`, `SUPPRESSION`, `ADR Email`, `COMMENTAIRES`.
- `Enfants` : `LOC2`, `LOC3`, `PHASE PROCESS`, `REMARQUES`, `ETIQUETTE`, `DIAMETRE INCH`,
  `SERIE LBS`, `NATURE JOINT`, `BESOIN ECHAF`, `SUPPRESSION`, `POSITION A LA DEPOSE`.

**Positionnement** : à défaut d'exigence stricte du client sur l'ordre de ces colonnes non
mappées, les positionner dans l'ordre où elles apparaissent dans `OXO_TRAME_IMPORT_MAD.xlsx`
(seule référence disponible pour l'ordre, même si le contenu de ce fichier n'est pas une source
de vérité — voir en-tête de ce document) :
- `Parents`, après `DESIGNATION PROCESS`/`Désignation` et avant `TABLEAUX` : `FLUIDE`,
  `RECURRENT`, puis après `PROGRESS` : `SUPPRESSION`, `ADR Email`, `COMMENTAIRES` ; `LOC2`/`LOC3`
  juste après `ZONE`.
- `Enfants` : `LOC2`/`LOC3` juste après `ZONE` ; `PHASE PROCESS`, `REMARQUES`, `ETIQUETTE`,
  `DIAMETRE INCH`, `SERIE LBS`, `NATURE JOINT`, `BESOIN ECHAF` après `POSITION A LA POSE` ;
  `POSITION A LA DEPOSE` juste après `POSITION A LA POSE` ; `SUPPRESSION` après `PROGRESS`.

Ce positionnement est indicatif, pas bloquant — si son implémentation stricte complique le seed
existant, une position raisonnable en fin de bloc "colonnes descriptives" (avant les colonnes
Points) est acceptable ; le signaler dans le compte-rendu d'implémentation plutôt que de bloquer.

**Tests** (Infrastructure) :
- Le profil d'export seedé contient bien chaque nouvelle colonne, avec `Source = null`.
- Génération contre les 3 fixtures réelles : chaque nouvelle colonne apparaît dans l'en-tête,
  toutes ses cellules sont vides sur toutes les lignes générées.
- Non-régression : les colonnes déjà existantes (`Header`, `Source`, ordre relatif entre elles)
  ne sont pas altérées par cet ajout.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (+ tests associés).

---

## 66.3. Application — agrégation Isolement → Équipement pour les colonnes Points (nouvelle capacité moteur)

**Comportement attendu**, extension de `SheetGenerationEngine` :
- Pour une `SheetGenerationRule` de `PivotSource = Equipement` portant des
  `PointColumnDefinition` : pour chaque colonne, la cellule est marquée (`MarkValue`) si :
  - un `PointPivot` existe avec `ParentRepere == EquipementPivot.Repere` (comportement actuel,
    inchangé — cas des 2 `Point` `TRAVAUX COMPLET`/`TRAVAUX DETAIL`, désormais sans objet sur
    `Parents` après 66.1 mais le mécanisme reste générique et ne doit pas être supprimé), **ou**
  - **(nouveau)** un `PointPivot` existe avec `ColonneNom` correspondant, dont le
    `ParentRepere` est le `Repere` d'un `IsolementPivot` du run tel que
    `IsolementPivot.RepereParent == EquipementPivot.Repere`.
- Comparaison insensible à la casse et `.Trim()` sur `ColonneNom`, cohérent avec le reste du
  moteur (voir spec §7, décisions déjà actées côté import).
- Le comportement des lignes `Isolement` (marquage direct par `ParentRepere == Isolement.Repere`,
  sans agrégation) est **inchangé** — l'agrégation ne s'applique qu'aux lignes `Equipement`.

**Point de conception à confirmer en investigation (66.0)** : le moteur actuel ne reçoit-il, pour
générer une feuille `Equipement`, que l'`EquipementPivot` et les `PointPivot` du run, ou a-t-il
déjà accès à la liste complète des `IsolementPivot` du run à cet endroit ? Si non, il faut étendre
la signature interne du moteur (pas l'interface publique `ExportProfile`/`SheetGenerationRule`,
qui n'a pas besoin de changer) pour lui donner accès aux `IsolementPivot` du run lors de la
génération d'une feuille `Equipement`. Un seul `EquipementPivot` par run (contrainte déjà actée
ailleurs) simplifie cette agrégation : pas besoin de désambiguïser entre plusieurs Équipements.

**Tests** (Application) :
- `EquipementPivot` sans aucun `Point` direct + un `IsolementPivot` enfant (`RepereParent` =
  repère de l'Équipement) portant un `Point` de `ColonneNom` `"X"` → la colonne `"X"` est marquée
  sur la ligne `Parents` générée.
- Deux `IsolementPivot` enfants, un seul porte le `Point` → la colonne est tout de même marquée
  (agrégation "au moins un", pas "tous").
- Aucun `IsolementPivot` ne porte le `Point` → colonne vide sur `Parents`.
- `IsolementPivot` dont `RepereParent` ne correspond **pas** à l'Équipement du run (cas
  théorique/défensif) → son `Point` n'est **pas** agrégé — test de garde-fou.
- Non-régression : le marquage existant sur les lignes `Isolement` (`Enfants`) n'est pas affecté
  par ce changement.
- Non-régression : le marquage déjà en place pour un `Point` directement rattaché à l'Équipement
  (mécanisme historique, ex. anciennes colonnes `TRAVAUX COMPLET`/`TRAVAUX DETAIL` si testé
  synthétiquement) continue de fonctionner sans dépendre de l'agrégation.

**Dossier** : `src/ExcelETL.Application/Generation/` (+ miroir tests).

---

## 66.4. Infrastructure — ajout des 24 `PointColumnDefinition` sur la règle `Parents`

**Comportement attendu** : ajouter à la `SheetGenerationRule` `Parents` du profil d'export seedé
les mêmes 24 `PointColumnDefinition` (mêmes `ColonneNom`/`Header`/`MarkValue`, même ordre) que
celles déjà seedées sur `Enfants` (relevées en 66.0). Dépend de 66.3 pour produire un résultat
correct — implémenter 66.3 avant 66.4, ou au moins avant d'exécuter les tests d'intégration de
66.4 contre les fixtures réelles.

**Tests** (Infrastructure, non-régression contre les 3 fixtures réelles C7401, D8570, G6306B) :
- Les 24 colonnes points apparaissent dans l'en-tête généré de `Parents`, dans le même ordre et
  avec les mêmes libellés que sur `Enfants`.
- Pour chacune des 3 fixtures, une colonne point cochée sur au moins un Isolement (`Enfants`)
  est bien cochée sur la ligne `Parents` correspondante (agrégation 66.3 exercée en conditions
  réelles).
- Pour chacune des 3 fixtures, une colonne point jamais cochée sur aucun Isolement reste vide sur
  `Parents`.

**Dossier** : `src/ExcelETL.Infrastructure/Seeding/DefaultProfileSeeder.cs` (+ tests associés).

---

## 66.5. Tests d'intégration de clôture (bout-en-bout, 3 fixtures réelles)

**Comportement attendu** : un test d'intégration par fixture (C7401, D8570, G6306B), pipeline
complet import → pivot → génération avec le profil d'export seedé mis à jour, qui vérifie en un
seul endroit :
- L'en-tête complet de `Parents` (37 + 7 colonnes d'identité − 2 colonnes retirées − 8 colonnes
  écartées = ordre et nombre exact à figer par le test, pas dans ce ticket, pour éviter toute
  divergence de comptage manuel).
- L'en-tête complet de `Enfants` (43 + 11 colonnes d'identité − 1 doublon retiré, même principe).
- Au moins une ligne de données par feuille, avec les colonnes vides pour les colonnes non
  mappées et au moins une colonne point correctement cochée par agrégation sur `Parents`.

**Dossier** : `tests/ExcelETL.Infrastructure.Tests/Generation/` (ou équivalent existant pour les
tests d'intégration bout-en-bout du Lot I/U).

---

## Ordre d'implémentation recommandé

66.0 → 66.1 → 66.2 → 66.3 → 66.4 → 66.5. Ne pas paralléliser 66.3/66.4 : 66.4 sans 66.3 produit
un résultat silencieusement incorrect (colonnes jamais cochées), contraire au principe de ce
projet de ne jamais livrer un comportement observable erroné sans échec de test explicite.
