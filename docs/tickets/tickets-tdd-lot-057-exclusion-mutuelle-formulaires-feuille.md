# Tickets TDD — Lot 057 : un seul formulaire de feuille ouvert à la fois

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 056,
dont il **dépend techniquement** (voir « Dépendance » ci-dessous).*

**Origine** : revue d'usage de Simon le 29/07 sur `/import-profiles/{id}/edit`, remarque 4 :

> Par défaut, quand j'édite un profil, le formulaire complet pour l'ajout d'une nouvelle feuille est
> affiché en bas de la page. C'est perturbant car cela représente un volume considérable. Il est
> possible, et même probable, que je veuille juste modifier une feuille existante. Pourtant,
> l'affichage est pollué par cette nouvelle feuille. Lorsque je démarre l'édition d'une feuille
> existante, c'est pire, car la page devient très longue avec de nombreux input. Très difficile à
> appréhender pour l'utilisateur.

**Décision retenue** : affichage sur action explicite **+ exclusion mutuelle** — un seul formulaire
ouvert à la fois, jamais « ajout » et « édition » en même temps, jamais deux éditions en même temps.

Valable pour les **quatre** routes éditeur : `/import-profiles/new`, `/import-profiles/{id}/edit`,
`/export-profiles/new`, `/export-profiles/{id}/edit`.

---

## Dépendance : ce lot ne démarre pas avant que 56.2 soit vert

L'exclusion mutuelle pose une question que le lot 056 a déjà tranchée : **que devient la saisie d'un
formulaire qu'on ferme implicitement ?** La perdre serait reproduire exactement le défaut que le lot
056 corrige. La réponse est donc la même règle, appliquée partout :

> **Toute action qui ferme un formulaire ouvert tente d'abord de le commettre.** Si la validation
> échoue, l'action est **refusée** et l'erreur s'affiche sur le formulaire concerné.

Ce lot **réutilise** `TryCommitAsync()` introduit en 56.2 (`SheetRuleForm` /
`SheetGenerationRuleForm`). Il n'en écrit pas une seconde version et ne réimplémente aucune validation.

---

## Décisions actées avec Simon (29/07)

| Sujet | Décision |
| :--- | :--- |
| Affichage du formulaire d'ajout de feuille | **Replié par défaut** en édition, derrière un bouton bascule. |
| Panneau latéral / offcanvas | **Écarté** : introduirait un motif d'interaction absent de tout l'admin (aucune modale métier aujourd'hui) et compliquerait les tests bUnit. |
| Édition en place dans la carte (accordéon) | **Écartée** : la page reste longue dès qu'une carte est ouverte, et ça fait converger carte et formulaire. |
| Exclusion mutuelle | **Retenue** : un seul formulaire ouvert à la fois. |
| Saisie en cours lors d'une fermeture implicite | **Commise** via `TryCommitAsync()` (56.2). Échec de validation → changement de formulaire refusé, erreur affichée. |
| Comportement en mode **création** (`/new`) | **Formulaire d'ajout ouvert d'emblée** — sur une page de création, le replier laisserait un écran quasi vide sans indication de la marche à suivre. En **édition**, il est replié. |

**Point d'arbitrage assumé** : l'asymétrie création/édition ci-dessus est le seul choix de ce lot où
une autre décision serait défendable (tout replier partout, pour une règle unique). Elle est retenue
parce que le coût d'un écran vide en création est plus élevé que celui d'une règle à deux branches, et
elle est **testée explicitement** des deux côtés (57.1).

---

## Constats vérifiés dans le code (29/07, dépôt `C:\AM-OXO-ETL`)

Ces points étaient des hypothèses à la rédaction ; ils ont été **lus dans le code** avant publication.
Les numéros de ligne sont ceux du 29/07 et dériveront — repères, pas contrat.

1. **Le formulaire d'ajout est rendu inconditionnellement**, dans une carte dédiée :
   ```razor
   <div class="card mb-3">
       <div class="card-header">@Loc["ImportProfileEditor_AddSheetHeading"]</div>
       <div class="card-body">
           <SheetRuleForm SubmitLabel="@Loc["ImportProfileEditor_AddSheetButton"]" OnSubmit="HandleAddSheetRule" />
       </div>
   </div>
   ```
   (`ImportProfileEditor.razor:418-423` ; `ExportProfileEditor.razor:170-174` à l'identique avec
   `SheetGenerationRuleForm`.) **Aucun état d'ouverture n'existe** pour lui : c'est ce lot qui
   l'introduit.
2. **L'instance d'ajout ne reçoit ni `IdPrefix`, ni `ShowCancel`, ni `CancelButtonId`.** Elle utilise
   donc les valeurs par défaut des paramètres : `IdPrefix = ""`, `SubmitButtonId =
   "add-sheet-rule-button"`, `ShowCancel = false`. **Conséquence directe : le formulaire d'ajout n'a
   aujourd'hui aucun bouton « Annuler »** (`SheetRuleForm.razor:472-477` : le bouton n'est rendu que
   si `ShowCancel`). Le mécanisme de fermeture de 57.1 ne peut donc pas s'appuyer sur un bouton
   existant — voir la décision de conception en 57.1.
