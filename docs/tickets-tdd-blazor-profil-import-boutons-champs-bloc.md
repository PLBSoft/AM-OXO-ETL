# Tickets TDD — Lot O : mise en forme visuelle de l'écran Profils d'import (boutons de champ + vue résumé)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Complète le
Lot F (`tickets-tdd-blazor-profil-import.md`) et **dépend du Lot N**
(`tickets-tdd-blazor-profil-import-lisibilite-plages-excel.md`, résolu) — ce lot ne change que la
mise en forme visuelle autour du texte déjà produit par N (`B19:E20`), pas son contenu.*

**Périmètre : uniquement `ImportProfileEditor.razor` (Profils d'import).** Même remarque que pour
le Lot N : la même restylisation sera probablement pertinente sur `ExportProfileEditor.razor`
(Lot J), mais **reportée à un lot ultérieur**, une fois validée côté client sur les profils
d'import.

---

## O0. Constat

Retour client sur capture d'écran réelle (20/07, écran `Modifier le profil d'import` en édition
d'une règle de feuille) : chaque champ de bloc affiche son nom + sa plage suivis de deux boutons
texte (`Modifier`, `Supprimer`) collés en ligne, sans séparation visuelle claire entre les items
de la liste — lisible, mais pas au niveau du reste de l'écran (respect de charte, titres,
sidebar déjà propres).

**Décision actée** : remplacer les deux boutons texte par des boutons icône seuls (crayon /
corbeille, Bootstrap Icons déjà présents dans le projet — `bi-pencil-square`, `bi-trash`, pas de
nouvelle dépendance), et restructurer chaque ligne en deux niveaux : nom du champ en premier plan,
plage Excel (produite par le Lot N) en second plan, discret.

Concerne l'unique endroit où cette liste éditable apparaît : le panneau "règle en cours de
modification" (en haut de l'écran), qui affiche chaque champ de bloc avec ses boutons
`Modifier`/`Supprimer` individuels.

**Correction par rapport à la version précédente de ce ticket** : capture d'écran réelle du 21/07
en main — la liste résumé des autres règles (plus bas sur l'écran, ex. `ISOLEMENT`, `PLATINES`...)
n'a **pas** de bouton par champ ; elle n'affiche qu'un seul `Modifier`/`Supprimer` pour la règle
entière. La restylisation icône (O1) ne concerne donc que le panneau d'édition, un seul endroit,
pas deux. Voir O2 ci-dessous pour les changements demandés sur la vue résumé, qui sont d'une
nature différente (labels de section + liste à puce, pas de boutons).

---

## O1. Restructuration visuelle de chaque item de champ de bloc

**Avant** (texte concaténé + boutons texte) :
```
TypeElement (B:E, 3-4)   [Modifier] [Supprimer]
```

**Après** (deux niveaux + boutons icône) :
```
TypeElement                          [✎] [🗑]
B22:E23
```

- Nom du champ en premier plan (poids de police plus marqué que la plage).
- Plage Excel (texte déjà produit par le Lot N, `BlockFieldRangeFormatter.ToAbsoluteRange`) en
  second plan, sur sa propre ligne sous le nom du champ, couleur atténuée (texte secondaire, pas
  primaire) **et police à chasse fixe** (monospace) — **variante A actée avec le client (21/07)** :
  texte nu sur sa ligne, pas de puce/badge avec fond coloré autour (variante B explicitement
  écartée). Objectif : désambiguïser visuellement les caractères proches (`O`/`0`, `I`/`1`) et
  signaler que c'est une coordonnée technique, sans ajouter d'élément décoratif supplémentaire à
  l'écran. Utiliser la pile de polices monospace déjà en place dans le projet si elle existe
  (vérifier `_content`/CSS globaux de `BlazorAdmin` avant d'en introduire une nouvelle) ; à
  défaut, une pile standard (`Consolas, "SFMono-Regular", Menlo, monospace`) suffit — pas de
  nouvelle dépendance de police web à ajouter pour ce détail. **Aucune nouvelle couleur
  d'accentuation** pour ce texte — reste sur la couleur secondaire déjà utilisée pour le texte
  atténué ailleurs dans l'écran, cohérent avec le principe de sobriété déjà appliqué au reste de
  l'interface.
- Séparateur horizontal fin entre chaque item de la liste (au lieu du style `list-group` par
  défaut), pour un rendu plus "tableau d'administration" que "liste Bootstrap brute".
- Boutons icône carrés (~30-36px), style neutre (bordure fine, fond transparent) pour Modifier ;
  variante avec bordure/texte de couleur danger pour Supprimer — cohérent avec la charte rouge du
  projet sans s'y confondre (le rouge de marque reste réservé à la sidebar/actions primaires,
  voir capture existante).
- `aria-label="Modifier"` / `aria-label="Supprimer"` sur les boutons icône (perte du texte
  visible, accessibilité à préserver).

**Garde-fou non négociable** : **aucun changement d'id HTML** sur les boutons existants
`Modifier`/`Supprimer` de champ, ni sur leur comportement (mêmes gestionnaires d'événement, même
logique). Claude Code doit vérifier contre le code réel les ids actuellement utilisés par les
tests bUnit existants (probablement `#edit-block-field-button-*`/`#delete-block-field-button-*`
ou équivalent — nom exact à confirmer par lecture du fichier avant modification) et les conserver
strictement à l'identique. Seul le contenu visuel à l'intérieur du bouton change (icône au lieu
de texte), pas l'identifiant, pas le texte ciblé par un éventuel test qui chercherait le contenu
textuel `"Modifier"`/`"Supprimer"` — si un tel test existe, il devra être adapté pour cibler l'id
ou l'`aria-label` plutôt que le texte visible (cohérent avec la convention déjà en place : "pas de
sélection par texte ou position" dans les tests bUnit du projet).

