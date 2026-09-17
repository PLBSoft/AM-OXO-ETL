# Tickets TDD — Lot 079 : vue "Détails" en langage courant du profil d'export

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suite du lot 078
(`tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md`), qui a livré la vue Détails du
profil d'import et renvoyait explicitement le profil d'export à un lot séparé. Même objectif : une
reformulation en langage courant, pour un utilisateur qui n'est ni développeur ni expert de
l'application, de ce que le profil produit réellement dans le fichier généré.*

**Objet** : page de lecture seule `/export-profiles/{Id:guid}/details`, accessible depuis
`ExportProfiles.razor` par un nouveau bouton de ligne, qui décrit feuille par feuille et colonne par
colonne le classeur généré par le profil. Ne remplace rien : l'éditeur
(`ExportProfileEditor.razor`) reste tel quel.

**Hors périmètre explicite de ce lot** :
- Toute modification du domaine (`ExportProfile`, `SheetGenerationRule`, colonnes), du moteur
  (`SheetGenerationEngine`) ou de l'écriture (`ClosedXmlWorkbookWriter`). Un défaut trouvé est
  **signalé** par la page, jamais corrigé ici.
- Toute modification de l'éditeur de profil d'export.
- La traduction anglaise (même décision que le lot 078, D5).

---

## 79.0. Investigation préalable (obligatoire avant tout code)

**Nature de ce ticket** : pas de code de production. Il produit la note de conception ci-dessous,
à valider (décisions du §6) avant d'écrire les sous-tickets 79.1+.

**À confirmer par lecture directe du dépôt réel** :
1. Inventaire exhaustif des champs de `ExportProfile` et de leur effet réel dans le moteur
   (`SheetGenerationEngine`, `PivotFieldResolver`) et à l'écriture (`ClosedXmlWorkbookWriter`).
2. Réglages enregistrés mais sans effet, et configurations qui font échouer la génération.
3. Ce qui peut être repris du lot 078 (modèle de description, rendu des segments, page).
4. Conventions de la page liste `ExportProfiles.razor` pour le bouton.
5. Catalogue complet rendu sur le profil d'export semé « Profil OXO standard ».

---

### Conclusions de l'investigation (2026-09-17) — note de conception à valider

*Relu dans le code au commit `4c32bb2`. Comportement des noms de feuilles vérifié par une sonde
ClosedXML jetable (test supprimé après usage), pas supposé.*

#### 1. Inventaire réel des champs

**Racine `ExportProfile`** : `Name` (≤ 60, sans effet sur le fichier) et `SheetRules` (au moins une).
Contrairement au profil d'import, **aucun réglage global** : pas de section « paramètres généraux »
au sens du lot 078, seulement un résumé du classeur.

**`SheetGenerationRule`** : `SheetName`, `PivotSource` (`Equipement`, `Isolement`,
`TacheMultiple`), et 4 listes de colonnes :

| Liste | Forme | Cellule écrite | Autorisée pour |
| :--- | :--- | :--- | :--- |
| `ColumnDefinitions` | `Header`, `Source` (`PivotFieldRef?`) | valeur du champ ; `Source = null` → cellule toujours vide | les 3 sources (champ compatible vérifié à la construction) |
| `ConstantColumnDefinitions` | `Header`, `Value` | `Value` sur chaque ligne, titres de section compris | les 3 sources |
| `ApplicationColumnDefinitions` | `ApplicationNom`, `Header`, `MarkValue` | `MarkValue` si la ligne est rattachée à l'application (après `Trim`, insensible à la casse), sinon vide | Équipement, Élément (refusé pour `TacheMultiple`) |
| `PointColumnDefinitions` | `ColonneNom`, `Header`, `MarkValue` | `MarkValue` si la ligne a un point de cette colonne, sinon vide | Équipement, Élément (refusé pour `TacheMultiple`) |

**Ordre des colonnes dans le fichier, fixe** : le moteur écrit toujours les colonnes descriptives,
puis les constantes, puis les applications, puis les points, chaque groupe dans l'ordre de sa liste.
La lettre de colonne Excel de chaque colonne est donc calculable à partir du profil seul.

**Lignes produites selon `PivotSource`** (`SheetGenerationEngine`) :
- `Equipement` : **une seule ligne**, l'équipement. Une colonne de point est cochée si l'équipement
  porte lui-même le point (comparaison exacte — c'est le cas des points des tableaux par défaut de
  PROCEDURE) **ou** si au moins un de ses éléments le porte (comparaison après `Trim`, insensible à
  la casse, lot 066).
- `Isolement` : **une ligne par élément**, dans l'ordre des feuilles ISOLEMENT, PLATINES, ORIFICES
  CAPACITES, AUTRES JOINTS TOUCHES, DIVERS (`ImportPipelineOrchestrator`). Une colonne de point est
  cochée si l'élément porte le point, **comparaison exacte** (majuscules et espaces compris).
  **Écart** : la feuille Équipement tolère casse et espaces pour les points hérités des éléments,
  la feuille Éléments non. Signalé, pas corrigé (hors périmètre).
