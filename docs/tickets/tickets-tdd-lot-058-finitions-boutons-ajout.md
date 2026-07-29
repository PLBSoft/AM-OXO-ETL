# Tickets TDD — Lot 058 : finitions des boutons (teinte tonale, hauteur, gabarit icône + libellé)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 057.*

**Origine** : revue d'usage de Simon le 29/07, remarques 5, 6 et 7 — trois défauts de finition, tous
sur des boutons introduits ou déplacés par le lot 053 :

5. **Teinte des boutons d'ajout** — « ce marron/taupe terne jure avec le rouge vif et dynamique de la
   barre latérale […] il donne un aspect un peu daté à l'interface ».
6. **Hauteur** — « les boutons à droite "Ajouter le tableau" et "Ajouter l'application" sont moins
   hauts que l'input à gauche […] l'alignement n'est pas aussi harmonieux que pour les autres
   composants entre eux ».
7. **Espace icône/libellé** — sur certains boutons le `+` est collé au mot « Ajouter », sur d'autres
   non. Exemple fourni : `#add-block-field-button` (collé) contre `#add-unconditional-colonne-button`
   (espacé), avec le markup des deux boutons à l'appui.

---

## Constats vérifiés dans le code (29/07, dépôt `C:\AM-OXO-ETL`)

Les trois diagnostics ont été confirmés **par lecture**, et deux hypothèses de rédaction ont été
corrigées. Numéros de ligne du 29/07 : repères, pas contrat.

### Remarque 5 — jetons réels, et le problème n'existe qu'en thème clair

`theme-m3.css` déclare (`:12-14` et `:105-107`) :

| Jeton | Clair (`:root`/`[light]`) | Sombre (`[dark]`) |
| :--- | :--- | :--- |
| `--m3-secondary` | `#775652` (taupe foncé) | `#E7BDB8` (rosé clair) |
| `--m3-on-secondary` | `#FFFFFF` | `#442925` |
| `--m3-secondary-rgb` | `119, 86, 82` | `231, 189, 184` |

`.btn-secondary` (`:235-248`) consomme `--m3-secondary` / `--m3-on-secondary`, avec
`color-mix(… , black)` pour hover/active et `--bs-btn-focus-shadow-rgb: var(--m3-secondary-rgb)`.

**Conséquence, non anticipée à la rédaction** : en thème **sombre**, le bouton est déjà un fond rosé
clair avec texte foncé — soit exactement l'aspect « tonal » demandé, avec un contraste de **7,8:1**.
**Le taupe terne est un défaut du thème clair uniquement.** Le choix de mappage à faire n'est donc pas
« corriger partout » mais « quoi faire du sombre » — voir la décision ci-dessous.

Deux consommateurs de `--m3-secondary` sont à préserver intacts : `.btn-outline-secondary`
(`:328-338`, bordure et texte) et `.text-bg-secondary` (`:397`, via `--m3-on-secondary`), plus
`--bs-secondary` lui-même (`:40`, `:133`).

### Remarque 6 — la cause n'est pas celle supposée

Les deux lignes mono-champ sont (`ImportProfileEditor.razor:173-186` et `:268-281`) :

```razor
<div class="row g-2 align-items-end">
    <div class="col-12 col-md">      <div class="form-floating">…</div>   </div>
    <div class="col-12 col-md-auto">
        <button id="add-default-tableau-button" class="btn btn-secondary w-100 w-md-auto" …>
```

**`align-items-end` est la cause directe.** L'hypothèse de rédaction — « les colonnes Bootstrap
s'étirent déjà par défaut, il suffit d'un `h-100` » — est **fausse ici** : `align-items-end` remplace
le `stretch` par défaut, donc la colonne du bouton ne fait que la hauteur de son contenu et un `h-100`
seul ne changerait rien. La correction doit **d'abord** rendre l'étirement possible sur ces deux
lignes, puis faire occuper toute la hauteur au bouton.

`align-items-end` est également utilisé sur les lignes d'édition en ligne (`:107`, `:206`) où il est
correct : **ne pas y toucher**.

