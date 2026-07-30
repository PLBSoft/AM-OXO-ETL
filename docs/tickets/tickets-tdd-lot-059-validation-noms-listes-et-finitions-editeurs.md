# Tickets TDD — Lot 059 : validation des noms de listes, disposition deux colonnes et finitions d'éditeur

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 058.*

**Origine** : revue d'usage de Simon le 30/07 sur `/import-profiles/{id}/edit` et `/import-profiles`
(4 captures, profil « Profil OXO standard (Copie SLB) »), **après** livraison et push des lots 056,
057 et 058. Dix remarques, dont une explicitement **reportée** à une session dédiée (voir la dernière
section) et une qui s'est révélée être la même que sa voisine (voir 59.6).

Les remarques ont été énoncées « valables pour les profils d'import et d'export ». C'est vrai pour
quatre d'entre elles, **faux pour trois** : `ExportProfile` **n'a aucune collection de noms de
tableaux ni d'applications** (constat 3 ci-dessous). L'asymétrie est factuelle, pas un défaut de
parité — elle se documente comme le fait déjà `ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion`.

---

## Constats vérifiés dans le code (30/07, dépôt `C:\AM-OXO-ETL`, arbre au commit courant)

Tous les points ci-dessous ont été **lus** avant rédaction. Les numéros de ligne sont ceux du 30/07 :
repères, pas contrat.

1. **Le CTA final ne porte aucun `disabled`.** `ImportProfileEditor.razor:466` :
   `class="btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4 d-flex align-items-center justify-content-center gap-1"`,
   attributs `title="@Loc["ImportProfileEditor_SaveButtonShortcutHint"]"` et `@onclick="SaveProfileAsync"`.
   Miroir strict côté export (`:204`, id `save-export-profile-button`, **chaîne de classes identique au
   caractère près**). **Aucun test n'asserte aujourd'hui l'absence de `disabled`** — la nouvelle
   assertion ne contredit donc aucun test existant, elle s'ajoute.

2. **`_hasUnsavedChanges` est déjà exact.** Le lot 056 (56.3) a branché `OnDirty` sur les deux
   formulaires de feuille (`SheetRuleForm.razor:512-514`, `SheetGenerationRuleForm.razor:283-285`),
   invoqué depuis les 4 champs racine et les ~15 mutations de sous-liste. Le drapeau est levé en 15
   points côté import, 6 côté export, et retombe à `false` en **exactement deux** endroits :
   `SaveProfileAsync()` après un `SaveAsync` réussi, et `DiscardChangesAndLeave()`.
   **Conséquence directe** : la condition d'activation du CTA demandée par la remarque 1 existe déjà et
   est déjà testée. 59.4 la **consomme**, il ne la reconstruit pas.

3. **`ExportProfile` n'a ni tableaux ni applications au niveau du profil.**
   `ImportProfile.cs:35-36` : `public IReadOnlyList<string> DefaultTableaux { get; }` et
   `DefaultApplicationNames { get; }`. `ExportProfile.cs` n'expose que `Id`, `Name`, `SheetRules` — les
   noms d'application y vivent dans `ApplicationColumnDefinition`, **au niveau de la règle de feuille**,
   et y sont **déjà** protégés par `DomainErrorCode.SheetGenerationRule_DuplicateApplicationNom`.
   Ce code est le **précédent à imiter** en 59.1, pas un doublon à réunifier.

4. **Aucune validation de contenu sur ces deux collections aujourd'hui.** `ImportProfile.cs:92-93` ne
   fait que `ArgumentNullException.ThrowIfNull(...)`, puis copie défensive `[.. defaultTableaux]`
   (`:105-106`). Pas de non-vide par élément, pas de longueur, pas d'unicité, pas de trim, pas de
   normalisation de casse. `ImportProfile` **n'a aucune méthode de mutation** (commentaire explicite
   `:56-59`) : les collections sont alimentées **uniquement par constructeur**.
   `ImportProfile.MaxNameLength` (`:13`) redirige vers `ProfileNaming.MaxNameLength = 60` et concerne
   le **nom du profil** — c'est une limite distincte de celle demandée ici (50).

5. **Côté UI, la seule validation existante est le rejet du blanc.** `AddDefaultTableau()` (`:566`) et
   `AddDefaultApplicationName()` (`:578`) font un `string.IsNullOrWhiteSpace → return` **silencieux** ;
   l'édition en ligne affiche `ImportProfileEditor_EmptyTableauNameError` (`:601`) /
   `ImportProfileEditor_EmptyApplicationNameError` (`:638`). Deux chemins de validation pour la même
   règle, dont un muet. C'est ce que 59.2 unifie.

6. **`Shared/ProfileDuplicateNaming.cs` n'a aucun rapport** avec ce lot : il construit le nom d'un
   profil **dupliqué** (`"{Name} (Copy 2)"`), en comparant `OrdinalIgnoreCase` sur valeurs trimées.
   Sa **méthode de comparaison** est en revanche le précédent de casse à réutiliser en 59.1.

7. **Les blocs « Tableaux » et « Applications » sont deux blocs frères successifs**, pas une `row` :
   `<h2 class="h3">` + `<div class="card bg-light mb-3"><div class="card-body">`, enfants directs de
   `<div class="container-fluid px-3 profile-editor-container">` (`:36`). Tableaux : `:95-197`.
   Applications : `:199-293`, structure strictement identique. La ligne de saisie interne est déjà
   `row g-2` > `col-12 col-md` (champ) + `col-12 col-md-auto` (bouton `field-inline-action`),
   acquis 53.3 + 58.2.

8. **`sheet-rule-sublist-details` tire sa couleur d'un seul jeton.** `app.css:203-216` ; la seule
   couleur déclarée est `color: var(--bs-link-color)` (`:209`), sur `> summary`. Chaîne de résolution :
   `theme-m3.css:66` (clair) et `:168` (sombre) mappent `--bs-link-color` sur `--m3-primary`, qui vaut
   `#D81F11` en clair et `#FFB4AB` en sombre — **le rouge de la marque, celui des alertes**. C'est
   exactement la confusion signalée. `app.css` est **l'unique consommateur** de `--bs-link-color` :
   le remappage ne peut pas déborder. **Ne pas toucher `--m3-primary`**, consommé 25 fois dans
   `theme-m3.css` (dont `.btn-primary`, donc le CTA lui-même).
   La classe est posée en **deux points seulement** : `ImportProfileEditor.razor:339` et
   `ExportProfileEditor.razor:100`, verbatim. Le résumé est le `<summary>`
   `#sheet-rule-details-toggle-{index}`.

