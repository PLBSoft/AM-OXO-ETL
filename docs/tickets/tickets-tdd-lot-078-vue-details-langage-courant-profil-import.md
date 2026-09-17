# Tickets TDD — Lot 078 : vue "Détails" en langage courant du profil d'import

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Prolonge le
travail du lot 048 (règles d'en-tête) et de `tickets-tdd-blazor-profil-import-edition-listes-conditionnelles.md`
(édition des sous-listes `UnconditionalColonneNames`/`ConditionalPointRule`) : ces lots ont rendu
les règles éditables et visibles techniquement (`<details>` du 48.6), mais dans le vocabulaire du
domaine (`SheetExtractionRule`, `StripReperePrefix`, plages Excel brutes). Ce lot ajoute une
**deuxième vue, séparée**, destinée à un utilisateur qui n'est ni développeur ni expert de
l'application : une reformulation en langage courant des mêmes règles.*

**Objet** : page de lecture seule `/import-profiles/{id}/details`, accessible depuis
`ImportProfiles.razor` par un nouveau bouton `#import-profile-details-button-{id}`, qui traduit
paramètres généraux + règles de chaque feuille en phrases métier. Ne remplace **rien** d'existant
— le `<details>` technique du lot 048 (§48.6) reste en place tel quel.

**Hors périmètre explicite de ce lot** :
- `ExportProfile` — lot séparé, après validation de l'approche sur l'import (accord déjà donné).
- Toute modification du domaine (`ImportProfile`, `SheetExtractionRule`, etc.) — ce lot est
  strictement présentation, aucune règle n'est reconstruite ni revalidée différemment.
- Toute modification de l'éditeur (`ImportProfileEditor.razor`/`SheetRuleForm.razor`) ou du
  `<details>` du 48.6 — nouvelle page, nouveau composant, zéro retouche du code existant.

---

## 78.0. Investigation préalable (obligatoire avant tout code — pas de sous-ticket rouge/vert avant validation)

**Nature de ce ticket** : il ne produit pas de code de production. Il produit une **note de
conception** (ajoutée en conclusion de cette section, même format que les investigations
précédentes du dépôt — voir `tickets-tdd-blazor-profil-import-edition-listes-conditionnelles.md`
§"Conclusions de l'investigation") qui sera validée avant que les sous-tickets 78.1+ soient
rédigés. Ne pas anticiper l'implémentation.

**À confirmer par lecture directe du dépôt réel** (ne pas supposer que les descriptions
ci-dessous, issues des tickets historiques, sont encore exactes — le dépôt a probablement avancé
au-delà de ce que ce contexte reflète) :

1. **Inventaire exhaustif des champs à traduire**, avec leur forme réelle actuelle :
   - Racine `ImportProfile` : `Name`, `ReperePrefix`, `EquipementTypeElementNom`.
   - `SheetExtractionRule` : `UnconditionalColonneNames`, `ConditionalPointRule`
     (`SourceFieldName`, `Operator`, `ComparisonValue`, `ColonneName`), `HeaderFieldRule`
     (`Name`, `Cell` (`DirectCell.Sheet`/`.Range`), `StripReperePrefix`, `DateFormat`),
     `HeaderCompositeRule` (`Name`, `Template`, `PlaceholderNames()`).
   - **Décision à confirmer, pas à supposer** : `RepeatingBlockLocator` et `BlockFieldDefinition`
     (mécanique d'extraction des blocs répétitifs) entrent-ils dans le périmètre de la vue
     Détails, ou restent-ils un détail d'implémentation trop technique pour l'utilisateur cible
     (probable, à faire trancher explicitement plutôt que d'omettre silencieusement) ?
   - Lister les valeurs réelles de l'énumération `ConditionOperator` (le glossaire et les tickets
     historiques ne mentionnent que `Equals`/`NotEquals` — confirmer qu'il n'y en a pas d'autre
     avant de figer les gabarits de phrase).

2. **Conventions UI existantes à réutiliser** : lire `convention-ui-blazor-icones-boutons.md` et
   `convention-ui-blazor-alignement-boutons.md` pour le bouton `#import-profile-details-button-{id}`
   sur `ImportProfiles.razor` ; confirmer le style de routing des pages en lecture seule existantes
   (vérifier si une page 100 % lecture seule existe déjà ailleurs dans `BlazorAdmin`).

3. **Dictionnaire de phrases métier** : n'existe pas encore (confirmé). Proposer :
   - son emplacement (probablement `ExcelETL.BlazorAdmin`, pas `Application` — c'est de la
     présentation, pas une règle métier réutilisable côté API) ;
   - sa forme (classe statique de gabarits + clés `.resx` paramétrées façon
     `…_WellKnownHeaderNamesMissingWarning`, ou un service dédié type
     `ImportProfileDescriptionBuilder`) ;
   - comment il gère les deux ressources EN/FR (même contrainte que le 48.7).

4. **Algorithme de regroupement des `ConditionalPointRule`** : proposer la clé de regroupement
   (`SourceFieldName` + `Operator` + `ComparisonValue`) et le gabarit de phrase résultant pour un
   groupe à N colonnes (N=1 cas simple, N>1 cas SOUPAPE/POINT FEU) — avec un exemple construit sur
   les données réelles du profil OXO actuellement seedé, pas un exemple inventé.

5. **Catalogue de gabarits complet**, feuille par feuille du profil OXO seedé (PROCEDURE,
   ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS), pour validation
   humaine avant d'écrire le moindre test — chaque feuille rendue en phrases complètes, pas en
   pseudo-code de gabarit.

**Livrable attendu de 78.0** : la note de conception ci-dessus complétée avec les réponses
réelles, plus le catalogue de gabarits du point 5 rendu en clair sur les 6 feuilles du profil
OXO. Aucun sous-ticket 78.1+ n'est écrit avant validation explicite de cette note.

---

### Conclusions de l'investigation (2026-09-17) — note de conception à valider

*Tout ce qui suit a été relu dans le code au commit `f4a20f3`, pas repris des tickets historiques.
Plusieurs descriptions du contexte initial étaient périmées : elles sont corrigées au fil du texte
(marquées **Écart**).*

#### 1. Inventaire réel des champs

**Racine `ImportProfile`** (6 champs métier, pas 3) :

| Champ | Forme | Effet réel (lu où) |
| :--- | :--- | :--- |
| `Name` | texte ≤ 60 | aucun effet d'extraction, titre de la page |
| `ReperePrefix` | texte, défaut `"OXO-"` | retiré de chaque `HeaderFieldRule` marquée `StripReperePrefix` ; une valeur qui ne commence pas par ce préfixe (comparaison **sensible à la casse**, `StartsWith` ordinal) fait refuser le fichier entier (`ProcedureExtractionService`) |
| `EquipementTypeElementNom` | texte | type d'élément de l'équipement créé |
| `DefaultTableaux` | liste de textes | un Point par nom sur l'**équipement seulement** (`ProcedureExtractionService`), et liste diffusée sur l'équipement et tous les éléments (`ImportPipelineOrchestrator`) |
| `DefaultApplicationNames` | liste de textes | diffusée sur l'équipement et tous les éléments |
| `TacheMultipleTypeLabels` | liste `(Code, Label)` | libellé « Colonne Travaux » d'une tâche multiple selon son code |

**Écart** : le contexte initial parlait d'un préfixe `MAD-OXO-` ; la valeur réelle par défaut
(`ImportProfile.DefaultReperePrefix`) est `"OXO-"`.

**`SheetExtractionRule`** (12 membres, pas 5) : `SheetName`, `Locator` (`RepeatingBlockLocator` :
`FirstBlockStartRow`, `Step`, `StopFieldName`, `Fields` de `BlockFieldDefinition` :
`Name`/`ColumnRange`/`RowOffsetStart`/`RowOffsetEnd`), `PointRules` (`ConditionalPointRule` :
`SourceFieldName`/`Operator`/`ComparisonValue`/`ColonneName`), `UnconditionalColonneNames`,
`HeaderFields` (`HeaderFieldRule` : `Name`/`Cell` (`DirectCell` `Sheet`+`Range`)/`StripReperePrefix`/
`DateFormat`), `HeaderComposites` (`HeaderCompositeRule` : `Name`/`Template`/`PlaceholderNames()`),
et 5 membres absents de l'inventaire initial : `FieldPresencePointRules` (`Cell`/`ColonneName`/
`ExpectedValue?`), `ZeroEnergieExpectedValue?`, `CouleurEtiquetteCell?`, `DefaultCouleurEtiquette?`,
`AllowedCouleursEtiquette?`.

**`ConditionOperator`** : exactement `Equals` et `NotEquals` (confirmé). La comparaison se fait après
`Trim`, **insensible à la casse** (`ConditionalPointRuleEvaluator`).

**Constat structurant : chaque service ne lit qu'une partie de la règle.** Le rôle d'une règle vient
uniquement de son `SheetName` (6 noms littéraux dans `ImportPipelineOrchestrator`) :

| Membre lu | PROCEDURE | ISOLEMENT | PLATINES / ORIFICES CAPACITES | AUTRES JOINTS TOUCHES | DIVERS |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `Locator` (+ `Fields` par nom) | oui | oui | oui | oui | oui |
| `HeaderFields`/`HeaderComposites` | oui (`nomMAD`, `dateRev`, `Designation`) | **non** | **non** | oui (`repereEcho`) | oui (`repereEcho`) |
| `UnconditionalColonneNames` | **non** | oui | oui | oui | oui |
| `PointRules` | **non** | oui | **non** | oui | oui |
| `FieldPresencePointRules` | non | non | oui | non | non |
| `ZeroEnergieExpectedValue` | non | oui | non | non | non |
| Couleur (cellule/défaut/autorisées) | non | non | oui | oui | non |

Un membre renseigné dans une case « non » est **enregistré mais sans effet**. Cas réel : le 16/09 le
client avait ajouté « VISITE PRÉALABLE CHANTIER » aux colonnes inconditionnelles de PROCEDURE, sans
effet (voir le commentaire de `DefaultProfileSeeder`). Le profil seedé actuel, lui, ne contient aucun
membre sans effet.

**Comportements fixes, hors profil** (codés dans les services, non modifiables par l'admin) :
- repère d'un élément = `K6:T6` (ISOLEMENT) ou `K6:U6` (PLATINES, ORIFICES CAPACITES), un tiret, puis
  l'identifiant ; AUTRES JOINTS TOUCHES et DIVERS utilisent `repereEcho` du profil à la place ;
- zone (`loc1`) lue en `B6:E6` de DIVERS, appliquée à l'équipement et à tous les éléments ;
- type de tâche PROCEDURE `MAD` → `TM_PROC_MAD`, `REL` → `TM_PROC_REL` (autre valeur gardée telle
  quelle) ;
- tâche sans ordre = ligne de titre de section ;
- date de révision illisible → fichier refusé.

**Regroupement côté moteur** : `ConditionalPointGroupEvaluator` regroupe par `ColonneName`, avec un
**OU** entre les règles d'une même colonne, et émet un avertissement non bloquant quand aucune
colonne conditionnelle n'est cochée pour un élément.

#### 2. Conventions UI à réutiliser

- Bouton de ligne de `ImportProfiles.razor` : icône seule, `btn btn-outline-secondary btn-sm
  block-field-icon-btn`, `aria-label` + `title`, à côté de Modifier/Dupliquer/Réinitialiser/Supprimer ;
  doublé dans la carte mobile avec un id suffixé `-card-` (convention V2). La matrice de
  `convention-ui-blazor-icones-boutons.md` classe ce cas en « ligne de grille/tableau → icône ».
  Aucune icône « voir » n'existe dans `AdminIconMarkup` : il faudra une constante `Eye` (forme
  `bi-eye`, SVG en ligne).
- **Écart de nommage à trancher** : les ids de ligne de cette page suivent
  `{action}-profile-button-{id}` (`edit-profile-button-{id}`, `duplicate-profile-button-{id}`…).
  L'id demandé, `import-profile-details-button-{id}`, s'en écarte. Proposition :
  `details-profile-button-{id}` et `details-profile-button-card-{id}`.
- **Aucune page de consultation d'une seule entité en lecture seule n'existe** dans `BlazorAdmin`
  (seules `Home`, `Error`, `NotFound`, `AccessDenied` sont sans interaction). Ce serait la première.
  Modèle proposé, calqué sur l'éditeur : route `/import-profiles/{Id:guid}/details`, `[Authorize]`
  sans rôle (page métier, `convention-autorisation-pages-blazoradmin.md`), `PageBackNavLink` vers la
  liste, alerte « profil introuvable » pour un id inconnu (comme `ImportProfileEditor`).

#### 3. Dictionnaire de phrases

- **Emplacement** : `ExcelETL.BlazorAdmin/Formatting/ImportProfileDescriptionBuilder.cs`, à côté de
  `BlockFieldRangeFormatter`/`FieldPresencePointRuleFormatter`. Présentation pure, rien dans
  `Application`.
- **Forme** : classe statique, entrée `ImportProfile` + `IStringLocalizer<BlazorAdminMessages>`,
  sortie un modèle structuré (sections → phrases en texte), jamais du markup. Testable sans bUnit
  avec le vrai `.resx`. Pas de service DI : aucune dépendance autre que le localiseur.
- **Ressources** : clés `ImportProfileDetails_*` paramétrées (`{0}`, `{1}`…) dans
  `BlazorAdminMessages.resx`/`.fr.resx`, même mécanique que
  `ImportProfileEditor_WellKnownHeaderNamesMissingWarning`. Les valeurs du profil (noms de colonnes,
  valeurs comparées) restent des données, jamais traduites.
- **Libellés de champs** : table de correspondance des noms techniques connus vers un libellé métier
  (`Identification` → « identifiant », `TypeElement` → « type d'élément », `Designation` →
  « désignation », `PositionALaPose` → « position à la pose », `HasZeroEnergie` → « indicateur zéro
  énergie », plus `Action`/`Ordre`/`Acteur`/`Risques`/`TypeTacheMultipleAlias`/`DateValidation`).
  Un nom inconnu (saisi par l'admin) s'affiche tel quel entre guillemets.
- **Connaissance du rôle des feuilles** : le tableau du §1 (qui lit quoi) et les comportements fixes
  sont codés dans les services, pas dans le profil. Le constructeur de phrases devra les dupliquer,
  comme `KnownHeaderFieldNames` le fait déjà (précédent du lot 048). À valider (décision D2).

#### 4. Regroupement des `ConditionalPointRule`

- **Clé** : (`SourceFieldName`, `Operator`, `ComparisonValue` après `Trim`, insensible à la casse —
  même normalisation que le moteur), dans l'ordre de première apparition.
- **Gabarits** :
  - N = 1, `Equals` : « Si le {champ} est « {valeur} », l'élément est coché dans la colonne
    « {colonne} ». »
  - N > 1, `Equals` : « Si le {champ} est « {valeur} », l'élément est coché dans les {N} colonnes
    « {c1} », « {c2} »… »
  - `NotEquals` : mêmes gabarits avec « n'est pas ».
  - Phrase de clôture si la feuille a au moins une règle : « Un élément qui ne remplit aucune de ces
    conditions est importé normalement, avec un avertissement. »
- **Cas `HasZeroEnergie`** : la règle seedée « `HasZeroEnergie` = `true` » n'a pas de sens lue
  littéralement. Elle est fusionnée avec `ZeroEnergieExpectedValue` et la cellule du champ en une
  seule phrase (voir ISOLEMENT ci-dessous).
- **OU implicite** : ce regroupement est l'inverse de celui du moteur (par colonne). Une colonne
  visée par deux groupes apparaît dans deux phrases, ce qui se lit correctement « cochée si A, ou si
  B ». Aucun cas dans le profil seedé.
- **Même principe pour `FieldPresencePointRules`** : clé (`ColonneName`, `ExpectedValue`), les
  cellules du groupe listées avec « ou ».
- **Exemple réel (DIVERS seedé)** : 7 règles → 4 phrases (INSTRUMENTATION ×1, ZERO ENERGIE ×1,
  SOUPAPE ×2, POINT DE FEU ×3). **Écart** : la valeur seedée est « POINT DE FEU », pas « POINT FEU ».

#### 5. Catalogue rendu sur le profil seedé « Profil OXO standard »

*Plages indiquées pour le premier bloc, calculées comme `BlockFieldRangeFormatter.ToAbsoluteRange`.
Les phrases marquées « (fixe) » décrivent un comportement codé, non modifiable dans le profil
(décision D3).*

**Paramètres généraux**
- Le repère de l'équipement est lu dans la feuille PROCEDURE. Il doit commencer par « OXO- » (majuscules comprises), qui est retiré ; sinon le fichier entier est refusé.
- L'équipement est créé avec le type d'élément « MAD TRAVAUX ».
- L'équipement est coché dans les 3 colonnes « TRAVAUX COMPLET », « TRAVAUX DETAIL », « VISITE PRÉALABLE CHANTIER ». L'équipement et tous ses éléments sont rattachés à ces 3 tableaux.
- L'équipement et tous ses éléments sont rattachés à l'application « PROGRESS ».
- Une tâche de type « TM_PROC_MAD » est écrite dans la colonne travaux « Procédure MAD ».
- Une tâche de type « TM_PROC_REL » est écrite dans la colonne travaux « Procédure REL ».

**Feuille PROCEDURE**
- En-tête : le repère de l'équipement (« nomMAD ») est lu en M2:O2, préfixe « OXO- » retiré.
- En-tête : la révision (« revision ») est lue en P2:Q2.
- En-tête : la date de révision (« dateRev ») est lue en R2:T2 et écrite au format jj/mm/aaaa. Une date illisible fait refuser le fichier entier (fixe).
- La désignation de l'équipement suit le modèle « Rév {revision} du {dateRev} », où {revision} et {dateRev} sont remplacés par les valeurs ci-dessus.
- Une tâche est lue par ligne à partir de la ligne 9. La lecture s'arrête à la première ligne dont l'action est vide.
- Pour chaque tâche : ordre en B9, action en C9:L9, acteur en M9:N9, risques en O9:Q9, type en R9, date de validation en T9:U9.
- Un type « MAD » devient « TM_PROC_MAD », un type « REL » devient « TM_PROC_REL » (fixe).
- Une ligne sans ordre est un titre de section, pas une tâche à réaliser (fixe).

**Feuille ISOLEMENT**
- Un élément est lu toutes les 7 lignes à partir de la ligne 19. La lecture s'arrête au premier bloc dont l'identifiant est vide.
- Pour le premier élément : identifiant en B19:E20, désignation en H18:U19, position à la pose en H20:O21, type d'élément en B22:E23, indicateur zéro énergie en V18:V19.
- Le repère de l'élément est la cellule K6:T6, un tiret, puis l'identifiant (fixe).
- Chaque élément est coché dans les 2 colonnes « PROLOCK VANNES », « DEPROLOCK VANNES ».
- Si l'indicateur zéro énergie contient « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ». Toute autre valeur non vide donne un avertissement.
- Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.

**Feuille PLATINES**
- Un élément est lu toutes les 8 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.
- Pour le premier élément : identifiant en B17:E18, désignation en H16:V17, type d'élément en B20:E22.
- Le repère de l'élément est la cellule K6:U6, un tiret, puis l'identifiant (fixe).
- Chaque élément est coché dans les 5 colonnes « POSE ÉTIQUETTES », « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS », « RÉCEPTION PLATINES/TAMPONS PLEINS », « PLATINES / TAMPONS PLEINS ».
- Si la cellule H19:N19 ou H20:N20 contient « DEBUT MAD », l'élément est coché dans la colonne « RECEPTION DEBUT MAD ».
- Si la cellule H19:N19 ou H20:N20 contient « DEBUT REL », l'élément est coché dans la colonne « RECEPTION DEBUT REL ».
- La couleur d'étiquette est lue en H18:N18. Couleurs acceptées : ROUGE, BLANC, JAUNE, VERT, BLEUE. Une autre valeur est ignorée, avec un avertissement.

**Feuille ORIFICES CAPACITES**
- Un élément est lu toutes les 8 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.
- Pour le premier élément : identifiant en B17:E18, désignation en H16:V17, type d'élément en B20:E22.
- Le repère de l'élément est la cellule K6:U6, un tiret, puis l'identifiant (fixe).
- Chaque élément est coché dans les 4 colonnes « POSE ÉTIQUETTES », « RÉCEPTION PLATINES/TAMPONS PLEINS », « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS ».
- La couleur d'étiquette est lue en H18:N18. Couleurs acceptées : ROUGE, BLANC. Une autre valeur est ignorée, avec un avertissement.

**Feuille AUTRES JOINTS TOUCHES**
- Un élément est lu toutes les 7 lignes à partir de la ligne 17. La lecture s'arrête au premier bloc dont l'identifiant est vide.
- Pour le premier élément : identifiant en B17:E18, désignation en F16:Y17, type d'élément en B20:E21.
- En-tête : le repère de l'équipement (« repereEcho ») est lu en N6. Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.
- Chaque élément est coché dans les 2 colonnes « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS ».
- Si le type d'élément n'est pas « TUBING », l'élément est coché dans la colonne « POSE ÉTIQUETTES ».
- Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.
- La couleur d'étiquette de chaque élément est toujours « BLEUE ».

**Feuille DIVERS**
- Un élément est lu toutes les 3 lignes à partir de la ligne 9. La lecture s'arrête au premier bloc dont l'identifiant est vide.
- Pour le premier élément : type d'élément en B9:G11, identifiant en H9:K11, désignation en L9:V11.
- En-tête : le repère de l'équipement (« repereEcho ») est lu en N6. Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.
- La zone lue en B6:E6 est appliquée à l'équipement et à tous les éléments du fichier (fixe).
- Si le type d'élément est « INSTRUMENTATION », l'élément est coché dans la colonne « SYNCHRONISATION INSTRUMENTATION ».
- Si le type d'élément est « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ».
- Si le type d'élément est « SOUPAPE », l'élément est coché dans les 2 colonnes « SOUPAPE : CONSTAT ENCRASSEMENT », « SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS ».
- Si le type d'élément est « POINT DE FEU », l'élément est coché dans les 3 colonnes « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES », « PF : VALIDATION CONSTAT ENCRASSEMENT », « PF : ACCORD TRAVAUX FEU ».
- Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.

#### 6. Décisions à trancher avant d'écrire 78.1+

- **D1 — Blocs répétés (`RepeatingBlockLocator`/`BlockFieldDefinition`)** : recommandation, les
  **inclure** (« lu toutes les N lignes… », « pour le premier élément : … ») sous forme de plages
  Excel absolues du premier bloc. Un utilisateur non expert sait lire une cellule Excel, et c'est ce
  qui lui permet de comprendre pourquoi une valeur n'est pas importée. Option : les omettre.
- **D2 — Profil tel qu'enregistré ou comportement réel** : recommandation, ne décrire que ce que la
  feuille utilise réellement (tableau du §1) et lister à part les membres renseignés mais sans effet
  (« Configuré mais ignoré pour cette feuille : … »). Coût : dupliquer côté `BlazorAdmin` la
  connaissance « quelle feuille lit quoi », comme `KnownHeaderFieldNames`. Option : tout décrire
  littéralement, au risque d'annoncer une colonne cochée qui ne l'est jamais.
- **D3 — Comportements fixes hors profil** : recommandation, les afficher, marqués comme non
  modifiables (catalogue ci-dessus). Option : ne décrire que le profil.
- **D4 — Id du bouton** : `import-profile-details-button-{id}` (demandé) ou
  `details-profile-button-{id}` (convention de la page), avec la variante `-card-`.
- **D5 — Anglais** : le catalogue est rédigé en français ; l'anglais suivra les mêmes gabarits via
  le `.resx`, sans relecture séparée prévue. À confirmer.

#### 7. Décisions actées (Simon, 2026-09-17)

- **D1 — acté : oui, avec plages Excel.** Chaque feuille décrit le pas, la ligne de départ, la
  condition d'arrêt et les cellules du premier bloc, affichés directement (pas de section repliée).
- **D2 — acté : effet réel + avertissement.** La vue ne décrit que ce que la feuille utilise
  réellement ; les réglages enregistrés mais ignorés sont listés à part sous « Configuré mais ignoré
  pour cette feuille ». La connaissance « quelle feuille lit quoi » (tableau du §1) est dupliquée côté
  `BlazorAdmin`, sur le modèle de `KnownHeaderFieldNames`.
- **D3 — acté : oui, marqués « non modifiable ».** Les comportements codés en dur sont mêlés aux
  autres phrases de la feuille, avec cette mention.
- **D4 — acté : `details-profile-button-{id}`**, et `details-profile-button-card-{id}` pour la carte
  mobile. Remplace l'id `import-profile-details-button-{id}` de l'objet du ticket.
- **D5 — acté : français seulement pour l'instant.** Les clés anglaises (`BlazorAdminMessages.resx`)
  reprennent provisoirement le texte français ; traduction à faire dans un lot ultérieur.
  **Conséquence connue et acceptée** : `en-US` est la culture par défaut de BlazorAdmin, donc un
  utilisateur qui n'a pas choisi le français sur `/profile` verra cette page en français, au milieu
  d'une interface en anglais.

Prochaine étape : rédiger les sous-tickets 78.1+ à partir de cette note.
