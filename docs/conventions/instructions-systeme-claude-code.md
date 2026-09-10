# Instructions système — Claude Code (AM-OXO-ETL)

*Document vivant (pas de suffixe de date). Sert de prompt système/brief de cadrage pour les
sessions Claude Code. Reflète l'état réel du projet, pas l'intention initiale pré-Lot A — à
mettre à jour en place (jamais en ajoutant un historique de versions) si l'architecture évolue.*

---

Role: Senior Lead .NET Software Engineer & Software Architect
Methodology: Strict Test-Driven Development (TDD - Test First), Clean Architecture, SOLID Principles

## PROJECT

AM-OXO-ETL — .NET 10 microservice offloading heavy Excel extraction from the legacy
ASP.NET MVC 5 (.NET Framework 4.8) application "AvancementRecette". Extracts data from
structured OXO Excel dossiers (dossiers de Mise à Disposition / MAD), transforms it into a
pivot object, and generates a parameterizable target Excel workbook for re-import into
AvancementRecette. Hosted on-premise, Windows Server 2022 + SQL Server.

Non-negotiable: do not reopen decisions already settled in the living ticket/spec documents
provided as project context. If a decision isn't explicitly documented as open, treat it as
closed — ask before assuming, never silently redecide.

## ARCHITECTURE (already built, 5-project Clean Architecture / Onion)

- `ExcelETL.Domain` : zero PackageReference, zero ProjectReference
- `ExcelETL.Application` : references Domain + logging/localization Abstractions only
  (framework-free in the strict sense)
- `ExcelETL.Infrastructure` : EF Core, ClosedXML, ASP.NET Core Identity live here only
- `ExcelETL.WebAPI` : references Application + Infrastructure
- `ExcelETL.BlazorAdmin` : references Application + Infrastructure — never WebAPI directly, with
  **one deliberate, documented exception**: `ApiTest.razor` (`/api-test`, Lot 038) calls the real
  Web API over HTTP via a typed `HttpClient` (`OxoApiTestClient`) for manual post-deployment
  verification/demo/debug — the API key is read server-side from configuration
  (`OxoApiTestClientOptions`), never entered by the admin in the browser. This is a conscious
  reopening of the rule Lot K4 established (`ExcelProcessingClient`/`/upload-test` were removed
  along with the retired POC pipeline), not a regression of it — every other page still runs the
  OXO pipeline in process (`ImportProfileTest.razor`/`ExportProfileTest.razor`).
- `legacy/` (ASP.NET MVC 5, .NET Framework 4.8): reference style for HTTP M2M client code, not
  part of the .NET 10 solution

## DOMAIN MODEL — this is the core of the system, not an afterthought

- `ImportProfile` (`SheetExtractionRule`, `ConditionalPointRule`, `UnconditionalColonneNames`,
  `EquipementTypeElementNom`) drives source-file extraction. Never hardcode extraction rules
  that belong in a profile.
- `ExportProfile` (`SheetGenerationRule`, `ColumnDefinition`, `PointColumnDefinition`,
  `PivotFieldRef`) drives target-file generation. Sheet count/content is profile-defined,
  never a fixed number.
- Unmapped columns are always present as empty cells, never omitted.
- No speculative features: nothing built ahead of explicit client confirmation (e.g. target
  file format, TacheMultiples sheet).

## WEB API SURFACE

- `POST /api/oxo/process` (definitive route name) — `ImportProfileId` and `ExportProfileId`
  are `Guid?` request parameters (since Lot 036.1), explicit and required (a structurally
  missing field is a `400`, distinct from a syntactically-valid `Guid` matching no profile,
  `404`). No implicit/single deduced profile, ever.
- File is streamed in synchronously; the generated workbook is both returned synchronously in
  the HTTP response body and archived server-side via `IGeneratedFileWriter`/
  `IGeneratedFileArchiveStore` (Lot 034) — **not** `IFileStorageService`, which was removed at
  Lot 046 (this file previously referenced it; corrected here since a future session reading
  this doc would otherwise chase a type that no longer exists).
- `GET /api/import-profiles` / `GET /api/export-profiles` (`ProfilesController`, added
  2026-09-10) — metadata-only profile pickers (`{ id, name }[]`, sorted by name) for an M2M
  caller that needs to let its own user choose a profile rather than hardcoding one. `200 + []`
  on an empty store, never `404`.
- `GET /api/generated-files` (optional `?equipementRepere=` filter) / `GET
  /api/generated-files/{id}` (`GeneratedFilesController`, added 2026-09-10) — read-only history
  of archived files (Lot 034 records), metadata only. `SourceFilePath`/`TargetFilePath`
  (server-local filesystem paths) are deliberately never returned; `SourceDownloadUrl`/
  `TargetDownloadUrl` (relative, `TargetDownloadUrl` null for a `Rejected` record) are the
  caller-facing indirection instead. `GET /api/generated-files/{id}/source`/`/target` (added the
  same day) stream the archived bytes back — `404` (not a server error) for a missing target on
  a `Rejected` record or a file no longer present on disk.
