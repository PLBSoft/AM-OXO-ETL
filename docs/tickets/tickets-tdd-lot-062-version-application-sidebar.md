# Tickets TDD — Lot 062 : numéro de version et date de publication dans la sidebar

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 061
(`tickets-tdd-lot-061-logo-client-bas-sidebar.md`) — le lot 060
(`tickets-tdd-lot-060-palette-m3-complete-suppression-dette-couleurs.md`) est sans lien avec ce
sujet.*

**Origine (demande client, 30/07)** — afficher en bas de la sidebar de BlazorAdmin un numéro de
version et une date de publication, pour que le client puisse identifier quelle version de
l'application est déployée sans avoir à demander. Contrainte explicite de Simon : aucun geste
manuel de bump de version à chaque publication — la mécanique doit être entièrement automatique.

**Décisions actées pour ce lot** :

- **Compteur incrémenté par MSBuild, pas d'horodatage en guise de version.** Un fichier texte
  versionné dans le repo (`version.txt`, un entier) est lu et incrémenté par un target MSBuild
  personnalisé, déclenché uniquement sur `Publish` (pas sur un simple `Build`/F5 en dev, pour ne
  pas gonfler le compteur à chaque compilation locale). Rationale : le client a explicitement
  demandé un « simple incrément », pas un horodatage — un numéro `v1`, `v2`, `v3`... reste plus
  lisible pour un client non technique qu'un horodatage brut.
- **La date de publication n'est pas persistée.** Elle est injectée à la compilation via
  `$([System.DateTime]::UtcNow...)` comme `AssemblyMetadata`, donc toujours exacte au moment du
  build réel — aucun état supplémentaire à maintenir.
- **Scope limité à `ExcelETL.BlazorAdmin`.** Le compteur de version n'a aucune raison de vivre dans
  `ExcelETL.WebAPI` : seule la sidebar de BlazorAdmin affiche cette information, et coupler les deux
  projets sur un même compteur ajouterait une dépendance artificielle entre deux composants
  déployés indépendamment (cf. décision de deux sites IIS distincts,
  `guide-deploiement-am-oxo-etl-windows-server.md` §0).
- **Rien ne traverse Domain/Application.** Lire `Assembly.GetExecutingAssembly()` est un détail de
  présentation pur, pas une règle métier — la logique reste entièrement dans
  `ExcelETL.BlazorAdmin` (pas de nouvelle abstraction dans `ExcelETL.Application`).
- **Le fichier `version.txt` reste committé normalement.** Il est modifié sur disque par MSBuild à
  chaque `Publish` ; il est ensuite committé par Simon comme n'importe quel autre fichier modifié —
  ce n'est pas un geste additionnel spécifique au versionnement, juste un fichier de plus dans un
  commit déjà prévu.

**Ce que ce lot n'est pas** : ni un système de versionnement sémantique (pas de major.minor décidé
manuellement), ni une page de changelog, ni un mécanisme de version pour les profils d'import/export
(explicitement écarté ailleurs, cf. `tickets-tdd-lot-056-modele-enregistrement-editeurs-profil.md`
— sujet distinct, pas rouvert ici).

---

## 62.0. Investigation préalable (obligatoire avant tout code)

- [ ] Confirmer l'état réel de `NavMenu.razor` **après** l'implémentation du lot 061 : le logo
  client (`#sidebar-client-logo`, dans son conteneur `.sidebar-logo-container`) est désormais le
  dernier enfant de `<nav class="nav flex-column">`, après le dernier `<AuthorizeView>`
  (`tickets-tdd-lot-061-logo-client-bas-sidebar.md` §61.1) — ne pas repartir de l'état pré-061
  décrit dans un lot antérieur.
- [ ] Décider et confirmer avec Simon la position du bloc version/date par rapport au logo : sous
  le logo (dernier enfant absolu de `nav.nav.flex-column`, logo au-dessus/version en dessous) par
  défaut, sauf préférence contraire explicite — à valider avant d'écrire le markup, pas une
  supposition à coder directement.
- [ ] Vérifier qu'aucun mécanisme de version (`Version`, `AssemblyVersion`,
  `AssemblyInformationalVersion`) n'est déjà défini dans `ExcelETL.BlazorAdmin.csproj` ou dans un
  éventuel `Directory.Build.props`/`.targets` partagé — ne pas créer de second mécanisme en
  parallèle si un existe déjà.
- [ ] Confirmer que le processus de publication réel (VS "Publier", cible Dossier, §3 du guide de
  déploiement) déclenche bien le target MSBuild `Publish` et pas uniquement `Build` — vérifier avec
  un test de publication locale que le compteur s'incrémente une seule fois par clic sur "Publier".

---

## 62.1. Target MSBuild — compteur de version incrémenté au Publish

**Comportement attendu** :
- Fichier `version.txt` à la racine de `ExcelETL.BlazorAdmin`, contenant un entier (initialisé à
  `0`), versionné dans git.
- Target MSBuild `BeforeTargets="Publish"` dans `ExcelETL.BlazorAdmin.csproj` : lit `version.txt`,
  incrémente la valeur, réécrit le fichier, puis fixe `$(Version)` /
  `$(AssemblyInformationalVersion)` à `1.0.<compteur>`.
- Un second `PropertyGroup`/tâche injecte la date UTC de build comme `AssemblyMetadata` (clé
  `BuildDate`, valeur ISO 8601) — pas de fichier généré à committer pour cette partie.
