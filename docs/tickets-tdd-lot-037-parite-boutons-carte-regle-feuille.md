# Tickets TDD — Lot 037 : parité des boutons Modifier/Supprimer de carte de règle de feuille

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Sixième lot
numérique, après le Lot 036 (`tickets-tdd-lot-036-validation-payload-oxo-process.md`). Fait suite
à `audit-qualite-blazoradmin-2026-07-25.md` §2.1, trié et classé "impact réel" — c'est le même
type de dérive silencieuse que le Lot 030 avait pour but de corriger, sur un point précis que le
Lot 030 n'avait pas couvert.*

**Constat** : au niveau racine d'une carte `.sheet-rule-card` (la carte représentant une règle de
feuille entière, pas les champs de bloc à l'intérieur), les boutons Modifier/Supprimer sont :
- **`ExportProfileEditor.razor:127-149`** : boutons icône seule (`btn-outline-secondary`/
  `btn-outline-danger`, SVG inline, `aria-label`/`title`), conformes à
  `convention-ui-blazor-icones-boutons.md`.
- **`ImportProfileEditor.razor:192-202`** : boutons texte brut sans icône
  (`btn-secondary`/`btn-danger`, libellés `ImportProfileEditor_ModifyButton`/`_DeleteButton`).

Aucun des tests de parité croisée existants (`ProfileEditorParityTests.cs`) ne compare ce point
précis — ils couvrent la grille `.sheet-rule-grid`, la grille de champs `.block-field-grid`, le
disclosure `.sheet-rule-sublist-details`, les conteneurs `form-floating`, les cartes `bg-light`,
le bouton d'ajout intermédiaire et le bouton de sauvegarde final (Lots R/30), mais jamais ce
bouton-ci.

**Décision actée pour ce lot** : **`ExportProfileEditor.razor` est la référence** (déjà conforme à
la convention icônes) — ce lot aligne `ImportProfileEditor.razor` dessus, pas l'inverse. Utiliser
`Shared/AdminIconMarkup` (extrait au Lot 035.5) plutôt que de dupliquer à nouveau du SVG inline.

**Conventions déjà en place à respecter** : `convention-ui-blazor-icones-boutons.md` (icône seule
+ `aria-label`/`title` obligatoires pour une action CRUD standard sur une ligne/carte) ; IDs HTML
stables — **les IDs existants ne changent pas** (`modify-sheet-rule-button-{index}`/
`delete-sheet-rule-button-{index}`), seul leur contenu interne (texte → icône) change ; xUnit
2.9.3 + FluentAssertions 7.x + Moq + bUnit ; aucun JS interop nouveau.

---

## 37.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `ExportProfileEditor.razor:127-149` dans son état actuel exact : structure précise du
  markup des deux boutons icône (classes, structure du `<span>`/SVG, attributs `aria-label`/
  `title`, clés `.resx` utilisées pour ces attributs) — c'est le gabarit exact à reproduire, pas
  une description approximative.
