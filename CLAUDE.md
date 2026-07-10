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
- Blazor Web App (Server Interactivity model with direct DbContext access)

### Architecture
Clean Architecture (Onion Pattern)

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
