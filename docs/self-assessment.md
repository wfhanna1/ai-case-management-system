# Self-Assessment

An honest evaluation of the Intake Document Processor system: what was built, what is missing, the trade-offs made, and how AI tools were used throughout development.

---

## Requirements Coverage

| # | Requirement | Status | Notes |
|---|------------|--------|-------|
| 1 | Secure Login | Complete | JWT + refresh token rotation, 3 roles (Admin, IntakeWorker, Reviewer) |
| 2 | Form Templates | Complete | 4 templates, 6 field types, view/download/print |
| 3 | Upload and Processing | Complete | Real OCR (Tesseract in Docker), regex field extraction, confidence scoring |
| 4 | Review and Human-in-the-Loop | Complete | Role-gated workflow, inline corrections, color-coded confidence, audit trail |
| 5 | Search and Case View | Complete | Name/date/template search, case aggregation by subject |
| 6 | RAG + Vector DB | Complete | Qdrant integrated, tenant-isolated, similar cases surfaced in review UI; real semantic embeddings via SmartComponents bge-micro-v2 |
| 7 | Multi-Tenancy | Complete | Row-level isolation, JWT propagation, vector store filtering, message bus verification |
| 8 | Observability and Resilience | Complete | Grafana/Prometheus/Loki/Tempo, OpenTelemetry tracing as correlation IDs, health checks, MassTransit retry policies |
| 9 | Tests | Complete | 516 backend + 102 isolation + 53 E2E tests |
| 10 | Deliverables | Complete | Docker Compose, OpenAPI/AsyncAPI specs, architecture doc, README, self-assessment |

---

## Completed Features

### Core Pipeline
- **Document upload** with local file storage, template association, and automatic status tracking
- **OCR processing** via Tesseract (real OCR in Docker, mock adapter for tests), with PDF-to-image conversion using Docnet
- **Field extraction** using regex key-value pattern matching on OCR output text, with **confidence scoring** per field (0.0 to 1.0)
- **Review workflow** with role-based access: start review, correct extracted fields, finalize with full audit trail
- **Case assignment** that auto-groups documents by subject name, with priority resolution (Subject > Client > any Name field)

### RAG and Similar Case Context
- **Real semantic embeddings** via SmartComponents `bge-micro-v2` model (384-dimensional, CPU-only, ~23MB ONNX model running inside the RagService process with no external dependencies). Configurable via `Embedding:Provider` (default: `local`, fallback: `mock` for tests)
- **Embedding strategy:** each document is embedded as a single vector built from concatenated extracted field name-value pairs (corrected values take precedence over originals when available)
- **No chunking:** one embedding per document, which is sufficient for single-page intake forms but would need chunking for longer documents
- **Similar cases surfaced during review:** when a reviewer opens a document, the system generates an embedding on the fly from the document's extracted fields, queries Qdrant for the top 5 most similar documents (excluding itself), and presents them in the review UI
- **UI presentation:** an accordion panel shows each similar case with a percentage-based similarity score (color-coded: green above 90%, yellow above 70%, red below), a generated summary, and expandable field details pulled from the vector store metadata
- **Tenant isolation in vectors:** a single Qdrant collection with payload filtering on `tenant_id`, so each tenant's similarity searches only return their own cases
- **Summary generation:** `ISummaryPort` is called for each similar case result; currently uses a template-based adapter (not an LLM), which produces structured but not narrative summaries

### Architecture
- **Three microservices** (API, OCR Worker, RAG Service) communicating via RabbitMQ/MassTransit
- **Hexagonal architecture** in all services: Domain (entities, ports), Application (handlers), Infrastructure (adapters), Host/WebApi (composition root)
- **Domain-Driven Design** with aggregates enforcing their own invariants, value objects for type safety, static factory methods, and domain events
- **Result\<T\> pattern** everywhere, eliminating exception-driven control flow for business failures
- **Strongly-typed IDs** (DocumentId, CaseId, TenantId, etc.) preventing cross-type ID confusion at compile time

