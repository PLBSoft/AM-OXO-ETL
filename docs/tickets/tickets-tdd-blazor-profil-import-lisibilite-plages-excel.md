# Tickets TDD — Lot N : lisibilité des plages Excel (Profils d'import)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Complète le
Lot F (`tickets-tdd-blazor-profil-import.md`, `ImportProfileEditor.razor`), sans le rouvrir sur
ses autres aspects. Déclenché par un retour client sur capture d'écran réelle (`ISOLEMENT`,
20/07) : le format actuel des champs de bloc n'est pas lisible pour un utilisateur Excel qui
n'est pas familier de la logique `RowOffsetStart`/`RowOffsetEnd`.*

**Périmètre de ce lot : uniquement `ImportProfileEditor.razor` (Profils d'import).** Le même
défaut existe très probablement côté Profils d'export (`ExportProfileEditor.razor`, Lot J) mais
sa correction est **explicitement reportée à un lot ultérieur**, une fois ce lot validé côté
client sur les profils d'import qui serviront de modèle.

---

## N0. Constat et décision actée

**Problème** : la liste des champs d'une règle de feuille affiche aujourd'hui `TypeElement (B:E,
3-4)` — une plage de colonnes lisible, mais des offsets de ligne (`RowOffsetStart`-`RowOffsetEnd`,
relatifs à `FirstBlockStartRow` de la règle) qui n'ont aucun sens pour quelqu'un qui pointe une
cellule dans Excel. Exemple vérifié sur la feuille `ISOLEMENT` (`FirstBlockStartRow = 19`) :

| Champ | Stocké (offsets) | Affiché aujourd'hui | Attendu (Excel réel) |
|---|---|---|---|
| Identification | `RowOffsetStart=0, RowOffsetEnd=1` | `(B:E, 0-1)` | `B19:E20` |
| TypeElement | `RowOffsetStart=3, RowOffsetEnd=4` | `(B:E, 3-4)` | `B22:E23` |

Calcul : ligne réelle = `FirstBlockStartRow + RowOffset`. Le client, en pointant la 1ʳᵉ cellule
`Identification` dans le fichier Excel réel, voit `B19:E20` — c'est ce format qu'il faut
reproduire, pas l'offset relatif.

**Décision actée** : une seule représentation humaine, **partout** (liste ET formulaire de
saisie/modification) — la plage Excel absolue du 1ᵉʳ bloc (ex. `B19:E20`, ou `B9` pour une
cellule unique). Deux formats différents entre affichage et édition ont été explicitement
écartés : l'utilisateur retomberait sur les offsets bruts au moment de modifier un champ qu'il
vient de lire en coordonnées Excel, recréant la confusion d'origine.

Le modèle de domaine **ne change pas** : `BlockFieldDefinition` reste `ColumnRange` +
`RowOffsetStart`/`RowOffsetEnd` relatifs (nécessaire pour appliquer `Step` à tous les blocs
suivants, pas seulement au premier). Seule la couche Blazor traduit dans les deux sens au moment
de l'affichage et de la saisie.

**Point d'attention non négociable** : les offsets peuvent être **négatifs** (ex. ISOLEMENT,
`Designation (H:U, -1-0)` → `H18:U19`, une ligne au-dessus du début nominal du bloc — voir
`tickets-tdd-extraction.md` §C2/`spec-extraction-fichier-source-oxo.md`). Le convertisseur doit
gérer ce cas normalement, ce n'est pas une erreur.

---

## N1. `BlockFieldRangeFormatter` — conversion pure absolu ↔ offsets

Nouvelle classe utilitaire, **côté `ExcelETL.BlazorAdmin`** (pas Domain — c'est une pure
préoccupation de présentation, `BlockFieldDefinition` n'a pas besoin de connaître ce format ;
cohérent avec le principe "architecture par pertinence, pas par mimétisme" déjà appliqué au
projet).

### Signatures attendues
```csharp
// FirstBlockStartRow + ColumnRange + RowOffsetStart/End -> "B19:E20" ou "B9"
string ToAbsoluteRange(int firstBlockStartRow, string columnRange, int rowOffsetStart, int rowOffsetEnd);

// "B19:E20" ou "B9" + FirstBlockStartRow -> (ColumnRange, RowOffsetStart, RowOffsetEnd)
// Result<T> ou exception typée en cas de format invalide — à aligner sur le style d'erreurs
// déjà en place (voir etat-des-lieux-technique.md §"Exceptions").
BlockFieldRangeParseResult FromAbsoluteRange(string absoluteRange, int firstBlockStartRow);
```