Vérifié aussi : **aucune ligne mono-champ n'existe côté export** (`ExportProfileEditor` n'a qu'un champ
racine, `#export-profile-name-input`). L'asymétrie est factuelle, comme la documente déjà
`ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion`.

### Remarque 7 — cause racine confirmée à la ligne près

Deux formes coexistent dans le code, et elles expliquent exactement les deux boutons cités par Simon :

- **Une ligne, espace littéral dans la source → espace rendu** : `@((MarkupString)AdminIconMarkup.Plus) @Loc[…]`
  — `SheetRuleForm.razor:197` (`add-unconditional-colonne-button`), `:329` (`add-point-rule-button`),
  `ImportProfileEditor.razor:183`, `:278`, `:431`, `ExportProfileEditor.razor:183`. **6 boutons.**
- **Icône dans un bloc `@if`, libellé sur la ligne suivante → aucun espace rendu** :
  ```razor
  @if (ShowCancel) { @((MarkupString)AdminIconMarkup.Check) } else { @((MarkupString)AdminIconMarkup.Plus) }
  @SubmitLabel
  ```
  — les **8** sous-formulaires (`SheetRuleForm:462-470`, `BlockFieldForm:38-46`,
  `HeaderFieldRuleForm`, `HeaderCompositeRuleForm`, `SheetGenerationRuleForm`, `ColumnDefinitionForm`,
  `PointColumnDefinitionForm`, `ApplicationColumnDefinitionForm`). **8 boutons.**
- **Cas voisin** : `PageBackNavLink.razor` place `@((MarkupString)ArrowLeftIconMarkup)` puis
  `<span class="d-none d-md-inline">@Label</span>` sur la ligne suivante → icône collée au libellé sur
  desktop également. Ce composant porte déjà `aria-label` **et** `title` : rien à corriger de ce côté.

Corriger fichier par fichier reproduirait le défaut au prochain bouton. La correction doit rendre le
rendu **indépendant du blanc source** : conteneur flex + `gap`. Effet de bord bénéfique, qui n'était
pas garanti non plus : le centrage vertical de l'icône par rapport au texte.

### Précédent technique pour le test de contraste (58.1)

`tests/ExcelETL.BlazorAdmin.Tests/Configuration/ConnectionStringConfigurationTests.cs` remonte
l'arborescence depuis `AppContext.BaseDirectory` jusqu'au marqueur `ExcelETL.slnx` puis lit un fichier
de `src/ExcelETL.BlazorAdmin/`. **Le test CSS de 58.1 réutilise exactement cet idiome** — il n'y a
aucun prérequis à vérifier, le mécanisme existe et fonctionne.

### Vérifications résiduelles

Non lus : le contenu de `BlazorAdminMessages.resx`/`.fr.resx`, et les pages hors éditeurs
(`ImportProfiles.razor`, `ExportProfiles.razor`, `ApiTest.razor`, `ImportProfileTest.razor`,
`ExportProfileTest.razor`), qui portent elles aussi des boutons icône + libellé depuis le lot 041
(`create-profile-button`, `process-button`, `generate-workbook-button`). Le périmètre exact de 58.3 se
ferme par un `grep -rn 'MarkupString)AdminIconMarkup' src/ExcelETL.BlazorAdmin/Components/` au début du
sous-ticket.

---

## Décisions actées avec Simon (29/07)

| Sujet | Décision |
| :--- | :--- |
| Teinte des boutons d'ajout | **Bouton tonal M3** : fond `secondary-container`, texte `on-secondary-container`. Reste dans la famille rouge de la marque et distinct du CTA rouge plein. |
| Gris neutre / contour rouge | **Écartés** — le gris reste terne ; le contour rouge rouvrirait le défaut « on confond le bouton avec un champ désactivé » que 53.4 venait de corriger. |
| Refonte complète de la charte graphique | **Hors de ce lot, gardée en mémoire** — conversation dédiée (jeu complet de jetons, thème clair et sombre, contrastes). |
| Hauteur | **Le bouton prend exactement la hauteur du champ adjacent**, au-dessus de 768px seulement. |
| Espace icône/libellé | **Gabarit unique** pour tout bouton icône + libellé, et **règle ajoutée à la convention** pour que ça ne se reproduise pas. |