3. **`ShowCancel` pilote aussi l'apparence du bouton de soumission** (`:461-470`) : classe
   `btn-outline-secondary` vs `btn-secondary`, et icône `Check` vs `Plus`. Passer `ShowCancel="true"`
   à l'instance d'ajout pour récupérer « Annuler » **changerait donc son icône et sa couleur** — effet
   de bord inacceptable, qui rouvrirait 53.4 et
   `IntermediateAddButton_CssClass_IsIdenticalBetweenImportAndExportEditors`. C'est ce qui motive le
   bouton bascule autonome de 57.1.
4. **La carte lecture-seule est bien remplacée** par le formulaire en mode édition
   (`ImportProfileEditor.razor:298` : `<li class="@(_editingIndex == index ? "sheet-rule-editing-item"
   : "sheet-rule-card")">`). Il n'y a **pas** de duplication carte + formulaire, contrairement à
   l'hypothèse envisagée à la rédaction : **aucun sous-ticket supplémentaire n'est nécessaire** sur ce
   point.
5. **`_editingIndex` et `_pendingDeleteIndex` sont deux champs indépendants** (`:463-464`), sans aucune
   coordination : ouvrir une édition ne referme pas une confirmation de suppression en attente sur une
   autre carte, et réciproquement. 57.2 les met sous une règle commune.
6. **`_editingIndex` est positionné directement dans le markup** (`:401` : `@onclick="() =>
   _editingIndex = index"`), sans passer par une méthode — donc sans aucun point d'accroche pour une
   règle de fermeture implicite. 57.2 doit d'abord extraire une méthode.
7. **`SheetRuleForm.OnInitialized()` (`:542-558`) n'hydrate qu'une fois par instance montée**, avec le
   commentaire explicite : *« the parent gives an edit-mode instance a fresh component identity
   (conditionally rendered inside an `@if`, not a persistently bound one), so `InitialRule` never
   changes underneath an already-initialized instance »*. **Ce contrat est à préserver** : le
   formulaire d'ajout replié/déplié doit être monté et démonté (rendu conditionnel), jamais masqué,
   sinon `OnInitialized` ne rejoue pas et l'état persiste d'une ouverture à l'autre.
8. **Coût de non-régression, le vrai risque du lot** : `ImportProfileEditorTests.cs` fait 144 Ko et
   `ExportProfileEditorTests.cs` 86 Ko. Un grand nombre de tests ajoutent une règle de feuille via le
   formulaire d'ajout (`#sheet-rule-name-input`, `#add-sheet-rule-button`) **sans clic d'ouverture**,
   puisqu'aujourd'hui il n'y en a pas besoin. Tous vont rougir en 57.1. Le nombre exact n'a pas été
   relevé (fichiers non lus intégralement) — il se découvre par un premier run rouge. Voir la note
   d'efficacité pour la façon de les corriger sans 40 copier-coller.
