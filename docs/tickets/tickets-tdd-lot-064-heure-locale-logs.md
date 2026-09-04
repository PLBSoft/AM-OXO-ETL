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

---

## 64.3. Correctif — horodatages affichés dans le futur en production (incident, 01/09)

**Constat terrain** (deux captures prises au même instant réel sur le serveur de production) :
- `/logs` affichait la dernière entrée à `2026-09-02 01h13`.
- L'horloge système Windows du serveur affichait, au même instant, `1er septembre 2026, 23h23`.
- Fuseau Windows du serveur : `(UTC+11:00) Solomon Is., New Caledonia`, réglage manuel
  (« Set time zone automatically » = Off), synchronisation NTP active et récente.

**Ce fuseau n'est pas une anomalie.** Ce serveur est dédié à un client final basé en
Nouvelle-Calédonie (Alpha Maintenance héberge et travaille depuis la France, mais chaque
déploiement d'AM-OXO-ETL peut être un serveur dédié à un client différent, dans un fuseau
différent — c'est un cas normal et récurrent de l'architecture de déploiement de cette
application). **Aucune modification de la configuration Windows du serveur n'a été recommandée
ni effectuée** — elle est correcte telle quelle. Le code doit être robuste à n'importe quel
fuseau serveur, sans aucune hypothèse implicite sur sa valeur.

### Investigation (code réel cité, pas d'hypothèse)

Décompilation directe des packages réellement installés dans ce dépôt
(`Serilog.Sinks.MSSqlServer` 10.0.0, `Serilog.Extensions.Logging` 10.0.0 — via `ilspycmd`, aucun
accès nécessaire à un serveur de production) :

- `Serilog.Extensions.Logging.SerilogLogger.PrepareWrite` (le pont utilisé par tout `ILogger<T>`
  de cette solution, y compris tout le pipeline OXO) construit chaque `LogEvent` avec
  `LogEvent.UnstableAssembleFromParts(DateTimeOffset.Now, ...)` — une valeur qui porte
  correctement le décalage réel du serveur au moment de l'écriture, quel qu'il soit.
- `Serilog.Sinks.MSSqlServer.Output.StandardColumnDataGenerator
  .GetTimeStampStandardColumnNameAndValue` calcule, quand `ColumnOptions.TimeStamp.ConvertToUtc`
  vaut `true` (ce que `BuildSystemLogsColumnOptions()` fait déjà depuis 64.1) :
  `logEvent.Timestamp.ToUniversalTime()`, puis — comme `TimeStamp.DataType` n'est jamais fixé
  explicitement par ce dépôt et reste donc au défaut du package (`SqlDbType.DateTime`, confirmé
  en décompilant `ColumnOptions.TimeStampColumnOptions..ctor` ; jamais `DateTimeOffset`) —
  stocke `.UtcDateTime`.
- `LocalTimeFormatter.ToIso8601Utc` (64.2) force `DateTimeKind.Utc` sur la valeur lue en base
  avant sérialisation — correct **si et seulement si** la valeur stockée est déjà une vraie
  instant UTC, ce qui est le cas d'après le point précédent.
- `localTime.js` délègue entièrement à `Intl.DateTimeFormat(undefined, ...)` sur une chaîne ISO
  8601 correctement suffixée `Z`, sans aucun calcul manuel de décalage — confirmé par lecture
  directe, aucune régression trouvée côté 64.2.

**Conclusion de l'investigation** : la chaîne `DateTimeOffset.Now` → `ConvertToUtc` →
`.ToUniversalTime().UtcDateTime` → `LocalTimeFormatter` → `Intl.DateTimeFormat` est
**mathématiquement correcte pour n'importe quel décalage serveur**, y compris +11:00. Le code
source de ce dépôt tel qu'il existe aujourd'hui (commit `df5929f`, Lot 064) ne contient **pas**
un simple ré-étiquetage de `DateTimeKind` sur une valeur toujours issue de `DateTime.Now` —
l'hypothèse initiale envisagée pour cet incident. C'est un changement de source réel
(`ColumnOptions.TimeStamp.ConvertToUtc = true`, qui fait effectivement exécuter
`.ToUniversalTime()` côté sink) déjà en place.

**Explication la plus probable de l'incident observé** : l'arithmétique
`23h13 (NC, ≈ stockage local non converti) + 2h (fuseau du navigateur du client en France) =
01h13 le lendemain` correspond exactement à la capture — c'est-à-dire au comportement
**d'avant** le commit `df5929f` (aucune conversion appliquée à l'écriture, cumulée à la
conversion navigateur ajoutée par 64.2). Cette session n'a pas eu accès au serveur de production
pour confirmer directement quel binaire/quelle table `SystemLogs` étaient réellement actifs au
moment de la capture — mais le calcul concorde exactement avec « le serveur écrit encore son
heure locale telle quelle » (fix 64.1 pas encore effectif sur ce serveur au moment de cette
entrée précise — binaire non republié et/ou table déjà existante avec des lignes antérieures au
correctif), et ne concorde avec aucun défaut résiduel identifiable dans le code source actuel.

### Correctif livré

Aucun changement de code de production (l'investigation ne révèle aucun défaut dans le code
source actuel — voir ci-dessus). Durcissement par les tests uniquement,
`tests/ExcelETL.Hosting.Tests/SerilogHostLoggingExtensionsTests.cs` :