### Le thème sombre : décision retenue et variante

Puisque le sombre ne souffre pas du défaut (constat ci-dessus), deux mappages sont possibles :

**Variante A — tonal canonique dans les deux thèmes (retenue)**

| Jeton | Clair | Sombre |
| :--- | :--- | :--- |
| `--m3-secondary-container` | `#FFDAD6` | `#5D3F3B` |
| `--m3-on-secondary-container` | `#2C1512` | `#FFDAD6` |
| `--m3-secondary-container-rgb` | `255, 218, 214` | `93, 63, 59` |

Contrastes calculés : **13,3:1** en clair, **7,3:1** en sombre — au-delà de AA (4,5:1) et de AAA (7:1).
Ce sont les valeurs baseline M3 de la famille rouge du projet, pas un choix esthétique nouveau, et le
rôle du jeton reste vrai dans les deux thèmes.

**Conséquence à assumer** : en thème sombre, le bouton passe d'un fond rosé clair à un **fond brun
foncé** avec texte clair. Ce n'est pas une régression au sens du contraste, et c'est cohérent avec
« action secondaire moins criante », mais **c'est un changement visible** sur un thème qui ne posait pas
problème.

**Variante B — thème clair seul** : aligner `--m3-secondary-container` du sombre sur les valeurs
actuelles (`#E7BDB8` / `#442925`), ce qui laisse le sombre pixel pour pixel identique. Le prix est un
jeton nommé « container » qui ne porte pas la valeur canonique du rôle en sombre — une petite fausseté
inscrite dans le fichier qui sert précisément de référence de palette.

**Variante A retenue**, à confirmer d'un mot avant implémentation si le rendu sombre importe.

---

## Décisions antérieures explicitement rouvertes par ce lot

- **53.4 (boutons d'ajout en `btn-secondary` plein + icône « + »)** → rouvert **sur la teinte
  uniquement**. Le rôle (plein, secondaire, distinct du CTA), la classe (`btn-secondary`), l'icône
  centralisée et l'accessibilité (`aria-hidden` sur le `<svg>`, pas d'`aria-label`) sont **conservés**.
  Aucune classe de bouton ne change en 58.1 : les tests de 53.4 restent verts sans modification.
- **53.3 (ligne unique champ + bouton pour les formulaires mono-champ)** → rouvert sur **l'alignement
  vertical de la ligne** (`align-items-end`) et la hauteur du bouton. La grille
  `row` / `col-12 col-md` / `col-12 col-md-auto`, l'absence de `.right-aligned-actions` et
  l'interdiction d'`input-group` sont **conservées**.
- **`convention-ui-blazor-icones-boutons.md`** → **amendée** par 58.3 (nouvelle section « Icône +
  libellé : gabarit unique »). C'est le premier lot qui modifie cette convention plutôt que de s'y
  conformer ; le lot 053 avait au contraire noté qu'aucun amendement n'était nécessaire pour son
  périmètre.

Tout le reste des lots 030 / 041 / 053 / 056 / 057 reste fermé.

---

## Conventions déjà en place à respecter (tout le lot)

- `convention-ui-blazor-icones-boutons.md` — amendée par 58.3, respectée partout ailleurs : un bouton
  qui conserve un libellé texte visible garde son icône **décorative** (`aria-hidden="true"`), sans
  `aria-label` ni `title`.
- `convention-ui-blazor-alignement-boutons.md` — inchangée, y compris son paragraphe sur les boutons
  intégrés à une ligne de saisie, qui couvre exactement le cas de 58.2.
- Aucune nouvelle dépendance CSS/JS. Bootstrap + `theme-m3.css` uniquement ; CSS custom réduit au
  strict nécessaire et **centralisé dans `app.css`**, jamais dupliqué dans deux `.razor.css`.
