# Architecture and Rationale

This document describes the architecture of the Handwritten Intake Document Processor, a case management system that digitizes handwritten intake forms using OCR, indexes them with vector embeddings for similarity search, and provides a review workflow for human verification.

---

## Table of Contents

1. [System Architecture Overview](#system-architecture-overview)
2. [UML Component Diagram](#uml-component-diagram)
3. [Document Processing Flow](#document-processing-flow)
4. [Microservice Responsibilities and Communication](#microservice-responsibilities-and-communication)
3. [Hexagonal Architecture and DDD Patterns](#hexagonal-architecture-and-ddd-patterns)
4. [Template and Extraction Design](#template-and-extraction-design)
5. [RAG: Embedding, Chunking, and Similarity](#rag-embedding-chunking-and-similarity)
6. [Multi-Tenancy: Row-Level Isolation](#multi-tenancy-row-level-isolation)
7. [Messaging Topology](#messaging-topology)
8. [Real vs Mocked Services](#real-vs-mocked-services)
9. [Architecture Decisions and Rationale](#architecture-decisions-and-rationale)
10. [Testing Suite and Coverage](#testing-suite-and-coverage)

---

## System Architecture Overview

The following diagram shows the overall system architecture: all services, infrastructure components, and how they connect.

```mermaid
graph TB
    subgraph "Frontend"
        SPA["React SPA<br/>(React 19, MUI v6, Vite)"]
    end

    subgraph "Application Services"
        API["ApiService<br/>(.NET 9, REST API)"]
        OCR["OcrWorkerService<br/>(.NET 9, Background Worker)"]
        RAG["RagService<br/>(.NET 9, Background Worker)"]
    end

    subgraph "Data Stores"
        PG["PostgreSQL 16<br/>(Documents, Users, Cases)"]
        QD["Qdrant<br/>(Vector Database)"]
        FS["Local File Storage<br/>(Document Files)"]
    end

    subgraph "Messaging"
        RMQ["RabbitMQ 3<br/>(MassTransit)"]
    end

    subgraph "Observability"
        PROM["Prometheus"] --- GRAF["Grafana"]
        LOKI["Loki"] --- GRAF
        TEMPO["Tempo"] --- GRAF
    end

    SPA -->|"REST /api/*"| API
    API -->|"Publish events"| RMQ
    API -->|"EF Core"| PG
    API -->|"File I/O"| FS
    API -->|"HTTP /api/similar"| RAG

    RMQ -->|"DocumentUploadedEvent"| OCR
    OCR -->|"Read files"| FS
    OCR -->|"DocumentProcessedEvent"| RMQ

    RMQ -->|"EmbeddingRequestedEvent"| RAG
    RAG -->|"Store/query vectors"| QD

    RMQ -->|"DocumentProcessedEvent"| API

    API -.->|"OTLP"| TEMPO
    OCR -.->|"OTLP"| TEMPO
    RAG -.->|"OTLP"| TEMPO
    API -.->|"Docker log driver"| LOKI
    OCR -.->|"Docker log driver"| LOKI
    RAG -.->|"Docker log driver"| LOKI
    PROM -.->|"Scrape /metrics"| API
```

---

## UML Component Diagram

The ApiService follows hexagonal (ports-and-adapters) architecture. Dependencies always point inward.

```mermaid
classDiagram
    namespace WebApi {
        class DocumentsController {
            +Submit(file) IActionResult
            +GetById(id) IActionResult
            +DownloadFile(id) IActionResult
            +Stats() IActionResult
            +RecentActivity(limit) IActionResult
        }
        class ReviewController {
            +StartReview(id) IActionResult
            +CorrectField(id, request) IActionResult
            +FinalizeReview(id) IActionResult
            +GetAuditTrail(id) IActionResult
        }
        class TenantResolutionMiddleware {
            +InvokeAsync(context) Task
        }
    }

    namespace Application {
        class SubmitDocumentHandler {
            +HandleAsync(command) Result~DocumentDto~
        }
        class GetDashboardStatsHandler {
            +HandleAsync(tenantId) Result~DashboardStatsDto~
        }
        class GetRecentActivitiesHandler {
            +HandleAsync(tenantId, limit) Result~List~
        }
    }

    namespace Domain {
        class IntakeDocument {
            +Submit(file, tenant) IntakeDocument
            +StartProcessing() Result~Unit~
            +Complete(fields) Result~Unit~
            +StartReview(reviewer) Result~Unit~
            +Finalize(reviewer) Result~Unit~
        }
        class IDocumentRepository {
            <<interface>>
            +FindByIdAsync(id, tenant) Result~Document~
            +SaveAsync(doc) Result~Unit~
        }
        class IFileStoragePort {
            <<interface>>
            +UploadAsync(key, stream) Result~Unit~
            +DownloadAsync(key, tenant) Result~Stream~
        }
        class IMessageBusPort {
            <<interface>>
            +PublishDocumentUploadedAsync() Result~Unit~
        }
    }

    namespace Infrastructure {
        class EfDocumentRepository {
            -_db IntakeDbContext
        }
        class LocalFileStorageAdapter
        class MassTransitMessageBusAdapter
        class JwtTokenService
    }

    DocumentsController --> SubmitDocumentHandler
    DocumentsController --> GetDashboardStatsHandler
    DocumentsController --> GetRecentActivitiesHandler
    SubmitDocumentHandler --> IDocumentRepository
    SubmitDocumentHandler --> IFileStoragePort
    SubmitDocumentHandler --> IMessageBusPort
    EfDocumentRepository ..|> IDocumentRepository
    LocalFileStorageAdapter ..|> IFileStoragePort
    MassTransitMessageBusAdapter ..|> IMessageBusPort
```

---

## Document Processing Flow

End-to-end flow from document upload through OCR processing, review, and embedding.

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant API as ApiService
    participant RMQ as RabbitMQ
    participant OCR as OcrWorkerService
    participant RAG as RagService
    participant QD as Qdrant

    FE->>API: POST /api/documents (upload file)
    API->>API: Store file, create IntakeDocument (Submitted)
    API->>RMQ: Publish DocumentUploadedEvent

    RMQ->>OCR: Deliver to ocr-document-uploaded queue
    OCR->>OCR: Download file, run Tesseract OCR
    OCR->>OCR: Extract key-value fields from text
    OCR->>RMQ: Publish DocumentProcessedEvent

    RMQ->>API: Deliver to api-document-processed queue
    API->>API: Update document: Submitted -> Completed -> PendingReview
    API->>API: Auto-assign document to Case by subject name
    API->>RMQ: Publish EmbeddingRequestedEvent

    RMQ->>RAG: Deliver to rag-embedding-requested queue
    RAG->>RAG: Generate 384-dim embedding from extracted text
    RAG->>QD: Upsert vector with tenant_id payload
    RAG->>RMQ: Publish EmbeddingCompletedEvent

    Note over FE,API: Reviewer workflow begins

    FE->>API: POST /api/reviews/{id}/start
    API->>API: PendingReview -> InReview

    FE->>API: POST /api/reviews/{id}/correct-field
    API->>API: Store corrected value, log audit entry

    FE->>API: POST /api/reviews/{id}/finalize
    API->>API: InReview -> Finalized, log audit entry
```

---

## Microservice Responsibilities and Communication

### ApiService

The API gateway and primary backend. Handles all REST endpoints for the frontend:

- **Document lifecycle**: Upload, list, search, retrieve documents
- **Review workflow**: Start review, correct extracted fields, finalize review
- **Case management**: Auto-assign documents to cases by subject name, list/search cases, find similar cases via RAG
- **Authentication**: Register, login, JWT token issuance, refresh token rotation
- **Form templates**: Create and list configurable intake form templates
- **Audit trail**: Record and retrieve all actions taken on documents
- **Dashboard**: Aggregate statistics (pending review count, processed today, average processing time)

The API publishes `DocumentUploadedEvent` after storing an uploaded file. It consumes `DocumentProcessedEvent` to update document status after OCR completes and publishes `EmbeddingRequestedEvent` directly via MassTransit's `ConsumeContext` so the RagService can generate vector embeddings for similarity search.

### OcrWorkerService

A background worker that performs optical character recognition on uploaded documents.

- Consumes `DocumentUploadedEvent` from RabbitMQ
- Downloads the file from shared storage
- Runs OCR (Tesseract or mock adapter, configurable via `Ocr:Mode`)
- Extracts key-value fields from the OCR text using regex pattern matching
- Publishes `DocumentProcessedEvent` with extracted fields

The OCR worker has no REST API. It exposes only a `/health` endpoint and a Prometheus `/metrics` endpoint.

### RagService

A background worker that generates vector embeddings and provides similarity search.

- Consumes `EmbeddingRequestedEvent` from RabbitMQ
- Generates 384-dimensional embeddings (mock adapter uses SHA-256 seeded RNG; production would use OpenAI or a local model)
- Stores embeddings in Qdrant with tenant isolation via payload filtering
- Publishes `EmbeddingCompletedEvent` on success
- Exposes `GET /api/similar` and `POST /api/similar-by-text` endpoints for the API to query

### Inter-Service Communication

Services communicate exclusively through RabbitMQ (via MassTransit) for asynchronous processing and HTTP for synchronous queries (API to RAG for similarity search). There is no direct database sharing between services. The API owns the PostgreSQL schema, and the RAG service owns the Qdrant collection.

---

## Hexagonal Architecture and DDD Patterns

Each service follows the same four-layer hexagonal (ports-and-adapters) structure:

```
{Service}.Domain/          -- Entities, value objects, port interfaces
{Service}.Application/     -- Use case handlers (commands and queries)
{Service}.Infrastructure/  -- Adapters: EF Core, MassTransit, file storage
{Service}.Host or WebApi/  -- Composition root, DI registration, middleware
```

### Why Hexagonal Architecture

All business logic lives in the Domain layer. Aggregates enforce their own invariants (state transitions, validation, and tenant ownership) without any knowledge of how data is persisted or messages are dispatched. The domain layer defines port interfaces (e.g., `IDocumentRepository`, `IFileStoragePort`, `IMessageBusPort`) that infrastructure adapters implement. Infrastructure contains zero business logic; adapters only translate between domain contracts and external systems. This keeps the domain free of framework dependencies: the `IntakeDocument` aggregate does not know about EF Core, RabbitMQ, or the filesystem. The benefit is testability: every handler can be tested with hand-written test doubles that implement the same port interfaces, without spinning up databases or message brokers.

Dependencies always point inward: Infrastructure depends on Domain, never the reverse. Application depends on Domain for entities and ports. The Host/WebApi layer is the composition root that wires everything together via DI.

This strict decoupling is what enables a clean architecture approach. Because the Domain layer has no outward dependencies, it can be understood, tested, and refactored in complete isolation from delivery mechanisms and persistence details. The Application layer coordinates use cases by composing domain operations and port calls, but never contains business rules itself. Infrastructure is purely mechanical: it implements the contracts the domain defines, nothing more. The result is that each layer has a single reason to change: domain changes when business rules change, application changes when workflow orchestration changes, and infrastructure changes when external systems change. No layer bleeds into another.

### Domain-Driven Design Building Blocks

**Aggregates**: `IntakeDocument`, `Case`, `User`, `FormTemplate`, `AuditLogEntry`. Each aggregate inherits from `AggregateRoot<TId>`, which extends `Entity<TId>` with domain event support. Aggregates enforce their own invariants. For example, `IntakeDocument` enforces a strict state machine (Submitted -> Processing -> Completed -> PendingReview -> InReview -> Finalized) via `Result<T>` returns on transition methods.

**Value Objects**: `ExtractedField`, `TemplateField`, `TenantId`, `DocumentId`, `CaseId`, `UserId`, `FormTemplateId`. Value objects inherit from `ValueObject` (structural equality) or are implemented as sealed classes wrapping a `Guid`. Strongly-typed IDs prevent passing a `DocumentId` where a `CaseId` is expected.

**Domain Events**: `DocumentSubmittedEvent`, `DocumentProcessingStartedEvent`, `DocumentCompletedEvent`, `DocumentFailedEvent`, `UserRegisteredEvent`. These are raised by aggregates via `RaiseDomainEvent()` and cleared after dispatch.

**Static Factory Methods**: Aggregates use private constructors and static factory methods (`IntakeDocument.Submit(...)`, `Case.Create(...)`, `User.Register(...)`) to enforce creation invariants and raise initial domain events.

### The Result\<T\> Pattern

All port methods and handlers return `Result<T>` instead of throwing exceptions for business failures. `Result<T>` is a discriminated union: it holds either a `T` value (success) or an `Error` record (failure) with a machine-readable code and human-readable message.

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }        // throws if failure
    public Error Error { get; }    // throws if success
    public static Result<T> Success(T value) => ...;
    public static Result<T> Failure(Error error) => ...;
}
```

This eliminates null returns and exception-driven control flow for expected failures (e.g., "document not found", "invalid state transition"). Handlers check `IsSuccess`/`IsFailure` and propagate errors explicitly. The `Unit` type is used for `Result<Unit>` when there is no meaningful return value.

---

## Template and Extraction Design

### Form Templates

Form templates define the expected fields for different intake document types. The `FormTemplate` aggregate owns a collection of `TemplateField` value objects stored as a JSON column via EF Core's `OwnsMany(...).ToJson()`.

**Template types**: `ChildWelfare`, `AdultProtective`, `HousingAssistance`, `MentalHealthReferral`

**Field types**: `Text`, `Date`, `Number`, `Select`, `Checkbox`, `TextArea`. Select fields store their options as a JSON string array.

Templates are tenant-scoped and can be activated/deactivated. The frontend renders dynamic forms based on the template's field definitions.

### Field Extraction

When a document is uploaded, the API publishes a `DocumentUploadedEvent` with an optional `TemplateId`. The OCR worker processes the document and extracts fields:

- **Tesseract mode**: Runs the Tesseract CLI on the document (with PDF-to-image conversion via Docnet for PDFs). Parses the raw OCR text with a regex that matches `Key: Value` patterns on each line.
- **Mock mode**: Generates sample fields based on filename patterns (e.g., files containing "intake" get ClientName, DateOfBirth, CaseNumber, Address fields with random values and confidence scores between 0.3 and 1.0).

Extracted fields are represented as `ExtractedField` value objects with `Name`, `Value`, `Confidence`, and an optional `CorrectedValue`. The `CorrectedValue` is populated during the review workflow when a reviewer corrects an OCR extraction.

### Document State Machine

```mermaid
stateDiagram-v2
    [*] --> Submitted: Upload
    Submitted --> Processing: OCR worker picks up
    Processing --> Completed: OCR finishes
    Processing --> Failed: OCR error
    Completed --> PendingReview: Auto-transition
    PendingReview --> InReview: Reviewer claims
    InReview --> Finalized: Reviewer approves
    Submitted --> Failed: Unrecoverable error
```

Each transition is a method on the `IntakeDocument` aggregate that returns `Result<Unit>`. Invalid transitions (e.g., trying to finalize a document that is still Submitted) return a failure result with an `INVALID_TRANSITION` error code.

---

## RAG: Embedding, Chunking, and Similarity

### Embedding Generation

The RagService generates vector embeddings for document text to enable similarity search. The current architecture uses a port/adapter pattern:

- **Port**: `IEmbeddingPort.GenerateEmbeddingAsync(string text) -> Result<float[]>`
- **Local adapter** (default): `LocalEmbeddingAdapter` uses Microsoft's `SmartComponents.LocalEmbeddings` package with the `bge-micro-v2` model. Produces 384-dimensional L2-normalized vectors with real semantic meaning. Runs on CPU inside the RagService process (~50ms per embedding, ~23MB model auto-downloaded on first use).
- **Mock adapter** (fallback): `MockEmbeddingAdapter` produces deterministic 384-dimensional unit vectors by seeding a random number generator with a SHA-256 hash of the input text. No semantic meaning. Set `EMBEDDING_PROVIDER=mock` to use.
- **Configuration**: The `Embedding:Provider` setting (env var `Embedding__Provider` or `EMBEDDING_PROVIDER`) controls which adapter is registered. Default is `local`.

### Chunking Strategy

The current implementation embeds the full extracted text as a single chunk. The text content passed to the embedding port is a concatenation of the template type, subject name, and all extracted field key-value pairs. For example:

```
ChildWelfare. Emma Thompson. ChildName: Emma Thompson. Age: 7. ReasonForReferral: Educational neglect reported.
```

This works for intake documents because they are relatively short (a single page of extracted fields). If longer documents were introduced, a chunking strategy (e.g., sliding window with overlap) would be added at the `EmbedDocumentHandler` level.

### Vector Storage and Similarity Search

- **Port**: `IVectorStorePort` with `UpsertAsync`, `SearchAsync`, and `GetEmbeddingAsync`
- **Adapter**: `QdrantVectorStoreAdapter` uses the Qdrant .NET client over gRPC (port 6334)
- **Collection**: A single `documents` collection with cosine distance
- **Tenant isolation**: Each point's payload includes a `tenant_id` field. Search queries filter by tenant using Qdrant's `Must` filter condition, so tenants never see each other's documents.
- **Metadata**: Field values are stored as `meta_{key}` payload fields, enabling metadata-enriched search results.

The API service calls the RAG service's `POST /api/similar-by-text` endpoint, which generates an embedding from the query text on the fly and searches the vector store. This avoids requiring the queried document to already have a stored embedding.

### Data Seeding

In development, `RagDataSeeder` seeds 200+ synthetic case embeddings across all four template types and both demo tenants. This provides realistic similarity search results for development and demo purposes.

---

## Multi-Tenancy: Row-Level Isolation

### Design

Every data record is scoped to a tenant. `TenantId` is a strongly-typed wrapper around `Guid` that rejects `Guid.Empty` to prevent accidental use of uninitialized values.

### Tenant Resolution

1. User authenticates and receives a JWT containing a `tenant_id` claim
2. `TenantResolutionMiddleware` extracts the claim and populates a scoped `RequestTenantContext`
3. All downstream code accesses the tenant via `ITenantContext.TenantId`

Exempt paths (health checks, Swagger, auth endpoints, metrics) bypass tenant resolution.

### Database Isolation

EF Core global query filters enforce row-level isolation at the database layer:

```csharp
entity.HasQueryFilter(d =>
    _tenantContext != null &&
    _tenantContext.TenantId != null &&
    d.TenantId == _tenantContext.TenantId);
```

This filter is applied to `IntakeDocument`, `Case`, `FormTemplate`, and `AuditLogEntry`. Every query automatically includes the tenant predicate. The `Users` table does not use a global filter because auth endpoints (login, register) run before tenant context is established; tenant isolation for users is enforced explicitly in repository queries.

For background consumers that process messages without an HTTP request context, `FindByIdUnfilteredAsync` bypasses the global filter. The consumer then manually verifies the tenant from the event payload matches the document's tenant.

### Vector Store Isolation

Qdrant uses payload-based filtering. Each upserted point includes `tenant_id` in its payload, and every search query includes a `Must` filter on `tenant_id`. There is no cross-tenant data leakage because the filter is applied at the vector database level.

### Message Bus Isolation

Published messages include `TenantId` as a field in the event record. A `TenantHeaderPublishFilter` in the API service propagates tenant context as a message header for MassTransit consumers. Consumers verify tenant IDs from the message payload against the database record before processing.

---

## Messaging Topology

### Message Flow

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant API as ApiService
    participant RMQ as RabbitMQ
    participant OCR as OcrWorkerService
    participant RAG as RagService
    participant QD as Qdrant

    FE->>API: POST /api/documents (upload)
    API->>API: Store file, create IntakeDocument
    API->>RMQ: Publish DocumentUploadedEvent

    RMQ->>OCR: Deliver to ocr-document-uploaded queue
    OCR->>OCR: Download file, run OCR
    OCR->>RMQ: Publish DocumentProcessedEvent

    RMQ->>API: Deliver to api-document-processed queue
    API->>API: Update document: Processing -> Completed -> PendingReview
    API->>API: Auto-assign to Case by subject name
    API->>RMQ: Publish EmbeddingRequestedEvent

    RMQ->>RAG: Deliver to rag-embedding-requested queue
    RAG->>RAG: Generate embedding from extracted text
    RAG->>QD: Upsert vector with tenant payload
    RAG->>RMQ: Publish EmbeddingCompletedEvent
```

### Queue Configuration

| Queue Name | Service | Consumer | Event |
|---|---|---|---|
| `ocr-document-uploaded` | OcrWorkerService | `DocumentUploadedConsumer` | `DocumentUploadedEvent` |
| `api-document-processed` | ApiService | `DocumentProcessedConsumer` | `DocumentProcessedEvent` |
| `rag-embedding-requested` | RagService | `EmbeddingRequestedConsumer` | `EmbeddingRequestedEvent` |

### Retry Policy

All queues use the same exponential retry configuration:

- 3 retry attempts
- Initial interval: 1 second
- Maximum interval: 30 seconds
- Interval delta (factor): 2 seconds

After all retries are exhausted, MassTransit routes the message to the corresponding `_error` queue (dead-letter).

### Message Contracts

All message contracts are defined in the shared `Messaging.Contracts` library as immutable records:

| Event | Publisher | Consumer(s) | Key Fields |
|---|---|---|---|
| `DocumentUploadedEvent` | ApiService | OcrWorkerService | DocumentId, TenantId, FileName, StorageKey, TemplateId |
| `DocumentProcessedEvent` | OcrWorkerService | ApiService | DocumentId, TenantId, ExtractedFields, ProcessedAt |
| `EmbeddingRequestedEvent` | ApiService | RagService | DocumentId, TenantId, TextContent, FieldValues |
| `EmbeddingCompletedEvent` | RagService | -- | DocumentId, TenantId, CompletedAt |

---

## Real vs Mocked Services

The system uses port/adapter interfaces throughout, making it straightforward to swap between mock and production implementations. The current state:

| Component | Port Interface | Production Adapter | Mock/Stub Adapter | Current Default |
|---|---|---|---|---|
| OCR Engine | `IOcrPort` | `TesseractOcrAdapter` (shells out to Tesseract CLI, Docnet for PDF rendering) | `MockOcrAdapter` (generates random fields with configurable patterns) | **Configurable**: `Ocr:Mode=tesseract` selects production, `mock` selects stub. Docker uses `tesseract` mode. |
| Embedding Generation | `IEmbeddingPort` | `LocalEmbeddingAdapter` (SmartComponents bge-micro-v2, 384-dim, CPU) | `MockEmbeddingAdapter` (deterministic 384-dim vectors from SHA-256 hash) | **Configurable**: `Embedding:Provider=local` selects production, `mock` selects stub. Docker uses `local` mode. |
| Vector Store | `IVectorStorePort` | `QdrantVectorStoreAdapter` (gRPC client to Qdrant) | N/A | **Production** (Qdrant runs in Docker) |
| File Storage | `IFileStoragePort` | Planned: Azure Blob / S3 | `LocalFileStorageAdapter` (local filesystem) | **Local filesystem** |
| Message Bus | `IMessageBusPort` | `MassTransitMessageBusAdapter` (RabbitMQ) | N/A | **Production** (RabbitMQ runs in Docker) |
| Password Hashing | `IPasswordHasher` | `BcryptPasswordHasher` (work factor 12) | N/A | **Production** |
| Token Service | `ITokenService` | `JwtTokenService` (HMAC-SHA256 signing) | N/A | **Production** |
| Case Summary | `ISummaryPort` | Planned: OpenAI / LLM-based | `TemplateSummaryAdapter` (template string builder from field metadata) | **Template-based** |
| RAG Client | `IRagServiceClient` | `HttpRagServiceClient` (HTTP to RagService) | N/A | **Production** (calls RAG service) |

To move to production, the following adapters need replacement:
1. `LocalFileStorageAdapter` with Azure Blob or S3
2. `TemplateSummaryAdapter` with an LLM-based summary generator

---

## Architecture Decisions and Rationale

### Why microservices instead of a monolith?

The document processing pipeline has clear bounded contexts with different scaling profiles. OCR is CPU-intensive and benefits from independent scaling. The RAG service has its own data store (Qdrant) and could be replaced or upgraded independently. The API handles user-facing traffic with different latency requirements than background processing. Splitting into three services also enables independent deployment: updating the OCR engine does not require redeploying the API.

### Why hexagonal architecture?

Hexagonal architecture was chosen to decouple business logic from infrastructure concerns. All business logic lives inside the Domain layer. Aggregates enforce their own invariants (state transitions, validation rules, tenant ownership checks) and the Application layer orchestrates use cases by calling domain methods and port interfaces. Infrastructure adapters contain zero business logic; they only translate between domain contracts and external systems (EF Core, RabbitMQ, filesystem). This separation is what makes the architecture genuinely clean: the domain can be developed, tested, and reasoned about without knowing whether data lives in PostgreSQL or an in-memory dictionary, and without knowing whether messages flow through RabbitMQ or a direct method call. Each layer changes for exactly one reason: business rules change the domain, workflow changes touch the application layer, and technology changes affect only infrastructure. The primary benefit is testability: every handler in the Application layer can be tested with hand-written in-memory test doubles that implement the port interfaces. No database, no message broker, no file system needed for unit tests. The secondary benefit is portability: swapping from local file storage to Azure Blob requires changing only the adapter registration in the composition root, not any business logic.

### Why DDD with aggregates and value objects?

The domain has genuine complexity: documents go through a multi-step state machine, fields can be corrected during review, cases group related documents, and all of this must respect tenant boundaries. DDD aggregates enforce invariants at the domain level. The `IntakeDocument` aggregate prevents invalid state transitions regardless of which adapter or handler is calling it. Value objects like `ExtractedField` and `TenantId` provide structural equality and type safety.

### Why Result\<T\> instead of exceptions?

Business failures (document not found, invalid state transition, field not found) are expected outcomes, not exceptional conditions. Using exceptions for control flow obscures the actual failure paths and makes it hard to ensure all callers handle errors. `Result<T>` forces callers to explicitly check for failure, and the `Error` record provides both a machine-readable code (for switch/matching) and a human-readable message (for logging/display).

### Why strongly-typed IDs?

Passing raw `Guid` values around is error-prone: nothing prevents passing a document ID where a case ID is expected. `DocumentId`, `CaseId`, `UserId`, `FormTemplateId`, and `TenantId` are distinct types that catch misuse at compile time. They also reject `Guid.Empty` to prevent accidental use of default/uninitialized values.

### Why MassTransit over raw RabbitMQ?

MassTransit provides consumer abstraction, message retry with exponential backoff, dead-letter handling, message serialization, and OpenTelemetry integration out of the box. Raw RabbitMQ would require implementing all of this manually. MassTransit also supports swapping transports (e.g., to Azure Service Bus or Amazon SQS) without changing consumer code.

### Why EF Core global query filters for tenant isolation?

Global query filters provide defense-in-depth for multi-tenancy. Even if a developer forgets to add a `WHERE tenant_id = @tenantId` clause, the global filter ensures no cross-tenant data is returned. This is a safety net, not the only line of defense. Repositories also take `TenantId` as an explicit parameter.

### Why hand-written test doubles instead of mocking frameworks?

Hand-written test doubles (sealed inner classes implementing port interfaces) are explicit, readable, and do not depend on reflection-based mocking libraries. They make test behavior obvious: you can read the fake implementation and understand exactly what it returns. They also avoid the pitfalls of over-mocking, where tests become tightly coupled to implementation details.

### Why FluentValidation with a global filter?

The default ASP.NET model binding validation returns 400 with a format that does not match the `ApiResponse<T>` envelope. FluentValidation with a custom `ValidationFilter` returns 422 with `VALIDATION_ERROR` code and field-level `Details`, which the frontend can map directly to per-field inline error messages. This provides a consistent error contract across all endpoints.

### Why a shared Messaging.Contracts library?

Message contracts are the API boundary between services. Putting them in a shared library ensures type safety: publishers and consumers compile against the same record definitions. Breaking changes to a message contract cause compile errors in both the publisher and consumer, which is caught before deployment.

### Why PostgreSQL JSON columns for extracted fields and template fields?

`ExtractedField` and `TemplateField` are owned collections that belong to their parent aggregate. Storing them as JSON columns (via EF Core's `OwnsMany(...).ToJson()`) keeps the data model simple: no separate tables, no joins, no orphan cleanup. The tradeoff is that you cannot query individual fields efficiently with SQL, but the system does not need to. Fields are always loaded and saved as part of their parent aggregate.

---

## Testing Suite and Coverage

### Testing Pyramid

The project implements a full testing pyramid. Each layer runs faster and covers more granular behavior than the one above it. Together they form a safety net that makes refactoring low-risk: change any layer of the system and the tests catch regressions before code reaches production.

```
         /  E2E (Playwright)  \          50 tests -- real browser, real backend
        / Isolation (Playwright) \      100 tests -- real browser, mocked API
       /   Integration (.NET)     \      39 tests -- real DB (SQLite), real DI
      /     Unit (.NET + Vitest)    \  ~506 tests -- pure logic, no I/O
```

**Why this matters for refactoring.** The bottom of the pyramid (unit tests) pins down every domain invariant, handler behavior, and validation rule. Changing an aggregate's state machine or a handler's logic immediately breaks a targeted test that explains what went wrong. The middle layer (integration + isolation) confirms that adapters, repositories, and UI components still wire together correctly after a refactor. The top layer (E2E) validates that complete user workflows survive end-to-end. A developer can restructure internal code with confidence because all three layers must stay green before merging.

### Backend Test Projects

| Test Project | Layer | Count | What It Tests |
|---|---|---|---|
| `SharedKernel.Tests` | Unit | 42 | `Entity<T>`, `ValueObject`, `AggregateRoot<T>`, `Result<T>`, `TenantId`, `DomainEvent`, `AppMetrics`, `ServiceDiagnostics` |
| `Api.Domain.Tests` | Unit | 66 | `IntakeDocument` state machine, `Case`, `User`, `FormTemplate`, `ExtractedField`, `AuditLogEntry`, `FormTemplateId` |
| `Api.Application.Tests` | Unit | 115 | All command and query handlers: Submit, Review, Correct, Finalize, Login, Register, Search, Dashboard, RecentActivities, SimilarCases, AuditTrail, AssignDocumentToCase (name field priority) |
| `Api.Infrastructure.Tests` | Integration | 39 | EF Core repositories (in-memory SQLite), tenant isolation, cross-tenant isolation, `BcryptPasswordHasher`, `JwtTokenService`, `LocalFileStorageAdapter`, `HttpRagServiceClient`, `TemplateSummaryAdapter` |
| `Api.WebApi.Tests` | Unit | 148 | Controllers (Auth, Documents, Cases, Review, FormTemplates), `TenantResolutionMiddleware`, `ValidationFilter`, all FluentValidation validators, `ApiResponse<T>`, OpenAPI contract drift tests (bidirectional) |
| `Messaging.Tests` | Unit | 33 | `MassTransitMessageBusAdapter`, all three consumers (`DocumentProcessedConsumer`, `DocumentUploadedConsumer`, `EmbeddingRequestedConsumer`), `TenantHeaderPublishFilter`, AsyncAPI contract drift tests |
| `OcrWorker.Tests` | Unit | 19 | `ProcessDocumentHandler`, `MockOcrAdapter`, `TesseractOcrAdapter`, `LocalFileStorageReadAdapter`, consumer log scopes |
| `RagService.Tests` | Unit + Integration | 29 | `EmbedDocumentHandler`, `SimilarDocumentsHandler`, `FindSimilarByTextHandler`, `MockEmbeddingAdapter`, `EmbeddingRequestedConsumer`, `RagDataSeeder`, OpenAPI contract drift tests, topK default validation |

**Total backend tests**: ~506 across 8 test projects.

### Frontend Tests (Vitest)

| Scope | Count | What It Tests |
|---|---|---|
| Unit | ~10 | Auth store (`authStore.test.ts`), utility functions (`index.test.ts`), JWT parsing (`jwt.test.ts`), validation (`validation.test.ts`) |

### Playwright Test Suite

Playwright provides the browser-level testing layers. The suite is split into two Playwright projects configured in `playwright.config.ts`, each serving a distinct purpose in the pyramid.

#### Isolation Tests (100 tests, 20 spec files)

Isolation tests run against a real browser with the Vite dev server, but **mock all API calls** using Playwright's `page.route()`. This makes them fast, deterministic, and independent of backend state. They verify that UI components render correctly, handle user interactions, and display API responses as expected.

| Spec File | What It Tests |
|---|---|
| `dashboard-page.spec.ts` | Dashboard heading, stat cards, auth redirect |
| `dashboard-stats.spec.ts` | Stat card values from mocked stats endpoint |
| `dashboard-recent-activity.spec.ts` | Activity list rendering, empty state, API failure, data freshness on navigation |
| `documents-page.spec.ts` | Document list rendering, status chips |
| `documents-review.spec.ts` | Review button visibility based on document status |
| `upload-page.spec.ts` | File upload form, success/error messages |
| `review-queue-page.spec.ts` | Pending review list, pagination |
| `review-detail-page.spec.ts` | Start review, correct field, finalize workflow |
| `review-workflow.spec.ts` | Full review state transitions with mocked API |
| `ocr-extracted-fields.spec.ts` | Extracted field display, confidence indicators, OCR status |
| `document-preview.spec.ts` | Image/PDF preview rendering, error states |
| `similar-cases-panel.spec.ts` | Similar cases list, scores, empty state |
| `cases-page.spec.ts` | Case list rendering, search |
| `case-detail-page.spec.ts` | Case detail with linked documents |
| `search-page.spec.ts` | Search form, results rendering |
| `login-page.spec.ts` | Login form rendering, validation |
| `templates-page.spec.ts` | Template list, create form |
| `template-printable.spec.ts` | Printable form rendering |
| `navigation.spec.ts` | Responsive nav, mobile drawer |
| `role-nav-visibility.spec.ts` | Role-based menu item visibility, route access control |

Isolation tests use shared fixtures (`auth.fixture.ts`) to inject authentication state and shared helpers (`api-mocks.ts`) to set up route mocks with consistent mock data (`mock-data.ts`).

#### E2E Tests (50 tests, 14 spec files)

E2E tests run against the **full running stack** (frontend + API + database + RabbitMQ + OCR worker). They exercise complete user workflows from login through document processing, verifying that all services communicate correctly.

| Spec File | What It Tests |
|---|---|
| `auth-flow.spec.ts` | Login, token storage, authenticated navigation, logout |
| `registration.spec.ts` | User registration, duplicate email rejection |
| `upload.spec.ts` | Document upload via real API, document appears in list |
| `review-workflow.spec.ts` | Start review, correct fields, finalize (role-based access) |
| `ocr-processing.spec.ts` | Upload triggers OCR pipeline, extracted fields appear |
| `case-assignment.spec.ts` | Document assigned to case by subject name after OCR |
| `dashboard-e2e.spec.ts` | Real stats from API, recent activity from audit log |
| `templates.spec.ts` | Create template, verify in list |
| `template-upload-flow.spec.ts` | View template, navigate to upload with template selected |
| `template-printable-e2e.spec.ts` | Print-friendly template rendering |
| `multi-tenant.spec.ts` | Tenant A cannot see tenant B data |
| `validation.spec.ts` | Login/register form validation with real server errors |
| `routing.spec.ts` | Root redirect, protected route behavior |
| `bug77-repro.spec.ts` | Regression test: review status persists after finalize |

### Why This Structure Enables Safe Refactoring

The three test layers protect different aspects of the system during refactoring:

**Unit tests** pin down business rules. If you refactor `IntakeDocument.MarkCompleted()` or change the name field priority logic in `AssignDocumentToCaseHandler`, the 66 domain tests and 115 handler tests catch any behavioral change immediately. These run in under a second, so you get instant feedback.

**Integration and isolation tests** verify wiring. If you restructure a repository, change a controller's response shape, or refactor a React component's props, these tests catch the breakage at the boundary. Integration tests boot real EF Core with SQLite. Isolation tests render real React components with mocked API responses.

**E2E tests** guard user workflows. If a refactor silently breaks the upload-to-review pipeline or the multi-tenant isolation, the E2E tests fail because they exercise the real stack. These are the slowest to run but catch the hardest-to-find bugs: race conditions between services, message bus misconfiguration, and authentication edge cases.

**Contract drift tests** protect cross-service boundaries. The OpenAPI and AsyncAPI drift tests compare runtime Swagger output and C# message records against checked-in YAML specs. Any structural change to an API endpoint or message event that is not reflected in the spec (or vice versa) fails the build. This prevents silent contract breakage between independently deployed services.

### Backend Testing Approach

- **Framework**: xUnit with `[Fact]` assertions and `Assert.*` methods
- **Test doubles**: Hand-written sealed inner classes implementing port interfaces. No Moq or other mocking frameworks.
- **Logging**: `NullLogger<T>` for all tests that require an `ILogger<T>` parameter
- **Naming convention**: Test methods use descriptive names (e.g., `HandleAsync_PrefersSubjectName_OverClientName`)
- **Infrastructure tests**: Use EF Core's in-memory SQLite provider for repository tests, verifying tenant isolation and query filter behavior
- **Messaging tests**: Verify consumer behavior by calling `Consume()` directly with constructed `ConsumeContext` wrappers, asserting on repository state changes and published messages
- **Contract drift tests**: Bidirectional comparison of runtime Swagger/reflection output against checked-in YAML specs. Any divergence fails the build.

### Coverage Areas

- All domain aggregate invariants (state transitions, validation, factory methods)
- All command and query handlers (success and failure paths)
- Tenant isolation at the database layer (global query filters, cross-tenant rejection)
- JWT token generation and validation
- Password hashing and verification
- File storage operations
- Input validation for all request DTOs
- Controller routing and response mapping
- Middleware behavior (tenant resolution, authentication checks, exempt paths)
- Message consumer processing (field mapping, status transitions, error handling)
- Message contract serialization compatibility
- OpenAPI and AsyncAPI contract drift detection
- UI component rendering, user interactions, and API response handling (Playwright isolation)
- Full user workflows across all services (Playwright E2E)
- Multi-tenant data isolation at the browser level
- Role-based access control and navigation visibility
- Data freshness after navigation (staleTime behavior)