9. **Un seul vrai bouton d'ajout diverge du gabarit, et c'est la bascule du lot 057.** Les 12 boutons
   « Ajouter … » des deux éditeurs et de leurs 8 sous-formulaires portent tous
   `btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1` — à deux
   exceptions près, toutes deux voulues : `add-default-tableau-button` /
   `add-default-application-name-button` remplacent `mt-3` par `w-md-auto field-inline-action`
   (53.3 + 58.2). En revanche `toggle-add-sheet-rule-form-button` (`ImportProfileEditor.razor:438`) et
   `toggle-add-sheet-generation-rule-form-button` (`ExportProfileEditor.razor:177`) portent
   `btn btn-sm btn-outline-secondary d-flex align-items-center justify-content-center gap-1` :
   **ni `w-100`, ni fond plein**. C'est le seul bouton de la page à ne pas occuper la largeur de sa
   zone, **et** le seul bouton libellé « Ajouter … » en contour.
   → **Les remarques 2 et 8 de la revue désignent donc le même bouton** ; confirmé par Simon le 30/07.
   Elles sont traitées ensemble en 59.6.

10. **Les boutons de ligne des trois pages de liste sont tous en contour gris.**
    `ImportProfiles.razor`, `ExportProfiles.razor`, `Users.razor` : modifier, dupliquer, supprimer,
    réinitialiser — **tous** en `btn btn-outline-secondary btn-sm block-field-icon-btn`, dans le
    gabarit tableau **et** dans le gabarit carte (donc 2 occurrences par bouton).
    Dans les éditeurs, les boutons de suppression sont en
    `btn btn-sm btn-outline-danger block-field-icon-btn` (`delete-default-tableau-button-{index}` `:157`,
    `delete-default-application-name-button-{index}` `:261`, `delete-sheet-rule-button-{index}` `:417`,
    `delete-sheet-generation-rule-button-{index}` export `:160`).
    **Deux écarts, pas un** : la couleur, et **l'ordre des tokens** (`btn-sm` avant la couleur dans les
    éditeurs, après dans les listes). **Aucune constante ni composant partagé** n'existe pour ces
    boutons : les chaînes sont écrites littéralement dans chaque `.razor`. `AdminIconMarkup` ne mutualise
    que du SVG ; `.block-field-icon-btn` (`app.css:107-114`) est la seule mutualisation CSS.
    **Aucun test n'asserte aujourd'hui la chaîne de classes de ces boutons de ligne** — les tests
    existants (`RowActionButtons_AreIconOnly_WithAriaLabelAndTitle_InBothTableAndCardTemplates`, …)
    portent sur l'icône, le texte vide et `aria-label`/`title`. Il n'y a donc rien à corriger, seulement
    un garde-fou à créer.

11. **Le CTA n'a pas de clé de tooltip « rien à enregistrer ».** Existent :
    `*_SaveButton` (« Save profile »), `*_SaveButtonShortcutHint` (« Shortcut: Ctrl+Enter », utilisé
    comme `title`), `*_UnsavedChangesIndicator`. **À créer** en 59.4 : une clé de `title` pour l'état
    inactif, par éditeur.

12. **Aucune clé de message n'existe pour une longueur maximale ou un doublon de nom de tableau ou
    d'application.** Les seules voisines sont hors périmètre :
    `DomainErrorMessages.resx` → `ImportProfile_NameTooLong` = « Name must not exceed {0} characters. »
    (nom **du profil**) ; `ApplicationMessages.resx` → `ProfileNameAlreadyExists` (unicité **du nom de
    profil**, lot 027). Côté BlazorAdmin existent `ImportProfileEditor_EmptyTableauNameError` et
    `ImportProfileEditor_EmptyApplicationNameError`.

13. **Le mécanisme de localisation d'exception Domain est en place et suffit.**
    `BusinessExceptionLocalizer` (`src/ExcelETL.Application/Exceptions/BusinessExceptionLocalizer.cs`),
    injecté dans les deux éditeurs (`ImportProfileEditor.razor:15`) et consommé en
    `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message` (`:803`, `:807`). Convention de clé :
    **le nom du membre de `DomainErrorCode`, tel quel** (`DomainValidationException.cs:26` :
    `ResourceKey => ErrorCode.ToString()`). Aucun mécanisme nouveau n'est à inventer en 59.1.

14. **Un test d'audit lisant un fichier CSS existe déjà** :
    `tests/ExcelETL.BlazorAdmin.Tests/Styling/ThemeSecondaryButtonContrastTests.cs` (lot 58.1), qui
    remonte l'arborescence jusqu'à `ExcelETL.slnx` puis lit `theme-m3.css`. **59.5 réutilise cet
    idiome** sur `app.css` — il n'y a aucun prérequis à mettre en place.

**Vérifications résiduelles** (non lues, sans surprise attendue) : le corps exact des assertions de
`ImportProfileEditorTests.cs`, `ExportProfileEditorTests.cs`, `ImportProfilesTests.cs`,
`ExportProfilesTests.cs`, `UsersTests.cs` ; le contenu de `BlazorAdminMessages.fr.resx` et
`DomainErrorMessages.fr.resx` ; la configuration EF Core de `DefaultTableaux` /
`DefaultApplicationNames` dans `ExcelETL.Infrastructure` (à vérifier en 59.1 : si la persistance passe
par une conversion de valeur, le trim change la chaîne stockée).

---

## Décisions actées avec Simon (30/07)

| Sujet | Décision |
| :--- | :--- |
| CTA sans modification en attente | **`disabled` + `title` explicatif localisé**. Le bouton reste visible et à sa place : aucun saut de mise en page, la barre collante du 56.6 n'est pas rouverte. |
| Masquage du CTA | **Écarté** : provoque un déplacement de mise en page et vide la barre collante de son contenu principal. |
| Unicité + longueur des noms de Tableaux/Applications | **Règle portée par le Domain**, exceptions typées, messages via `BusinessExceptionLocalizer`. **Un seul chemin de validation.** |
| Blocage préventif du bouton d'ajout selon la saisie | **Écarté** : deux vérités de validation à maintenir, pour un gain de confort marginal. |
| Validation UI seule | **Écartée** : contournable par `POST /api/oxo/process`. |
| Profils existants porteurs de doublons | **Aucune migration, aucun nettoyage automatique.** Simon est en développement et repart d'une base vide au prochain lancement. Le rejet au premier enregistrement suffit. |
| Comparaison de casse | **Insensible à la casse** — `OrdinalIgnoreCase` sur valeurs trimées, comme `ProfileDuplicateNaming` (constat 6). |
| Bornes de longueur | **1 à 50 caractères**, mesurés **après trim**. Distinct des 60 caractères du nom de profil. |
| Bouton étroit de la remarque 2 | **C'est la bascule « Ajouter la feuille »** (constat 9), pas le CTA final. Le `w-md-auto` du CTA reste tel quel (53.4 / 56.6 non rouverts). |
| Couleur du résumé dépliable | **`var(--bs-secondary-color)`** — gris neutre du thème, lisible en clair et en sombre, sans parenté visuelle avec une alerte. L'affordance de dépliage reste portée par le curseur et le soulignement. |
| Bleu de lien dédié pour le résumé | **Écarté** : introduirait une couleur étrangère à la palette, exactement le reproche fait au bleu Bootstrap actuel. |
| Disposition Tableaux / Applications | **Les deux blocs entiers côte à côte** en `col-12 col-md-6` dans une même `row`, titre `h2` inclus dans chaque colonne. Sous 768px, empilement strictement inchangé. |
| Boutons de suppression des pages de liste | **Harmonisés en contour rouge**, sur les trois pages, dans les deux gabarits (tableau et carte). |
| Palette générale de l'application | **Hors de ce lot, gardée de côté** — voir la dernière section. |