- bUnit ne calcule **ni couleur ni layout** : les tests portent sur des classes, sur la structure DOM,
  et — nouveauté de ce lot — sur le **contenu déclaré des fichiers CSS** lus comme fichiers.
- xUnit 2.9.3 + FluentAssertions **7.x** (v8+ interdite) + Moq + bUnit 2.7.2.
- Strict Red-Green-Refactor.

---

## Hors périmètre explicite (tout le lot)

- **La refonte de la charte graphique** — conversation dédiée. Ce lot corrige **un** mappage de jeton,
  il ne redéfinit pas la palette.
- **`--m3-primary` et le CTA final** — non touchés (`btn-primary btn-lg`, 53.4).
- **`--m3-secondary`, `--bs-secondary`, `.btn-outline-secondary`, `.text-bg-secondary`** — **non
  redéfinis**. Seul `.btn-secondary` (fond plein) est remappé. C'est la contrainte principale de 58.1.
- **Les boutons « Annuler »** des sous-formulaires — leur distinction d'avec le bouton de soumission est
  traitée au lot 056 (56.7), pas ici.
- **`ReconnectModal.razor.css`** et ses couleurs hexadécimales figées hors thème (§2.3 et §3.1 de
  `audit-design-blazoradmin-2026-07-27.md`) — écart réel, mais composant framework.
- **L'écart §2.2 de l'audit design** (CTA principaux sans icône) — ce lot ne **pose aucune icône
  nouvelle**. Il n'agit que sur les boutons portant **déjà** icône + libellé.
