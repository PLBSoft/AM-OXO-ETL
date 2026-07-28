# Tickets TDD — Lot 038 : page Blazor de test du contrat M2M réel (`/api-test`)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Cinquième lot
utilisant la convention numérique à trois chiffres, après le lot 037
(`tickets-tdd-lot-037-parite-boutons-carte-regle-feuille.md`).*

**Contexte et décision assumée de réouverture** : ce lot **recrée consciemment** ce que le Lot K4
(21/07) avait supprimé — une page BlazorAdmin qui appelle réellement le Web API en HTTP
(`UploadTest.razor`, typed `HttpClient`, seule exception documentée à la règle "BlazorAdmin ne
référence jamais WebAPI directement"). L'audit qualité BlazorAdmin du 25/07 a confirmé qu'il
n'existe aujourd'hui plus aucune occurrence de `HttpClient` dans `ExcelETL.BlazorAdmin`. Ce n'est
pas une régression de ce constat d'audit : c'est une nouvelle demande explicite de Simon,
motivée par trois besoins distincts non couverts par l'existant :
1. **Vérification manuelle post-déploiement** sur le vrai serveur Windows cible, sans dépendre de
   l'application legacy (`AvancementRecette`) pour déclencher un appel réel.
2. **Démonstration/preuve pour le client** du contrat M2M fonctionnant de bout en bout.
3. **Debug ad hoc** en cas de souci en production, sans avoir à reproduire le scénario depuis la
   legacy.

**Ce que ce lot n'est pas** : ni un remplacement de `ImportProfileTest.razor`/
`ExportProfileTest.razor` (qui restent la référence pour tester le pipeline métier en process,
profil par profil), ni un substitut aux tests d'intégration `WebApplicationFactory` existants sur
`OxoController` (Lot K/034, qui couvrent déjà automatiquement le contrat HTTP en CI). Ce lot ajoute
un **troisième outil**, complémentaire, pour un usage humain/manuel/ponctuel — pas pour remplacer
une couverture automatisée déjà en place.

**Dépend entièrement des Lots K et 034**, tous deux terminés (`POST /api/oxo/process` exposé,
authentifié par `ApiKeyAuthenticationHandler`, archivage best-effort en place). Ce lot ne modifie
ni ne rouvre aucune décision de ces deux lots — il consomme le contrat existant tel quel.

**Décisions actées avec Simon (24/07)** :
- **Nouvelle page dédiée**, pas une extension de `ImportProfileTest.razor`/
  `ExportProfileTest.razor` — cette page prend un couple `ImportProfileId`/`ExportProfileId` en un
  seul appel HTTP, contrairement aux deux pages existantes qui testent chacune un seul profil en
  process.
- **Authentification de l'appel** : la clé API est lue côté serveur depuis la configuration
  (`appsettings`), jamais saisie par l'admin dans le navigateur. Cohérent avec Blazor Server (le
  secret ne quitte jamais le serveur) et reproduit exactement ce que fait
  `ExcelProcessingClientService` côté legacy — pas de nouveau mécanisme d'authentification
  inventé.
- **Base URL du Web API configurable par environnement** (`appsettings.json`/
  `appsettings.Development.json`/`appsettings.Production.json`), pas codée en dur — c'est la
  raison d'être principale de la vérification post-déploiement (pointer vers le vrai serveur
  cible, pas `localhost`).
- **Upload mono-fichier uniquement** — le contrat `/api/oxo/process` ne gère pas le multi-fichier
  (contrairement aux pages de test en process du Lot 033), donc aucune raison d'introduire cette
  complexité ici.
- **Hors périmètre explicite** (voir section dédiée ci-dessous) : pas de test de charge, pas de
  consultation des fichiers archivés (Lot 034, déjà couvert par son propre écran), pas d'édition
  de profil depuis cette page.

