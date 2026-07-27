# Tickets TDD — Lot 034 : archivage des fichiers source et cible générés via l'API OXO

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Cinquième lot
utilisant la convention numérique à trois chiffres, après le lot 033
(`tickets-tdd-lot-033-upload-multiple-fichiers-pages-test.md`).*

**Demande client (remontée par Simon, session du 25/07)** : le client veut pouvoir retrouver un
fichier — source ou cible — généré via l'API à la demande de l'application legacy
(`POST /api/oxo/process`), y compris plusieurs jours après. Aujourd'hui, aucune trace n'existe :
le fichier source est lu en mémoire et le fichier cible est retourné en synchrone dans le corps de
la réponse HTTP, sans aucune persistance (voir `tickets-tdd-migration-webapi-oxo.md`, Lot K —
« pas d'archivage, ne pas assumer de persistance filesystem des fichiers générés »). Ce point
n'avait jamais été demandé explicitement avant ce jour ; conforme au principe déjà en place de ne
rien construire par anticipation (voir `tickets-tdd-extraction.md`, `tickets-tdd-seed-profils-defaut.md`).

**Décisions actées avec Simon (25/07)** :
- **Périmètre : API uniquement.** `POST /api/oxo/process` (canal réellement utilisé en
  production par le client). `ExportProfileTest.razor`/`ImportProfileTest.razor` restent des
  outils de test/configuration internes, non concernés par ce lot.
- **Stockage : filesystem** (Option B), pas de BLOB en base — volumétrie estimée faible (fichiers
  source entre ~1 et ~6 Mo, fichiers cible entre ~20 et ~100 Ko, usage occasionnel côté client) ;
  un admin peut se connecter en accès distant sur le serveur pour consultation manuelle directe,
  cas d'usage explicitement cité par Simon.
- **Métadonnées en base SQL Server** (EF Core, même style que le reste : `IDbContextFactory`, pas
  de `DbContext` scopé), pour permettre une recherche sans avoir à parcourir le filesystem.
- **Nommage des fichiers archivés** : horodatage `yyyyMMdd-HHmmss-fff` (précision milliseconde,
  décision actée le 25/07 pour éviter toute collision de nom en cas d'appels rapprochés, tout en
  restant trié naturellement et lisible dans l'explorateur Windows), arborescence `{yyyy}\{MM}\`.
- **Archivage systématique, y compris en cas de rejet de l'import** (`Equipement is null`,
  validation PROCEDURE) : le fichier source est conservé même si aucun fichier cible n'est
  généré — cas d'usage explicite de Simon (« preuve que les données sources étaient corrompues »).
- **Accès en consultation : Blazor uniquement, réservé aux admins** (`AuthorizeView`, même
  garde que le reste de l'admin — voir Lot L). Pas d'exposition via l'API pour l'instant
  (YAGNI — à ouvrir plus tard si le besoin se manifeste réellement).
- **Recherche simple** : par repère d'équipement (`EquipementRepere`, dénormalisé en base pour
  éviter de rouvrir chaque fichier à la recherche), insensible à la casse.
- **Pas de purge/rétention automatique dans ce lot** — la politique de rétention n'est pas encore
  connue côté client, à traiter dans un lot séparé si le besoin se précise.

**Conventions déjà en place à respecter (tout le lot)** : `IDbContextFactory<T>` injecté par
repository, jamais de `DbContext` scopé (BlazorAdmin *et* WebAPI, cohérence déjà actée) ; IDs HTML
stables, jamais de sélection par texte/position en bUnit ; xUnit 2.9.3 + FluentAssertions 7.x +
Moq + bUnit ; `WebApplicationFactory` pour les tests d'intégration Web API ; EF Core InMemory
provider pour les tests de repository (jamais mocké au niveau `DbContext`) ; construction directe
de l'objet Domain réel dans un `try/catch`, erreurs localisées via `BusinessExceptionLocalizer` ;
`AuthorizeView Roles="@IdentitySeeder.AdminRoleName"` pour toute nouvelle page admin, avec
vérification de la véritable absence DOM quand non-authentifié (leçon du Lot L).

---

## Hors périmètre explicite de ce lot

- Archivage des fichiers générés via `ExportProfileTest.razor`/`ImportProfileTest.razor`
  (Blazor) — ce sont des outils de test, pas le canal client.
- Purge automatique / politique de rétention (durée de conservation, suppression programmée) —
  aucune durée n'est encore connue côté client ; lot séparé le jour où elle sera précisée.
- Exposition d'un endpoint API pour consulter/retélécharger un fichier archivé depuis
  l'application legacy — accès Blazor admin uniquement pour ce lot.
- Recherche avancée (filtres multiples, export CSV de la liste, pagination serveur) — une liste
  simple triée par date décroissante + recherche texte sur le repère suffit pour ce lot.
- Compression, déduplication, ou déplacement vers un stockage externe (Azure Blob, S3-compatible)
  — aucun signal que ce soit nécessaire, l'infra est on-premise.
- Modification du contrat HTTP existant de `/api/oxo/process` (codes retour, contenu de la
  réponse) — l'archivage est un effet de bord, pas un changement de contrat.
- Gestion des collisions résiduelles au-delà de la précision milliseconde (jugée suffisante en
  usage réel occasionnel ; à revisiter uniquement si un incident réel le justifie).

---

## 34.0. Investigation préalable (obligatoire avant tout code)

- [ ] Lire l'implémentation actuelle de `POST /api/oxo/process` (`OxoController` ou équivalent,
  nom exact à confirmer — voir Lot K) et le service d'orchestration HTTP dédié s'il existe, pour
  localiser précisément où insérer l'étape d'archivage sans dupliquer la logique de résolution
  des profils déjà en place.
