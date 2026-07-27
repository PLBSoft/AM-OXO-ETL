# Tickets TDD — Lot 030 : parité visuelle Import/Export, correctif `form-floating` généralisé,
boutons globaux sur une seule ligne

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Deuxième lot
numérique après le lot 029 (`tickets-tdd-lot-029-renommage-base-donnees.md`). Déclenché par une
comparaison directe de captures d'écran mobile fournies par Simon le 24/07 :
`/export-profiles/{id}/edit` (qualité jugée correcte, cible de référence) contre
`/import-profiles/{id}/edit` (qualité jugée médiocre), ainsi que les deux pages de création
`/export-profiles/new` et `/import-profiles/new`.*

**Ce lot lève explicitement une exclusion actée dans deux lots précédents** :
- Le Lot X (`tickets-tdd-blazor-polish-ux-lot-x.md`, Partie B/C, X3-X9) précise noir sur blanc
  que `ImportProfileEditor.razor` est **hors périmètre**, l'extension à l'import n'étant
  "envisageable qu'après validation client explicite de la parité, pas par défaut".
- Le Lot Y (`tickets-tdd-blazor-polish-ux-lot-y.md`) répète la même exclusion dans sa section
  "Hors périmètre explicite".

La comparaison de captures d'écran fournie par Simon ce jour **est** cette validation client
explicite. Ce lot ne rouvre donc pas X3-X5/Y2/Y3 dans leur contenu (le pattern qu'ils décrivent
reste la référence, inchangé), il **étend leur application** à `ImportProfileEditor.razor` et
généralise par ailleurs le correctif `form-floating` (Y2) à tout le périmètre applicatif plutôt
qu'au seul champ qu'il ciblait à l'origine.

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`
(boutons d'action alignés à droite en desktop), `convention-ui-blazor-icones-boutons.md` ; IDs
HTML stables, jamais de sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions
7.x + Moq + bUnit ; Bootstrap déjà en usage (aucune nouvelle dépendance CSS/JS). bUnit ne calcule
pas de layout réel : tous les tests de ce lot portent sur la présence de classes/structure DOM,
pas sur un rendu pixel.

**Hors périmètre de ce lot (reporté, à garder en mémoire)** : la demande d'un composant d'upload
de fichier personnalisé masquant le texte natif du navigateur reste hors périmètre (décision X11,
confirmée à nouveau le 24/07 — voir mémoire de session) — ne pas la réouvrir ici.

---

## 30.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `ExportProfileEditor.razor` dans son état actuel réel (routes `/export-profiles/new`
  et `/export-profiles/{id}/edit`) : confirmer lesquels des correctifs X3 (empilement vertical),
  X4 (`bg-light` + `form-floating`), X5 (boutons d'ajout `w-100 mt-3` en contour) et Y3 (CTA final
  `w-100 btn-lg mt-4 mb-4`) sont réellement fusionnés à ce jour, plutôt que supposé d'après les
  captures d'écran. C'est la référence exacte à reproduire côté import — pas la description
  textuelle des tickets X/Y, qui peut avoir légèrement dérivé à l'implémentation.
- [ ] Lire `ImportProfileEditor.razor` dans son état actuel réel (routes `/import-profiles/new`
  et `/import-profiles/{id}/edit`) : cartographier tous les champs racine (Nom du profil, Préfixe
  de repère, Nom du type d'élément d'équipement), tous les sous-formulaires (Tableaux,
  Applications, Règles de feuille avec leurs blocs `RepeatingBlockLocator`/`BlockFieldDefinition`/
  `ConditionalPointRule`) et leurs classes CSS/structure actuelles, pour établir la liste complète
  des conversions à faire.
- [ ] Rechercher (`grep`) toutes les occurrences de `form-floating` dans
  `ExcelETL.BlazorAdmin` (pas seulement `ExportProfileEditor.razor`) pour établir la liste
  exhaustive des champs concernés par 30.6 — l'audit doit couvrir toute page utilisant ce motif,
  pas seulement les deux éditeurs de profil.
- [ ] Lire `ImportProfiles.razor` et `ExportProfiles.razor` : structure exacte du conteneur
  `right-aligned-actions`/`d-grid gap-2 d-md-flex mb-3` et des classes portées individuellement
  par `#test-export-profile-button`/`#create-export-profile-button` et leurs équivalents import
  (`#test-import-profile-button`/`#create-profile-button`, voir Lot S/V4).
