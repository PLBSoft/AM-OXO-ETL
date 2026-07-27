# Tickets TDD — Parité visuelle `ExportProfileEditor.razor` ↔ `ImportProfileEditor.razor`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Périmètre
restreint à la parité **visuelle** — labels, cartes, monospace, alignement des boutons. La
parité **fonctionnelle** (Modifier/Supprimer un élément déjà ajouté, confirmation de
suppression) est un ticket séparé, volontairement postérieur :
`tickets-tdd-blazor-profil-export-parite-fonctionnelle.md`. Ce découpage a été discuté et
confirmé avec le client — faire d'abord ce qui est purement additif et sans risque de
régression, valider, puis seulement ensuite investir dans les nouveaux composants d'édition.*

**Contexte** : `convention-ui-blazor-alignement-boutons.md` et la clôture du Lot P
(`tickets-tdd-blazor-profil-import-cartes-regles-feuille.md`) actent explicitement que
l'ensemble des règles UI/UX appliquées aux profils d'import (Lots N, O, P + les deux
follow-up du 22/07 : labels visibles, confirmation de suppression) est **applicable telle
quelle** à `ExportProfileEditor.razor`, mais **reporté volontairement** à un lot ultérieur, le
temps de valider ces règles côté client sur les profils d'import qui servent de modèle.

**Ce ticket est cette réouverture explicite**, demandée par le client : uniformiser
`localhost:7013/export-profiles/*` sur `import-profiles/*`, sans dupliquer de style — même
mise en page, mêmes classes CSS globales, mêmes composants réutilisés autant que possible.

---

## Q0. Constat (revue de code, préalable à toute implémentation)

Comparaison ligne à ligne de `ExportProfileEditor.razor` contre l'état actuel de
`ImportProfileEditor.razor`/`SheetRuleForm.razor`/`BlockFieldForm.razor` après les Lots N/O/P
— uniquement les écarts **visuels** couverts par ce ticket (voir le ticket de parité
fonctionnelle pour le reste du constat) :

| Règle UI/UX déjà en place côté import | État côté export (`ExportProfileEditor.razor`) |
|---|---|
| Labels visibles sur tous les champs de saisie | **Absents partout** — `export-profile-name-input` (l.35), `sheet-generation-rule-name-input`/`sheet-generation-rule-pivot-source-select` (l.78/82), `column-header-input`/`column-source-select` (l.101/105), `point-column-nom-input`/`point-column-header-input`/`point-column-mark-value-input` (l.132/136/140) — placeholder uniquement |
| Règles de feuille en cartes visuellement distinctes (Lot P) | Simple `<ul class="list-group">`/`<li class="list-group-item">` (l.48-65), aucune bordure de carte, aucun en-tête typographique distinct nom/métadonnées |
| Nom de feuille séparé des métadonnées (Lot P) | `<strong>@rule.SheetName</strong> (@rule.PivotSource)` — un seul élément, même poids visuel (l.52) |
| Éléments de liste imbriqués en spans séparés + monospace (Lot O1) | Texte concaténé brut : `@column.Header — @(...Source...)` (l.56/95) et `@pointColumn.ColonneNom / @pointColumn.Header / @pointColumn.MarkValue` (l.60/126) |
| Boutons d'action alignés à droite (convention, Lot P2) | Tous les boutons sont dans le flux naturel (gauche) — `add-column-definition-button`, `add-point-column-definition-button`, `add-sheet-generation-rule-button`, `save-export-profile-button` |

---

## Q1. Labels visibles sur tous les champs de saisie

Parité avec le follow-up « labels visibles » du Lot N côté import.

- `export-profile-name-input` → nouveau label (nouvelle clé resx `ExportProfileEditor_NameLabel`,
  séparée de `ExportProfileEditor_NamePlaceholder` existante, même principe que
  `ImportProfileEditor_NameLabel`/`_NamePlaceholder`).
- `sheet-generation-rule-name-input` → `ExportProfileEditor_SheetNameLabel`.
- `sheet-generation-rule-pivot-source-select` → `ExportProfileEditor_PivotSourceLabel` (le
  `<select>` n'a aujourd'hui aucun placeholder équivalent — un label est donc la seule
  amélioration possible ici, pas de clé `*Placeholder` à dupliquer).
