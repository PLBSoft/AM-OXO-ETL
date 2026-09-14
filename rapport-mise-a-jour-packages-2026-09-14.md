# Rapport de mise à jour des packages NuGet — 2026-09-14

Instantané daté, non destiné à être modifié après coup (voir `docs/convention-nommage-documents.md`).

## Base de référence

Suite complète (`dotnet test ExcelETL.slnx`, hors `ExcelETL.Hosting.Tests` qui est déjà
inclus dans la solution — contrairement à ce que suggérait un ancien constat de
`CLAUDE.md`, le "trou de solution" n'existe plus) exécutée avant toute modification :
**1958/1959 tests verts**. Le seul échec est
`ExcelETL.BlazorAdmin.Tests.Pages.Admin.ExportProfileTestTests.ClickingGenerate_WithoutExportProfileSelected_ShowsError_WithRoleAlert`,
un défaut préexistant, déjà signalé et documenté comme non lié (`task_e009d615`,
confirmé sans rapport avec les packages sur des dizaines de lots antérieurs — voir
`CLAUDE.md`). Traité comme base de référence acceptée : vérifié après chaque étape
que ce test restait le **seul** échec, sans jamais tenter de le corriger (hors
périmètre de cette mission).

État final après toutes les mises à jour : **identique**, 1958/1959, même seul échec,
aucune régression introduite.

## Paquets mis à jour

### Famille Microsoft.Extensions.* / Microsoft.EntityFrameworkCore.* / Microsoft.AspNetCore.* (montée patch, alignée sur le runtime .NET 10.0.12 déjà installé localement)

| Projet | Paquet | Avant | Après |
|---|---|---|---|
| ExcelETL.Application | Microsoft.Extensions.Localization.Abstractions | 10.0.0 | 10.0.12 |
| ExcelETL.Application | Microsoft.Extensions.Logging.Abstractions | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.EntityFrameworkCore.Design | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.EntityFrameworkCore.SqlServer | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.Extensions.Configuration.Abstractions | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.Extensions.Configuration.Binder | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.Extensions.Localization.Abstractions | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure | Microsoft.Extensions.Logging.Abstractions | 10.0.0 | 10.0.12 |
| ExcelETL.WebAPI | Microsoft.AspNetCore.OpenApi | 10.0.9 | 10.0.12 |
| ExcelETL.WebAPI | Microsoft.EntityFrameworkCore.Design | 10.0.0 | 10.0.12 |
| ExcelETL.Hosting | Microsoft.EntityFrameworkCore.Relational | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure.Tests | Microsoft.Extensions.Configuration | 10.0.0 | 10.0.12 |
| ExcelETL.Hosting.Tests | Microsoft.Extensions.Configuration | 10.0.0 | 10.0.12 |
| ExcelETL.BlazorAdmin.Tests | Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | 10.0.12 |
| ExcelETL.BlazorAdmin.Tests | Microsoft.AspNetCore.Mvc.Testing | 10.0.0 | 10.0.12 |
| ExcelETL.Infrastructure.Tests | Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | 10.0.12 |
| ExcelETL.WebAPI.Tests | Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | 10.0.12 |
| ExcelETL.WebAPI.Tests | Microsoft.AspNetCore.Mvc.Testing | 10.0.0 | 10.0.12 |

Validation : `dotnet build` + suite complète verte (même seul échec préexistant),
après chaque sous-étape.

### Microsoft.OpenApi (montée patch forcée, pas la montée majeure demandée — voir "laissés en l'état")

| Projet | Paquet | Avant | Après |
|---|---|---|---|
| ExcelETL.WebAPI | Microsoft.OpenApi | 2.10.0 | **2.12.0** |

`Microsoft.AspNetCore.OpenApi` 10.0.12 exige `Microsoft.OpenApi >= 2.12.0 && < 3.0.0`
(erreur NU1605 bloquante sans cette montée) — 2.12.0 est donc la version minimale
compatible, mise à jour dans le même pas que la famille ci-dessus. La montée vers la
dernière version disponible (3.10.2) a été tentée séparément et rejetée — voir
ci-dessous.

### ClosedXML (montée patch)

| Projet | Paquet | Avant | Après |
|---|---|---|---|
| ExcelETL.Infrastructure | ClosedXML | 0.105.0 | 0.105.1 |

Validé par la suite `ExcelETL.Infrastructure.Tests` (256/256), qui exerce
massivement la lecture/écriture de vrais fichiers `.xlsx` — bon signal de
compatibilité pour ce paquet.

### bunit (montée mineure)

| Projet | Paquet | Avant | Après |
|---|---|---|---|
| ExcelETL.BlazorAdmin.Tests | bunit | 2.10.3 | 2.11.3 |

### Outillage de test (montées majeures, faible risque — n'affectent que l'exécution des tests, jamais le code métier)

Appliqué identiquement à tous les projets `tests/*` de la solution .NET 10
(`ExcelETL.Domain.Tests`, `ExcelETL.Application.Tests`, `ExcelETL.Infrastructure.Tests`,
`ExcelETL.WebAPI.Tests`, `ExcelETL.BlazorAdmin.Tests`, `ExcelETL.Hosting.Tests`) :

| Paquet | Avant | Après |
|---|---|---|
| coverlet.collector | 6.0.4 | 10.0.1 |
| Microsoft.NET.Test.Sdk | 17.14.1 | 18.10.0 |
| xunit.runner.visualstudio | 3.1.4 | 4.0.0 |

Chaque montée a été suivie d'un `dotnet build` + suite complète verte avant de
passer à la suivante (pas de correctif de code nécessaire pour aucune des trois).

### FluentAssertions (montée mineure/patch, strictement à l'intérieur de la ligne 7.x autorisée)

Appliqué identiquement à tous les projets `tests/*` de la solution .NET 10 :

| Paquet | Avant | Après |
|---|---|---|
| FluentAssertions | 7.0.0 | 7.2.2 |

Vérifié explicitement avant la montée : `7.2.2` reste sous licence `Apache-2.0`
(confirmé dans le `.nuspec` téléchargé), la bascule vers une licence commerciale
n'intervenant qu'à partir de la v8.0.0 (déjà documenté dans `CLAUDE.md`). 7.2.2 est
la dernière version de la ligne 7.x disponible sur NuGet à ce jour — la contrainte
"ne jamais monter au-delà de la dernière version 7.x" est donc respectée à la
lettre, sans pour autant laisser le paquet sur un patch obsolète de cette même ligne.

## Paquets volontairement laissés en l'état

| Projet(s) | Paquet | Version actuelle | Dernière version | Raison |
|---|---|---|---|---|
| Tous les projets tests/ (.NET 10) | FluentAssertions | 7.2.2 | 8.11.0 | **Licence commerciale interdite** à partir de la v8 (contrainte explicite de la mission). Confirmé : la v8+ passe sous licence propriétaire (Xceed), la ligne 7.x reste Apache-2.0. Mis à jour au maximum de la ligne 7.x autorisée (voir ci-dessus). |
| ExcelETL.WebAPI | Microsoft.OpenApi | 2.12.0 | 3.10.2 | **Incompatibilité bloquante confirmée par build**, pas seulement un changelog lu à distance : `Microsoft.AspNetCore.OpenApi` 10.0.12 épingle `Microsoft.OpenApi (>= 2.12.0 && < 3.0.0)`. Forcer 3.10.2 fait apparaître une erreur de compilation réelle dans le générateur de source `Microsoft.AspNetCore.OpenApi.SourceGenerators` (`CS0200 : Impossible d'assigner la propriété 'IOpenApiMediaType.Example' -- il est en lecture seule`), signe d'une rupture d'API entre OpenApi.NET v2 et v3. Rollback immédiat vers 2.12.0 (la version maximale compatible avec le paquet AspNetCore lié), suite verte confirmée après rollback. |
| legacy/ExcelProcessingClientService, legacy/ExcelProcessingClientService.Tests, legacy/NewApiPingService, legacy/NewApiPingService.Tests | coverlet.collector, FluentAssertions, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio | (inchangés) | — | **Hors périmètre explicite de la mission** (".NET Framework 4.8, hors périmètre"). Aucune tentative de mise à jour. |
| ExcelETL.Domain, ExcelETL.BlazorAdmin | — | — | — | Aucun paquet obsolète signalé pour ces deux projets — rien à faire. |

## Aucune modification de comportement métier

Seuls les fichiers `.csproj` (versions de `PackageReference`) ont été modifiés.
Aucun fichier de code source (`.cs`, `.razor`) n'a été touché — aucune montée
majeure absorbée n'a nécessité d'adaptation d'API (les deux tentées et
incompatibles — Microsoft.OpenApi 3.x — ont été annulées plutôt que corrigées).

## Fichiers modifiés

```
src/ExcelETL.Application/ExcelETL.Application.csproj
src/ExcelETL.Hosting/ExcelETL.Hosting.csproj
src/ExcelETL.Infrastructure/ExcelETL.Infrastructure.csproj
src/ExcelETL.WebAPI/ExcelETL.WebAPI.csproj
tests/ExcelETL.Application.Tests/ExcelETL.Application.Tests.csproj
tests/ExcelETL.BlazorAdmin.Tests/ExcelETL.BlazorAdmin.Tests.csproj
tests/ExcelETL.Domain.Tests/ExcelETL.Domain.Tests.csproj
tests/ExcelETL.Hosting.Tests/ExcelETL.Hosting.Tests.csproj
tests/ExcelETL.Infrastructure.Tests/ExcelETL.Infrastructure.Tests.csproj
tests/ExcelETL.WebAPI.Tests/ExcelETL.WebAPI.Tests.csproj
```

Aucun commit n'a été créé par cette session (changements laissés dans l'arbre de
travail pour revue) — voir la consigne du projet sur les sessions Claude Code
concurrentes avant de committer/pousser.