### Multi-Tenancy
- **Row-level isolation** via EF Core global query filters on all tenant-scoped entities
- **JWT-based tenant resolution** from the `tenant_id` claim, enforced by middleware
- **Vector store isolation** via Qdrant payload filtering on `tenant_id`
- **Message bus isolation** with `TenantId` in every event payload and explicit verification in consumers

### Authentication and Authorization
- **JWT access tokens** (15-minute expiry) with **refresh token rotation** (SHA-256 hashed, 7-day expiry)
- **BCrypt password hashing** (work factor 12)
- **Role-based authorization** policies: RequireIntakeWorker, RequireReviewer, RequireAdmin
- **Frontend auth state** persisted via Zustand with localStorage, automatic token refresh on 401

### Frontend
- **React 19 + TypeScript + Vite** SPA with MUI v6 components
- **React Query v5** for server state, **Zustand v5** for client state (auth)
- **Role-based navigation** with menu items and routes gated by user role
- **Per-field inline validation** with server-side FluentValidation returning field-level error details
- **Dashboard** with live stats (pending reviews, processed today, avg processing time) and recent activity feed
- **Document preview** for images and PDFs, template printing, search, case detail views
- **Confidence display in review:** extracted fields table shows per-field confidence as color-coded chips (green >90%, yellow 70-90%, red <70%), letting reviewers quickly identify fields that need attention

### Observability
- **Grafana + Prometheus + Loki + Tempo** stack running in Docker
- **OpenTelemetry tracing** across all three services with Tempo as the backend; trace IDs serve as **correlation IDs** and are included in structured log scopes (`TraceId`, `TenantId`, `DocumentId`) on every consumer and middleware, enabling cross-service request tracing through Loki and Tempo
- **Structured logging** via the Loki Docker log driver
- **Prometheus metrics** endpoints on all services
- **Health checks** on every service, used by Docker Compose for dependency ordering

### Testing
- **516 .NET tests** across 9 test projects (unit, integration)
- **102 Playwright isolation tests** (real browser, mocked API) across 20 spec files
- **53 Playwright E2E tests** (real browser, full running stack) across 15 spec files
- **OpenAPI and AsyncAPI contract drift tests** providing bidirectional comparison of runtime output against checked-in YAML specs
- **Hand-written test doubles** (no mocking frameworks) for all port interfaces

### DevOps
- **Docker Compose** orchestrating 11 services with health checks and dependency ordering
- **GitHub Actions CI** running on every PR: dotnet restore/build/test + npm ci/lint/build
- **Environment validation** script (`scripts/check-env.sh`) for required variables
- **Seed data** with 6 users, 8 templates, 216 cases, 400+ documents across 2 tenants

---

## Missing Items

### Production Readiness
- ~~**Real embedding model.**~~ Resolved. `LocalEmbeddingAdapter` uses SmartComponents `bge-micro-v2` model (384-dim, CPU-only, ~23MB). Runs inside the RagService process with no external dependencies. Configurable via `Embedding:Provider` (default: `local`, fallback: `mock`).
- **Cloud file storage.** LocalFileStorageAdapter writes to the local filesystem. Needs Azure Blob Storage or S3 for production.
- **LLM-based case summaries.** TemplateSummaryAdapter uses string templates. Needs an LLM to generate meaningful case narrative summaries.
- **Rate limiting.** No rate limiting on API endpoints. Should add middleware or use a reverse proxy.
- **HTTPS.** Docker Compose runs everything over HTTP. Production needs TLS termination (nginx or a load balancer).
- **Database migrations in production.** EF Core auto-migrates on startup, which is not safe for production. Needs a migration pipeline.

### Features Not Implemented
- **User management UI.** No admin interface for creating/editing users or managing roles. Users are seeded or created via the register endpoint.
- **Batch operations.** No bulk upload, bulk review, or bulk status changes.
- **Notifications.** No email or in-app notifications when documents are ready for review.
- **Document versioning.** No support for re-uploading or versioning documents.
- **Pagination on all list endpoints.** Most list endpoints are paginated, but some edge cases may not be.
- **Audit log export.** Audit trail is viewable but not exportable (CSV, PDF).