- `TacheMultiple` : **une feuille par type de tâche** présent dans le fichier importé, nommée
  d'après le code du type (caractères interdits remplacés par `_`, 31 caractères au plus), feuilles
  classées par ordre alphabétique du code. Chaque feuille contient toutes les tâches de ce type,
  titres de section compris, dans l'ordre de PROCEDURE. **Le `SheetName` de la règle n'apparaît
  nulle part dans le fichier** : c'est un libellé interne.

Chaque feuille a une ligne de titres en ligne 1, les données commencent en ligne 2
(`ClosedXmlWorkbookWriter`). Les feuilles sont écrites dans l'ordre des règles.

**`PivotFieldRef`** (27 valeurs) — effet réel de chaque champ (`PivotFieldResolver`) :

| Champ | Contenu écrit |
| :--- | :--- |
| `EquipementRepere` / `IsolementRepere` | repère de l'équipement / de l'élément |
| `EquipementDesignation` / `IsolementDesignation` | désignation |
| `EquipementTypeElementNom` / `IsolementTypeElementNom` | type d'élément |
| `EquipementLocalisation` / `IsolementLocalisation` | zone (lue dans DIVERS, appliquée à tout le fichier) |
| `EquipementTableaux` / `IsolementTableaux` | tableaux, séparés par « , » |
| `EquipementSourceSheet` / `IsolementSourceSheet` | nom de la feuille du fichier source d'où vient la ligne |
| `IsolementPositionALaPose` | position à la pose (vide hors ISOLEMENT) |
| `IsolementRepereParent` | repère de l'équipement |
| `IsolementCouleurEtiquette` | couleur d'étiquette (vide si la feuille d'origine n'en fournit pas) |
| `TacheMultipleOrdre` | ordre (vide pour un titre de section) |
| `TacheMultipleAction` / `Acteur` / `Risques` | action / acteur / risques |
| `TacheMultipleDateValidation` | date de validation au format jj/mm/aaaa (vide si absente) |
| `TacheMultipleRepere` | repère de l'équipement |
| `TacheMultipleTypeElementNom` | type d'élément de l'équipement |
| `TacheMultipleColonneTravaux` | libellé « colonne travaux » du type de tâche (vide si le profil d'import n'en définit pas) |
| `TacheMultipleTypeTacheMultipleCode` | code du type de tâche |
| `TacheMultipleLocalisation` | zone de l'équipement |
| `TacheMultipleLigneSource` | numéro de ligne de la tâche dans la feuille PROCEDURE |
| `TacheMultipleCritere` | « A faire », ou « Pour info » pour un titre de section (**codé en dur**, non modifiable) |

**Dépendance au profil d'import** : un profil d'export ne connaît pas le profil d'import avec
lequel il sera utilisé (les deux sont choisis à chaque traitement). Les points, applications,
tableaux, libellés « colonne travaux » et types de tâches dépendent donc du profil d'import. La vue
décrit les champs **sans coordonnées de cellules** (elles appartiennent au profil d'import, déjà
décrites par sa propre vue Détails).

#### 2. Réglages sans effet et configurations bloquantes

**Réglages enregistrés mais sans effet** : **aucun** que le domaine laisse passer. Les colonnes de
points et d'applications sont refusées pour `TacheMultiple` dès la construction ; le seul champ
« non utilisé » est le `SheetName` d'une règle `TacheMultiple`, qui se décrit comme un fait (libellé
interne), pas comme un réglage ignoré. **La vue d'export n'a donc pas de bloc « Configuré mais
ignoré ».**

**Configurations qui font échouer la génération** — le domaine les accepte, `SheetGenerationEngine`
les produit, puis `ClosedXmlWorkbookWriter` lève une `ArgumentException` à l'écriture (erreur
technique 500 sur `POST /api/oxo/process`, échec du téléchargement sur la page de test). Vérifié
avec ClosedXML :

| Cas | Résultat |
| :--- | :--- |
| `SheetName` de plus de 31 caractères (règle Équipement ou Élément) | exception |
| `SheetName` contenant `\ / ? * [ ] :` | exception |
| `SheetName` commençant ou finissant par `'` | exception |
| Deux règles Équipement/Élément de même `SheetName`, **même à la casse près** (« Parents » / « parents ») | exception |
| Deux règles `TacheMultiple` | exception dès qu'un type de tâche existe (même nom de feuille généré deux fois) |
| Règle Équipement/Élément nommée comme un code de type de tâche (« TM_PROC_MAD », « TM_PROC_REL », casse ignorée) avec une règle `TacheMultiple` | exception si le fichier contient des tâches de ce type |
| Espaces en début ou fin de nom (`" Parents "`) | accepté, conservé tel quel |

Seuls les codes `TM_PROC_MAD`/`TM_PROC_REL` sont connus à l'avance (conversion fixe `MAD`/`REL` du
lot 078) : une autre valeur du fichier source est gardée telle quelle et ne peut pas être prévue.

Ces cas sont décrits en bloc « Problèmes qui empêchent la génération » (même présentation que le
bloc bloquant du lot 078). Ils sont **signalés**, pas corrigés : ajouter une validation au domaine
ou nettoyer les noms est un autre lot.

#### 3. Ce qui se reprend du lot 078

- **Modèle** : `ProfileDescriptionSection`/`Sentence`/`Text`/`Segment` sont déjà neutres. Seul
  l'enregistrement racine s'appelle `ImportProfileDescription` : proposition, le renommer
  `ProfileDescription` (refactor sans changement de comportement).
