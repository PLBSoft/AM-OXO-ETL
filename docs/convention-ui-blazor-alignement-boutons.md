# Convention UI — alignement des boutons d'action (BlazorAdmin)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Décision actée
avec le client le 21/07, en marge du Lot P (`tickets-tdd-blazor-profil-import-cartes-regles-feuille.md`).
Référencer ce document plutôt que réexpliquer la règle dans chaque futur ticket UI.*

## Règle générale

Tous les boutons d'action qui opèrent sur un contenu (créer, modifier, supprimer, enregistrer,
annuler) sont **alignés à droite** du conteneur qui les entoure (carte de règle, formulaire, ligne
de champ) — jamais laissés à l'alignement naturel gauche du flux HTML. Objectif : un point de
repère visuel unique et prévisible pour l'utilisateur ("les actions sont toujours à droite"),
plutôt qu'une position qui varie d'un écran à l'autre.

## Portée

Toutes les pages d'administration `ExcelETL.BlazorAdmin` :
- **Profils d'import** — déjà en cours de traitement (Lots N/O/P).
- **Profils d'export** (`ExportProfileEditor.razor`, Lot J) — **même règle applicable, mais
  traitement reporté à un lot ultérieur**, une fois la restylisation validée côté client sur les
  profils d'import qui servent de modèle. Ce n'est pas un oubli : c'est une décision explicite de
  séquencement, à ne pas rouvrir prématurément.
- Tout futur écran d'administration.

## Application concrète

- **Boutons de bas de carte/section** (ex. `modify-sheet-rule-button-{i}`/
  `delete-sheet-rule-button-{i}` d'une règle de feuille entière, `save-profile-button` du
  formulaire racine, `Annuler`) : alignés à droite du conteneur (`justify-content: flex-end` ou
  équivalent), quel que soit le nombre de boutons.
- **Boutons intégrés à une ligne de saisie existante** (ex. `Ajouter le champ` en fin de ligne
  "Nom du champ / Plage Excel / Ajouter le champ") : déjà à l'extrémité droite de leur ligne par
  construction — cette règle ne change rien pour eux, mentionné ici pour éviter de rouvrir la
  question à chaque futur ticket.
- **Boutons icône par champ de bloc** (Lot O1, `Modifier`/`Supprimer` de champ) : déjà à droite
  de leur ligne par construction (voir mockup O1, `justify-content: space-between` avec les
  boutons à l'extrémité droite) — conforme sans modification supplémentaire.

## Portée future

Toute nouvelle page ou tout nouveau composant Blazor Admin doit respecter cette convention dès sa
conception, sans qu'il soit nécessaire de le repréciser dans chaque nouveau ticket — une simple
référence à ce document suffit.