### Test Gaps
- **No load/performance tests.** No k6 or similar load testing to validate throughput under concurrent users.
- **No chaos testing.** No verification of behavior when services crash mid-processing (for example, the OCR worker dies after consuming but before publishing).
- **Frontend unit test coverage is thin.** Only ~10 Vitest tests covering auth store and utilities. Component-level unit tests are covered by Playwright isolation tests instead.

---

## Trade-offs

### Mock adapters for some AI services
The case summary adapter (`TemplateSummaryAdapter`) is still a mock. The embedding adapter has been replaced with a real local model (`LocalEmbeddingAdapter` using SmartComponents bge-micro-v2). The port/adapter pattern means swapping in remaining real implementations requires only a new adapter class and a DI registration change, with no business logic changes.

### Multi-tenancy isolation strategy

**Decision:** Row-level tenant isolation via `TenantId` column and EF Core global query filters.

**Context:** The system must prevent tenants from seeing each other's data across all operations (documents, cases, templates, vector search, review actions). Three strategies were evaluated:

**Option 1: Row-level isolation (chosen)**

All tenants share a single database. Every entity carries a `TenantId` column, and EF Core global query filters automatically scope every query.

| Pros | Cons |
|------|------|
| Single database, single connection string, single migration path | All tenant data in the same physical database |
| Fastest to implement and operate | A query filter bug could leak data across tenants |
| Lowest infrastructure cost at small to medium scale | Cannot do per-tenant backup/restore independently |
| Simple connection pooling and resource management | Noisy-neighbor risk under heavy load from one tenant |

**Option 2: Database-per-tenant**

Each tenant gets a dedicated PostgreSQL database (or schema). Tenant resolution maps to the correct connection string at request time.

| Pros | Cons |
|------|------|
| Strongest physical data isolation | Connection string management grows with tenant count |
| Per-tenant backup, restore, and scaling | Migrations must run against every database |
| Required for some HIPAA or contractual data residency requirements | Higher infrastructure cost and operational complexity |
| No noisy-neighbor risk at the database level | Cross-tenant reporting or analytics requires federation |

**Option 3: Subscription-level separation**

Tiered isolation based on tenant subscription level. Standard-tier tenants share infrastructure via row-level filtering. Premium-tier tenants get dedicated databases or dedicated compute.

| Pros | Cons |
|------|------|
| Balances cost efficiency with compliance flexibility | Most complex to implement and reason about |
| Premium tenants get the isolation guarantees they pay for | Two isolation codepaths to test and maintain |
| Standard tenants keep infrastructure costs low | Tenant upgrades/downgrades require data migration |
| Natural fit for SaaS pricing models | Operational runbooks differ by tier |

**Why row-level isolation was chosen:** Given the time constraint, row-level isolation provided the fastest path to correct tenant boundaries across all operations. It keeps infrastructure simple while still enforcing strict data separation at the query level. The codebase is structured to support migrating to either alternative: tenant resolution is centralized in `TenantResolutionMiddleware`, all data access goes through port interfaces, and the `ITenantContext` abstraction means switching the isolation strategy would be a DI and infrastructure change, not a domain or application layer rewrite.

### Local file storage shared via Docker volume
The API writes uploaded files to a local directory. The OCR worker reads from the same directory via a shared Docker volume. This works for single-node deployments but does not scale horizontally. The `IFileStoragePort` and `IFileStorageReadPort` interfaces exist specifically so this can be swapped for object storage.

### JSON columns for extracted fields and template fields
Storing `ExtractedField` and `TemplateField` as JSON columns (EF Core `OwnsMany(...).ToJson()`) avoids join tables and simplifies the data model. The trade-off is that you cannot efficiently query individual fields with SQL. This is acceptable because fields are always loaded as part of their parent aggregate and never queried independently.