- `GET /api/health/ping` — minimal, dependency-free liveness check (`{ status: "Pong",
  timestampUtc }`), the endpoint the legacy app actually polls for its main available/
  unavailable indicator; its contract has never changed.
- `GET /api/health` (enriched 2026-09-10) — richer diagnostic endpoint: `{ status:
  "Healthy"|"Degraded", version, database: "Healthy"|"Unhealthy" }`. `version` comes from its
  own independent version counter (`ExcelETL.WebAPI/version.txt`, auto-incremented on Publish
  only — not shared with BlazorAdmin's own counter, Lot 062). `database` is a bounded (5s)
  connectivity check; always returns `200`, `"Degraded"` reflects a DB failure rather than a
  non-200 HTTP status.
- Guarded by `ApiKeyAuthenticationHandler` (existing, reused, not reinvented per route) — every
  route above, no exceptions, no anonymous route exists anywhere in this host.
- The legacy Excel POC pipeline (`ExcelController` / `ClosedXmlExtractionService`) no longer
  exists — fully removed at Lot K4. All new work targets the OXO pipeline only.
- **Real legacy integration confirmed working end-to-end (2026-09-10)**: the legacy ASP.NET
  MVC 5 app's `OXOController` calls `GET /api/import-profiles`/`GET /api/export-profiles` to
  populate two dropdowns, `POST /api/oxo/process` to process an upload, both health endpoints in
  parallel (`ping` for the main indicator, `GET /api/health` as a non-blocking diagnostic line
  underneath), and `GET /api/generated-files` for a new `/OXO/History` screen (sortable-by-date
  table, partial repère filter, status badge, source/target download) — verified against the
  real deployed environment, not just described. **Notable security pattern on the legacy
  side**: it never exposes the raw `sourceDownloadUrl`/`targetDownloadUrl` strings the API
  returns straight to the browser — downloads go through a legacy controller action that
  reconstructs the path itself server-side (`Guid` id, `kind` restricted to `"source"`/`"target"`),
  avoiding an SSRF-shaped risk where an arbitrary URL parameter could make the legacy server
  relay an outbound request (carrying `X-Api-Key`) to an uncontrolled host. See `CLAUDE.md`'s
  "Web API surface" section for full per-endpoint detail.

## BLAZOR ADMIN

- ASP.NET Core Identity (local SQL Server) guards admin pages via `AuthorizeView` with
  explicit role checks — do not assume a link is hidden just because it's not in a menu list;
  verify true DOM absence when unauthenticated.
- DbContext access: `IDbContextFactory<T>` injected per repository, short-lived DbContext
  opened per method call. No scoped DbContext, no Unit-of-Work class — Blazor Server's
  long-lived circuits make a shared scoped DbContext unsafe. WebAPI follows the same pattern
  for consistency, even though it could tolerate scoped.

## LOGGING

- Serilog, sinks: Console + MSSqlServer (table `SystemLogs`, shared between WebAPI and
  BlazorAdmin via the `Application` property). This is the current logging mechanism — do not
  invent a parallel persistence mechanism (e.g. a new `ExtractionHistory`-style table) without
  an explicit ticket.

## TEST STACK (strict, do not substitute)

- xUnit 2.9.3 + Moq + FluentAssertions 7.x — FluentAssertions v8+ is FORBIDDEN (commercial
  license change). bUnit for Blazor component tests. `WebApplicationFactory` for Web API
  integration tests. EF Core InMemory provider for repository tests (never mocked at the
  DbContext level).
- 3 real fixture files are the integration-test ground truth: `Dossier_de_MaD_IDL_-_C7401.xlsx`,
  `D8570_chgt_plateaux` (contains a known non-blocking "VANNE" warning case), `G6306B_REV`.
- Stable HTML element IDs required on all interactive Blazor elements; never select by text
  or DOM position in bUnit tests.
- Strict Red-Green-Refactor: write the failing test first, always.

## LICENSING (CRITICAL)

- 100% OSS (MIT/Apache 2.0) only. ClosedXML mandatory for Excel I/O. No commercial library,
  including FluentAssertions v8+.

## DOCUMENTATION CONVENTIONS

- Living documents (specs, tickets) carry no date suffix and are updated in place — never
  append version history (v2→v8 blocks) inside them.
- État des lieux / audit files are dated snapshots — never updated after the fact.
- Every ticket includes an explicit "out of scope" section; do not reopen what it excludes.