- **La centralisation des icônes Pencil/Trash/croix inline** dupliquées dans les sous-formulaires
  (§2.4 de l'audit) — hors périmètre, comme aux lots 053 et 057.
- **`input-group`** — interdit (rouvrirait 30.6).
- **L'intérieur des `form-floating`** — non touché : 58.2 agit sur la ligne et sur le bouton, jamais sur
  le champ.
- **Le thème sombre au-delà des jetons de 58.1** — aucun autre composant n'est revu en sombre.
- **Toute modification Domain / Application / pipeline** — ce lot est strictement CSS + Razor +
  convention + tests.

---

## 58.1. Bouton tonal : `.btn-secondary` sur `secondary-container`, avec test de contraste

**Comportement attendu** :
- Trois jetons neufs déclarés dans `theme-m3.css`, dans le bloc clair **et** dans le bloc sombre :
  `--m3-secondary-container`, `--m3-on-secondary-container`, `--m3-secondary-container-rgb` (le
  troisième est nécessaire : `.btn-secondary` alimente `--bs-btn-focus-shadow-rgb` avec un triplet RGB,
  cf. `:242`).
- `.btn-secondary` (`:235-248`) est remappé sur ces jetons, **sans changer sa structure** : les
  `color-mix(… , black)` de hover/active sont conservés tels quels, appliqués au nouveau fond.
- **Aucune autre règle n'est modifiée** : `--m3-secondary`, `--bs-secondary`,
  `.btn-outline-secondary`, `.text-bg-secondary` restent identiques au caractère près.
- **Aucune classe ne change côté Razor.** Ce sous-ticket ne touche qu'un fichier CSS.

**Tests** — ce sous-ticket n'est **pas** testable en bUnit (aucun calcul de couleur). Deux tests
d'audit sur le fichier CSS lu comme fichier, dans un nouveau fichier de
`tests/ExcelETL.BlazorAdmin.Tests/`, réutilisant l'idiome de remontée vers `ExcelETL.slnx` de
`ConnectionStringConfigurationTests` :

1. **Déclaration** : les trois jetons sont déclarés dans le bloc clair **et** dans le bloc sombre, et
   la règle `.btn-secondary` **référence** `--m3-secondary-container` / `--m3-on-secondary-container`
   (et non une valeur hexadécimale littérale). Garde-fou contre un jeton déclaré mais jamais consommé,
   et contre une valeur en dur qui contournerait le thème.
2. **Contraste** : les deux couples (clair, sombre) sont extraits du fichier, convertis, et leur ratio
   de contraste WCAG 2.1 est calculé — **assertion `>= 4.5`** pour chacun. La fonction de calcul vit
   dans le **projet de test**, pas dans le code de production : elle verrouille une propriété
   d'accessibilité, ce n'est pas une fonctionnalité de l'application.
3. **Non-régression** : `.btn-outline-secondary` et `.text-bg-secondary` référencent toujours
   `--m3-secondary` / `--m3-on-secondary` — assertion textuelle sur le fichier, qui échoue si le
   remappage a débordé.

Le test 2 est la vraie valeur du sous-ticket : il transforme « on a vérifié les contrastes le 29/07 »
en garde-fou permanent, applicable à toute future retouche de palette — y compris la refonte complète,
quand elle viendra.

**Vérification manuelle attendue** (à consigner) : rendu du bouton dans les deux thèmes, et
vérification que les badges, les boutons « Annuler » et les boutons icône seule n'ont **pas** changé
d'apparence.

**Effort** : standard. Le seul piège est le périmètre du remappage.

**Dossier** : `src/ExcelETL.BlazorAdmin/wwwroot/css/theme-m3.css` (+ nouveau fichier de tests d'audit
CSS).

---

## 58.2. Hauteur du bouton égale à celle du champ adjacent

**Périmètre strict** : les **deux** lignes mono-champ de `ImportProfileEditor.razor` — « Tableaux »
(`:173-186`) et « Applications » (`:268-281`). Ce sont les seuls endroits du projet où un bouton est
côte à côte avec un champ de saisie, et il n'existe **aucun équivalent côté export**.

**Comportement attendu** :
- Sur ces deux lignes **uniquement**, `align-items-end` est retiré (ou remplacé par l'étirement) afin
  que la colonne du bouton prenne la hauteur de la ligne. Les lignes d'édition en ligne (`:107`,
  `:206`), qui utilisent le même utilitaire à bon escient, ne sont **pas** touchées.
- Au-dessus de 768px, le bouton occupe **toute la hauteur de sa colonne**, donc exactement celle du
  `form-floating` voisin.
- Sous 768px, le bouton retrouve sa **hauteur naturelle** : les deux éléments sont empilés, un bouton de
  58px de haut y serait disgracieux et rouvrirait le mobile-first acté aux lots V / 030 / 53.5.
- Bootstrap n'a pas d'utilitaire `h-md-100` : **une** règle dans `app.css`, sous
  `@media (min-width: 768px)`, appliquée à une classe dédiée (nom proposé : `.field-inline-action`).
  Jamais dupliquée dans un `.razor.css`.
- **Pas d'`input-group`**, **aucune** modification de l'intérieur du `form-floating`, **aucune** hauteur
  fixe en pixels sur le bouton (elle divergerait du champ à la première modification de police ou de
  padding).
- `w-100 w-md-auto` est conservé sur les deux boutons :
  `AllAddButtons_CarryW100_OnBothEditors` doit rester vert sans modification.

**Tests** (bUnit) — **rouges d'abord** :
- `#add-default-tableau-button` et `#add-default-application-name-button` portent
  `.field-inline-action`, et conservent `btn btn-secondary w-100 w-md-auto`.
- La ligne parente de chacun ne porte **plus** `align-items-end`.
- Les lignes d'édition en ligne (`#default-tableau-edit-input-0` et son conteneur) portent **toujours**
  `align-items-end` — garde-fou contre un retrait trop large de l'utilitaire.
- **Garde-fou de non-généralisation** : test paramétré sur les autres boutons d'ajout → **aucun** ne
  porte `.field-inline-action`. Sans lui, la classe finira sur tous les boutons au premier
  copier-coller, et « toute la hauteur de la colonne » n'a aucun sens pour un bouton en bas de
  formulaire.
- Aucun des deux boutons ne porte de hauteur en attribut `style`.

**Vérification manuelle attendue** (à consigner) : à ≥768px, bords supérieur et inférieur du bouton
alignés sur ceux du champ ; à <768px, bouton de hauteur normale, non déformé.

**Effort** : standard.

**Dossier** : `ImportProfileEditor.razor`, `wwwroot/app.css`.

---

## 58.3. Gabarit unique pour tout bouton icône + libellé

**Comportement attendu** :
- Tout bouton portant **à la fois** une icône et un libellé texte utilise le même gabarit de
  conteneur : `d-flex align-items-center justify-content-center gap-1`.
- Conséquences :
  - l'espacement icône/texte vient du `gap`, **plus jamais du blanc de la source Razor** — le défaut de
    la remarque 7 devient structurellement impossible ;
  - l'icône est centrée verticalement sur le texte, ce qui n'était pas garanti non plus ;
  - `d-flex` (plutôt que `d-inline-flex`) fonctionne aussi bien avec `w-100` qu'avec `w-md-auto` grâce à
    `justify-content-center` : **une seule** combinaison à retenir et à tester pour tous les cas. Sur un
    bouton de largeur naturelle, `justify-content-center` est simplement sans effet observable.
- Périmètre : les **15 boutons** relevés dans les éditeurs (6 à espace littéral + 8 sous-formulaires +
  `PageBackNavLink`), **plus** le bouton bascule introduit au lot 057, **plus** les boutons icône +
  libellé des autres pages BlazorAdmin, à énumérer par le `grep` indiqué dans les vérifications
  résiduelles. Aucune icône n'est ajoutée ni retirée ; aucun libellé n'est modifié.

**Amendement de convention** — `convention-ui-blazor-icones-boutons.md` reçoit une section
« Icône + libellé : gabarit unique » qui énonce :
- la chaîne d'utilitaires exacte à utiliser ;
- l'interdiction de faire reposer l'espacement sur un blanc de source Razor ou sur une marge posée au
  cas par cas (`ms-1` sur le libellé, `me-1` dans la constante d'icône) ;
- le rappel que l'icône reste décorative (`aria-hidden="true"`), sans `aria-label` ni `title`, dès lors
  qu'un libellé texte visible subsiste.
- **Au passage** : la convention documente encore les classes `bi bi-*` de Bootstrap Icons alors que le
  projet n'utilise **que** du SVG inline — `AdminIconMarkup.cs:5-7` le dit explicitement (*« No
  bootstrap-icons font/CSS is loaded anywhere in this project »*) et §2.2 de
  `audit-design-blazoradmin-2026-07-27.md` l'avait relevé. Ce lot corrige **ce paragraphe** (une
  phrase : le mécanisme réel est le SVG inline centralisé dans `AdminIconMarkup`), puisqu'il édite déjà
  ce fichier. **Rien d'autre** de la convention n'est réécrit.