- [ ] Confirmer que la suite de tests existante (`ImportProfileEditorTests`,
  `ExportProfileEditorTests`, `ImportProfilesTests`, `ExportProfilesTests`) est verte avant toute
  modification (baseline).

---

# Partie A — Extension des patterns X3/X4/X5/Y3 à `ImportProfileEditor.razor`

## 30.1. Empilement vertical pleine largeur des champs (extension de X3)

**Comportement attendu**, strictement identique au pattern déjà livré côté export (pas une
nouvelle variante) :
- Champs racine "Nom du profil", "Préfixe de repère", "Nom du type d'élément d'équipement" :
  suppression de toute classe de grille à trois colonnes, passage en `col-12`/pleine largeur avec
  `mb-3`, empilés verticalement.
- Sous-formulaire "Tableaux" (champ "Nom du tableau") et sous-formulaire "Applications" (champ
  "Nom de l'application") : même traitement, `col-12`/`mb-3`.
- Sous-formulaire "Ajouter une règle de feuille" : "Nom de la feuille", "Ligne de début du
  premier bloc", "Pas", "Nom du champ d'arrêt" empilés verticalement, chacun `col-12`/`mb-3` (au
  lieu de la grille à quatre colonnes actuelle visible en capture).
- Sous-formulaire "Champs du bloc" ("Nom du champ", "Plage Excel du champ") : même traitement.

**Tests** (bUnit) :
- Chaque conteneur de champ listé ci-dessus porte `col-12 mb-3`, plus aucune classe `col-md-*`
  (test sur absence de classe, pas seulement présence de la nouvelle) — même assertion que X3.
- Non-régression fonctionnelle complète : tous les tests existants de `ImportProfileEditorTests`
  sur la saisie/l'ajout d'un tableau/d'une application/d'une règle de feuille/d'un champ de bloc
  restent verts sans modification de leur intention (seule la classe CSS du conteneur change).

## 30.2. Hiérarchie visuelle `bg-light` et étiquettes flottantes `form-floating` (extension de X4)

**Comportement attendu** :
- Les sous-formulaires imbriqués ("Tableaux", "Applications", "Champs du bloc",
  "Conditions"/`ConditionalPointRule` s'ils existent sous forme de carte) reçoivent `bg-light` en
  plus de leur `card` existante, à l'identique de X4.
- Tous les champs de saisie de `ImportProfileEditor.razor` (racine et sous-formulaires) passent en
  `form-floating`, structure identique à celle validée côté export (X4) et côté "Mon Profil" (V6) :
  `input`/`select` avant le `<label for="...">`, jamais l'inverse.

**Tests** (bUnit) :
- Les conteneurs de sous-formulaires listés portent bien `bg-light` en plus de `card`.
- Un échantillon de champs (au moins "Nom du profil" et un champ de "Règles de feuille") est bien
  enveloppé dans un conteneur `form-floating`, avec cohérence `id`/`for`.
- Non-régression : la liaison Blazor (`@bind`) de chaque champ reste fonctionnelle après passage
  en `form-floating` (réutiliser les tests de saisie existants de `ImportProfileEditorTests`).

## 30.3. Boutons d'ajout intermédiaires — pleine largeur, contour, `mt-3` (extension de X5)

**Comportement attendu** :
- "Ajouter le tableau", "Ajouter l'application", "Ajouter le champ", "Ajouter une règle de
  feuille" passent en `w-100 mt-3` avec un style contour (`btn-outline-secondary` ou
  `btn-outline-primary` — reprendre exactement la couleur retenue en X5 pour l'export, pas une
  nouvelle décision), pour ne pas concurrencer visuellement le bouton d'enregistrement final.

