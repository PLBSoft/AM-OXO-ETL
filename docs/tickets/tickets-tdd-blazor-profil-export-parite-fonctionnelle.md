# Tickets TDD — Parité fonctionnelle `ExportProfileEditor.razor` ↔ `ImportProfileEditor.razor`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Dépend de
`tickets-tdd-blazor-profil-export-parite-visuelle.md` (Q1-Q3, Q6) — à faire **après** ce dernier,
une fois la parité visuelle validée côté client : les nouveaux boutons Modifier/Supprimer
introduits ici doivent naître directement stylés avec les classes finales
(`.right-aligned-actions`, `.block-field-*`) plutôt que d'être restylés une seconde fois.*

**Constat (revue de code)** : contrairement à `ImportProfileEditor.razor`, une fois un élément
ajouté côté export — `SheetGenerationRule` (dans `_sheetRules`), `ColumnDefinition` ou
`PointColumnDefinition` (dans `_newSheetRule.ColumnDefinitions`/`.PointColumnDefinitions`) —
**il n'existe aujourd'hui aucun moyen de le modifier ou de le supprimer** avant
`SaveProfileAsync` : pas de bouton, pas d'état d'édition, pas de confirmation de suppression
(puisqu'il n'y a rien à confirmer). C'est un écart fonctionnel, pas seulement stylistique — une
parité uniquement visuelle laisserait les deux écrans incohérents dans leur comportement, pas
seulement leur apparence.

---

## Q4. Modifier/Supprimer un élément déjà ajouté

Reproduit l'architecture de composants réutilisables add/edit déjà en place côté import
(`SheetRuleForm.razor`/`BlockFieldForm.razor`) — ce n'est pas un simple copier-coller de style.

- **`SheetGenerationRuleForm.razor`** (nouveau composant, à extraire de la partie
  "Add a sheet rule" actuelle de `ExportProfileEditor.razor`) : même forme que
  `SheetRuleForm.razor` — `IdPrefix`/`InitialRule`/`SubmitButtonId`/`SubmitLabel`/`ShowCancel`/
  `CancelButtonId`/`OnCancel`/`OnSubmit<SheetGenerationRule>` — encapsule les champs
  nom de feuille/`PivotSource` + les deux sous-listes (colonnes, colonnes de point).
- **`ColumnDefinitionForm.razor`** et **`PointColumnDefinitionForm.razor`** (nouveaux, même
  forme que `BlockFieldForm.razor`) : chaque colonne/colonne de point déjà ajoutée dans un
  `SheetGenerationRuleForm` gagne ses propres boutons icône Modifier/Supprimer
  (`.block-field-icon-btn`, mêmes SVG inline que côté import — pas de nouvelle iconographie à
  inventer).
- `ExportProfileEditor.razor` gagne `_editingIndex`/`_pendingDeleteIndex` (identiques dans leur
  rôle à `ImportProfileEditor.razor`) pour permettre Modifier/Supprimer une `SheetGenerationRule`
  déjà ajoutée à la racine.
- **Point d'attention Domain, à vérifier avant de coder** : `SheetGenerationRule`/
  `ColumnDefinition`/`PointColumnDefinition` sont des `sealed record` (contrairement à
  `SheetExtractionRule`, une classe simple). Un `record` expose une syntaxe `with { ... }` qui
  **ne repasse pas** par le constructeur validant (elle copie les champs directement) —
  reconstruire un élément modifié doit donc toujours passer par le vrai constructeur (comme le
  fait déjà `ImportProfileEditor.HandleSaveSheetRule`, qui remplace l'entrée de liste plutôt que
  de muter en place), jamais via `with`, pour ne pas contourner la validation Domain.
- Les boutons Modifier/Supprimer/Enregistrer/Annuler introduits ici naissent directement dans un
  conteneur `.right-aligned-actions` (convention `convention-ui-blazor-alignement-boutons.md`),
  pas de passage par un état "non aligné" intermédiaire.

