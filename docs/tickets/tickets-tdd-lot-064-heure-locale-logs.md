# Tickets TDD — Lot 064 : affichage des heures de logs en heure locale du navigateur

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Suit le lot 063
(`tickets-tdd-lot-063-condition-zero-energie-isolement.md`).*

**Origine** : signalé par Simon (01/09) lors d'un test réel sur `/api-test` — le serveur de
production n'est pas sur le même fuseau horaire que le poste client, et la page « Journaux »
affiche les horodatages tels quels (`2026-09-01 20:43:27` pour un test effectué à 11h46 heure
locale), rendant la corrélation avec un incident vécu inutilement pénible.

**Constat préalable** : `SystemLogsDbContext` est en lecture seule, son schéma physique est créé
par le sink Serilog `MSSqlServer` (`AutoCreateSqlTable = true`), pas par EF Core
(`etat-des-lieux-technique-2026-08-31.md`). La configuration Serilog partagée vit dans
`ExcelETL.Hosting/SerilogHostLoggingExtensions.cs` (`AddOxoHostLogging`, Lot G3) — seul point
d'entrée pour les deux hôtes (`WebAPI`, `BlazorAdmin`).

---

## Décisions actées avec Simon (01/09, non négociables)

- **Aucune configuration de fuseau horaire dans BlazorAdmin.** Ni champ de paramétrage, ni valeur
  en dur type `"Europe/Paris"`, ni dépendance à la configuration du serveur IIS. La conversion
  doit être **transparente pour l'utilisateur**, quel que soit son fuseau.