---

## Décisions antérieures explicitement rouvertes par ce lot

- **56.6 / 53.4 (CTA final, apparence et attributs)** → rouvert **sur le seul attribut `disabled` et
  sur le `title`**. La chaîne de classes, la taille, la position dans la barre collante et
  `.right-aligned-actions` sont **conservés au caractère près**.
- **56.5 (`Ctrl+Entrée` enregistre le profil)** → rouvert : le raccourci doit désormais respecter la
  même condition que le clic. Un raccourci qui enregistre alors que le bouton est inactif serait un
  second chemin, donc une seconde vérité.
- **53.3 (ligne unique champ + bouton pour Tableaux et Applications)** → **non rouvert**. 59.3 place les
  deux blocs dans des colonnes ; la `row g-2` interne, ses `col-12 col-md` / `col-12 col-md-auto`,
  l'absence de `.right-aligned-actions` et l'interdiction d'`input-group` restent intactes.
- **58.2 (`field-inline-action`, hauteur du bouton à ≥768px)** → **non rouvert**. La règle s'applique à
  l'intérieur d'une colonne comme elle s'appliquait en pleine largeur. À **vérifier visuellement** en
  59.3 : c'est le seul risque de régression esthétique du sous-ticket.
- **Lot 057 (apparence de la bascule d'ouverture du formulaire d'ajout de feuille)** → rouvert **sur la
  chaîne de classes uniquement** (59.6). Le comportement de bascule, le remontage du formulaire, le
  commit-avant-changement et la règle « icône si fermé, pas d'icône si ouvert » sont **conservés**.
- **Lot 037, section hors périmètre (« toute extension de ce correctif aux boutons d'action de ligne
  des pages de liste — sujet distinct […] leur style visuel, qui est déjà cohérent entre les deux
  pages »)** → rouvert par 59.7. Le constat de 037 était exact — les listes sont cohérentes **entre
  elles** — mais elles divergent des éditeurs, et c'est cette divergence-là que Simon signale.
  Ce n'est pas une contradiction, c'est un périmètre élargi.
- **Le rejet silencieux du blanc dans `AddDefaultTableau` / `AddDefaultApplicationName`** (constat 5)
  → rouvert par 59.2 : il devient un message d'erreur visible, issu du Domain.

Tout le reste des lots 027 / 030 / 041 / 043 / 053 / 056 / 057 / 058 reste fermé. En particulier : le
conteneur 1140 px (53.1), le CTA `btn-primary btn-lg` (53.4), la barre collante (56.6), les jetons
`secondary-container` et le test de contraste WCAG (58.1), le gabarit icône + libellé (58.3).

---

## Conventions déjà en place à respecter (tout le lot)

Citées par leur nom de fichier, pas recopiées — voir `recommandations-tickets-tdd.md` §8 « un fait, un
endroit » : `convention-ui-blazor-alignement-boutons.md`,
`convention-ui-blazor-icones-boutons.md` (y compris la section « Icône + libellé : gabarit unique »
ajoutée au 58.3), `convention-nommage-documents.md`.

Rappels transverses du projet, non négociables ici : IDs HTML stables sur tout élément interactif,
jamais de sélection par texte ou position en bUnit ; bUnit ne calcule ni couleur ni layout (les tests
portent sur des classes, la structure DOM, le comportement, ou le **contenu déclaré** d'un fichier CSS
lu comme fichier) ; aucune nouvelle dépendance CSS/JS, CSS custom centralisé dans `app.css` et jamais
dupliqué dans deux `.razor.css` ; xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq +
bUnit 2.7.2 ; strict Red-Green-Refactor.

---

## Hors périmètre explicite (tout le lot)

- **La palette générale de l'application et le bleu Bootstrap du CTA** — reportés, voir dernière
  section. Ne rien « améliorer au passage ».
- **`--m3-primary`, `--bs-primary`, `.btn-primary`, `.btn-outline-primary`** — non touchés. 59.5 ne
  remappe **que** la couleur déclarée sur `.sheet-rule-sublist-details > summary`.
- **Les jetons `secondary-container` et le test de contraste du 58.1** — non touchés.
- **`ExportProfile`** — aucune collection de noms de tableaux ou d'applications n'y est **ajoutée**
  (constat 3). 59.1 à 59.3 sont **import-only**, et c'est définitif pour ce lot : créer une telle
  collection côté export serait une fonctionnalité spéculative, interdite par les instructions projet.
- **`SheetGenerationRule_DuplicateApplicationNom` et `SheetGenerationRule_DuplicateColonneNom`** —
  règles existantes, non revues, non réunifiées avec celles de 59.1. Elles portent sur un autre
  agrégat.
- **Le nom du profil** (60 caractères, unicité inter-profils du lot 027) — non touché.
- **Toute migration ou tout nettoyage de données existantes** — écartés par décision.
- **L'ajout d'une méthode de mutation à `ImportProfile`** — l'agrégat reste immuable (constat 4) ;
  59.1 n'introduit qu'un **validateur**, pas un `Add...`.
- **Le nombre minimal d'éléments dans les collections** : une liste de tableaux vide reste valide.
  `Constructor_WithEmptyTableauxAndApplicationNames_CreatesImportProfile` doit rester vert **sans
  modification** — c'est le garde-fou contre une sur-interprétation de « min 1 », qui porte sur la
  **longueur d'un nom**, pas sur la taille d'une collection.
- **L'intérieur des `form-floating`** (input avant label, `placeholder` non vide) — acquis 30.6,
  verrouillé par `FormFloatingStructureAuditTests`. Si une étape semble exiger de réordonner un
  `input` et son `<label>`, s'arrêter et le signaler.
- **`input-group`** — interdit (rouvrirait 30.6).
- **Le comportement de bascule du lot 057** — seule sa chaîne de classes change (59.6).
- **Les boutons d'en-tête des pages de liste** (`create-profile-button`, `test-*-profile-button`) et
  leurs tests de parité `HeaderButton*_CssClass_IsIdenticalBetween...` — non touchés par 59.7, qui ne
  traite que les boutons **de ligne**.
- **Les boutons Modifier / Dupliquer / Réinitialiser le mot de passe** des pages de liste — restent en
  contour gris. Seul le **bouton de suppression** passe en rouge : c'est l'action destructrice, et la
  distinguer n'a de sens que si les autres ne le sont pas.
- **`ReconnectModal.razor.css`** et ses couleurs figées hors thème — composant framework, écart connu.
- **`[Authorize]` et les routes des quatre pages éditeur** — inchangés. Les tests HTTP du lot 052
  (`BusinessPageAuthorizationHttpTests`) doivent rester verts **sans modification**.
- **Toute modification de pipeline d'extraction / génération** — hors 59.1, ce lot est strictement
  Razor + CSS + `.resx` + tests.

---

## 59.0. Investigation préalable (courte — les faits sont déjà établis)