**Conventions déjà en place à respecter (tout le lot)** :
`convention-ui-blazor-alignement-boutons.md` (boutons d'action alignés à droite),
`convention-ui-blazor-icones-boutons.md` (icônes Bootstrap Icons selon la matrice de décision) ;
IDs HTML stables, jamais de sélection par texte/position en bUnit ; xUnit 2.9.3 +
FluentAssertions 7.x + Moq + bUnit ; `AuthorizeView Roles="@IdentitySeeder.AdminRoleName"` pour
toute nouvelle page admin, avec vérification de la véritable absence DOM quand non-authentifié
(leçon du Lot L) ; pattern accordéon/statuts déjà validé (Lot V8/R3/033) réutilisé pour
l'affichage du résultat, plutôt que d'en inventer un nouveau.

---

## Hors périmètre explicite de ce lot

- **Test de charge / appels répétés en boucle** — cette page sert un test manuel ponctuel, pas un
  outil de performance. Aucun bouton "rejouer N fois".
- **Consultation des fichiers archivés** (Lot 034) — déjà couvert par son propre écran Blazor
  dédié ; cette page se contente d'afficher le résultat de l'appel HTTP en cours, pas l'historique
  des appels passés.
- **Édition de profil (`ImportProfile`/`ExportProfile`) depuis cette page** — la sélection se fait
  par dropdown en lecture seule sur les profils déjà existants (via `IImportProfileStore`/
  `IExportProfileStore`, en process, pas via HTTP) ; toute création/modification reste sur
  `ImportProfiles.razor`/`ExportProfiles.razor`.
- **Upload multi-fichiers** — voir décision actée ci-dessus, le contrat HTTP est mono-fichier.
- **Remplacement des tests d'intégration `WebApplicationFactory` existants** — cette page ne
  dispense d'aucun test automatisé déjà en place sur `OxoController` ; elle ne les duplique pas
  non plus intentionnellement (son but est l'usage manuel, pas l'assertion automatisée du contrat).
- **Saisie manuelle de la clé API dans le navigateur** — décision actée : lecture serveur
  uniquement, jamais un champ de saisie exposé côté client.

---

## 38.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire `ExcelProcessingClientService` (legacy, `.NET Framework 4.8`) pour confirmer exactement
  comment la clé API est actuellement transmise en en-tête HTTP (nom de l'en-tête, format) — la
  nouvelle page BlazorAdmin doit reproduire ce même contrat, pas en inventer un nouveau.
- [ ] Lire `ApiKeyAuthenticationHandler` (WebAPI) pour confirmer le nom exact de l'en-tête attendu
  et le nom de la clé de configuration (`Options.ApiKey`) source de vérité.
- [ ] Lire `OxoController` (ou nom équivalent confirmé au Lot K) pour confirmer la forme exacte du
  multipart attendu (`ImportProfileId`, `ExportProfileId`, nom du champ fichier) — construire la
  requête HTTP identique côté client Blazor, pas une réinterprétation supposée.
- [ ] Lire `GlobalExceptionHandler`/mapping `ProblemDetails` pour connaître les codes HTTP réels
  possibles (401, 404, 422, 400, 500) et préparer l'affichage de chacun distinctement.
- [ ] Vérifier l'emplacement actuel des entrées de configuration par environnement
  (`appsettings.json` de `ExcelETL.BlazorAdmin`) pour choisir où ajouter la nouvelle section
  (ex. `OxoApiTestClient:BaseUrl`, `OxoApiTestClient:ApiKey`) sans dupliquer une configuration déjà
  présente ailleurs.
- [ ] Confirmer l'emplacement du pattern accordéon/statuts réutilisable (Lot V8/R3, réutilisé au
  Lot 033) pour reproduire la même structure/CSS ici plutôt que d'en inventer une nouvelle.
- [ ] Vérifier si un projet de test HTTP client (mock de `HttpMessageHandler`) existe déjà côté
  BlazorAdmin ou WebAPI pour s'aligner sur la même convention de mock plutôt que d'en introduire
  une différente.

---

## 38.1. Configuration — base URL et clé API par environnement

**Comportement attendu** :
- Nouvelle section de configuration typée (`OxoApiTestClientOptions` ou nom équivalent confirmé en
  38.0), liée via `IOptions<T>`, exposant `BaseUrl` et `ApiKey`.
- Valeurs distinctes dans `appsettings.Development.json` (ex. `https://localhost:xxxx`) et
  `appsettings.Production.json` (vraie URL du serveur Windows cible — valeur réelle à demander à
  Simon au moment du déploiement, pas supposée ici).
- Si `BaseUrl` ou `ApiKey` sont absents/vides au démarrage : échec explicite au démarrage de
  l'application (fail-fast), pas une exception différée au premier clic sur la page — cohérent
  avec le principe "rien de silencieusement dégradé" déjà appliqué ailleurs dans le projet.

**Tests** :
- Configuration absente/vide → l'application refuse de démarrer avec un message explicite
  (test au niveau `IOptions<T>`/validation, pas un test bUnit).
- Configuration présente → les valeurs sont bien lues et exposées telles quelles (pas de
  transformation implicite, ex. pas de trim silencieux qui masquerait un espace parasite en
  configuration).

**Dossier** : `src/ExcelETL.BlazorAdmin/Configuration/OxoApiTestClientOptions.cs`,
`appsettings*.json`.

---

## 38.2. Client HTTP typé — `OxoApiTestClient`

**Comportement attendu** :
- `HttpClient` typé enregistré via `AddHttpClient<IOxoApiTestClient, OxoApiTestClient>(...)`, base
  address = `OxoApiTestClientOptions.BaseUrl`.
- Une seule méthode publique, ex. `Task<OxoApiTestResult> ProcessAsync(Guid importProfileId, Guid
  exportProfileId, Stream fileContent, string fileName, CancellationToken ct)` — construit le
  multipart exact attendu par `OxoController` (voir 38.0), ajoute l'en-tête de clé API depuis
  `OxoApiTestClientOptions.ApiKey`, appelle `POST {BaseUrl}/api/oxo/process`.
- `OxoApiTestResult` distingue explicitement : succès (flux + nom de fichier généré), rejet métier
  (422, détail des erreurs bloquantes), profil inconnu (404), non authentifié (401), erreur
  technique/inattendue (tout le reste) — un type de résultat par catégorie, pas une simple
  exception propagée telle quelle jusqu'à la page.
- **Aucune logique métier dans ce client** — il ne fait qu'appeler l'API et mapper la réponse HTTP
  vers `OxoApiTestResult` ; aucune règle de validation dupliquée ici (contrairement à
  l'observation faite dans l'audit WebAPI du 25/07 sur `Equipement is null` dupliqué ailleurs — ne
  pas reproduire ce travers ici).

**Tests** :
- Mock de `HttpMessageHandler` (pattern à confirmer en 38.0 sur l'existant du projet, sinon
  introduire un `FakeHttpMessageHandler` minimal, cohérent avec le test déjà fait côté legacy sur
  `ExcelProcessingClientService`, "mock du endpoint").
- Un cas par code HTTP retourné par `OxoController` (200, 401, 404, 422, 400, 500) → mapping
  correct vers la variante de `OxoApiTestResult` attendue.
- En-tête de clé API bien présent sur la requête sortante, avec la valeur lue depuis
  `OxoApiTestClientOptions.ApiKey`.
- Multipart bien formé : `ImportProfileId`/`ExportProfileId`/fichier présents avec les noms de
  champ exacts attendus par `OxoController` (confirmés en 38.0).

**Dossier** : `src/ExcelETL.BlazorAdmin/Services/OxoApiTestClient.cs`,
`IOxoApiTestClient.cs`, `OxoApiTestResult.cs`.

---

## 38.3. Page `ApiTest.razor` — sélection des profils et upload mono-fichier

**Route** : `/api-test`.

**Comportement attendu** :
- Deux dropdowns, `#import-profile-select`/`#export-profile-select`, peuplés via
  `IImportProfileStore.GetAllAsync()`/`IExportProfileStore.GetAllAsync()` **en process** (pas via
  HTTP — seul l'appel de traitement final passe par `OxoApiTestClient`).
- `<InputFile>` mono-fichier (`#source-file-input`), pas `multiple` (décision actée : contrat HTTP
  mono-fichier).
- Bouton `#process-button` désactivé tant que les deux profils et le fichier ne sont pas
  sélectionnés.
- Au clic : appel `IOxoApiTestClient.ProcessAsync(...)`, affichage d'un statut de chargement
  pendant l'appel (l'appel HTTP réel n'est pas instantané, contrairement au traitement en process
  des autres pages de test).
- Affichage du résultat selon la variante d'`OxoApiTestResult` :
  - **Succès** : bouton de téléchargement du fichier généré (même pattern que
    `ExportProfileTest.razor`), nom de fichier affiché.
  - **Rejet métier (422)** : liste des erreurs bloquantes, même pattern d'affichage que
    `ImportProfileTest.razor`/`ExportProfileTest.razor` pour un fichier rejeté.
  - **Profil inconnu (404)** : message explicite précisant lequel des deux identifiants est en
    cause.
  - **Non authentifié (401)** : message explicite suggérant une vérification de la configuration
    serveur (clé API) — pas un message générique, puisque l'admin ne peut rien faire côté
    navigateur pour corriger ce cas.
  - **Erreur technique/inattendue** : message générique, pas de détail technique brut exposé à
    l'écran (cohérent avec le principe déjà en place ailleurs de ne pas remonter de stack trace
    brute côté UI).

