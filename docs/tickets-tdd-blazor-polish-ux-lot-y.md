# Tickets TDD — Lot Y : corrections post-revue mobile (bandeau titre/hamburger, form-floating, CTA enregistrer, débordement édition)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Fait suite à
une nouvelle série de captures d'écran client (mobile), postérieure au Lot X
(`tickets-tdd-blazor-polish-ux-lot-x.md`). Numéroté **Lot Y** pour ne pas entrer en collision avec
le Lot X, qui reste le document de référence pour X0-X11 — ce document ne les modifie pas et ne
les rouvre pas.*

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`
(boutons d'action alignés à droite en desktop), `convention-ui-blazor-icones-boutons.md` ; IDs
HTML stables, jamais de sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions
7.x + Moq + bUnit ; Bootstrap déjà en usage (aucune nouvelle dépendance CSS/JS). bUnit ne calcule
pas de layout réel : tous les tests de ce lot portent sur la présence de classes/structure DOM, pas
sur un rendu pixel.

**Point d'attention transverse — dépendances au Lot X non encore confirmées livrées** : deux
tickets de ce lot (Y1, Y4) corrigent des symptômes visibles **après** l'application de correctifs
prévus par le Lot X (respectivement X11 et X6). Chaque ticket concerné démarre donc par une
vérification explicite en Y0 de l'état réel du code avant toute modification — ne pas supposer que
X6/X11 sont fusionnés seulement parce que les captures d'écran client semblent le montrer.

---

## Y0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `MainLayout.razor`/`NavMenu.razor`/leurs `.razor.css` : confirmer si X11 (bouton retour
  dans le bandeau rouge global) est déjà fusionné sur `main`. Si oui, localiser la structure exacte
  du `top-row` (flèche retour, lien `navbar-brand` "Alpha - MAD / REL OXO", bouton
  `navbar-toggler`/hamburger) et les classes Bootstrap/CSS scopé actuellement appliquées à chacun.
  Si X11 n'est pas encore fusionné, documenter ce fait et traiter Y1 comme un correctif à appliquer
  **en même temps** que X11 plutôt qu'après (voir Y1).
- [ ] Lire `ExportProfileEditor.razor`, section "Colonnes Points" (champ "Nom de la colonne") :
  confirmer si X4 (généralisation `form-floating`) est déjà fusionné. Inspecter la structure HTML
  exacte de ce champ précis (ordre `input`/`label`, présence et valeur de l'attribut `placeholder`,
  classe `form-control` vs `form-select`) pour isoler la violation structurelle responsable du
  chevauchement label/texte saisi.
- [ ] Lire le bouton `#save-profile-button` (ou équivalent) de `ExportProfileEditor.razor` :
  confirmer ses classes actuelles et si un traitement `w-100`/`btn-lg` façon V12 (Lot V,
  `tickets-tdd-blazor-mobile-first-lot-v.md`) lui a déjà été appliqué ailleurs par erreur de
  périmètre (X5 exclut explicitement ce bouton — vérifier qu'aucun ticket X ne l'a couvert malgré
  cette exclusion).
- [ ] Lire `ExportProfileEditor.razor` en mode édition (route `/export-profiles/{id}/edit`) :
  confirmer si X6 (conteneur `container`/`container-fluid px-3`) est déjà fusionné. Si oui,
  identifier l'élément réellement responsable du débordement horizontal résiduel (recherche d'un
  `row` Bootstrap sans `col` englobant qui hérite de ses marges négatives, d'un texte long non
  contraint — nom de règle de feuille, nom de colonne —, ou d'un tableau/`pre` sans wrapper
  `overflow-x`) plutôt que de réappliquer X6 tel quel.
- [ ] Confirmer que la suite de tests bUnit existante (`NavMenuTests`, `ExportProfileEditorTests`)
  passe avant toute modification (baseline verte).

---

## Y1. Collision entre le titre de marque et le bouton hamburger dans le bandeau rouge (mobile)

**Constat client** : sur mobile, le texte de marque "Alpha - MAD / REL OXO" déborde par-dessus le
bouton hamburger à droite du bandeau rouge. Cause probable : l'ajout de la flèche retour à gauche
(X11) réduit l'espace disponible pour le texte de marque, qui ne se contracte pas.

