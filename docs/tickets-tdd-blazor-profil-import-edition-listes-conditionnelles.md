# Tickets TDD — Lot W : édition et suppression des colonnes inconditionnelles et des règles de point conditionnelles (`ImportProfileEditor`)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`).*

**Contexte** : `ImportProfileEditor.razor` (Lot F, `tickets-tdd-blazor-profil-import.md`) permet
aujourd'hui d'**ajouter** un par un des éléments aux deux sous-listes à taille variable d'une
`SheetExtractionRule` :
- `UnconditionalColonneNames` (liste libre de noms — un `Colonne.Nom` créé systématiquement pour
  tout Isolement extrait de la feuille, sans condition) ;
- `ConditionalPointRule` (`ColonneName`, `SourceFieldName`, `Operator`, `ComparisonValue`).

Le ticket `docs/ticket-profil-import-edition.md` (2026-07-21) a déjà livré l'édition/suppression
inline pour les `SheetExtractionRule` (règles de feuille entières) et les `BlockFieldDefinition`
(champs de bloc) — voir aussi Lot O1 (`convention-ui-blazor-alignement-boutons.md`, boutons icône
par champ de bloc). **Ce lot ne couvre pas** `UnconditionalColonneNames`/`ConditionalPointRule` :
ces deux sous-listes restent, à ce jour, **ajout uniquement** — un élément une fois ajouté ne peut
ni être modifié ni être supprimé sans recréer tout le profil. C'est l'écart que ce lot comble.

Le ticket R3 (`ticket-r3-accordeon-sous-listes.md`) a par ailleurs livré le repli/dépliage
(accordéon) de ces deux sous-listes — **non réouvert ici**, ce lot ajoute des actions à
l'intérieur de la sous-liste déjà dépliable, sans toucher au mécanisme d'accordéon lui-même.

**Décision actée** : "autoriser la modification" couvre **à la fois l'édition en place et la
suppression** d'un élément existant de ces deux sous-listes — confirmé explicitement, cohérent
avec le précédent `ticket-profil-import-edition.md` qui bundlait déjà les deux. Ne pas rouvrir
cette question pendant l'implémentation.

**Conventions déjà en place à respecter** : `convention-ui-blazor-alignement-boutons.md` (actions
alignées à droite du conteneur), `convention-ui-blazor-icones-boutons.md` (icônes Bootstrap Icons
`bi-pencil`/`bi-trash`/`bi-check`/`bi-x` selon la matrice de décision, `aria-label`/`title` pour
tout bouton icône seule) ; IDs HTML stables, jamais de sélection par texte/position en bUnit ;
xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; construction directe de l'objet Domain réel
dans un `try/catch`, erreurs localisées via `BusinessExceptionLocalizer`, même pattern que F1/F3
et `ticket-profil-import-edition.md`.

---

## Étape 0 — Investigation préalable (obligatoire avant tout code)

- [x] Confirmer dans `ImportProfileEditor.razor` que `UnconditionalColonneNames` et
  `ConditionalPointRule` sont bien rendus en lecture seule une fois ajoutés (aucun bouton
  `Modifier`/`Supprimer` déjà présent sur ces deux sous-listes) — ne pas dupliquer une
  fonctionnalité déjà livrée silencieusement par `ticket-profil-import-edition.md`.
- [x] Relire le mécanisme d'édition inline déjà en place pour `BlockFieldDefinition` (Lot O1/
  `ticket-profil-import-edition.md`) : bascule affichage/édition par élément (état C# indexé, pas
  une variable globale), boutons `Enregistrer`/`Annuler` en mode édition — réutiliser exactement
  ce pattern plutôt qu'en inventer un nouveau, pour la cohérence visuelle et de test avec le reste
  de l'éditeur.
- [x] Relire le mécanisme d'accordéon (R3) pour confirmer que les nouveaux boutons d'action
  s'intègrent dans le contenu déjà déplié de la sous-liste, sans casser le repli par défaut ni le
  calcul du résumé (`"{N} colonnes inconditionnelles, {M} règles conditionnelles"`).
- [x] Confirmer les IDs HTML existants sur les éléments actuels de ces deux sous-listes (ligne
  affichée par élément) pour choisir des IDs de boutons cohérents et ne rien renommer par
  erreur.

**Conclusions de l'investigation (2026-07-24)** : `SheetRuleForm.razor` (pas `ImportProfileEditor.razor`
lui-même — les deux sous-listes n'existent que dans le formulaire éditable, jamais dans le résumé
en lecture seule de la carte, qui reste couvert par R3 sans y toucher) rendait bien
`UnconditionalColonneNames` en un seul `<p>@string.Join(...)</p>` et `PointRules` en `<ul><li>` texte
brut, sans aucun bouton. Le pattern `BlockFieldDefinition` (état `_editingFieldIndex` indexé,
boutons icône `Modifier`/`Supprimer` sur la ligne affichée, sous-formulaire `Enregistrer`/`Annuler`
en mode édition, réutilisant les classes globales `.block-field-list`/`.block-field-item`/
`.block-field-actions`/`.block-field-icon-btn` de `app.css`) a été repris tel quel : deux nouveaux
jeux d'état indexés (`_editingUnconditionalColonneIndex`, `_editingPointRuleIndex`), inline dans
`SheetRuleForm.razor` (pas de nouveau sous-composant séparé, les champs édités sont trop simples
pour le justifier). Les nouveaux boutons vivent à l'intérieur du contenu déjà déplié par R3 (la
sous-liste elle-même, pas le `<details>`) — le mécanisme d'accordéon n'a pas été touché.

