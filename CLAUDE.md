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