### Hand-written test doubles over mocking frameworks
Hand-written sealed inner classes implementing port interfaces are more verbose than Moq or NSubstitute, but they are explicit, readable, and do not depend on reflection magic. Each test double's behavior is visible in the test file. The downside is more boilerplate when port interfaces change.

### Playwright isolation tests as the primary frontend test layer
Instead of writing hundreds of React Testing Library component tests, the project uses 100 Playwright isolation tests that render real components in a real browser with mocked API responses. This gives higher confidence (real DOM, real CSS, real routing) at the cost of slower execution compared to jsdom-based unit tests.

### EF Core auto-migration on startup
Migrations run automatically when the API starts. This is convenient for development but dangerous for production (concurrent instances could conflict, failed migrations leave the database in an inconsistent state). A production deployment would need a separate migration step.

---

## Development Approach and AI Tool Usage

All development was done using **Claude Code** (Anthropic's CLI agent, powered by Claude Opus 4.6), extended with custom skill plugins and agentic workflows described below.

### Planning and backlog creation

After receiving the project requirements document, I uploaded it to Claude Code and used it to build a structured roadmap and backlog. Claude Code analyzed the requirements and broke the full project scope into nine phases:

| Phase | Name |
|-------|------|
| 0 | Scaffolding |
| 1 | Auth and Tenancy |
| 2 | Templates |
| 3 | Upload and OCR Pipeline |
| 4 | Review Workflow |
| 5 | Search and Cases |
| 6 | RAG Pipeline |
| 7 | Observability |
| 8 | Documentation |

I then created the backlog as GitHub Issues and organized them in a GitHub Project board to track progress across all phases.

### Phased execution

I began by working on the Phase 0 and Phase 1 architecture stories individually, since those foundational decisions (solution structure, hexagonal architecture, multi-tenancy, authentication) were the most critical to get right early. Each story was reviewed and validated before moving on. The development process within each story was iterative: I would describe what I wanted, Claude would implement it, and I would review the output and request changes.

As the project matured and a safety net of quality gates emerged (unit and integration tests, contract drift tests, and a growing suite of Playwright isolation and E2E tests), I shifted to working through the backlog one full phase at a time. After completing each phase, I performed manual testing of the entire application to catch issues that automated tests might miss, then ran a code review pass before merging into main.

This phased approach provided a balance between moving quickly and maintaining confidence in the system. Early phases demanded careful, story-by-story attention. Later phases benefited from the accumulated test coverage, which made it safe to move faster without sacrificing quality.

### How AI was used throughout

**Architecture and design.** I made the key architectural decisions: microservices over monolith, hexagonal architecture, DDD with aggregates, row-level multi-tenancy, Result\<T\> over exceptions. Claude translated those decisions into concrete code structures, project layouts, and dependency injection configurations. When Claude proposed alternatives (for example, using MediatR for command dispatch), I evaluated and either accepted or redirected based on the project's goals.

**Test-driven development.** Claude followed strict TDD, writing failing tests first, verifying they fail for the right reason, then writing minimal code to make them pass. The TDD skill plugin enforced this discipline throughout.

**Code generation.** All production code, tests, Docker configuration, CI pipeline, and documentation was generated by Claude based on my direction. I provided requirements and reviewed output.

**Debugging.** When tests failed or Docker services would not start, I described the symptoms and Claude traced through the code to identify root causes. Examples include tracing a Playwright test failure through the axios interceptor chain to find a missing API mock, and identifying a unique constraint violation in seed data caused by random name collisions.

**Refactoring.** Claude performed architectural refactoring (for example, extracting business logic from Infrastructure consumers into Application handlers, moving state-transition logic from Application into Domain aggregates) while keeping all tests green.

**Agentic workflows and custom plugins.** Beyond using Claude Code as a single agent, I built and used two custom plugins to accelerate development:

- **Ralph Loop** (`/ralph-loop`): a recurring loop that runs a prompt or slash command on a configurable interval within the same session. This was used to continuously monitor test results, re-run builds, and catch regressions during active development without manual re-invocation.

- **Enterprise Agent Team** (`/enterprise-agent-team`): a custom multi-agent plugin that spawns specialized subagents (backend engineer, frontend engineer, QA engineer, platform engineer, security reviewer, code reviewer, tech lead) to work on tasks in parallel. Each agent operates with scoped tool access and domain focus. For example, the backend engineer and frontend engineer could implement both sides of a new feature simultaneously, while the QA engineer wrote tests in parallel. A tech lead agent coordinated cross-cutting concerns.

These plugins, combined with the TDD skill and the accumulated test suite as a safety net, allowed later phases to move significantly faster. The trade-off was higher token consumption: parallel agents and recurring loops burn through context quickly, and the Enterprise Agent Team in particular consumed substantially more tokens per phase than single-agent development. The parallelization was worth it for throughput, but it required careful prompt design to avoid agents duplicating or conflicting with each other's work.

**Documentation.** README, architecture.md, handover.md, and this self-assessment were all written by Claude based on the actual codebase state.

### Example prompts

- "Set up the solution structure with hexagonal architecture and DDD patterns"
- "Implement document upload with local file storage and publish a DocumentUploadedEvent"
- "Add multi-tenancy with row-level isolation using EF Core global query filters"
- "Write Playwright isolation tests for the review workflow"
- "The Playwright navigation tests are failing. The tests are isolated so they should not need a running backend. Investigate why."
- "Refactor FinalizeReviewHandler to use a domain method for the PendingReview-to-InReview auto-transition instead of doing the state check in the application layer"
- "Add 200+ seed data cases with realistic field values"
- "Run the full test suite and fix any failures"
- "/ralph-loop 5m dotnet test" (recurring test monitoring during active development)
- "/enterprise-agent-team" followed by delegating backend, frontend, and QA work to parallel agents

### What worked well
- TDD enforcement caught bugs early and made refactoring safe
- The port/adapter pattern made it easy to swap between mock and real implementations
- Playwright isolation tests provided high-confidence UI testing without needing the full stack
- Claude's ability to trace through multi-layer failures (for example, a missing API mock causing a 401 causing auth clear causing a redirect) was effective
- The Enterprise Agent Team plugin allowed parallelizing independent frontend and backend work within a single phase, reducing wall-clock time for later phases
- Ralph Loop provided continuous feedback during development without manual re-invocation

### What was challenging
- Context window limits required session continuations, which sometimes lost context about in-progress work
- Complex Docker Compose interactions (health checks, volume mounts, service dependencies) required trial and error
- Playwright E2E tests are flaky when the Docker stack is not fully healthy, requiring retries
- Parallel agent workflows consumed significantly more tokens than single-agent development, requiring awareness of cost-to-throughput trade-offs
- Agents occasionally duplicated work or made conflicting changes when task boundaries were not clearly defined in the prompt

---

## What Would Change

### If starting over

1. **Start with a monolith, extract services later.** The microservice boundaries are correct, but building three services from day one added complexity to development and testing that was not needed early on. A modular monolith with clear bounded contexts could have been split later when scaling requirements became clear.

2. **Use a real embedding model from the start.** The mock embedding adapter was in place for most of development, meaning similarity search results were meaningless until the local model was added late. Starting with the real model earlier would have allowed validating RAG quality throughout development rather than only at the end.

3. **Add database migration tooling early.** EF Core auto-migration is fine for development, but having a proper migration pipeline (e.g., FluentMigrator or a CI step) from the start would prevent the "works on my machine" problem.

4. **Invest more in frontend unit tests.** The Playwright isolation tests are valuable but slow. A layer of fast React Testing Library tests for individual components would give quicker feedback during frontend development.

5. **Add structured error codes earlier.** The `ApiResponse<T>` envelope with error codes was added midway. Having it from the first endpoint would have avoided retrofitting error handling across the frontend.

---

## Documentation Review

README.md and architecture.md have been reviewed and confirmed accurate against the current codebase state, including service URLs, demo credentials, seed data counts, test counts, diagrams, and architecture decision rationales.