---

## W1. Édition en place d'une colonne inconditionnelle existante (`UnconditionalColonneNames`)

**Comportement attendu** :
- Chaque entrée affichée de `UnconditionalColonneNames` porte un bouton `#edit-unconditional-colonne-button-{sheetRuleIndex}-{itemIndex}`
  (icône `bi-pencil`, voir convention icônes) qui bascule la ligne en mode édition : le nom devient
  un champ texte (`#unconditional-colonne-edit-input-{sheetRuleIndex}-{itemIndex}`) pré-rempli
  avec la valeur actuelle.
- En mode édition, deux boutons : `#save-unconditional-colonne-button-{sheetRuleIndex}-{itemIndex}`
  (icône `bi-check`) et `#cancel-unconditional-colonne-edit-button-{sheetRuleIndex}-{itemIndex}`
  (icône `bi-x`).
- `Enregistrer` reconstruit la liste `UnconditionalColonneNames` de la `SheetExtractionRule`
  concernée avec la valeur modifiée à la position éditée — même mécanisme de reconstruction
  immuable que celui déjà utilisé pour les autres listes de l'éditeur (pas de mutation en place
  d'une collection, cohérent avec le style `IReadOnlyList` du Domain).
- Validation : un nom vide après édition est rejeté (même règle qu'à l'ajout), message localisé,
  pas de fermeture du mode édition tant que l'erreur n'est pas corrigée.
- `Annuler` restaure l'affichage précédent sans modifier la liste.

**Tests** (bUnit) :
- Clic sur `#edit-unconditional-colonne-button-{i}-{j}` fait apparaître le champ de saisie
  pré-rempli avec la valeur actuelle, le texte statique disparaît du DOM (assertion `FindAll`).
- Modification de la valeur puis clic sur `#save-unconditional-colonne-button-{i}-{j}` : la
  nouvelle valeur est affichée à la même position dans la liste, l'ancienne valeur n'apparaît plus.
- Édition avec valeur vide → message localisé affiché, liste non modifiée, mode édition toujours
  actif.
- Clic sur `#cancel-unconditional-colonne-edit-button-{i}-{j}` après une modification non
  enregistrée : la valeur d'origine reste affichée, aucune sauvegarde déclenchée.
- Édition sur une carte de règle de feuille n'affecte pas l'état d'édition des autres cartes ni des
  autres éléments de la même liste (état indexé, pas une variable globale partagée).
- Non-régression : l'ajout d'une nouvelle colonne inconditionnelle (comportement F1.2 existant)
  continue de fonctionner sans modification de son test.

---

## W2. Suppression d'une colonne inconditionnelle existante (`UnconditionalColonneNames`)

**Comportement attendu** :
- Bouton `#delete-unconditional-colonne-button-{sheetRuleIndex}-{itemIndex}` (icône `bi-trash`,
  `aria-label`/`title` explicites) à côté du bouton d'édition (W1), aligné à droite de la ligne.
- Clic retire immédiatement l'élément de la liste affichée (pas de confirmation modale — cohérent
  avec le comportement déjà en place pour la suppression des règles de feuille et des champs de
  bloc, voir incident documenté dans `CLAUDE.md` : suppression directe et immédiate est le pattern
  déjà accepté sur ce projet, pas une régression à corriger ici).
- La sauvegarde du profil (`#save-profile-button`) persiste bien la liste sans l'élément supprimé.

**Tests** (bUnit) :
- Clic sur `#delete-unconditional-colonne-button-{i}-{j}` retire l'élément du DOM, les autres
  éléments de la liste restent affichés à l'identique (pas de décalage d'index qui casserait un
  autre élément).
- Suppression du dernier élément restant → liste vide gérée sans erreur (cohérent avec l'état vide
  déjà couvert par R3).
- Sauvegarde après suppression → `SaveAsync` appelé avec une `SheetExtractionRule` dont
  `UnconditionalColonneNames` ne contient plus l'élément supprimé.
- Suppression sur une carte n'affecte pas les autres cartes de règle de feuille.

---

## W3. Édition en place d'une règle de point conditionnelle existante (`ConditionalPointRule`)