### Règles de format
- Si `ColumnRange` a une seule colonne (`"B"`, pas de `:`) **et** `RowOffsetStart == RowOffsetEnd`
  → cellule unique, ex. `B9` (pas `B9:B9`).
- Sinon → plage complète, ex. `B19:E20`.
- `FromAbsoluteRange` doit rejeter (erreur localisée, pas d'exception non gérée) : format qui ne
  matche pas un pattern colonne(s)+ligne(s) Excel, ligne de fin strictement inférieure à la ligne
  de début, colonne de fin strictement avant la colonne de début.

### Validation des bornes — deux niveaux, actés avec le client (20/07)

1. **Bornes strictes Excel (bloquant, toujours appliqué)** : colonne entre `A` et `XFD` (limite
   technique réelle d'un classeur Excel), ligne entre `1` et `1 048 576`. Toute coordonnée hors
   de ces bornes n'est pas une cellule Excel valide → `FromAbsoluteRange` rejette avec une erreur
   localisée (pas de sauvegarde possible). Ce n'est pas une hypothèse métier, c'est une
   correction de saisie impossible.
2. **Avertissement de plausibilité (non bloquant)** : si la colonne dépasse `AZ` ou la ligne
   dépasse `1000`, `FromAbsoluteRange` reste **valide et sauvegardable**, mais expose une
   information supplémentaire (ex. `bool IsBeyondPracticalRange` sur le résultat, ou méthode
   séparée) permettant à l'appelant (N3) d'afficher un message d'avertissement à l'utilisateur
   sans bloquer sa saisie. Ces seuils (`AZ`/`1000`) sont un choix métier arbitraire basé sur les
   fichiers réels observés à ce jour (colonne la plus à droite : `U` ; ligne de départ la plus
   haute : `19`) — documentés ici comme tels, pas comme une limite Excel réelle, pour ne pas être
   confondus avec le niveau 1 si ce seuil doit être ajusté plus tard.

### Tests (xUnit, purs, pas de bUnit nécessaire ici)
- `ToAbsoluteRange` : les 2 cas du tableau ci-dessus (`Identification`→`B19:E20`,
  `TypeElement`→`B22:E23`), un cas à offset négatif (`Designation`→`H18:U19`), un cas cellule
  unique.
- `FromAbsoluteRange` : round-trip exact sur tous les champs de bloc du profil OXO standard
  seedé (`tickets-tdd-seed-profils-defaut.md`, §M2) — `ToAbsoluteRange` puis `FromAbsoluteRange`
  doit redonner exactement le `ColumnRange`/`RowOffsetStart`/`RowOffsetEnd` d'origine, pour
  chaque champ des 6 feuilles réelles. C'est le test de non-régression le plus important de ce
  lot : il garantit que la conversion ne dérive aucune coordonnée déjà validée en production.
- `FromAbsoluteRange` : cas d'erreur bloquant (format invalide, plage inversée, colonne au-delà
  de `XFD`, ligne au-delà de `1 048 576`, ligne `0` ou négative) → erreur localisée, pas
  d'exception non gérée remontant jusqu'au composant Razor.
- `FromAbsoluteRange` : cas limites du niveau 2 (colonne `AZ` exactement → pas d'avertissement ;
  `BA` → avertissement ; ligne `1000` exactement → pas d'avertissement ; `1001` → avertissement)
  — plage sauvegardée dans les deux cas, seul le signal d'avertissement change.

---

## N2. `ImportProfileEditor.razor` — affichage liste

- Remplacer `{ColumnRange}, {RowOffsetStart}-{RowOffsetEnd}` par
  `BlockFieldRangeFormatter.ToAbsoluteRange(...)` dans le rendu de chaque champ de bloc, pour
  toutes les règles de feuille existantes (pas seulement à la création).
- Aucun changement d'ID HTML sur les éléments existants (`Modifier`/`Supprimer` par champ) — pur
  changement de texte affiché.

### Tests (bUnit)
- Rendu d'une règle de feuille existante (ex. ISOLEMENT du profil OXO seedé) → le texte affiché
  contient `B19:E20` et `B22:E23`, ne contient plus `0-1` ni `3-4`.

---

## N3. `ImportProfileEditor.razor` — formulaire d'ajout/modification d'un champ de bloc

- Remplacer les 3 champs actuels (`Plage de colonnes`, 2 champs numériques offset) par **un seul
  champ texte** : `Plage Excel du 1ᵉʳ bloc` (placeholder `B19:E20`), utilisant la valeur courante
  de `Ligne de début du premier bloc` déjà présente dans le même formulaire (pas de champ
  dupliqué, pas de resaisie).
- À la soumission (ajout ou modification) : appel à `FromAbsoluteRange`.
  - Erreur bloquante (format invalide, plage inversée, hors bornes Excel réelles) → message
    localisé affiché sous le champ, aucune sauvegarde (même mécanisme que les erreurs Domain
    existantes, `BusinessExceptionLocalizer.TryLocalize`).
  - Avertissement de plausibilité (colonne au-delà de `AZ` ou ligne au-delà de `1000`) → message
    localisé de type "attention" (pas "erreur"), affiché à côté/sous le champ, **sauvegarde
    autorisée** malgré tout. Nouvelle clé `.resx` distincte de celle de l'erreur bloquante, pour
    ne pas mélanger les deux niveaux de gravité dans le même texte.
- À l'ouverture du formulaire de modification d'un champ existant : pré-remplir avec
  `ToAbsoluteRange(...)` calculé à partir des offsets stockés et de la ligne de début courante de
  la règle — jamais les offsets bruts.
- Nouvelle clé `.resx` pour le label (`ImportProfileEditor_PlageExcelBloc` ou équivalent aligné
  sur les conventions de nommage déjà en place dans `Application/Resources/`) et pour le message
  d'erreur de format invalide.

### Tests (bUnit)
- Ajout d'un nouveau champ avec `B19:E20` saisi, `FirstBlockStartRow=19` → le champ créé porte
  bien `RowOffsetStart=0, RowOffsetEnd=1`.
- Modification d'un champ existant (`TypeElement`) → le champ texte est pré-rempli avec
  `B22:E23`, pas `3-4`.
- Saisie invalide (`"abc"`, ou plage inversée) → message d'erreur affiché, aucun champ créé/modifié.
- Saisie hors bornes Excel réelles (ex. ligne `2000000`) → message d'erreur bloquant, aucun champ
  créé/modifié.
- Saisie au-delà du seuil de plausibilité (ex. colonne `BA` ou ligne `1500`) → message
  d'avertissement affiché, **mais le champ est bien créé/modifié** (pas de blocage).

---

## Hors périmètre (explicitement reporté)

- **`ExportProfileEditor.razor`** (Lot J) — même défaut probable, correction dans un lot séparé
  une fois ce lot-ci validé côté client.
- **`BlockFieldDefinition` / modèle Domain** — aucun changement, reste en offsets relatifs.
- **Lot L (NavMenu)** et **Lot K (migration Web API)** — aucun fichier en commun avec ce lot, pas
  de dépendance.
- **`Colonnes inconditionnelles` / `Règles de point conditionnelles`** (autres sections du même
  formulaire) — hors périmètre, ne portent pas de coordonnées Excel.

---

## Note d'efficacité d'implémentation

Séquencer dans cet ordre pour minimiser les allers-retours :
1. `BlockFieldRangeFormatter` seul, testé en xUnit pur (cycles red-green rapides, pas de bUnit à
   ce stade) — en particulier le test de round-trip contre les 6 feuilles du profil OXO standard
   seedé, qui sert de garde-fou de non-régression pour tout le reste du lot.
2. Brancher l'affichage liste (N2) — changement isolé, bUnit simple.
3. Brancher le formulaire (N3) — dépend de N1 déjà testé, donc peu de nouvelle logique de calcul
   à ce stade, essentiellement du câblage Razor + validation.

Ne pas commencer par N3 : le formulaire est le point le plus coûteux à tester en bUnit et dépend
entièrement de la justesse de N1.
