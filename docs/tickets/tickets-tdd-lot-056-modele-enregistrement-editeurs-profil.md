# Tickets TDD — Lot 056 : modèle d'enregistrement des éditeurs de profil

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 055.*

**Origine** : revue d'usage de Simon le 29/07 sur `/import-profiles/{id}/edit` (6 captures d'écran,
profil « Profil OXO standard (Copie SLB) »). Scénario vécu : ajouter un champ de bloc `ETIQUETTE`
(`H18:N18`) à la feuille `ORIFICES CAPACITES`, cliquer « Enregistrer le profil », revenir sur la
page — **le champ a disparu**. Diagnostiqué d'abord comme un bug de persistance, en réalité une
étape intermédiaire manquée : le bouton « Enregistrer les modifications », au milieu de la page.

L'éditeur a **trois niveaux de commit**, dont un seul persiste :

| Niveau | Action | Effet réel |
| :--- | :--- | :--- |
| 3 | `BlockFieldForm` → « Ajouter le champ » | ajoute à `SheetRuleForm._fields`, **interne au sous-formulaire** |
| 2 | `SheetRuleForm.Submit()` → « Enregistrer les modifications » | `OnSubmit` → `HandleSaveSheetRule` → `_sheetRules[index]`, **en mémoire** |
| 1 | `ImportProfileEditor.SaveProfileAsync()` → « Enregistrer le profil » | **seule** écriture en base |

Trois griefs en découlent, tous traités par ce lot :

1. **Trop de clics** — 4 clics pour un champ ajouté (Modifier / Ajouter le champ / Enregistrer les
   modifications / Enregistrer le profil).
2. **Le bouton de niveau 2 est presque invisible** — et le code confirme pourquoi, précisément :
   `SheetRuleForm.razor:461` porte `class="@(ShowCancel ? "btn btn-outline-secondary w-100 mt-3" :
   "btn btn-secondary w-100 mt-3")"`. En mode **édition** (`ShowCancel="true"`), le bouton de
   soumission est donc `btn-outline-secondary` — **exactement la même chaîne de classes que le bouton
   « Annuler »** juste à côté (`:474`). En mode ajout il est plein. Écart déjà relevé en §2.3 de
   `audit-design-blazoradmin-2026-07-27.md`, explicitement laissé hors périmètre par le lot 053.
3. **Perte silencieuse** — ce qui est affiché à l'écran au moment du clic sur « Enregistrer le
   profil » n'est pas ce qui part en base, et **rien n'avertit** : `_hasUnsavedChanges` (lot 043)
   n'est levé que par `MarkAsChanged()`, appelé depuis les 8 points de mutation de l'éditeur racine
   (`AddDefaultTableau`, `HandleAddSheetRule`, `HandleSaveSheetRule`, `DeleteSheetRule`, …) —
   **jamais** depuis l'intérieur de `SheetRuleForm`, qui n'expose que `OnSubmit` et `OnCancel`.

Tout est transposable à l'export : `ExportProfileEditor.razor` est un miroir structurel
(`_editingIndex`, `_pendingDeleteIndex`, `_hasUnsavedChanges`, `MarkAsChanged()`,
`SheetGenerationRuleForm` rendu inconditionnellement dans une carte, même CTA). Chaque sous-ticket est
à livrer **des deux côtés**, clôture par le test de parité (56.8).

---

## Décisions actées avec Simon (29/07)

| Sujet | Décision |
| :--- | :--- |
| Modèle d'enregistrement | **Flush implicite au save** (« option 1a ») : « Enregistrer le profil » commet d'abord le formulaire de feuille ouvert, puis persiste **en une seule écriture**. |
| Autosave | **Écartée.** Le profil est lu en direct par `POST /api/oxo/process` et il n'existe ni brouillon/publication, ni undo : l'écriture unique et atomique est une propriété à défendre. |
| Versionnement des profils | **Hors scope, écarté** (jugé overkill par Simon le 29/07). N'apparaît plus comme piste dans ce lot ni dans les suivants. |
| Modèle d'édition mutable (ViewModel) | **Écartée** : coût de 2 à 3 lots et forte réécriture des tests, pour **le même nombre de clics** que le flush implicite sur le scénario réel. |
| Maître-détail (une feuille = une route) | **Écartée** : muterait la base en plusieurs fois, et imposerait d'autoriser un profil à 0 feuille — décision de validation actée, non rouverte. |
| Réduction des clics | **Trois leviers retenus** : validation au blur/Entrée des sous-listes (56.4), `Ctrl+Entrée` (56.5), barre d'enregistrement collante (56.6). Résultat visé : **2 clics et zéro défilement**. |
| Suppression du clic « Modifier » de la carte de feuille | **Non retenue** : impliquerait de rendre les sous-listes de la carte lecture-seule directement éditables, donc de faire converger carte et formulaire — ce qui contredit l'exclusion mutuelle retenue au lot 057. |

