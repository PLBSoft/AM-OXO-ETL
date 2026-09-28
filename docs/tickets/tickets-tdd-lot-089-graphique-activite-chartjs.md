# Tickets TDD — Lot 089 : graphique d'activité de l'accueil avec Chart.js

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 28/09 après
publication du lot 088 (`tickets-tdd-lot-088-accueil-activite-recente-fichiers-generes.md`), dont il
remplace la décision D6 (SVG écrit à la main). Option « Chart.js » choisie par Simon le 28/09 ; les
décisions D1-D9 ci-dessous sont des propositions à valider avec le ticket.*

---

## 0. La demande

Retour de Simon sur le graphique publié au lot 088 :

- le graphique est « basique » ;
- aucune information au survol ;
- il ne se redimensionne pas.

Choix retenu : afficher le graphique avec la bibliothèque **Chart.js**.

---

## 1. Constat

- Le graphique du lot 088 (`Components/Pages/HomeActivityChart.razor`) est un SVG écrit à la main. Son
  infobulle est la balise SVG `<title>`, affichée par le navigateur après environ une seconde
  d'immobilité, sans style, parfois pas du tout. Il est limité à 900 px de large (texte agrandi avec le
  dessin), donc il ne remplit pas la carte sur un grand écran.
- Le reste du lot 088 est sain et ne change pas : lecture des 31 derniers jours
  (`GetActivitySinceAsync`), regroupement par jour local (`GenerationActivityBuilder`), fuseau du
  navigateur (`BrowserTimeZoneResolver`, `localTime.js`), tuile « 30 derniers jours ». Seul l'affichage
  du graphique est remplacé.
- BlazorAdmin n'a aucune bibliothèque de graphique. Dépendances d'affichage actuelles : QuickGrid
  (NuGet, Microsoft), Bootstrap (copié dans `wwwroot/lib/bootstrap`), `theme.js` et `localTime.js`
  (scripts du projet, chargés dans `App.razor` via `@Assets[...]`).
- **Chart.js** : dernière version 4.5.1, licence **MIT** (vérifiée sur le registre npm et dans l'en-tête
  du fichier). Le paquet npm fournit `dist/chart.umd.min.js` (≈ 200 Ko, utilisable directement dans une
  balise `<script>`, expose `window.Chart`) et `LICENSE.md`.
- Chart.js dessine dans un `<canvas>` : bUnit ne peut rien vérifier du dessin, seulement les données
  transmises au script. Un canvas n'est pas lu par les lecteurs d'écran : le résumé et le tableau cachés
  du lot 088 restent nécessaires.

---

## 2. Décisions