**Tests bUnit** :
- Bouton désactivé tant que profils/fichier incomplets ; activé une fois les trois présents.
- Chaque variante de résultat (`OxoApiTestResult`) rendue avec le bon contenu, via mock de
  `IOxoApiTestClient` (Moq) — pas d'appel HTTP réel dans les tests bUnit.
- IDs stables sur tous les éléments interactifs, sélection uniquement par ID dans les tests
  (jamais texte/position).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/ApiTest.razor` (+ code-behind si le
pattern du projet sépare `.razor.cs`, à confirmer en 38.0 sur les pages existantes).

---

## 38.4. Garde d'accès et intégration NavMenu

**Comportement attendu** :
- `@attribute [Authorize(Roles = IdentitySeeder.AdminRoleName)]` sur `ApiTest.razor`, identique aux
  autres pages admin (Lot J/L).
- Nouvelle entrée dans `NavMenu.razor`, dans la même section que les liens existants vers
  `ImportProfileTest`/`ExportProfileTest` (regroupement logique par proximité fonctionnelle), pas
  une section séparée.
- Vérification de la véritable absence DOM du lien quand non-authentifié (leçon du Lot L,
  `NavMenuTests.cs`).

**Tests** :
- Lien présent et navigable pour un admin authentifié.
- Lien réellement absent du DOM (pas seulement masqué en CSS) pour un utilisateur non
  authentifié — même assertion que celles introduites au Lot L.

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Layout/NavMenu.razor`.