**Tests** (bUnit) :
- Chaque bouton d'ajout porte `w-100 mt-3` et une classe `btn-outline-*` (et non plus une classe
  pleine), tandis que le bouton d'enregistrement final conserve sa classe pleine actuelle.
- Non-régression : clic sur chaque bouton d'ajout déclenche toujours la même logique d'ajout à la
  collection en mémoire, sans navigation ni soumission prématurée du formulaire racine.

## 30.4. CTA final "Enregistrer le profil" — pleine largeur mobile-first (extension de Y3)

**Comportement attendu** : le bouton de sauvegarde de `ImportProfileEditor.razor` reçoit
`w-100 btn-lg mt-4 mb-4` (+ la classe de breakpoint retenue en V12/Y3, ex. `w-md-auto`),
strictement identique au traitement du bouton équivalent côté export.

**Tests** (bUnit) :
- Le bouton de sauvegarde porte bien `w-100 btn-lg mt-4 mb-4` (+ classe de breakpoint), sans
  changement de son `id` existant.
- Non-régression : le clic déclenche toujours la même logique d'enregistrement.

## 30.5. Test de parité structurelle explicite Import/Export

**Comportement attendu** : au-delà des tests unitaires de chaque ticket ci-dessus, un test dédié
compare directement les classes CSS des éléments équivalents entre `ImportProfileEditor.razor` et
`ExportProfileEditor.razor` rendus avec un jeu de données comparable (même pattern que le test de
parité déjà utilisé en R1) — garde-fou explicite contre toute dérive future entre les deux écrans,
plutôt qu'une simple confiance dans deux suites de tests indépendantes qui pourraient diverger
silencieusement avec le temps.

**Tests** (bUnit) :
- Comparaison de chaîne (pas juste "les deux ont une classe non vide") sur : conteneur de champ
  racine, conteneur de sous-formulaire (`card bg-light`), bouton d'ajout intermédiaire, bouton de
  sauvegarde final.

---

# Partie B — Correctif structurel généralisé `form-floating` (label/placeholder superposé)

## 30.6. Audit et correctif de toutes les occurrences `form-floating`, pas seulement "Nom de la colonne"

**Constat client (capture)** : sur `/export-profiles/new`, le label "Nom de la colonne" se
superpose au texte déjà saisi/au placeholder dans le sous-formulaire "Colonnes Points" — visible
en création **et** en édition.