- **Marquage des valeurs** (caractères U+E000-E003) et helpers `Quote`/`JoinWithAnd`/`Marked` :
  privés dans `ImportProfileDescriptionBuilder`. Proposition : les extraire dans une classe interne
  partagée, utilisée par les deux constructeurs. Les tests existants du lot 078 restent inchangés.
- **Rendu** : le markup des sections et `RenderText` de `ImportProfileDetails.razor` extraits dans un
  composant partagé (`ProfileDescriptionView.razor`), mêmes ids (`details-section-general`,
  `details-section-sheet-{i}`, suffixes `-blocking`/`-ignored`). Les tests bUnit de la page d'import
  restent inchangés.
- **Nouveau** : `ExportProfileDescriptionBuilder` (statique, `ExportProfile` +
  `IStringLocalizer<BlazorAdminMessages>`), clés `ExportProfileDetails_*` (convention de clés par
  page). Conversion numéro → lettre de colonne Excel : n'existe pas (`BlockFieldRangeFormatter` n'a
  que lettre → numéro, en privé), à ajouter.
- **Aucune table d'usage** du type `ImportSheetUsage` : le moteur d'export applique les mêmes règles
  à toutes les feuilles de même `PivotSource`, rien n'est lié au nom de la feuille.

#### 4. Conventions UI

- Ids de ligne de `ExportProfiles.razor` : `{action}-export-profile-button-{id}`
  (`edit-export-profile-button-{id}`…), doublés `-card-{id}` pour la carte mobile. Proposition :
  `details-export-profile-button-{id}` et `details-export-profile-button-card-{id}`, placés avant
  Modifier, icône `AdminIconMarkup.Eye`, classe et `aria-label`/`title` identiques au lot 078.
- Page : route `/export-profiles/{Id:guid}/details`, `[Authorize]` sans rôle, `PageBackNavLink`
  `back-to-export-profiles-button`, alerte `#export-profile-details-not-found`, même conteneur que
  la page d'import.

#### 5. Catalogue rendu sur le profil seedé « Profil OXO standard »

*Proposition de rédaction, à valider. Lettres de colonnes calculées selon l'ordre fixe du §1.
« (fixe) » = comportement codé, affiché avec le badge « non modifiable ». Les colonnes de points sont
regroupées en une phrase (proposition D2).*

**Classeur généré**
- Le fichier contient les feuilles « Parents », « Enfants », puis une feuille par type de tâche (règle « Tâches multiples »), dans cet ordre.
- Chaque feuille commence par une ligne de titres ; les données commencent à la ligne 2. (fixe)
- Les points, applications, tableaux et types de tâches viennent du profil d'import utilisé avec ce profil d'export.

**Feuille Parents**
- Une seule ligne : l'équipement. (fixe)
- Colonne A « Repère » : le repère de l'équipement.
- Colonne B « Feuille » : le nom de la feuille du fichier source d'où vient la ligne.
- Colonne C « Type Elément » : le type d'élément de l'équipement.
- Colonne D « Zone » : la zone de l'équipement.
- Colonne E « LOC2 » : toujours vide.
- Colonne F « LOC3 » : toujours vide.
- Colonne G « Désignation » : la désignation de l'équipement.
- Colonne H « FLUIDE » : toujours vide.
- Colonne I « RECURRENT » : toujours vide.
- Colonne J « Tableaux » : les tableaux de l'équipement, séparés par une virgule.
- Colonne K « SUPPRESSION » : toujours vide.
- Colonne L « ADR Email » : toujours vide.
- Colonne M « COMMENTAIRES » : toujours vide.
- Colonne N « PROGRESS » : « O » si l'équipement est rattaché à l'application « PROGRESS », sinon vide.
- Colonnes O à AD : « X » si l'équipement, ou au moins un de ses éléments, est coché dans la colonne du même nom à l'import, sinon vide : « PROLOCK VANNES » (O), « DEPROLOCK VANNES » (P), « ZÉRO ENERGIE EN PRESENCE EE (PS941) » (Q), « POSE ÉTIQUETTES » (R), « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS » (S), « CONTRÔLE ETANCHÉITÉS » (T), « RECEPTION DEBUT MAD » (U), « RÉCEPTION PLATINES/TAMPONS PLEINS » (V), « RECEPTION DEBUT REL » (W), « PLATINES / TAMPONS PLEINS » (X), « SYNCHRONISATION INSTRUMENTATION » (Y), « SOUPAPE : CONSTAT ENCRASSEMENT » (Z), « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS » (AA), « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES » (AB), « PF : VALIDATION CONSTAT ENCRASSEMENT » (AC), « PF : ACCORD TRAVAUX FEU » (AD).

