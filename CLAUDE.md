# Project Instructions: AM-OXO-ETL Microservice

## Role
**Senior Lead .NET Software Engineer & Software Architect**

## Methodology
- Strict Test-Driven Development (TDD - Test First)
- Clean Architecture
- SOLID Principles

---

## GLOBAL CONTEXT & SYSTEM OVERVIEW

You are tasked with building a modern, isolated, and highly maintainable micro-service designed to offload a heavy Excel data-extraction workload from a legacy application. The system will be hosted on a dedicated on-premise Windows Server 2022.

### Key Applications:
- **Legacy Application:** ASP.NET MVC 5, C#, .NET Framework 4.8. It captures raw uploaded Excel files and ingests the processed files. No business logic modifications are allowed here.
- **New Application:** A greenfield independent system built using the latest modern .NET LTS release (.NET 10). It exposes a secure ASP.NET Core Web API for synchronous Machine-to-Machine (M2M) communication and a Blazor Web App (Server Interactivity) for administration.

---

## FUNCTIONAL & DATA FLOW SPECIFICATIONS

### Ingestion & Transfer
Legacy app securely streams the `.xlsx` file synchronously over HTTP to the new .NET Web API.

### Processing
The backend extracts form-style data from heavily merged cells based on dynamic coordinates.

### Persistence
The generated structured workbook is archived on the local Windows Server filesystem. Execution is logged in the database.

### Egress
The generated workbook is returned synchronously in the HTTP response body.

### Constraints
- Input files contain no macros or encryption
- Output must be exactly 4-5 distinct `.xlsx` sheets
- CSV and ZIP formats are explicitly banned

---

## TECHNICAL STACK & CONSTRAINTS

### Core Frameworks
- ASP.NET Core Web API
- Blazor Web App (Server Interactivity model)

### Architecture
Clean Architecture (Onion Pattern). **Both the Web API and the Blazor admin app access data exclusively through the Application-layer services/repositories** — never by talking to each other over HTTP, and never by injecting `DbContext` (or any Infrastructure type) directly into a controller or a Razor component. All EF Core access lives behind repository interfaces defined in `Application` and implemented in `Infrastructure`. This is a strict rule, not a suggestion — short-circuiting the service/repository layer for convenience is exactly the kind of violation to flag rather than implement (see "Architectural Oversight" below).

### Database
- Microsoft SQL Server (Local on-premise)
- Entity Framework Core (Code-First Fluent API)

### Licensing Constraint (CRITICAL)
**100% strict adherence to free, open-source software (OSS) licenses (MIT, Apache 2.0). Commercial libraries are strictly forbidden.**
- **ClosedXML** is the mandatory library for all Excel manipulation

### Security
- Web API guarded by robust API Key validation
- Blazor admin guarded by ASP.NET Core Identity (Local SQL Server)

### Performance & HTTP
Because processing is synchronous and can take several seconds, explicit configuration of HTTP Timeouts is mandatory:
- Kestrel configuration
- Web API timeouts
- Legacy HttpClient timeouts

---

## QUALITY ASSURANCE & TDD STRATEGY

Apply a strict Test-First (Red-Green-Refactor) lifecycle:
1. Write failing unit/integration tests before implementation
2. Use xUnit/NUnit, Moq, FluentAssertions
3. Explicitly test reading values from merged range coordinates in Excel
4. Ensure all business logic is test-covered before deployment

### Repository test conventions
- Test projects live under `tests/`, one per `src/` project, mirroring its internal folder structure 1:1 (a repository in `src/ExcelETL.Infrastructure/Identity/` is tested in `tests/ExcelETL.Infrastructure.Tests/Identity/`).
- **FluentAssertions is pinned to 7.0.0** — v8+ is commercially licensed, which the OSS constraint above forbids. Always verify a NuGet package's license before adding or upgrading it.
- Repositories backed by an EF Core `DbContext` are tested against the **real EF Core InMemory provider**, not mocked. Each test class gets its own tiny `internal sealed class Test{ContextName}DbContextFactory(string databaseName) : IDbContextFactory<TContext>` colocated in the same test folder, constructed with a GUID-suffixed database name per test class for isolation — there is no shared/generic factory helper.
- Repositories that depend on ASP.NET Core Identity's `UserManager<T>`/`RoleManager<T>` (rather than an injected `IDbContextFactory<T>` directly) are tested with **Moq** instead, since those managers aren't meaningfully exercised against the InMemory provider alone.
- Assertions use FluentAssertions exclusively (`.Should().Be(...)`, `.Should().BeEquivalentTo(...)`, `.Should().ThrowAsync<T>()`, etc.) — never xUnit's `Assert.*`.

