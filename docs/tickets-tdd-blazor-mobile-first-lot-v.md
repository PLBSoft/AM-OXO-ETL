# Tickets TDD — Lot V : mobile-first Blazor (listes, journaux, Mon Profil, pages de test de profil)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). **Fusion de
deux documents rédigés en parallèle sous le même nom de lot "Lot U"** (collision détectée le 23/07
avec le vrai Lot U — `tickets-tdd-pivot-tableaux-applications-export.md`, Domain/pivot Tableaux et
Applications, **non renommé, non touché par cette fusion**). Les deux documents fusionnés ici
couvraient des écrans différents mais partageaient la même thématique mobile-first ; ils sont
regroupés en un seul lot, renumérotés en V1–V13 pour éliminer toute ambiguïté de référence.*

**Ce document remplace** `tickets-tdd-blazor-mobile-first-listes-logs-profil.md` (Partie A) et
`tickets-tdd-blazor-mobile-ux-pages-test-profils.md` (Partie B) — les deux fichiers d'origine
doivent être retirés du Project Context/dépôt une fois ce document en place, pour respecter la
règle "un document vivant = une seule source de vérité" (`convention-nommage-documents.md`).

**Conventions déjà en place à respecter (tout le lot)** : `convention-ui-blazor-alignement-boutons.md`
(boutons d'action alignés à droite), `convention-ui-blazor-icones-boutons.md` (icônes Bootstrap
Icons, matrice de décision, accessibilité `aria-hidden`/`aria-label`) ; IDs HTML stables, jamais de
sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x + Moq + bUnit ; Bootstrap
déjà en usage dans le projet (aucune nouvelle dépendance CSS/JS).

---

# Partie A — Listes de profils, page Journaux, page « Mon Profil »

*Fait suite à une revue visuelle du client sur mobile (captures d'écran des pages Profils
d'import/export, Journaux, Mon Profil, 23/07). Complète les Lots L/M/S (NavMenu) et R
(densification desktop des cartes de règles de feuille) sans les rouvrir : R traitait la densité
des *cartes de règles* sur grand écran, cette partie traite la *responsivité mobile des listes
tabulaires* (Profils, Utilisateurs) et des pages Journaux / Mon Profil, sujets disjoints.*

**Décision actée explicitement avec le client** : le tableau natif (`<table>`) est **conservé tel
quel pour la page Journaux** (`Logs.razor`, route `/logs`) — ne pas y appliquer la bascule en
cartes de V2. Cette page suit son propre traitement (V5).

## V1. Correctif bug — interpolation manquante dans l'en-tête de colonne « règle(s) de feuille »

**Constat client** : la colonne « nombre de règles de feuille » des listes `ImportProfiles.razor`/
`ExportProfiles.razor` affiche littéralement le texte `{0} règle(s) de feuille` au lieu du nombre
réel interpolé.

**Étape 0 — investigation préalable** :
- [ ] Localiser la clé resx concernée (probablement une clé du type `ImportProfiles_SheetRulesCount`
  ou équivalent) et vérifier si l'appel côté Razor utilise `string.Format`/interpolation `$""` ou
  passe directement la chaîne resx brute sans substitution du paramètre `{0}`.
- [ ] Confirmer si le bug touche uniquement l'écran import, uniquement export, ou les deux
  (parité déjà actée par les tickets Q — vérifier que le correctif s'applique aux deux si le bug
  y est présent des deux côtés).

**Test (rouge)** :
- [ ] Test bUnit rendant la liste avec un profil ayant N règles de feuille (N > 0) → le texte
  rendu contient le nombre réel `N` et non le littéral `{0}`.

**Correctif (vert)** :
- [ ] Appliquer `string.Format(resx, count)` ou l'équivalent Razor déjà utilisé ailleurs dans le
  projet pour l'interpolation resx paramétrée (rechercher un pattern existant avant d'en inventer
  un nouveau).

