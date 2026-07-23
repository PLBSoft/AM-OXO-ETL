# Convention UI — lisibilité des tableaux à contenu dynamique (BlazorAdmin)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Décision actée
avec le client le 23/07, suite à un retour sur `ExportProfileTest.razor` (colonne "Action" du
tableau `generated-sheet-TM_PROC_MAD-table` illisible sur une seule ligne très large). Référencer
ce document plutôt que réexpliquer la règle dans chaque futur ticket UI.*

## Problème

Un tableau dont le **contenu et le nombre de colonnes dépendent du profil et du fichier importé**
(pas un nombre de colonnes fixe et connu à l'avance) ne peut pas être stylé avec des largeurs de
colonne codées en dur. Si le CSS force `white-space: nowrap` sur les cellules pour un rendu dense,
la moindre colonne à texte long (ex. "Action" d'un `TacheMultiplePivot`) force toute la colonne —
et potentiellement toute la page — à s'élargir bien au-delà de l'écran.

## Règle générale (contenu des cellules)

Pour tout tableau de ce type (`th`/`td`) :
- `white-space: normal` (jamais `nowrap`) — le texte revient à la ligne.
- `max-width` (un plafond raisonnable, ex. `320px`) — plafonne une colonne à texte long sans
  pénaliser les colonnes courtes (ex. "Ordre", "Date de validation"), qui restent compactes par le
  calcul naturel du `table-layout: auto` (par défaut, ne pas passer en `table-layout: fixed` — un
  `fixed` diviserait la largeur également entre toutes les colonnes, y compris les courtes, ce qui
  est une moins bonne lecture quand le nombre de colonnes varie par profil).
- `overflow-wrap: break-word` — filet de sécurité pour un mot isolé plus large que le plafond.
- `vertical-align: top` — les lignes multi-lignes restent alignées en haut, plus lisible que le
  centrage vertical par défaut.

## Règle générale (conteneur de page)

Le retour à la ligne ci-dessus ne suffit pas seul si la page elle-même peut s'élargir au-delà de
l'écran : `MainLayout.razor.css`, `main { flex: 1; }` est un item flex sans `min-width: 0`, et un
item flex ne se réduit jamais sous la largeur intrinsèque de son contenu par défaut. Un tableau
large forçait donc toute la page (`main`, `article`) à s'élargir, au lieu de laisser le conteneur
de défilement dédié (`overflow: auto` + `max-width: 100%`, ex. `.generated-sheet-scroll` /
`.test-table-scroll`) gérer son propre débordement horizontal. **Corrigé une fois pour toutes**
dans `MainLayout.razor.css` (`main { min-width: 0; }`) — ne pas dupliquer ce correctif par page,
il s'applique déjà à toute la mise en page.

## Portée actuelle

- **`ExportProfileTest.razor` / `.generated-sheet-table`** (`generated-sheet-Parents-table`,
  `generated-sheet-Enfants-table`, et tout `generated-sheet-{TypeTacheMultipleCode}-table`
  dynamique) — traité, une seule règle CSS partagée par tous les tableaux générés puisqu'ils
  utilisent tous la même classe et le même gabarit de rendu (`@foreach` sur
  `_generatedWorkbook.Sheets`).
- **`ImportProfileTest.razor` / `.test-table`** — n'a jamais eu besoin de ce traitement : ce CSS
  n'a jamais fixé `white-space: nowrap`, donc le comportement par défaut du navigateur
  (`white-space: normal`) s'applique déjà. Pas un oubli, juste un problème qui ne s'est jamais posé
  ici (colonnes de ces tableaux : `Repère`/`Designation`/`TypeElementNom`/etc., pas de colonne
  aussi structurellement longue qu'`Action`).

## Portée future

Tout futur tableau BlazorAdmin dont les colonnes dépendent d'un profil ou d'un fichier importé
(pas une liste de colonnes fixe et connue à la conception) doit appliquer la même règle dès sa
conception, sans qu'il soit nécessaire de le redécouvrir à chaque nouveau ticket — une simple
référence à ce document suffit. Si un futur écran réintroduit `white-space: nowrap` "pour faire
compact", vérifier explicitement que ses colonnes ne peuvent pas contenir de texte libre long
avant de le faire.
