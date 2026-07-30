# Convention UI — usage des icônes dans les boutons (BlazorAdmin)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Complète
`convention-ui-blazor-alignement-boutons.md` : l'un régit la position des boutons, l'autre régit
la présence et l'intégration d'une icône à l'intérieur. Référencer ce document plutôt que
réexpliquer la règle dans chaque futur ticket UI.

Bibliothèque d'icônes du projet : les formes **Bootstrap Icons**, mais **jamais** via la police
`bi bi-*` — aucune police d'icônes externe n'est chargée dans ce projet (voir
`NavMenu.razor.css`, dont les icônes de navigation sont déjà du SVG inline encodé en data-URI, pas
des classes de police). Pour les boutons d'action des pages d'administration, le mécanisme réel
est le **SVG inline**, exposé via le dictionnaire de constantes statiques
`ExcelETL.BlazorAdmin.Shared.AdminIconMarkup` (`AdminIconMarkup.Pencil`, `.Trash`, `.Check`, etc.)
— un seul point de vérité par forme d'icône, réutilisé tel quel partout où cette action apparaît,
au lieu de dupliquer le SVG dans chaque composant (cf. `ImportProfiles.razor`/`ExportProfiles.razor`,
Lot 035.5). Rationale : pas de dépendance à une police externe (pas de flash de contenu non
stylé), contrôle exact de la couleur via `currentColor`/`fill` pour s'adapter aux thèmes
clair/sombre. **Ce mécanisme est déjà appliqué à 100 % dans le code réel — aucune migration vers
`bi bi-*` n'est prévue ni souhaitable** (décision actée, `tickets-tdd-lot-041-convention-icones-coherence.md`).

Exemple réel, tiré de `ImportProfiles.razor` (bouton `create-profile-button`, action CRUD
« Créer ») :

```razor
<button id="create-profile-button" class="btn btn-primary flex-fill" @onclick="CreateProfile">
    @((MarkupString)AdminIconMarkup.Plus) @Loc["ImportProfiles_Create"]
</button>
```

Et pour un bouton icône seule (ligne de grille, ex. `ImportProfiles.razor`'s
`edit-profile-button-@profile.Id`) :

```razor
<button id="edit-profile-button-@profile.Id" type="button"
        class="btn btn-outline-secondary btn-sm block-field-icon-btn"
        aria-label="@Loc["ImportProfiles_Edit"]" title="@Loc["ImportProfiles_Edit"]"
        @onclick="() => EditProfile(profile)">
    @((MarkupString)AdminIconMarkup.Pencil)
</button>
```

Ce document étend l'usage de ces formes aux boutons d'action des pages d'administration, sans en
introduire de nouvelles bibliothèques.*

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

## Icône + libellé : gabarit unique (Lot 058)

**Origine** : deux formes de markup coexistaient pour un bouton icône + libellé — un espace blanc
littéral dans le source Razor entre l'icône et `@Loc[...]` (rendu correctement), ou l'icône dans un
bloc `@if`/`else` suivi du libellé sur la ligne suivante (aucun espace rendu, icône collée au
texte). La correction ne porte pas fichier par fichier : elle rend l'espacement **indépendant du
blanc de la source**.

- **Chaîne d'utilitaires exacte** à ajouter à la classe du bouton, dans cet ordre, en plus des
  classes déjà en place (couleur, largeur, marges — jamais remplacées) :
  `d-flex align-items-center justify-content-center gap-1`.
- **Interdit** : faire reposer l'espacement icône/libellé sur un blanc de source Razor, ou sur une
  marge posée au cas par cas (`ms-1` sur le libellé, `me-1` dans la constante d'icône). L'espacement
  vient uniquement du `gap-1` du conteneur flex.
- `d-flex` (et non `d-inline-flex`) fonctionne aussi bien avec `w-100` qu'avec une largeur naturelle
  grâce à `justify-content-center` : une seule combinaison à retenir pour tous les cas, y compris un
  bouton de largeur naturelle (`justify-content-center` y est alors simplement sans effet visible).
- Effet de bord bénéfique : l'icône se retrouve centrée verticalement par rapport au texte, ce qui
  n'était pas garanti non plus avec les deux formes précédentes.
- **L'icône reste décorative** (`aria-hidden="true"`, déjà porté par chaque constante
  `AdminIconMarkup`) tant qu'un libellé texte visible subsiste — ce gabarit ne change rien à la
  règle d'accessibilité déjà énoncée ci-dessous.
- Exemple réel, après application (`create-profile-button`) :

```razor
<button id="create-profile-button" class="btn btn-primary flex-fill d-flex align-items-center justify-content-center gap-1" @onclick="CreateProfile">
    @((MarkupString)AdminIconMarkup.Plus) @Loc["ImportProfiles_Create"]
</button>
```

## Accessibilité (A11Y)

- **Bouton icône + texte** : l'icône est décorative, elle doit être masquée aux lecteurs d'écran —
  chaque constante `AdminIconMarkup` porte déjà `aria-hidden="true"` sur son `<svg>` racine, donc
  utiliser la constante telle quelle (`@((MarkupString)AdminIconMarkup.Plus)`) suffit, sans rien
  ajouter.
- **Bouton icône seule** (ex. ligne de grille sans libellé visible) : le bouton n'ayant pas de
  texte, il **doit** porter un `aria-label` explicite décrivant l'action, et un tooltip visuel
  (`title` ou équivalent, contenu identique) pour les utilisateurs à la souris :
  `<button aria-label="Supprimer l'élément" title="Supprimer l'élément">@((MarkupString)AdminIconMarkup.Trash)</button>`
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