- [x] `TimeStampConversion_GivenAnyServerTimeZoneOffset_NeverProducesAnInstantLaterThanUtcNow`
  (`[Theory]`, 4 décalages dont +11:00 — l'incident réel, un décalage négatif, UTC, et un décalage
  non entier) : **l'invariant explicite demandé** — un horodatage produit par la même opération
  que celle appliquée par le sink (`DateTimeOffset.ToUniversalTime()`) n'est jamais postérieur à
  un `DateTime.UtcNow` pris juste après. Volontairement indépendant du fuseau de la machine qui
  exécute le test elle-même (CI, poste développeur en France, etc.) — documenté explicitement en
  commentaire dans le test, précisément parce que le fuseau serveur peut légitimement varier d'un
  déploiement client à l'autre.
- [x] `BuildSystemLogsColumnOptions_TimeStampColumnStaysPlainDateTime_NotDateTimeOffset` : pin le
  défaut vendeur (`SqlDbType.DateTime`) dont dépend implicitement le chemin de conversion, pour
  détecter une régression silencieuse sur un futur upgrade de `Serilog.Sinks.MSSqlServer`.
- **Limite de testabilité documentée, comme demandé, plutôt que comblée par un mécanisme non
  sollicité** : aucune abstraction d'horloge injectable n'existe dans ce dépôt pour le pipeline
  Serilog (`LogEvent.Timestamp` vient directement de l'appel BCL statique `DateTimeOffset.Now`,
  pas d'un seam contrôlé par cette solution) — introduire une telle abstraction n'a pas été fait
  ici, conformément à l'instruction de ne pas ajouter de mécanisme non demandé par le ticket
  d'origine. Un test de bout en bout réel (écriture via le sink → lecture via
  `SystemLogsDbContext` → assertion) nécessiterait une vraie base SQL Server dans la suite de
  tests, contraire à la convention déjà établie de ce fichier
  (`Configure_DoesNotOpenARealSqlConnection_WhenTheMsSqlServerSinkIsDisabled`).

**Découverte annexe, non traitée (hors périmètre de ce correctif)** : `Logs.razor`'s filtre
« Aujourd'hui »/« Hier » compare `SystemLogEntry.TimestampUtc.Date` à `DateTime.UtcNow.Date` —
une frontière de journée calculée en UTC, pas dans le fuseau de l'utilisateur qui lit le libellé
« Aujourd'hui ». C'est un défaut de filtrage distinct de l'affichage de l'heure elle-même
(64.2 ne touche pas ce filtre) et n'est pas la cause de l'incident investigué ici — signalé pour
un futur ticket, non corrigé dans ce correctif conformément à la consigne de ne pas élargir le
périmètre sans preuve que 64.2 en est également la cause.

**Action recommandée pour Simon (hors code, pas exécutée par cette session)** : au prochain
déploiement, vérifier qu'une nouvelle entrée écrite après republication du binaire (Lot 064,
commit `df5929f` ou plus récent) s'affiche correctement — la base sera de toute façon recréée
avant publication (voir « Décisions actées », pas de migration des lignes déjà erronées).

### Suite (même jour) — le correctif 64.3 confirmé, un second cas identique repéré

Simon a confirmé, sur le serveur réel republié, que les logs affichent désormais l'heure locale
correcte. Dans la foulée, il a repéré exactement le même défaut sur un affichage qui avait été
**délibérément exclu** du périmètre de ce lot lors de l'arbitrage 64.0 : le pied de page « Publié
le... » (`ApplicationBuildInfo.BuildDateUtc`, `NavMenu.razor`/`Home.razor`) affichait « mardi 1
septembre 2026, 12h57 » pour une publication réellement effectuée à 14h57 (heure française) —
même cause que le bug des logs (valeur UTC affichée telle quelle, seuls les noms de jour/mois
étaient localisés), mais sur un mécanisme différent (jamais branché sur `ILocalTimeFormatter`).

**Décision, demandée explicitement à Simon avant d'agir (réouverture d'une exclusion de
périmètre actée)** : corriger maintenant, en réutilisant le même mécanisme que pour les logs.

**Correctif** : `NavMenu.razor`/`Home.razor` injectent désormais `ILocalTimeFormatter` et
convertissent `BuildDateUtc` en heure locale via `FormatAsync(buildDateUtc, "yyyy-MM-dd HH:mm")`
(seul motif numérique supporté par `localTime.js`), puis re-parsent la chaîne locale obtenue en
`DateTime` pour que `CultureInfo.CurrentUICulture` puisse toujours produire les noms de
jour/mois corrects — pas de nouvelle méthode sur `ILocalTimeFormatter`, juste une réutilisation
du motif existant. Repli sur l'ancienne valeur UTC brute tant que la conversion n'a pas eu lieu
(même convention que le reste du lot 064). Aucune nouvelle clé `.resx` (réutilise
`NavMenu_BuildDateTooltip` telle quelle).

Détail complet dans `CLAUDE.md` (bullet « Follow-up, same day » sous le Lot 064). Suite de tests
`ExcelETL.BlazorAdmin.Tests` : 965/965 verts (dont 2 nouveaux tests couvrant explicitement la
conversion, avec un scénario qui traverse minuit pour prouver que le libellé compact change
réellement, pas seulement l'infobulle).

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