**Dépendance** : si X11 n'est pas encore fusionné (voir Y0), ce correctif est appliqué **dans le
même commit/PR que X11** plutôt qu'en correctif séparé après coup, pour ne pas livrer une régression
connue même temporairement. Si X11 est déjà fusionné, ce ticket est un correctif indépendant
classique.

**Comportement attendu** :
- Le conteneur du `top-row` (flèche retour éventuelle + texte de marque + bouton hamburger) reste
  un conteneur flex sur toute sa largeur ; le lien de marque (`navbar-brand`) reçoit `text-truncate`
  (troncature avec points de suspension) plus `flex-grow-1` pour absorber l'espace restant, et
  `min-width: 0` — via une classe utilitaire Bootstrap si disponible dans la version du projet,
  sinon un ajustement minimal dans le `.razor.css` scopé déjà existant du composant, **jamais** une
  nouvelle feuille de style globale (l'absence de `min-width: 0` sur un enfant flex est la cause
  technique classique empêchant `text-truncate` de fonctionner dans un conteneur flex).
- La flèche retour (si présente) et le bouton hamburger reçoivent chacun `flex-shrink-0` pour ne
  jamais être compressés ou poussés hors champ, quelle que soit la longueur du texte de marque.
- Aucun changement du texte de marque lui-même (pas de raccourci de "Alpha - MAD / REL OXO") — la
  troncature visuelle avec ellipse est jugée suffisante par ce ticket ; un texte plus court est un
  sujet distinct (contenu éditorial, pas mise en page) à ne pas trancher ici sans validation client.

**Tests** (bUnit) :
- Le lien `navbar-brand` porte bien `text-truncate` (test sur `class`).
- La flèche retour (si le composant est rendu avec `SectionContent` actif dans le test) et le
  bouton hamburger portent bien `flex-shrink-0` (test sur `class` de chacun).
- Non-régression : le lien de marque garde son `href`/route de destination actuelle, le bouton
  hamburger garde son comportement d'ouverture/fermeture de la sidebar (réutiliser les tests
  fonctionnels `NavMenuTests` existants sans les dupliquer).
- Test de structure : les trois éléments (flèche éventuelle, marque, hamburger) restent bien
  enfants directs du même conteneur `top-row` après ce correctif (garde-fou contre une
  restructuration DOM accidentelle qui casserait X11).

---

## Y2. `form-floating` cassé sur le champ "Nom de la colonne" (`/export-profiles/new`)

**Constat client** : le label "Nom de la colonne" se superpose au texte déjà saisi/au placeholder
dans le champ, sur la page de création d'un profil d'export.

**Rappel de la contrainte technique `form-floating` (Bootstrap 5)**, à vérifier précisément en Y0
sur ce champ : l'élément `input`/`select` doit apparaître **avant** son `<label for="...">` dans le
DOM (jamais l'inverse), et l'attribut `placeholder` de l'input doit être présent et non vide (a
minima un espace `" "` si aucun texte de substitution n'est voulu) — le positionnement du label
flottant repose sur le pseudo-sélecteur CSS `:placeholder-shown`, qui ne se déclenche pas sans un
`placeholder` défini.

**Comportement attendu** :
- Le champ "Nom de la colonne" respecte exactement la même structure `form-floating` que les
  champs déjà conformes de cette même page (voir X4) et que le pattern validé côté client sur "Mon
  Profil" (Lot V6) : `input` puis `label`, `placeholder` non vide sur l'`input`, `id`/`for`
  cohérents.
- Aucun changement de la liaison `@bind` ni du nom du champ métier — uniquement la structure
  HTML/l'attribut `placeholder`.

**Tests** (bUnit) :
- Le champ "Nom de la colonne" est bien enveloppé dans un conteneur `form-floating`, avec l'`input`
  précédant le `<label>` dans l'arbre DOM rendu (test d'ordre des nœuds, pas seulement de présence).
- L'`input` porte un attribut `placeholder` non vide (test explicite sur la valeur de l'attribut,
  pas seulement sur sa présence — une chaîne vide `""` est un attribut présent mais invalide pour
  ce mécanisme).
