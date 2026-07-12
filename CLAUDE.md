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
