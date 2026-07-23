# Ticket : R3 — Repli (accordéon) des sous-listes à taille variable ne réagit pas au clic

✅ Implémenté — voir commit `3b0390c`

## Contexte

Page Blazor « Modifier le profil d'import » (`/import-profiles/{id}/edit`). Les cartes de « Règles de feuille » (PROCEDURE, ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS) contiennent des sous-listes de taille variable (colonnes inconditionnelles, règles conditionnelles) qui doivent pouvoir se replier/déplier (accordéon).

**Statut actuel : KO.** En lançant l'app dans Visual Studio et en cliquant sur l'élément portant la classe `sheet-rule-sublist-details`, rien ne se passe — les règles ne s'affichent pas/ne se déplient pas.

Ce ticket suit une approche **TDD** : chaque tâche commence par un test qui échoue, puis l'implémentation minimale pour le faire passer, puis un refactor si nécessaire.

---

## Étape 0 — Investigation préalable (obligatoire avant tout code)

- [ ] Localiser le(s) fichier(s) `.razor` contenant la classe CSS `sheet-rule-sublist-details` (recherche texte dans le repo).
- [ ] Déterminer le mécanisme d'accordéon utilisé :
  - Élément HTML natif `<details>`/`<summary>` ?
  - État C# (`bool isExpanded` / dictionnaire par règle) piloté par `@onclick` ?
  - Composant de librairie UI (MudBlazor `MudExpansionPanel`, etc.) ?
  - JS interop (toggle de classe CSS via `IJSRuntime`) ?
- [ ] Si `<details>`/`<summary>` natif : vérifier qu'aucun élément parent ne capte le clic (ex. `@onclick` sur un `<div>` englobant sans `stopPropagation`), et qu'aucun CSS (`pointer-events: none`, z-index, overlay) ne bloque l'interaction.
- [ ] Si état C# : vérifier que la variable d'état est bien liée par instance de règle (et pas une variable partagée écrasée pour toutes les cartes), et que le rendu conditionnel du contenu dépend bien de cette variable.
- [ ] Si JS interop : ouvrir la console navigateur (F12) pendant le clic et noter toute erreur JS (probable cause du "rien ne se passe").
- [ ] Identifier le framework de test de composants Blazor déjà utilisé dans le repo (ex. bUnit) et un test existant similaire à copier pour les conventions (nommage, mocks, setup).
- [ ] Confirmer la cause racine avant de passer à l'étape 1.

---

## Étape 1 — Reproduire le bug par un test (rouge)

- [ ] Écrire un test bUnit qui :
  - Rend la carte de règle de feuille (ou la page complète) avec un profil contenant au moins une règle ayant une sous-liste à taille variable (colonnes inconditionnelles et/ou règles conditionnelles non vides).
  - Simule un clic sur l'élément portant la classe `sheet-rule-sublist-details`.
  - Vérifie que le contenu de la sous-liste (ex. noms des colonnes inconditionnelles, ou règles conditionnelles) devient visible/présent dans le DOM rendu après le clic.
- [ ] Lancer le test et confirmer qu'il échoue actuellement (contenu absent après clic), ce qui documente le bug de façon reproductible.

---

## Étape 2 — Corriger le mécanisme d'accordéon (vert)

- [ ] Appliquer le correctif identifié à l'étape 0 (selon la cause racine trouvée) :
  - Cas `<details>` natif cassé par un handler parent → retirer/corriger le `@onclick` parent, ou ajouter `@onclick:stopPropagation` là où c'est légitime.
  - Cas état C# non lié correctement → binder l'état d'expansion par identifiant unique de règle (`Guid`/clé), pas une seule variable globale.
  - Cas JS interop en échec → corriger l'appel interop (nom de fonction, timing `OnAfterRenderAsync`, erreur silencieuse).
- [ ] Faire passer le test de l'étape 1.

---

## Étape 3 — Couvrir les cas limites (rouge → vert)

- [ ] Test : cliquer une seconde fois referme la sous-liste (toggle bidirectionnel).
- [ ] Test : déplier une carte n'affecte pas l'état des autres cartes (chaque règle de feuille a un état d'accordéon indépendant).
- [ ] Test : une sous-liste vide (0 colonnes inconditionnelles, 0 règles conditionnelles) ne casse pas le clic et affiche un état vide cohérent (ex. « Aucune règle »).
- [ ] Implémenter le nécessaire pour faire passer chaque test.

---

## Refactor

- [ ] Vérifier l'accessibilité : l'élément cliquable doit être focusable au clavier (`tabindex`, rôle ARIA `button`/`aria-expanded` si ce n'est pas un `<details>` natif).
- [ ] Nettoyer tout code de debug ou console.log ajouté pendant l'investigation.
- [ ] Vérifier la cohérence visuelle avec R1 (grille responsive) et R2 (grille compacte) déjà en place — l'accordéon ne doit pas casser leur mise en page.

---

## Definition of Done

- [ ] Le clic sur `sheet-rule-sublist-details` déplie/replie la sous-liste correspondante.
- [ ] Le comportement est indépendant par carte de règle de feuille.
- [ ] Les sous-listes vides sont gérées sans erreur.
- [ ] Tous les tests écrits dans ce ticket sont verts.
- [ ] Aucune régression visuelle sur R1/R2.