- [ ] Confirmer que `Shared/AdminIconMarkup` (extrait au Lot 035.5) expose bien des constantes
  réutilisables pour les icônes Modifier (crayon)/Supprimer (corbeille) utilisées par
  `ExportProfileEditor.razor` — si `ExportProfileEditor.razor` n'utilise pas encore
  `AdminIconMarkup` lui-même (le Lot 035.5 a factorisé les icônes des pages de liste, pas
  nécessairement celles des éditeurs), vérifier si les icônes des deux contextes (liste vs carte
  d'éditeur) sont identiques avant de réutiliser la même constante, ou s'il faut une constante
  dédiée si le style diverge légitimement.
- [ ] Lire `ImportProfileEditor.razor:192-202` dans son état actuel exact (le double appel
  `_editingIndex = index`/`_pendingDeleteIndex = index`) pour confirmer qu'aucune logique
  fonctionnelle autre que l'affichage n'est attachée au texte des boutons.
- [ ] Lire `ProfileEditorParityTests.cs` intégralement pour confirmer la structure des tests de
  parité déjà écrits (R1/R2/R3/30.5) — ce lot doit ajouter un test dans le **même style** que les
  tests existants, pas introduire un nouveau patron d'assertion.
- [ ] Vérifier si les clés de ressource `ImportProfileEditor_ModifyButton`/`_DeleteButton`
  (actuellement affichées en texte) sont utilisées ailleurs dans le projet (recherche exhaustive)
  avant de décider si elles deviennent des `aria-label` (conservées, réutilisées autrement) ou si
  elles restent orphelines après ce changement (à documenter dans le lot, pas à supprimer sans
  vérification).

---

## 37.1. Alignement d'`ImportProfileEditor.razor` sur le gabarit icône d'`ExportProfileEditor.razor`

**Comportement attendu** : remplacer le texte des boutons Modifier/Supprimer de
`.sheet-rule-card` dans `ImportProfileEditor.razor` par le même gabarit icône seule
qu'`ExportProfileEditor.razor` (SVG via `AdminIconMarkup` ou constante dédiée selon 37.0),
`aria-label`/`title` explicites réutilisant les clés `.resx` existantes (`ImportProfileEditor_ModifyButton`/
`_DeleteButton`) comme valeur de ces attributs plutôt que comme texte visible. **IDs HTML
inchangés** (`modify-sheet-rule-button-{index}`/`delete-sheet-rule-button-{index}`).

**Tests** (bUnit, `ImportProfileEditorTests.cs`) :
- [ ] Les boutons `#modify-sheet-rule-button-{index}`/`#delete-sheet-rule-button-{index}`
  contiennent désormais un `<span>` avec la classe SVG icône attendue (`aria-hidden="true"`),
  avec `aria-label`/`title` explicites, **sans texte visible** — même structure d'assertion que
  les tests d'icônes déjà existants ailleurs dans le projet (Lot V3/028).
- [ ] **Non-régression comportementale** : clic sur Modifier ouvre toujours le formulaire d'édition
  de la règle correspondante ; clic sur Supprimer déclenche toujours la même confirmation/
  suppression — réutiliser les assertions fonctionnelles déjà existantes, ne pas les dupliquer.
- [ ] **Non-régression** : tous les autres tests existants de `ImportProfileEditorTests.cs` qui ne
  portent pas sur ces deux boutons restent verts sans modification.

---

## 37.2. Garde-fou — test de parité croisée dédié à ce bouton

**Comportement attendu** : ajouter dans `ProfileEditorParityTests.cs` un test dédié comparant
explicitement la structure des boutons Modifier/Supprimer de `.sheet-rule-card` entre les deux
éditeurs, sur le modèle des tests de parité existants — pour que toute divergence future sur ce
point précis soit détectée automatiquement, contrairement à aujourd'hui.

**Tests** :
- [ ] Un test qui charge un profil avec au moins une règle de feuille sur les deux éditeurs et
  vérifie que la classe CSS/structure interne des boutons Modifier/Supprimer de la carte de règle
  est **identique** entre `ImportProfileEditor.razor` et `ExportProfileEditor.razor` (comparaison
  de chaîne sur la structure du bouton, pas juste "les deux ont une icône non vide") — même
  patron que `SubformCardContainer_CssClass_IsIdenticalBetweenImportAndExportEditors` ou
  équivalent déjà présent dans le fichier.

---

## Hors périmètre explicite

- Toute modification des boutons Modifier/Supprimer à l'intérieur d'une carte (champs de bloc,
  `BlockFieldForm.razor`) — non concernés par ce constat, déjà couverts par un autre patron.
- Le bouton d'ajout intermédiaire, le bouton de sauvegarde final, la grille responsive — déjà
  couverts et testés par les Lots R/030, non rouverts.
- Toute extension de ce correctif aux boutons d'action de ligne des pages de liste
  (`ImportProfiles.razor`/`ExportProfiles.razor`) — sujet distinct (Lot 035.5 a déjà traité leur
  duplication de code, pas leur style visuel, qui est déjà cohérent entre les deux pages selon
  l'audit) — ne pas mélanger les deux périmètres.
- Toute modification de `convention-ui-blazor-icones-boutons.md` elle-même — ce lot applique la
  convention existante, il ne la révise pas.

---

## Ordre recommandé

1. **37.0** (investigation — confirme le gabarit exact et l'état réel des ressources)
2. **37.1** (correctif applicatif)
3. **37.2** (garde-fou de non-régression, écrit en dernier pour capturer l'état final réellement
   livré par 37.1 plutôt qu'un état anticipé)

## Note d'efficacité d'implémentation

- Ce lot est un cas d'école de "copier un gabarit déjà validé ailleurs dans le même fichier
  projet" — ne pas concevoir un nouveau style d'icône, reprendre exactement celui d'
  `ExportProfileEditor.razor`.
- Le Lot 035.5 a déjà posé les bases d'un dossier `Shared/` pour ce type de constante — vérifier
  en 37.0 s'il est directement réutilisable avant d'introduire une nouvelle constante isolée dans
  `ImportProfileEditor.razor` ou `ExportProfileEditor.razor`.
- 37.2 est la partie la plus importante à ne pas sauter malgré sa taille réduite : c'est le
  garde-fou qui évite qu'un futur correctif ne fasse diverger ce point une troisième fois sans
  être détecté avant une revue visuelle client.
