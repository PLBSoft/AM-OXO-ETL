# Tickets TDD — Lot 060 : palette Material Design 3 complète, sans dette de couleurs codées en dur

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 059.*

**Origine** : revue de la palette de couleurs de `ExcelETL.BlazorAdmin` demandée par Simon (30/07,
en session Claude/chat). Le fichier `src/ExcelETL.BlazorAdmin/wwwroot/css/theme-m3.css` (421 lignes)
suit bien les recommandations Material Design 3 — tokens `--m3-*` bridgés sur les variables Bootstrap
5.3 `--bs-*`, deux blocs `[data-bs-theme="light"|"dark"]`, états hover/active/disabled dérivés via
`color-mix()`. La revue a mis en évidence deux choses distinctes, traitées dans ce lot :

1. Une **dette réelle** : 4 fichiers CSS du projet contiennent des couleurs codées en dur ou des RGB
   Bootstrap par défaut, hors du système de tokens `--m3-*` — ils ne suivent donc pas le thème sombre.
2. Un **modèle M3 incomplet** : il manque les paires `container`/`on-container` pour plusieurs rôles,
   et le client a signalé ne pas aimer la couleur secondaire actuelle (un marron/taupe terne).

Simon a validé : on commence par la suppression de la dette (60.1), puis on complète le modèle
(60.2), puis on remplace le secondaire (60.3) et on ajoute un rôle tertiary coordonné (60.4).

---

## 60.0 — Constats vérifiés (déjà faits en session chat, pas à refaire par Claude Code)

Tous les points ci-dessous ont été **lus directement dans le dépôt** (`C:\AM-OXO-ETL`, commit
`83109a5`, 2026-07-30) avant rédaction. Les numéros de ligne sont ceux de ce commit : repères, pas
contrat — vérifier qu'ils n'ont pas dérivé avant d'éditer.

1. **Système de tokens sain, aucune duplication de littéral trouvée.** Chaque composant Bootstrap
   (`.btn-primary`, `.btn-secondary`, `.text-bg-*`, etc.) lit `var(--m3-*)` — aucune couleur hex n'est
   dupliquée en dehors des deux déclarations racine (`grep -rn "775652\|E7BDB8" wwwroot/` : zéro
   occurrence en dehors des lignes 12/113 de `theme-m3.css`). Conséquence directe : changer la
   *valeur* d'un token aux lignes 60.3/60.4 ci-dessous suffit, aucun autre fichier n'a besoin d'édition
   pour ce changement de teinte.

2. **4 fichiers CSS hors du système de tokens**, sourcés fichier + ligne + valeur :
   - `Components/Layout/MainLayout.razor.css:18-19` — `.top-row { background-color: #f7f7f7; border-bottom: 1px solid #d6d5d5; }`. Reste gris clair figé en thème sombre.
   - `Components/Layout/ReconnectModal.razor.css:93,100,104,115` — `#6b9ed2`/`#3b6ea2`/`#6b9ed2`/`#0087ff` (bleu Blazor par défaut, jamais raccordé à la marque ni au thème sombre — dette déjà notée dans l'historique du lot 058, non traitée jusqu'ici).
   - `wwwroot/app.css:26` — `.blazor-error-boundary` se termine par `, #b32121;` (rouge codé en dur, proche mais distinct de `--m3-danger`).
   - `wwwroot/app.css:36` — `.darker-border-checkbox.form-check-input { border-color: #929292; }`.
   - `Components/Pages/Admin/Logs.razor.css:3,8` — `.log-row-error`/`.log-row-warning` : bordure gauche en `var(--bs-danger)`/`var(--bs-warning)` (correct) mais fond en `rgba(220, 53, 69, 0.08)`/`rgba(255, 193, 7, 0.08)` — ce sont les RGB **Bootstrap par défaut**, pas `var(--m3-danger-rgb)`/`var(--m3-warning-rgb)` (qui valent `186, 26, 26`/`255, 117, 24` en thème clair, tout autre chose en thème sombre).