| # | Question | Proposition |
| :--- | :--- | :--- |
| D1 | Comment livrer Chart.js | **Copier `chart.umd.min.js` 4.5.1 et `LICENSE.md` dans `wwwroot/lib/chartjs/`**, comme Bootstrap, chargé dans `App.razor` via `@Assets[...]`. Jamais de CDN : le serveur du client est on-premise et la page ne doit dépendre d'aucun site extérieur. |
| D2 | Paquet NuGet « wrapper » Blazor (ChartJs.Blazor, Blazor-ApexCharts…) | **Non.** Souvent en retard sur la bibliothèque, licences à vérifier au cas par cas, et un appel direct suffit pour un seul graphique. |
| D3 | Lien Blazor ↔ Chart.js | Un script du projet, `wwwroot/js/activityChart.js` (`window.amOxoActivityChart.render(canvasId, model)` / `dispose(canvasId)`), appelé par une petite interface C# `IActivityChartInterop` (même forme que `ILocalTimeFormatter`). Tous les textes (dates, noms de statut, infobulle) sont préparés et traduits en C# ; le script ne contient aucun texte. |
| D4 | Couleurs | Lues dans les variables du thème (`--m3-success`, `--m3-warning`, `--m3-danger`, texte, bordures) au moment du dessin. Le graphique est redessiné quand le thème clair/sombre change (observation de l'attribut `data-bs-theme` de `<html>`). Aucune couleur écrite en dur. La limite de contraste de l'orange acceptée au lot 088 (D5) reste valable. |
| D5 | Taille | Largeur : toute la carte (plus de plafond à 900 px). Hauteur fixe du conteneur : 280 px, 220 px sous 576 px. Chart.js se redessine à chaque changement de largeur ; les textes gardent leur taille. |
| D6 | Infobulle | Au survol ou au toucher d'une barre, immédiate : titre = date complète (ex. « lundi 28 septembre 2026 »), une ligne par statut non nul (« Succès : 3 »), pied = total (« 5 fichiers »). Survol par colonne entière, pas seulement par segment. |
| D7 | Légende et axes | Légende Chart.js en bas, cliquable pour masquer un statut. Axe vertical en nombres entiers uniquement, quadrillage discret. Axe horizontal : dates `dd/MM`, Chart.js en saute automatiquement quand la place manque. |
| D8 | Accessibilité | Le canvas porte `role="img"` et est décrit par le résumé caché existant ; le tableau caché (30 lignes) est conservé. Animation désactivée si le système demande de réduire les animations (`prefers-reduced-motion`). |
| D9 | Avant que la page soit interactive (prérendu) | Rien n'est dessiné : le conteneur garde sa hauteur (pas de saut de mise en page), le tableau et le résumé cachés sont déjà là. Le dessin arrive dès que le circuit est interactif, puis est refait si le regroupement change (fuseau du navigateur connu). Même principe que le lot 064. |

---

## 3. Tickets

Un commit par ticket. Tests filtrés sur la classe en cours ; en fin de lot, `ExcelETL.BlazorAdmin.Tests`
seul (aucun autre projet n'est touché).

### 089.1 — Chart.js copié dans le projet

- `wwwroot/lib/chartjs/chart.umd.min.js` et `wwwroot/lib/chartjs/LICENSE.md`, tels quels depuis le paquet
  npm `chart.js@4.5.1` (fichier téléchargé depuis `registry.npmjs.org`, jamais modifié).
- `App.razor` : `<script src="@Assets["lib/chartjs/chart.umd.min.js"]"></script>`, avant `activityChart.js`.
- Tests (`Styling/`, lecture des fichiers en texte, même convention que `ThemeToggleScriptTests`) :
  l'en-tête annonce `Chart.js v4.5.1` et `MIT License` ; `LICENSE.md` est présent ; `App.razor` le charge via
  `@Assets[...]`, jamais par une URL `http`.

### 089.2 — Script de dessin `activityChart.js`

- `window.amOxoActivityChart.render(canvasId, model)` : crée ou remplace le graphique du canvas
  (`Chart.getChart(canvas)?.destroy()` avant de recréer), barres empilées, options de D4 à D8.
  `dispose(canvasId)` : détruit le graphique s'il existe.
- `model` : `{ labels, tooltipTitles, datasets: [{ label, status, values }], totalLabels }` — `status`
  (`success`/`warning`/`rejected`) choisit la variable de couleur ; `totalLabels[i]` est le pied
  d'infobulle déjà traduit.
- Redessin au changement de thème : un seul `MutationObserver` sur `data-bs-theme`, qui redessine les
  graphiques existants avec les nouvelles couleurs.
- Chargé dans `App.razor` après Chart.js.
- Tests (lecture du fichier en texte) : pas de couleur écrite en dur (`#…`, `rgb(`), lecture des trois
  variables `--m3-success`/`--m3-warning`/`--m3-danger`, `responsive: true` et `maintainAspectRatio: false`,
  prise en compte de `prefers-reduced-motion`, fonctions `render` et `dispose` exposées. Le rendu réel
  n'est pas testable en bUnit : vérification manuelle (§5).

### 089.3 — Interface C# et modèle

- `Services/IActivityChartInterop` / `ActivityChartInterop` (`RenderAsync(canvasId, model)`,
  `DisposeAsync(canvasId)`), enregistré `AddScoped` dans `Program.cs`.
- `ActivityChartModel` construit en C# à partir des 30 `DailyGenerationActivity` : libellés `dd/MM`,
  titres d'infobulle (date longue dans la culture de l'interface), trois séries dans l'ordre succès,
  avertissements, rejetés, pieds « {n} fichier(s) ». Construction dans une méthode statique pure
  (`ActivityChartModelBuilder`), testée seule.
- Tests : `ActivityChartModelBuilderTests` (30 points, ordre, valeurs, textes FR/EN, pluriel du pied) ;
  `ActivityChartInteropTests` (`Mock<IJSRuntime>` : noms de fonctions JS appelées, arguments transmis).

### 089.4 — Composant `HomeActivityChart.razor` sur Chart.js

- Le SVG est remplacé par `<div class="home-activity-chart-canvas"><canvas id="home-activity-chart"
  role="img" aria-labelledby="home-activity-chart-summary"></canvas></div>`. Le résumé et le tableau
  cachés restent ; la légende texte du lot 088 est retirée (remplacée par celle de Chart.js).
- `OnAfterRenderAsync`, seulement si `RendererInfo.IsInteractive` : dessin au premier rendu, puis à
  chaque changement de `Days` (comparaison des valeurs, pas de redessin inutile). `IAsyncDisposable` :
  `DisposeAsync` du graphique ; une déconnexion du circuit (`JSDisconnectedException`) est ignorée.
- `HomeActivityChart.razor.css` : hauteur du conteneur (D5), suppression des règles SVG devenues inutiles.
- Tests :
  - `HomeActivityChartTests` réécrit : canvas présent avec son `role`/`aria-labelledby` ; résumé et
    tableau inchangés ; non interactif → aucun appel JS ; interactif → un appel `RenderAsync` avec le
    modèle attendu ; nouveaux `Days` → nouvel appel ; mêmes `Days` → pas de nouvel appel ; `dispose`
    appelé à la destruction. Les tests de géométrie SVG du lot 088 sont supprimés (le dessin appartient
    désormais à Chart.js).
  - `HomeActivityChartCssTests` réécrit : hauteur 280 px / 220 px sous 576 px, largeur non plafonnée.
  - `HomeTests` : les tests existants de la section (message sans fichier, indisponible, hiérarchie des
    titres, tableau = total de la tuile) restent verts sans modification d'assertion ; ceux qui cherchaient
    `g.home-activity-bar` passent sur le tableau caché. Enregistrer un `IActivityChartInterop` simulé.
  - `HomeHttpTests` : le marqueur `id="home-activity-chart"` est toujours présent dans la réponse.

### 089.5 — Documentation

- `CLAUDE.md` : entrée du lot ; note « remplacé » dans le bloc du lot 088 (D6) ; mention de Chart.js dans
  les dépendances d'affichage.
- Ce ticket : section « Résultat ».

---

## 4. Hors périmètre

- Autres graphiques ou tableaux de l'application (Chart.js n'est ajouté que pour celui-ci).
- Changement de période, de granularité ou de source de données (lot 088 inchangé).
- Clic sur une barre pour ouvrir `/generated-files` filtré par date (pas de filtre par date sur cette page).
- Mise à jour automatique de Chart.js : toute montée de version passe par un ticket (licence revérifiée).