---

## 38.5. Documentation — mise à jour de l'exception BlazorAdmin/WebAPI

**Comportement attendu** :
- `instructions-systeme-claude-code.md` déjà mis à jour manuellement (session du 26/07) pour
  annoncer par anticipation cette nouvelle exception documentée — **Claude Code doit relire ce
  document une fois ce lot livré et confirmer que la description correspond exactement au code
  réel** (nom de la page, route `/api-test`, mécanisme de configuration), et corriger le texte en
  place si un détail diverge (ex. nom de fichier différent, route finale différente) — jamais
  ajouter un second paragraphe daté à côté, remplacer le texte existant.

**Dossier** : `instructions-systeme-claude-code.md` (racine du dépôt de documentation).

---

## Note d'efficacité d'implémentation (Claude Code)

- **38.0 doit rester court** : relecture ciblée de `ExcelProcessingClientService`,
  `ApiKeyAuthenticationHandler`, `OxoController` et de la config existante — pas une relecture
  intégrale des dossiers `WebAPI`/`legacy`. S'arrêter dès que le nom de l'en-tête, le format du
  multipart, et les codes HTTP possibles sont confirmés.
- **38.1 et 38.2 peuvent être livrés dans le même commit/PR** : 38.2 dépend directement de 38.1,
  aucune raison de les séparer en deux cycles de revue.
- **38.3 dépend de 38.2** (le mock `IOxoApiTestClient` en bUnit suppose l'interface déjà stable) —
  ne pas commencer 38.3 avant que `IOxoApiTestClient`/`OxoApiTestResult` soient figés.
- **38.4 est trivial et rapide** — attacher au même commit que 38.3 plutôt qu'un cycle de revue
  séparé.
- **38.5 en tout dernier**, une fois le code réel stabilisé — éviter de documenter un nom de page
  ou une route qui pourrait encore changer pendant l'implémentation.
- Ne pas réouvrir de décision déjà actée (voir "Décisions actées avec Simon" en tête de document)
  — tout doute résiduel pendant l'implémentation doit être signalé explicitement, pas retranché
  silencieusement à nouveau.

## Ordre recommandé

1. **38.0** (investigation préalable, à garder court)
2. **38.1 + 38.2** (configuration + client HTTP typé — même PR)
3. **38.3 + 38.4** (page Blazor + NavMenu — même PR, 38.4 trivial)
4. **38.5** (mise à jour documentaire, en dernier)