- **Approche la plus simple retenue** : conversion **côté navigateur**, à partir du fuseau horaire
  du poste client lui-même (déjà connu du navigateur, sans qu'aucune configuration serveur
  n'intervienne) — pas de saisie, pas de préférence à mémoriser.
- **Pas de migration de données** : les entrées déjà présentes dans `SystemLogs` ne sont pas
  corrigées rétroactivement. Seul le comportement d'écriture/affichage futur change.

---

## 64.0. Investigation préalable (obligatoire avant tout code)

- [x] Confirmer, par lecture de `SerilogHostLoggingExtensions.cs` et de la configuration Serilog
  effective (`appsettings.json`/sink `MSSqlServer`), quel horodatage est réellement écrit
  aujourd'hui dans la colonne `TimeStamp` de `SystemLogs` : heure locale de la machine hôte
  (comportement par défaut de Serilog si rien n'est configuré explicitement) ou UTC. **Confirmé** :
  aucune configuration explicite (`ConvertToUtc` absent) — le sink `MSSqlServer` écrit par défaut
  l'heure locale du serveur hôte (`DateTimeOffset.Now`, offset tronqué), documenté noir sur blanc
  dans le README du package (`ConvertToUtc = false` par défaut, "the time stamp value reflects the
  local time of the machine issuing the log event"). Aucun accès à la base de production réelle
  dans cette session — hypothèse tranchée par lecture de code/documentation officielle du package,
  pas par supposition.
- [x] Grep exhaustif de `src/ExcelETL.BlazorAdmin/` pour tout autre endroit affichant un
  horodatage brut issu de la base — révèle aussi `GeneratedFiles.razor` (`GeneratedAtUtc`, colonne
  date + carte mobile) et `Home.razor` (`lastGenerationAtUtc`, tuile « dernière génération »).
  **Signalé à Simon avant extension, périmètre confirmé élargi à ces deux pages** (question posée
  via AskUserQuestion, réponse : « Inclure GeneratedFiles.razor et Home.razor »). Le build-date
  (`ApplicationBuildInfo.BuildDateUtc`, affiché dans `NavMenu.razor`/`Home.razor`) reste hors
  périmètre — non nommé dans la réponse de Simon, et de nature différente (date de publication, pas
  un horodatage à corréler avec un incident vécu par le client).
- [x] Identifier le composant Blazor exact rendant la page Journaux : `Logs.razor`
  (`src/ExcelETL.BlazorAdmin/Components/Pages/Admin/`). Mécanisme précédent :
  `entry.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss")`, aucun ID HTML sur la cellule.
- [x] Identifier le repository et le type .NET : `ISystemLogRepository`/`SystemLogRepository`
  (`ExcelETL.Infrastructure.Diagnostics`), transporte un `DateTime` (`SystemLogEntry.TimestampUtc`,
  mappé sur la colonne physique `TimeStamp` — déjà nommé « Utc » avant ce lot alors que ce n'était
  pas garanti, source du problème). Kind généralement `Unspecified` en sortie d'EF Core — condition
  la forme de l'appel JS interop en 64.2 (voir `LocalTimeFormatter.ToIso8601Utc`, qui force
  `DateTimeKind.Utc` avant sérialisation).

**Hors périmètre de cette investigation** : toute page découverte par le grep mais non listée
explicitement dans le tableau de résultat n'est pas traitée silencieusement dans ce lot — si le
grep révèle d'autres emplacements, les lister à Simon avant d'étendre le périmètre d'implémentation.

---

## 64.1. Infrastructure — garantir un stockage UTC fiable

**Comportement attendu** : si l'investigation 64.0 confirme que l'horodatage écrit n'est pas UTC,
configurer explicitement `AddOxoHostLogging` pour que le sink écrive systématiquement en UTC
(propriété `Timestamp` du `LogEvent`, ou équivalent selon ce que révèle l'investigation) — sans
changer le format d'affichage côté Serilog `Console` (hors périmètre, non demandé). Si
l'investigation confirme que c'est déjà le cas, ce sous-ticket se limite à l'ajout d'un test de
non-régression explicite (aucun changement de code de production).

**Tests** (xUnit, `tests/ExcelETL.Hosting.Tests/SerilogHostLoggingExtensionsTests.cs`, même patron
que les tests existants, `Serilog:EnableMsSqlServerSink=false`) :
- [x] **Équivalent choisi** : `TimeStamp.ConvertToUtc` (propriété officielle de
  `Serilog.Sinks.MSSqlServer.ColumnOptions`, documentée pour exactement ce cas d'usage) ne s'active
  qu'au moment de l'écriture SQL elle-même, pas sur le `LogEvent` partagé visible par d'autres sinks
  — donc pas assertable via un sink `CapturingSink` sans ouvrir une vraie connexion SQL Server
  (que les tests de ce fichier évitent explicitement). `Configure` extrait la construction des
  `ColumnOptions` dans une méthode publique dédiée, `BuildSystemLogsColumnOptions()`, directement
  assertable — même convention que `ApplicationIdentityDbContextModelTests` (asserter la
  configuration déclarée, pas le comportement réel de SQL Server).
- [x] Non-régression : les 6 tests existants de ce fichier restent verts sans modification de leur
  intention (7ᵉ test ajouté, aucun existant modifié).

**Dossier** : `src/ExcelETL.Hosting/SerilogHostLoggingExtensions.cs`.

---

## 64.2. BlazorAdmin — conversion à l'affichage via interop JS

**Comportement attendu** : la colonne Heure de la page Journaux (composant identifié en 64.0)
convertit chaque horodatage UTC en heure locale du navigateur au moment du rendu, via `IJSRuntime`
et l'API navigateur standard (`Intl.DateTimeFormat`/`toLocaleString`, fuseau détecté automatiquement
par le navigateur — jamais transmis explicitement depuis Blazor). Aucun état de configuration,
aucune préférence utilisateur à créer ou persister.

**Périmètre élargi (accord Simon, voir 64.0)** : le même mécanisme est appliqué à
`GeneratedFiles.razor` (colonne date + carte mobile) et `Home.razor` (tuile « dernière
génération »), pas seulement à `Logs.razor`.

**Implémentation** :
- `wwwroot/js/localTime.js` (script global, même convention que `theme.js`) : `window.amOxoLocalTime
  .format(isoUtc, pattern)` / `.formatMany(isoUtcValues, pattern)`, basés sur
  `Intl.DateTimeFormat(undefined, ...).formatToParts(...)` (fuseau du navigateur, jamais transmis) —
  `pattern` ne supporte que les jetons littéraux `yyyy`/`MM`/`dd`/`HH`/`mm`/`ss` déjà utilisés par
  les 3 pages concernées.
- `Services/ILocalTimeFormatter.cs`/`LocalTimeFormatter.cs` : enveloppe `IJSRuntime`, force
  `DateTimeKind.Utc` avant `.ToString("o")` (une chaîne ISO 8601 sans suffixe `Z` est interprétée
  par `new Date(...)` comme une heure **locale** du navigateur, pas UTC — piège identifié et
  neutralisé explicitement). `FormatManyAsync` bat en un seul aller-retour JS pour toute une liste
  (tables Logs/GeneratedFiles), `FormatAsync` pour une valeur unique (Home).
- Chaque page appelle `LocalTimeFormatter` depuis `OnAfterRenderAsync`, gardé par
  `RendererInfo.IsInteractive` (l'interop JS échoue pendant le prérendu statique — même garde que
  `PasswordChangeGuard.razor`, Lot 045), avec un cache par jeu d'ids visibles pour éviter une boucle
  JS → `StateHasChanged` → rendu → JS. Tant que la conversion n'a pas eu lieu (prérendu, ou avant le
  premier `OnAfterRenderAsync` interactif), la cellule affiche l'ancien format UTC brut en
  repli — flash bref et documenté, jamais une cellule vide.
- Libellé de colonne `Logs_TimeUtc` → `Logs_TimeLocal` (« Time (UTC) » → « Time »), pour ne pas
  afficher « UTC » à côté d'une valeur désormais locale.

**Tests** (bUnit, `ExcelETL.BlazorAdmin.Tests`) :
- [x] `IJSRuntime` substitué (`Mock<IJSRuntime>`, `LocalTimeFormatterTests.cs`) : vérifie que
  l'appel JS reçoit bien une représentation ISO 8601 **se terminant par `Z`** (UTC réelle, pas une
  chaîne déjà pré-formatée côté serveur ni une chaîne locale-par-défaut ambiguë).
- [x] ID HTML stable sur la cellule Heure (`#log-timestamp-{id}`, `#generated-file-date-{id}`/
  `-card-{id}`, `#home-kpi-last-generation-value`) — sélection par ID dans tous les nouveaux tests,
  jamais par texte/position.
- [x] Un test par page prouve le repli UTC en mode non interactif (défaut bUnit) et la bascule
  effective vers le résultat d'un `ILocalTimeFormatter` substitué une fois
  `SetRendererInfo(..., isInteractive: true)` posé.
- **Limite documentée** : bUnit s'exécute sans navigateur réel, donc la conversion de fuseau
  elle-même (résultat visuel final produit par `localTime.js`) ne peut pas être vérifiée par un test
  automatisé — seule la mécanique d'appel (bon format transmis, bon ciblage HTML, bascule
  effective) l'est. Une ligne a été ajoutée à la checklist de premier démarrage du
  `guide-deploiement-am-oxo-etl-windows-server.md` pour la vérification visuelle manuelle réelle.