- Non-régression : la saisie dans ce champ reste bien liée à la propriété C# correspondante
  (réutiliser le test de saisie existant de `PointColumnDefinition` s'il existe, sinon en écrire un
  minimal).
- Si l'investigation Y0 révèle que d'autres champs de la page partagent la même violation
  structurelle (au-delà de "Nom de la colonne" seul) : étendre le correctif et le test à tous les
  champs concernés dans ce même ticket plutôt que d'ouvrir un ticket par champ.

---

## Y3. Bouton "Enregistrer le profil" — CTA final pleine largeur mobile-first

**Constat client** : le bouton final d'enregistrement du profil d'export reste de taille naturelle
et aligné à droite sur mobile, ce qui le rend peu visible et difficile à atteindre au pouce.

**Périmètre distinct de X5** : X5 traite exclusivement les boutons d'ajout intermédiaires ("Ajouter
une règle de feuille"/"Ajouter une colonne"/"Ajouter une colonne Points"), pas le bouton
d'enregistrement final — ce dernier reste hors périmètre de X5 par exclusion explicite. Ce ticket
comble ce point, réutilisant exactement le pattern déjà établi par **V12**
(`tickets-tdd-blazor-mobile-first-lot-v.md`) plutôt que d'en inventer un nouveau.

**Comportement attendu** (mobile-first, cohérent avec V12) :
- `#save-profile-button` reçoit `w-100 btn-lg` sur mobile, avec un retour à une largeur naturelle
  sur écran large via la même classe de breakpoint que celle retenue en V12 (`w-md-auto` ou
  équivalent déjà tranché à cette occasion — à réutiliser telle quelle, pas de nouvelle décision).
- Marge verticale généreuse pour le détacher visuellement du dernier bouton d'ajout intermédiaire
  au-dessus (`mt-4`) et du bord inférieur de l'écran (`mb-4`).
- Sur desktop (au-delà du breakpoint), l'alignement à droite déjà en place
  (`convention-ui-blazor-alignement-boutons.md`) est conservé sans modification — ce ticket ne
  touche que la largeur/les marges, pas l'alignement du conteneur parent.
- Aucun autre bouton de la page (boutons d'ajout intermédiaire de X5, bouton retour) n'est concerné
  par ce ticket.

**Tests** (bUnit) :
- `#save-profile-button` porte bien `w-100 btn-lg mt-4 mb-4` (+ la classe de breakpoint retenue en
  V12) — test sur `class`, sans modifier son `id`.
- Non-régression : le clic déclenche toujours la même logique d'enregistrement (réutiliser le test
  fonctionnel existant sans le dupliquer).
- Test de contraste explicite avec X5 : `#save-profile-button` ne porte jamais la classe
  `btn-outline-*` réservée aux boutons d'ajout intermédiaire (garde-fou de non-confusion entre les
  deux catégories de boutons, même esprit que le test de contraste déjà écrit en X5).

---

## Y4. Débordement horizontal résiduel en édition (`/export-profiles/{id}/edit`)

**Constat client** : en édition, le contenu apparaît décalé — texte tronqué à gauche ("odifier",
"arents", "nfants" au lieu de "Modifier"/"Parents"/"Enfants") et bouton de suppression rouge coupé
à droite ("Suppr...").

**Diagnostic à confirmer en Y0, pas à supposer** : ce symptôme (texte tronqué des deux côtés
simultanément, pas seulement un manque de padding) est cohérent avec un **débordement horizontal
de la page** (un élément plus large que le viewport force un défilement horizontal, et le
navigateur mobile affiche la page déjà décalée) plutôt qu'avec la seule absence de conteneur
`container`/`container-fluid px-3` déjà corrigée par X6. Si X6 est déjà fusionné et que ce
symptôme persiste, la cause probable est un enfant spécifique plus large que son parent — candidat
le plus probable : un `row` Bootstrap sans `col-*` englobant (le `row` porte nativement des marges
négatives de compensation qui débordent si aucun `col` ne les absorbe), un nom de règle de feuille
ou de colonne long non contraint (`text-truncate`/`word-break` manquant), ou un tableau/bloc de
code sans wrapper `overflow-x-auto`. Y0 doit identifier lequel avant correctif.