**Décompte visé** (scénario d'origine, clic « Modifier » de la carte inclus) :

| | Clics | Défilement |
| :--- | :--- | :--- |
| Aujourd'hui | 4 | oui |
| Après ce lot | **2** | non |

---

## Constats vérifiés dans le code (29/07, dépôt `C:\AM-OXO-ETL`, arbre au commit courant)

Ces points étaient des hypothèses à la rédaction ; ils ont été **lus dans le code** avant publication.
Les numéros de ligne sont ceux du 29/07 et dériveront — ils servent de repère, pas de contrat.

1. **`SheetRuleForm.Submit()` ne rend aucun signal de succès** (`:757`, `private async Task Submit()`).
   En cas d'échec il pose `_errorMessage` et retourne. Il attrape **trois** types :
   `DomainValidationException or DomainArgumentOutOfRangeException or DomainRuleViolationException`
   (`:782`). `BlockFieldForm.Submit()` (`:108`) n'attrape que `DomainValidationException`, et
   `ImportProfileEditor.SaveProfileAsync()` (`:635`) attrape `DomainValidationException` +
   `ProfileNameAlreadyExistsException`. **Trois périmètres d'attrapage différents** : à ne pas
   uniformiser au passage, mais à connaître pour 56.2.
2. **La clé `ImportProfileEditor_SaveChangesButton` a 12 usages**, dont **un seul** est le bouton de
   niveau 2 visé par la remarque 2 (`ImportProfileEditor.razor:304`). Les 11 autres sont les
   `SubmitLabel` des sous-formulaires imbriqués (`SheetRuleForm.razor:62`, `:357`, `:415`) et les
   `aria-label`/`title` des boutons icône d'édition en ligne (`ImportProfileEditor.razor:115-116`,
   `:214-215`, `SheetRuleForm.razor:135-136`, `:243-244`). **Renommer la valeur de cette clé
   changerait 12 libellés** → 56.1 crée une **clé neuve**, il ne renomme pas l'existante.
3. **La carte lecture-seule est bien remplacée** par le formulaire en mode édition
   (`ImportProfileEditor.razor:298` : `<li class="@(_editingIndex == index ? "sheet-rule-editing-item"
   : "sheet-rule-card")">`). Il n'y a **pas** de duplication carte + formulaire — l'hypothèse
   inverse, envisagée à la rédaction, est fausse.
4. **Le formulaire d'ajout de feuille est rendu inconditionnellement**
   (`ImportProfileEditor.razor:418-423`, `ExportProfileEditor.razor:170-174`), dans
   `<div class="card mb-3"><div class="card-header">…AddSheetHeading</div><div class="card-body">`.
   Il n'existe **aucun** état d'ouverture pour lui : c'est le lot 057 qui l'introduira.
5. **`SheetRuleForm.ResetForm()` (`:790`) vide les 9 listes et champs d'état**, et est appelé après
   `OnSubmit` **y compris en mode édition** — inoffensif aujourd'hui puisque `HandleSaveSheetRule`
   remet `_editingIndex = null` et démonte le composant. À ne pas casser en 56.2.
6. **8 composants** — et non 6 — portent le même conditionnel de classe de soumission :
   `SheetRuleForm`, `BlockFieldForm`, `HeaderFieldRuleForm`, `HeaderCompositeRuleForm`,
   `SheetGenerationRuleForm`, `ColumnDefinitionForm`, `PointColumnDefinitionForm`,
   `ApplicationColumnDefinitionForm`. 56.7 les couvre **tous les huit**.
7. **Nombre de champs par sous-formulaire** (détermine le périmètre de 56.4) :
   `BlockFieldForm` 2 inputs / 0 select ; `HeaderCompositeRuleForm` 2 / 0 ;
   colonnes inconditionnelles 1 input (en ligne dans `SheetRuleForm`) ;
   `HeaderFieldRuleForm` 4 inputs + 1 case à cocher ; règles de point 3 inputs + 1 select ;
   `ColumnDefinitionForm` 1 input + **2 selects** ; `PointColumnDefinitionForm` 3 inputs ;
   `ApplicationColumnDefinitionForm` 3 inputs.
   → **Aucun sous-formulaire côté export n'est éligible** à 56.4. L'asymétrie est factuelle.
8. **Les tests de parité existants** (`ProfileEditorParityTests.cs`, 15 méthodes) comparent notamment
   `RootContainer_CssClass_…`, `IntermediateAddButton_CssClass_…`, `FinalSaveButton_CssClass_…`,
   `AllAddButtons_CarryW100_OnBothEditors`,
   `ProfileEditorContainer_HasNoInlineStyle_OnlyTheMaxWidthCssClass`,
   `NavigationLockAndUnsavedChangesConfirmation_AreStructurallyIdenticalBetweenImportAndExportEditors`.
   `IntermediateAddButton_CssClass_…` porte sur le bouton en mode **ajout** (`ShowCancel="false"`) :
   56.7 ne modifiant que la branche `ShowCancel="true"`, **ce test reste vert sans modification**.
   `ProfileEditorContainer_HasNoInlineStyle_…` **contraint 56.6** : la barre collante ne doit
   introduire aucun `style` inline sur le conteneur.
9. **Aucun raccourci clavier de niveau page** n'existe dans les deux éditeurs (aucun `@onkeydown`) :
   56.5 n'entre en conflit avec rien.
10. **`ImportProfileEditor.razor:425-433`** : l'indicateur `#unsaved-changes-indicator` est un `<span>`
    frère, placé **avant** le `<div class="right-aligned-actions">` qui contient
    `#save-profile-button` (`btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4`). Les deux sont dans
    `.profile-editor-container` (lot 53.1). Structure identique côté export (`:177-185`).