9. **`ProfileEditorParityTests.cs`** contient déjà
   `NavigationLockAndUnsavedChangesConfirmation_AreStructurallyIdenticalBetweenImportAndExportEditors`
   et `IntermediateAddButton_CssClass_…` : le bouton bascule de 57.1 devient un nouveau comparable
   (57.3).
10. **`AdminIconMarkup` ne contient pas de constante « croix »** (Pencil, Copy, Trash, Plus, Check,
    Send, FileEarmarkSpreadsheet, Key, Collection, Archive, Clock). Les croix des lignes d'édition en
    ligne sont des SVG inline dupliqués (`ImportProfileEditor.razor:127-129`, `:226-228`) — §2.4 de
    `audit-design-blazoradmin-2026-07-27.md`. Ce lot **n'ajoute pas** de constante et **n'utilise pas**
    d'icône pour l'état « fermer » (voir 57.1).

---

## Décisions antérieures explicitement rouvertes par ce lot

- **Rendu inconditionnel du formulaire d'ajout de feuille** (posé implicitement aux lots F1/J2, jamais
  formalisé comme décision) → rouvert par 57.1. Les tests existants qui interagissent directement avec
  le formulaire d'ajout sans l'ouvrir sont **corrigés**, pas supprimés ni contournés (même exigence
  qu'en 51.2, 53.2 et 56.7).

Tout le reste des lots 030 / 041 / 043 / 047 / 048 / 053 / 056 reste fermé. En particulier : la grille
`.sheet-rule-grid` (lot R), le conteneur de 1140 px (53.1), la barre collante (56.6) et l'apparence des
boutons de sous-formulaire (56.7) ne sont pas touchés.

---

## Conventions déjà en place à respecter (tout le lot)

- IDs HTML stables sur tout élément interactif ; jamais de sélection par texte ou position en bUnit.
- Un formulaire replié est **absent du DOM**, pas masqué en CSS — seule forme testable en bUnit, seule
  compatible avec le contrat d'hydratation du constat 7, et déjà l'usage du projet (`@if`).
- `convention-ui-blazor-icones-boutons.md` — le bouton bascule à l'état « ouvrir » est une action CRUD
  standard (« Ajouter ») : icône `AdminIconMarkup.Plus` + libellé texte, comme les autres boutons
  d'ajout. À l'état « fermer », c'est une action secondaire : **pas d'icône**, libellé seul.
- `convention-ui-blazor-alignement-boutons.md` — inchangée.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit 2.7.2.
- Aucune nouvelle dépendance CSS/JS.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite (tout le lot)

- **Le modèle d'enregistrement** (flush, indicateur, raccourci clavier, barre collante) — lot 056.
- **La teinte, la classe et le gabarit des boutons** — lots 056 (56.7) et 058. Ce lot ne change
  l'apparence d'aucun bouton existant.
- **Le repliement des cartes de feuille en lecture seule** ou tout autre accordéon supplémentaire :
  ce lot réduit le nombre de **formulaires** ouverts, il ne change pas l'affichage de la liste.
  `.sheet-rule-grid` et `.sheet-rule-sublist-details` (lots R / R3) restent tels quels.
- **Le nombre, l'ordre et le libellé des champs** d'un formulaire de feuille — aucun champ ajouté,
  retiré, renommé ou réordonné.
- **L'ajout d'une constante « croix » à `AdminIconMarkup`** et la centralisation des SVG inline
  dupliqués (§2.4 de l'audit) — hors périmètre, comme aux lots 053 et 058.
- **L'intérieur des `form-floating`** (30.6) et **`input-group`** (interdit).
- **Une modale ou un panneau latéral** — écartés par décision.
- **La suppression du clic « Modifier »** de la carte de feuille — écartée au lot 056, non rouverte.
- **L'attribut `[Authorize]` des quatre pages éditeur** et leurs routes — inchangés ; les tests HTTP du
  lot 052 restent verts sans modification.
- **Toute modification Domain / Application / pipeline** — ce lot est strictement Razor + `.resx` +
  tests.

---

## 57.1. Formulaire d'ajout de feuille replié derrière un bouton bascule

**Décision de conception** (motivée par les constats 2 et 3) : **un seul bouton bascule autonome**,
porté par l'éditeur — pas le `ShowCancel` du sous-formulaire, qui changerait au passage l'icône et la
couleur du bouton de soumission.

- ID : `#toggle-add-sheet-rule-form-button` (export : `#toggle-add-sheet-generation-rule-form-button`).
- **Toujours rendu**, avec deux états :
  - **fermé** → libellé « Ajouter une feuille » + `AdminIconMarkup.Plus` ;
  - **ouvert** → libellé de fermeture (« Annuler l'ajout »), **sans icône** (action secondaire, cf.
    convention). Le clic referme le formulaire et abandonne la saisie, exactement comme le
    « Annuler » d'un formulaire d'édition.
- Deux clés `.resx` neuves par éditeur (EN + FR) : `*_AddSheetToggleOpenButton` et
  `*_AddSheetToggleCloseButton`. Le libellé de l'état fermé peut réutiliser
  `*_AddSheetHeading`/`*_AddSheetButton` si le texte convient exactement — à vérifier dans le `.resx`
  avant d'en créer une.

**Comportement attendu** :
- **En mode édition** (`/{id}/edit`) : au chargement, le formulaire d'ajout **n'est pas rendu du
  tout** — aucun de ses `input`, `select` ou boutons n'existe dans le DOM. Le bouton bascule est rendu
  à l'état « fermé ».
- **En mode création** (`/new`) : le formulaire d'ajout est **ouvert au chargement**, le bouton bascule
  rendu à l'état « ouvert ».
- Après une soumission réussie (`#add-sheet-rule-button`), le formulaire **se referme** : ajouter deux
  feuilles d'affilée coûte un clic de réouverture, prix assumé de la lisibilité pour l'action la moins
  fréquente.
- La carte englobante (`card mb-3` + `card-header`) reste rendue en permanence : seule la
  `card-body` — donc le `SheetRuleForm` — est conditionnelle, et le bouton bascule vit dans la carte.
  Le repère visuel « c'est ici qu'on ajoute une feuille » ne disparaît jamais.
- Le montage/démontage du `SheetRuleForm` est un vrai rendu conditionnel (`@if`), pour préserver le
  contrat d'hydratation du constat 7 : réouvrir le formulaire redonne une instance vierge.

**Tests** (bUnit) — **rouges d'abord** :
- Mode édition, au chargement : `#sheet-rule-name-input` et `#add-sheet-rule-button` **absents** ;
  `#toggle-add-sheet-rule-form-button` **présent**.
- Clic sur la bascule → les champs du formulaire d'ajout sont rendus.
- Clic à nouveau (état « ouvert ») → les champs sont **à nouveau absents** (absence DOM réelle, pas une
  classe de masquage).
- Mode création, au chargement : les champs du formulaire d'ajout sont **présents**.
- Après une soumission réussie en mode édition : les champs sont absents et la bascule est à l'état
  « fermé ».
- Réouverture après une saisie partielle puis fermeture : les champs sont **vides** (preuve du
  remontage, constat 7).
- La bascule à l'état « fermé » contient un `<svg>` portant `aria-hidden="true"` et un libellé texte non
  vide ; à l'état « ouvert », **aucun** `<svg>` (règle de convention, et garde-fou contre un bouton
  icône seule qui exigerait `aria-label` + `title`).
- **Non-régression** : le parcours complet « ajouter une feuille de bout en bout puis enregistrer le
  profil » passe toujours, avec le clic d'ouverture ajouté.
- Miroir export sur l'ensemble.

**Effort** : standard pour le rouge et le vert ; la correction des tests existants (constat 8) est
mécanique mais volumineuse — voir la note d'efficacité.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
`Resources/BlazorAdminMessages.resx` + `.fr.resx`.

---

## 57.2. Exclusion mutuelle : un seul formulaire ouvert à la fois

**Comportement attendu** :
- Un **unique** état d'ouverture remplace `_editingIndex` + le nouvel état d'ajout de 57.1 — trois
  valeurs possibles : aucun formulaire, formulaire d'ajout, formulaire d'édition d'index *n*.
- Conséquences directes :
  - Ouvrir l'édition d'une feuille **ferme** le formulaire d'ajout s'il était ouvert.
  - Ouvrir le formulaire d'ajout **ferme** l'édition en cours.
  - Ouvrir l'édition d'une **autre** feuille ferme l'édition courante.
  - Ouvrir n'importe quel formulaire **annule** une confirmation de suppression en attente
    (`_pendingDeleteIndex = null`) : la règle « une seule chose en cours » couvre aussi ce cas, laissé
    indépendant aujourd'hui (constat 5). L'inverse est vrai aussi : demander une suppression ferme le
    formulaire ouvert, en tentant d'abord de le commettre.
- **Toute fermeture implicite tente d'abord de commettre** le formulaire fermé, via `TryCommitAsync()`
  (56.2) :
  - **Succès** → la règle est intégrée en mémoire (le résumé de la carte correspondante reflète
    immédiatement la modification), puis le nouveau formulaire s'ouvre.
  - **Échec de validation** → **le nouveau formulaire ne s'ouvre pas**, le formulaire courant reste
    ouvert avec sa saisie intacte, et le message localisé s'affiche sur **lui**. Une seule vérité
    d'erreur, au même endroit qu'ailleurs.
- Une fermeture **explicite** (« Annuler » d'un formulaire d'édition, bascule à l'état « ouvert » de
  57.1) garde son comportement d'abandon : **aucune** tentative de commit. C'est le seul chemin
  d'abandon volontaire, il doit rester prévisible.
- Prérequis d'implémentation : extraire une méthode de l'affectation en ligne
  `@onclick="() => _editingIndex = index"` (constat 6) — sans quoi il n'existe aucun point d'accroche
  pour la règle ci-dessus.
- À l'ouverture d'un formulaire, la carte concernée porte `.sheet-rule-editing-item` (classe
  existante) et le focus est placé sur le premier champ de saisie. Le focus réel n'étant pas assertable
  de façon fiable en bUnit, il relève de la **vérification manuelle** ci-dessous — mais il fait partie
  du comportement attendu.

**Tests** (bUnit) — **rouges d'abord** :
- Éditer la feuille 0, modifier `#edit-0-sheet-rule-stop-field-name-input`, cliquer
  `#modify-sheet-rule-button-1` → le formulaire de la feuille 1 est ouvert, celui de la feuille 0 n'est
  plus rendu, **et** la modification de la feuille 0 est visible dans le résumé de sa carte (donc bien
  commise en mémoire).
- Même scénario avec la feuille 0 rendue **invalide** (`#edit-0-sheet-rule-step-input` à `0`) → le
  formulaire de la feuille 1 **n'est pas** ouvert, celui de la feuille 0 est toujours rendu avec sa
  saisie, alerte présente sur lui.
- Formulaire d'ajout ouvert et rempli de façon **valide**, puis clic sur `#modify-sheet-rule-button-0`
  → la nouvelle feuille est ajoutée à la liste et l'édition de la feuille 0 s'ouvre.
- Édition ouverte, puis clic sur la bascule d'ajout → l'édition est commise et fermée, l'ajout est
  ouvert.
- « Annuler » sur un formulaire d'édition modifié → la saisie est abandonnée, **aucun** commit (le
  résumé de la carte est inchangé), et aucun autre formulaire ne s'ouvre.