3. **Exclus de la dette, vérifié et écarté** : `ImportProfileTest.razor.css:46` et
   `ExportProfileTest.razor.css:58` contiennent tous les deux `var(--bs-table-bg, #fff)` — c'est une
   valeur de repli standard sur une variable déjà correcte, pas une couleur codée en dur qui ignore le
   thème. Aucune action requise ici.

4. **Rôles M3 manquants dans le fichier** : pas de `primary-container`/`on-primary-container`, pas de
   `tertiary` (aucune occurrence de `tertiary` dans tout `theme-m3.css`), pas de container pour
   `success`/`warning`/`info`. Seul `secondary-container` existe (ajouté au lot 058 pour corriger un
   bug de contraste ponctuel, pas dans une démarche de complétude).

5. **Chaîne de teinte du secondaire actuel confirmée algorithmique, pas arbitraire.** En rejouant
   l'algorithme M3 officiel (Material Theme Builder / espace colorimétrique HCT, bibliothèque de
   référence `materialyoucolor`) à partir du seed `--m3-primary: #D81F11`, la variante par défaut
   (« TonalSpot ») ressort `#785752` en secondaire clair — quasi identique au `#775652` déjà dans le
   fichier. Le marron/taupe n'est donc pas une erreur de saisie : c'est le résultat correct de cette
   variante sur un seed rouge (réduction de chroma à teinte égale → brun terne perçu par l'œil humain
   sur les rouges chauds). Décision actée avec Simon (30/07) : remplacer par la variante **Expressive**
   du même algorithme, qui décale la teinte du secondaire (et coordonne un tertiary) au lieu de
   simplement désaturer — voir 60.3/60.4. Écartées et pourquoi : rose (« Vibrant », déjà signalé
   négativement par le client) et rouge-orange (« Fidelity/Content », jugé trop proche visuellement
   d'un warning).

6. **Risque de proximité de teinte accepté, pas résolu.** Le secondaire Expressive retenu a une teinte
   HCT de 226.7°, le tertiary Expressive coordonné 222.0°, le `info` déjà existant (`#008c94`) 203.6°
   — les trois dans une fenêtre de 23°, donc visuellement voisins (famille bleu/sarcelle). Décision
   actée avec Simon (30/07) : on ajoute quand même le tertiary Expressive tel quel, on ne retouche pas
   `info`. Si retour client négatif après livraison, rouvrir uniquement la question du `info` — ne pas
   revenir sur le secondaire ni le tertiary sans nouvelle instruction explicite.

7. **Piège de nommage à ne pas reproduire** : Bootstrap 5.3 possède déjà `--bs-tertiary-bg`/
   `--bs-tertiary-color` — des tokens de gris neutre utilitaires (fonds de tableau zébrés, éléments
   désactivés), **sans aucun rapport** avec le rôle d'accent `tertiary` de Material Design 3. Ne pas
   remapper `--bs-tertiary-bg`/`--bs-tertiary-color` sur `--m3-tertiary` en 60.4 : ce serait fusionner
   deux concepts différents et casser l'utilitaire neutre existant de Bootstrap.

---

## 60.1 — Suppression de la dette : couleurs codées en dur / RGB Bootstrap par défaut

Un cycle rouge/vert par fichier — ce sont 4 comportements indépendants, pas un seul.

**Effort** : standard pour les 4 (remplacement mécanique de valeur, pas de décision architecturale).

Test conventions à réutiliser telles quelles (ne pas réinventer) :
`tests/ExcelETL.BlazorAdmin.Tests/Styling/ThemeSecondaryButtonContrastTests.cs` (helpers
`ExtractRule`/`ExtractBlock`/`ExtractHexValue`, lecture texte brut du CSS, pas de bUnit — aucun calcul
de couleur/layout n'est possible en bUnit) et
`Styling/AppCssSheetRuleSublistDetailsColorTests.cs` (même idiome appliqué à `app.css`). Un nouveau
fichier de test par composant CSS touché, dans `tests/ExcelETL.BlazorAdmin.Tests/Styling/`.

### 60.1.a — `MainLayout.razor.css` : `.top-row` suit le thème

- **Rouge** : nouveau `MainLayoutRazorCssTopRowColorTests.cs`. Assertions : la règle `.top-row`
  contient `var(--bs-body-bg)` (ou `var(--m3-surface)` — trancher au vu du rendu voulu : le bandeau
  du haut doit-il se fondre dans le fond de page, ou rester une surface visuellement distincte ? à
  défaut d'instruction, préférer `var(--bs-body-bg)` pour la couleur de fond et `var(--bs-border-color)`
  pour la bordure, cohérent avec le rôle générique de ce bandeau) ; la règle ne contient plus
  `#f7f7f7` ni `#d6d5d5` (regex `#[0-9A-Fa-f]{3,8}` absente de la règle extraite).
- **Vert** : remplacer les deux valeurs par les tokens choisis.
- **Refactor** : aucun — changement d'une seule règle, pas de piste de factorisation identifiée.

### 60.1.b — `ReconnectModal.razor.css` : suit la marque et le thème

- **Rouge** : nouveau `ReconnectModalRazorCssColorTests.cs`. Assertions : `#components-reconnect-modal button`
  référence `var(--m3-primary)`/`var(--m3-on-primary)` (c'est un bouton d'action, même rôle que
  `.btn-primary` ailleurs dans l'appli — pas de nouveau rôle sémantique à inventer) ; son état `:hover`
  référence une variante `color-mix()` de `--m3-primary` (même mécanique que `.btn-primary` dans
  `theme-m3.css:220-226`, à reproduire ici plutôt qu'un nouveau hex fixe) ; `.components-rejoining-animation div`
  référence `var(--m3-primary)` à la place de `#0087ff` ; aucune des 4 valeurs hex d'origine ne
  subsiste dans le fichier.
- **Vert** : remplacer les 4 occurrences par les tokens/`color-mix()` équivalents.
- **Refactor** : ce fichier est un composant scaffoldé par défaut (`components-reconnect-modal`),
  jamais touché depuis — vérifier qu'aucune autre règle du même fichier ne contient une couleur codée
  en dur oubliée par le grep initial avant de clore.

### 60.1.c — `app.css` : `.blazor-error-boundary` et `.darker-border-checkbox`

- **Rouge** : nouveau `AppCssScaffoldedColorTests.cs`. Assertions : `.blazor-error-boundary` référence
  `var(--m3-danger)` à la place de `#b32121` (c'est un bandeau d'erreur, même rôle sémantique que
  `--bs-danger` ailleurs) ; `.darker-border-checkbox.form-check-input` référence `var(--bs-border-color)`
  à la place de `#929292` (c'est une bordure, pas une couleur d'accent — réutiliser le token de bordure
  déjà mappé sur `--m3-border`, pas `--m3-secondary`).
- **Vert** : remplacer les deux valeurs.
- **Refactor** : aucun.

### 60.1.d — `Logs.razor.css` : fonds de ligne alignés sur les vraies teintes M3

- **Rouge** : nouveau `LogsRazorCssRowColorTests.cs`. Assertions : `.log-row-error` référence
  `rgba(var(--m3-danger-rgb), 0.08)` (pas `rgba(220, 53, 69, ...)`) ; `.log-row-warning` référence
  `rgba(var(--m3-warning-rgb), 0.08)` (pas `rgba(255, 193, 7, ...)`) ; la bordure gauche des deux
  règles continue de référencer `var(--bs-danger)`/`var(--bs-warning)` (non-régression — ne pas
  toucher aux lignes 2/7 de `Logs.razor.css`).
- **Vert** : remplacer les deux littéraux RGB par `var(--m3-danger-rgb)`/`var(--m3-warning-rgb)` —
  ces deux tokens existent déjà dans `theme-m3.css` (lignes 27/33 clair, 128/134 sombre), aucune
  déclaration à ajouter.
- **Refactor** : aucun.

---

## 60.2 — Compléter le modèle M3 : paires container/on-container pour les ancres existantes

**Ne touche à aucune couleur de base** (`primary`/`danger`/`success`/`warning`/`info` restent
pixel-identiques). Ajoute uniquement les paires `container`/`on-container` manquantes, calculées avec
le même algorithme (palette tonale HCT, seed = la couleur de base **actuelle**, tons standard M3 —
90/10 en clair, 30/90 en sombre) que celui déjà utilisé au lot 058 pour `secondary-container`.

**Décision de périmètre (à respecter, pas à réinterpréter)** : ces tokens sont **déclarés mais pas
câblés** dans un nouveau composant Bootstrap ce lot-ci (pas de nouveau `.btn-primary-container` ou
équivalent). Aucun consommateur n'existe aujourd'hui pour ces containers-là — les ajouter sans les
câbler correspond à la demande explicite de Simon (« compléter le fichier pour avoir un modèle M3
complet »), le câblage viendra dans un lot séparé si un besoin d'UI apparaît. Par cohérence YAGNI
(cf. `memory.md`, principe déjà invoqué plusieurs fois dans ce projet), **ne pas ajouter les variantes
`-rgb`** de ces nouveaux containers : `--m3-secondary-container-rgb` n'existe que parce que
`.btn-secondary` le consomme réellement via `--bs-btn-focus-shadow-rgb` (lot 058) — sans consommateur
équivalent ici, une variante `-rgb` non utilisée est un ajout spéculatif à éviter.

**Effort** : standard (valeurs déjà calculées ci-dessous, aucun arbitrage restant).

Valeurs à ajouter (une ligne `--m3-{role}-container`/`--m3-on-{role}-container` par rôle et par
thème, à la suite de la déclaration de base de chaque rôle, même structure que `secondary-container`
au lot 058) :

| Rôle | Container (clair) | On-container (clair) | Container (sombre) | On-container (sombre) |
|---|---|---|---|---|
| primary | `#FFDAD4` | `#410100` | `#930200` | `#FFDAD4` |
| danger (error) | `#FFDAD5` | `#410002` | `#930009` | `#FFDAD5` |
| success | `#A3F69C` | `#002204` | `#005312` | `#A3F69C` |
| warning | `#FFDBCB` | `#341100` | `#783100` | `#FFDBCB` |
| info | `#8CF2FB` | `#002022` | `#004F54` | `#8CF2FB` |

- **Rouge** : nouveau `ThemePrimaryDangerSuccessWarningInfoContainerTests.cs` (même dossier
  `Styling/`, même idiome que `ThemeSecondaryButtonContrastTests.cs`) : pour chacun des 5 rôles ×
  2 thèmes, assertion que `--m3-{role}-container`/`--m3-on-{role}-container` sont déclarés avec les
  valeurs ci-dessus, et que le contraste `on-container`/`container` est ≥ 4.5:1 (réutiliser la méthode
  `ContrastRatio` existante — ne pas la dupliquer). Ajouter aussi une assertion de non-régression :
  `--m3-primary`/`--m3-danger`/`--m3-success`/`--m3-warning`/`--m3-info` (les couleurs de base, pas
  les containers) gardent exactement leur valeur actuelle dans les deux thèmes — ce test doit rester
  vert après 60.3/60.4 puisque ceux-ci ne touchent que `secondary`/`tertiary`.
- **Vert** : ajouter les 20 lignes (5 rôles × 2 tons × 2 thèmes) dans `theme-m3.css`.
- **Refactor** : vérifier qu'aucun commentaire du lot 058 sur `secondary-container` ne devient trompeur
  une fois que d'autres containers existent à côté (par ex. le commentaire ligne 15-18 dit « distinct
  from --m3-secondary above » — toujours vrai, mais s'assurer que la lecture reste claire une fois le
  fichier plus dense).

---

## 60.3 — Nouvelle couleur secondaire (variante M3 « Expressive », dérivée du rouge primaire)

Remplace les valeurs de `--m3-secondary`/`--m3-secondary-rgb`/`--m3-on-secondary` (lignes 12-14 clair,
113-115 sombre) et de `--m3-secondary-container`/`--m3-secondary-container-rgb`/
`--m3-on-secondary-container` (lignes 20-22 clair, 122-124 sombre). Aucun autre fichier à modifier
(60.0.1 — tout passe par `var()`).

Valeurs calculées (variante Expressive, seed `#D81F11`, bibliothèque `materialyoucolor`, contrastes
vérifiés ≥ 6:1 sur les 4 paires) :

| Token | Clair | Sombre |
|---|---|---|
| `--m3-secondary` | `#3E6474` (rgb 62, 100, 116) | `#B4CAD5` (rgb 180, 202, 213) |
| `--m3-on-secondary` | `#F2FAFF` (rgb 242, 250, 255) | `#2E434C` (rgb 46, 67, 76) |
| `--m3-secondary-container` | `#C4EBFF` (rgb 196, 235, 255) | `#132831` (rgb 19, 40, 49) |
| `--m3-on-secondary-container` | `#325868` (rgb 50, 88, 104) | `#91A7B2` (rgb 145, 167, 178) |

- **Rouge** : nouveau `ThemeSecondaryHueReplacementTests.cs` (`Styling/`). Assertions : les 4 tokens
  ci-dessus valent exactement les nouvelles valeurs, dans les deux thèmes ; assertion négative que
  l'ancien `#775652`/`#E7BDB8`/`#FFDAD6`/`#2C1512`/`#5D3F3B` n'apparaît plus nulle part dans
  `theme-m3.css`. Ne **pas** dupliquer les 4 tests de contraste déjà présents dans
  `ThemeSecondaryButtonContrastTests.cs` (`LightThemeSecondaryContainerPair_MeetsWcagAaContrast` etc.)
  — ceux-ci sont déjà dynamiques (ils lisent la valeur courante du fichier, ne codent aucun hex en dur)
  et resteront verts sans modification une fois 60.3 appliqué ; les relancer suffit à prouver la
  non-régression de contraste, inutile de les réécrire.
- **Vert** : remplacer les 6 lignes (3 par thème) dans `theme-m3.css`.
- **Refactor** : mettre à jour le commentaire du lot 058 aux lignes 15-18/116-121 (`theme-m3.css`) qui
  documente encore l'ancienne teinte marron/taupe et sa justification de contraste — il devient
  trompeur une fois la teinte changée ; le réviser pour référencer ce lot 060 sans effacer
  l'explication historique du pourquoi de la paire container (toujours valable, seule la teinte
  change).

---

## 60.4 — Nouveau rôle `tertiary` (variante Expressive, coordonné avec le secondaire retenu)

Ajoute un rôle entièrement nouveau — absent du fichier aujourd'hui (60.0.4). Valeurs de la même
variante Expressive que 60.3, pour rester coordonné avec le secondaire déjà choisi (les deux teintes
sont calculées ensemble par l'algorithme à partir du même seed, pas piochées séparément).

| Token | Clair | Sombre |
|---|---|---|
| `--m3-tertiary` | `#00687F` (rgb 0, 104, 127) | `#88E0FF` (rgb 136, 224, 255) |
| `--m3-tertiary-rgb` | `0, 104, 127` | `136, 224, 255` |
| `--m3-on-tertiary` | `#F1FAFF` (rgb 241, 250, 255) | `#005063` (rgb 0, 80, 99) |
| `--m3-tertiary-container` | `#5BD5FA` (rgb 91, 213, 250) | `#5BD5FA` (rgb 91, 213, 250) |
| `--m3-on-tertiary-container` | `#004657` (rgb 0, 70, 87) | `#004657` (rgb 0, 70, 87) |

**Point vérifié, pas une erreur de recopie** : `tertiary-container`/`on-tertiary-container` calculent
la **même** valeur en clair et en sombre (comportement observé de la bibliothèque de référence pour ce
couple hue/chroma sous cette variante) — ne pas « corriger » en inventant une deuxième valeur pour le
thème sombre. Contraste vérifié ≥ 6:1 sur les 4 paires (secondary et tertiary).

**Rappel du piège 60.0.7** : ne pas toucher `--bs-tertiary-bg`/`--bs-tertiary-color` (déjà définis par
Bootstrap, rôle neutre sans rapport). `--m3-tertiary` est un nouveau token, pas un remplacement.

**Décision de périmètre** : comme pour 60.2, ces tokens sont déclarés sans câblage dans un nouveau
composant Bootstrap ce lot-ci — aucun `.btn-tertiary`/`.text-bg-tertiary-m3` n'est introduit. Ni
`--m3-tertiary-container-rgb` ni `--bs-*` remap ne sont ajoutés tant qu'aucun consommateur n'existe
(même raisonnement YAGNI que 60.2, sauf pour `--m3-tertiary-rgb` lui-même qui suit le même schéma que
`--m3-secondary-rgb`/`--m3-primary-rgb` — cohérent avec le fait que ce sont les seules couleurs de rôle
« accent » du fichier à porter systématiquement une variante `-rgb`, container compris ou non).

- **Rouge** : nouveau `ThemeTertiaryTokenTests.cs` (`Styling/`). Assertions : les 5 tokens (`tertiary`,
  `tertiary-rgb`, `on-tertiary`, `tertiary-container`, `on-tertiary-container`) sont déclarés dans les
  deux blocs de thème avec les valeurs ci-dessus ; contraste `tertiary`/`on-tertiary` et
  `tertiary-container`/`on-tertiary-container` ≥ 4.5:1 dans les deux thèmes ; `--bs-tertiary-bg`/
  `--bs-tertiary-color` (les tokens Bootstrap natifs) restent absents de tout remap `var(--m3-tertiary...)`
  — assertion de non-régression explicite contre le piège 60.0.7.
- **Vert** : ajouter les 10 lignes (5 tokens × 2 thèmes) dans `theme-m3.css`, à la suite de la
  déclaration `secondary-container` de chaque bloc (même emplacement logique que `secondary`, cohérent
  avec l'ordre déjà établi primary → secondary → success → danger → warning → info → background/surface).
- **Refactor** : aucun — nouveau rôle, pas de code existant à faire évoluer.

---

## 60.5 — Clôture : garde-fou de non-régression globale

- **Rouge** : nouveau `ThemeM3PaletteChangeScopeTests.cs` (`Styling/`). Un seul test paramétré,
  couvrant les deux thèmes : `--m3-primary`, `--m3-danger`, `--m3-success`, `--m3-warning`,
  `--m3-info` gardent **exactement** leur valeur d'avant ce lot (celles citées en 60.0.2-60.0.4/60.2).
  Ce test doit être écrit **avant** 60.1-60.4 et rester vert tout du long — s'il rougit à un moment du
  lot, c'est qu'une couleur hors périmètre a été touchée par erreur.
- **Vert** : rien à coder — ce test valide un existant qui ne doit pas bouger.
- **Refactor** : aucun.

Après 60.1-60.5, relancer `dotnet test --filter "FullyQualifiedName~Styling" --verbosity quiet` (cf.
`recommandations-tickets-tdd.md` §3) plutôt que la suite complète à chaque cycle ; suite complète une
seule fois en toute fin de lot.

---

## Hors périmètre

- **Câblage des nouveaux containers (60.2) et du rôle tertiary (60.4) dans un composant Bootstrap** —
  déclarés mais non consommés ce lot-ci ; revisiter seulement si un besoin d'UI concret apparaît
  (badge, bouton tonal, etc.).
- **`--bs-tertiary-bg`/`--bs-tertiary-color`** (rôle Bootstrap neutre existant) — jamais touché, jamais
  fusionné avec `--m3-tertiary`.
- **La couleur `info` (`#008c94`)** — proximité de teinte avec le nouveau secondaire/tertiary
  explicitement acceptée avec Simon (60.0.6). Ne pas la retoucher sans nouvelle instruction.
- **`--m3-primary` lui-même** — couleur de marque, jamais recalculée par un algorithme de variante,
  quel que soit le lot.
- **Valeurs de base `success`/`warning`/`danger`** — seuls leurs containers sont ajoutés (60.2), les
  couleurs elles-mêmes ne changent pas.
- **Rôles M3 additionnels non demandés** : `surface-variant`/`on-surface-variant`/`outline-variant`,
  niveaux d'élévation de surface (`surface-dim`/`surface-bright`/`surface-container-*`) — aucun
  consommateur identifié, pas dans la demande initiale.
- **Redesign visuel de `ReconnectModal.razor.css`** au-delà du remplacement de couleurs (60.1.b) —
  structure/comportement du composant de reconnexion inchangés.
- **Toute modification hors de `ExcelETL.BlazorAdmin`** — ce lot est strictement CSS, aucun impact
  Domain/Application/Infrastructure/WebAPI.