### Tests (bUnit)
- Rendu d'une règle existante (ex. ISOLEMENT du profil OXO seedé, après application du Lot N) :
  chaque item affiche le nom du champ et sa plage sur deux éléments distincts (assertion sur deux
  sélecteurs séparés, pas sur un seul texte concaténé) ; l'élément portant la plage a bien la
  classe/style monospace appliqué (assertion sur la classe CSS, pas sur une valeur de police
  calculée — cohérent avec la convention "pas de sélection par texte ou position").
- Les boutons Modifier/Supprimer de champ conservent leurs ids existants (non-régression —
  reprendre les mêmes assertions d'id que les tests bUnit déjà en place pour F3, sans les
  réécrire).
- Clic sur le bouton icône Modifier/Supprimer déclenche toujours le même comportement qu'avant
  (pré-remplissage du formulaire / suppression) — non-régression fonctionnelle pure, aucun
  changement de logique attendu ici.
- Présence des `aria-label` sur les deux boutons icône.

---

## O2. Vue résumé d'une règle de feuille : rappel des labels de section + liste à puce

Retour client sur capture d'écran réelle (21/07, vue résumé des règles ISOLEMENT/PLATINES/
ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS, sous le panneau d'édition). Deux défauts
constatés dans ce mode lecture seule :

1. **Colonnes inconditionnelles** : affichées comme une simple ligne de texte séparée par des
   virgules (ex. `PROLOCK VANNES, DEPROLOCK VANNES`), sans aucun label au-dessus — l'utilisateur
   ne sait pas à quoi correspond cette ligne sans revenir au formulaire d'édition.
2. **Règles de point conditionnelles** : déjà affichées en liste à puce (`<ul><li>`), mais sans
   label de section au-dessus non plus — même défaut de contexte manquant.

**Décision actée** :
- Ajouter, au-dessus de la liste des colonnes inconditionnelles, le même label que celui déjà
  utilisé dans le formulaire d'édition : **"Colonnes inconditionnelles (créer le Point
  systématiquement)"** — réutiliser la clé `.resx` existante, ne pas en créer une nouvelle.
- Remplacer la ligne de texte séparée par des virgules par une **liste à puce `<ul><li>`**, une
  entrée par nom de colonne — même format que la liste des règles de point conditionnelles juste
  en dessous, pour une cohérence visuelle entre les deux sections.
- Ajouter, au-dessus de la liste des règles de point conditionnelles (déjà en `<ul><li>`), le
  label **"Règles de point conditionnelles"** — réutiliser la clé `.resx` existante du formulaire
  d'édition.
- **Chaque label + sa liste ne s'affichent que si la collection correspondante n'est pas vide.**
  Ne jamais afficher un label de section suivi d'une liste vide (ex. `PLATINES`/
  `ORIFICES CAPACITES` n'ont pas de `ConditionalPointRule` — le label "Règles de point
  conditionnelles" ne doit donc pas apparaître du tout pour ces deux règles ; `PROCEDURE` n'a ni
  l'un ni l'autre — aucun des deux labels n'apparaît).
- Ordre inchangé : colonnes inconditionnelles avant les règles de point conditionnelles (déjà
  l'ordre actuel à l'écran).

### Tests (bUnit)
- Rendu résumé de `ISOLEMENT` (profil OXO seedé) : label "Colonnes inconditionnelles (créer le
  Point systématiquement)" présent, suivi d'un `<ul>` contenant exactement 2 `<li>` (`PROLOCK
  VANNES`, `DEPROLOCK VANNES`) ; label "Règles de point conditionnelles" présent, suivi d'un
  `<ul>` contenant l'entrée `ZÉRO ENERGIE EN PRESENCE EE (PS941)`.
- Rendu résumé de `PLATINES` (pas de `ConditionalPointRule`) : label + liste "Colonnes
  inconditionnelles" présents (7 colonnes) ; **aucune trace** du label "Règles de point
  conditionnelles" dans le DOM (assertion sur absence, pas juste liste vide).
- Rendu résumé de `PROCEDURE` (aucune des deux collections) : ni l'un ni l'autre label présent.

---

- **`ExportProfileEditor.razor`** — même restylisation probable, lot séparé après validation
  client sur import.
- **Contenu du texte de plage Excel** — déjà traité par le Lot N, non retouché ici.
- **Logique de modification/suppression d'un champ de bloc** — aucun changement de comportement,
  uniquement la présentation.
- **Sections "Colonnes inconditionnelles" / "Règles de point conditionnelles" / "Ajouter une
  règle de feuille"** du même formulaire — hors périmètre visuel de ce lot, non touchées.

---

## Note d'efficacité d'implémentation

1. Localiser et lire le balisage actuel exact du panneau d'édition (O1) avant toute modification —
   confirmer les ids réels des boutons de champ.
2. **O1 et O2 sont indépendants** (fichiers/sections différentes du même composant) — peuvent être
   faits dans n'importe quel ordre ou en parallèle si Claude Code le juge plus efficace ; aucune
   dépendance entre les deux.
3. Pour O2, vérifier que les clés `.resx` des deux labels existent déjà (utilisées dans le
   formulaire d'édition) avant d'en créer de nouvelles — les réutiliser telles quelles.
4. Vérifier la suite bUnit complète de `ImportProfileEditor` après coup (pas seulement les tests
   nouveaux) pour confirmer qu'aucun test existant ne dépendait implicitement du texte
   `"Modifier"`/`"Supprimer"` visible (O1) ni du format texte séparé par virgules (O2).
