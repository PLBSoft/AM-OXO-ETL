# Tickets TDD — Lot X : polish UX mobile (navbar, listes, vue succès, formulaires export)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à
une nouvelle revue client mobile du 24/07, postérieure au Lot V (V1→V13, mobile-first) et au
Lot W (dernier lot livré, `édition/suppression UnconditionalColonneNames/ConditionalPointRule`).
Ce lot est donc numéroté **Lot X** pour ne pas entrer en collision avec un lot existant.*

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`
(boutons d'action alignés à droite), `convention-ui-blazor-icones-boutons.md` (icônes Bootstrap
Icons, matrice de décision, accessibilité `aria-hidden`/`aria-label`) ; IDs HTML stables, jamais de
sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; Bootstrap
déjà en usage dans le projet (aucune nouvelle dépendance CSS/JS sauf mention explicite ci-dessous).

**Point d'attention transverse** : ce lot touche à la fois un composant de layout global
(`MainLayout.razor`/`NavMenu.razor`, X10-X11) et plusieurs pages spécifiques (`ExportProfiles.razor`,
`ExportProfileTest.razor`, `ExportProfileEditor.razor` en création et en édition). Traiter X10-X11 en
dernier malgré son numéro, car c'est le changement le plus structurant (voir Note d'efficacité) :
il touche un composant partagé par toutes les pages, donc tout changement doit être fait sur une
base de tests déjà stabilisée par le reste du lot.

**Hors périmètre de ce lot (reporté, à garder en mémoire)** : la demande d'un composant d'upload
de fichier personnalisé masquant le texte natif du navigateur (réouverture de la décision Lot
V10 sur `ImportProfileTest.razor`/`ExportProfileTest.razor`) est **volontairement retirée** de ce
lot à la demande de Simon (24/07) — à traiter dans un lot ultérieur séparé, pas dans ce document.

---

## X0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `MainLayout.razor` et `NavMenu.razor` : confirmer la structure exacte du bandeau rouge
  supérieur (probablement un `top-row` Blazor standard contenant le lien `navbar-brand`
  `"Alpha - MAD / REL OXO"`, voir Lot S3) — est-ce un élément unique partagé par toutes les pages,
  ou dupliqué par layout ? Confirmer qu'il n'existe qu'un seul point de modification.
- [ ] Lire le bandeau de page introduit par le Lot V8 (`ImportProfileTest.razor`/
  `ExportProfileTest.razor`) : structure CSS exacte, classe dédiée, `id` du lien retour existant.
  Confirmer si ce même bandeau de page existe aussi sur `ExportProfileEditor.razor` (créé/édité —
  `back-to-export-profiles-button`, ajouté le 22/07 selon `etat-avancement-global-2026-07-22.md`
  §5) : structure probablement différente de celle du Lot V8, à vérifier avant d'unifier.
- [ ] Lire le markup actuel de `ExportProfiles.razor` (V4) pour comprendre pourquoi les boutons
  `#create-export-profile-button`/`#test-export-profile-button` s'arrêtent au milieu de l'écran
  sur mobile malgré l'empilement déjà livré par V4 — probablement `w-100` manquant ou mal propagé
  sur le conteneur plutôt que sur les boutons eux-mêmes.
- [ ] Lire `ExportProfileTest.razor` (J3) pour localiser le bouton de téléchargement du fichier
  généré dans la "vue Succès" (zone résultat en carte, V13) et ses classes actuelles.
- [ ] Lire `ExportProfileEditor.razor` pour les deux routes (`/export-profiles/new` et
  `/export-profiles/{id}/edit`) : confirmer la présence ou l'absence d'un conteneur Bootstrap
  racine (`container`/`container-fluid`), les classes de grille actuelles (`col-md-6` ou
  équivalent) sur les champs de `SheetGenerationRule`/`ColumnDefinition`/`PointColumnDefinition`,
  et la structure des sous-formulaires ("Ajouter une règle de feuille", "Ajouter une colonne",
  "Ajouter une colonne Points").
- [ ] Confirmer que les tests bUnit existants (`ExportProfilesTests`, `ExportProfileTestTests`,
  `ExportProfileEditorTests`, `NavMenuTests`) passent avant toute modification (baseline verte).

---

# Partie A — Corrections ponctuelles (listes, vue succès)