**Feuille Enfants**
- Une ligne par élément, dans l'ordre des feuilles ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS. (fixe)
- Colonne A « Numéro » : le repère de l'élément.
- Colonne B « Feuille » : le nom de la feuille du fichier source d'où vient la ligne.
- Colonne C « Type Elément » : le type d'élément de l'élément.
- Colonne D « Zone » : la zone de l'élément.
- Colonne E « LOC2 » : toujours vide.
- Colonne F « LOC3 » : toujours vide.
- Colonne G « ELEMENT PARENT » : le repère de l'équipement.
- Colonne H « Désignation » : la désignation de l'élément.
- Colonne I « Position à la pose » : la position à la pose (vide hors feuille ISOLEMENT).
- Colonne J « POSITION A LA DEPOSE » : toujours vide.
- Colonne K « PHASE PROCESS » : toujours vide.
- Colonne L « REMARQUES » : toujours vide.
- Colonne M « ETIQUETTE » : la couleur d'étiquette (vide si la feuille d'origine n'en fournit pas).
- Colonne N « DIAMETRE INCH » : toujours vide.
- Colonne O « SERIE LBS » : toujours vide.
- Colonne P « NATURE JOINT » : toujours vide.
- Colonne Q « BESOIN ECHAF » : toujours vide.
- Colonne R « Tableaux » : les tableaux de l'élément, séparés par une virgule.
- Colonne S « SUPPRESSION » : toujours vide.
- Colonne T « PROGRESS » : « O » si l'élément est rattaché à l'application « PROGRESS », sinon vide.
- Colonnes U à AJ : « X » si l'élément est coché dans la colonne du même nom à l'import (nom identique, majuscules comprises), sinon vide : « PROLOCK VANNES » (U), « DEPROLOCK VANNES » (V), « ZÉRO ENERGIE EN PRESENCE EE (PS941) » (W), « POSE ÉTIQUETTES » (X), « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS » (Y), « CONTRÔLE ETANCHÉITÉS » (Z), « RECEPTION DEBUT MAD » (AA), « RÉCEPTION PLATINES/TAMPONS PLEINS » (AB), « RECEPTION DEBUT REL » (AC), « PLATINES / TAMPONS PLEINS » (AD), « SYNCHRONISATION INSTRUMENTATION » (AE), « SOUPAPE : CONSTAT ENCRASSEMENT » (AF), « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS » (AG), « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES » (AH), « PF : VALIDATION CONSTAT ENCRASSEMENT » (AI), « PF : ACCORD TRAVAUX FEU » (AJ).

**Feuilles par type de tâche (règle « Tâches multiples »)**
- Une feuille est créée par type de tâche présent dans le fichier importé, nommée d'après le code du type (« TM_PROC_MAD », « TM_PROC_REL »…), par ordre alphabétique. Le nom « Tâches multiples » n'apparaît pas dans le fichier. (fixe)
- Chaque feuille contient une ligne par tâche de ce type, titres de section compris, dans l'ordre de la feuille PROCEDURE. (fixe)
- Colonne A « GUID » : toujours vide.
- Colonne B « TYPE TACHE » : le code du type de tâche.
- Colonne C « Repère TM » : le repère de l'équipement.
- Colonne D « ZONE » : la zone de l'équipement.
- Colonne E « LOC2 » : toujours vide.
- Colonne F « LOC3 » : toujours vide.
- Colonne G « TYPE ELEMENT CODE » : le type d'élément de l'équipement.
- Colonne H « LOT » : toujours vide.
- Colonne I « Ressource » : toujours vide.
- Colonne J « Ligne » : le numéro de ligne de la tâche dans la feuille PROCEDURE.
- Colonne K « Ordre » : l'ordre de la tâche (vide pour un titre de section).
- Colonne L « Action » : l'action.
- Colonne M « Acteur » : l'acteur.
- Colonne N « Risques » : les risques.
- Colonne O « Date de validation » : la date de validation, au format jj/mm/aaaa.
- Colonne P « Colonne Travaux » : le libellé « colonne travaux » du type de tâche, défini dans le profil d'import.
- Colonne Q « CRITERE » : « A faire », ou « Pour info » pour un titre de section. (fixe)
- Colonne R « SUPPRESSION » : toujours « N ».

Le profil semé ne déclenche aucun cas du §2 : aucun bloc bloquant sur cette page.

#### 6. Décisions à trancher avant d'écrire 79.1+

- **D1 — Lettres de colonnes Excel** : recommandation, les afficher (« Colonne N ») et les mettre en
  évidence comme les coordonnées du lot 078 (`<code>`). Elles permettent de lire la page à côté du
  fichier. Option : titres seuls, dans l'ordre.
- **D2 — Regroupement des colonnes de points** : recommandation, une phrase par règle regroupant les
  colonnes de points qui ont la même valeur de coche et un titre égal au nom de colonne (cas du
  profil semé : 16 colonnes → 1 phrase par feuille) ; une phrase par colonne sinon. Option : une
  phrase par colonne partout (30 lignes pour Parents, 36 pour Enfants).
- **D3 — Configurations bloquantes (§2)** : recommandation, les détecter et les afficher en bloc
  « Problèmes qui empêchent la génération ». Coût : dupliquer côté `BlazorAdmin` les règles de nom
  de feuille d'Excel. Option : ne rien signaler.
- **D4 — Contrôle croisé avec les profils d'import** : signaler une colonne de points qu'aucun
  profil d'import enregistré ne coche, ou une application qu'aucun ne déclare. Recommandation :
  **non** dans ce lot (le profil d'export est indépendant du profil d'import, et la comparaison
  exacte du §1 rendrait les messages difficiles à expliquer). Option : l'ajouter.
