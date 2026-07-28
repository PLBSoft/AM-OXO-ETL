# Tickets TDD — Lot 053 : largeur, densité et affordance des formulaires d'édition de profil

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 052
(autorisation des pages métier BlazorAdmin, livré le 28/07 — voir
`convention-autorisation-pages-blazoradmin.md`).*

> **Note de renumérotation (28/07)** — ce document a d'abord été rédigé sous le numéro **052**,
> pendant qu'une session parallèle numérotait 052 le lot d'autorisation des pages métier. Ce
> dernier ayant été **livré et poussé** (commit `439a744`, rebasé en `7bba21e`), son numéro figure
> désormais dans l'historique git, les noms de tests et
> `convention-autorisation-pages-blazoradmin.md` : c'est lui qui conserve 052, et le présent
> document qui devient **053**. Ses sous-tickets sont renumérotés `53.0` à `53.6`.
>
> Même mécanique que la collision « Lot U » du 23/07 (voir l'en-tête de
> `tickets-tdd-blazor-mobile-first-lot-v.md`) : le lot déjà livré garde son identifiant, le lot non
> commencé se décale. **Aucun identifiant `52.x` de ce document n'a jamais été référencé dans du
> code ou un commit** — la renumérotation est sans effet de bord.

**Origine** : revue visuelle **desktop** de Simon le 28/07 sur `/import-profiles/{id}/edit`
(capture 2560×1528, profil « Profil OXO standard »). Trois remarques, formulées explicitement sous
l'angle KISS / YAGNI / affordance :

1. **Largeur** — les champs s'étirent sur 100 % de la largeur de l'écran, ce qui fatigue la lecture
   et donne un aspect « vide ».
2. **Densité** — l'empilement vertical de champs courts (« Préfixe de repère », « Nom du type
   d'élément d'équipement ») consomme trop de hauteur pour rien.
3. **Affordance** — les boutons « Ajouter le tableau » / « Ajouter l'application » ressemblent à des
   champs de saisie désactivés et sont visuellement séparés du champ auquel ils se rapportent.

Les trois remarques valent pour les **quatre routes d'édition** : `/import-profiles/new`,
`/import-profiles/{id}/edit`, `/export-profiles/new`, `/export-profiles/{id}/edit`.

**La revue portait sur la vue desktop, mais le lot ne dégrade pas la vue mobile** : tout ce qui a
été acté mobile-first aux lots V et 030 reste vrai sous 768px. C'est une contrainte de ce lot, pas
une intention générale — elle est testée explicitement en 53.5.

---

## Décisions actées avec Simon (28/07)

| Sujet | Décision |
| :--- | :--- |
| Grille 2 colonnes (remarque 2) | **Réouverture assumée de 30.1, desktop seulement** — `col-12 col-md-6` sur les champs courts. Sous 768px, l'empilement de 30.1 est strictement inchangé. |
| Portée du traitement « Ajouter » (remarque 3) | **Ligne unique champ+bouton uniquement pour les formulaires d'ajout mono-champ** (Tableaux, Applications). **Apparence de bouton plein + icône « + » pour tous** les boutons d'ajout, y compris les sous-formulaires multi-champs où la ligne unique est structurellement impossible. |
| Portée de la largeur maximale (remarque 1) | **Les 4 routes éditeur uniquement.** Pages de liste, Journaux, Utilisateurs, Fichiers générés et pages de test conservent la pleine largeur — leurs tableaux en bénéficient. |
| Valeur de largeur maximale | **1140 px.** |

### Précision technique sur les 1140 px (à lire avant 53.1)

La valeur 1140 px a été choisie en référence aux conteneurs Bootstrap, mais **aucune variante
`.container-*` ne plafonne à 1140 px sur un très grand écran** : toutes partagent la même échelle
`--bs-container-max-widths` (sm 540 / md 720 / lg 960 / xl 1140 / **xxl 1320**) et ne diffèrent que
par le breakpoint à partir duquel elles commencent à plafonner. `.container-lg` rendrait donc
**1320 px** sur l'écran 2560 px de la capture, pas 1140.