- Aucun impact sur `dotnet build`/F5 en dev : le compteur ne bouge que sur `Publish`.

**Tests** :
- Pas de test unitaire xUnit possible sur un target MSBuild — vérification par exécution réelle :
  `dotnet publish` local exécuté deux fois de suite, `version.txt` doit contenir une valeur
  incrémentée de 1 à chaque exécution, jamais plus.
- Vérifier par inspection de l'assembly publiée (`dotnet-info`/réflexion dans un petit script de
  contrôle, ou test d'intégration décrit en 62.2) que `AssemblyInformationalVersion` et le
  `AssemblyMetadata("BuildDate", ...)` sont bien présents et cohérents avec `version.txt` au moment
  du build.

**Dossier** : `ExcelETL.BlazorAdmin/ExcelETL.BlazorAdmin.csproj`, `ExcelETL.BlazorAdmin/version.txt`
(nouveau).

---

## 62.2. Service de lecture de la version — `IApplicationBuildInfo`

**Comportement attendu** :
- Petite classe `ApplicationBuildInfo` (namespace `ExcelETL.BlazorAdmin`, pas d'interface dans
  `Application` — décision actée ci-dessus) exposant `Version` (string) et `BuildDateUtc`
  (`DateTime?`), lus par réflexion sur `Assembly.GetExecutingAssembly()` :
  `GetName().Version` / `GetCustomAttribute<AssemblyInformationalVersionAttribute>()` pour la
  version, `GetCustomAttributes<AssemblyMetadataAttribute>()` filtré sur la clé `BuildDate` pour la
  date.
- Enregistrée en DI (`AddSingleton`) — lue une seule fois par process, pas de recalcul par requête.
- Si l'attribut `BuildDate` est absent (ex. exécution locale sans passer par le target `Publish`,
  cf. 62.1), le service renvoie `null` pour `BuildDateUtc` sans lever d'exception — le composant
  d'affichage (62.3) doit gérer ce cas (cf. tests 62.3).

**Tests xUnit** :
- Assembly de test factice ou fixture avec `AssemblyMetadata("BuildDate", ...)` présent → `Version`
  et `BuildDateUtc` non nuls et correctement parsés.
- Assembly sans `AssemblyMetadata("BuildDate", ...)` → `BuildDateUtc` retourne `null`, pas
  d'exception.

**Dossier** : `ExcelETL.BlazorAdmin/Services/ApplicationBuildInfo.cs` (nouveau),
`ExcelETL.BlazorAdmin/Program.cs` (enregistrement DI).

---

## 62.3. Affichage dans `NavMenu.razor`

**Comportement attendu** :
- Le bloc version/date prend place **après** `.sidebar-logo-container` (ajouté par le lot 061),
  toujours à l'intérieur de `<nav class="nav flex-column">` — il devient le nouveau dernier enfant
  du conteneur, le logo restant juste au-dessus (sauf décision contraire actée en 62.0). Ne pas
  toucher au markup du logo lui-même (`#sidebar-client-logo`), uniquement ajouter ce nouveau bloc à
  sa suite.
- Bloc discret (taille de police réduite, couleur atténuée — cohérent avec le patron déjà utilisé
  pour les éléments secondaires de la sidebar, et avec `.sidebar-logo-container` en termes
  d'espacement vertical pour ne pas recréer un double padding en bas de sidebar) affichant
  `v{Version}` et, si `BuildDateUtc` n'est pas `null`, la date formatée en heure locale (format
  `dd/MM/yyyy`, cohérent avec le reste de l'admin — vérifier le format déjà utilisé ailleurs dans
  BlazorAdmin, ex. `tickets-tdd-lot-054-page-accueil-indicateurs.md`, pour ne pas introduire un
  format de date divergent).
- Si `BuildDateUtc` est `null` (exécution locale hors `Publish`), n'afficher que la version, sans
  date vide ni texte d'erreur.
- Élément avec un `id` stable (ex. `sidebar-version-info`) pour permettre un test bUnit sans
  sélection par texte ou position — hors de tout `<AuthorizeView>`, comme le logo (visible que
  l'utilisateur soit authentifié ou non, cf. `NavMenu_ClientLogo_VisibleRegardlessOfAuthorizationState`
  du lot 061, même principe à reproduire ici).

**Tests bUnit** (dans `NavMenuTests.cs`, réutilisation du helper `WithCulture` existant, cf. lot
061 §61.0.6) :
- Avec `BuildDateUtc` renseigné : `sidebar-version-info` contient la version et la date formatée.
- Avec `BuildDateUtc` à `null` : `sidebar-version-info` contient uniquement la version, aucune
  date, aucun texte d'erreur affiché.
- `sidebar-version-info` est positionné après `#sidebar-client-logo` dans le DOM (assertion sur
  l'ordre des enfants de `nav.nav.flex-column`, pas seulement sur la présence des deux éléments).
- Présent que l'état soit authentifié ou non (même assertion que le logo, lot 061).

**Dossier** : `ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor`,
`ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor.css`.

---

## Hors périmètre

- Versionnement sémantique manuel (major.minor décidé par un humain).
- Changelog ou historique des versions affiché dans l'application.
- Toute forme de versionnement des profils d'import/export (sujet distinct, déjà écarté).
- Affichage de la version côté `ExcelETL.WebAPI` (hors scope de ce lot, cf. décision de scope
  ci-dessus).
