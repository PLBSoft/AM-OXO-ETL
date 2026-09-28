# Tickets TDD — Lot 088 : activité récente des fichiers générés sur la page d'accueil

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 28/09 à partir
d'une revue de la page d'accueil en production (`oxo-etl-admin.alphamaintenance.fr`, v1.0.21) : la page n'a
pas bougé depuis le lot 054 alors que l'application et l'API ont évolué. Esprit KISS/YAGNI demandé par
Simon. **Choix D1-D4 tranchés par Simon le 28/09 ; D5-D7 sont des propositions à valider avec le ticket.***

---

## 0. La demande

Ajouter à la page d'accueil un ou deux indicateurs, et peut-être un graphique montrant les fichiers générés
dans le temps. Rester simple.

---

## 1. Constat

- La page (`Components/Pages/Home.razor`, lot 054) affiche 4 tuiles : profils d'import (3), profils
  d'export (2), fichiers générés (84, cumul depuis le début), dernière génération.
- Les deux tuiles de profils bougent rarement : ce sont surtout des raccourcis. Le cumul ne dit rien de
  l'activité récente.
- Ce qui compte aujourd'hui n'est pas affiché : le **statut** des fichiers archivés
  (`GeneratedFileArchiveStatus` : `Success` / `NonBlockingWarning` / `Rejected`). Depuis les lots 085/086
  (rejet des repères en double), les rejets sont précisément ce que suit l'équipe AlphaMaintenance.
- Données disponibles sans nouvelle colonne : `GeneratedFileRecord.GeneratedAtUtc` et `.Status`.
- `IGeneratedFileArchiveStore` n'offre que `SearchAsync` (lignes complètes, sans limite de date) et
  `GetSummaryAsync` (nombre total + date max). Il faut une lecture dédiée, limitée à la période et à ces
  deux colonnes.
- Les heures s'affichent dans le fuseau du navigateur depuis le lot 064 (`localTime.js`,
  `ILocalTimeFormatter`). Le serveur de production d'un client peut être dans un autre fuseau que le
  navigateur (cas réel : serveur Nouvelle-Calédonie UTC+11, navigateur en France).
- Aucune bibliothèque de graphiques dans le projet. Contrainte OSS stricte (MIT/Apache).
- `HomeIndicators` porte un commentaire du lot 054 : « exactement quatre indicateurs, un cinquième
  demande un nouveau ticket ». Ce ticket est ce nouveau ticket.

---

## 2. Décisions

| # | Question | Décision |
| :--- | :--- | :--- |
| D1 | Jour auquel rattacher un fichier | **Jour du fuseau du navigateur** (Simon, 28/09). Cohérent avec le lot 064. |
| D2 | Période et granularité | **30 jours, une barre par jour** (Simon, 28/09). Aujourd'hui inclus, donc aujourd'hui et les 29 jours précédents. Pas de sélecteur de période. |
| D3 | Tuiles | **La tuile « Fichiers générés » (cumul) est remplacée** par « 30 derniers jours » (Simon, 28/09). Tuiles de profils et « Dernière génération » inchangées. |
| D4 | Statuts dans le graphique | **Barres empilées par statut** (Simon, 28/09), légende en texte. |
| D5 | Couleurs du graphique | *Proposition* : `--m3-success` / `--m3-warning` / `--m3-danger`, les couleurs des badges de `/generated-files`. Limite connue : l'orange `--m3-warning` (#ff7518) n'atteint pas 3:1 sur le fond clair (≈2,7:1, WCAG 1.4.11). On l'accepte : les segments sont séparés par un filet de la couleur du fond, et l'information existe aussi en texte (infobulle, tableau accessible, tuile). |
| D6 | Dessin du graphique | *Proposition* : **SVG écrit directement dans le Razor**, sans bibliothèque ni JavaScript. Aucune licence à vérifier, testable en bUnit, 30 barres ne justifient pas une dépendance. |
| D7 | Avant que le fuseau du navigateur soit connu (prérendu, tests non interactifs) | *Proposition* : regroupement par jour UTC, puis nouveau calcul avec le fuseau du navigateur dès que le circuit est interactif. Même principe que le lot 064 (affichage provisoire en UTC, jamais vide). |

