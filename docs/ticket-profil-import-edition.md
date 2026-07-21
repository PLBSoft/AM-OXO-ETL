# Ticket : Correction du formulaire d'édition d'un profil d'import

## Contexte

Page Blazor « Modifier le profil d'import » (`/import-profiles/{id}/edit`).

Deux problèmes constatés :

1. Les 3 champs en haut du formulaire (`profile-name-input`, `profile-repere-prefix-input`, `profile-equipement-type-element-nom-input`) n'ont aucun libellé visible — l'utilisateur ne sait pas à quoi ils correspondent.
2. Les « Règles de feuille » existantes (PROCEDURE, ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS, etc.) sont affichées en texte brut, sans aucun moyen de les éditer ou de les supprimer. Seul l'ajout d'une nouvelle règle est possible via le formulaire du bas.

Ce ticket suit une approche **TDD** : chaque tâche commence par l'écriture d'un test qui échoue, puis l'implémentation minimale pour le faire passer, puis un refactor si nécessaire.

---

## Étape 0 — Investigation préalable (pas de code)

- [ ] Localiser le fichier `.razor` de la page d'édition (rechercher `profile-name-input` ou « Modifier le profil d'import »).
- [ ] Identifier le code-behind (`.razor.cs`) ou le `@code` inline.
- [ ] Identifier les modèles C# concernés : `ImportProfile`, `RegleFeuille` (ou équivalent), `ChampBloc`, `ColonneInconditionnelle`, `RegleConditionnelle`.
- [ ] Identifier comment la liste de règles est actuellement rendue (`@foreach`, composant enfant, etc.) et comment elle est persistée (méthode déclenchée par « Enregistrer le profil »).
- [ ] Identifier la lib UI utilisée (MudBlazor, Bootstrap, composants maison) pour rester cohérent dans le style.
- [ ] Identifier le framework de test existant pour les composants Blazor (ex. bUnit) et où se trouvent les tests similaires déjà écrits dans le repo, pour copier les conventions.

---

## Étape 1 — Ajouter les libellés manquants

### Test (rouge)
- [ ] Écrire un test bUnit qui rend la page d'édition et vérifie la présence d'un `<label>` (ou équivalent) associé à chacun des 3 champs, avec le texte attendu :
  - « Nom du profil » pour `profile-name-input`
  - « Préfixe de repère » pour `profile-repere-prefix-input`
  - « Nom du type d'élément d'équipement » pour `profile-equipement-type-element-nom-input`
- [ ] Vérifier que le test échoue (aucun label présent actuellement).

### Implémentation (vert)
- [ ] Ajouter un `<label>` (ou `Label="..."` si MudBlazor) pour chacun des 3 champs, en réutilisant le style des labels déjà présents dans l'app (ex. « Règles de feuille »).
- [ ] Faire passer le test.

### Refactor
- [ ] Vérifier l'accessibilité (association `for`/`id` correcte entre label et input).

---

## Étape 2 — Rendre les règles de feuille existantes éditables

### 2.1 — Affichage d'un bouton « Modifier » par règle

**Test (rouge)**
- [ ] Écrire un test qui rend la page avec un profil contenant au moins une règle de feuille, et vérifie la présence d'un bouton « Modifier » pour cette règle.

**Implémentation (vert)**
- [ ] Ajouter un bouton « Modifier » à côté de chaque règle affichée dans la boucle existante.

### 2.2 — Bascule en mode édition

**Test (rouge)**
- [ ] Écrire un test qui simule un clic sur « Modifier » et vérifie que la règle correspondante passe en mode édition (ex. affichage des champs pré-remplis au lieu du texte brut), sans affecter les autres règles.

**Implémentation (vert)**
- [ ] Ajouter un état d'édition par règle (ex. `Guid? _editingRuleId` ou flag `IsEditing` sur le modèle d'affichage).
- [ ] Au clic sur « Modifier », basculer uniquement la règle concernée en mode édition.

### 2.3 — Formulaire d'édition pré-rempli

**Test (rouge)**
- [ ] Écrire un test qui vérifie qu'en mode édition, tous les champs suivants sont pré-remplis avec les valeurs existantes de la règle :
  - Nom de la feuille, ligne de début, pas, champ d'arrêt
  - Champs du bloc (nom, plage de colonnes, les 2 valeurs numériques)
  - Colonnes inconditionnelles
  - Règles de point conditionnelles

**Implémentation (vert)**
- [ ] Extraire (si pas déjà fait) un composant partagé pour le sous-formulaire de règle, réutilisé à la fois pour l'ajout et l'édition.
- [ ] Pré-remplir ce composant avec les valeurs de la règle en cours d'édition.

**Refactor**
- [ ] Factoriser le code dupliqué entre formulaire d'ajout et formulaire d'édition si ce n'est pas déjà un composant unique.

### 2.4 — Enregistrement des modifications

**Test (rouge)**
- [ ] Écrire un test qui modifie une valeur dans le formulaire d'édition, clique sur « Enregistrer les modifications », et vérifie que :
  - la règle existante est mise à jour dans la liste en mémoire (pas ajoutée en double),
  - le mode édition se referme.

**Implémentation (vert)**
- [ ] Ajouter un bouton « Enregistrer les modifications » qui met à jour l'item existant.
- [ ] Ajouter un bouton « Annuler » qui sort du mode édition sans appliquer les changements.

**Test complémentaire (rouge → vert)**
- [ ] Écrire un test qui vérifie qu'« Annuler » restaure les valeurs d'origine (aucune modification appliquée).

### 2.5 — Suppression d'une règle

**Test (rouge)**
- [ ] Écrire un test qui clique sur un bouton « Supprimer » d'une règle et vérifie qu'elle disparaît de la liste en mémoire.

**Implémentation (vert)**
- [ ] Ajouter un bouton « Supprimer » par règle de feuille.
- [ ] (Si pertinent) ajouter aussi la suppression unitaire d'un champ / d'une colonne inconditionnelle / d'une règle conditionnelle à l'intérieur d'une règle de feuille.

### 2.6 — Persistance globale

**Test (rouge)**
- [ ] Écrire un test (ou test d'intégration) qui modifie une règle, clique sur « Enregistrer le profil » (bouton global), recharge les données, et vérifie que la modification a persisté côté backend/API.

**Implémentation (vert)**
- [ ] Vérifier/adapter la méthode de sauvegarde globale pour qu'elle inclue bien l'état à jour de la liste des règles, et pas seulement les 3 champs du haut.

---

## Étape 3 — Non-régression

- [ ] Écrire un test vérifiant que l'ajout d'une nouvelle règle (formulaire du bas, fonctionnalité déjà existante) fonctionne toujours après les changements ci-dessus.
- [ ] Lancer l'ensemble de la suite de tests existante sur la page pour vérifier l'absence de régression.

---

## Definition of Done

- [ ] Les 3 champs du haut ont un libellé clair et accessible.
- [ ] Chaque règle de feuille existante peut être modifiée, annulée, et supprimée.
- [ ] Les modifications persistent après clic sur « Enregistrer le profil » et rechargement de la page.
- [ ] L'ajout d'une nouvelle règle fonctionne toujours (pas de régression).
- [ ] Tous les tests écrits dans ce ticket sont verts.