---

## I18N (ENGLISH/FRENCH) STRATEGY

The solution is being incrementally refactored to support en-US (default) and fr-FR, one Clean Architecture layer at a time (Domain → Application → Infrastructure → WebAPI → BlazorAdmin). Resource format is `.resx`.

### Scope: user-facing business errors only
Only translate messages a **user or admin actually sees** (validation failures, business-rule violations, HTTP error payloads, UI text). Do **not** touch developer-facing invariant violations — `ArgumentNullException.ThrowIfNull`, assertion/guard-clause messages, log-only diagnostic text aimed at engineers. Those stay in plain English BCL exceptions; they are out of i18n scope entirely.

### Domain layer stays framework-free
`ExcelETL.Domain` must never reference `Microsoft.Extensions.Localization` or any other localization package — that would put a framework dependency in the innermost layer, violating Clean Architecture. Instead:
- Domain throws `DomainValidationException` / `DomainArgumentOutOfRangeException` / `DomainRuleViolationException` (in `ExcelETL.Domain/Exceptions`), each carrying a `DomainErrorCode` enum value plus the raw `Args` that were interpolated into the (English) `Message`.
- The `DomainErrorCode` member name doubles as the resource key.

### Application layer owns the resource tables
`ExcelETL.Application/Resources/` holds the `.resx` files, resolved via `IStringLocalizer<T>` marker classes (no generated designer — resolution is by naming convention, so no Visual Studio tooling is required):
- `DomainErrorMessages.resx` / `.fr.resx` — the single translation table for every `DomainErrorCode`, shared by both WebAPI and BlazorAdmin so the mapping is never duplicated between the two hosts.
- `ApplicationMessages.resx` / `.fr.resx` — Application-owned messages (service results, Data Annotations validation text via `ErrorMessageResourceType`/`ErrorMessageResourceName`).

`Microsoft.Extensions.Localization.Abstractions` (interfaces only, no ASP.NET Core coupling) is an accepted Application-layer dependency, consistent with the existing `Microsoft.Extensions.Logging.Abstractions` reference.