### Tests (bUnit)
- Modifier une règle déjà ajoutée : le formulaire se pré-remplit avec les valeurs existantes
  (nom, `PivotSource`, colonnes, colonnes de point), Enregistrer remplace l'entrée en place
  sans dupliquer, Annuler restaure l'affichage résumé sans perte.
- Supprimer une règle déjà ajoutée : la règle disparaît de `_sheetRules`.
- Idem pour une colonne / colonne de point à l'intérieur d'un `SheetGenerationRuleForm` (add ou
  edit-mode), y compris le cas imbriqué (modifier une colonne à l'intérieur d'une règle
  elle-même en cours d'édition), à l'image du test de non-régression équivalent côté import.

---

## Q5. Confirmation avant suppression d'une règle de feuille

Dépend de Q4 (il n'y a rien à confirmer tant qu'il n'y a pas de suppression). Parité avec le
follow-up « confirmation de suppression » du 22/07 côté import :

- Même bascule à deux temps (`_pendingDeleteIndex`), pas de nouvelle dépendance JS (le projet
  évite volontairement l'interop JS pour ce genre de confirmation, cf. notes Lot K4/suivi
  import) — réutilisation de l'idiome existant, pas d'un nouveau pattern.
- Nouvelles clés resx **propres à cette page** (`ExportProfileEditor_ConfirmDeleteSheetRuleMessage`,
  `ExportProfileEditor_ConfirmDeleteButton`, `ExportProfileEditor_CancelButton` — ou réutilisation de
  `ExportProfileEditor_CancelButton` s'il existe déjà un bouton Annuler générique une fois Q4 fait) :
  chaque page possède déjà ses propres clés resx même quand le texte anglais/français coïncide
  avec une autre page (`Users_*` vs `Logs_*` vs `ImportProfileEditor_*`, etc.) — ce n'est pas une
  duplication de style, seulement une convention de nommage par page déjà en place partout dans
  ce projet.

### Tests (bUnit)
- Premier clic sur Supprimer : affiche la confirmation sans retirer la règle.
- Confirmer : retire la règle. Annuler : restaure les boutons Modifier/Supprimer d'origine, règle
  toujours présente.

---

## Hors périmètre (explicitement exclu de ce ticket)

- **Parité visuelle** (labels, cartes, monospace, alignement des boutons pré-existants) —
  prérequis traité par `tickets-tdd-blazor-profil-export-parite-visuelle.md`, pas reproduit ici.
- `ExportProfileTest.razor` / `ImportProfileTest.razor` — non concernées.
- Toute nouvelle règle CSS au-delà de celles déjà globales dans `wwwroot/app.css`.

---

## Note d'efficacité d'implémentation

1. Confirmer que `tickets-tdd-blazor-profil-export-parite-visuelle.md` est bien validé côté
   client avant de commencer — les nouveaux composants de ce ticket doivent réutiliser les
   classes finales dès leur création.
2. Extraire `SheetGenerationRuleForm`/`ColumnDefinitionForm`/`PointColumnDefinitionForm` en
   suivant le code de `SheetRuleForm.razor`/`BlockFieldForm.razor` comme gabarit direct (mêmes
   noms de paramètres, même idiome `IdPrefix`), pas en réinventant une architecture différente.
3. Faire Q4 puis Q5 (Q5 dépend de Q4), pas en parallèle.
4. Vérifier la suite bUnit complète de `ExportProfileEditorTests.cs` après chaque sous-lot.
5. Vérification manuelle en dev server recommandée (captures d'écran) plutôt qu'obligatoire —
   cf. `feedback_browser_preview_caution` : si le Browser pane est instable, se rabattre sur la
   suite bUnit, ne pas cliquer en direct sur un profil seedé partagé (utiliser un profil
   jetable créé via `/export-profiles/new`, jamais enregistré).