- [ ] Confirmer la convention de configuration déjà utilisée dans le projet pour les chemins/
  paramètres d'environnement (`appsettings.json` + section dédiée, ou `IOptions<T>` existant) —
  s'aligner dessus pour le chemin racine d'archivage plutôt que d'introduire un nouveau
  mécanisme de configuration.
- [ ] Lire `ImportResult` (Application/Domain) pour confirmer le chemin d'accès exact au repère
  de l'Équipement parent (`Repere`) et son état en cas de rejet (`Equipement is null` — le champ
  `EquipementRepere` de l'archive sera alors `null`, à gérer explicitement, pas une exception).
- [ ] Lire le pattern `EfImportProfileStore`/`EfExportProfileStore` (Lot E/H) pour répliquer
  exactement le même style de repository EF Core (interface Application, implémentation
  Infrastructure, configuration EF Core dédiée, migration associée).
- [ ] Confirmer qu'aucun mécanisme d'écriture fichier sur disque n'existe déjà ailleurs dans
  `ExcelETL.Infrastructure` qui pourrait être réutilisé/étendu plutôt que dupliqué.

---

## 34.1. Domain/Application — modèle d'archive et interface de persistance

**Comportement attendu** :
- Nouvelle entité `GeneratedFileRecord` (Domain, zéro dépendance) :
  - `Id` (`Guid`)
  - `GeneratedAtUtc` (`DateTime`)
  - `EquipementRepere` (`string?`, `null` si rejet avant résolution de l'Équipement)
  - `SourceFileName` (`string`, nom d'origine du fichier uploadé)
  - `SourceFilePath` (`string`, chemin relatif à la racine d'archivage — jamais de chemin absolu
    stocké, pour rester portable si la racine change)
  - `TargetFileName` (`string?`, `null` si aucun fichier cible n'a été généré)
  - `TargetFilePath` (`string?`, idem)
  - `ImportProfileId` (`Guid`)
  - `ExportProfileId` (`Guid?`, `null` si le pipeline s'est arrêté avant la génération — la
    résolution du profil d'export peut ne jamais avoir lieu en cas de rejet précoce, à confirmer
    en 34.0)
  - `Status` (enum `GeneratedFileArchiveStatus` : `Success`, `NonBlockingWarning`, `Rejected` —
    même sémantique que les statuts déjà affichés côté Blazor, Lot 033)
- Interface `IGeneratedFileArchiveStore` (Application layer), miroir de
  `IImportProfileStore`/`IExportProfileStore` :

```csharp
public interface IGeneratedFileArchiveStore
{
    Task SaveAsync(GeneratedFileRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<GeneratedFileRecord>> SearchAsync(string? equipementRepere, CancellationToken ct = default);
    Task<GeneratedFileRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
```

**Tests** (xUnit, Domain/Application, sans dépendance EF) :
- Construction d'un `GeneratedFileRecord` valide (cas `Success`) → toutes les propriétés
  correctement assignées.
- Construction d'un `GeneratedFileRecord` en cas de rejet → `EquipementRepere` et
  `TargetFileName`/`TargetFilePath` acceptent `null` sans lever d'exception (pas d'invariant
  Domain interdisant ce cas, contrairement à d'autres entités du projet qui valident strictement
  — à documenter explicitement pourquoi celle-ci est permissive).

**Dossiers** : `src/ExcelETL.Domain/Archiving/GeneratedFileRecord.cs`,
`src/ExcelETL.Application/Archiving/IGeneratedFileArchiveStore.cs`.

---

## 34.2. Infrastructure — persistance EF Core des métadonnées

**Comportement attendu** :
- `EfGeneratedFileArchiveStore : IGeneratedFileArchiveStore`, contre le vrai provider EF Core
  InMemory (jamais mocké), même style que `EfImportProfileStore`.
- `SearchAsync(equipementRepere: null)` → retourne tous les enregistrements triés par
  `GeneratedAtUtc` décroissant.
- `SearchAsync(equipementRepere: "C7401")` → filtre `Contains`, insensible à la casse (cohérent
  avec la recommandation `.Trim()` + comparaison insensible à la casse déjà en place pour
  `Colonne.Nom`/`TypeElement.Nom`, `spec-extraction-fichier-source-oxo.md` §7).
- Configuration EF Core (`GeneratedFileRecordConfiguration`) dans
  `src/ExcelETL.Infrastructure/Persistence/Configurations/`, migration associée.

**Tests** (xUnit, EF Core InMemory) :
- `SaveAsync` puis `GetByIdAsync` → round-trip complet, toutes les propriétés relues à
  l'identique, y compris les champs `null` (cas rejet).
- `SearchAsync` sans filtre → ordre décroissant par date vérifié explicitement (pas seulement la
  présence des éléments).
- `SearchAsync("c7401")` (minuscules) → retrouve un enregistrement dont `EquipementRepere` vaut
  `"C7401"` (test dédié insensibilité à la casse).
- `SearchAsync` sur un repère absent → liste vide, pas d'exception.

**Dossier** : `src/ExcelETL.Infrastructure/Persistence/Repositories/EfGeneratedFileArchiveStore.cs`.

---

## 34.3. Infrastructure — écriture sur disque des fichiers archivés

**Comportement attendu** :
- Interface `IGeneratedFileWriter` (Application), implémentation `FileSystemGeneratedFileWriter`
  (Infrastructure) :

```csharp
public interface IGeneratedFileWriter
{
    Task<string> WriteSourceAsync(Stream content, string originalFileName, DateTime timestampUtc, CancellationToken ct = default);
    Task<string> WriteTargetAsync(Stream content, string originalFileName, DateTime timestampUtc, CancellationToken ct = default);
}
```
  (retourne le chemin relatif écrit, à stocker tel quel dans `GeneratedFileRecord`).
- Racine configurable via `appsettings.json` (section `GeneratedFilesArchive:RootPath`, ex.
  `D:\AM-OXO-ETL\generated-files` en production ; un chemin temporaire dédié en environnement de
  test — jamais le même dossier que les tests d'un autre lot, pour éviter toute pollution
  croisée).
- Arborescence : `{RootPath}\{yyyy}\{MM}\{yyyyMMdd-HHmmss-fff}_source_{NomFichierOriginalAssaini}.xlsx`
  et `..._target_{NomFichierOriginalAssaini}.xlsx` — création automatique des sous-dossiers
  `{yyyy}\{MM}` si absents.
- **Assainissement défensif du nom de fichier d'origine** avant écriture : remplacement des
  caractères interdits Windows (`\ / : * ? " < > |`) par `_` — même logique défensive que celle
  déjà actée pour le nommage des feuilles Excel Tâches Multiples (`tickets-tdd-export-taches-multiples.md`,
  T4), appliquée ici au nom de fichier plutôt qu'au nom de feuille.
- Aucune tentative de retry/gestion de collision au-delà de la précision milliseconde du nom —
  décision actée, cas résiduel jugé négligeable en usage réel occasionnel.

**Tests** (xUnit, Infrastructure, contre un dossier temporaire réel — pas de mock du filesystem) :
- Écriture d'un flux source → fichier physiquement présent au chemin retourné, contenu identique
  octet pour octet à l'entrée.
- Écriture répétée à la même milliseconde simulée (horloge injectée/mockable) → deux fichiers
  distincts si le nom d'origine diffère ; comportement documenté (pas de garde spécifique) si le
  nom d'origine et l'horodatage coïncident exactement — test qui caractérise le comportement réel
  plutôt que d'imposer une garantie non actée.
- Nom de fichier d'origine contenant un caractère interdit Windows (ex. `"dossier:test.xlsx"`) →
  fichier écrit avec succès, caractère remplacé par `_`, pas d'exception.
- Dossier `{yyyy}\{MM}` absent au départ → créé automatiquement, écriture réussie.

**Dossier** : `src/ExcelETL.Infrastructure/Archiving/FileSystemGeneratedFileWriter.cs`.

---

## 34.4. WebAPI — câblage de l'archivage dans le pipeline `/api/oxo/process`

**Comportement attendu** :
- Après résolution des profils et lecture du fichier source, **avant** de retourner la réponse
  HTTP (succès ou rejet), le service d'orchestration :
  1. Écrit le fichier source via `IGeneratedFileWriter.WriteSourceAsync(...)`.
  2. Si la génération aboutit (succès ou avertissement non bloquant type VANNE), écrit le fichier
     cible via `WriteTargetAsync(...)`.
  3. Construit et persiste un `GeneratedFileRecord` via `IGeneratedFileArchiveStore.SaveAsync(...)`
     avec le statut approprié (`Success`/`NonBlockingWarning`/`Rejected`) et
     `TargetFileName`/`TargetFilePath` à `null` en cas de rejet.
- **L'échec de l'archivage ne doit pas faire échouer la requête HTTP principale** : si l'écriture
  disque ou la persistance en base échoue (ex. disque plein, base indisponible), le fichier généré
  est tout de même retourné au client dans la réponse HTTP (contrat existant préservé), et
  l'échec de l'archivage est journalisé via le mécanisme Serilog déjà en place (`SystemLogs`) —
  décision explicite à documenter dans le code (l'archivage est un effet de bord best-effort, pas
  une garantie transactionnelle bloquante du flux principal).
- Aucun changement du contrat HTTP externe (codes retour, format de réponse) au-delà de ce qui
  est déjà défini par le Lot K.

**Tests** (xUnit + `WebApplicationFactory`) :
- Requête réussie contre une fixture réelle (ex. C7401) → réponse 200 inchangée **et** un
  `GeneratedFileRecord` avec `Status = Success` est persisté, fichiers source et cible présents
  sur le disque de test.
- Requête sur D8570 (cas VANNE) → réponse 200 inchangée **et** `Status = NonBlockingWarning`
  persisté, fichiers présents.
- Requête avec fichier synthétique invalide (`Equipement is null`) → réponse d'erreur inchangée
  (comportement Lot K préservé) **et** `Status = Rejected` persisté, fichier source présent sur
  disque, `TargetFilePath` à `null`, aucun fichier cible écrit.
- Panne simulée de l'écriture disque (ex. `IGeneratedFileWriter` mocké levant une exception) →
  la réponse HTTP au client reste 200 avec le fichier généré normalement (non-régression du
  contrat principal), l'échec est journalisé (vérifiable via un logger injecté/mocké).
- Non-régression complète des tests d'intégration existants du Lot K (aucune assertion existante
  modifiée).

**Dossier** : service d'orchestration HTTP existant du Lot K (`src/ExcelETL.WebAPI/` ou
`src/ExcelETL.Application/`, selon ce que confirme l'investigation 34.0).

---

## 34.5. Blazor — page de consultation `/generated-files` (admin uniquement)

**Comportement attendu** :
- Nouvelle route `/generated-files`, protégée par `<AuthorizeView Roles="@IdentitySeeder.AdminRoleName">`
  (même garde que le reste de l'admin, vraie absence DOM si non-authentifié — leçon du Lot L, à
  vérifier explicitement par un test dédié plutôt que supposée).
- Entrée de menu ajoutée dans `NavMenu.razor`, dans la section admin existante (pas de nouvelle
  section de menu à inventer).
- Liste des `GeneratedFileRecord` via `IGeneratedFileArchiveStore.SearchAsync(...)`, triée par
  date décroissante (délégué au store, pas de tri client-side).
- Champ de recherche texte simple (`#generated-files-search-input`) sur le repère d'équipement,
  déclenchant `SearchAsync` (pas de filtrage client-side sur une liste déjà chargée — cohérent
  avec le fait que la liste peut grossir dans le temps).
- Par ligne : date de génération, repère (ou `"—"` si absent, cas rejet), badge de statut
  (réutilisation du pattern de badges déjà en place, Lot 033), bouton téléchargement fichier
  source (toujours actif), bouton téléchargement fichier cible (masqué ou désactivé si
  `TargetFilePath` est `null`).
- Téléchargement : lecture du fichier depuis le chemin stocké (relatif + racine configurée),
  déclenchement du téléchargement navigateur — pas de nouvel appel HTTP vers l'API externe, accès
  direct filesystem depuis BlazorAdmin (cohérent avec l'architecture : BlazorAdmin référence déjà
  Infrastructure directement).
- Pas de pagination serveur dans ce lot (liste complète chargée) — acceptable vu le volume estimé
  faible ; à revisiter si le volume réel dépasse les attentes.

**Tests** (bUnit) :
- Utilisateur non authentifié → route `/generated-files` absente du DOM du menu **et** accès
  direct à l'URL ne rend pas le contenu de la page (vraie absence, pas juste un lien caché).
- Liste de `GeneratedFileRecord` mockée (3 éléments, statuts variés dont un `Rejected` sans
  `TargetFilePath`) → 3 lignes rendues, badge de statut correct par ligne, bouton téléchargement
  cible absent/désactivé pour la ligne `Rejected`.
- Saisie dans le champ de recherche + déclenchement → `SearchAsync` appelé avec le terme saisi
  (vérifiable via `Mock<IGeneratedFileArchiveStore>`), liste mise à jour avec le résultat retourné
  par le mock.
- Clic sur le bouton de téléchargement source/cible → déclenche le mécanisme de téléchargement
  attendu (même pattern que `ExportProfileTest.razor`, Lot J3/X1).

**Dossier** : `src/ExcelETL.BlazorAdmin/Components/Pages/Admin/GeneratedFiles.razor`
(+ miroir tests), `NavMenu.razor`.

---

## 34.6. Câblage DI (`Program.cs`, `ExcelETL.WebAPI` et `ExcelETL.BlazorAdmin`)

**Tests** : vérification légère (même convention que les lots précédents, Lot J4) que sont bien
enregistrés dans les deux hôtes :
- `IGeneratedFileArchiveStore`/`EfGeneratedFileArchiveStore` (`AddScoped`, même style que les
  autres stores EF).
- `IGeneratedFileWriter`/`FileSystemGeneratedFileWriter` (`AddSingleton`, sans état mutable
  partagé dangereux).
- Section de configuration `GeneratedFilesArchive` correctement liée (`IOptions<T>` ou équivalent
  déjà en convention dans le projet).

**Dossiers** : `src/ExcelETL.WebAPI/Program.cs`, `src/ExcelETL.BlazorAdmin/Program.cs`.

---

## Ordre recommandé

1. **34.0** (investigation — conditionne tout le reste, notamment le point de câblage exact dans
   le pipeline API existant du Lot K)
2. **34.1** (modèle Domain/Application — aucune dépendance, base des deux couches suivantes)
3. **34.2** et **34.3** en parallèle possible (persistance base / écriture disque — deux
   responsabilités indépendantes, aucune dépendance croisée entre elles)
4. **34.4** (câblage API — dépend de 34.1/34.2/34.3)
5. **34.6** (DI — peut être fait dès que 34.2/34.3 compilent, avant 34.5)
6. **34.5** (page Blazor de consultation — dépend de 34.2 pour la lecture, dernier car le moins
   critique fonctionnellement : le besoin client porte d'abord sur le fait que l'archive existe,
   la consultation UI est le confort qui vient ensuite)

## Note d'efficacité d'implémentation

- 34.1 est un pur ajout Domain/Application sans dépendance externe : à livrer et valider avant
  de toucher à EF Core ou au filesystem, pour isoler tout problème de compilation/logique du
  reste.
- 34.3 (écriture disque) doit être testé contre un vrai dossier temporaire, jamais un mock du
  filesystem — cohérent avec la convention déjà en place « EF Core InMemory réel, jamais mocké au
  niveau DbContext », transposée ici à l'I/O fichier.
- Ne pas chercher à unifier `IGeneratedFileWriter` avec `IWorkbookReader`/`ClosedXmlWorkbookWriter`
  existants : responsabilités différentes (l'un lit/écrit des classeurs Excel en mémoire, l'autre
  archive des flux bruts sur disque) — les faire cohabiter sans lien de dépendance artificiel.
- Le point le plus sensible du lot est 34.4 (le non-échec de la requête HTTP principale en cas de
  panne d'archivage) : à tester en isolation avec un `IGeneratedFileWriter` mocké levant une
  exception, avant de brancher l'implémentation réelle — évite de découvrir un couplage trop fort
  uniquement en fin de lot.