**Comportement attendu**, symétrique de W1 mais sur les 4 champs de `ConditionalPointRule`
(`ColonneName`, `SourceFieldName`, `Operator`, `ComparisonValue`) :
- Bouton `#edit-conditional-point-rule-button-{sheetRuleIndex}-{itemIndex}` (icône `bi-pencil`)
  bascule la ligne en mode édition : 4 champs pré-remplis (`#conditional-point-rule-edit-colonne-name-input-{i}-{j}`,
  `#conditional-point-rule-edit-source-field-input-{i}-{j}`,
  `#conditional-point-rule-edit-operator-select-{i}-{j}` — select `Equals`/`NotEquals`, même
  option que le formulaire d'ajout — `#conditional-point-rule-edit-comparison-value-input-{i}-{j}`).
- `#save-conditional-point-rule-button-{i}-{j}` (icône `bi-check`) reconstruit l'objet
  `ConditionalPointRule` (record immuable) avec les 4 valeurs éditées, à la même position dans la
  liste ; `#cancel-conditional-point-rule-edit-button-{i}-{j}` (icône `bi-x`) restaure l'affichage
  précédent.
- Validation : mêmes invariants qu'à l'ajout (aucun des 4 champs vide) — message localisé si
  violé, mode édition non fermé tant que non corrigé.

**Tests** (bUnit) :
- Clic sur `#edit-conditional-point-rule-button-{i}-{j}` affiche les 4 champs pré-remplis avec les
  valeurs actuelles de la règle, y compris le select `Operator` positionné sur la valeur actuelle
  (`Equals` ou `NotEquals`).
- Modification d'un seul champ (ex. `ComparisonValue`) puis sauvegarde → la règle affichée reflète
  la nouvelle valeur, les 3 autres champs restent inchangés.
- Un des 4 champs laissé vide → message localisé, sauvegarde non effectuée, mode édition toujours
  actif.
- Annulation après modification non enregistrée → valeurs d'origine réaffichées.
- Non-régression : l'ajout d'une nouvelle `ConditionalPointRule` (comportement F1.2 existant)
  continue de fonctionner sans modification de son test.

---

## W4. Suppression d'une règle de point conditionnelle existante (`ConditionalPointRule`)

**Comportement attendu et tests** : symétrique exact de W2, appliqué à `ConditionalPointRule` —
bouton `#delete-conditional-point-rule-button-{sheetRuleIndex}-{itemIndex}` (icône `bi-trash`,
`aria-label`/`title`), suppression immédiate sans confirmation modale, persistance vérifiée par
sauvegarde, indépendance entre cartes de règle de feuille, gestion de la liste vide cohérente avec
R3.

---

## Hors périmètre explicite de ce lot

- Le mécanisme d'accordéon (repli/dépliage) lui-même — traité par R3, non réouvert. Ce lot ajoute
  des boutons d'action à l'intérieur du contenu déjà dépliable, sans changer son déclenchement.
- Toute confirmation modale avant suppression — cohérent avec l'absence de confirmation déjà en
  place pour la suppression des règles de feuille/champs de bloc ailleurs dans l'éditeur ; à ne
  pas introduire ici seul sans décision explicite applicable à tout l'éditeur.
- `ExportProfileEditor.razor` — ce lot est strictement `ImportProfileEditor.razor`
  (`UnconditionalColonneNames`/`ConditionalPointRule` n'ont pas d'équivalent côté export). Aucune
  parité Q à maintenir ici.
- Tout changement du modèle Domain (`ConditionalPointRule`, `SheetExtractionRule`) — ce lot est
  strictement `BlazorAdmin`, les objets Domain existants sont reconstruits tels quels via leurs
  constructeurs/records actuels, sans modification de leur forme.

---

## Note d'efficacité d'implémentation (Claude Code)

- Traiter **W1 et W2 en un seul passage** (même sous-liste, boutons d'édition et de suppression
  ajoutés ensemble à la même ligne) plutôt qu'en deux cycles red/green séparés qui rééditeraient
  deux fois le même bloc de markup.
- Traiter **W3 et W4 immédiatement après**, en réutilisant le pattern d'état (dictionnaire/tableau
  d'index en édition) déjà écrit pour W1/W2 — la structure est identique, seul le nombre de champs
  édités par élément change (1 pour W1/W2, 4 pour W3/W4).
- Réutiliser explicitement le pattern d'état d'édition déjà en place pour `BlockFieldDefinition`
  (`ticket-profil-import-edition.md`) comme modèle de départ, plutôt que d'en concevoir un nouveau
  — l'Étape 0 doit se conclure par une confirmation de ce pattern avant d'écrire le premier test
  rouge.
- Un seul passage de lecture de `ImportProfileEditor.razor` (+ `.razor.cs` si le code-behind est
  séparé) suffit avant de commencer l'Étape 0 — pas besoin de relire entre W1/W2/W3/W4, les quatre
  se font dans la continuité d'un même bloc d'édition du fichier.

## Ordre recommandé

1. **Étape 0** (investigation, confirmation du pattern d'édition existant à réutiliser)
2. **W1 + W2** (`UnconditionalColonneNames`, édition + suppression)
3. **W3 + W4** (`ConditionalPointRule`, édition + suppression)