### Host wiring
- **WebAPI**: `RequestLocalizationOptions` negotiates culture from `Accept-Language` only (it's M2M — no cookie/query-string relevance). The `GlobalExceptionHandler` resolves a thrown `DomainErrorCode` + `Args` against `DomainErrorMessages` to build the localized HTTP error payload.
- **BlazorAdmin**: default provider order (query string, then culture cookie, then `Accept-Language`) so an admin's language choice persists. A circuit is long-lived, so switching language requires a real navigation — done via the `/culture/set` minimal API endpoint (`CultureEndpointRouteBuilderExtensions`), not a component re-render.

### Workflow
Work proceeds milestone by milestone (one Clean Architecture layer, or one batch of Razor components, at a time). At the end of each milestone: stop, summarize, and wait for validation plus the next milestone's files before continuing. Each milestone's deliverable includes an EN/FR resource key table.

---

## ARCHITECTURAL OVERSIGHT (CRITICAL)

The user is not a professional software architect and expects to make mistakes in architectural or strategic direction from time to time. **Proactively flag it** — before implementing — whenever a requested change, an instruction in this file, or an existing pattern in the codebase:
- Contradicts Clean Architecture, SOLID, or another established best practice
- Contradicts the architecture already in place elsewhere in this solution (e.g. one app going through repositories while another talks to EF Core directly)
- Introduces inconsistency between the Web API and the Blazor admin app's data-access patterns
- Trades away testability, layering, or maintainability for short-term convenience without the user asking for that trade-off explicitly

Say so directly, explain the concrete downside, and propose the alternative — then wait for a decision rather than silently complying or silently "fixing" it. Do not assume a past instruction (including one written in this file) is correct just because it's already written down; instructions can contain mistakes too.

---

## CURRENT SOLUTION STATE (living reference — read this before exploring the codebase; update it at the end of each milestone)

### Projects
- `src/`: `ExcelETL.Domain`, `ExcelETL.Application`, `ExcelETL.Infrastructure`, `ExcelETL.WebAPI`, `ExcelETL.BlazorAdmin`
- `tests/`: mirrors each `src/` project 1:1 (`ExcelETL.Domain.Tests`, etc.)
- `legacy/`: `NewApiPingService`, `ExcelProcessingClientService` (+ `.Tests`) — the .NET Framework 4.8 legacy app's own HTTP client for calling the new API; the style precedent for any new M2M `HttpClient` code (multipart build, timeout translation, header setup).

### Extraction pipeline (new feature area — Lot A, Domain only so far)
- Source docs: `docs/spec-extraction-fichier-source-oxo-2026-07-16_4.md` (feuille par feuille), `docs/modele-domaine-import-profile-2026-07-16.md` (catalogue de primitives + modèle pivot), `docs/tickets-tdd-extraction-2026-07-16.md` (découpage TDD Lot A→E). Test fixtures (real client `.xlsx` files): `tests/Fixtures/`.
- This is a **second, separate pipeline** from the existing `ExtractionConfig`/`CellMapping`/`SheetConfig` one (Web API surface above) — different Domain folder (`Extraction/`), not yet wired to any host. The two are not meant to merge; `ExtractionConfig` stays as-is.
- `src/ExcelETL.Domain/Extraction/Primitives/`: the 5-primitive catalogue read-time model — `DirectCell`, `RepeatingBlockLocator`/`BlockFieldDefinition` (the generic repeating-block reader, covers all 6 source sheets incl. PROCEDURE's step=1 special case), the `TextTransform` hierarchy (`RawValue`/`SubstringAfter`/`Concat`+`ConcatPart`(`Literal`/`FieldRef`)), `ConditionalPointRule`/`ConditionOperator`. All `sealed record`s validating via `DomainValidationException`/`DomainArgumentOutOfRangeException`, same convention as the existing `Entities/`. `RepeatingBlockLocator` and `Concat` hold a list property (`Fields`/`Parts`) — both override `Equals`/`GetHashCode` with `SequenceEqual` because default record equality on `IReadOnlyList<T>` is reference equality, not structural; every other primitive is plain scalar fields so default record equality suffices.
- `src/ExcelETL.Domain/Extraction/Pivot/`: the extraction *output* model — `EquipementPivot`, `IsolementPivot` (its `Localisation` has an `init` accessor specifically so the future `loc1` broadcast, Lot D, can do `isolement with { Localisation = ... }` on an already-built instance without re-running the other fields' validation), `PointPivot`, `TacheMultiplePivot` (only `Action` is required non-empty — it's PROCEDURE's stop-condition field so always populated, including for `EstFactice` placeholder rows; `Acteur`/`Risques`/`TypeTacheMultipleCode` are deliberately left unvalidated since factice rows can leave them blank per spec §1.2), `ExtractionErrorCode` (intentionally only 3 members for now — `RequiredFieldMissing`/`UnparsableValue`/`UnrecognizedTypeElement`, more will be added as Lot B/C need them, not pre-guessed), `ExtractionError`, `ImportResult` (a plain `sealed class`, not a record — **a second deliberate exception to the project's "no generic Result pattern" rule**, alongside `IdentityOperationResult`; needed because per-block extraction errors accumulate while processing continues, which doesn't fit "throw and stop"). `ImportResult`'s constructor does *not* enforce "Equipement null ⇒ other collections empty" (the whole-file-rejection invariant from the model doc §3.1) — that's tested at the orchestrator level instead (Lot D, via mock verification that the other 5 sheet services are never called), not baked into the Domain constructor.
- `src/ExcelETL.Domain/Extraction/Profile/`: `ImportProfile` (aggregate root, extends `Entity`, has `Id`) and `SheetExtractionRule` (plain class, no `Id` — a configuration value owned by its `ImportProfile`, not an entity in its own right). `ImportProfile.ReperePrefix` defaults to `"MAD-OXO-"` (`ImportProfile.DefaultReperePrefix`) via a constructor overload, per the spec's "paramétrable, défaut MAD-OXO-". `SheetExtractionRule` validates its `SheetName` matches its `Locator.Sheet` (`DomainRuleViolationException`) — a consistency check beyond what the ticket explicitly asked for, added because the two are otherwise redundant data that could silently diverge. `SheetExtractionRule.PointRules` may legitimately be empty (means "always create the Point", not an error) — only `ImportProfile.SheetRules` itself is required non-empty (at least one rule).
- Persistence (EF Core `ImportProfile`) is deliberately deferred — Lot A-D are being validated against a hardcoded in-memory profile first, per the ticket doc's proposed sequencing. Don't add an `ExtractionProfileConfiguration`/repository until that's explicitly requested.
- **Lot B (Application-layer generic engine, done)** lives in `src/ExcelETL.Application/Extraction/Oxo/` — a **new subfolder**, deliberately not flat in the pre-existing `src/ExcelETL.Application/Extraction/` (that's the old `ExtractionConfig` pipeline; putting new OXO code there would collide both on disk and in namespace — see `docs/etat-des-lieux-technique.md`'s "deux dossiers `Extraction/`" note). `IWorkbookReader`: `ReadCellValue(sheet, range)` / `SheetExists(sheet)`. `TextTransformEvaluator`/`RepeatingBlockReader`/`ConditionalPointRuleEvaluator` are all stateless, tested against `Mock<IWorkbookReader>` or plain dictionaries — no real file I/O in their own tests (that's Lot E1 + the per-sheet integration tests below). `RepeatingBlockReader`'s range math (column split + row offset) is factored into a standalone static `BlockFieldRangeCalculator`, added while building Lot C1 so `ProcedureExtractionService` could reuse the same math without going through `RepeatingBlockReader`'s required-field policy (see Lot C1 note below) or duplicating it.
  - None of these three evaluators return an `ExtractionError` directly — they return a plain `(Value, ErrorMessage)`/`(ShouldCreatePoint, WarningMessage)` tuple (just a 2-tuple, not a new named Result type) because they have no `Sheet`/`BlockIdentifier` context to build one with. Wrapping a message into a real `ExtractionError` is the caller's job (Lot C, which does have that context) — don't push `ExtractionError` construction down into these evaluators later without a reason.
  - `RepeatingBlockReader` reads a block's `StopFieldName` cell *first*, before any other field, and bails immediately if it's blank — so a block that fails the stop check is never misreported as "partially empty"; it's simply never read beyond its stop field. A block whose stop field is populated but another field is blank produces one `ExtractionError` (`RequiredFieldMissing`) and is skipped, and reading continues on the next block.
  - `UnknownFieldReferenceException` (new `ApplicationErrorCode` member, follows the existing per-exception `IHasApplicationErrorCode` pattern — there is no shared `ApplicationValidationException` base class in this codebase, despite the ticket doc's conventions section implying one) is thrown — not returned as an `ExtractionError` — whenever a `Concat`'s `FieldRef` or a `ConditionalPointRule`'s `SourceFieldName` names a field that isn't in the already-extracted-fields dictionary. That's treated as a profile/configuration bug (the `SheetExtractionRule` doesn't line up with itself), not per-row file data noise.
  - `RepeatingBlockReadResult` (`Blocks`/`Errors`) is a plain DTO, matching the pre-existing `ExtractionResult`'s precedent — not a third exception to the "no generic Result pattern" rule (that count stays at 2: `IdentityOperationResult`, `ImportResult`).
- **Open question, still deferred to Lot C2+ — not needed for C1**: `SheetExtractionRule.PointRules` is a single flat `IReadOnlyList<ConditionalPointRule>` per sheet, but real sheets mix unconditional Points (e.g. ISOLEMENT's "PROLOCK VANNES") with conditional ones (ISOLEMENT's "ZÉRO ENERGIE..." if `TypeElement = ZERO ENERGIE`). The model doc itself flags this mapping as unresolved (§10: "à faire lors de la prochaine étape de modélisation"). B4's `ConditionalPointRuleEvaluator` only solves the mechanics (Equals/NotEquals, empty-list-always-creates, no-match-warns) for whatever rule subset it's handed — it does **not** decide how a sheet's unconditional Colonnes get represented or how Lot C groups `PointRules` by Colonne. PROCEDURE's 2 Points (below) sidestepped this entirely since they're unconditional and sheet-specific, not `PointRules`-driven — still need to make a call on this for ISOLEMENT (C2) onward.
- **Lot E1 (`ClosedXmlWorkbookReader`, done ahead of its ticket slot)** lives in `src/ExcelETL.Infrastructure/Excel/`, the first real `IWorkbookReader` implementation. Built alongside Lot C1 rather than deferred, because the Lot C ticket requires each per-sheet service to be tested against the 3 real fixtures, which needs a working reader to open them. Opens the `XLWorkbook` once per instance and owns its disposal (`IDisposable`, not part of `IWorkbookReader` itself — Lot B/C consumers never need to know). Reads a range's top-left cell via `GetString()`, returning `null` for blank (mirrors what the Mock-based tests already assume). Reuses the pre-existing `WorksheetNotFoundInWorkbookException` (from the old `ExtractionConfig` pipeline, `ExcelETL.Application.Extraction` namespace) for an unknown sheet, rather than declaring a near-duplicate. Its own tests use small in-memory workbooks built with ClosedXML directly (merged range, blank cell, missing sheet) — real-fixture coverage comes from the per-sheet integration tests instead (see below).
- **Lot C1 (`ProcedureExtractionService`, done)** lives in `src/ExcelETL.Application/Extraction/Oxo/Procedure/`. Reads PROCEDURE's header (`M2:O2` repère, `P2:Q2` numéro révision, `R2:T2` date révision — all 3 ranges hardcoded in the service, not part of `SheetExtractionRule`, since the spec only calls the repère *prefix* profile-configurable, not the header cell coordinates themselves) into `EquipementPivot`, plus its `TacheMultiple` repeating block (`Step=1`, stop field `Action`/`C:L`) into `TacheMultiplePivot`s, and creates 2 unconditional `PointPivot`s ("TRAVAUX COMPLET"/"TRAVAUX DETAIL") for the Equipement per spec §1.3 — no `ConditionalPointRuleEvaluator` involved, these aren't conditioned on anything.
  - **Deliberately does not use `IRepeatingBlockReader`** for the TacheMultiple block, despite `RepeatingBlockLocator`/`Step=1` otherwise fitting: every field in a TacheMultiple row *except* `Action` is optional at the pivot level, not just `Ordre` (the documented "ligne de mise en page" → `EstFactice` rule, spec §1.2) but `Acteur`/`Risques`/`TypeTacheMultipleCode`/`DateValidation` too (`TacheMultiplePivot` itself leaves them unvalidated — confirmed against the real fixtures, e.g. C7401 has section-header rows with blank `Ordre` scattered well past row 9, and populated `Acteur`("ADF")/`Risques` values are the exception, not the rule). `RepeatingBlockReader`'s shared policy (any non-stop field blank ⇒ `ExtractionError`, block dropped) would wrongly reject/drop valid rows, so `ProcedureExtractionService` walks the block itself, calling `BlockFieldRangeCalculator.BuildRange` directly for the range math only. Field names read from `sheetRule.Locator.Fields` by name (constants in `ProcedureFieldNames`) so the field list stays profile-configurable exactly like every other sheet's locator.
  - **Whole-file rejection** (model doc §3.1): if `M2:O2` is blank, doesn't start with the configured repère prefix, or `R2:T2` is blank/unparsable, `Extract` returns immediately with `Equipement = null` and exactly one blocking `ExtractionError` — the TacheMultiple block is never read in that case (verified by `Mock.Verify(..., Times.Never)` in the unit tests).
  - **Date handling**: `R2:T2`/`T:U` cells are parsed via `DateTime.TryParseExact` (`"dd/MM/yyyy HH:mm:ss"` / `"dd/MM/yyyy"`, invariant culture) into `DateOnly`, then reformatted as `dd/MM/yyyy` for `Designation` — deliberately **not** a raw pass-through of `IWorkbookReader.ReadCellValue`'s string, because probing the real C7401 fixture showed ClosedXML's `GetString()` on a date cell returns `"12/12/2025 00:00:00"` (a full datetime, locale/number-format dependent), not a clean `dd/mm/yyyy` string — the spec's format requirement is enforced in code, not assumed from the source file's display formatting.
  - **`EquipementPivot.TypeElementCode` is hardcoded to the literal `"MAD"`** (a `const` in the service). The spec (§0/§9) says this is `"MAD"` or `"REL"` depending on which dossier is being processed — a dossier-level fact, not a cell value — but no REL fixture exists yet to validate that path against, and no cell in PROCEDURE carries it. Revisit (likely via a profile- or call-level parameter) once a REL fixture is available; don't generalize speculatively before then.
  - Tested against `Mock<IWorkbookReader>` (`ProcedureExtractionServiceTests`, Application.Tests) and, per the Lot C ticket's requirement, against all 3 real fixtures via the real `ClosedXmlWorkbookReader` (`ProcedureExtractionServiceIntegrationTests`, **Infrastructure.Tests**, not Application.Tests — it needs both the Application service and the Infrastructure reader, and Infrastructure already references Application, so putting it there avoids adding a new Application.Tests → Infrastructure reference).
- Next up per the ticket doc: Lot C2 (ISOLEMENT — first sheet needing the `PointRules` conditional/unconditional grouping decision above), then C3-C6, then Lot D (orchestrator + integration tests against the real fixtures).

### Web API surface
- `POST /api/excel/process` — `ExcelController` (`src/ExcelETL.WebAPI/Controllers/ExcelController.cs`). `multipart/form-data`, `[FromForm] ProcessExcelFileRequest { Guid ExtractionConfigId; IFormFile File }`. Returns `FileStreamResult` (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`). Max 10 MB, 5-min server timeout policy (`UploadLimits.MaxExcelFileSizeBytes` / `ExcelProcessingTimeout` / `ExcelProcessingTimeoutPolicy`) — kept identical to BlazorAdmin's `/upload-test` client-side cap (Milestone 10) so that page exercises the server's real limit.
- API key auth: header `X-Api-Key` (`ApiKeyAuthenticationDefaults.HeaderName`), constant-time compare in `ApiKeyAuthenticationHandler`, value at appsettings key `ApiKeyAuthentication:ApiKey` (only set in `appsettings.Development.json` today — prod value expected via user secrets/env var). Applied as the fallback auth policy for the whole WebAPI app.
- Kestrel (WebAPI `Program.cs`): `MaxRequestBodySize`=100 MB, `KeepAliveTimeout`=5 min, `RequestHeadersTimeout`=2 min.

### BlazorAdmin state
- Auth pattern for admin pages: `@attribute [Authorize(Roles = IdentitySeeder.AdminRoleName)]` (see `Components/Pages/Admin/Users.razor`).
- Rendermode is set **once, globally**: `App.razor` → `<Routes @rendermode="InteractiveServer" />`, `Program.cs` → `.AddInteractiveServerRenderMode()`. Individual pages must not declare their own `@rendermode`.
- i18n: `src/ExcelETL.BlazorAdmin/Resources/BlazorAdminMessages.resx`/`.fr.resx` exists and is used by Dashboard/History/Logs/Mappings/Users/UploadTest via `IStringLocalizer<BlazorAdminMessages>`. Key convention: `{PageName}_{Element}` (e.g. `Users_PageTitle`, `History_Status`, `Upload_*`).
- As of Milestone 10: `ExcelProcessingClient` (`src/ExcelETL.BlazorAdmin/ExternalApi/`) is a typed `HttpClient` (`AddHttpClient<ExcelProcessingClient>` in `Program.cs`) that calls the WebAPI's `POST /api/excel/process` directly — a **deliberate, narrow exception** to the "never talk to the Web API over HTTP" rule (see `Architecture` section above), scoped to the `/upload-test` admin page only, which exists to exercise the real M2M contract. Configured via `WebApiClientOptions` bound from the `WebApiClient:BaseUrl` / `WebApiClient:ApiKey` config section (dev values only in `appsettings.Development.json`, matching WebAPI's own dev API key; no base `appsettings.json` entry, same fail-fast-in-prod pattern as WebAPI's `ApiKeyAuthentication`). Timeout is 6 minutes (`ExcelProcessingClient.DefaultTimeout`), matching the legacy client's precedent. The JS-interop/stream-download step is isolated behind `IExcelDownloadInterop` (`ExcelDownloadInterop` impl uses `wwwroot/js/fileDownload.js` + `DotNetStreamReference`) specifically so it can be swapped for a fake in bUnit tests — bUnit's fake `IJSRuntime` cannot marshal a real `DotNetStreamReference`.
- `wwwroot/js/fileDownload.js` is the first custom JS module in this project (previously only inline `IJSRuntime` calls to browser globals existed, e.g. clipboard write in `Logs.razor`).
- Nav-item icons (`NavMenu.razor`): every `<span class="bi bi-{name}-nav-menu" aria-hidden="true">` markup reference must have a matching CSS rule in `Components/Layout/NavMenu.razor.css` defining its `background-image` (inline Bootstrap Icons SVG data URI, `fill='white'`, matching the existing entries' format). The two files are edited as a pair — adding a `NavLink`/icon span in the `.razor` without its `.razor.css` counterpart (or vice versa) silently renders a blank icon with no build error or warning.
- As of Milestone 11 (self-service `/profile` page, `Components/Pages/Admin/Profile.razor`): `IUserRepository` (`Application/Identity/`) was extended beyond its original list-only `GetAllAsync` with `GetByIdAsync` (plain EF projection via `IDbContextFactory<ApplicationIdentityDbContext>`, returns nullable `UserProfile`), `UpdateProfileAsync`, and `ChangePasswordAsync` (both backed internally by `UserManager<ApplicationUser>`, added as a second constructor dependency on `UserRepository`). This resolved a pre-existing architectural inconsistency (`Users.razor` went through `IUserRepository` while `Login.razor`/`Register.razor` injected `UserManager<ApplicationUser>` straight from Infrastructure into components) — `Profile.razor` only ever injects `IUserRepository`, never `UserManager` directly. Identity failures (wrong current password, duplicate email, etc.) are surfaced as a new framework-free `IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors)` record — `UserRepository` maps `IdentityResult.Errors[].Description` (already localized via the existing `LocalizedIdentityErrorDescriber`) into it, so Application stays free of `Microsoft.AspNetCore.Identity` types. "User id not found" is treated as a developer invariant (`InvalidOperationException`, out of i18n scope), not a translatable `IdentityOperationResult` failure, since callers always pass the current authenticated user's own id.
- Post-password-change re-authentication: Interactive Server components cannot write the auth cookie mid-circuit (same constraint documented on `IdentityComponentsEndpointRouteBuilderExtensions` for Logout), so `Profile.razor` cannot silently refresh the sign-in after `ChangePasswordAsync` succeeds. Instead it swaps its password form for a success message plus a raw (non-`EditForm`) `<form action="Account/Logout" method="post">` + `<AntiforgeryToken />`, identical to `NavMenu.razor`'s existing logout form — reuses the existing `/Account/Logout` endpoint verbatim, no new backend code.
- `Profile.razor` reads the current user id via `[CascadingParameter] Task<AuthenticationState> AuthState` (first consumer of this cascading value in the app; `AddCascadingAuthenticationState()` was already registered) + `ClaimTypes.NameIdentifier`, not `UserManager.GetUserId(principal)`, to avoid injecting `UserManager` into the component.
- `NavMenu.razor`'s generic `<AuthorizeView><Authorized>` block (any signed-in user, not just admins) now includes a `href="profile"` link (`bi-person-circle-nav-menu` icon) alongside the username display and logout form.
- **Bug fixed while building Milestone 11, found only by manual browser verification, not bUnit**: `StatusMessage.razor`'s `[CascadingParameter] HttpContext HttpContext` threw `NullReferenceException` in `OnInitialized()` when the component is reached via in-circuit client-side navigation (e.g. clicking a `NavLink` to another page while already connected to an established Interactive Server circuit) — `HttpContext` only cascades during the HTTP request that (re)established the circuit, so it's `null` for any later same-circuit navigation. Every prior `<StatusMessage>` usage (`Login.razor`, `Register.razor`) happened to always be reached via a fresh full-page request, so this never surfaced until `Profile.razor` became the first page reached from an in-app `NavLink` that also renders it. Fixed by making the cascading parameter nullable (`HttpContext?`) with null-conditional access; regression-tested in `StatusMessageWithoutHttpContextTests` (a separate `BunitContext` class deliberately *without* the `CascadingValue<HttpContext>` wrapper the other `StatusMessageTests` add). Existing bUnit tests for Login/Register/Profile all explicitly add that wrapper, so none of them would have caught this — worth remembering when writing new tests for any page reachable via in-app navigation that renders `StatusMessage`.
- **Known pre-existing test failures, unrelated to any single milestone's changes — do not "fix" them incidentally while touching nearby code, treat as a separate task**: `LoginTests.cs` selects `#Input\.Email`, but `Login.razor`'s field has been `Input.UserName` since commit `b67ddf5` ("Fix admin login: authenticate by username, not email") — the test was never updated to match. `MainLayoutTests.cs` asserts markup contains "About"/"Reload" chrome that commit `b4caa3b` ("Remove About link from BlazorAdmin top bar") removed.

### Test conventions in practice
- bUnit (v2.7.2): `class FooTests : BunitContext`, `Render<Foo>()`. Examples across `Components/Pages/Admin/*`, `Account/*`, `Layout/*`.
- HTTP-calling code (e.g. `ExcelProcessingClient`) is tested with a hand-rolled `FakeHttpMessageHandler` (mirrors `legacy/ExcelProcessingClientService.Tests/FakeHttpMessageHandler.cs`), not RichardSzalay.MockHttp — avoids a new dependency for equivalent capability. Colocated per test project (`tests/ExcelETL.BlazorAdmin.Tests/ExternalApi/FakeHttpMessageHandler.cs`).
- WebAPI endpoints are tested via `WebApplicationFactory<Program>` integration tests (e.g. `ExcelProcessEndpointTests.cs`), not mocked HTTP.
- bUnit + `InputFile`: `cut.FindComponent<InputFile>()` + `InputFileContent.CreateFromText(...)` + `.UploadFiles(...)`. Note: triggering `InputFile.OnChange` in bUnit blocks synchronously until the whole async handler chain completes — a `TaskCompletionSource`-gated fake HTTP response that you only complete *after* calling `UploadFiles` will deadlock the test. Use immediately-resolved fake responses instead; the transient "Uploading" state isn't observable this way.
- bUnit auth with a specific user id (not just username/roles): `this.AddAuthorization().SetAuthorized("user@example.com")` then `.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"))` on the same `BunitAuthorizationContext` — `AddAuthorization()` already wires `CascadingAuthenticationState` into the render tree, so components consuming `[CascadingParameter] Task<AuthenticationState>` work without extra setup. See `ProfileTests.cs`.
- `UserRepository` is now a hybrid repository (plain EF reads via `IDbContextFactory` + `UserManager`-backed writes): its test class mixes both conventions from the top-level rule above — `GetByIdAsync`/`GetAllAsync` tests seed the real EF InMemory provider directly, `UpdateProfileAsync`/`ChangePasswordAsync` tests mock `UserManager<ApplicationUser>` via `IdentityManagerMocks.CreateUserManagerMock()`. Both fixtures live side by side in the same `UserRepositoryTests` class.

### Maintenance rule
Whenever a milestone adds new DI registrations, HTTP endpoints, config keys, JS modules, or resource files, update the relevant bullet above in the same commit. Treat this section as a cache of the exploration an agent would otherwise have to redo — keep it accurate rather than exhaustive.
