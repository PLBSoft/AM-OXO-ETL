# Lot 076 — Alignement de l'éditeur de profil d'export sur les outils génériques de brouillon

**Prérequis** : lot 074 (pilote P3 sur l'export) et lot 075 (migration de l'import, création de
`ListItemEditState<T>`/`DraftListActions`) livrés. Proposé par la section « Suite proposée » du ticket 075.

## Contexte

Le pilote P3 (lot 074) est antérieur aux outils génériques du lot 075. L'éditeur d'export gère donc
encore ses listes imbriquées à la main, et n'efface jamais les erreurs d'un arbre de brouillons avant de
le reconvertir. Refactor interne : aucun changement d'interface, mêmes identifiants, gestes et messages.
**Pas de nouvelle discussion d'architecture : P3 est acquis.**

## Constats (relus dans le code au commit `5efe8b6`, non supposés)

1. `SheetGenerationRuleForm.razor` (552 lignes) : 3 paires index/copie écrites à la main
   (`_editingColumnIndex`/`_columnEditSnapshot`, idem point et application), 3 `OpenXxxEdit`,
   3 `CancelXxxEdit`, 3 `HandleXxxSubmit` quasi identiques, 3 `DeleteXxx`, plus une boucle
   `ApplyErrors` recopiée dans `Submit` et une méthode vide `OnCancelledNestedEdit`.
2. `ExportProfileEditor.razor` (517 lignes) : sa propre méthode `ApplyErrors` (identique à
   `DraftListActions.ApplyErrors`) et deux branches recopiées dans `TryValidateAndCloseCurrentForm`.
3. **Défaut réel** : aucun `ClearErrors` avant `ConvertSheetRule` (bouton de la règle, bascule de
   formulaire du lot 057) ni avant `ToDomain` (sauvegarde). Une erreur corrigée depuis reste affichée à
   côté de la nouvelle. Reproduit par 3 tests rouges (voir 076.1).
4. Écarts de gestes avec l'import, non couverts par un test :
   - ouvrir un 2e élément imbriqué alors qu'un 1er est ouvert gardait la saisie du 1er, sans validation
     ni retour possible (la copie du 1er était écrasée) ;
   - supprimer un élément placé avant celui qui est ouvert ne décalait pas l'index : l'édition passait
     sur l'élément suivant.

## Décisions

- Les 3 listes imbriquées passent sur `ListItemEditState<T>` + `DraftListActions.Submit`/`Delete`.
- `ClearErrors` avant chacune des 3 reconversions d'arbre, `DraftListActions.ApplyErrors` à la place
  des boucles locales.
- La règle de feuille ouverte garde sa copie écrite à la main dans `ExportProfileEditor`, comme
  `ImportProfileEditor` : son état est partagé avec le formulaire d'ajout (lot 057), ce que
  `ListItemEditState` ne modélise pas.
- Les 2 écarts du constat 4 sont alignés sur l'import (comportement des outils génériques), assumés et
  documentés ci-dessous, sans test existant touché.

## Étapes TDD

### 076.1 — Rouge

`ExportProfileEditorStaleErrorTests.cs`, 3 cas, un par reconversion d'arbre :
- bouton « Ajouter la règle » : erreur « nom de feuille vide », puis nom saisi et colonne en attente
  invalide → l'erreur de nom ne doit plus s'afficher ;
- bascule vers le formulaire d'ajout (lot 057) : colonne vidée en édition, puis corrigée et nom de
  feuille vidé → l'erreur de colonne ne doit plus s'afficher ;
- sauvegarde du profil : même scénario → plus aucune erreur dans la colonne en édition.

Vérifié rouge pour la bonne raison : les 3 échouent sur la présence de l'ancien message
(ex. `{"Sheet name must not be empty.", "Header must not be empty."}`), pas sur un sélecteur.

### 076.2 — Vert

Migration des 2 composants (constats 1 à 3). Commit unique avec le test rouge, pour garder `main` vert.

### 076.3 — Non-régression et résultat

Portée filtrée (Editing, ExportProfileEditor*, ProfileEditorParity, FormFloatingStructureAudit,
IconLabelButtonGabarit), puis `ExcelETL.BlazorAdmin.Tests` complet.

## Hors périmètre

- L'éditeur d'import, le domaine, la persistance, toute évolution d'interface.
- Les 3 composants feuilles (`ColumnDefinitionForm`, `PointColumnDefinitionForm`,
  `ApplicationColumnDefinitionForm`), déjà sans état depuis le lot 074.
- Les SVG crayon/corbeille recopiés en ligne dans `SheetGenerationRuleForm` (dette connue, audit design §2.4).

## Résultat

### Lignes de code (`git diff --numstat 5efe8b6 c36050a`)

| Fichier | Avant | Après | Delta |
| :--- | ---: | ---: | ---: |
| `ExportProfileEditor.razor` | 517 | 490 | −27 (+25/−52) |
| `SheetGenerationRuleForm.razor` | 552 | 388 | −164 (+49/−213) |
| **Total** | **1 069** | **878** | **−191** |

### Champs d'état

| Composant | Avant | Après |
| :--- | ---: | ---: |
| `SheetGenerationRuleForm` | 6 (3 index + 3 copies) | 3 (`ListItemEditState<T>`) |
| `ExportProfileEditor` | 11 | 11 (inchangé, cf. décisions) |

Méthodes de `SheetGenerationRuleForm` : 19 → 9 (`MarkDirty`, `OnPivotSourceChangedAndMarkDirty`,
`Localize`, `Delete<T>`, 3 `SubmitXxx`, `Submit`, `Cancel`).
`ExportProfileEditor` perd `ApplyErrors` et `DeleteSheetRule` (fusionnée dans `ConfirmDeleteSheetRule`,
seul appelant).

### Tests

- Assertions de tests existants modifiées : **0** (`git diff` ne touche aucun fichier de test existant).
- 3 tests ajoutés (`ExportProfileEditorStaleErrorTests.cs`).
- Portée filtrée : 373/373.
- `ExcelETL.BlazorAdmin.Tests` complet : 1 313/1 314 (seul échec :
  `ExportProfileTestTests.ClickingGenerate_WithoutExportProfileSelected_ShowsError_WithRoleAlert`,
  préexistant et sans rapport).

### Changements de comportement assumés (aucun test existant touché)

- Erreurs corrigées effacées à chaque nouvelle tentative (constat 3, objet du lot).
- Ouvrir un élément imbriqué annule l'édition d'un autre élément ouvert de la même liste (retour à sa
  valeur d'ouverture), comme à l'import et comme avant le lot 074.
- Supprimer un élément avant l'élément ouvert garde l'édition sur le même élément.
- Ouvrir un élément ou une règle ne remet plus explicitement son erreur à zéro : l'effacement se fait
  désormais avant chaque conversion, et une copie (`DraftJson.Clone`) ne porte jamais d'erreur.

### Conclusion

Les deux éditeurs de profil utilisent maintenant les mêmes outils génériques pour leurs listes et le
même enchaînement `ClearErrors` → conversion → `ApplyErrors`. Il ne reste aucune gestion de liste
écrite à la main côté export.