**Vérifications résiduelles** (non lues, à faire au fil des sous-tickets, sans surprise attendue) :
le contenu de `BlazorAdminMessages.resx`/`.fr.resx` (existence exacte des clés citées),
le corps des assertions de `ImportProfileEditorTests.cs` (144 Ko) et `ExportProfileEditorTests.cs`
(86 Ko), et `SheetGenerationRuleForm.razor` en détail (seuls ses ancres ont été relevées).

---

## Décisions antérieures explicitement rouvertes par ce lot

- **Lot 043 (`_hasUnsavedChanges` levé uniquement par les mutations de l'éditeur racine)** → rouvert
  par 56.3. C'est la correction d'un angle mort vérifié (constat 3 de l'origine, constat  ci-dessus),
  pas un changement d'intention.
- **§2.3 de `audit-design-blazoradmin-2026-07-27.md` / hors périmètre du lot 053 (« le bouton Annuler
  des sous-formulaires et son partage de classe avec le bouton principal — non retenu dans ce lot, à
  traiter séparément le cas échéant »)** → c'est ce lot qui le traite (56.7). Le lot 053 avait prévu
  ce renvoi ; il n'y a pas de contradiction.
- **Le commentaire de `SheetRuleForm.razor:457-459`** (« "Save changes" […] keeps its pre-existing
  btn-outline-secondary + Check icon (Lot 041) — the ticket's hierarchy table only re-colors "add"
  buttons ») documente le choix que 56.7 renverse. **Ce commentaire est à mettre à jour**, pas à
  laisser contredire le code.

Tout le reste des lots 030 / 041 / 043 / 047 / 048 / 053 reste fermé. En particulier : le CTA final
garde `btn-primary btn-lg` (53.4), le conteneur 1140 px reste tel quel (53.1), la ligne unique
champ + bouton des Tableaux/Applications reste telle quelle (53.3).

---

## Conventions déjà en place à respecter (tout le lot)

- `convention-ui-blazor-alignement-boutons.md` — `.right-aligned-actions` reste le motif
  d'alignement, y compris **à l'intérieur** de la barre collante de 56.6. Noter la règle existante
  `.sheet-rule-card > .right-aligned-actions` (`app.css:169`), volontairement limitée aux cartes.
- `convention-ui-blazor-icones-boutons.md` — aucune icône n'est ajoutée ni retirée par ce lot
  (l'apparence et le gabarit des boutons sont traités au lot 058).
- IDs HTML stables sur tout élément interactif ; **jamais** de sélection par texte ou position en
  bUnit.
- bUnit ne calcule aucun layout : tous les tests portent sur des classes CSS, la structure DOM et le
  comportement, jamais sur des pixels.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit 2.7.2.
- Aucune nouvelle dépendance CSS/JS. CSS custom centralisé dans `app.css`, jamais dupliqué dans deux
  `.razor.css`.
- Validation métier : **un seul chemin**. Les objets Domain continuent d'être construits dans un
  `try/catch` et les messages passent par `BusinessExceptionLocalizer`. Ce lot n'introduit aucune
  règle de validation côté UI.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite (tout le lot)

- **Autosave, brouillon/publication, versionnement des profils** — écartés par décision.
- **Modèle d'édition mutable / ViewModel** et **maître-détail par route** — écartés.
- **Le clic « Modifier » de la carte de feuille** — conservé.
- **Le repli du formulaire d'ajout de feuille et l'exclusion mutuelle** — c'est le lot 057. Ce lot ne
  touche pas à *quand* un formulaire est rendu, seulement à ce qui se passe quand il est ouvert.
- **La teinte des boutons et le gabarit icône + libellé** — c'est le lot 058. 56.7 pose la **classe**
  `btn-secondary` ; sa couleur reste le taupe actuel jusqu'à la livraison de 058. **C'est attendu et
  transitoire**, pas un défaut à corriger dans ce lot.
- **L'uniformisation des trois périmètres d'attrapage d'exceptions** (constat 1) — chacun reste tel
  quel ; les élargir est un sujet en soi.
- **L'intérieur des `form-floating`** (input avant label, `placeholder` non vide) — acquis fragile du
  lot 030 Partie B, verrouillé par `FormFloatingStructureAuditTests`. Si une étape semble exiger de
  réordonner un `input` et son `<label>`, s'arrêter et le signaler.
- **`input-group`** — interdit (rouvrirait 30.6), y compris pour 56.6.
- **Les sous-formulaires non éligibles à 56.4** (constat 7) — le bouton reste le seul chemin d'ajout.
- **Les listes « Tableaux » et « Applications »** de l'éditeur racine — exclues de 56.4 : elles sont
  déjà à 1 clic sur une ligne unique depuis 53.3, et le lot 058 traite précisément la hauteur de leur
  bouton. Leur retirer ce bouton rouvrirait 53.3 et rendrait 58.2 sans objet.
- **L'attribut `[Authorize]` des quatre pages éditeur** et leurs routes — inchangés. Les tests HTTP du
  lot 052 (`BusinessPageAuthorizationHttpTests`) doivent rester verts **sans modification**.
- **Toute modification Domain / Application / pipeline** — ce lot est strictement Razor + CSS +
  `.resx` + tests.
- **L'écart §2.2 de l'audit design** (CTA principaux sans icône) — non traité ici.

---

## 56.1. Nouvelle clé de libellé pour le bouton de niveau 2 : il annonçait un enregistrement inexistant

**Comportement attendu** :
- En **mode édition** d'une règle de feuille, le bouton de soumission n'affiche plus « Enregistrer les
  modifications » mais **« Appliquer les modifications »**. En **mode ajout**, le libellé (« Ajouter la
  feuille ») est déjà exact et **ne change pas**.
- **Deux clés neuves** (EN + FR) : `ImportProfileEditor_ApplySheetRuleButton` et
  `ExportProfileEditor_ApplySheetRuleButton`. Elles remplacent l'usage de
  `*_SaveChangesButton` **au seul point d'appel du niveau 2** — `ImportProfileEditor.razor:304` et
  `ExportProfileEditor.razor:85`.
- **`ImportProfileEditor_SaveChangesButton` n'est ni renommée ni modifiée** : ses 11 autres usages
  (constat 2) sont des sous-formulaires imbriqués et des `aria-label` de boutons icône, où
  « Enregistrer les modifications » reste exact — ils enregistrent bien dans l'état du formulaire
  parent, qui est leur horizon.

**Pourquoi c'est un ticket à part entière** : la cause racine de l'incident du 29/07 est un libellé qui
promettait un enregistrement. Le flush implicite (56.2) rend l'oubli inoffensif, mais ne corrige pas le
mensonge — un utilisateur qui clique « Appliquer » sait qu'il reste quelque chose à faire ; un
utilisateur qui clique « Enregistrer » a légitimement terminé.

**Tests** (bUnit) — **rouges d'abord** :
- Mode édition (`#modify-sheet-rule-button-0` cliqué) : `#save-sheet-rule-button-0` rend le libellé de
  la clé neuve, lu **depuis la ressource localisée**, pas une chaîne littérale dupliquée dans le test.
- Mode ajout : `#add-sheet-rule-button` rend toujours `*_AddSheetButton` (non-régression).
- Un bouton de sous-formulaire imbriqué (ex. `#edit-0-add-block-field-button` en mode édition) rend
  toujours l'ancien libellé — garde-fou prouvant qu'on n'a pas touché aux 11 autres usages.
- Miroir export.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
`Resources/BlazorAdminMessages.resx` + `.fr.resx`.

---

## 56.2. Flush implicite : « Enregistrer le profil » commet d'abord le formulaire ouvert

**Comportement attendu** :
- Si un formulaire de règle de feuille est ouvert (mode ajout **ou** mode édition) au moment du clic
  sur `#save-profile-button` / `#save-export-profile-button`, l'éditeur **tente d'abord de le
  commettre**, puis persiste le profil en **une seule** écriture (`SaveAsync` appelé une fois).
- **Succès** : la règle est intégrée à `_sheetRules` avant la construction de l'`ImportProfile`
  persisté. Le formulaire se referme comme s'il avait été soumis normalement.
- **Échec de validation Domain** : **aucune persistance** — `SaveAsync` n'est **jamais** appelé,
  aucune navigation, le formulaire reste ouvert avec sa saisie intacte, et le message localisé
  s'affiche à l'endroit habituel du formulaire (`_errorMessage` → `alert alert-danger role="alert"`),
  pas dans une seconde zone d'erreur au niveau de la page (`_profileErrorMessage` n'est pas utilisé
  pour ça). Une seule vérité d'erreur.
- Aucun formulaire ouvert : comportement strictement identique à aujourd'hui.

**Implémentation** :
- `SheetRuleForm.Submit()` (`:757`) est **transformé** en
  `public async Task<bool> TryCommitAsync()` — même corps, `return true` après `ResetForm()`,
  `return false` dans le `catch`. `Submit()` devient
  `private async Task Submit() => await TryCommitAsync();` (le `@onclick` du bouton reste inchangé).
  **Un seul chemin de validation** : pas de seconde implémentation en parallèle, sinon deux vérités
  divergeront au premier changement de règle Domain.
- L'éditeur conserve une référence `@ref` sur le composant de formulaire ouvert (il y en a au plus
  deux emplacements de rendu aujourd'hui : le `SheetRuleForm` d'édition à `:301` et celui d'ajout à
  `:421` ; le lot 057 réduira ça à un seul état, mais ce lot n'a pas à l'attendre).
- `SaveProfileAsync` commence par tenter le commit du formulaire ouvert et **retourne immédiatement**
  si celui-ci échoue.
- `TryCommitAsync()` est **aussi** le point d'entrée réutilisé par le lot 057 (fermeture implicite d'un
  formulaire lors de l'ouverture d'un autre). Sa signature est un **contrat entre les deux lots**.
