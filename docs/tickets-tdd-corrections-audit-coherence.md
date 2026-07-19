# Tickets TDD — corrections issues de l'audit de cohérence globale

*Nouveau document, créé le 2026-07-19. Note de transparence : la demande qui a déclenché ce
document faisait référence à ce fichier comme déjà existant, avec une section G3 déjà mise à
jour — recherche exhaustive faite avant d'écrire quoi que ce soit (`git log --all --oneline --
"*tickets-tdd-corrections*"`, plus un parcours du répertoire `docs/`) : **ce fichier n'existait
nulle part dans le dépôt avant cette session**, ni sur disque, ni dans l'historique git, sur
aucune branche. Il est donc créé ici pour la première fois, à partir des écarts déjà listés dans
`docs/audit-coherence-globale-2026-07-17.md` (section "Écarts à corriger") et de l'instantané
`docs/etat-avancement-lot-g-logging-oxo-2026-07-19.md` (qui posait explicitement la question G3).
Aucune décision antérieure à cette session n'a été retrouvée dans le code ou l'historique pour
G3 — la décision documentée ci-dessous a été prise et actée dans cette même session.

---

## G3 — Configuration Serilog partagée entre hôtes

**Statut : ✅ terminé (2026-07-19)**

### Décision actée
Pas de persistance de log dédiée type `ExtractionHistory` pour le pipeline OXO. Le pipeline
reste, comme depuis le Lot G1, sans dépendance au package Serilog concret dans
`ExcelETL.Application`/`ExcelETL.Infrastructure` — seule l'abstraction `ILogger<T>` y est
visible. Le problème réel n'était pas l'absence de logs (G1/G2 l'ont réglé) mais la
**duplication** de la configuration Serilog (sinks `Console` + `MSSqlServer`/`SystemLogs`) entre
`ExcelETL.WebAPI/Program.cs` et `ExcelETL.BlazorAdmin/Program.cs` : rien ne garantissait qu'un
futur hôte résolvant un service du pipeline OXO (ex. une future exposition Web API M2M)
reproduirait la bonne configuration.

### Réalisé
- **Nouveau projet `src/ExcelETL.Hosting/`** — référencé uniquement par les deux hôtes
  (`ExcelETL.WebAPI`, `ExcelETL.BlazorAdmin`), jamais par `Application`/`Infrastructure`/`Domain`
  ([ExcelETL.Hosting.csproj](../src/ExcelETL.Hosting/ExcelETL.Hosting.csproj)). Contient
  `SerilogHostLoggingExtensions.AddOxoHostLogging(this IHostBuilder, applicationName,
  connectionString)` ([SerilogHostLoggingExtensions.cs](../src/ExcelETL.Hosting/SerilogHostLoggingExtensions.cs)),
  qui remplace les deux blocs `UseSerilog(...)` précédemment tapés indépendamment dans chaque
  `Program.cs`.
- **Appels de site** :
  [`ExcelETL.WebAPI/Program.cs`](../src/ExcelETL.WebAPI/Program.cs) —
  `builder.Host.AddOxoHostLogging("ExcelETL.WebAPI", connectionString);` ;
  [`ExcelETL.BlazorAdmin/Program.cs`](../src/ExcelETL.BlazorAdmin/Program.cs) —
  `builder.Host.AddOxoHostLogging("ExcelETL.BlazorAdmin", connectionString);`. Seul
  l'argument `applicationName`/`connectionString` diffère entre les deux, plus aucune logique
  Serilog n'est retapée par hôte.
- Les deux `.csproj` hôtes ont perdu leur `PackageReference` directe à `Serilog.AspNetCore`/
  `Serilog.Sinks.MSSqlServer` (désormais tirée transitivement via la nouvelle
  `ProjectReference` vers `ExcelETL.Hosting`).
- **Test de non-duplication** :
  [`tests/ExcelETL.Hosting.Tests/SerilogHostLoggingExtensionsTests.cs`](../tests/ExcelETL.Hosting.Tests/SerilogHostLoggingExtensionsTests.cs) —
  `[Theory]` sur les deux noms d'hôte (`"ExcelETL.WebAPI"`/`"ExcelETL.BlazorAdmin"`) contre la
  méthode `Configure(...)` publique (testable sans `IHostBuilder` réel), avec
  `Serilog:EnableMsSqlServerSink=false` pour ne jamais ouvrir de vraie connexion SQL Server.
  Vérifie que la propriété `Application` correspond bien au nom passé, que
  `MinimumLevel.Override("Microsoft.AspNetCore", Warning)` filtre/garde les événements de façon
  identique pour les deux hôtes, et qu'aucune exception n'est levée même avec une chaîne de
  connexion injoignable. 6/6 verts.
- **`CLAUDE.md`** mis à jour (sections "Projects", "Web API surface", et un nouveau paragraphe
  "Lot G3" dans la description du pipeline d'extraction) avec le texte de décision demandé :
  *"Le pipeline OXO n'a pas de persistance de log dédiée ; il s'appuie sur `ILogger<T>` + la
  configuration Serilog de l'hôte appelant. Tout hôte qui résout un service du pipeline OXO doit
  appeler `AddOxoHostLogging(...)` pour garantir l'écriture dans `SystemLogs`."*

### Non fait, délibérément
- Aucune entité Domain ni migration EF de type `ExtractionHistory` pour le pipeline OXO —
  explicitement écarté par la décision ci-dessus.
- `ExcelETL.Application`/`ExcelETL.Infrastructure` ne référencent toujours aucun package Serilog
  concret — vérifié par lecture des deux `.csproj`, inchangés par ce ticket.

### Preuve d'exécution des tests (2026-07-19)
Voir le tableau récapitulatif en bas de ce document — la suite complète (613 tests, tous
projets) est verte, y compris `ImportPipelineOrchestratorLoggingIntegrationTests` (Lot G1/G2),
sans aucune modification de ces tests.

---

## Autres écarts hérités de l'audit du 17/07 (non traités par ce ticket, laissés en l'état)

Ces items viennent de `docs/audit-coherence-globale-2026-07-17.md` (section "Écarts à corriger")
et sont listés ici pour mémoire/traçabilité — **hors périmètre de la demande G3**, non touchés
dans cette session :

- ⬜ `CLAUDE.md` affichait `MaxRequestBodySize`=100 MB vs 10 MB réel (cosmétique doc, non
  vérifié à nouveau dans cette session).
- ⬜ `docs/tickets-tdd-blazor-profil-import-2026-07-17.md`, référencé par `CLAUDE.md`, n'existe
  toujours pas dans le dépôt.
- ⬜ `UnconditionalColonneNames` toujours absent de `modele-domaine-import-profile.md` /
  `tickets-tdd-extraction.md` (cosmétique, sans impact fonctionnel).
- ⬜ Renommage `PlatinesExtractionService`→`UnconditionalIsolementSheetExtractionService` non
  répercuté dans `tickets-tdd-extraction.md` (cosmétique).

---

## Résultat de l'exécution des tests (session G3, 2026-07-19)

```
dotnet test tests/ExcelETL.Hosting.Tests/ExcelETL.Hosting.Tests.csproj
→ Réussi : 6/6, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.WebAPI.Tests/ExcelETL.WebAPI.Tests.csproj
→ Réussi : 14/14, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.BlazorAdmin.Tests/ExcelETL.BlazorAdmin.Tests.csproj
→ Réussi : 112/112, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.Infrastructure.Tests/ExcelETL.Infrastructure.Tests.csproj
→ Réussi : 135/135, 0 échec, 0 ignoré (inclut ImportPipelineOrchestratorLoggingIntegrationTests, Lot G1/G2, inchangés)

dotnet test tests/ExcelETL.Application.Tests/ExcelETL.Application.Tests.csproj
→ Réussi : 91/91, 0 échec, 0 ignoré

dotnet test tests/ExcelETL.Domain.Tests/ExcelETL.Domain.Tests.csproj
→ Réussi : 255/255, 0 échec, 0 ignoré
```

Total : **613/613 tests verts**, aucune régression détectée sur l'ensemble du dépôt (hors
`legacy/`, non concerné par ce ticket).