- Confirmation de suppression affichée sur la carte 1, puis clic sur `#modify-sheet-rule-button-0` →
  la confirmation a disparu et **rien n'a été supprimé**.
- **Invariant global** : à tout moment, au plus **un** formulaire de règle de feuille est rendu — test
  enchaînant plusieurs ouvertures et comptant les occurrences d'un champ discriminant.
- La carte en cours d'édition porte `.sheet-rule-editing-item`, les autres non.
- Miroir export sur l'ensemble.

**Effort** : standard pour le rouge et le vert ; **élevé au refactor** — la fusion des états
d'ouverture est le seul endroit du lot où une conception approximative se paiera (états contradictoires
possibles si les champs d'origine survivent tous « au cas où »).

**Vérification manuelle attendue** (à consigner) : à l'ouverture d'un formulaire, le focus arrive bien
sur son premier champ et la page n'a pas sauté à un endroit inattendu ; la page en mode édition est
visiblement plus courte qu'avant le lot.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`.

---

## 57.3. Parité structurelle import/export (clôture)

**Comportement attendu** : `ProfileEditorParityTests.cs` gagne un nouvel élément comparable — le
**bouton bascule du formulaire d'ajout**, qui n'existait dans aucun des deux éditeurs avant ce lot.

**Tests** (bUnit) :
- Comparaison de chaîne **stricte** des classes du bouton bascule entre les deux éditeurs, dans le
  style des méthodes existantes (`…_CssClass_IsIdenticalBetweenImportAndExportEditors`).