La reconnaissance de code a été faite avant rédaction : les 14 constats ci-dessus sont **lus**, pas
supposés. Ne pas les re-vérifier un par un. Trois points seulement restent à établir :

- [ ] **Configuration EF Core des deux collections** : lire la configuration de `DefaultTableaux` /
  `DefaultApplicationNames` dans `ExcelETL.Infrastructure` (conversion de valeur ? table de
  jointure ?). C'est le seul inconnu susceptible de changer l'implémentation de 59.1 : si la
  persistance sérialise la chaîne telle quelle, le trim modifie la donnée stockée et il faut un test
  de repository.
- [ ] **Baseline verte** avant toute modification : `ImportProfileEditorTests`,
  `ExportProfileEditorTests`, `ImportProfilesTests`, `ExportProfilesTests`, `UsersTests`,
  `ProfileEditorParityTests`, `ImportProfileTests` (Domain).
- [ ] **Fermer le périmètre de 59.7 par un `grep`** :
  `grep -rn 'delete-.*-button' src/ExcelETL.BlazorAdmin/Components/` — le test paramétré du sous-ticket
  ne vaut que par son exhaustivité, et chaque bouton existe **deux fois** (gabarit tableau + gabarit
  carte).

**Effort** : standard.

---

## 59.1. Domain — validation des noms de Tableaux et d'Applications d'un profil d'import

**Comportement attendu** :
- Chaque élément de `DefaultTableaux` et de `DefaultApplicationNames` est validé à la construction de
  l'`ImportProfile` :
  - **non vide / non blanc** après trim ;
  - **longueur ≤ 50** après trim (nouvelle constante `ImportProfile.MaxListItemNameLength = 50`,
    **distincte** de `MaxNameLength = 60`, qui reste le plafond du nom de profil) ;
  - **unicité insensible à la casse** au sein de **sa propre** collection, comparaison
    `OrdinalIgnoreCase` sur valeurs trimées. Un tableau et une application peuvent porter le même nom :
    ce sont deux listes indépendantes, et rien dans le domaine ne l'interdit.
- Les valeurs sont **stockées trimées**. Sans cela, `" zzz"` et `"zzz"` seraient rejetées comme
  doublons puis persistées comme deux chaînes différentes — l'unicité stockée ne refléterait pas
  l'unicité validée.
- **Quatre nouveaux membres de `DomainErrorCode`**, nommés dans le style existant :
  `ImportProfile_EmptyTableauName`, `ImportProfile_TableauNameTooLong`,
  `ImportProfile_DuplicateTableauName`, et les trois équivalents `..._ApplicationName`
  (soit **six** au total : vide / trop long / doublon, pour chacune des deux collections). Deux
  collections distinctes justifient deux familles de messages : « Tableau name … » et
  « Application name … » ne sont pas interchangeables à l'écran.
- Clés correspondantes dans `DomainErrorMessages.resx` **et** `.fr.resx`, nommées comme le membre
  d'enum (constat 13). Les messages « trop long » prennent l'argument `{0}` = la limite, comme
  `ImportProfile_NameTooLong` ; les messages de doublon prennent `{0}` = le nom fautif, comme
  `ProfileNameAlreadyExists`.
- **Un validateur public réutilisable**, appelé par le constructeur **et** par l'UI (59.2) :
  une méthode statique par collection, du type
  `public static void ValidateDefaultTableauName(string candidate, IReadOnlyList<string> existing)`,
  qui lève la `DomainValidationException` typée appropriée. Le constructeur l'appelle en boucle sur la
  liste reçue. **C'est le point de conception du sous-ticket** : si le constructeur finit par contenir
  une copie de la logique du validateur, s'arrêter — c'est la seule erreur réellement coûteuse ici,
  exactement comme `TryCommitAsync()` au 56.2.
- `ImportProfile` **reste immuable** : aucun `Add...`, aucun setter (constat 4).

**Tests** (`tests/ExcelETL.Domain.Tests/Extraction/Profile/ImportProfileTests.cs`) — **rouges d'abord** :
- Nom de tableau vide / blanc → `DomainValidationException` portant
  `DomainErrorCode.ImportProfile_EmptyTableauName`.
- Nom de tableau de **exactement 50** caractères → accepté. De **51** → rejeté avec
  `ImportProfile_TableauNameTooLong`, `Args` contenant `50`.
- Nom de **55 caractères dont 5 d'espaces en périphérie**, trimé à 50 → accepté (miroir de
  `Constructor_WithNameOf65CharactersTrimmingTo60_CreatesImportProfile`).
- `["zzz", "ZZZ"]` → rejeté avec `ImportProfile_DuplicateTableauName`, `Args` contenant le nom fautif.
  **C'est le test qui reproduit littéralement la capture du 30/07.**
- `["zzz", " zzz "]` → rejeté également (trim avant comparaison).
- Valeurs stockées : `["  zzz  "]` → `DefaultTableaux[0]` vaut `"zzz"`.
- Les **six** mêmes cas côté `DefaultApplicationNames`.
- Un même nom présent dans les deux collections → **accepté** (garde-fou de non-généralisation : ne pas
  inventer une unicité croisée qui n'a pas été demandée).
- **Non-régression** : `Constructor_WithEmptyTableauxAndApplicationNames_CreatesImportProfile`,
  `Constructor_WithNullDefaultTableaux_ThrowsArgumentNullException`,
  `Constructor_WithNullDefaultApplicationNames_ThrowsArgumentNullException` et les 4 tests de nom de
  profil restent verts **sans modification**.
- **Localisation** (`tests/ExcelETL.Application.Tests/`) : les 6 nouvelles clés sont résolues par
  `BusinessExceptionLocalizer` et ne retombent pas sur `ex.Message` — dans le style de
  `DomainErrorMessagesHeaderRuleLocalizationTests`.
- **Repository** (seulement si 59.0 révèle une conversion de valeur) : un profil enregistré puis relu
  restitue les noms trimés.

**Refactor** : **effort élevé** sur ce sous-ticket uniquement — l'unicité du chemin de validation entre
constructeur et validateur public est le seul arbitrage du lot. Tout le reste est de l'application.

**Dossier** : `src/ExcelETL.Domain/Extraction/Profile/ImportProfile.cs`,
`src/ExcelETL.Domain/Exceptions/DomainErrorCode.cs`,
`src/ExcelETL.Application/Resources/DomainErrorMessages.resx` (+ `.fr.resx`).

---

## 59.2. Blazor — l'erreur est montrée au moment de la saisie, par le validateur du Domain

**Comportement attendu** (`ImportProfileEditor.razor`, import uniquement — constat 3) :
- `AddDefaultTableau()` et `AddDefaultApplicationName()` appellent le validateur public du 59.1 sur la
  valeur saisie et la liste courante, dans un `try/catch` sur `DomainValidationException`, et affichent
  le message via `BusinessExceptionLocalizer.TryLocalize(ex) ?? ex.Message` — **au même endroit et dans
  le même gabarit `alert alert-danger role="alert"`** que les erreurs d'édition en ligne existantes.
  Le rejet **silencieux** du blanc (constat 5) disparaît.
- En cas d'échec : **rien n'est ajouté**, la **saisie est conservée** dans le champ, le drapeau
  `_hasUnsavedChanges` **n'est pas levé** (aucune modification n'a eu lieu).
