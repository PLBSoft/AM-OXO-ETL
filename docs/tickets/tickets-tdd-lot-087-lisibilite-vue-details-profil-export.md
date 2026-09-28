# Tickets TDD — Lot 087 : lisibilité de la vue « Détails » du profil d'export

*Document vivant (voir `convention-nommage-documents.md`). Ouvert et réalisé le 28/09 à partir d'une revue
de la page `/export-profiles/{id}/details` en production (capture de Simon). Suite du lot 079
(`tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md`), dont il change la présentation de deux
familles de phrases. **Décisions validées par Simon le 28/09.***

---

## 0. Constat

- Le groupe de colonnes de points (« Colonnes O à AL : … ») était une seule phrase de 24 noms en gras,
  chacun suivi de sa lettre entre parenthèses : la partie la plus utile de la page était la plus dure à lire.
- Les colonnes « toujours vide » avaient le même poids visuel que les colonnes qui ont un contenu.

## 1. Décisions

- **D1** : un groupe de points garde sa phrase d'introduction (terminée par « : »), puis liste ses colonnes
  en sous-liste, une par ligne, lettre en tête comme les autres colonnes : `O « VISITE PRÉALABLE CHANTIER »`.
- **D2** : une colonne toujours vide (`ColumnDefinition` sans source) est grisée (`text-muted`), sans être
  regroupée — l'ordre des lettres reste intact.
- Pas de tableau (Colonne | Titre | Contenu) : gain faible une fois D1 fait, refonte du builder et de la page.

## 2. Réalisation

- `ProfileDescriptionSentence` : `SubItems` (`IReadOnlyList<ProfileDescriptionText>`, vide par défaut) et
  `IsMuted` (init). Le profil d'import n'utilise ni l'un ni l'autre.
- `ExportProfileDescriptionBuilder` : `DescribePointGroup` remplit `SubItems` ; la colonne vide porte
  `IsMuted = true`. Clés modifiées (mêmes valeurs FR dans les deux `.resx`, décision D7 du lot 079) :
  `ExportProfileDetails_PointGroupEquipement`/`Isolement` (« … sinon vide : », sans `{2}`),
  `ExportProfileDetails_PointGroupItem` (`{1} {0}`, lettre puis titre).
- `ProfileDescriptionView.razor` : `ul.profile-details-subitems` imbriquée sous la phrase ; `li.text-muted`
  pour une phrase grisée.
- Tests : `DescriptionTestSupport.Texts()`/`Lines()` aplatissent une phrase et ses sous-éléments (préfixe
  « - ») ; catalogues figés (`ExportProfileDescriptionBuilderSeededProfileTests`, lot 079 §5) et
  `ExportProfileDescriptionBuilderPointTests` mis à jour ; ajout de
  `AlwaysEmptyColumn_IsMuted_OtherColumnsAreNot`, `PointGroup_ListsItsColumnsAsNestedItems_LetterInCode`,
  `AlwaysEmptyColumn_IsGreyedOut`. Périmètre filtré 154/154.
- Non vérifié dans un navigateur réel.
