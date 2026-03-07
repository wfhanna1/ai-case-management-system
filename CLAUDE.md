# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

### Backend (.NET 9)

```bash
dotnet build                                          # Build entire solution
dotnet test                                           # Run all tests (~106 tests)
dotnet test tests/SharedKernel.Tests                  # Run one test project
dotnet test --filter "FullyQualifiedName~ClassName"   # Run single test class
dotnet test --filter "DisplayName~test_method_name"   # Run single test method
dotnet run --project src/ApiService/Api.WebApi        # Run API on port 5003
```

### Frontend (from `src/ClientApp/`)

```bash
npm install
npm run dev       # Vite dev server on port 3000
npm run build     # TypeScript check + Vite production build
npm run lint      # ESLint (max-warnings 0)
```

### Docker

```bash
cp .env.example .env   # Required before first run; set passwords
docker compose up --build
```

Services: API (:5003), Frontend (:3000), PostgreSQL (:5432), RabbitMQ (:5672/:15672), Qdrant (:6333/:6334), Grafana (:3001), Prometheus (:9090), Loki (:3100), Tempo (:3200).

**Loki logging driver (first time):** `docker plugin install grafana/loki-docker-driver:3.4.3 --alias loki --grant-all-permissions`

**Linux users:** Set `LOKI_DRIVER_URL=http://localhost:3100/loki/api/v1/push` in `.env` (the default `host.docker.internal` only resolves on Docker Desktop for macOS/Windows).

### Demo Credentials

Seeded automatically in Development via `DevelopmentDbSeeder`. Password for all: `Demo123!`

| Email | Role | Tenant |
|-------|------|--------|
| admin@alpha.demo | Admin | Alpha Clinic |
| worker@alpha.demo | IntakeWorker | Alpha Clinic |
| reviewer@alpha.demo | Reviewer | Alpha Clinic |
| admin@beta.demo | Admin | Beta Hospital |
| worker@beta.demo | IntakeWorker | Beta Hospital |
| reviewer@beta.demo | Reviewer | Beta Hospital |

---

## Architecture

Three microservices communicating via RabbitMQ (MassTransit), each following hexagonal architecture with the same four-layer structure:

```
{Service}.Domain/         # Entities, value objects, port interfaces
{Service}.Application/    # Use case handlers (commands/queries)
{Service}.Infrastructure/ # Adapters: EF Core, MassTransit consumers, file storage
{Service}.Host or WebApi/ # Composition root, DI, middleware
```

### Services

- **ApiService** -- REST API for document upload, retrieval, listing. Entry point for the pipeline.
- **OcrWorkerService** -- Consumes `DocumentUploadedEvent`, runs OCR, publishes `DocumentProcessedEvent`. Currently stubbed.
- **RagService** -- Consumes `EmbeddingRequestedEvent`, generates vector embeddings, stores in Qdrant, publishes `EmbeddingCompletedEvent`. Handler is wired; consumers are stubbed.

### Shared Libraries

- **SharedKernel** -- `Entity<TId>`, `ValueObject`, `AggregateRoot<TId>` (with domain events), `Result<T>` + `Error`, `TenantId`, `DocumentId`, `Unit`. Referenced by all services.
- **Messaging.Contracts** -- Shared MassTransit message records: `DocumentUploadedEvent`, `DocumentProcessedEvent`, `EmbeddingRequestedEvent`, `EmbeddingCompletedEvent`, `ExtractedFieldResult`.

### Message Flow

```
API upload -> DocumentUploadedEvent -> OcrWorker -> DocumentProcessedEvent
API        -> EmbeddingRequestedEvent -> RagService -> EmbeddingCompletedEvent
```

### Frontend

React 19 + TypeScript + Vite SPA. MUI v6 for UI, React Query v5 for server state, Zustand v5 for client state (auth). Nginx serves static files and proxies `/api/` to the API container.

---

## Key Patterns

**Result\<T\> for error handling.** All port methods and handlers return `Result<T>`. No exceptions for business failures. Access `.Value` on success, `.Error` on failure. Use `Result<Unit>` for void operations.

**Multi-tenancy.** All data access is scoped by `TenantId` (strongly-typed Guid wrapper). Tenant is resolved from the JWT `tenant_id` claim via `TenantResolutionMiddleware`. EF Core global query filters enforce row-level isolation. `ITenantContext` (in SharedKernel) provides the ambient tenant for the request scope.

**JWT Authentication.** BCrypt password hashing (work factor 12). JWT access tokens (15 min) with refresh token rotation (SHA-256 hashed, 7 day expiry). Authorization policies: RequireIntakeWorker, RequireReviewer, RequireAdmin. Auth endpoints (`/api/auth/*`) are exempt from tenant middleware.

**Port interfaces live in Domain.** Adapters (EF Core repos, file storage, message bus) implement port interfaces and are registered in the composition root. Dependencies point inward.

**Test conventions.** xUnit with `[Fact]`, `Assert.*`. Hand-written test doubles (sealed inner classes implementing port interfaces) instead of mocking frameworks. `NullLogger<T>` for logging. No Moq.

**MassTransit messaging.** Each worker uses `AddMassTransit` with RabbitMQ transport. Queue names follow `{service}-{event}` convention. Exponential retry: 3 attempts, 1s to 30s, factor 2.

**ApiResponse\<T\> envelope.** All controller responses wrap data in `ApiResponse<T>.Ok(data)` or `ApiResponse<T>.Fail(code, message)`. For validation errors, use `ApiResponse<T>.Fail(code, message, details)` where `details` is `Dictionary<string, string[]>` mapping field names to error messages. `ApiError.Details` is nullable so existing consumers are unaffected.

**Input validation.** FluentValidation validators in `Api.WebApi/Validation/` for all request DTOs. A global `ValidationFilter` (IAsyncActionFilter) resolves validators from DI, runs them, and returns 422 with `VALIDATION_ERROR` code and field-level `Details`. The default `[ApiController]` model state filter is suppressed. Validators must validate nested collections with `RuleForEach().ChildRules()`. Frontend uses per-field inline errors (MUI `error` + `helperText`) with validators in `src/utils/validation.ts`. Server errors display in a form-level `<Alert>`.

**Form templates.** `FormTemplate` aggregate with `TemplateField` value objects stored as JSON column via `OwnsMany(...).ToJson()`. Four template types: ChildWelfare, AdultProtective, HousingAssistance, MentalHealthReferral. Six field types: Text, Date, Number, Select, Checkbox, TextArea. Select fields store options as JSON string array. Shared DTO mapping in `Api.Application.Mappings.FormTemplateMappings`.

---

## Project Conventions

- .NET 9, C# with nullable reference types enabled, implicit usings
- `sealed` classes by default (entities, handlers, test classes, adapters)
- Records for DTOs, message contracts, and value-like types
- Domain aggregates use static factory methods (e.g., `IntakeDocument.Submit(...)`)
- No `async void`; all async methods return `Task` or `Task<T>`
- CI runs on PR to main: `dotnet restore/build/test` + `npm ci/lint/build`

---

## Workflow

**After completing each issue:** Commit the work and push it to a feature branch. Do not merge into main directly. Go back and review the issue's acceptance criteria. Verify each item is truly complete, then check off the completed items on the GitHub issue. Do not mark items complete unless you have verified them.

**At the end of each phase:** Run `/code-review` on all changes before merging into main. Fix any high-severity findings, then merge the feature branch into main via a pull request.