**Comportement attendu** :
- L'élément identifié en Y0 comme responsable du débordement est contraint pour ne jamais excéder
  la largeur du viewport (ajout du `col` manquant, `text-truncate`/`word-break: break-word` sur le
  texte long, ou wrapper `overflow-x-auto` selon le cas réel constaté).
- Après correctif, la page ne déclenche plus de défilement horizontal sur mobile : le contenu
  (titres "Modifier le profil d'export", noms de règles "Parents"/"Enfants", boutons d'action) est
  visible intégralement sans défilement latéral.
- Ce correctif n'entre pas en conflit avec X6 : si X6 est déjà en place, ce ticket est un correctif
  additionnel sur la cause réelle distincte, pas une réapplication de X6.

**Tests** (bUnit) :
- bUnit ne mesurant pas de layout réel, le test porte sur la présence de la classe corrective
  identifiée en Y0 sur l'élément concerné (`col-*` ajouté, `text-truncate`/`word-break` sur le nom
  de règle, ou `overflow-x-auto` sur le wrapper) — test structurel, pas de rendu pixel.
- Test de non-régression sur le contenu textuel réel (nom de règle, libellés de boutons) : le texte
  complet doit toujours être présent dans le DOM même si visuellement tronqué par CSS (`text-
  truncate` ne retire rien du DOM, seul l'affichage change) — vérifier que les assertions de texte
  existantes de `ExportProfileEditorTests` restent valides sans modification.
- Non-régression sur les IDs et le comportement des boutons `Modifier`/`Supprimer` déjà couverts
  par X7 (icônes seules) — ce ticket ne touche pas leur contenu, seulement le conteneur englobant
  ou le texte adjacent qui déborde.

---

# Hors périmètre explicite (tout le lot)

- Raccourcissement du texte de marque "Alpha - MAD / REL OXO" lui-même — Y1 se limite à la
  troncature visuelle, pas à une décision éditoriale sur le contenu du texte.
- Toute modification de la logique métier des pipelines import/export (Lots D/I/T inchangés).
- Toute introduction de bibliothèque UI supplémentaire (pas de MudBlazor, pas de framework CSS
  concurrent de Bootstrap).
- `ImportProfileEditor.razor` : ce lot cible explicitement les pages touchées par les captures
  client (`ExportProfileEditor.razor`, bandeau global) — étendre à l'import n'est envisageable
  qu'après validation client explicite de la parité, pas par défaut (même principe que X hors
  périmètre).
- Réouverture de X10/X11 elles-mêmes (mécanisme `SectionOutlet`/`SectionContent`, portée de la
  migration) — Y1 corrige un effet de bord visuel, pas le mécanisme sous-jacent.

---

# Note d'efficacité d'implémentation

1. **Traiter Y0 en premier**, intégralement — l'état réel de X6/X11 (fusionnés ou non) change la
   manière de traiter Y1 et Y4 (correctif combiné vs. correctif indépendant).
2. **Y2 et Y3 peuvent être traités indépendamment de Y0/Y1/Y4** et l'un de l'autre : aucune
   dépendance de fichier ou de composant partagé avec le reste du lot (page/composant différents
   du bandeau global).
3. **Y1 avant Y4** si les deux dépendent d'un état encore non fusionné de Lot X (X11 avant X6 dans
   l'ordre de dépendance amont) — sinon, aucun ordre strict entre eux.
4. Un seul passage de lecture de `NavMenu.razor`/`MainLayout.razor` (Y0) suffit pour couvrir Y1.
   Un seul passage de lecture de `ExportProfileEditor.razor` (Y0) suffit pour couvrir Y2, Y3, Y4.

---

# Ordre recommandé

1. Y0 (investigation, y compris confirmation de l'état réel de X6/X11)
2. Y1 (bandeau titre/hamburger — combiné à X11 si non encore fusionné)
3. Y2 (form-floating "Nom de la colonne")
4. Y3 (CTA "Enregistrer le profil" pleine largeur)
5. Y4 (débordement horizontal en édition — combiné à X6 si non encore fusionné)
