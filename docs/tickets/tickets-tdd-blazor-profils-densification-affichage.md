# Tickets TDD — Lot R : densification de l'affichage des profils (import + export)

✅ Implémenté — voir commit `4bb4bb9`

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite au
Lot P (`tickets-tdd-blazor-profil-import-cartes-regles-feuille.md`, cartes par règle de feuille)
et aux tickets de parité Q (`tickets-tdd-blazor-profil-export-parite-visuelle.md`/
`-fonctionnelle.md`). Ce lot ne change ni le modèle de domaine ni la logique métier : uniquement
la structure CSS/HTML d'affichage des cartes déjà existantes.*

**Demande client** : sur `ImportProfileEditor.razor` (`/import-profiles/{id}/edit`), la
présentation actuelle des cartes de règles de feuille est trop aérée sur un écran 27" — une seule
carte par ligne, contenu empilé verticalement, seulement ~2 des 6 feuilles visibles sans scroll.
**Exigence explicite** : la présentation doit rester strictement identique entre
`ImportProfileEditor.razor` et `ExportProfileEditor.razor` (parité déjà actée aux tickets Q,
`.sheet-rule-card` partagée) — ce lot s'applique donc **aux deux composants simultanément**, pas
seulement à l'import.

**Conventions déjà en place à respecter** : `convention-ui-blazor-alignement-boutons.md` (boutons
`Modifier`/`Supprimer` toujours alignés à droite du conteneur — **inchangé par ce lot**, aucune
carte ne doit perdre cet alignement pendant la restructuration) ; IDs HTML stables, jamais de
sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit.

---

## R1. Grille responsive des cartes `.sheet-rule-card`

**Comportement attendu** : remplacer l'empilement vertical (une carte = une ligne pleine largeur)
par une grille CSS (`display: grid; grid-template-columns: repeat(auto-fill, minmax(480px, 1fr))`
ou équivalent) sur le conteneur parent des cartes, identique sur les deux éditeurs. Le nombre de
colonnes se détermine automatiquement selon la largeur de viewport — pas de breakpoint fixe codé
en dur par nombre de feuilles.

**Tests** (bUnit) :
- Le conteneur parent des `.sheet-rule-card` porte bien la classe CSS de grille attendue (test sur
  l'attribut `class`, pas sur un rendu visuel — bUnit ne calcule pas de layout réel).
- Aucune régression sur le nombre de cartes rendues ni sur leur contenu (réutiliser les
  assertions déjà existantes de `ImportProfileEditorTests`/`ExportProfileEditorTests` sur le
  rendu des règles).
- **Test de parité** : un test dédié qui charge le même nombre de règles côté import et côté
  export et vérifie que la classe CSS du conteneur est **identique dans les deux composants**
  (comparaison de chaîne, pas juste "les deux ont une classe non vide") — garde-fou explicite
  contre une dérive future entre les deux écrans.

## R2. Grille compacte des champs à l'intérieur d'une carte

**Comportement attendu** : à l'intérieur d'une `.sheet-rule-card`, remplacer l'empilement
un-champ-par-ligne (libellé + plage Excel) par une disposition en grille 2-3 colonnes selon la
largeur disponible, même mécanisme CSS que R1 mais à l'échelle du champ plutôt que de la carte.
S'applique à `SheetExtractionRule`/`BlockFieldDefinition` côté import et à
`SheetGenerationRule`/`ColumnDefinition`/`PointColumnDefinition` côté export.

**Tests** :
- Rendu inchangé au niveau du contenu (même libellés, mêmes valeurs affichées) — seule la classe/
  structure CSS du conteneur de champs change, à vérifier par assertion sur la classe attendue.
- Parité de classe CSS entre les deux composants, même principe que R1.

## R3. Repli (accordéon) des sous-listes à taille variable ✅ terminé

**Statut** : implémenté (commit `3b0390c`). Documenté ici pour mémoire, non réouvert.

**Comportement attendu** : les sous-blocs de taille non bornée (`UnconditionalColonneNames` +
`ConditionalPointRule` côté `ISOLEMENT` en import ; listes de `ColumnDefinition`/
`PointColumnDefinition` côté export si elles grossissent de façon comparable) sont **toujours
repliés par défaut** (décision actée, pas de seuil conditionnel) dans un élément `<details>` ou
équivalent Bootstrap, avec un résumé visible par défaut du type
`"{N} colonnes inconditionnelles, {M} règles conditionnelles"` (clé resx dédiée, EN/FR). Cliquer
développe la liste complète.

**Correctif post-implémentation** : la première version ne réagissait pas au clic sur l'élément
portant la classe `sheet-rule-sublist-details` (accordéon inerte). Corrigé en TDD — cause exacte
tracée dans l'historique du commit `3b0390c`, non recopiée ici pour éviter la duplication
d'information entre le document vivant et git.

**Tests** :
- État initial replié : la liste complète n'est pas présente dans le DOM (vérification `FindAll`
  vide, pas un test de style `display:none` — même règle que L2/NavMenu), seul le résumé est
  visible.
- Clic sur le résumé → liste complète apparaît dans le DOM (nouvelle assertion `FindAll` non
  vide), avec les mêmes valeurs qu'avant restructuration.
- Toggle bidirectionnel : un second clic referme la sous-liste.
- Indépendance par carte : déplier une carte n'affecte pas l'état d'accordéon des autres cartes
  de règle de feuille.
- Sous-liste vide (0 colonnes inconditionnelles, 0 règles conditionnelles) : le clic ne casse pas
  le rendu, un état vide cohérent est affiché.
- Résumé affiche le bon compte quand la liste change (ajout/suppression d'un élément avant
  sauvegarde) — pas de valeur figée au premier rendu.
- Parité de comportement (replié par défaut, même structure de résumé) entre import et export,
  même principe de test que R1/R2.
- Accessibilité : élément cliquable focusable au clavier (`tabindex`/rôle ARIA `button`/
  `aria-expanded` si le mécanisme retenu n'est pas un `<details>` natif).

**Hors périmètre explicite de R3** : tabs par feuille (option évoquée en discussion mais non
retenue) — reporté, ne pas anticiper.

---

## Note d'efficacité d'implémentation

- Faire R1 et R2 **en un seul passage sur les deux composants à la fois** (import puis export
  immédiatement après, ou un composant CSS partagé si l'architecture actuelle le permet déjà —
  vérifier d'abord si `.sheet-rule-card` vit dans un fichier `.razor.css` partagé ou dupliqué
  entre les deux éditeurs avant de choisir l'approche, pour éviter une divergence silencieuse).
- Réutiliser les tests de parité déjà écrits aux tickets Q comme modèle pour les nouveaux tests de
  parité R1/R2/R3, plutôt que d'inventer un nouveau pattern de test.
- Aucun nouveau concept Domain/Application : ce lot est purement `BlazorAdmin`, comme le Lot J
  pour la parité initiale.

**Dossiers** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor(.css)` et
`ExportProfileEditor.razor(.css)` (+ miroir tests dans
`tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`).