- `column-header-input` / `point-column-header-input` → réutilisent une même
  `ExportProfileEditor_HeaderLabel` (les deux champs partagent déjà `ExportProfileEditor_HeaderPlaceholder`).
- `column-source-select` → `ExportProfileEditor_SourceLabel`.
- `point-column-nom-input` → `ExportProfileEditor_ColonneNomLabel`.
- `point-column-mark-value-input` → `ExportProfileEditor_MarkValueLabel`.

Toutes en EN + FR dans `BlazorAdminMessages.resx`/`.resx.fr`, même clé séparée
label/placeholder que côté import (pas de fusion des deux, même si le texte anglais coïncide
parfois — précédent déjà établi par `AbsoluteRangeLabel`/`AbsoluteRangePlaceholder`).

### Tests (bUnit)
- Un test par label, vérifiant l'association `<label for="...">` ↔ id du champ (même forme que
  les tests de labels existants dans `ImportProfileEditorTests.cs`).

---

## Q2. Cartes par règle de feuille + en-tête distinct (parité Lot P)

- La liste résumé des `SheetGenerationRule` déjà ajoutées (l.48-65) passe de
  `<ul class="list-group">`/`<li class="list-group-item">` à `<ul class="sheet-rule-list">`/
  `<li class="sheet-rule-card">` — **réutilisation verbatim** des classes déjà globales dans
  `wwwroot/app.css` (`.sheet-rule-list`, `.sheet-rule-card`, `.sheet-rule-card-header`,
  `.sheet-rule-card-title`, `.sheet-rule-card-meta`), **aucune nouvelle règle CSS** — c'est
  exactement l'objectif « ne pas dupliquer les styles » de la demande.
- En-tête de carte : `<h4 class="sheet-rule-card-title">@rule.SheetName</h4>` +
  `<span class="sheet-rule-card-meta text-muted">` pour le `PivotSource` (nouvelle clé
  `ExportProfileEditor_SheetMetadata`, un seul placeholder `{0}` — plus simple que
  `ImportProfileEditor_SheetMetadata` qui en a 3, car `SheetGenerationRule` n'a qu'un seul champ
  de métadonnée hors nom/colonnes).
- Le contenu interne (colonnes, colonnes de point) garde son comportement, seul le conteneur et
  l'en-tête changent — même principe que le Lot P côté import.

### Tests (bUnit)
- Rendu avec plusieurs règles ajoutées : chaque règle est enveloppée dans un `.sheet-rule-card`
  distinct (compte de conteneurs == nombre de règles).
- Nom de feuille et métadonnées (`PivotSource`) dans deux éléments séparés.

---

## Q3. Colonnes / colonnes de point en éléments séparés + monospace (parité Lot O1)

- `ColumnDefinition` (Header / Source) et `PointColumnDefinition` (ColonneNom / Header /
  MarkValue), aussi bien dans le résumé de règle (l.54-61) que dans le sous-formulaire « Add a
  sheet rule » (l.90-97, l.121-128), passent du texte concaténé brut à des spans séparés,
  **en réutilisant verbatim les classes déjà globales** `.block-field-list`/`.block-field-item`/
  `.block-field-info`/`.block-field-name`/`.block-field-range` (déjà partagées entre
  `ImportProfileEditor.razor` et `SheetRuleForm.razor`, documentées comme volontairement
  génériques dans `app.css`).
  - `ColumnDefinition` : nom = `Header` (`.block-field-name`), valeur secondaire = le `Source`
    (ou `ExportProfileEditor_SourceNotMapped` si `null`) en `.block-field-range` (muted +
    monospace, cohérent avec l'affichage d'un identifiant technique, comme une plage Excel côté
    import).
  - `PointColumnDefinition` : nom = `Header` (`.block-field-name`), valeur secondaire =
    `"{ColonneNom} · {MarkValue}"` en `.block-field-range`.
