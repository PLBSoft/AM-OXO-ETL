# Ticket TDD — Lot 077 : regroupement visuel des paramètres généraux de `SheetRuleForm`

Document vivant (voir `convention-nommage-documents.md`). Numéro 077 vérifié libre le 17/09 (aucun document ni mention de « Lot 077 » dans `docs/`/`CLAUDE.md`, `origin/main` à jour).

**Classe identifiée (étape 0)** : `card bg-light mb-3` > `card-body` > `h3.h5` — classes Bootstrap génériques, déjà réutilisées telles quelles par les 6 sous-sections de `SheetRuleForm.razor` (« Champs du bloc », colonnes inconditionnelles, règles conditionnelles, colonnes cochées par cellule, champs/composites d'en-tête) et par les cartes Tableaux/Applications d'`ImportProfileEditor.razor`. Aucune logique n'en dépend, aucun CSS dédié (`SheetRuleForm.razor.css` n'existe pas).

## Origine

Revue UI/UX de Simon (17/09) sur le panneau d'édition d'une règle de feuille : les 8 champs globaux (Nom de la feuille, Ligne de début du premier bloc, Pas, Nom du champ d'arrêt, Valeur « zéro énergie » attendue, Couleur d'étiquette par défaut, Cellule de la couleur d'étiquette, Couleurs d'étiquette autorisées) s'affichaient sans conteneur, contrairement à « Champs du bloc ».

**Décision actée avec Simon (17/09)** : envelopper ces champs dans le même conteneur que « Champs du bloc », en réutilisant la classe existante.

## 1. Regroupement des champs globaux — fait

- Les 8 blocs `div.mb-3 > div.form-floating` sont enfants directs d'un `card-body` d'une carte `card bg-light mb-3`, sous un en-tête `h3.h5` (nouvelle clé `ImportProfileEditor_SheetGeneralSettingsHeading` : EN « General sheet settings », FR « Paramètres généraux de la feuille »).
- Aucun champ ajouté, retiré, renommé ou réordonné ; aucun id modifié. Même rendu en mode ajout et en mode édition (`IdPrefix` `edit-{i}-`).
- Niveau de titre : `h3`, identique aux sections sœurs (pas de saut de niveau).

Tests (`ImportProfileEditorTests.cs`, rouges avant le changement) :
- `SheetRuleForm_AddMode_GeneralSettings_AreGroupedInABgLightCardWithHeading` (EN + FR) ;
- `SheetRuleForm_EditMode_GeneralSettings_AreGroupedInABgLightCardWithHeading`.

Non-régression : aucune assertion existante modifiée. `SheetRuleForm_Field_ContainerIsFullWidthFormFloating_WithNoColumnGridClass` (conteneur `mb-3`, sans `row`/`col-`) et `SheetRuleForm_FieldsPointRulesAndUnconditionalColonnesSubforms_AreEachWrappedInABgLightCard` restent verts inchangés. Périmètre filtré (éditeur d'import, parité, audits form-floating/titres) : 432/432.

## 2. Regroupement des champs racine du profil — fait (demande de Simon, 17/09, après validation du 1.)

- Les 3 champs du profil (Nom du profil, Préfixe de repère, Nom du type d'élément d'équipement) sont dans une carte `card bg-light mb-3` > `card-body`, précédée d'un titre `h2.h3` (clé `ImportProfileEditor_ProfileGeneralSettingsHeading` : EN « General profile settings », FR « Paramètres généraux du profil »).
- Titre **au-dessus** de la carte (et non dedans comme au 1.) : même forme que les sections Tableaux/Applications de ce niveau de page ; `h2` car sœur de ces sections sous le `h1`.
- Ids, ordre et grille deux colonnes du lot 53.2 inchangés. Import uniquement : l'éditeur d'export (un seul champ racine) n'est pas touché.

Test : `RootFields_AreGroupedInABgLightCard_UnderAGeneralSettingsHeading` (EN + FR), rouge avant le changement. Aucune assertion existante modifiée (`ShortRootFields_AreDirectChildrenOfTheSameRow`, parité du conteneur racine `mb-3` import/export, audit des titres restent verts). Périmètre filtré : 434/434.

## 3. Même regroupement côté profil d'export — fait (demande de Simon, 17/09)

- `ExportProfileEditor.razor` : le champ « Nom du profil » est dans la même carte `card bg-light mb-3` > `card-body`, précédée du même titre `h2.h3` (clé propre à la page, `ExportProfileEditor_ProfileGeneralSettingsHeading`, même texte EN/FR que l'import — convention des clés par page).
- Id inchangé ; conteneur `mb-3` du champ inchangé.

Tests : `RootField_IsGroupedInABgLightCard_UnderAGeneralSettingsHeading` (`ExportProfileEditorTests.cs`, EN + FR) et `RootFieldsCardAndHeading_AreIdenticalBetweenImportAndExportEditors` (`ProfileEditorParityTests.cs`, comparaison stricte classe de carte + HTML du titre), rouges avant le changement. Aucune assertion existante modifiée. Périmètre filtré (éditeurs import/export, parité, audits form-floating/titres) : 678/678.

## Hors périmètre explicite

- `SheetGenerationRuleForm.razor` (export) — reporté, ne pas anticiper.
- Grille 2 colonnes (`col-md-6`) sur ces champs — ticket séparé si souhaité.
- Nombre, ordre, libellé des champs ; modale/panneau latéral/accordéon (lot 057) ; modèle d'enregistrement, exclusion mutuelle, boutons d'ajout (lots 056-059).