---

## 3. Ce qui est affiché

### Tuile « 30 derniers jours » (remplace « Fichiers générés »)

```
[icône archive]  30 derniers jours
12
9 succès · 2 avec avertissements · 1 rejeté
```

- Lien vers `generated-files`, comme la tuile qu'elle remplace (même id `home-kpi-generated-files`).
- Le nombre total est la somme des barres du graphique : tuile et graphique ne peuvent pas se contredire.
- Une catégorie à zéro reste affichée (« 0 rejeté ») : l'absence de rejet est une information.
- « rejeté » passe en `text-danger` seulement quand il y en a au moins un ; le texte porte toujours
  l'information.
- Lecture impossible : « Indisponible », comme les autres tuiles.

### Graphique « Fichiers traités par jour »

- Sous les tuiles, pleine largeur, dans une carte ; titre `h2` (hiérarchie `h1` → `h2` respectée).
- 30 barres, de la plus ancienne (gauche) à aujourd'hui (droite). Un jour sans fichier reste une place
  vide, pour ne pas donner l'impression d'une activité continue.
- Segments empilés de bas en haut : succès, avertissements, rejetés.
- Axe horizontal : une date toutes les semaines (`dd/MM`), et la date du jour.
- Axe vertical : seulement la valeur maximale et zéro. Pas de quadrillage.
- Survol d'une barre : infobulle native SVG (`<title>`) « 28/09 : 5 fichiers (3 succès, 1 avec
  avertissements, 1 rejeté) ».
- Légende en texte sous le graphique.
- Accessibilité : `<figure>` avec `<figcaption>`, `svg role="img"` décrit par un résumé, et un tableau
  `visually-hidden` (date, succès, avertissements, rejetés) pour les lecteurs d'écran.
- Aucun fichier sur 30 jours : pas de graphique, le texte « Aucun fichier traité ces 30 derniers jours. ».
- Lecture impossible : « Indisponible » à la place du graphique.
- Mobile : le SVG prend la largeur de la carte (`viewBox`, `width: 100%`) ; environ 10 px par barre sur un
  écran de 375 px, lisible.

---

## 4. Conception

- **Lecture** : `IGeneratedFileArchiveStore.GetActivitySinceAsync(DateTime fromUtc)` renvoie
  `IReadOnlyList<GeneratedFileActivityEntry>` (`GeneratedAtUtc`, `Status`), projection SQL de ces deux
  colonnes seulement (`Where` + `Select`), jamais les lignes complètes.
- **Fenêtre lue** : `maintenant UTC - 31 jours`. La journée de marge couvre tout décalage de fuseau
  (±14 h) ; le regroupement écarte ensuite ce qui sort des 30 jours locaux.
- **Regroupement** : fonction pure `GenerationActivityBuilder.Build(entries, TimeZoneInfo, nowUtc)`
  (`Application/Home/`) qui renvoie toujours exactement 30 `DailyGenerationActivity(DateOnly Day,
  int Success, int Warning, int Rejected)`, du plus ancien au plus récent. Elle prend le fuseau en
  paramètre (`TimeZoneInfo`, changements d'heure compris) et le « maintenant » en paramètre (testable sans
  horloge).
- **Service** : `HomeIndicators.GeneratedFileCount` est remplacé par
  `RecentActivity : HomeIndicatorValue<IReadOnlyList<GeneratedFileActivityEntry>>`, lu dans son propre
  bloc isolé (un échec ne bloque pas les autres tuiles). « Dernière génération » continue de venir de
  `GetSummaryAsync`. Le service reçoit `TimeProvider` pour calculer la fenêtre (type du framework,
  `TimeProvider.System` enregistré dans `Program.cs`, `FakeTimeProvider` inutile : un `TimeProvider`
  écrit à la main dans les tests suffit).