## X1. Vue Succès (`ExportProfileTest.razor`) — bouton Télécharger en vert, pleine largeur

**Constat client** : le bouton de téléchargement du fichier généré reste marron (`btn-warning`
ou équivalent) et de largeur naturelle, alors qu'il marque la fin réussie du processus.

**Comportement attendu** :
- Le bouton de téléchargement passe en `btn-success w-100 btn-lg` (couleur verte, pleine largeur,
  grande cible tactile — cohérent avec V11/V12 déjà en place sur cette page pour les autres
  boutons).
- Aucun changement de comportement (déclenchement du téléchargement inchangé), seul le style
  change.

**Tests** (bUnit) :
- Le bouton de téléchargement porte bien les classes `btn-success w-100 btn-lg` (test sur
  `class`), sans changer son `id` existant.
- Non-régression : le clic déclenche toujours le même mécanisme de téléchargement qu'aujourd'hui
  (réutiliser le test fonctionnel existant si présent).

---

## X2. `ExportProfiles.razor` — correction du gap de largeur des boutons globaux

**Constat client** : sur mobile, `#create-export-profile-button`/`#test-export-profile-button`
sont bien empilés (héritage V4) mais s'arrêtent au milieu de l'écran au lieu d'occuper la pleine
largeur — signe que `w-100` n'a pas été appliqué (ou l'a été sur un conteneur qui ne contraint pas
la largeur réelle du bouton).

**Comportement attendu** :
- Les deux boutons portent explicitement `w-100` chacun (pas seulement leur conteneur), en plus
  de `mb-2` sur le premier (`#create-export-profile-button` ou `#test-export-profile-button`
  selon l'ordre d'affichage réel — à confirmer en X0), sous le breakpoint déjà utilisé par V4.