- **Point de décision, à trancher avant d'implémenter** : ces classes s'appellent
  `.block-field-*`, un nom hérité du domaine import (`BlockFieldDefinition`) qui n'a plus de
  sens direct une fois réutilisé pour des `ColumnDefinition`/`PointColumnDefinition` côté
  export. Deux options :
  1. **Réutiliser telles quelles** (recommandé) — risque nul, ces classes sont déjà purement
     visuelles (mise en page + typographie), aucune logique n'en dépend, et c'est le choix qui
     colle le mieux à la consigne « éviter de dupliquer les styles ».
  2. Les renommer en quelque chose de neutre (ex. `.definition-item`/`.definition-name`/
     `.definition-value`) — implique de retoucher `ImportProfileEditor.razor`/
     `SheetRuleForm.razor`/`BlockFieldForm.razor` (qui fonctionnent aujourd'hui) uniquement pour
     un renommage cosmétique, avec le risque de régression que ça comporte pour un gain
     symbolique.

  Recommandation : option 1, sauf avis contraire du client.

### Tests (bUnit)
- Chaque colonne/colonne de point affichée avec nom et valeur secondaire dans deux sélecteurs
  distincts (même approche que les tests O1 côté import).

---

## Q6. Alignement à droite des boutons d'action (convention `convention-ui-blazor-alignement-boutons.md`)

Réutilisation verbatim de `.right-aligned-actions` (déjà globale dans `app.css`, zéro nouvelle
règle CSS) sur :
- `add-column-definition-button`, `add-point-column-definition-button` (lignes de saisie —
  déjà en bout de ligne par construction, cf. remarque équivalente du Lot P pour l'import ; à
  confirmer au cas par cas selon le layout retenu en Q1/Q3).
- `add-sheet-generation-rule-button`.
- `save-export-profile-button`.

### Tests (bUnit)
- Non-régression fonctionnelle pure sur les boutons déjà existants (clic déclenche toujours la
  même action) — pas de nouveau test de positionnement CSS, cohérent avec l'absence de ce type
  de test ailleurs dans le projet (même remarque que Lot P2 côté import).

---

## Hors périmètre (explicitement exclu de ce ticket)

- **Modifier/Supprimer un élément déjà ajouté (sheet rule / colonne / colonne de point) et sa
  confirmation de suppression** — écart fonctionnel réel, pas seulement visuel, traité dans
  `tickets-tdd-blazor-profil-export-parite-fonctionnelle.md`, volontairement séquencé après ce
  ticket (voir l'en-tête de ce document).
- `ExportProfileTest.razor` / `ImportProfileTest.razor` — pages de test d'exécution du pipeline,
  aucune règle N/O/P ne les concerne, non retouchées ici.
- Renommage des classes CSS `.block-field-*`/`.sheet-rule-*` vers un nom neutre — voir Q3,
  décision à prendre séparément si le nom actuel gêne réellement.
- Toute nouvelle règle CSS : ce ticket doit pouvoir se faire à review-list CSS strictement
  constante (`wwwroot/app.css` inchangé, aucun nouveau `.razor.css`) — si un sous-lot en
  implémentation révèle un besoin de nouvelle classe, le signaler avant de l'ajouter plutôt que
  de l'introduire silencieusement.

---

## Note d'efficacité d'implémentation

1. Purement additif, zéro nouvelle CSS, risque de régression quasi nul (aucun changement de
   comportement, uniquement de markup/classes) — Q1/Q2/Q3/Q6 peuvent se faire dans n'importe
   quel ordre, voire dans un seul passage.
2. Vérifier la suite bUnit complète de `ExportProfileEditorTests.cs` après chaque sous-lot pour
   confirmer l'absence de régression sur les 10 tests déjà en place.
3. Vérification manuelle en dev server recommandée (captures d'écran) plutôt qu'obligatoire —
   cf. `feedback_browser_preview_caution` : si le Browser pane est instable, se rabattre sur la
   suite bUnit, ne pas cliquer en direct sur un profil seedé partagé (utiliser un profil
   jetable créé via `/export-profiles/new`, jamais enregistré).
4. Une fois ce ticket validé côté client, ouvrir/reprendre
   `tickets-tdd-blazor-profil-export-parite-fonctionnelle.md`.