- **Fuseau du navigateur** : `localTime.js` gagne `timeZone()`
  (`Intl.DateTimeFormat().resolvedOptions().timeZone`, identifiant IANA, ex. `Pacific/Noumea`).
  `ILocalTimeFormatter.GetBrowserTimeZoneIdAsync()` l'expose. La page le convertit avec
  `TimeZoneInfo.FindSystemTimeZoneById` (.NET accepte les identifiants IANA sous Windows) et garde UTC si
  l'identifiant est absent ou inconnu.
- **Page** : `OnInitializedAsync` charge les indicateurs et regroupe en UTC (D7).
  `OnAfterRenderAsync`, si interactif, lit le fuseau une seule fois, regroupe de nouveau et
  `StateHasChanged`. Le regroupement (tuile + graphique) est fait par la page, qui seule connaît le fuseau.
- **Graphique** : composant `Components/Pages/HomeActivityChart.razor` (+ `.razor.css`), paramètre
  `IReadOnlyList<DailyGenerationActivity>`. Calcul des hauteurs en C#, couleurs par classes CSS utilisant
  les variables du thème (clair et sombre sans code en plus).

Aucune migration, aucun changement d'API Web, aucun changement de `/generated-files`.

---

## 5. Tickets

Un commit par ticket. Tests filtrés sur la classe en cours ; en fin de lot, les projets touchés
(Application, Infrastructure, BlazorAdmin).

### 088.1 — Lecture de l'activité sur une période (Infrastructure)

- `GeneratedFileActivityEntry` (record, `Application/Archiving/`).
- `IGeneratedFileArchiveStore.GetActivitySinceAsync(fromUtc)` + implémentation `EfGeneratedFileArchiveStore`.
- Tests `EfGeneratedFileArchiveStoreTests` (InMemory) : renvoie seulement les fichiers à partir de
  `fromUtc` (borne incluse), avec date et statut ; base vide → liste vide.

### 088.2 — Regroupement par jour local (Application)

- `DailyGenerationActivity`, `GenerationActivityBuilder` (`Application/Home/`).
- Tests `GenerationActivityBuilderTests` :
  - toujours 30 jours, du plus ancien à aujourd'hui, jours sans fichier à zéro ;
  - chaque statut compté dans sa colonne ;
  - fuseau appliqué : un fichier à 22:00 UTC le 27/09 compte le 28/09 à Nouméa (UTC+11) et le 28/09 à
    Paris (UTC+2) ; un fichier à 21:30 UTC le 27/09 compte le 27/09 à Paris ;
  - changement d'heure dans la fenêtre (Paris, nuit du 25/10) sans jour décalé ;
  - fichiers hors des 30 jours locaux ignorés (y compris ceux de la journée de marge).

### 088.3 — Indicateur d'activité récente dans le service (Application)

- `HomeIndicators` : `RecentActivity` ajouté. Commentaire du lot 054 (« exactement quatre ») mis à jour.
  *Écart assumé (28/09)* : `GeneratedFileCount` n'est retiré qu'au 088.5, avec la tuile qui l'affiche,
  pour que chaque commit reste vert.
- `HomeIndicatorsService` : `TimeProvider` injecté, fenêtre `now - 31 jours`, lecture isolée.
- `Program.cs` : `TimeProvider.System` enregistré.
- Tests `HomeIndicatorsServiceTests` : fenêtre demandée au store ; échec de cette lecture → seule
  `RecentActivity` indisponible. `HomeTests` : les 3 constructions de `HomeIndicators` reçoivent le
  nouveau champ.

### 088.4 — Fuseau du navigateur

- `localTime.js` : `timeZone()`. `ILocalTimeFormatter.GetBrowserTimeZoneIdAsync()`.
- Résolution en `TimeZoneInfo` avec repli UTC (petite méthode statique testée à part).
- Tests `LocalTimeFormatterTests` (appel JS attendu) ; tests de la résolution : `Europe/Paris`,
  `Pacific/Noumea`, identifiant inconnu → UTC, `null` → UTC.