**Rappel de la contrainte technique `form-floating` (Bootstrap 5)** : l'élément `input`/`select`
doit apparaître **avant** son `<label for="...">` dans le DOM (jamais l'inverse), et l'attribut
`placeholder` de l'input doit être présent et non vide (a minima `" "` si aucun texte de
substitution n'est voulu) — le positionnement du label flottant repose sur le pseudo-sélecteur CSS
`:placeholder-shown`, qui ne se déclenche pas sans un `placeholder` défini. Exemple fautif fourni
par Simon :

```html
<div class="form-floating">
  <input id="point-column-nom-input" class="form-control" placeholder="Nom de la colonne">
  <label for="point-column-nom-input">Nom de la colonne</label>
</div>
```

Ici l'ordre DOM `input` puis `label` est correct et le `placeholder` est non vide — le
chevauchement visible en capture vient donc probablement d'un autre facteur à isoler en
investigation (ex. `placeholder` et texte du `<label>` strictement identiques, ce qui rend le
chevauchement visible uniquement pendant la phase où le label flottant n'est pas encore monté/CSS
non chargé, ou variante Blazor où le rendu SSR initial diffère du rendu interactif) — **30.0 doit
donc inclure une inspection précise du CSS `:focus`/`:not(:placeholder-shown)` réellement appliqué
à cet input avant de conclure sur la cause**, plutôt que de réappliquer mécaniquement le correctif
Y2 qui ciblait uniquement l'ordre DOM et le `placeholder`.

**Portée volontairement élargie par rapport à Y2** : Y2 ne corrigeait que le champ "Nom de la
colonne" de `ExportProfileEditor.razor`. Ce ticket reprend l'audit à zéro sur **tout le
périmètre applicatif** identifié en 30.0 (`grep form-floating`), incluant :
- `ImportProfileEditor.razor` et `ExportProfileEditor.razor`, routes `/new` **et** `/{id}/edit`
  des deux (le bug est confirmé présent dans les deux vues par Simon).
- Tout champ converti en `form-floating` par 30.2 dans ce même lot (import) — l'audit doit
  s'exécuter **après** 30.2 pour couvrir les champs nouvellement convertis, pas seulement les
  champs déjà en `form-floating` avant ce lot.
- Toute autre page du périmètre `grep` (ex. "Mon Profil" si des champs y ont été ajoutés depuis
  V6, page de test de profil si elle utilise ce motif).

**Comportement attendu** :
- Chaque champ en violation (ordre DOM, `placeholder` vide/absent, ou toute autre cause isolée en
  30.0) est corrigé individuellement, sans modification de la liaison `@bind` ni du nom du champ
  métier — uniquement la structure HTML/l'attribut `placeholder`/le CSS responsable.

**Tests** (bUnit) :
- Un test structurel par champ concerné (ou un test paramétré qui énumère tous les `id` de champs
  `form-floating` connus des deux éditeurs, pour servir de garde-fou contre une régression future
  plutôt qu'un test isolé par champ qui n'empêche pas l'apparition d'un nouveau champ mal
  structuré) : `input`/`select` précède le `<label>` dans l'arbre DOM rendu, et l'attribut
  `placeholder` est non vide.
- Non-régression : la saisie dans chaque champ concerné reste bien liée à la propriété C#
  correspondante (réutiliser les tests de saisie existants).

---

# Partie C — Boutons globaux des pages listes : retour à une seule ligne

## 30.7. `ImportProfiles.razor` / `ExportProfiles.razor` — boutons côte à côte sur une seule ligne (réouverture explicite de V4/X2)

**Ce ticket rouvre explicitement une décision actée précédemment** : le Lot V (V4) avait empilé
verticalement les boutons globaux ("Tester un profil"/"Créer") en pleine largeur sur mobile
(`d-grid gap-2 d-md-flex mb-3` + `w-100 mb-2`), et le Lot X (X2) avait corrigé un bug d'application
partielle de ce même empilement. Simon demande aujourd'hui (24/07, capture à l'appui) l'inverse :
les deux boutons doivent rester **sur une seule ligne, côte à côte**, y compris sur mobile — ce
n'est pas un bug résiduel de V4/X2, c'est un changement de décision produit assumé, qui remplace
leur comportement mobile sans rouvrir le reste de ces deux tickets (couleurs, IDs, navigation).

**Comportement attendu** :
- Le conteneur `right-aligned-actions`/`d-grid gap-2 d-md-flex mb-3` est remplacé par un
  conteneur flex qui garde les deux boutons sur une seule ligne à **tous** les breakpoints
  (`d-flex gap-2 mb-3`, sans `d-grid`).
- Chaque bouton perd sa classe `w-100`/`mb-2` individuelle et reçoit à la place une classe qui lui
  fait partager équitablement la largeur disponible avec l'autre bouton (`flex-fill` ou
  équivalent `flex-grow-1`), pour rester utilisable au pouce sans redevenir une largeur naturelle
  minuscule comme avant V4.
- Si le texte d'un bouton ("Tester un profil d'export"/"Tester un profil d'import") ne tient pas
  sur une ligne à la largeur résultante, le texte du bouton peut s'enrouler sur deux lignes **à
  l'intérieur du bouton** (comportement natif d'un `<button>` Bootstrap, aucun CSS
  supplémentaire nécessaire) — ce qui est explicitement acceptable et distinct du problème
  d'origine (qui était deux boutons sur deux lignes, pas un bouton sur deux lignes de texte).