- Le comportement d'exclusion mutuelle est vérifié **des deux côtés** (les tests de 57.2 existent en
  version import **et** export) — la parité ici n'est pas seulement cosmétique.
- Ce test est **le dernier rendu vert du lot**.

**Effort** : standard.

**Dossier** : `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ProfileEditorParityTests.cs`.

---

## Ordre recommandé

1. **57.1** — repli du formulaire d'ajout derrière la bascule (et correction des tests existants)
2. **57.2** — exclusion mutuelle (refactor à effort élevé)
3. **57.3** — parité structurelle (clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **Ne pas démarrer sans 56.2.** Sans `TryCommitAsync()`, 57.2 n'a que de mauvaises options : perdre la
  saisie (le défaut qu'on corrige) ou dupliquer la validation (deux vérités).
- **Le gros du travail de 57.1 est la correction des tests existants** (constat 8), pas la
  fonctionnalité. Ajouter **un helper privé** dans chaque fichier de tests (ex.
  `OpenAddSheetRuleForm(cut)`, qui clique la bascule si le formulaire n'est pas déjà rendu) et
  l'appeler depuis les tests concernés, plutôt que d'insérer 40 fois le même `cut.Find("#toggle-…").Click()`.
  Un helper unique permet aussi de neutraliser d'un seul geste un futur changement d'ID.
- **Ne pas passer `ShowCancel="true"` à l'instance d'ajout** pour récupérer un bouton « Annuler »
  (constat 3) : ça changerait l'icône et la couleur du bouton de soumission et ferait rougir les tests
  de parité de 53.4. C'est le piège principal de 57.1.
- **Un seul état d'ouverture, pas deux plus un booléen de coordination.** S'il reste à la fin du
  refactor deux champs d'état capables de se contredire, l'invariant « au plus un formulaire ouvert »
  est faux même si les tests passent sur les chemins testés.
- **Absence DOM, jamais masquage CSS.** Un `display:none` rendrait la moitié des tests de 57.1 verts
  pour de mauvaises raisons, laisserait les champs dans l'ordre de tabulation, et casserait le contrat
  d'hydratation du constat 7.
- Tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~ProfileEditor" --verbosity quiet`.

**Dossiers concernés** :
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor`,
`ExportProfileEditor.razor`,
`src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx` (+ `.fr.resx`),
et le miroir `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`.