### 088.5 — Tuile « 30 derniers jours »

- `Home.razor` : la tuile `home-kpi-generated-files` affiche le total et le détail par statut ; nouvelles
  clés `Home_RecentActivityLabel`, `Home_RecentActivitySuccess`, `Home_RecentActivityWarning`,
  `Home_RecentActivityRejected` (EN/FR réels).
- Tests `HomeTests` : total et détail ; « 0 rejeté » affiché sans `text-danger` ; rejets > 0 en
  `text-danger` ; indisponible ; lien conservé ; mise à jour après résolution du fuseau (interactif,
  `ILocalTimeFormatter` simulé renvoyant `Pacific/Noumea`, un fichier qui change de jour).
- `HomeIndicators.GeneratedFileCount` retiré (reporté du 088.3), avec sa lecture dans le service et ses
  assertions dans `HomeIndicatorsServiceTests`.
- Test existant sur le cumul corrigé sur place (il n'est plus affiché).

### 088.6 — Graphique

- `HomeActivityChart.razor` + CSS ; clés `Home_ActivityChartTitle`, `Home_ActivityChartSummary`,
  `Home_ActivityBarTooltip`, `Home_ActivityLegendSuccess/Warning/Rejected`, `Home_NoRecentActivity`.
- Tests bUnit : 30 barres ; hauteurs proportionnelles (la barre maximale occupe toute la hauteur) ;
  segment absent quand le nombre est zéro ; infobulle de chaque barre ; tableau accessible de 30 lignes ;
  aucun fichier → message, pas de SVG ; indisponible ; hiérarchie des titres
  (`HeadingHierarchyAssertions`).
- Test CSS (lecture du fichier, convention `Styling/`) : les classes des segments utilisent
  `--m3-success`/`--m3-warning`/`--m3-danger`, jamais une couleur écrite en dur.
- `HomeHttpTests` : le marqueur du graphique est présent dans la réponse de `/`.

### 088.7 — Documentation

- `CLAUDE.md` : entrée du lot ; mention dans le bloc du lot 054.
- Ce ticket : section « Résultat ».

---

## 6. Hors périmètre

- Sélecteur de période, zoom, export du graphique.
- Répartition par utilisateur, par profil ou par équipement.
- Compteurs d'isolements, de points ou de tâches.
- Filtre par statut sur `/generated-files` (le lien de la tuile ouvre la liste complète).
- Rafraîchissement automatique de la page.
- Suppression de `GeneratedFileArchiveSummary.Count` (encore utilisé par ses tests, sans coût).

---

## 7. Résultat (28/09)

Fait en un commit par ticket (088.1 → 088.7), chaque test vu en échec avant son code (pour 088.5, les
deux comportements clés — regroupement dans le fuseau du navigateur, rouge seulement s'il y a un rejet —
ont été vérifiés en réintroduisant le défaut : les tests échouent).

- Écart assumé : `GeneratedFileCount` retiré au 088.5 plutôt qu'au 088.3 (voir 088.3).
- Clés de traduction : pluriel français de « rejeté » géré par deux clés
  (`Home_RecentActivityRejectedSingular`/`Plural`) ; l'infobulle et le résumé accessible gardent « (s) ».
- Graphique : SVG à l'échelle de la carte, plafonné à 900 px de large ; libellés des axes agrandis sous
  576 px pour rester lisibles sur téléphone ; valeur maximale ancrée par le haut pour ne pas être coupée.
- Suites complètes : Application 291 / Infrastructure 294 / BlazorAdmin 1643 / WebAPI 78, all green.
- Non fait : le validateur de palette de la compétence dataviz demande Node, absent de ce poste (le
  contraste de l'orange était déjà connu et accepté, D5).
- **Non vérifié dans un vrai navigateur** : taille réelle du graphique, lisibilité des libellés sur
  téléphone, thème sombre, infobulles au survol.