- Application symétrique aux deux pages `ImportProfiles.razor`
  (`#test-import-profile-button`/`#create-profile-button`) et `ExportProfiles.razor`
  (`#test-export-profile-button`/`#create-export-profile-button`), par souci de parité explicite
  demandé par Simon — pas seulement l'export illustré en capture.
- Aucun changement des `id` existants, aucun changement de la logique de navigation/déclenchement.

**Tests** (bUnit) :
- Le conteneur commun des deux boutons ne porte plus `d-grid` (test sur absence de classe).
- Les deux boutons ne portent plus `w-100` (test sur absence de classe), et portent bien la
  nouvelle classe de partage de largeur (`flex-fill` ou équivalent retenu).
- Test de structure : les deux boutons restent bien enfants directs du même conteneur flex (pas
  de wrapper intermédiaire qui les sépare en deux lignes).
- Test de parité explicite entre `ImportProfiles.razor` et `ExportProfiles.razor` : mêmes classes
  sur les conteneurs et sur les boutons respectifs des deux pages (comparaison de chaîne, même
  pattern que R1/30.5).
- Non-régression : navigation et comportement de clic inchangés (réutiliser les tests
  fonctionnels existants de V4/X2/S1 sans les dupliquer).

---

# Hors périmètre explicite (tout le lot)

- Toute modification de la logique métier des pipelines import/export (Domain/Application
  inchangés).
- Toute introduction de bibliothèque UI supplémentaire (pas de MudBlazor, pas de framework CSS
  concurrent de Bootstrap).
- Le mécanisme `SectionOutlet`/`SectionContent` du bandeau global (X10/X11) — non concerné par ce
  lot, ne pas y toucher.
- La densification desktop en grille des cartes de règles de feuille (Lot R) — ce lot ne touche
  pas `.sheet-rule-card`, seulement les formulaires de saisie et les boutons globaux de liste.
- Le composant d'upload de fichier personnalisé masquant le texte natif (X11/V10, reporté).
- Toute décision sur le format définitif du fichier Excel cible ou sur `TacheMultiples` (hors
  périmètre UI, en attente de confirmation client par ailleurs).
- Migration de données ou changement de schéma — ce lot est strictement CSS/structure Razor.

---

# Note d'efficacité d'implémentation

1. **Traiter 30.0 intégralement en premier** — la cartographie exacte de l'état réel de X3-X5/Y3
   côté export et de la structure actuelle de l'import conditionne toute la Partie A ; le `grep
   form-floating` conditionne toute la Partie B.
2. **Partie A avant Partie B (30.6)** : convertir d'abord `ImportProfileEditor.razor` en
   `form-floating` (30.2) avant d'auditer *tous* les champs `form-floating` de l'application
   (30.6), pour ne pas auditer deux fois (une fois avant conversion, une fois après).
3. **30.5 (test de parité) en dernier de la Partie A** : il a besoin que 30.1-30.4 soient
   terminés des deux côtés pour comparer un état stable.
4. **Partie C (30.7) est indépendante** des Parties A et B (fichiers différents,
   `ImportProfiles.razor`/`ExportProfiles.razor` vs `*ProfileEditor.razor`) — peut être traitée en
   parallèle ou dans n'importe quel ordre relatif.
5. Un seul passage de lecture de `ExportProfileEditor.razor` (30.0) suffit pour servir de
   référence à toute la Partie A — ne pas le relire à chaque sous-ticket.

---

# Ordre recommandé

1. 30.0 (investigation, y compris `grep form-floating` global et état réel de X3-X5/Y3)
2. 30.1 → 30.2 → 30.3 → 30.4 (extension séquentielle du pattern export vers l'import)
3. 30.5 (test de parité structurelle, une fois 30.1-30.4 stabilisés)
4. 30.6 (audit et correctif généralisé `form-floating`, après 30.2)
5. 30.7 (boutons sur une ligne — indépendant, à tout moment)