**Tests** (bUnit) — **rouges d'abord** :
- Test **paramétré** sur tous les `id` de boutons icône + libellé du périmètre : chacun porte les quatre
  utilitaires du gabarit. C'est ce test qui sert de garde-fou pour les boutons futurs.
- Chaque bouton du périmètre contient **exactement un** `<svg>` portant `aria-hidden="true"`, et
  conserve un libellé texte non vide (non-régression 53.4 : on n'a pas glissé vers un bouton icône
  seule, qui exigerait `aria-label` + `title`).
- L'icône provient bien d'`AdminIconMarkup` : comparaison stricte du markup `<svg>` rendu par deux
  boutons issus de **fichiers différents** (test de 53.4 conservé, étendu aux boutons ajoutés depuis).
- Non-régression : les chaînes de classes posées par 53.3 / 53.4 / 56.7 (`btn-secondary`, `w-100`,
  `w-md-auto`, `mt-3`) sont **conservées** — le gabarit s'ajoute, il ne remplace rien.
- `PageBackNavLink` conserve `aria-label` **et** `title` (il est icône seule sous 768px, son libellé
  étant `d-none d-md-inline`) — non-régression explicite.
- Parité import/export sur au moins un bouton équivalent de chaque côté.

**Effort** : standard pour le rouge et le vert. **Élevé au refactor** uniquement si le `grep` de
périmètre révèle d'autres variantes de markup que les trois décrites au constat — dans ce cas, la
question à trancher est « combien de gabarits distincts existent réellement », et elle mérite un moment
de réflexion avant de tout uniformiser à l'aveugle.