- En cas de succès : l'élément est ajouté **trimé**, le champ est vidé, `MarkAsChanged()` est appelé
  comme aujourd'hui.
- `SaveDefaultTableauEdit(int)` et `SaveDefaultApplicationNameEdit(int)` passent par le **même**
  validateur, en excluant l'élément en cours d'édition de la liste comparée — sans quoi renommer
  « zzz » en « zzz » se rejetterait lui-même. **C'est le piège du sous-ticket.**
- Les deux clés `ImportProfileEditor_EmptyTableauNameError` et
  `ImportProfileEditor_EmptyApplicationNameError` deviennent **orphelines** : le message vient désormais
  du Domain. Elles sont **laissées en place** et leur statut est **consigné** — pas supprimées sans
  vérification exhaustive de leurs usages (même prudence qu'au 37.0).
- Aucun blocage préventif du bouton d'ajout (décision actée).

**Tests** (bUnit, `ImportProfileEditorTests.cs`) — **rouges d'abord** :
- Saisir « zzz » puis à nouveau « zzz » → alerte présente, **un seul** élément dans la liste, saisie
  conservée dans le champ.
- Saisir « zzz » puis « ZZZ » → même résultat (insensibilité à la casse **à travers l'UI**, pas
  seulement en test Domain).
- Nom de 51 caractères → alerte présente, aucun élément ajouté.
- Champ vide → alerte présente (**changement de comportement** : le test existant qui vérifie
  qu'un ajout blanc ne fait « rien » est **corrigé dans son intention**, pas doublé).
- Ajout réussi avec espaces périphériques → l'élément rendu dans la liste est trimé.
- Édition en ligne : renommer un élément **en sa propre valeur** → **accepté**, pas d'alerte.
- Édition en ligne : renommer un élément avec le nom d'un **autre** élément → alerte, mode édition
  **maintenu ouvert**, valeur d'origine non écrasée.
- Après un ajout refusé, `#unsaved-changes-indicator` reste **absent** si aucune autre modification
  n'a eu lieu.
- **Non-régression** : les tests existants `DefaultTableau_AddThenEditThenSave_PersistsAfterSavingProfile`,
  `DefaultTableau_AddThenDeleteThenSave_...`, `DefaultTableau_ClickingModify_...`,
  `DefaultTableau_Cancel_...`, `DefaultTableau_Delete_...` et leurs 5 équivalents
  `DefaultApplicationName_*` restent verts. `DefaultTableau_SaveWithEmptyValue_ShowsError_AndKeepsEditModeOpen`
  et son jumeau restent verts **en comportement**, avec le message d'origine Domain.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`.

---

## 59.3. Tableaux et Applications côte à côte au-dessus de 768px

**Comportement attendu** (import uniquement — constat 3) :
- Les deux blocs (`h2` + carte, constat 7) sont placés chacun dans un `col-12 col-md-6` d'une **même**
  `row` (avec `g-3`, comme la `row` des champs courts du 53.2), enfant direct de
  `.profile-editor-container`. Au-dessus de 768px : Tableaux à gauche, Applications à droite. En
  dessous : `col-12` seul, empilement **strictement** identique à aujourd'hui.
- Le `h2` de chaque bloc est **à l'intérieur** de sa colonne, pas au-dessus de la `row` — sans quoi les
  deux titres se retrouveraient sur une ligne et les cartes sur une autre.
- **Rien ne change à l'intérieur des cartes** : la `row g-2` de la ligne de saisie, ses
  `col-12 col-md` / `col-12 col-md-auto`, `field-inline-action`, `block-field-list`,
  `block-field-item`, les ids et les classes des boutons de ligne restent identiques au caractère près.
- Aucun id ne change.

**Tests** (bUnit) — **rouges d'abord** :
- Le conteneur du bloc Tableaux et celui du bloc Applications portent **`col-12 col-md-6`** (égalité
  stricte de chaîne), et sont **enfants directs d'un même élément portant `row`** (test de structure :
  sans le parent `row`, la classe est cosmétique et sans effet).
- Le `h2` de chaque bloc est **descendant** de sa colonne.
- **Non-régression de disposition interne** : `DefaultTableauxAndApplications_FieldAndButtonContainers_HaveExpectedColumnClasses`
  et `DefaultTableauxAndApplications_FieldAndButton_AreDirectChildrenOfSameRow` restent verts **sans
  modification** — c'est le garde-fou qui prouve que 53.3 n'a pas été rouvert par accident.
- `TableauAndApplicationAddButtons_CarryFieldInlineActionClass_AndKeepExistingClasses` et
  `TableauAndApplicationAddRows_NoLongerCarryAlignItemsEnd` (58.2) restent verts **sans modification**.
- **Garde-fou mobile** (esprit 53.5) : aucun des deux conteneurs ne porte `col-md-6` **sans** `col-12`.
- Non-régression fonctionnelle : ajout, édition en ligne et suppression dans chacun des deux blocs
  continuent de fonctionner (réutiliser les tests existants, ne pas les réécrire).

**Vérification manuelle attendue** (non testable en bUnit, à consigner) : à ≥768px, dans le conteneur
de 1140 px, la ligne « champ + bouton Ajouter » reste lisible dans une demi-largeur et le bouton
conserve la hauteur du champ (acquis 58.2) sans que son libellé passe sur deux lignes. Si le libellé
se casse, **le consigner et demander** plutôt que de raccourcir le libellé ou de bricoler une largeur.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`.

---

## 59.4. Le CTA n'est actif que s'il y a des modifications en attente

**Comportement attendu** (les deux éditeurs) :
- `#save-profile-button` / `#save-export-profile-button` portent `disabled` **si et seulement si**
  `_hasUnsavedChanges` est `false`. La condition existe déjà et est déjà exacte (constat 2) : ce
  sous-ticket la **consomme**, il ne la reconstruit pas.
- Le `title` devient conditionnel : `*_SaveButtonShortcutHint` quand le bouton est actif, une **clé
  neuve** par éditeur (`ImportProfileEditor_SaveButtonNoChangesHint` /
  `ExportProfileEditor_SaveButtonNoChangesHint`, EN + FR) quand il est inactif. Un bouton inactif sans
  explication est une régression d'usage, pas une simplification.
- **La chaîne de classes ne change pas d'un caractère.** Bootstrap style déjà `:disabled` ; ajouter une
  classe d'état serait un second mécanisme.
- **`Ctrl+Entrée` (56.5) respecte la même condition** : si rien n'est en attente, le raccourci ne
  déclenche **aucun** appel à `SaveAsync`.
- Sur les routes de **création** (`/import-profiles/new`, `/export-profiles/new`), le bouton est
  **inactif au chargement** : un formulaire vierge n'a rien à enregistrer, et un profil sans règle de
  feuille serait de toute façon rejeté par le Domain. La première saisie (nom, ajout, ou modification
  dans un formulaire de feuille ouvert — `OnDirty`, constat 2) l'active.
- Après un enregistrement réussi, `_hasUnsavedChanges` retombe à `false` : le bouton redevient inactif
  **puis** la navigation vers la liste a lieu. Ordre inchangé, aucune interaction nouvelle.

**Tests** (bUnit) — **rouges d'abord** :
- Route d'édition, profil chargé, **aucune** interaction → le bouton porte `disabled`, et son `title`
  est celui de la clé neuve.
- Après modification de `#profile-name-input` → `disabled` **absent**, `title` redevenu le rappel de
  raccourci.
- Après ouverture d'un formulaire de feuille et modification d'un de ses champs, **sans soumettre** →
  `disabled` absent (preuve que le chemin `OnDirty` du 56.3 alimente bien l'état du bouton — c'est ce
  test qui relie les deux lots).
- Ouverture d'un formulaire de feuille **sans rien modifier** → `disabled` **toujours présent** (pas de
  faux positif, symétrique du garde-fou de 56.3).