- **D5 — Reprise du lot 078 (§3)** : renommer `ImportProfileDescription` en `ProfileDescription`,
  extraire les helpers de marquage et le rendu des sections dans des éléments partagés, sans changer
  les tests du lot 078. Option : dupliquer le rendu dans la page d'export.
- **D6 — Id du bouton** : `details-export-profile-button-{id}` / `-card-{id}` (convention de la
  page). À confirmer.
- **D7 — Anglais** : comme le lot 078, français seulement ; les clés `ExportProfileDetails_*`
  reprennent le texte français dans les deux `.resx`. À confirmer.

#### 7. Décisions actées (Simon, 2026-09-17)

- **D1 — acté : oui.** Chaque colonne est précédée de sa lettre Excel, mise en évidence en `<code>`
  comme les coordonnées de cellules du lot 078.
- **D2 — acté : phrase groupée.** Les colonnes de points de même valeur de coche dont le titre est
  égal au nom de colonne forment une seule phrase par règle, avec la lettre de chacune ; les autres
  colonnes de points ont chacune leur phrase.
- **D3 — acté : oui, bloc rouge.** Les cas du §2 sont listés sous « Problèmes qui empêchent la
  génération ». Les règles de nom de feuille d'Excel sont dupliquées côté `BlazorAdmin` et
  vérifiées par test contre ClosedXML.
- **D4 — acté : non.** Pas de contrôle croisé avec les profils d'import dans ce lot.
- **D5, D6, D7 — actés (Simon, 2026-09-17)** : reprise du lot 078 par éléments partagés (renommage
  `ProfileDescription`, helpers et rendu extraits, tests du lot 078 inchangés), id
  `details-export-profile-button-{id}` / `-card-{id}`, français seulement.

---

## Sous-tickets 79.1 à 79.9

*Rédigés le 2026-09-17 à partir de la note ci-dessus (§1 à §7), qui reste la référence : les phrases
attendues sont celles du catalogue §5, citées ici par feuille plutôt que recopiées.*

### Cadre commun à tous les sous-tickets

- **Emplacement** : `src/ExcelETL.BlazorAdmin/Formatting/` pour le code non visuel,
  `Components/Pages/Admin/` pour la page et le rendu commun des sections ; tests en miroir sous
  `tests/ExcelETL.BlazorAdmin.Tests/`.
- **Modèle de sortie** : celui du lot 078, renommé en 79.1 : `ProfileDescription(Sections)`,
  `ProfileDescriptionSection(Title, Sentences, Ignored, Blocking)`,
  `ProfileDescriptionSentence(Content, IsFixed)`, `ProfileDescriptionText`/`Segment` (`Text`, `Value`,
  `CellReference`). Côté export, `Ignored` est toujours vide (§2). Les lettres de colonnes sont des
  segments `CellReference` (D1 : rendues en `<code class="profile-details-cell">`, comme une
  coordonnée de cellule).
- **Point d'entrée** : `ExportProfileDescriptionBuilder.Build(ExportProfile, IStringLocalizer<BlazorAdminMessages>)`,
  classe statique ; une méthode privée par partie (classeur, section de règle, colonnes descriptives
  et constantes, applications et points, blocages).
- **Ordre** : sections dans l'ordre de `ExportProfile.SheetRules` tel que chargé, colonnes dans
  l'ordre du moteur (§1). Contrairement au lot 078, pas de réordonnancement : le moteur et l'écriture
  suivent exactement cet ordre sur le profil chargé, la page décrit donc le fichier réel.
- **Ressources** : clés `ExportProfileDetails_*` (et `ExportProfiles_Details` pour le bouton) dans
  `BlazorAdminMessages.resx` **et** `.fr.resx` avec le même texte français (D7). Clés communes aux
  deux vues renommées `ProfileDescription_*` en 79.1. Guillemets français « … ».