**La valeur retenue reste 1140 px** — c'est la décision de Simon, elle n'est pas rouverte. Seule
l'implémentation change : une règle CSS unique dans `app.css` plutôt qu'une classe Bootstrap
existante. C'est une correction factuelle d'hypothèse, pas un choix de conception à re-trancher.

---

## Décisions antérieures explicitement rouvertes par ce lot

Ce lot rouvre **deux** décisions, toutes deux de façon assumée et documentée — même mécanique que
30.7 qui avait rouvert V4/X2 sur capture client. Tout le reste des lots X / Y / V / 030 / R reste
fermé.

- **30.1 (empilement vertical `col-12` de tous les champs racine)** → rouvert **au-dessus de
  768px seulement**, et **uniquement pour les champs courts** listés en 53.2. « Nom du profil »
  reste pleine largeur. Sous 768px, 30.1 s'applique intégralement.
- **30.3 (boutons d'ajout en `w-100 mt-3` + contour `btn-outline-*`)** → rouvert sur **l'apparence**
  (plein + icône) et, pour les seuls formulaires mono-champ, sur **la largeur et la position**.
  L'intention d'origine de 30.3 — *« ne pas concurrencer visuellement le bouton d'enregistrement
  final »* — **reste valide et est préservée autrement** : voir la hiérarchie retenue en 53.4.

**Conséquence directe sur la suite de tests** : les tests bUnit issus de 30.1 et 30.3 asserent
aujourd'hui l'**absence** de `col-md-*` et la **présence** de `btn-outline-*`/`w-100`. Ils vont
passer au rouge. C'est attendu. **Ces tests existants sont corrigés, pas doublés d'un second test à
côté** (même exigence qu'en 51.2).

---

## Conventions déjà en place à respecter (tout le lot)

- `convention-ui-blazor-alignement-boutons.md` — et en particulier son paragraphe *« Boutons
  intégrés à une ligne de saisie existante […] déjà à l'extrémité droite de leur ligne par
  construction »*, qui couvre exactement le cas de 53.3 : **la convention n'est pas amendée par ce
  lot**, elle prévoit déjà ce motif.
- `convention-ui-blazor-icones-boutons.md` — matrice de décision : une action CRUD standard
  (« Ajouter ») **doit** porter une icône. L'écart relevé en §2.2 de
  `audit-design-blazoradmin-2026-07-27.md` est donc corrigé par 53.4, pas contourné.
- Aucune nouvelle dépendance CSS/JS. Bootstrap + `theme-m3.css` uniquement, CSS custom réduit au
  strict nécessaire et centralisé dans `app.css` (jamais dupliqué dans deux `.razor.css`).
- IDs HTML stables sur tout élément interactif ; jamais de sélection par texte ou position en bUnit.
- bUnit ne calcule aucun layout : **tous** les tests de ce lot portent sur des classes CSS et sur la
  structure DOM, jamais sur un rendu en pixels.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite (tout le lot)

- **Toute page hors des 4 routes éditeur** : listes de profils, Journaux, Utilisateurs, Fichiers
  générés, pages de test, Mon Profil, ApiTest. La largeur maximale ne leur est **pas** appliquée
  (décision actée) — ne pas « harmoniser » spontanément.
- **La grille responsive des cartes de règle de feuille** (`.sheet-rule-grid`, `minmax(480px,1fr)`,
  lot R) — non touchée. À 1140 px de large elle continue de rendre deux colonnes sur grand écran :
  **c'est précisément pourquoi la valeur 1140 ne régresse pas le lot R**, et c'est à vérifier en
  53.1, pas à modifier.
- **Le nombre, l'ordre et le libellé des champs** — aucun champ ajouté, retiré, renommé ou
  réordonné par ce lot. Uniquement leur conteneur et leur disposition.
- **La structure interne `form-floating`** (input avant label, `placeholder` non vide) — acquis
  fragile du lot 030 Partie B. Ce lot déplace des conteneurs `form-floating` entiers, il n'en
  réécrit **jamais** l'intérieur. En particulier : **ne pas envelopper un `form-floating` dans un
  `input-group`** pour réaliser 53.3 (variante Bootstrap supportée mais qui modifie les rayons de
  bordure et le CSS `:placeholder-shown` appliqué — risque direct de rouvrir 30.6 pour un gain nul).
- **Le bouton `Annuler` des sous-formulaires** et son partage de classe avec le bouton principal
  (écart §2.3 de l'audit design) — non retenu dans ce lot, à traiter séparément le cas échéant.
- **La centralisation des icônes Pencil/Trash inline** des sous-formulaires (§2.4 de l'audit) — hors
  périmètre. Seule l'icône « + » nouvellement introduite est centralisée (53.4).
- **Toute modification de logique métier** (Domain / Application / pipelines) — ce lot est
  strictement Razor + CSS + tests.
- **L'attribut `[Authorize]` des quatre pages éditeur.** Le lot 052 les a fait passer de
  `[Authorize(Roles = Admin)]` à `[Authorize]` simple, et `BusinessPageAuthorizationHttpTests`
  porte désormais une assertion par route. Ce lot modifie le markup **à l'intérieur** de ces pages
  et ne touche ni leur attribut d'autorisation, ni leur route : les tests HTTP du lot 052 doivent
  rester verts sans être modifiés. Si l'un d'eux vire au rouge, c'est le signal qu'on a déplacé
  autre chose que du balisage.
- **Le thème sombre**, les contrastes de couleur et l'accessibilité au-delà de ce qui est
  explicitement requis en 53.4 (`aria-hidden` sur l'icône).

---

## 53.0. Investigation préalable (obligatoire avant tout code)

- [ ] **Cartographier les champs racine réels des deux éditeurs.** Côté import, la capture montre
  « Nom du profil », « Préfixe de repère », « Nom du type d'élément d'équipement ». **Côté export,
  ne rien supposer** : lire `ExportProfileEditor.razor` et relever la liste exacte, pour déterminer
  quels champs sont « courts » et appariables deux à deux en 53.2. Si le nombre de champs courts est
  impair côté export, le dernier reste en `col-12` — le consigner plutôt que d'inventer un
  appariement artificiel.
- [ ] **Confirmer la divergence de conteneur racine relevée en §2.3 de
  `audit-design-blazoradmin-2026-07-27.md`** : `ExportProfileEditor.razor` enveloppe son contenu
  dans `<div class="container-fluid px-3">` (lignes 20 et 165 à la date de l'audit) alors que
  `ImportProfileEditor.razor` n'a **aucun** conteneur équivalent. C'est le point d'ancrage de 53.1 —
  vérifier que c'est toujours vrai avant d'écrire quoi que ce soit.
- [ ] **Recenser tous les boutons d'ajout des deux éditeurs et de leurs sous-formulaires**, avec
  leur `id` et le nombre de champs de leur formulaire, pour classer chacun en « mono-champ »
  (relève de 53.3 **et** 53.4) ou « multi-champs » (relève de 53.4 seul). Périmètre attendu, à
  confirmer par lecture : `ImportProfileEditor.razor` (Tableaux, Applications),
  `SheetRuleForm.razor`, `BlockFieldForm.razor`, `SheetGenerationRuleForm.razor`,
  `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`,
  `ApplicationColumnDefinitionForm.razor`.
- [ ] **Lister les tests existants qui vont passer au rouge** : ceux de 30.1 (absence de `col-md-*`),
  ceux de 30.3 (`w-100 mt-3` + `btn-outline-*`) et le test de parité structurelle 30.5. Les
  connaître **avant** de coder évite de les découvrir un par un et de les « réparer » au jugé.
- [ ] **Vérifier que `AdminIconMarkup` ne contient pas déjà une icône « + »** avant d'en ajouter une
  (53.4).
- [ ] **Baseline verte** : `ImportProfileEditorTests`, `ExportProfileEditorTests` et les tests des
  sous-formulaires passent avant toute modification.

**Effort** : standard. Recensement, pas conception — les quatre décisions sont prises.

---

## 53.1. Conteneur de formulaire à largeur maximale (1140 px), partagé par les deux éditeurs

**Comportement attendu** :
- Une classe unique déclarée dans `wwwroot/app.css` (nom proposé : `.profile-editor-container`)
  applique `max-width: 1140px` et un centrage horizontal (`margin-inline: auto`). Sous 1140 px de
  viewport, le conteneur occupe 100 % de la largeur disponible — **la vue mobile est donc
  strictement inchangée par ce sous-ticket**.
- Cette classe est appliquée au conteneur racine de `ImportProfileEditor.razor` **et** de
  `ExportProfileEditor.razor`, ce qui **résout au passage la divergence §2.3 de l'audit design** :
  les deux pages ont désormais le même conteneur racine, avec le même padding horizontal.
- **Une seule déclaration CSS**, dans `app.css`, jamais dupliquée dans deux `.razor.css` — même
  réflexe de factorisation que `.sheet-rule-card` / `.block-field-*`.
- Le titre de page (`<h1>`) est inclus dans le conteneur, pour qu'il reste aligné sur les champs et
  non collé au bord gauche de l'écran.

**Pourquoi pas une classe Bootstrap** : voir la précision technique en tête de document. Aucune
variante `.container-*` ne plafonne à 1140 px au-delà d'un viewport de 1400 px.

**Tests** (bUnit) :
- Le conteneur racine de `ImportProfileEditor.razor` porte la classe de largeur maximale — test sur
  `class`, jamais sur une largeur calculée (bUnit ne fait pas de layout).
- Idem pour `ExportProfileEditor.razor`.
- Le `<h1>` de chaque éditeur est bien un descendant de ce conteneur (test de structure DOM), pas un
  frère placé avant lui.
- Non-régression : tous les tests existants de rendu des deux éditeurs restent verts — l'ajout d'un
  wrapper ne doit casser aucun sélecteur `#id` existant.

**Vérification manuelle attendue (non testable en bUnit)** : sur un écran large, la grille
`.sheet-rule-grid` (`minmax(480px, 1fr)`, lot R) rend toujours **deux** colonnes de cartes à
l'intérieur du conteneur de 1140 px. Si ce n'était pas le cas, c'est un signal d'arrêt : la
densification desktop du lot R serait annulée par ce lot, ce qui n'est pas l'intention. Le consigner
plutôt que d'ajuster la valeur de 1140 px de sa propre initiative.

---

## 53.2. Champs courts appariés sur deux colonnes au-dessus de 768px

**Comportement attendu** :
- Côté import : « Préfixe de repère » et « Nom du type d'élément d'équipement » passent de `col-12`
  à **`col-12 col-md-6`**, dans une même `<div class="row g-3">`.
- « Nom du profil » **reste `col-12`** : c'est le champ identitaire principal du formulaire, et sa
  valeur est longue par nature. Le passer en demi-largeur n'apporterait rien à la remarque 2, qui
  vise l'empilement de champs **courts**.
- Côté export : même règle, appliquée aux champs courts identifiés en 53.0. Si leur nombre est
  impair, le champ orphelin reste `col-12`.
- **Sous 768px, rien ne change** : `col-12` seul s'applique, l'empilement acté en 30.1 est intact.
- Aucune modification de l'intérieur des `form-floating` (voir Hors périmètre).

**Tests** (bUnit) :
- Les conteneurs des deux champs courts portent **`col-12 col-md-6`** — et le test existant issu de
  30.1 qui assertait l'**absence** de toute classe `col-md-*` sur ces deux champs précis est
  **corrigé** pour asserter la nouvelle intention. Ne pas ajouter un second test concurrent à côté
  d'un test devenu faux.
- Le conteneur de « Nom du profil » porte toujours `col-12` **sans** `col-md-*` — le test de 30.1 le
  concernant reste vert **tel quel**, et sert de garde-fou contre une généralisation accidentelle de
  la grille à tous les champs.
- Les deux champs courts sont bien enfants directs d'un même conteneur `row` (test de structure) —
  sans quoi la grille Bootstrap ne s'applique pas et la classe serait cosmétiquement présente mais
  sans effet.
- Non-régression de liaison : la saisie dans chaque champ déplacé reste liée à sa propriété C#
  (réutiliser les tests de saisie existants de `ImportProfileEditorTests`, ne pas les dupliquer).
- Parité import/export sur les champs courts identifiés (voir 53.6).

---

## 53.3. Formulaires d'ajout mono-champ : champ et bouton sur une même ligne

**Périmètre strict** : les seuls formulaires d'ajout n'ayant **qu'un seul champ de saisie** —
« Tableaux » (`Nom du tableau` + `Ajouter le tableau`) et « Applications » (`Nom de l'application` +
`Ajouter l'application`), dans `ImportProfileEditor.razor`. Tout formulaire à deux champs ou plus
(Règle de feuille, Champ de bloc, Colonnes, Colonnes Points, Colonnes Applications) est **exclu de
ce sous-ticket** — il ne relève que de 53.4.

**Comportement attendu** :
- Le champ et son bouton sont placés dans un même conteneur `row`, le champ en `col-12 col-md`
  (largeur restante) et le bouton en `col-12 col-md-auto` (largeur naturelle). Au-dessus de 768px ils
  sont sur une seule ligne, le bouton immédiatement à droite du champ ; **sous 768px ils s'empilent
  et le bouton reste pleine largeur**, comportement acté au lot V / 30.3.
- Le wrapper `.right-aligned-actions` propre à ces deux boutons disparaît : le bouton est désormais à
  l'extrémité droite de sa ligne **par construction**, cas explicitement prévu par
  `convention-ui-blazor-alignement-boutons.md`. Aucun amendement de convention n'est nécessaire, et
  `.right-aligned-actions` **reste en place partout ailleurs**.
- **Pas d'`input-group`** (voir Hors périmètre) : une grille `row`/`col`, rien de plus.
- Les `id` des deux champs et des deux boutons sont **inchangés**.

**Tests** (bUnit) :
- Le champ et son bouton sont enfants directs d'un même conteneur `row` (test de structure DOM sur la
  relation parent/enfant, pas sur l'ordre visuel).
- Le conteneur du champ porte `col-12 col-md`, celui du bouton `col-12 col-md-auto`.
- Le bouton n'est plus enveloppé dans `.right-aligned-actions` (test sur l'absence de la classe sur
  son ancêtre immédiat).
- Non-régression fonctionnelle : le clic sur `Ajouter le tableau` / `Ajouter l'application` ajoute
  toujours l'élément à la collection en mémoire, **sans navigation ni soumission prématurée du
  formulaire racine** (réutiliser les tests existants de 30.3, ne pas les réécrire).
- Garde-fou de non-généralisation : les boutons d'ajout des formulaires **multi-champs** ne sont
  **pas** passés en ligne unique — un test vérifie qu'au moins un d'entre eux (ex. `Ajouter le
  champ` de `BlockFieldForm.razor`) conserve sa disposition en bas de formulaire.

---

## 53.4. Affordance des boutons d'ajout : bouton plein + icône « + »

**Périmètre** : **tous** les boutons d'ajout des deux éditeurs et de leurs sous-formulaires,
mono-champ et multi-champs confondus (liste établie en 53.0).

**Hiérarchie visuelle retenue** — l'intention de 30.3 est préservée, par un autre moyen :

| Rôle | Traitement |
| :--- | :--- |
| CTA final (`save-profile-button` / `save-export-profile-button`) | **Inchangé** : `btn-primary btn-lg`, teinte primaire M3 (rouge). |
| Bouton d'ajout intermédiaire | **`btn-secondary`** (plein, teinte secondaire M3) + icône « + », taille standard. |
| Bouton d'annulation de sous-formulaire | **Inchangé** dans ce lot (voir Hors périmètre). |

Un bouton plein secondaire donne l'affordance demandée — on ne le confond plus avec un champ
désactivé — tout en restant distinct du CTA d'enregistrement **par la teinte et par la taille**. Un
`btn-primary` sur chaque bouton d'ajout mettrait cinq boutons rouges en concurrence avec l'unique
action d'enregistrement, ce que 30.3 avait précisément cherché à éviter.

**Comportement attendu** :
- Chaque bouton d'ajout : `btn btn-secondary`, précédé d'une icône « + » SVG inline.
- **Largeur** : `w-100 w-md-auto` pour les boutons de formulaires mono-champ (53.3), et **`w-100`
  conservé** pour les boutons de formulaires multi-champs, qui restent en bas de leur formulaire —
  seule leur couleur et leur icône changent.
- **L'icône est ajoutée comme constante `Plus` dans `AdminIconMarkup`**, pas déclarée en SVG inline
  dans chaque fichier. C'est le sens du lot 035.5 et la réponse directe au constat de duplication
  §2.4 de `audit-design-blazoradmin-2026-07-27.md` — introduire une septième occurrence dupliquée
  serait aggraver un écart déjà documenté.
- **Accessibilité** : le bouton conserve son libellé texte visible (« Ajouter le tableau »), donc
  l'icône est **décorative** → `aria-hidden="true"` sur le `<svg>`, **aucun** `aria-label` ni `title`
  à ajouter (règle réservée aux boutons icône seule). Aucune nouvelle clé `.resx` n'est nécessaire.

**Tests** (bUnit) :
- Chaque bouton d'ajout porte `btn-secondary` et **ne porte plus** `btn-outline-secondary` (test sur
  présence **et** absence — le test de 30.3 correspondant est corrigé, pas doublé).
- Chaque bouton d'ajout contient un `<svg>` portant `aria-hidden="true"`.
- Chaque bouton d'ajout conserve son libellé texte (test sur `TextContent` non vide), pour prouver
  qu'on n'a pas glissé vers un bouton icône seule qui exigerait `aria-label` + `title`.
- Le CTA final conserve `btn-primary btn-lg` — non-régression explicite de la hiérarchie.
- Le markup de l'icône provient bien de `AdminIconMarkup.Plus` (au moins un test comparant le rendu
  de deux boutons d'ajout de fichiers différents : chaînes strictement identiques, ce qui prouve la
  source commune).
- Non-régression : le comportement au clic de chaque bouton d'ajout est inchangé.

---

## 53.5. Non-régression mobile explicite (< 768px)

Ce sous-ticket n'introduit **aucun** code. Il existe parce que la revue d'origine portait sur le
desktop et que trois des quatre décisions du lot sont conditionnées par un breakpoint : sans
garde-fou dédié, une implémentation « qui rend bien sur la capture » peut casser silencieusement ce
qui a été acté aux lots V et 030 sans qu'aucun test ne s'en plaigne.

**Tests** (bUnit, sur les deux éditeurs) :
- Aucun conteneur de champ ne porte une classe de grille **sans** son pendant `col-12` — autrement
  dit, aucun champ ne se retrouve en demi-largeur sur mobile. Test paramétré énumérant les `id` de
  champs racine connus des deux éditeurs, pour servir de garde-fou contre l'ajout futur d'un champ
  mal classé (même esprit que le test paramétré de 30.6).
- Les boutons d'ajout portent tous `w-100` (avec ou sans `w-md-auto` selon leur catégorie) — aucun
  bouton d'ajout ne perd sa pleine largeur mobile.
- Le conteneur de 53.1 ne porte **aucune** largeur fixe ni `min-width` (test sur `class`/`style`) :
  seul un `max-width` est admis, sans quoi le formulaire déborderait sur petit écran.

---

## 53.6. Mise à jour du test de parité structurelle Import/Export (30.5)

**Comportement attendu** : le test de parité issu de 30.5 compare les chaînes de classes des
éléments équivalents entre les deux éditeurs. Ce lot modifie trois de ces éléments — conteneur
racine (53.1), conteneur de champ court (53.2), bouton d'ajout (53.4). Le test est **mis à jour pour
refléter les nouvelles chaînes attendues**, et **étendu** au nouveau conteneur racine, qui devient
comparable pour la première fois (avant ce lot, l'import n'en avait aucun — §2.3 de l'audit).

**Tests** (bUnit) :
- Comparaison de chaîne stricte (pas « les deux ont une classe non vide ») sur : conteneur racine,
  conteneur de champ court, bouton d'ajout, CTA final.
- Ce test doit être **le dernier rendu vert du lot**. S'il passe avant que 53.1–53.4 ne soient
  terminés des deux côtés, c'est qu'il ne compare pas ce qu'il prétend comparer.

---

## Ordre recommandé

1. **53.0** — investigation (conditionne le contenu réel de 53.2 côté export et la liste de 53.4)
2. **53.1** — conteneur de largeur maximale (résout aussi la divergence §2.3 de l'audit)
3. **53.2** — grille 2 colonnes des champs courts
4. **53.3** — ligne unique champ+bouton, formulaires mono-champ
5. **53.4** — apparence des boutons d'ajout + icône centralisée
6. **53.5** — garde-fous mobile
7. **53.6** — parité structurelle (clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **Les tests de 30.1 et 30.3 vont passer au rouge. C'est le point de départ, pas un accident.**
  Les corriger dans leur intention (53.2, 53.4) et non les contourner ni les supprimer. Un test
  supprimé au lieu d'être corrigé fait disparaître le garde-fou en même temps que l'assertion
  périmée.
- **Traiter 53.1 avant 53.2.** Poser d'abord la contrainte de largeur, puis densifier à l'intérieur :
  dans l'ordre inverse, la grille 2 colonnes s'évalue sur une largeur d'écran entière et le rendu
  observé n'a rien à voir avec le rendu final.
- **Une seule lecture de `ExportProfileEditor.razor` suffit** (53.0) pour alimenter 53.1, 53.2 et
  53.6 — ne pas le relire à chaque sous-ticket.
- **Ne pas toucher à l'intérieur des `form-floating`.** Ce lot déplace des conteneurs entiers. Si une
  étape semble exiger de réordonner un `input` et son `<label>` ou de modifier un `placeholder`,
  c'est le signal qu'on est en train de rouvrir 30.6 par accident — s'arrêter et le signaler.
- **La valeur 1140 px n'est pas à ré-arbitrer en cours d'implémentation.** Si le rendu déplaît sur un
  écran donné, le consigner et demander — ne pas ajuster à 1280 ou 1000 de sa propre initiative.
- **Aucune nouvelle clé `.resx`** n'est attendue sur tout le lot (les libellés existent, les icônes
  sont décoratives). En créer une est le signe qu'une exigence a été sur-interprétée.
- Exécuter les tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~ProfileEditor|FullyQualifiedName~SheetRuleForm|FullyQualifiedName~ColumnDefinition" --verbosity quiet`.
- **Effort standard sur tout le lot.** Aucune étape ne demande de conception : les quatre décisions
  structurantes sont actées, le reste est de l'application exhaustive et symétrique.

**Dossiers concernés** :
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ImportProfileEditor.razor`,
`ExportProfileEditor.razor`, `SheetRuleForm.razor`, `BlockFieldForm.razor`,
`SheetGenerationRuleForm.razor`, `ColumnDefinitionForm.razor`, `PointColumnDefinitionForm.razor`,
`ApplicationColumnDefinitionForm.razor`, `src/ExcelETL.BlazorAdmin/wwwroot/app.css`,
`AdminIconMarkup.cs` (+ miroir tests dans `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/`).