- `Ctrl+Entrée` sans modification en attente → `SaveAsync` **jamais** appelé.
- `Ctrl+Entrée` avec modification en attente → `SaveAsync` appelé **une** fois (non-régression 56.5,
  flush du 56.2 inclus).
- Route de création, au chargement → `disabled` présent ; après saisie du nom → absent.
- **Non-régression** : `SaveButton_IsDescendantOfStickySaveBar`,
  `SaveProfileButton_HasNonEmptyTitle_MentioningShortcut`,
  `SaveProfileButton_KeepsItsFullPrimaryClass_WhileIntermediateButtonsAreOutline`,
  `SaveProfileButton_IsFullWidthLargeCta_WithVerticalMargins_*`,
  `SaveProfileButton_IsInRightAlignedContainer`, `SaveProfileButton_HasIcon`,
  `FinalSaveButton_CssClass_IsIdenticalBetweenImportAndExportEditors`,
  `GabaritButtons_StillCarryTheirPreExistingClasses_NonRegression` restent verts **sans
  modification**. `SaveProfileButton_HasNonEmptyTitle_MentioningShortcut` exige d'avoir une
  modification en attente dans son arrangement : si le test doit être **retouché**, retoucher son
  arrangement, pas son assertion.
- **Le test qui prouve que le test précédent n'est pas vide de sens** (`recommandations-tickets-tdd.md`
  §6) : `SaveProfileButton_StillSavesAndNavigatesToList` doit toujours passer, ce qui suppose que son
  arrangement produit bien une modification en attente. S'il passe **sans** que rien n'ait été modifié,
  c'est que le `disabled` n'est pas effectif dans bUnit et que l'assertion ne prouve rien — vérifier en
  remettant volontairement le correctif au rouge.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
`Resources/BlazorAdminMessages.resx` (+ `.fr.resx`).

---

## 59.5. Le résumé dépliable n'emprunte plus le rouge des alertes

**Comportement attendu** :
- `app.css:209` : `color: var(--bs-link-color)` devient `color: var(--bs-secondary-color)` sur
  `.sheet-rule-sublist-details > summary`. `cursor: pointer`, `font-size: 0.9rem`, `list-style: none`
  et la neutralisation du marqueur WebKit sont **conservés** — l'affordance de dépliage ne repose pas
  sur la couleur.