**Cas limite** :
- [ ] N = 0 → le texte reste grammaticalement correct (« 0 règle de feuille » ou équivalent déjà
  géré par le pluriel resx `(s)` existant — ne pas introduire de nouvelle clé si le `(s)` littéral
  suffit déjà, sujet distinct du bug d'interpolation lui-même).

---

## V2. Bascule tableau → cartes sur mobile pour les listes (Profils import/export, Utilisateurs)

**Breakpoint retenu : `md` (768px)**, breakpoint standard Bootstrap — recherche exhaustive dans
les documents projet et `NavMenu.razor.css` : aucun breakpoint custom déjà en usage ailleurs dans
le projet, donc pas de raison de s'écarter du standard Bootstrap plutôt que d'en introduire un
propre à ce lot.

**Comportement attendu** : sur les pages de liste **hors Journaux** (`ImportProfiles.razor`,
`ExportProfiles.razor`, `Users.razor` — route `/users`), sous 768px (`md`), le `<table>` est
masqué et remplacé par une liste de `<div class="card">`, une carte par ligne de données. Au-dessus
de 768px, le tableau reste affiché tel quel (classes `d-none d-md-table` sur le tableau, `d-md-none`
sur le conteneur de cartes) — aucune régression desktop.

**Approche technique** : un seul jeu de données, deux gabarits de rendu conditionnés par classes
Bootstrap responsives (`d-none d-md-table` sur le tableau, `d-md-none` sur le conteneur de
cartes) plutôt que deux composants dupliqués — éviter la duplication de la logique de récupération
des données (`GetAllAsync()` etc.), seul le template d'affichage diffère.

**Contenu d'une carte** : reprend les mêmes champs que les colonnes du tableau (nom,
`EquipementTypeElementNom` ou équivalent, nombre de règles), plus les boutons d'action de la ligne
(voir V3 pour leur forme sur cette carte).

**Tests (bUnit)** :
- [ ] Rendu par défaut (pas de simulation de largeur réelle, bUnit ne calcule pas de layout) :
  présence simultanée dans le DOM des deux gabarits (tableau + cartes), chacun portant sa classe
  responsive respective — test sur la classe, pas sur un rendu visuel.
- [ ] Contenu identique entre les deux gabarits pour un même profil (même nom, même compte de
  règles) — pas de divergence de données entre les deux représentations.
- [ ] Aucune régression sur les tests existants de `ImportProfilesTests`/`ExportProfilesTests`
  qui vérifient déjà le contenu du tableau (les assertions existantes doivent rester vraies,
  seule la structure autour change).
- [ ] Page Journaux : test de non-régression explicite confirmant que le tableau reste seul
  (aucune classe `d-none`/`d-md-none` ajoutée sur ce tableau spécifique) — garde-fou contre une
  généralisation accidentelle à cette page lors de l'implémentation.

**Hors périmètre explicite de V2** : réordonnancement ou masquage sélectif de colonnes sur
tableau desktop (sujet distinct, non demandé) ; la page Journaux (traitée séparément en V5).

---

## V3. Actions de ligne (Modifier/Dupliquer) — icônes ou menu déroulant « Actions »

**Constat client** : les boutons d'action empilés verticalement dans chaque ligne/carte
(`Modifier`, `Dupliquer`) prennent trop de hauteur.

**Décision** : remplacer le texte des boutons par des icônes seules — `bi-pencil` pour Modifier,
`bi-copy` pour Dupliquer (confirmé, icône Bootstrap Icons standard) — conformément à
`convention-ui-blazor-icones-boutons.md` (ligne de grille/tableau → icône seule ou icône + libellé
court). Un bouton icône seule **doit** porter `aria-label` + `title` explicites (règle A11Y déjà
actée dans cette convention).