**Dossier** : `Logs.razor`, `GeneratedFiles.razor`, `Home.razor`, `Services/ILocalTimeFormatter.cs`,
`Services/LocalTimeFormatter.cs`, `wwwroot/js/localTime.js` (`src/ExcelETL.BlazorAdmin/`).

---

## Hors périmètre explicite de ce lot

- `ApplicationBuildInfo.BuildDateUtc` (date de publication, affichée dans `NavMenu.razor`/
  `Home.razor`) — grep-détecté mais non retenu par Simon lors de l'arbitrage de périmètre (64.0) ;
  différente nature qu'un horodatage de log/génération à corréler avec un incident vécu.
- Toute autre page BlazorAdmin non listée par le grep de 64.0 — si le grep en révèle d'autres,
  elles sont signalées à Simon, pas traitées silencieusement dans ce lot.
- Correction rétroactive des horodatages déjà écrits dans `SystemLogs` avant ce lot — décision
  actée, pas de migration de données.
- Tout paramétrage de fuseau horaire, y compris optionnel ou avec valeur par défaut — explicitement
  écarté par Simon.
- Le format d'affichage du sink `Console` (logs de la console IIS/Event Viewer) — seule la page
  Journaux de BlazorAdmin est concernée.

---

## Ordre recommandé

1. **64.0** (investigation — conditionne entièrement le contenu de 64.1 et le détail technique de
   64.2)
2. **64.1** (fondation : sans horodatage UTC fiable en base, 64.2 convertirait à partir d'une base
   déjà fausse)
3. **64.2** (affichage — dépend de 64.1)

## Note d'efficacité d'implémentation

- Ce lot est court mais **64.0 en est le vrai centre de gravité** : toute la suite dépend d'un fait
  vérifié (UTC ou non) et non d'une hypothèse — ne pas écrire 64.1 avant d'avoir la réponse.
- Risque d'implémentation faible une fois 64.0 tranché : aucun changement de comportement métier,
  uniquement de présentation.