- Vérifier si `ImportProfiles.razor` présente la même régression (V4 ne le précisait pas
  explicitement pour l'écran import) ; si oui, appliquer le même correctif par souci de parité —
  sinon, ne pas modifier `ImportProfiles.razor` sans confirmation que le bug y est aussi présent.

**Tests** (bUnit) :
- Les deux boutons portent individuellement la classe `w-100` (test sur l'attribut `class` de
  chaque bouton, pas seulement de leur conteneur commun).
- Non-régression sur les IDs et la navigation déjà couverts par V4/S1.
- Si le correctif est étendu à `ImportProfiles.razor` : même assertion, avec un test de parité
  explicite comparant les classes des deux paires de boutons (cohérent avec le pattern de test de
  parité déjà utilisé en R1).

---

# Partie B — `/export-profiles/new` : mobile-first (empilement, sous-formulaires, boutons)

*Périmètre : `ExportProfileEditor.razor`, route `/export-profiles/new` (et par construction,
`/export-profiles/{id}/edit` puisque c'est le même composant — voir Partie C pour les points
spécifiques à l'édition qui s'ajoutent par-dessus cette base commune).*

## X3. Empilement vertical des champs racine et des sous-formulaires

**Comportement attendu** :
- "Nom de la feuille" et "Source pivot" : suppression de toute classe `col-md-*`/grille à deux
  colonnes, passage en `col-12`/pleine largeur avec `mb-3`, empilés l'un au-dessus de l'autre.
- Section "Colonnes" (`ColumnDefinition`) : même traitement — "En-tête" en pleine largeur,
  "Champ source" en dessous, `mb-3` entre les deux.
- Section "Colonnes Points" (`PointColumnDefinition`) : "Nom de la colonne", "En-tête", "Valeur de
  marquage" empilés verticalement (3 champs, chacun `col-12`/`mb-3`), au lieu de la grille à 3
  colonnes actuelle.
- Ce changement est **mobile-first sans média-query manuelle** : les classes Bootstrap
  responsives existantes (`col-12 col-md-6` par ex.) sont retirées ou remplacées, pas surchargées
  par du CSS scopé — cohérent avec le principe déjà appliqué en Lot V ("privilégier les classes
  utilitaires Bootstrap natives").

**Tests** (bUnit) :
- Les conteneurs des champs "Nom de la feuille"/"Source pivot" portent `col-12`/`mb-3`, plus
  aucune classe `col-md-6` (test sur absence de classe, pas seulement présence de la nouvelle).
- Même vérification pour les champs de "Colonnes" (`En-tête`/`Champ source`).
- Même vérification pour les 3 champs de "Colonnes Points".
- Non-régression fonctionnelle complète : tous les tests existants de `ExportProfileEditorTests`
  sur la saisie/l'ajout d'une règle de feuille/colonne restent verts sans modification de leur
  intention (seule la classe CSS du conteneur change, pas la structure des `id`/`name` des
  champs).

---

## X4. Hiérarchie visuelle des sous-formulaires (`bg-light`) et étiquettes flottantes (`form-floating`)

**Comportement attendu** :
- Les zones "Colonnes" et "Colonnes Points" (sous-formulaires imbriqués dans le sous-formulaire
  "Ajouter une règle de feuille") reçoivent un fond légèrement grisé (`bg-light`), en plus de leur
  `card` existante, pour créer un effet de boîte dans la boîte.
- Tous les champs de saisie de `ExportProfileEditor.razor` (racine et sous-formulaires) utilisent
  les étiquettes flottantes Bootstrap (`form-floating`), à l'identique du pattern déjà en place et
  validé côté client sur la page "Mon Profil" (V6) — réutiliser exactement la même structure
  `form-floating` (label après l'input, `id`/`for` cohérents), pas une variante.
- Objectif explicite (à ne pas perdre en implémentation) : le gain d'espace vertical de
  `form-floating` compense l'empilement en une seule colonne de X3 — les deux tickets sont
  complémentaires, pas indépendants.

**Tests** (bUnit) :
- Les conteneurs "Colonnes"/"Colonnes Points" portent bien la classe `bg-light` en plus de `card`
  (test sur `class`).
- Un échantillon de champs (au moins "Nom de la feuille" et un champ de "Colonnes") est bien
  enveloppé dans un conteneur `form-floating`, avec un `<label for="...">` dont l'attribut `for`
  correspond à l'`id` de l'input (test de cohérence `id`/`for`, pas juste présence de la classe).
- Non-régression : la liaison Blazor (`@bind`) de chaque champ reste fonctionnelle après passage
  en `form-floating` (réutiliser les tests de saisie existants).

---

## X5. Boutons d'action — pleine largeur, `mt-3`, et hiérarchie par contour

**Comportement attendu** :
- Les boutons "Ajouter la colonne" (et équivalents "Ajouter une règle de feuille"/"Ajouter une
  colonne Points" s'ils existent sous une forme similaire) passent en pleine largeur (`w-100`)
  avec une marge supérieure (`mt-3`).
- Ces boutons d'ajout intermédiaire utilisent un style contour (`btn-outline-secondary` ou
  `btn-outline-primary` — à trancher en X0 selon la palette déjà utilisée ailleurs dans le
  projet pour des actions non destructives et non finales) plutôt qu'un style plein, pour ne pas
  entrer en compétition visuelle avec le bouton d'enregistrement final du profil (`#save-profile-
  button` ou équivalent), qui reste seul à porter une couleur pleine (rouge ou vert selon la
  convention déjà en place).
- Le bouton retour (`back-to-export-profiles-button`) n'est **pas** concerné par ce style
  contour ici — il est traité séparément par X6/X7 (déplacement dans le bandeau, pas simple
  restylisation sur place).

**Tests** (bUnit) :
- Les boutons d'ajout intermédiaire portent `w-100 mt-3` (test sur `class`).
- Les boutons d'ajout intermédiaire portent une classe `btn-outline-*` (et non plus une classe
  `btn-primary`/`btn-danger` pleine), tandis que le bouton d'enregistrement final conserve sa
  classe pleine actuelle — test de contraste explicite entre les deux catégories de boutons pour
  garantir qu'elles ne portent jamais la même classe de couleur pleine simultanément.
- Non-régression : clic sur chaque bouton d'ajout déclenche toujours la même logique d'ajout à la
  collection en mémoire (règle de feuille/colonne/colonne Points), sans navigation ni soumission
  prématurée du formulaire racine.

---

# Partie C — `/export-profiles/{id}/edit` : débordement, actions de liste, sémantique des couleurs

*Périmètre additionnel sur le même composant que la Partie B, spécifique à ce qui est visible en
mode édition (liste des règles déjà configurées) plutôt qu'à la saisie d'une nouvelle règle.*

## X6. Conteneur racine et marges — correction du débordement (overflow)

**Constat client** : en édition, le texte touche le bord gauche de l'écran et les boutons rouges
d'action sont coupés à droite ("Suppr...").

**Comportement attendu** :
- Le contenu principal de `ExportProfileEditor.razor` (les deux routes, `new` et `edit`, puisque
  c'est le même composant) est enveloppé dans un `<div class="container-fluid px-3">` (ou
  `container` si la cohérence avec le reste du projet le préfère — à trancher en X0 selon ce qui
  est déjà utilisé sur `ImportProfileEditor.razor`/`ExportProfileEditor.razor`, pour ne pas
  introduire un deuxième pattern de conteneur dans le projet).
- Ce correctif s'applique à l'ensemble du composant, pas seulement à la liste des règles déjà
  configurées — il corrige la cause racine (absence de padding), pas seulement le symptôme visible
  sur la capture d'écran d'édition.

**Tests** (bUnit) :
- Le conteneur racine du composant porte bien la classe de conteneur attendue (`container` ou
  `container-fluid px-3` selon ce qui est tranché en X0) — test sur `class` du conteneur englobant
  le formulaire.
- Non-régression : aucun changement de structure des champs à l'intérieur, seul le conteneur
  englobant change.

---

## X7. Actions de ligne "Modifier"/"Supprimer" — icônes seules

**Constat client** : deux boutons texte côte à côte par ligne de règle provoquent le débordement
à droite sur mobile.

**Comportement attendu** :
- Remplacer le texte des boutons `Modifier`/`Supprimer` de chaque règle de feuille déjà
  configurée par des icônes seules Bootstrap Icons (`bi-pencil`/`bi-trash`), conformément à
  `convention-ui-blazor-icones-boutons.md` (ligne de grille/tableau → icône seule ou icône +
  libellé court) — même pattern que V3 (actions de ligne des listes `ImportProfiles.razor`/
  `ExportProfiles.razor`), appliqué ici aux actions de ligne des règles de feuille à l'intérieur
  de l'éditeur.
- `aria-label`/`title` explicites obligatoires sur chaque bouton icône seule (règle A11Y déjà
  actée dans cette convention), IDs existants conservés.

**Tests** (bUnit) :
- Les boutons `Modifier`/`Supprimer` de chaque règle de feuille contiennent désormais
  `<span class="bi bi-pencil" aria-hidden="true">`/`<span class="bi bi-trash" aria-hidden="true">`
  respectivement, sans texte visible, avec `aria-label`/`title` explicites.
- Comportement fonctionnel inchangé (clic déclenche toujours modification/suppression de la même
  règle) — réutiliser les tests fonctionnels existants sans les dupliquer.

---

## X8. Sémantique des couleurs — métadonnées neutres en `text-muted`

**Constat client** : les métadonnées ("4 colonnes, 2 colonnes de points") sont actuellement en
rouge, alors que le rouge est réservé aux erreurs/alertes/actions destructrices dans le reste du
projet.

**Comportement attendu** :
- Le texte de métadonnées par règle de feuille (nombre de colonnes, nombre de colonnes Points, ou
  équivalent) passe en `text-muted`, retire toute classe de couleur rouge/`text-danger`.

**Tests** (bUnit) :
- Le conteneur du texte de métadonnées porte `text-muted` et ne porte plus de classe
  `text-danger`/équivalent rouge (test sur `class`).
- Non-régression : le contenu textuel (nombre réel de colonnes/colonnes Points) reste identique
  et correctement interpolé (garde-fou contre une régression du type de celle corrigée en V1).

---

## X9. Règles de feuille en cartes (`card`)

**Comportement attendu** :
- Chaque règle de feuille déjà configurée (actuellement séparée par un simple séparateur/ligne)
  est enveloppée dans `<div class="card mb-3 shadow-sm">`, à l'identique du pattern `.sheet-rule-
  card` déjà utilisé sur cette même page en desktop (Lot R) — vérifier en X0 si cette carte existe
  déjà (probable, voir R1/R2) et si la demande porte en réalité sur un affichage encore non migré
  vers ce pattern (ex. liste simple en mode édition, distincte de la grille de cartes R1 en
  création) ; ne pas dupliquer un composant carte déjà existant sous un nom différent.

**Tests** (bUnit) :
- Chaque règle de feuille rendue est bien un enfant direct d'un conteneur portant `card mb-3
  shadow-sm` (test sur structure/`class`, pas sur rendu visuel).
- Non-régression sur le nombre de règles rendues et leur contenu (réutiliser les assertions déjà
  existantes de `ExportProfileEditorTests`).
- Si l'investigation en X0 confirme que ce pattern existe déjà (Lot R) : ce ticket devient un
  simple test de non-régression documentant l'état déjà conforme, pas une nouvelle implémentation
  — ne pas introduire une deuxième classe de carte redondante avec `.sheet-rule-card`.

---

# Partie D — Décisions nécessitant une réouverture explicite (à valider avant implémentation)

## X10. Vérification de faisabilité bUnit — `SectionOutlet`/`SectionContent` (prérequis bloquant)

**Contexte** : le mécanisme retenu pour X11 (voir ci-dessous) repose sur `SectionContent`/
`SectionOutlet` (`Microsoft.AspNetCore.Components.Sections`, natif .NET 8+, même principe que
`<PageTitle>`/`<HeadOutlet>`) plutôt qu'un service maison ou un `CascadingValue<MainLayout>`
custom — décision actée avec Simon le 24/07, au détriment d'un service scoped (plus de code à
maintenir pour un besoin déjà couvert nativement par le framework).

**Risque à couvrir avant tout le reste** : `SectionOutlet`/`SectionContent` s'appuient sur un
service (`SectionRegistry`) normalement enregistré automatiquement par l'hébergement Blazor réel
(Server/WebAssembly), mais **pas garanti enregistré par défaut dans un `TestContext` bUnit**. Ce
ticket est un prérequis **bloquant** : X11 ne démarre pas tant que ce ticket n'est pas vert.

**Comportement attendu** :
- Écrire un test bUnit minimal, isolé de tout le reste du lot : un composant hôte factice
  (représentatif de la structure réelle de `MainLayout.razor`, pas un composant vide simplifié à
  l'excès) contenant un `<SectionOutlet SectionName="test-section" />`, et un composant enfant
  rendu à l'intérieur contenant un `<SectionContent SectionName="test-section">Contenu de
  test</SectionContent>`.
- Vérifier que le contenu du `<SectionContent>` apparaît bien dans le DOM rendu, à l'endroit du
  `<SectionOutlet>` — sans ajout manuel de service dans un premier essai, pour observer le
  comportement par défaut du `TestContext` bUnit du projet.
- **Si le test échoue** (service `SectionRegistry` absent du `TestContext`) : ajouter
  explicitement le service nécessaire au `TestContext.Services` (voir documentation bUnit/
  `Microsoft.AspNetCore.Components.Sections` pour le type exact du service à enregistrer) et
  refaire passer le test avec cet ajout. Documenter cet ajout dans un endroit centralisé du
  projet de tests (ex. une classe de base `TestContext` partagée, ou un commentaire dans le
  fichier de test) pour que les futurs tests de `MainLayout`/pages n'aient pas à redécouvrir ce
  besoin.
- **Si le test échoue de façon non triviale** malgré cet ajout (incompatibilité bUnit/Sections
  difficile à contourner) : **arrêter X10/X11 et remonter le blocage à Simon** avant de basculer
  vers une solution de repli (service scoped maison avec événement `OnChange`) — ne pas décider
  seul du changement d'approche, ce point conditionne toute l'implémentation de X11.

**Tests** (bUnit) :
- Le test décrit ci-dessus constitue lui-même le livrable de ce ticket — il doit être vert avant
  de passer à X11.
- Test complémentaire : deux instances du même `<SectionOutlet>`/`<SectionContent>` rendues dans
  des tests bUnit différents (méthodes de test distinctes) ne se contaminent pas l'une l'autre
  (garde-fou contre un état partagé involontaire entre tests si le service est enregistré de
  façon trop globale).

**Definition of Done** : test(s) de ce ticket verts, approche de service de test documentée (ou
décision de repli actée avec Simon si le mécanisme natif s'avère impraticable en bUnit).

---

## X11. Bouton retour fusionné dans le bandeau rouge supérieur (`MainLayout`/`NavMenu`)

**Dépend de X10** (faisabilité bUnit validée) — ne pas commencer ce ticket avant que X10 soit vert.

**Décision à reconsidérer explicitement** : le Lot V8 a délibérément placé le lien retour dans un
**bandeau de page** dédié (fin, en haut du contenu de chaque page, pas dans le `top-row` global de
layout), pour rester un changement localisé par page sans toucher au composant de layout partagé.
La demande client actuelle va plus loin : elle souhaite que ce lien retour vive **directement
dans le bandeau rouge de premier niveau** (celui qui porte "Alpha - MAD / REL OXO", potentiellement
partagé par toutes les pages via `MainLayout.razor`/`NavMenu.razor`), à gauche du titre de marque,
en texte blanc pour contraster sur fond rouge — standard mobile iOS/Android déjà cité par le
client.

**Décisions actées avec Simon (24/07), remplaçant les points ouverts précédemment** :

- **Mécanisme retenu : `SectionContent`/`SectionOutlet`** — voir X10 pour la validation de
  faisabilité et la justification du choix face aux alternatives (service scoped maison,
  `RenderFragment` nommé).
- **Portée de la migration : un seul commit/PR**, pas de migration incrémentale — peu de pages
  sont concernées aujourd'hui (`ImportProfileTest.razor`/`ExportProfileTest.razor` du Lot V8,
  `ImportProfileEditor.razor`/`ExportProfileEditor.razor` du 22/07). Migrer les 4 pages en une
  seule passe plutôt que de laisser cohabiter temporairement bandeau de page (V8) et bandeau
  global (X11) sur des pages différentes.
- **Pages sans contexte de retour, confirmé** : les listes de premier niveau (`/import-profiles`,
  `/export-profiles`, `/logs`, `/users`, page "Mon Profil") n'affichent **aucune** flèche —
  seules les pages "enfants" (test, éditeur `new`/`edit`) en définissent une. Aucune de ces pages
  liste ne rend de `<SectionContent SectionName="page-back-nav">`, donc le `<SectionOutlet>` du
  layout reste vide sur ces pages (comportement natif de `SectionOutlet` sans contenu fourni : rien
  n'est rendu, pas de zone vide visible).

**Ce que cela implique concrètement** :
- Ce ticket **remplace** le comportement du Lot V8 pour les pages qu'il couvrait
  (`ImportProfileTest.razor`/`ExportProfileTest.razor`) et étend le même principe aux éditeurs
  (`back-to-import-profiles-button`/`back-to-export-profiles-button`, ajoutés le 22/07) — il ne
  s'agit pas d'un ajout supplémentaire à côté du bandeau de page V8, mais bien de son
  déplacement/absorption dans le bandeau global. **Ne pas garder les deux bandeaux simultanément
  sur une même page.**

**Comportement attendu** :
- Le lien retour apparaît dans le `top-row` rouge, à gauche du texte de marque, sous forme
  d'icône Bootstrap Icons (`bi-arrow-left`) en **texte blanc** (`text-white`, ou couleur héritée du
  `top-row` déjà rouge si le texte de marque "Alpha - MAD / REL OXO" est déjà blanc — à confirmer
  en X0 par lecture directe de `NavMenu.razor.css`/`MainLayout.razor.css` plutôt que supposé).
- Visible uniquement sur les 4 pages qui définissent un `<SectionContent SectionName="page-back-
  nav">` (voir ci-dessus) — rien sur les listes de premier niveau.
- Les `id` HTML existants des liens retour déjà présents (`back-to-import-profiles-button`,
  `back-to-export-profiles-button`, et ceux du Lot V8) sont conservés, seule leur position dans le
  DOM change (rendus désormais à l'intérieur du `<SectionOutlet>` du layout, plus dans le flux du
  contenu de page).
- Accessibilité : `aria-label` explicite par page, porté par le contenu de chaque
  `<SectionContent>` (le libellé reste propre à la page d'origine — "Retour à la liste des
  profils d'import" pour les pages liées à l'import, etc. — puisque c'est chaque page qui fournit
  son propre contenu, pas un texte générique fixé une fois dans le layout).

**Tests** (bUnit) :
- Sur une des 4 pages concernées (ex. `ImportProfileTest.razor`) : le lien retour apparaît bien
  dans le `top-row`/bandeau de layout (test de structure DOM : ancêtre commun avec le lien de
  marque, pas dans le flux du contenu de page), porte le bon `aria-label`, et navigue toujours
  vers la même route qu'avant ce ticket.
- Sur une page liste de premier niveau (ex. `ImportProfiles.razor`) : aucun lien retour n'apparaît
  dans le bandeau (absence réelle du DOM, pas un `display:none`).
- Non-régression : tous les tests existants qui ciblent les `id` de lien retour déjà en place
  (Lot V8, éditeurs du 22/07) continuent de passer après le déplacement — seule leur position
  DOM/parent change, pas leur `id` ni leur route cible.
- Test de non-collision : le bandeau rouge affiche à la fois le lien retour (si présent) et le
  texte de marque sans les faire se chevaucher ni tronquer l'un ou l'autre (test sur la présence
  simultanée des deux éléments dans le même conteneur, pas un test de rendu pixel).

**Hors périmètre de X11** : tout changement du contenu de la sidebar (`NavMenu.razor`) autre que
ce qui est strictement nécessaire pour accueillir le lien retour dans le bandeau partagé — l'ordre
des liens (Lot S2), les rôles d'autorisation, et le texte de marque (Lot S3) restent inchangés.
Le service scoped maison évoqué comme repli en X10 n'est **pas** implémenté préventivement —
seulement si le test de faisabilité `SectionOutlet`/bUnit de X10 échoue réellement.

---

# Hors périmètre explicite (tout le lot)

- Toute modification de la logique métier des pipelines import/export (Lots D/I/T inchangés).
- Toute introduction de bibliothèque UI supplémentaire (pas de MudBlazor, pas de framework CSS
  concurrent de Bootstrap).
- Glisser-déposer (drag & drop) et masquage du texte natif du navigateur pour l'upload de
  fichier — demande explicitement reportée hors de ce lot (voir en-tête du document).
- Réordonnancement des liens de la sidebar (Lot S2) ou des rôles d'autorisation (Lot L) — non
  concernés par le déplacement du bouton retour en X10-X11.
- Mode sombre/thématisation avancée — non demandé.
- `ImportProfileEditor.razor` pour les tickets de Partie B/C (X3-X9) : ce lot cible explicitement
  `ExportProfileEditor.razor` d'après la demande client ; étendre à l'import n'est envisageable
  qu'après validation client explicite de la parité (comme pour les Lots Q/R), pas par défaut.

---

# Note d'efficacité d'implémentation

1. **Traiter X0 en premier**, intégralement, avant tout code — plusieurs tickets de ce lot
   (X2, X9, X10) dépendent directement de constats d'investigation qui peuvent réduire leur
   périmètre réel (bug déjà partiellement corrigé, carte déjà existante, etc.).
2. **Partie A (X1, X2) en second** : corrections isolées, aucune dépendance avec le reste du lot,
   valident rapidement le cycle de build/test.
3. **Partie B (X3 → X5) avant Partie C (X6 → X9)** : X3/X4 restructurent le markup des champs de
   `ExportProfileEditor.razor` ; faire X6-X9 avant recréerait un conflit de merge/re-travail sur
   les mêmes zones de fichier.
4. **X10 puis X11, en dernier et dans cet ordre strict** : X10 (test de faisabilité) doit être
   vert avant que X11 (implémentation) ne commence — ce sont les deux seuls tickets de ce lot qui
   rouvrent une décision déjà actée (V8) et qui touchent un composant de layout partagé ; ils
   méritent leur propre revue avant merge, plutôt que d'être noyés dans un commit combiné avec le
   reste du lot.
5. Un seul passage de lecture complète de `ExportProfileEditor.razor` (X0) suffit pour couvrir
   X3, X4, X5, X6, X7, X8, X9 — ils modifient tous des zones du même fichier, pas besoin de
   relire entre chaque sous-ticket.

---

# Ordre recommandé

1. X0 (investigation)
2. X1, X2 (Partie A)
3. X3, X4, X5 (Partie B)
4. X6, X7, X8, X9 (Partie C)
5. X10 (vérification de faisabilité bUnit `SectionOutlet`/`SectionContent`, prérequis bloquant)
6. X11 (bouton retour dans le bandeau global — implémentation, une fois X10 vert)