---

## 5. Vérification manuelle attendue (après publication)

Le rendu d'un canvas n'est pas vérifiable par les tests. À contrôler par Simon sur le site publié :

- infobulle immédiate au survol et au toucher (téléphone), textes en français ;
- redimensionnement de la fenêtre : le graphique suit la largeur, les textes gardent leur taille ;
- bascule thème clair/sombre : couleurs mises à jour sans recharger ;
- clic sur un statut de la légende : la série est masquée puis réaffichée.

---

## 6. Résultat (28/09)

Fait en un commit par ticket (089.1 → 089.5), chaque test vu en échec avant son code. Décisions D1-D9
appliquées telles que proposées.

- Chart.js 4.5.1 copié tel quel depuis le paquet npm (empreinte vérifiée identique au fichier du paquet).
- Le script `activityChart.js` ne peut pas être exécuté par les tests : il est vérifié comme texte, et son
  analyse syntaxique a été contrôlée avec le moteur JScript de Windows (Node n'est pas installé sur ce poste).
- Tests réécrits : `HomeActivityChartTests` et `HomeActivityChartCssTests` (le dessin SVG n'existe plus) ;
  dans `HomeTests`, une seule assertion déplacée des barres SVG vers le canvas et le tableau caché.
- Clé supprimée : `Home_ActivityBarTooltip` (plus utilisée). Clés ajoutées : `Home_ActivityTotalSingular` /
  `Home_ActivityTotalPlural` (EN/FR réels).
- `ExcelETL.BlazorAdmin.Tests` : 1661/1661.
- **Publié et validé par Simon sur le site le 28/09** (« beaucoup mieux ») : le rendu Chart.js remplace le
  graphique SVG jugé basique du lot 088.