- **Aucun jeton de `theme-m3.css` n'est déclaré, modifié ni remappé.** `--bs-link-color` reste mappé
  sur `--m3-primary` (les vrais liens de l'application ne changent pas), et `--m3-primary` n'est pas
  touché (constat 8). Le périmètre est **une seule déclaration, dans un seul fichier**.
- Aucune classe ne change côté Razor.

**Tests** — non testable en bUnit (aucun calcul de couleur). Un test d'audit sur `app.css` **lu comme
fichier**, dans le style de `ThemeSecondaryButtonContrastTests` (constat 14), à placer dans
`tests/ExcelETL.BlazorAdmin.Tests/Styling/` :
- La règle `.sheet-rule-sublist-details > summary` **référence** `var(--bs-secondary-color)`.
- Elle ne référence **plus** `--bs-link-color`, ni `--m3-primary`, ni aucune valeur hexadécimale
  littérale — c'est l'assertion qui porte réellement l'intention de la remarque.
- **Garde-fou de non-débordement** : `theme-m3.css` mappe **toujours** `--bs-link-color` sur
  `--m3-primary` dans les blocs clair **et** sombre. Un test qui échoue si le correctif a été appliqué
  au thème plutôt qu'à la règle.
- **Non-régression bUnit** : les 8 tests `SheetRuleSublistDetails_*` des deux éditeurs et
  `SheetRuleSublistDetails_CollapsedByDefaultBehavior_IsIdenticalBetweenImportAndExportEditors`
  restent verts **sans modification** (aucune classe ne bouge).

**Vérification manuelle attendue** (à consigner) : lisibilité du résumé en thème clair **et** sombre,
et confirmation qu'il ne se lit plus comme un message d'erreur à côté d'une véritable
`alert alert-danger`.

**Effort** : standard.

**Dossier** : `src/ExcelETL.BlazorAdmin/wwwroot/app.css` (+ test d'audit CSS).

---

## 59.6. La bascule « Ajouter la feuille » rejoint le gabarit des boutons d'ajout

**Ce sous-ticket traite deux remarques de la revue qui désignent le même bouton** (constat 9, confirmé
par Simon) : la largeur (« la plupart des boutons occupent toute la largeur de leur zone, pas
celui-ci ») et la teinte (« n'a pas le même style que les autres boutons Ajouter »).

**Comportement attendu** :
- `toggle-add-sheet-rule-form-button` (`ImportProfileEditor.razor:438`) et
  `toggle-add-sheet-generation-rule-form-button` (`ExportProfileEditor.razor:177`) passent de
  `btn btn-sm btn-outline-secondary d-flex align-items-center justify-content-center gap-1` à
  **`btn btn-secondary w-100 d-flex align-items-center justify-content-center gap-1`** — soit le
  gabarit exact des 10 boutons d'ajout de sous-formulaire, **`mt-3` excepté** : la bascule est en tête
  de son bloc, pas en bas d'un formulaire, et un `mt-3` y serait un espacement sans cause.
- La classe est **la même dans les deux états** de la bascule. Faire varier l'apparence selon l'état
  reviendrait à réintroduire un conditionnel de classe sur un bouton — exactement ce que 56.7 a passé
  huit fichiers à supprimer. La distinction ouvert/fermé reste portée par le **libellé et l'icône**,
  règle du lot 057, **non rouverte**.
- Si le rendu du bouton **ouvert** (pleine largeur, fond plein, sans icône) déplaît visuellement, le
  **consigner et demander** — ne pas réintroduire un conditionnel de sa propre initiative.

**Tests** (bUnit) — **rouges d'abord** :
- Les deux bascules portent `btn-secondary` et `w-100`, et **ne portent plus** `btn-sm` ni
  `btn-outline-secondary` (présence **et** absence).
- Égalité stricte de la chaîne de classes entre les deux états de la bascule (fermé et ouvert) — test
  d'intention qui empêche la réintroduction d'un conditionnel.
- `AddSheetRuleToggleButton_CssClass_IsIdenticalBetweenImportAndExportEditors` est **corrigé dans son
  intention** (nouvelle chaîne attendue), **pas doublé** d'un second test à côté.
- `AllAddButtons_CarryW100_OnBothEditors` est **étendu** aux deux ids de bascule — ils entrent
  désormais dans le périmètre de ce garde-fou, ce qui est le but.
- **Non-régression lot 057, sans modification** : `EditMode_OnLoad_AddFormFieldsAreAbsent_ToggleButtonPresent`,
  `EditMode_ClickingToggle_RendersAddFormFields`, `EditMode_ClickingToggleTwice_HidesAddFormFieldsAgain`,
  `ClosedToggle_HasIconAndNonEmptyLabel_OpenToggle_HasNoIcon`,
  `ReopeningAfterPartialInput_FieldsAreEmpty_ProvingRemount`,
  `AtMostOneSheetRuleFormRendered_AcrossMultipleOpenActions`,
  `OpeningEdit_ThenClickingAddToggle_CommitsTheEditAndOpensAdd`.
- **Non-régression lot 058** : `ImportProfileEditor_IconLabelButtons_AllCarryTheGabarit` et son jumeau
  export restent verts — le gabarit icône + libellé est conservé, seules la couleur et la largeur
  changent.
- **Garde-fou de non-généralisation** : `add-default-tableau-button` et
  `add-default-application-name-button` conservent `w-md-auto field-inline-action` et **ne prennent
  pas** `mt-3` (`AddDefaultTableauAndApplication_AddButtons_AreSecondaryWithPlusIconAndVisibleLabel`
  reste vert sans modification). Uniformiser ces deux-là au gabarit `mt-3` rouvrirait 53.3 **et** 58.2
  d'un seul geste.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`.

---

## 59.7. Boutons de suppression en contour rouge sur les trois pages de liste

**Comportement attendu** :
- Sur `ImportProfiles.razor`, `ExportProfiles.razor` et `Users.razor`, les boutons de suppression
  passent de `btn btn-outline-secondary btn-sm block-field-icon-btn` à
  **`btn btn-sm btn-outline-danger block-field-icon-btn`** — chaîne **identique au caractère près** à
  celle des éditeurs (constat 10), ordre des tokens inclus. Harmoniser la couleur sans harmoniser
  l'ordre laisserait deux chaînes distinctes pour un même rôle, donc un test de parité impossible à
  écrire par égalité stricte.
- Périmètre : **les deux gabarits** de chaque page (tableau **et** carte). Soit, sous réserve du `grep`
  de 59.0 : `delete-profile-button-{id}` et `delete-profile-button-card-{id}`,
  `delete-export-profile-button-{id}` et `-card-{id}`, `delete-user-button-{id}` et
  `delete-user-button-card-{id}`.
- **Les boutons Modifier, Dupliquer et Réinitialiser le mot de passe restent en contour gris**
  (hors périmètre) : le rouge ne distingue l'action destructrice que si les autres ne le portent pas.
- **`Users.razor` conserve son `disabled`** sur les lignes non supprimables (utilisateur courant, seul
  administrateur restant). Un bouton rouge désactivé reste désactivé.
- Aucun id, aucun `aria-label`, aucun `title`, aucune icône, aucun comportement de confirmation ne
  change.

**Tests** (bUnit) — **rouges d'abord** :
- Test **paramétré** sur tous les ids de bouton de suppression des trois pages, dans les deux gabarits :
  chacun porte `btn-outline-danger` et **ne porte plus** `btn-outline-secondary`.
- Test paramétré symétrique sur les boutons Modifier / Dupliquer / Réinitialiser : chacun porte
  **toujours** `btn-outline-secondary` et **ne porte pas** `btn-outline-danger`. **Garde-fou de
  non-généralisation** — sans lui, un `replace` global repeindrait toute la barre d'actions.
- Égalité stricte de la chaîne de classes entre un bouton de suppression de page de liste et
  `delete-default-tableau-button-0` de `ImportProfileEditor.razor` : c'est l'assertion qui porte
  réellement le mot « harmoniser » de la demande, et elle échoue si seul le token de couleur a été
  changé sans réordonner.
- **Non-régression, sans modification** : `RowActionButtons_AreIconOnly_WithAriaLabelAndTitle_InBothTableAndCardTemplates`
  (import et export), `Users_RowActionButtons_AreInAHorizontallyAlignedContainer`,
  `CurrentUserRow_DeleteButtonIsDisabled`, `SoleRemainingAdminRow_DeleteButtonIsDisabled`,
  `NonAdminNonCurrentUserRow_DeleteButtonIsEnabled`, et l'ensemble des tests de confirmation de
  suppression des trois pages.
- **Hors périmètre vérifié** : `HeaderButtonWrapper_CssClass_IsIdenticalBetweenImportAndExportProfilesLists`
  et `HeaderButtons_CssClass_IsIdenticalBetweenImportAndExportProfilesLists` restent verts sans
  modification (boutons d'en-tête non touchés).

**Piste de refactor à considérer** : trois pages écrivent la même chaîne de classes six fois chacune,
sans constante partagée (constat 10). Une constante de classe dans `Shared/` — à côté de
`AdminIconMarkup`, dans l'esprit du lot 035.5 — supprimerait la cause racine plutôt que sa
manifestation. **À évaluer au refactor**, pas à imposer : si elle est retenue, le test d'égalité
stricte ci-dessus devient la preuve de sa bonne diffusion ; si elle est écartée, en consigner la
raison pour que la question ne se rouvre pas au prochain lot.

**Effort** : standard pour le rouge et le vert ; **élevé au refactor** si la constante partagée est
retenue.

**Dossier** : `ImportProfiles.razor`, `ExportProfiles.razor`, `Users.razor`
(+ éventuellement `src/ExcelETL.BlazorAdmin/Shared/`).

---

## 59.8. Parité structurelle (clôture)

**Comportement attendu** : `ProfileEditorParityTests.cs` et `ProfileListPageParityTests.cs` sont
étendus aux comparables **réellement** modifiés par ce lot :

- **CTA final** : l'attribut `disabled` et la valeur du `title` se comportent identiquement des deux
  côtés, dans les deux états (59.4). La chaîne de classes est déjà couverte par
  `FinalSaveButton_CssClass_…`, non modifiée.
- **Bascule d'ajout de feuille** : nouvelle chaîne, comparaison stricte (59.6) — via la correction de
  `AddSheetRuleToggleButton_CssClass_…`.
- **Résumé dépliable** : `sheet-rule-sublist-details` n'a **aucune classe modifiée** ; le test de
  parité existant reste vert sans modification. Rien à ajouter — le noter explicitement plutôt que
  d'inventer un comparable.
- **Boutons de suppression des pages de liste** : comparaison stricte entre les trois pages **et** avec
  l'éditeur (assertion déjà posée en 59.7) — à consolider dans
  `ProfileListPageParityTests.cs` pour les deux listes de profils. `Users.razor` n'a pas de page jumelle
  et se documente comme asymétrie assumée, à la manière de
  `ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion`.
- **59.1, 59.2 et 59.3 n'ont aucun pendant export** (constat 3). Écrire **une** assertion explicite
  d'asymétrie — `ExportProfileEditor` n'expose ni bloc Tableaux ni bloc Applications — plutôt que de
  laisser croire à un oubli de parité.

**Tests** : comparaisons de chaîne **strictes**, dans le style des méthodes existantes
(`…_IsIdenticalBetweenImportAndExportEditors`). Ce test est **le dernier rendu vert du lot** : s'il
passe avant que 59.4 et 59.6 ne soient terminés des deux côtés, c'est qu'il ne compare pas ce qu'il
prétend comparer.

**Effort** : standard.

**Dossier** : `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ProfileEditorParityTests.cs`,
`ProfileListPageParityTests.cs`.

---

## Reporté — session dédiée à la palette de couleur

**À ne pas traiter dans ce lot.** Consigné ici pour ne pas être perdu, et à reprendre tel quel en
ouverture de la session dédiée.

Remarque de Simon (30/07) : *« Je sais que les derniers lots ont modifié les couleurs de certains
boutons, notamment pour supprimer le marron taupe assez moche. C'est bien, mais maintenant j'ai
l'impression qu'on utilise un bleu Bootstrap de base qui ne va pas du tout avec la palette de couleur.
On voit cette couleur sur le bouton "Enregistrer le profil" par exemple. Ou le bouton "Créer"
`create-profile-button` de la page `import-profiles`. Il y en a un peu partout. »*

Éléments factuels à verser au dossier de cette session :

- `theme-m3.css` déclare `--m3-primary: #D81F11` en clair et `#FFB4AB` en sombre, et `.btn-primary`
  (`:238-249`) consomme bien ces jetons. **Un bouton qui apparaît bleu ne consomme donc pas
  `--m3-primary`** : soit la feuille de thème n'est pas chargée au moment observé, soit Bootstrap
  l'emporte sur la cascade, soit le bouton en question n'est pas un `.btn-primary`. **C'est un écart à
  diagnostiquer, pas seulement un goût à arbitrer** — et c'est probablement le point d'entrée le plus
  rentable de la session.
- Périmètre pressenti : jeu complet de jetons M3 (primary / secondary / tertiary / surface / error, et
  leurs variantes `container` / `on-`), thèmes clair et sombre, contrastes WCAG.
- Acquis à ne pas casser : les jetons `secondary-container` et le test de contraste permanent du 58.1
  (`ThemeSecondaryButtonContrastTests`), qui est précisément l'outil conçu pour sécuriser une refonte
  de palette.
- Écart connexe déjà documenté et resté hors périmètre : `ReconnectModal.razor.css` et ses couleurs
  hexadécimales figées hors thème (§2.3 et §3.1 de `audit-design-blazoradmin-2026-07-27.md`).
- La couleur du résumé dépliable (59.5) sera alors passée à `--bs-secondary-color` : si la session
  redéfinit ce jeton, ce point de consommation est à revérifier — le test d'audit CSS de 59.5 le
  signalera.

---

## Ordre recommandé

1. **59.0** — investigation courte (EF Core, baseline, `grep` du périmètre 59.7)
2. **59.1** — validation Domain (**cœur du lot**, effort élevé au refactor)
3. **59.2** — surface UI de cette validation (dépend de 59.1)
4. **59.4** — CTA conditionnel (indépendant, et le plus visible pour Simon)
5. **59.5** — couleur du résumé dépliable (indépendant, un seul fichier)
6. **59.6** — bascule « Ajouter la feuille »
7. **59.3** — deux colonnes (**après** 59.2, pour ne pas déplacer du markup en même temps qu'on change
   son comportement — sinon un test rouge ne dit plus lequel des deux l'a cassé)
8. **59.7** — boutons de suppression des pages de liste
9. **59.8** — parité structurelle (clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **Les 14 constats en tête de document sont lus, pas supposés.** Ne pas relire les fichiers pour les
  re-vérifier : seuls les trois points de 59.0 restent ouverts.
- **Un seul chemin de validation en 59.1.** Si le constructeur d'`ImportProfile` finit par contenir une
  copie de la logique du validateur public, s'arrêter. C'est la seule erreur de conception réellement
  coûteuse du lot, et c'est la même leçon que `TryCommitAsync()` au 56.2.
- **Le piège de 59.2 est l'édition en ligne** : la liste comparée doit exclure l'élément en cours
  d'édition, sinon renommer « zzz » en « zzz » se rejette lui-même. Écrire ce test **avant** le code.
- **59.4 ne construit aucun état.** `_hasUnsavedChanges` est déjà exact depuis le 56.3 — il n'y a qu'à
  le lire. Si le sous-ticket demande d'ajouter un drapeau, c'est qu'on a manqué le constat 2.
- **59.5 ne touche qu'une ligne de `app.css`.** Toute modification de `theme-m3.css` dans ce
  sous-ticket est un débordement : `--m3-primary` alimente 25 déclarations, dont le CTA lui-même.
- **En 59.6, ne pas conditionner la classe à l'état de la bascule** — 56.7 a passé huit fichiers à
  supprimer exactement ce motif.
- **En 59.7, fermer le périmètre par le `grep` avant d'écrire les tests**, pas en cours de route :
  chaque bouton existe **deux fois** (gabarit tableau + gabarit carte) et le test paramétré ne vaut
  que par son exhaustivité.
- **Ne pas « harmoniser » la teinte du CTA ou des autres boutons** au passage : c'est la session
  palette, et Simon l'a explicitement mise de côté.
- **Ne pas toucher à l'intérieur des `form-floating`** (30.6, verrouillé par
  `FormFloatingStructureAuditTests`), ni introduire d'`input-group`.
- Tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~ImportProfile|FullyQualifiedName~ProfileEditor|FullyQualifiedName~Profiles|FullyQualifiedName~Users|FullyQualifiedName~Styling" --verbosity quiet`.
- **Effort standard partout, sauf** le refactor de 59.1 (élevé) et, le cas échéant, celui de 59.7
  (élevé si la constante partagée est retenue).

**Dossiers concernés** :
`src/ExcelETL.Domain/Extraction/Profile/ImportProfile.cs`,
`src/ExcelETL.Domain/Exceptions/DomainErrorCode.cs`,
`src/ExcelETL.Application/Resources/DomainErrorMessages.resx` (+ `.fr.resx`),
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` — `ImportProfileEditor.razor`,
`ExportProfileEditor.razor`, `ImportProfiles.razor`, `ExportProfiles.razor`, `Users.razor` ;
`src/ExcelETL.BlazorAdmin/wwwroot/app.css` ;
`src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx` (+ `.fr.resx`) ;
et les miroirs `tests/ExcelETL.Domain.Tests/`, `tests/ExcelETL.Application.Tests/`,
`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`, `tests/ExcelETL.BlazorAdmin.Tests/Styling/`.
