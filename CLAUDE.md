# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

### Backend (.NET 9)

```bash
dotnet build                                          # Build entire solution
dotnet test                                           # Run all tests (~67 tests)
dotnet test tests/SharedKernel.Tests                  # Run one test project
dotnet test --filter "FullyQualifiedName~ClassName"   # Run single test class
dotnet test --filter "DisplayName~test_method_name"   # Run single test method
dotnet run --project src/ApiService/Api.WebApi        # Run API on port 5000
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

Services: API (:5000), Frontend (:3000), PostgreSQL (:5432), RabbitMQ (:5672/:15672), Qdrant (:6333/:6334).

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

**Multi-tenancy.** All data access is scoped by `TenantId` (strongly-typed Guid wrapper). The API receives tenant ID via `X-Tenant-Id` header (GET) or form field (POST).

**Port interfaces live in Domain.** Adapters (EF Core repos, file storage, message bus) implement port interfaces and are registered in the composition root. Dependencies point inward.

**Test conventions.** xUnit with `[Fact]`, `Assert.*`. Hand-written test doubles (sealed inner classes implementing port interfaces) instead of mocking frameworks. `NullLogger<T>` for logging. No Moq.

**MassTransit messaging.** Each worker uses `AddMassTransit` with RabbitMQ transport. Queue names follow `{service}-{event}` convention. Exponential retry: 3 attempts, 1s to 30s, factor 2.

**ApiResponse\<T\> envelope.** All controller responses wrap data in `ApiResponse<T>.Ok(data)` or `ApiResponse<T>.Fail(code, message)`.

---

## Project Conventions

- .NET 9, C# with nullable reference types enabled, implicit usings
- `sealed` classes by default (entities, handlers, test classes, adapters)
- Records for DTOs, message contracts, and value-like types
- Domain aggregates use static factory methods (e.g., `IntakeDocument.Submit(...)`)
- No `async void`; all async methods return `Task` or `Task<T>`
- CI runs on PR to main: `dotnet restore/build/test` + `npm ci/lint/build`
