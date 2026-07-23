# Convention UI — usage des icônes dans les boutons (BlazorAdmin)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Complète
`convention-ui-blazor-alignement-boutons.md` : l'un régit la position des boutons, l'autre régit
la présence et l'intégration d'une icône à l'intérieur. Référencer ce document plutôt que
réexpliquer la règle dans chaque futur ticket UI.

Bibliothèque d'icônes du projet : **Bootstrap Icons** (classes `bi bi-*`), déjà en place dans
`NavMenu.razor` / `NavMenu.razor.css` (ex. `bi-person-badge-nav-menu`,
`bi-file-earmark-spreadsheet-nav-menu`, `bi-download-nav-menu`). Ce document étend l'usage de
cette même bibliothèque aux boutons d'action des pages d'administration, sans en introduire une
nouvelle.*

## Règle générale

L'icône dans un bouton n'est **pas systématique** : elle sert un but fonctionnel précis
(renforcer une action principale, accélérer le repérage d'une action CRUD standard, gagner de
la place dans une grille de données). Un écran où *tous* les boutons portent une icône perd cet
effet de repère et devient visuellement chargé. Absence d'icône = décision par défaut ; présence
d'icône = décision justifiée par la matrice ci-dessous.

## Matrice de décision

| Type d'action | Icône ? | Exemples |
| :--- | :--- | :--- |
| Action principale (CTA) | **Oui** | `+ Créer un profil`, `Publier` |
| Action CRUD standard | **Oui** | `Modifier`, `Supprimer`, `Enregistrer` |
| Action secondaire | **Non** | `Annuler`, `Fermer`, `Retour` |
| Ligne de grille/tableau | **Oui** (icône seule ou icône + libellé court) | bouton `Voir` d'une ligne de résultats |

En cas de doute sur une action non listée : se demander si l'icône aide réellement à
reconnaître l'action *avant* la lecture du texte. Si non, ne pas en ajouter.

## Règles d'intégration (Blazor / CSS)

- **Placement** : icône toujours **à gauche** du texte (sens de lecture naturel gauche → droite).
- **Espacement** : espacement constant entre icône et texte, via les utilitaires déjà en usage
  dans le projet (ex. `gap-2` sur le conteneur flex, ou une marge dédiée sur l'icône — suivre le
  pattern CSS déjà présent dans `NavMenu.razor.css` plutôt qu'en introduire un nouveau).
- **Choix de l'icône** : icônes Bootstrap Icons universelles et non ambiguës uniquement. Ne pas
  ajouter d'icône seulement pour « faire joli » sur une action non standard — cf. règle générale.

## Accessibilité (A11Y)

- **Bouton icône + texte** : l'icône est décorative, elle doit être masquée aux lecteurs d'écran :
  `<span class="bi bi-xxx" aria-hidden="true"></span>` (pattern déjà utilisé dans
  `NavMenu.razor`, à reprendre tel quel).
- **Bouton icône seule** (ex. ligne de grille sans libellé visible) : le bouton n'ayant pas de
  texte, il **doit** porter un `aria-label` explicite décrivant l'action, et un tooltip visuel
  (`title` ou équivalent) pour les utilisateurs à la souris :
  `<button aria-label="Supprimer l'élément" title="Supprimer l'élément"><span class="bi bi-trash" aria-hidden="true"></span></button>`
- Cette exigence s'ajoute à la convention `id` stable déjà en vigueur sur tout élément interactif
  (voir conventions de tests bUnit) — l'`aria-label` ne remplace pas l'`id`, il s'y ajoute.

## Portée

Toutes les pages d'administration `ExcelETL.BlazorAdmin`, présentes et futures. S'applique en
complément de `convention-ui-blazor-alignement-boutons.md` (alignement à droite) : une icône ne
change pas la règle d'alignement, elle s'intègre à l'intérieur du bouton déjà positionné selon
cette dernière.

Toute nouvelle page ou tout nouveau composant Blazor Admin doit respecter cette convention dès sa
conception, sans qu'il soit nécessaire de le repréciser dans chaque nouveau ticket — une simple
référence à ce document suffit.