**Alternative envisagée et écartée par défaut** : regroupement dans un menu déroulant Bootstrap
(`dropdown`) « Actions ». Retenue uniquement si le nombre d'actions de ligne dépasse 2 dans un
lot futur (ex. ajout d'une action « Archiver ») — pour l'instant, 2 icônes côte à côte suffisent
et évitent un clic supplémentaire pour une action fréquente (Modifier).

**Tests (bUnit)** :
- [ ] Les boutons `#modify-profile-button-{id}`/`#duplicate-profile-button-{id}` (IDs existants,
  inchangés) contiennent désormais un `<span class="bi bi-pencil" aria-hidden="true">` /
  `<span class="bi bi-copy" aria-hidden="true">` respectivement, avec `aria-label`/`title`
  explicites, sans texte visible.
- [ ] Comportement fonctionnel inchangé (clic sur Modifier navigue toujours vers l'éditeur, clic
  sur Dupliquer déclenche toujours la même logique de duplication) — réutiliser les tests
  fonctionnels déjà existants, ne pas les dupliquer.
- [ ] Parité import/export si les deux écrans partagent ce pattern (même vérification que les
  tickets Q/R).

**Hors périmètre explicite de V3** : le menu déroulant « Actions » lui-même — reporté, ne pas
l'implémenter préventivement.

---

## V4. Boutons globaux de haut de page — empilement mobile

**Constat client** : « Tester un profil » et « Créer » sont collés horizontalement et illisibles
sur mobile.

**Comportement attendu** : sous le même breakpoint que V2, les deux boutons passent en pleine
largeur (`w-100 mb-2`) et s'empilent verticalement au lieu d'être côte à côte. Au-dessus du
breakpoint, comportement desktop inchangé (côte à côte, tailles actuelles).

**Tests (bUnit)** :
- [ ] Les deux boutons portent une classe responsive additionnelle (`w-100 mb-2` conditionnée par
  un breakpoint, ex. via une classe wrapper `d-grid gap-2 d-md-flex` sur leur conteneur commun)
  sans changer leurs IDs existants (`#create-profile-button`, `#test-import-profile-button` —
  voir Lot S) ni leur comportement de navigation.

**Hors périmètre explicite de V4** : masquer l'un des deux boutons dans un menu sur mobile (option
évoquée par le client mais écartée — l'empilement suffit et les deux actions restent fréquentes).

---

## V5. Page Journaux (`Logs.razor`, route `/logs`) — troncature du message et accordéon de détail

**Constat client** : la colonne « Message » affiche des requêtes SQL complètes, rendant les
lignes de tableau démesurément hautes.

**Comportement attendu** :
- La cellule « Message » du tableau n'affiche qu'un extrait (les 50 premiers caractères, `…` en
  suffixe si tronqué) suivi d'un lien/bouton `#view-log-details-button-{id}`.
- Le clic ouvre un accordéon Bootstrap (natif `<details>`/`<summary>` ou composant `accordion`,
  cohérent avec le mécanisme déjà en place pour R3 — réutiliser le même pattern
  `<details>`/`<summary>` plutôt qu'introduire un second mécanisme d'accordéon dans le projet) qui
  affiche le message complet dans une balise `<pre><code>` (formatage préservé, pas d'échappement
  cassé sur les caractères SQL comme guillemets ou retours à la ligne).
- Les filtres (menus déroulants, recherche) reçoivent les classes `form-control-lg`/
  `form-select-lg` pour une meilleure cible tactile.

**Tests (bUnit)** :
- [ ] Message source de plus de 50 caractères → cellule affiche exactement les 50 premiers
  caractères + `…`, pas le message complet.
- [ ] Message source de 50 caractères ou moins → affiché intégralement, pas de troncature ni de
  `…` superflu (pas de lien « Voir les détails » inutile dans ce cas, ou lien présent mais
  redondant — à trancher selon ce qui est le plus simple à tester : préférer masquer le lien si le
  message n'est pas tronqué, pour éviter un clic qui n'apporte rien).
- [ ] Clic sur `#view-log-details-button-{id}` → le message complet apparaît dans le DOM à
  l'intérieur d'un `<pre><code>` (vérification `FindAll` non vide après clic, même principe que
  R3 — pas un test de style `display:none`).
- [ ] Message contenant des caractères spéciaux SQL (guillemets simples, `<`, `>`) → rendu sans
  casser le HTML de la page (échappement correct, test avec un message de log synthétique
  contenant ces caractères).
- [ ] Les éléments de filtre portent bien les classes `form-control-lg`/`form-select-lg` (test sur
  attribut `class`).

**Hors périmètre explicite de V5** : bascule cartes/tableau pour cette page (décision actée en
introduction : le tableau est conservé) ; coloration syntaxique SQL dans le `<pre><code>` (non
demandée, `<pre><code>` brut suffit).

---

## V6. Page « Mon Profil » — boutons pleine largeur et espacement

**Comportement attendu** :
- Les boutons « Mettre à jour... » et « Changer le mot de passe » passent en pleine largeur
  (`w-100 btn-lg`).
- Une marge additionnelle (`mt-5`) est ajoutée au-dessus du titre « Sécurité / Mot de passe » pour
  séparer visuellement les deux zones (informations de profil vs. sécurité).

**Tests (bUnit)** :
- [ ] Les deux boutons portent bien les classes `w-100 btn-lg` (sans changer leurs IDs existants
  ni leur comportement de soumission).
- [ ] Le conteneur du titre « Sécurité / Mot de passe » porte bien la classe `mt-5` additionnelle.
- [ ] Aucune régression sur les tests existants de mise à jour de profil / changement de mot de
  passe (comportement fonctionnel inchangé, seule la présentation change).

---

# Partie B — Pages de test de profil (import/export)

*Fait suite à une revue client mobile des pages `/import-profiles/test` et `/export-profiles/test`.
Dépend du Lot S (les boutons d'entrée vers ces pages vivent maintenant sur `ImportProfiles.razor`/
`ExportProfiles.razor` — inchangé par cette partie) et réutilise les fixtures réelles déjà en place
(C7401, D8570, G6306B REV) pour les tests d'upload.*

**Périmètre** : `ImportProfileTest.razor` (`/import-profiles/test`) et `ExportProfileTest.razor`
(`/export-profiles/test`) uniquement. Cette partie ne touche ni `ImportProfiles.razor`/
`ExportProfiles.razor` (listes, voir Partie A), ni `ImportProfileEditor.razor`/
`ExportProfileEditor.razor` (éditeurs, déjà couverts par leur propre bouton retour depuis le
22/07 — voir `etat-avancement-global-2026-07-22.md` §5), ni la logique métier des pipelines
import/export.

**Principe directeur transverse pour cette partie** : privilégier systématiquement les classes
utilitaires et composants **Bootstrap natifs** (`btn-lg`, `form-control-lg`, `form-select-lg`,
`w-100`, `input-group`, `card`, `text-muted`, `small`, `d-flex`, breakpoints `w-md-*`, etc.) plutôt
que du CSS personnalisé. Un ajout de CSS scopé (`.razor.css`) n'est justifié que si aucune
combinaison de classes Bootstrap existantes ne couvre le besoin (à documenter en commentaire dans
le fichier `.razor.css` si le cas se présente). **Aucun JavaScript/JS interop nouveau** n'est
introduit par cette partie — toute interaction reste gérée par Blazor (état C#, liaison
d'événements `@onclick`/`@onchange`) ou par des mécanismes HTML natifs (`<label for="...">`
cliquable). Si une section ci-dessous semble nécessiter du CSS custom ou du JS, c'est un signal
pour simplifier l'approche plutôt que pour l'implémenter telle quelle — voir en particulier V10.

**Point d'attention architectural transverse** : `ImportProfileTest.razor`/`ExportProfileTest.razor`
utilisent tous deux le composant natif `<InputFile>` (bUnit cible directement
`cut.FindComponent<InputFile>()` puis `InputFileContent.CreateFromText(...)` +
`.UploadFiles(file)` — voir pattern documenté dans `etat-avancement-pipeline-extraction-2026-07-17.md`).
**Aucune restylisation de ce lot ne doit retirer, masquer ou remplacer le composant
`<InputFile>` dans le DOM** — voir V10, qui retient volontairement un habillage Bootstrap natif
(classes CSS uniquement, pas d'overlay ni de masquage) pour ne courir aucun risque sur
`FindComponent<InputFile>()`.

---

## V7. Investigation préalable (obligatoire avant tout code)

- [ ] Lire le markup actuel des deux fichiers `.razor` : structure du bouton retour existant
  (id, position), structure du bloc `<InputFile>`, présence ou non d'un `.razor.css` scopé par
  composant, classes Bootstrap déjà utilisées, version de Bootstrap réellement chargée (vérifier
  `wwwroot`/`_Layout`/`App.razor` — la proposition suppose Bootstrap 5.3+ pour les couleurs
  "subtle", à confirmer avant de les utiliser).
- [ ] Confirmer la présence ou l'absence d'un bouton retour existant sur ces deux pages
  spécifiquement (à ne pas confondre avec `back-to-import-profiles-button`/
  `back-to-export-profiles-button` des éditeurs, ajoutés le 22/07 sur un composant différent).
- [ ] Identifier les IDs HTML déjà stables sur les éléments interactifs de ces deux pages
  (sélecteur du profil à tester, zone de résultat, éventuel bouton de téléchargement — voir J3)
  pour ne rien renommer par erreur pendant la restructuration.
- [ ] Confirmer que les tests bUnit existants (`ImportProfileTestTests.cs`/
  `ExportProfileTestTests.cs`, voir Lot F/J) passent avant toute modification (baseline verte).

---

## V8. Navigation retour : lien icône dans un bandeau de page, pas un bouton de contenu

**Décision actée pour ce ticket** (à documenter dans le code en commentaire, pour ne pas être
rouvert par erreur comme une violation de `convention-ui-blazor-icones-boutons.md`) : la règle
"pas d'icône pour une action secondaire" de ce document concerne les **boutons qui opèrent sur le
contenu d'un formulaire ou d'une carte** (`Annuler`, `Fermer`). Le lien de retour traité ici est
une **navigation de page à page** (équivalent d'un fil d'Ariane), catégorie distincte non couverte
par cette règle — d'où l'usage d'une icône seule autorisé spécifiquement ici.

**Comportement attendu** :
- Le lien retour (actuellement en haut du contenu, probablement un `<button>`/`<a>` texte) est
  déplacé dans un bandeau de page fin en haut d'écran, aligné à gauche, sous forme d'icône flèche
  Bootstrap Icons (`bi-arrow-left`) sans texte visible sur mobile.
- **Accessibilité** : puisque l'icône est seule (pas de texte visible), elle porte un
  `aria-label` explicite (ex. `aria-label="Retour à la liste des profils d'import"`) et un `title`
  — conforme à la section A11Y de `convention-ui-blazor-icones-boutons.md` pour les boutons icône
  seule.
- L'`id` HTML existant du lien retour est conservé tel quel (pas de renommage) pour ne pas casser
  un test existant qui le cible déjà.

**Tests** (bUnit) :
- Le lien retour porte toujours le même `id` qu'avant restructuration, navigue toujours vers la
  même route (`/import-profiles` ou `/export-profiles`) — non-régression fonctionnelle.
- Le lien retour porte un `aria-label` non vide.
- Test de structure : le lien retour est bien un enfant du bandeau de page (nouvelle classe CSS
  dédiée), pas resté dans le flux du contenu principal.

---

## V9. Texte explicatif : réduction de la charge visuelle

**Comportement attendu** :
- Le paragraphe d'introduction (explication de l'écran) passe en taille réduite (`small`/
  `0.875rem`) et couleur atténuée (`text-muted` ou équivalent projet).
- Reformulation en liste à puces si le paragraphe dépasse 2 phrases, pour favoriser le "scan"
  plutôt que la lecture linéaire sur mobile.
- Le contenu textuel lui-même n'est pas modifié dans son sens, seulement sa présentation — pas de
  nouvelle traduction `.resx` nécessaire si le texte reste identique (vérifier avant d'en créer une
  nouvelle).

**Tests** (bUnit) :
- Le conteneur du texte explicatif porte bien les classes CSS attendues (`text-muted`, taille
  réduite) — test sur l'attribut `class`, pas sur un rendu visuel.
- Non-régression : le texte lui-même (clé `.resx`) reste présent et inchangé dans le DOM.

---

## V10. Habillage Bootstrap natif de l'upload de fichier (pas de dropzone custom)

**Décision de simplification pour ce ticket** : la "dropzone" évoquée en revue client (masquage de
l'input natif + zone de superposition cliquable) demanderait du CSS custom (positionnement
`opacity`/`absolute`) pour un gain d'expérience marginal par rapport à ce que Bootstrap propose
déjà nativement pour les champs de fichier. Ce ticket retient donc l'habillage **Bootstrap natif**
plutôt que la reconstruction d'un composant dropzone :
- L'`<InputFile>` reste affiché tel quel dans le DOM (jamais masqué), avec les classes Bootstrap
  `form-control form-control-lg` — Bootstrap stylise nativement les champs `type="file"` avec cette
  classe (bouton "Parcourir" intégré, pas de personnalisation CSS nécessaire).
  Cela résout déjà le point d'accessibilité tactile (44-48px) via `form-control-lg`.
- Le champ est enveloppé dans un `input-group` avec une icône Bootstrap Icons en `input-group-text`
  à gauche (ex. `bi-file-earmark-arrow-up`), pour l'indice visuel demandé — composant Bootstrap
  standard, aucun CSS additionnel.
- Aucun masquage, aucun overlay, aucun JS : le composant `<InputFile>` reste l'élément visible et
  cliquable, ce qui élimine tout risque sur `FindComponent<InputFile>()`.

**Tests** (bUnit) :
- **Test critique de non-régression** : `cut.FindComponent<InputFile>()` réussit toujours après
  restructuration, et `InputFileContent.CreateFromText(...)` + `.UploadFiles(file)` déclenche
  toujours `OnInputFileChangeAsync`/le pipeline d'import — réutiliser exactement les tests d'upload
  existants de `ImportProfileTestTests.cs`/`ExportProfileTestTests.cs` sans les réécrire, pour
  prouver qu'ils passent sans modification.
- Le `<InputFile>` porte bien les classes `form-control form-control-lg` (test sur `class`).
- Le conteneur parent porte bien la classe `input-group`, avec un `input-group-text` contenant
  l'icône attendue (test sur la présence et les classes, pas sur le rendu visuel).

---

## V11. Cibles tactiles ≥ 44-48px sur les champs de formulaire

**Comportement attendu** :
- Le select de sélection du profil à tester et tout bouton d'action de la page appliquent la
  classe de taille large disponible (`form-select-lg`/`btn-lg` si Bootstrap, ou classe CSS
  personnalisée équivalente si le projet n'est pas encore en Bootstrap 5.3+ — à confirmer en V7).
- Vérification que la hauteur rendue respecte le minimum de 44px (recommandation Apple/Google),
  cible haute de 48px si le composant CSS du projet le permet nativement.

**Tests** (bUnit) :
- Le select de profil et les boutons d'action portent bien la classe de taille large attendue
  (test sur `class`, pas de mesure de pixels réels — bUnit ne calcule pas de layout).

---

## V12. Boutons d'action pleine largeur sur mobile

**Comportement attendu** :
- Le bouton d'action principal (lancer le test / générer, selon la page) passe en pleine largeur
  sur petit écran (`w-100`) combiné à `btn-lg`, avec un retour à une largeur naturelle sur écran
  large via une classe de breakpoint (`w-md-auto` ou équivalent projet) — mobile-first, pas une
  largeur fixe sur tous les formats.
- Cette règle s'applique au bouton d'action principal uniquement, pas aux boutons secondaires
  (retour, annuler) — cohérent avec la hiérarchie CTA déjà en place ailleurs dans le projet.

**Tests** (bUnit) :
- Le bouton d'action principal porte bien les classes `w-100`/`btn-lg` (+ classe de breakpoint si
  utilisée) — test sur `class`.

---

## V13. Bloc résultat en composant carte

**Comportement attendu** :
- La zone d'affichage du résultat (aperçu tabulaire par feuille générée côté export — voir J3 —
  ou résumé d'import côté import) est enveloppée dans un composant `card` avec ombre légère et
  couleur de fond de succès (`bg-success-subtle` si Bootstrap 5.3+ confirmé en V7, sinon
  équivalent le plus proche disponible dans le projet).
- Le contenu fonctionnel de cette zone (tables `Parents`/`Enfants`, message de résultat) n'est pas
  modifié — seul le conteneur change.

**Tests** (bUnit) :
- Le conteneur de la zone résultat porte bien la classe `card` (+ classe de couleur si retenue).
- Non-régression : le contenu des tables générées (`Parents`/`Enfants`) reste identique et
  détectable par les tests existants (mêmes assertions qu'aujourd'hui sur le contenu, uniquement
  le conteneur parent change de classe).

---

# Hors périmètre explicite (tout le lot)

**Partie A** :
- Réordonnancement ou masquage sélectif de colonnes sur tableau desktop.
- Menu déroulant « Actions » (V3) — reporté.
- Masquer l'un des deux boutons globaux dans un menu sur mobile (V4) — écarté.
- Bascule cartes/tableau pour la page Journaux — décision actée : le tableau est conservé.
- Coloration syntaxique SQL dans le `<pre><code>` (V5) — non demandée.

**Partie B** :
- Aucune modification de la logique métier des pipelines import/export (Lots D/I inchangés).
- Aucune introduction de bibliothèque UI supplémentaire (pas de MudBlazor, pas de framework CSS
  concurrent de Bootstrap).
- `ImportProfiles.razor`/`ExportProfiles.razor` (pages de liste) : couvertes par la Partie A de ce
  même lot, pas par la Partie B — pas de duplication de traitement entre les deux parties.
- Mode sombre / thématisation avancée : non demandé, non traité.
- Répercussion du style de la Partie B sur `ImportProfileEditor.razor`/`ExportProfileEditor.razor` :
  ces pages ont leur propre trajectoire de restylisation (Lots O/P/Q/R) — ne pas fusionner les deux
  chantiers sans décision client explicite.

---

# Note d'efficacité d'implémentation (tout le lot)

**Partie A** :
- Traiter **V1 en premier** (correctif isolé, rapide, aucune dépendance avec le reste du lot).
- **V2 et V4 dans la même passe** : les deux dépendent du même breakpoint et du même conteneur de
  page de liste — les traiter ensemble évite de recalculer deux fois la même logique responsive.
- **V3** peut se faire indépendamment de V2/V4 mais est plus simple à valider visuellement une
  fois V2 en place (les icônes s'intègrent différemment dans une carte que dans une cellule de
  tableau) — faire V3 après V2 par commodité de vérification, pas par dépendance stricte de test.
- **V5 et V6** sont indépendants du reste de la Partie A (pages distinctes) et peuvent être
  traités dans n'importe quel ordre, y compris en parallèle d'une autre session.
- Breakpoint tranché (`md`, 768px, voir V2) — aucune vérification supplémentaire nécessaire avant
  de démarrer V2/V4.

**Partie B** :
1. Commencer par **V7** (lecture) avant tout code — évite de deviner une structure DOM qui
   n'existe pas réellement.
2. Traiter **V10 en priorité fonctionnelle** dès que V7 est fait : c'est le point qui présente le
   risque de régression le plus élevé (bris potentiel de `FindComponent<InputFile>()`) — valider
   ce point tôt permet d'arrêter/ajuster l'approche avant d'investir sur V8/V9/V11/V12/V13 qui sont
   des changements purement cosmétiques à faible risque.
3. Après V10, dérouler V8 → V9 → V11 → V12 → V13 dans l'ordre : chacun est indépendant, peut être
   committé séparément, et la suite de tests bUnit complète doit rester verte après chaque étape
   (pas seulement à la fin de la partie).
4. Si les deux pages (`ImportProfileTest.razor`/`ExportProfileTest.razor`) partagent un `.razor.css`
   scopé commun ou des classes CSS identiques, factoriser dans un seul endroit plutôt que dupliquer
   — suivre le même réflexe de parité que documenté dans les Lots Q/R pour les éditeurs.

**Ordre global recommandé** :
**V1 → V2 → V4 → V3 → V5 → V6 → V7 → V10 → V8 → V9 → V11 → V12 → V13**
(les deux parties restent indépendantes l'une de l'autre — cet ordre peut être éclaté entre deux
sessions Claude Code sans risque de conflit, aucun fichier n'étant partagé entre la Partie A et la
Partie B).

**Dossiers concernés** :
- Partie A : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfiles.razor(.css)`,
  `ExportProfiles.razor(.css)`, `Users.razor(.css)` (route `/users`), `Logs.razor(.css)` (route
  `/logs`), page « Mon Profil ».
- Partie B : `ImportProfileTest.razor(.css)`, `ExportProfileTest.razor(.css)`.
- (+ miroir tests dans `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/` pour les deux parties).