- **Tests du constructeur** : xUnit pur, localiseur réel en `fr-FR` via un support de test calqué
  sur `DescriptionTestSupport` (profils d'export minimaux construits à la main) ; le profil semé
  n'arrive qu'en 79.9. Assertions sur le texte exact.
- **Exécution** : `dotnet test tests/ExcelETL.BlazorAdmin.Tests --filter <classe> --verbosity quiet`
  (sortie `-p:BaseOutputPath=bin-claude/`, dossiers supprimés ensuite) ; projet complet en 79.9.
- **Commit** : un commit par sous-ticket passé au vert.

### 79.1 — Éléments partagés avec la vue d'import (D5)

**Comportement** : aucun changement visible. Refactor pur qui prépare la réutilisation :
- `ImportProfileDescription` renommé `ProfileDescription` (fichier `ProfileDescription.cs`) ;
- helpers de marquage sortis de `ImportProfileDescriptionBuilder` dans une classe interne partagée
  `ProfileDescriptionMarking` : `Quote`, `QuoteList`, `CellRef`, `Marked`, `JoinWithAnd`,
  `JoinWithOr`, `OneOrSeveral`. `CellRange` (propre au bloc d'import) reste dans le constructeur
  d'import ;
- clés utilisées par ces helpers et par le rendu renommées, valeur inchangée :
  `ImportProfileDetails_QuotedValue` → `ProfileDescription_QuotedValue`,
  `ImportProfileDetails_ListLastSeparator` / `_ListLastOrSeparator` →
  `ProfileDescription_ListLastSeparator` / `_ListLastOrSeparator`,
  `ImportProfileDetails_FixedMarker` → `ProfileDescription_FixedMarker` ;
- rendu des sections sorti de `ImportProfileDetails.razor` dans `ProfileDescriptionView.razor` :
  paramètres `Description`, `BlockingHeading`, `IgnoredHeading` ; mêmes ids
  (`details-section-general`, `details-section-sheet-{i}`, suffixes `-blocking` / `-ignored`), même
  markup, `RenderText` déplacé tel quel. La page d'import passe ses titres
  `ImportProfileDetails_BlockingHeading` / `_IgnoredHeading`.

**Rouge** : aucun (refactor). **Garde-fou** : toute la suite du lot 078
(`Formatting/ImportProfileDescription*`, `ImportSheetUsageTests`, `ImportProfileDetailsTests`, route
de `BusinessPageAuthorizationHttpTests`) reste verte **sans modifier une assertion** ; seul
`DescriptionTestSupport` change de type de retour. Vérifier par recherche qu'aucune clé renommée
n'est encore référencée sous l'ancien nom.

**Fait (2026-09-17)** : `Formatting/ProfileDescription.cs` (renommé), `Formatting/ProfileDescriptionMarking.cs`
(helpers + `ListSeparator`, importés par `using static`), `Components/Pages/Admin/ProfileDescriptionView.razor`
(`BlockingHeading` obligatoire, `IgnoredHeading` facultatif). 4 clés renommées dans les deux `.resx`,
aucune référence restante à l'ancien nom. Aucune assertion modifiée ; seul `DescriptionTestSupport`
change de type. Périmètre filtré (constructeur et table d'import, page Détails, liste des profils
d'import, routes HTTP) : 243/243.

### 79.2 — Lettres de colonnes et disposition des colonnes

**Comportement** :
- `ExcelColumnLetters.FromNumber(int)` : 1 → `A`, 26 → `Z`, 27 → `AA`, 52 → `AZ`, 53 → `BA`,
  702 → `ZZ`, 703 → `AAA`, 16384 → `XFD` ; 0 ou négatif → `ArgumentOutOfRangeException`.
- `ExportColumnLayout.For(SheetGenerationRule)` : liste ordonnée des colonnes d'une règle telle que
  le moteur les écrit — `(Letter, Header, Kind, Definition)`, `Kind` ∈ {`Descriptive`, `Constant`,
  `Application`, `Point`} ; ordre descriptives → constantes → applications → points (les deux
  dernières listes sont vides par construction pour `TacheMultiple`).

**Rouge** :
- `ExcelColumnLettersTests` : les cas ci-dessus.
- `ExportColumnLayoutTests` : ordre et lettres sur une règle Équipement mélangeant les 4 listes ;
  règle `TacheMultiple`.
- **Garde-fou contre la dérive avec le moteur** : pour chaque règle du profil d'export semé
  (`DefaultProfileSeeder`, EF InMemory), les `Header` de `ExportColumnLayout.For` sont égaux, dans
  l'ordre, aux `Headers` de la feuille produite par le vrai `SheetGenerationEngine` sur un
  `ImportResult` minimal (un équipement, un élément, une tâche). Si le moteur change d'ordre, ce test
  casse avant que la page ne donne de fausses lettres.

**Vert** : deux classes statiques dans `Formatting/`.

**Fait (2026-09-17)** : `ExcelColumnLetters.cs`, `ExportColumnLayout.cs` (record `ExportColumn(Letter,
Header, Kind, Definition)`, `Definition` = l'enregistrement de colonne du profil). Rouge = compilation.
Non-vacuité du garde-fou vérifiée : placer les points avant les applications fait échouer le test sur le
profil semé. `ExcelColumnLettersTests` + `ExportColumnLayoutTests` : 14/14.

### 79.3 — Section « Classeur généré » et sections de règles

**Comportement** : `ExportProfileDescriptionBuilder.Build` produit :
- section 0, titre « Classeur généré » : la liste des feuilles dans l'ordre des règles (« la feuille
  « Parents » », « une feuille par type de tâche » pour une règle `TacheMultiple`) ; la phrase fixe
  « Chaque feuille commence par une ligne de titres ; les données commencent à la ligne 2. » ; la
  phrase sur la dépendance au profil d'import ;
- une section par règle : titre « Feuille {nom} » (Équipement, Élément) ou « Feuilles par type de
  tâche (règle « {nom} ») » (`TacheMultiple`) ; phrase(s) fixe(s) de lignes selon la source :
  Équipement « Une seule ligne : l'équipement. », Élément « Une ligne par élément, dans l'ordre des
  feuilles ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS. »,
  `TacheMultiple` les deux phrases du §5 ;
- règle sans aucune colonne : phrase « Aucune colonne. ».

**Rouge** (`ExportProfileDescriptionBuilderSheetTests`) : un cas par source ; profil à 3 règles
(ordre des feuilles dans la section 0 et ordre des sections) ; `IsFixed` vrai exactement sur les
phrases fixes ; règle sans colonne.

**Vert** : modèle rempli, clés `ExportProfileDetails_Workbook*` et `ExportProfileDetails_Sheet*`.

**Fait (2026-09-17)** : `ExportProfileDescriptionBuilder.cs` (sections classeur et règles), 14 clés.
Liste des feuilles : phrase au singulier pour une règle (« Le fichier contient la feuille « Parents ». »),
sinon « Le fichier contient, dans cet ordre : … et … » — **écart avec le §5** (« les feuilles « Parents »,
« Enfants », puis une feuille par type de tâche »), à aligner en 79.9. Les codes `TM_PROC_MAD`/`TM_PROC_REL`
de la phrase fixe sont des valeurs mises en évidence. Support de test : `Describe(ExportProfile)`,
`ExportRule`, `ExportProfile` ajoutés à `DescriptionTestSupport`. `ExportProfileDescriptionBuilderSheetTests` :
7/7 (rouge = compilation).

### 79.4 — Colonnes descriptives et constantes (D1)

**Comportement** : une phrase par colonne, dans l'ordre de `ExportColumnLayout`, après les phrases de
79.3 : « Colonne {lettre} « {titre} » : {contenu}. », la lettre en segment `CellReference`, le titre
en segment `Value`.
- `Source` renseignée : libellé du champ, clé `ExportProfileDetails_Field_{PivotFieldRef}` (contenu du
  tableau du §1, formulé comme dans le catalogue §5) ;
- `Source = null` : « toujours vide » ;
- constante : « toujours « {valeur} » » ;
- la colonne de source `TacheMultipleCritere` est une phrase `IsFixed` (valeurs codées en dur, §1).

**Rouge** (`ExportProfileDescriptionBuilderColumnTests`) : colonnes renseignée, vide et constante
sur chaque source ; lettres au-delà de `Z` ; `CRITERE` fixe et aucune autre colonne fixe ;
**`[Theory]` sur toutes les valeurs de `PivotFieldRef`** : la clé de libellé existe (texte localisé
différent du nom de clé) — un nouveau champ ajouté au domaine sans libellé fait échouer ce test.

**Vert** : clés `ExportProfileDetails_Column*` et les 27 `ExportProfileDetails_Field_*`.

**Fait (2026-09-17)** : `DescribeColumns`/`ColumnSentence` dans `ExportProfileDescriptionBuilder`, 30 clés.
« A faire »/« Pour info » passés en valeurs mises en évidence. `ExportProfileDescriptionBuilderColumnTests` :
32/32 (rouge vérifié : 32 échecs) ; non-vacuité du `[Theory]` vérifiée en renommant une clé de libellé
(1 échec). Constructeur d'export complet : 39/39.

### 79.5 — Colonnes d'applications et de points (D1, D2)

**Comportement** :
- application : « Colonne {lettre} « {titre} » : « {coche} » si {l'équipement | l'élément} est
  rattaché à l'application « {application} », sinon vide. » ;
- points **regroupés** (D2) : les colonnes de points d'une règle dont le `Header` est égal au
  `ColonneNom` forment un groupe par valeur de coche ; un groupe d'au moins deux colonnes donne une
  seule phrase « Colonnes {première} à {dernière} : « {coche} » si … est coché dans la colonne du
  même nom à l'import, sinon vide : « {c1} » ({l1}), « {c2} » ({l2})… », placée à la position de sa
  première colonne ; colonnes du groupe non consécutives : « Colonnes {l1}, {l2}… » au lieu de
  « {première} à {dernière} » ;
- autres colonnes de points (titre différent du nom, ou groupe d'une seule colonne) : une phrase
  chacune, « Colonne {lettre} « {titre} » : « {coche} » si … est coché dans la colonne « {nom} » à
  l'import, sinon vide. » ;
- sujet selon la source : Équipement « l'équipement, ou au moins un de ses éléments, » ; Élément
  « l'élément », avec « (nom identique, majuscules comprises) » (écart du §1).

**Rouge** (`ExportProfileDescriptionBuilderPointTests`) : Parents et Enfants du §5 réduits à trois
colonnes de points ; deux valeurs de coche → deux groupes ; une colonne dont le titre diffère du
nom ; un groupe d'une seule colonne ; colonnes du groupe non consécutives ; application sur chaque
source.

**Vert** : clés `ExportProfileDetails_Application*` et `ExportProfileDetails_Point*`.

**Fait (2026-09-17)** : `GroupPointColumns`/`DescribePointGroup` dans `ExportProfileDescriptionBuilder`, 9 clés.
Groupe par valeur de coche exacte ; lettres non consécutives jointes par « , » et « et » (« Colonnes A, C
et D »). `ExportProfileDescriptionBuilderPointTests` : 6/6 (rouge vérifié : 6 échecs) ; constructeur
d'export complet : 45/45.

### 79.6 — Configurations qui empêchent la génération (D3)

**Comportement** :
- `ExcelSheetNameRules` (`Formatting/`) : nom valide pour Excel/ClosedXML — 1 à 31 caractères, aucun
  de `\ / ? * [ ] :`, ne commence ni ne finit par `'` ; doublons comparés sans tenir compte de la
  casse.
- `Blocking` de la section d'une règle Équipement/Élément : nom trop long, caractère interdit (cité),
  apostrophe en début ou fin. Les règles `TacheMultiple` ne sont pas concernées (nom interne).
- `Blocking` de la section « Classeur généré » : deux règles Équipement/Élément de même nom à la
  casse près (les deux noms cités) ; plus d'une règle `TacheMultiple` ; une règle Équipement/Élément
  nommée `TM_PROC_MAD` ou `TM_PROC_REL` (casse ignorée) en présence d'une règle `TacheMultiple`, avec
  la mention « si le fichier contient des tâches de ce type ». Ces deux codes sont dupliqués depuis
  la conversion fixe de `ProcedureExtractionService`.
- Titre du bloc sur la page : « Problèmes qui empêchent la génération : ».

**Rouge** :
- `ExportProfileDescriptionBuilderBlockingTests` : un cas par ligne du tableau du §2, plus le nom
  avec espaces (aucun blocage).
- **Garde-fou contre ClosedXML** (`ExcelSheetNameRulesTests`) : pour chaque cas du tableau du §2,
  générer le classeur avec le vrai `SheetGenerationEngine` sur un `ImportResult` contenant au moins
  une tâche `TM_PROC_MAD` et une `TM_PROC_REL`, l'écrire avec le vrai `ClosedXmlWorkbookWriter`, et
  vérifier que l'écriture échoue **si et seulement si** `ExportProfileDescriptionBuilder` signale un
  blocage.

**Vert** : clés `ExportProfileDetails_Blocking*`.

### 79.7 — Page `/export-profiles/{Id:guid}/details`

**Comportement** : `ExportProfileDetails.razor`, `[Authorize]` sans rôle, charge le profil via
`IExportProfileStore.GetByIdAsync`, affiche `ProfileDescriptionView` avec
`BlockingHeading = ExportProfileDetails_BlockingHeading` ; `PageBackNavLink`
`back-to-export-profiles-button` vers `export-profiles` ; `h1` « Détails du profil « {nom} » » ;
id inconnu : `div#export-profile-details-not-found.alert.alert-danger` (`role="alert"`), aucune
section ; conteneur `container-fluid px-3 profile-editor-container` ; aucun contrôle hors retour.

**Rouge** :
- bUnit (`ExportProfileDetailsTests`) : sections et phrases depuis un profil minimal en store EF
  InMemory ; lettre en `code.profile-details-cell`, titre en `strong.profile-details-value` ;
  badge « non modifiable » sur une phrase fixe seulement ; bloc bloquant présent ou absent, aucun
  bloc ignoré ; profil introuvable ; lecture seule ; hiérarchie des titres ; retour à la liste.
- HTTP : route `/export-profiles/00000000-0000-0000-0000-000000000001/details` ajoutée à
  `BusinessRoutes` de `BusinessPageAuthorizationHttpTests`, et test dédié du corps contenant
  `export-profile-details-not-found`.

**Vert** : la page, clés `ExportProfileDetails_PageTitle` / `_PageTitleGeneric` / `_NotFound` /
`_BackToListButton` / `_BlockingHeading`.

### 79.8 — Bouton « Voir les détails » sur la liste des profils d'export (D6)

**Comportement** : `ExportProfiles.razor`, tableau et carte mobile, bouton icône seule placé avant
« Modifier » : ids `details-export-profile-button-{id}` et `details-export-profile-button-card-{id}`,
classe `btn btn-outline-secondary btn-sm block-field-icon-btn`, `AdminIconMarkup.Eye`,
`aria-label`/`title` = `ExportProfiles_Details` (EN « View details » / FR « Voir les détails » : même
écart à D7 que le lot 078, libellé de la liste existante, déjà bilingue). Clic →
`export-profiles/{id}/details`. Masqué pendant une confirmation de suppression ou de
réinitialisation.

**Rouge** (`ExportProfilesTests.cs`) : navigation tableau et carte ; extension de
`RowActionButtons_AreIconOnly_WithAriaLabelAndTitle_InBothTableAndCardTemplates` ; position juste
avant `edit-export-profile-button-{id}` ; absent pendant une confirmation de suppression.
`ProfileListPageParityTests` : même classe pour le bouton détails des deux listes.

**Vert** : bouton, clé `ExportProfiles_Details`.

### 79.9 — Clôture : catalogue du profil semé

**Comportement** : aucun nouveau code attendu ; le test fige le catalogue.

**Rouge/vert** (`ExportProfileDescriptionBuilderSeededProfileTests`) : profil d'export semé relu par
`IExportProfileStore`, `Build` en `fr-FR` : chaque section produit exactement les phrases du §5,
dans l'ordre, `IsFixed` sur les phrases « (fixe) », `Blocking` et `Ignored` vides. Le §5 est d'abord
confronté à la sortie réelle et réécrit pour lui être identique si un écart voulu est apparu en
79.3 à 79.6. Non-vacuité : modifier une lettre attendue fait échouer le test.

**Puis** : suite `ExcelETL.BlazorAdmin.Tests` complète ; `CLAUDE.md` (« CURRENT SOLUTION STATE ») ;
route ajoutée au tableau de `convention-autorisation-pages-blazoradmin.md`.

### Hors périmètre de 79.1 à 79.9

- Traduction anglaise des clés `ExportProfileDetails_*` (D7, lot ultérieur).
- Contrôle croisé avec les profils d'import (D4).
- Toute correction des cas bloquants ou de l'écart de comparaison des points (§1, §2) : domaine,
  moteur, écriture et éditeur inchangés.
- Lien vers la vue Détails depuis l'éditeur ou les pages de test ; export PDF/impression.