- Idem `SheetGenerationRuleForm` côté export.

**Ordre des événements navigateur à connaître** (et à ne pas « corriger ») : `mousedown` → `blur` →
`click`. Un utilisateur qui remplit la ligne de saisie d'un sous-formulaire (56.4) puis clique
directement sur le CTA déclenche donc **d'abord** la validation au blur, **ensuite** le flush du
formulaire de feuille, **enfin** la persistance. C'est exactement le scénario du 29/07 réduit à son
minimum, et c'est testé.

**Tests** (bUnit) — **rouges d'abord** :
- **Le test qui reproduit littéralement l'incident** : profil existant, `#modify-sheet-rule-button-0`,
  ajouter un champ de bloc via `#edit-0-add-block-field-button`, cliquer `#save-profile-button`
  **sans** cliquer `#save-sheet-rule-button-0` → le profil relu depuis le store porte le nouveau champ
  de bloc.
- Mode **ajout** : remplir le formulaire de nouvelle feuille complet, cliquer le CTA sans cliquer
  `#add-sheet-rule-button` → la feuille est présente dans le profil persisté.
- **Invalide** : formulaire ouvert avec une valeur que le Domain rejette (ex. `#edit-0-sheet-rule-step-input`
  à `0`, qui lève déjà aujourd'hui) → `SaveAsync` **jamais** appelé, alerte présente dans le
  formulaire, formulaire toujours rendu, aucune navigation.
- **Un seul appel** : `SaveAsync` appelé exactement une fois dans le cas de succès (le flush ne doit
  pas produire d'écriture intermédiaire).
- Aucun formulaire ouvert : `SaveAsync` reçoit le même profil qu'avant le lot (non-régression).
- Chaîné avec 56.4 : remplir la ligne de saisie d'un sous-formulaire puis cliquer **directement** le
  CTA → le champ est dans le profil persisté.
- Miroir export sur les 5 premiers.

**Refactor** : **effort élevé** sur ce sous-ticket uniquement. Le point de conception réel est
l'unicité du chemin de validation entre `Submit()` et `TryCommitAsync()` ; tout le reste du lot est de
l'application.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `SheetRuleForm.razor`,
`SheetGenerationRuleForm.razor`.

---

## 56.3. Indicateur « modifications non enregistrées » étendu à l'état intra-formulaire

**Constat vérifié** : `MarkAsChanged()` (`ImportProfileEditor.razor:473`) est appelé par les 8 points
de mutation de l'éditeur racine, et par aucun chemin interne à `SheetRuleForm`. Une feuille ouverte,
modifiée, puis abandonnée par navigation ne déclenche **ni** `#unsaved-changes-indicator`, **ni** la
confirmation de `NavigationLock` — le garde-fou du lot 043 ne couvre pas le cas qui a causé
l'incident.

**Comportement attendu** :
- Toute **modification** d'un champ du formulaire de feuille ouvert, et toute mutation de ses
  sous-listes internes (ajout, modification, suppression, y compris via `BlockFieldForm`,
  `HeaderFieldRuleForm`, `HeaderCompositeRuleForm`), lève `_hasUnsavedChanges` dans l'éditeur racine.
- La **simple ouverture** d'un formulaire ne le lève **pas** : `#modify-sheet-rule-button-0` puis
  « Annuler » sans rien saisir ne doit pas produire d'avertissement (faux positif évitable).
- « Annuler » **ne remet pas** le drapeau à `false` : d'autres modifications peuvent être en attente
  ailleurs. Un faux positif d'avertissement est acceptable ; un faux négatif ne l'est pas.
- Le drapeau retombe à `false` **uniquement** après un `SaveAsync` réussi (`:649`) — règle du lot 043,
  inchangée.
- Mécanisme : un `[Parameter] EventCallback OnDirty` sur `SheetRuleForm` / `SheetGenerationRuleForm`,
  invoqué depuis les points de mutation **déjà existants** de ces composants (`AddBlockField`,
  `HandleSaveBlockField`, `DeleteBlockField`, `AddUnconditionalColonneName`,
  `SaveUnconditionalColonneEdit`, `DeleteUnconditionalColonneName`, `AddPointRule`, `SavePointRuleEdit`,
  `DeletePointRule`, `AddHeaderField`, `HandleSaveHeaderField`, `DeleteHeaderField`,
  `AddHeaderComposite`, `HandleSaveHeaderComposite`, `DeleteHeaderComposite`) plus un
  `@bind:after` sur les 4 champs du locator — **pas** de nouveau système d'événements, et surtout pas
  de `StateHasChanged` forcé côté parent.

**Tests** (bUnit) — **rouges d'abord** :
- Entrer en édition, modifier `#edit-0-sheet-rule-stop-field-name-input` **sans** soumettre →
  `#unsaved-changes-indicator` présent.
- Entrer en édition puis `#cancel-sheet-rule-button-0` **sans rien modifier** → indicateur **absent**.
- Entrer en édition, ajouter un champ de bloc, **sans** soumettre la feuille → indicateur présent.
- Supprimer un champ de bloc d'une feuille en édition, sans soumettre → indicateur présent.
- Après un enregistrement réussi → indicateur absent (non-régression 043).
- Miroir export.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `SheetRuleForm.razor`,
`SheetGenerationRuleForm.razor`.

---

## 56.4. Validation d'une sous-liste au blur du dernier champ, ou sur Entrée

**Périmètre strict, établi par lecture** (constat 7) — sous-listes à **1 ou 2 champs texte** et
**aucun `<select>`, aucune case à cocher** :

| Sous-liste | Composant | Champs |
| :--- | :--- | :--- |
| Champs du bloc | `BlockFieldForm` | `{p}name-input`, `{p}absolute-range-input` |
| Champs composés d'en-tête | `HeaderCompositeRuleForm` | 2 inputs |
| Colonnes inconditionnelles | en ligne dans `SheetRuleForm` | `{p}unconditional-colonne-name-input` |

**Exclues** : règles de point (3 inputs + 1 select), `HeaderFieldRuleForm` (4 inputs + 1 case),
`ColumnDefinitionForm` (1 input + 2 selects), `PointColumnDefinitionForm` et
`ApplicationColumnDefinitionForm` (3 inputs), et les listes Tableaux/Applications de l'éditeur racine
(voir Hors périmètre). **Aucun sous-formulaire côté export n'est éligible** — l'asymétrie est
factuelle, pas un défaut de parité ; elle se documente comme le fait déjà
`ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion`.

**Ce sous-ticket ne restructure rien.** Le formulaire d'ajout est **déjà** rendu en fin de sous-liste
(vérifié : `SheetRuleForm.razor:100` pour `BlockFieldForm`, `:190-197` pour les colonnes
inconditionnelles, `:450-453` pour les champs composés). Il n'y a **pas** de « ligne vide » à créer :
on change uniquement le **déclencheur** de la validation.

**Comportement attendu** :
- La validation est tentée quand :
  - **(a)** le focus quitte le **dernier** champ de la ligne de saisie, **ou**
  - **(b)** `Entrée` est pressée dans **n'importe lequel** de ses champs ;
  - et, dans les deux cas, **uniquement si tous** les champs de la ligne sont non vides.
- **Succès** : l'objet Domain est construit et `OnSubmit` invoqué comme s'il y avait eu clic ;
  `ResetForm()` vide les champs ; le focus revient sur le premier champ de la ligne (saisie en rafale
  sans souris).
- **Échec** (le Domain lève, ou `BlockFieldRangeFormatter.FromAbsoluteRange` refuse la plage) : la
  saisie est **conservée**, le message localisé s'affiche là où il s'affiche déjà aujourd'hui, rien
  n'est ajouté.
- **Ligne partiellement remplie** au blur : **rien ne se passe** — ni ajout, ni message d'erreur.
  Quitter un champ à moitié rempli pour cliquer ailleurs est un geste normal, il ne doit jamais
  produire une alerte rouge.
- **Le bouton d'ajout reste en place et fonctionnel.** Il n'est pas supprimé : il porte l'affordance
  (« cette ligne crée un élément ») et le supprimer rouvrirait les assertions de 53.4 et
  `AllAddButtons_CarryW100_OnBothEditors` pour un gain nul. Il devient un chemin de confirmation
  **redondant** — le clic devient optionnel, c'est tout le but.
- L'avertissement non bloquant `ImportProfileEditor_ExcelRangeBeyondPracticalRangeWarning`
  (`BlockFieldForm.razor:127-130`) continue de s'afficher après un ajout réussi, quel que soit le
  déclencheur.

**Tests** (bUnit) — **rouges d'abord** :
- `#block-field-name-input` + `#block-field-absolute-range-input` renseignés, `Blur` sur le
  **dernier** → l'élément est présent dans la liste rendue, **sans aucun clic**.
- Un seul des deux renseigné, `Blur` → aucun élément ajouté **et** aucune alerte rendue.
- Les deux renseignés, `KeyDown` `Enter` sur le **premier** → l'élément est ajouté.
- Plage invalide (`"ZZZ"`) + `Blur` → `ImportProfileEditor_InvalidExcelRangeError` présent, aucun
  élément ajouté, **valeurs de saisie conservées** dans les deux inputs.
- Après un ajout réussi, les deux inputs sont vides.
- Plage au-delà du seuil de plausibilité (`BA1`) + `Blur` → élément ajouté **et** avertissement
  non bloquant présent (non-régression du lot N).
- **Non-régression** : le clic sur le bouton d'ajout ajoute toujours l'élément (tests existants
  réutilisés, **pas** réécrits).
- Idem pour les colonnes inconditionnelles (1 champ) et les champs composés (2 champs).
- **Garde-fou de non-généralisation** : sur une sous-liste exclue (ex. règles de point), renseigner
  tous les champs puis `Blur` du dernier → **rien** n'est ajouté ; seul le clic ajoute.

**Effort** : standard.

**Dossier** : `BlockFieldForm.razor`, `HeaderCompositeRuleForm.razor`, `SheetRuleForm.razor`.

---

## 56.5. `Ctrl+Entrée` enregistre le profil

**Comportement attendu** :
- `Ctrl+Entrée`, depuis n'importe où dans l'éditeur, déclenche exactement le même chemin que le clic
  sur le CTA — donc **avec** le flush de 56.2.
- `Entrée` seule ne déclenche **jamais** l'enregistrement du profil (elle est réservée à la validation
  d'une ligne de sous-liste, 56.4).
- Pas de `Ctrl+S` : il faudrait neutraliser le raccourci natif du navigateur pour un gain identique.
- Découvrabilité : le CTA porte un `title` mentionnant le raccourci. **Deux** clés `.resx` neuves
  (une par éditeur, EN + FR) — les seules du lot avec celles de 56.1.
- Implémentation : `@onkeydown` avec `@onkeydown:preventDefault` sur le `<div
  class="container-fluid px-3 profile-editor-container">` racine (`:32` / `:26`). **Aucune interop
  JS.**

**Tests** (bUnit) — **rouges d'abord** :
- `KeyDown` `Enter` avec `CtrlKey = true` sur le conteneur racine → `SaveAsync` appelé une fois.
- `KeyDown` `Enter` **sans** `CtrlKey` → `SaveAsync` **jamais** appelé.
- Le raccourci passe bien par le flush : formulaire ouvert et modifié + `Ctrl+Entrée` → le contenu du
  formulaire est dans le profil persisté (dépend de 56.2, donc à écrire après).
- `#save-profile-button` porte un `title` non vide issu de la ressource localisée.
- Miroir export.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`,
`Resources/BlazorAdminMessages.resx` + `.fr.resx`.

---

## 56.6. Barre d'enregistrement collante

**Pourquoi** : le CTA est au bout d'une page très longue. Chaque enregistrement coûte un défilement
aller, un clic, et la perte du repère visuel. Ce sous-ticket ne retire aucun clic mais supprime ce coût
caché, et rend l'état « modifications non enregistrées » lisible en permanence — ce qui, combiné à
56.3, referme la remarque 3 sous son angle perception.

**Comportement attendu** :
- `#unsaved-changes-indicator` **et** le CTA (aujourd'hui frères à `:425-433`) sont regroupés dans une
  barre `position: sticky; bottom: 0`, en bas du conteneur d'éditeur, visible en permanence sur les
  **quatre** routes.
- Le CTA conserve **exactement** `btn btn-primary w-100 w-md-auto btn-lg mt-4 mb-4` — 53.4, 53.1 et
  `FinalSaveButton_CssClass_IsIdenticalBetweenImportAndExportEditors` ne sont pas rouverts.
- La barre reste **à l'intérieur** de `.profile-editor-container` : elle ne s'étend pas d'un bord de
  l'écran à l'autre sur grand écran.
- Le conteneur reçoit un `padding-bottom` au moins égal à la hauteur de la barre, pour que celle-ci ne
  recouvre jamais le dernier champ.
- `.right-aligned-actions` est conservé **à l'intérieur** de la barre. Ne pas réutiliser le sélecteur
  `.sheet-rule-card > .right-aligned-actions` (`app.css:169`), volontairement limité aux cartes.
- **Une seule** déclaration CSS, dans `app.css` (nom proposé : `.profile-editor-save-bar`), jamais
  dupliquée dans un `.razor.css`.
- Mobile (< 768px) : la barre reste collante, le CTA garde sa pleine largeur. **Aucune** largeur fixe
  ni `min-width` — un `max-width` seul est admis (53.5).
- **Aucun `style` inline** sur le conteneur ni sur la barre :
  `ProfileEditorContainer_HasNoInlineStyle_OnlyTheMaxWidthCssClass` doit rester vert sans
  modification.

**Tests** (bUnit) :
- `#save-profile-button` est **descendant** d'un élément portant `.profile-editor-save-bar`.
- `#unsaved-changes-indicator` est descendant du **même** élément (quand il est rendu).
- Le CTA conserve `btn-primary` et `btn-lg`, et reste dans un `.right-aligned-actions`.
- Ni la barre ni le conteneur ne portent d'attribut `style`.
- Miroir export, chaîne de classes identique (verrouillé en 56.8).

**Vérification manuelle attendue** (non testable en bUnit, à consigner) : sur grand écran et sur
mobile, la barre ne recouvre pas le dernier champ, ne crée pas de seconde barre de défilement, et
n'entre pas en conflit avec le bandeau `#unsaved-changes-navigation-confirmation` du lot 043 quand
celui-ci est affiché.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `ExportProfileEditor.razor`, `wwwroot/app.css`.

---

## 56.7. Distinguer visuellement soumission et annulation dans les huit sous-formulaires

**Constat vérifié** (constat 6) : dans les **8** composants concernés, le bouton de soumission porte
`class="@(ShowCancel ? "btn btn-outline-secondary w-100 mt-3" : "btn btn-secondary w-100 mt-3")"` et le
bouton « Annuler » porte `class="btn btn-outline-secondary w-100 mt-3"`. En mode édition, les deux sont
donc **strictement identiques**.

**Comportement attendu** :
- Le conditionnel de **classe** disparaît : le bouton de soumission est **toujours**
  `btn btn-secondary w-100 mt-3`, en ajout comme en édition.
- Le conditionnel d'**icône** est conservé tel quel : `AdminIconMarkup.Check` si `ShowCancel`,
  `AdminIconMarkup.Plus` sinon (lot 041, non rouvert).
- Le bouton « Annuler » reste `btn btn-outline-secondary w-100 mt-3` (contour), inchangé.
- Le CTA final reste seul en `btn-primary btn-lg` : il n'y a jamais deux boutons rouges à l'écran.
- Les commentaires de code de 53.4 qui documentent l'ancien choix (`SheetRuleForm.razor:457-459`,
  `BlockFieldForm.razor:34-35`, et leurs équivalents dans les 6 autres composants) sont **mis à jour**,
  pas laissés à contredire le code.

**Pourquoi pas `btn-primary` sur la soumission de sous-formulaire** : avec la barre collante (56.6), le
CTA rouge est visible en permanence. Un second bouton rouge dans le formulaire ouvert mettrait deux
actions rouges en concurrence à tout instant, ce que 30.3 puis 53.4 ont précisément cherché à éviter.

**Tests** (bUnit) — **rouges d'abord** :
- Pour chacun des **8** sous-formulaires, en mode édition : le bouton de soumission porte
  `btn-secondary` et **ne porte plus** `btn-outline-secondary` (présence **et** absence).
- Le bouton « Annuler » porte `btn-outline-secondary` et **ne porte pas** `btn-secondary`.
- Test explicite d'intention : les chaînes de classes des deux boutons **diffèrent** — c'est l'objet
  même du sous-ticket, et ce qui empêche une régression par copier-coller.
- Mode ajout : le bouton de soumission porte toujours `btn-secondary` (non-régression 53.4) →
  `IntermediateAddButton_CssClass_IsIdenticalBetweenImportAndExportEditors` et
  `AllAddButtons_CarryW100_OnBothEditors` restent verts **sans modification**.
- Le CTA final conserve `btn-primary btn-lg`.
- Les tests de 30.3 / 53.4 qui asseraient `btn-outline-secondary` sur la branche édition sont
  **corrigés dans leur intention**, pas doublés ni supprimés (même exigence qu'en 51.2 et 53.2).

**Effort** : standard.

**Dossier** : les 8 composants du constat 6.

---

## 56.8. Parité structurelle import/export (clôture)

**Comportement attendu** : `ProfileEditorParityTests.cs` compare les chaînes de classes des éléments
équivalents entre les deux éditeurs. Ce lot en modifie deux et en ajoute un :

- **barre d'enregistrement collante** (nouveau comparable, 56.6),
- **bouton de soumission de sous-formulaire en mode édition** (56.7),
- **bouton « Annuler » de sous-formulaire** (56.7, non modifié — présent comme non-régression),
- CTA final (non modifié — déjà couvert par `FinalSaveButton_CssClass_…`).

**Tests** (bUnit) :
- Comparaison de chaîne **stricte** (pas « les deux ont une classe non vide »), dans le style des
  méthodes existantes (`…_CssClass_IsIdenticalBetweenImportAndExportEditors`).
- Ce test doit être **le dernier rendu vert du lot**. S'il passe avant que 56.1–56.7 ne soient
  terminés des deux côtés, c'est qu'il ne compare pas ce qu'il prétend comparer.

**Effort** : standard.

**Dossier** : `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ProfileEditorParityTests.cs`.

---

## Ordre recommandé

1. **56.1** — clé de libellé neuve (petit, isolé, met la suite dans le bon cadre mental)
2. **56.2** — flush implicite (**cœur du lot**, effort élevé au refactor)
3. **56.3** — indicateur étendu
4. **56.4** — validation au blur / Entrée
5. **56.5** — `Ctrl+Entrée` (après 56.2, dont il réutilise le chemin)
6. **56.6** — barre collante
7. **56.7** — distinction soumission / annulation (8 composants)
8. **56.8** — parité structurelle (clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **`TryCommitAsync()` est un contrat inter-lots.** Le lot 057 l'appelle pour fermer implicitement un
  formulaire. Le concevoir en pensant à ce second appelant dès 56.2 évite de le refaire.
- **Un seul chemin de validation.** Si `TryCommitAsync()` finit par contenir une copie de la logique de
  `Submit()`, s'arrêter : c'est la seule erreur de conception réellement coûteuse du lot.
- **Ne pas ajouter d'écriture intermédiaire.** Le flush commet **en mémoire** puis persiste une fois.
  Un `SaveAsync` appelé deux fois est un échec du sous-ticket, pas un détail — c'est la propriété
  d'atomicité qui justifie d'avoir écarté l'autosave.
- **56.7 touche 8 fichiers, pas 6.** La liste est dans le constat 6 ; la vérifier par
  `grep -rl 'ShowCancel ? "btn btn-outline-secondary'` avant de commencer plutôt qu'après.
- **La teinte taupe des boutons pendant ce lot est normale** — 058 la corrige. Ne pas l'améliorer au
  passage.
- **Ne pas toucher à l'intérieur des `form-floating`** (30.6, verrouillé par
  `FormFloatingStructureAuditTests`), ni à `input-group` (interdit).
- Tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~ProfileEditor|FullyQualifiedName~SheetRuleForm|FullyQualifiedName~BlockField" --verbosity quiet`.
- **Effort standard partout, sauf le refactor de 56.2** (élevé).

**Dossiers concernés** :
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` — `ImportProfileEditor.razor`,
`ExportProfileEditor.razor`, `SheetRuleForm.razor`, `SheetGenerationRuleForm.razor`,
`BlockFieldForm.razor`, `HeaderFieldRuleForm.razor`, `HeaderCompositeRuleForm.razor`,
`ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`,
`ApplicationColumnDefinitionForm.razor` ;
`src/ExcelETL.BlazorAdmin/wwwroot/app.css` ;
`src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx` (+ `.fr.resx`) ;
et le miroir `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`.