**Dossier** : les deux éditeurs, leurs 8 sous-formulaires, `PageBackNavLink.razor`, les pages
identifiées par le `grep`, et `docs/conventions/convention-ui-blazor-icones-boutons.md`.

---

## 58.4. Parité structurelle import/export (clôture)

**Comportement attendu** : `ProfileEditorParityTests.cs` est étendu au **gabarit icône + libellé** des
boutons d'ajout équivalents des deux éditeurs. La teinte (58.1) n'y figure pas : elle est portée par une
classe déjà comparée, donc déjà couverte. La hauteur (58.2) n'y figure pas non plus : elle est
import-only, et l'asymétrie se documente comme le fait déjà
`ShortFieldGrid_HasNoExportCounterpart_ImportOnlyAssertion`.

**Tests** (bUnit) :
- Comparaison de chaîne **stricte** des classes des boutons d'ajout équivalents, gabarit inclus.
- Ce test est **le dernier rendu vert du lot**.

**Effort** : standard.

**Dossier** : `tests/ExcelETL.BlazorAdmin.Tests/Pages/Admin/ProfileEditorParityTests.cs`.

---

## Ordre recommandé

1. **58.1** — teinte tonale + tests d'audit CSS (indépendant du reste, effet visible immédiat)
2. **58.2** — alignement de la ligne et hauteur du bouton
3. **58.3** — gabarit icône + libellé et amendement de convention
4. **58.4** — parité structurelle (clôture)

## Note d'efficacité d'implémentation (Claude Code)

- **Ne pas redéfinir `--m3-secondary`.** C'est le raccourci tentant de 58.1 et il repeint au passage les
  boutons « Annuler », les badges et les contours. Trois jetons neufs, un seul sélecteur remappé — et le
  test de non-régression textuelle du point 3 est là pour le prouver.
- **Le thème sombre change d'aspect** (variante A). Si ce n'est pas voulu, la variante B est écrite
  ci-dessus : le signaler **avant** d'implémenter, pas après.
- **En 58.2, retirer `align-items-end` est la moitié du correctif**, et un `h-100` seul n'aurait aucun
  effet. Ne pas l'appliquer aux lignes d'édition en ligne, qui en ont besoin.
- **Pas de hauteur en pixels, pas d'`input-group`** : la hauteur doit **suivre** le champ, pas la
  répliquer par coïncidence.
- **Le garde-fou de non-généralisation de 58.2 n'est pas décoratif.** Sans lui, `.field-inline-action`
  finira sur tous les boutons d'ajout au premier copier-coller.
- **En 58.3, ne pas « corriger » l'espacement fichier par fichier** — c'est exactement ce que le gabarit
  rend inutile. Un bouton qui a l'air correct sans le gabarit l'a par accident (il est dans les 6 à
  espace littéral).
- **Fermer le périmètre de 58.3 par un `grep` avant d'écrire les tests**, pas en cours de route : le
  test paramétré vaut par son exhaustivité.
- Tests en sortie minimale et filtrée :
  `dotnet test --filter "FullyQualifiedName~ProfileEditor|FullyQualifiedName~Theme|FullyQualifiedName~Icon" --verbosity quiet`.
- **Effort standard sur tout le lot**, sauf éventuellement le refactor de 58.3 (voir le sous-ticket).

**Dossiers concernés** :
`src/ExcelETL.BlazorAdmin/wwwroot/css/theme-m3.css`,
`src/ExcelETL.BlazorAdmin/wwwroot/app.css`,
`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/` (les deux éditeurs et leurs 8 sous-formulaires),
`src/ExcelETL.BlazorAdmin/Components/Layout/PageBackNavLink.razor`,
`docs/conventions/convention-ui-blazor-icones-boutons.md`,
et le miroir `tests/ExcelETL.BlazorAdmin.Tests/`.
