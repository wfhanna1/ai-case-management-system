# Self-Assessment

An honest evaluation of the Intake Document Processor system: what was built, what is missing, the trade-offs made, and how AI tools were used throughout development.

---

## Completed Features

### Core Pipeline
- **Document upload** with local file storage, template association, and automatic status tracking
- **OCR processing** via Tesseract (real OCR in Docker, mock adapter for tests), with PDF-to-image conversion using Docnet
- **Field extraction** using regex key-value pattern matching on OCR output text
- **Review workflow** with role-based access: start review, correct extracted fields, finalize with full audit trail
- **Case assignment** that auto-groups documents by subject name, with priority resolution (Subject > Client > any Name field)
- **Vector embeddings** stored in Qdrant with tenant-isolated similarity search (mock embedding adapter; Qdrant is real)

### Architecture
- **Three microservices** (API, OCR Worker, RAG Service) communicating via RabbitMQ/MassTransit
- **Hexagonal architecture** in all services: Domain (entities, ports), Application (handlers), Infrastructure (adapters), Host/WebApi (composition root)
- **Domain-Driven Design** with aggregates enforcing their own invariants, value objects for type safety, static factory methods, and domain events
- **Result\<T\> pattern** everywhere -- no exception-driven control flow for business failures
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
- **Role-based navigation** -- menu items and routes gated by user role
- **Per-field inline validation** with server-side FluentValidation returning field-level error details
- **Dashboard** with live stats (pending reviews, processed today, avg processing time) and recent activity feed
- **Document preview** for images and PDFs, template printing, search, case detail views

### Observability
- **Grafana + Prometheus + Loki + Tempo** stack running in Docker
- **OpenTelemetry tracing** across all three services with Tempo as the backend
- **Structured logging** via the Loki Docker log driver
- **Prometheus metrics** endpoints on all services
- **Health checks** on every service, used by Docker Compose for dependency ordering

### Testing
- **506 .NET tests** across 8 test projects (unit, integration)
- **100 Playwright isolation tests** (real browser, mocked API) across 20 spec files
- **50 Playwright E2E tests** (real browser, full running stack) across 14 spec files
- **OpenAPI and AsyncAPI contract drift tests** -- bidirectional comparison of runtime output against checked-in YAML specs
- **Hand-written test doubles** (no mocking frameworks) for all port interfaces

### DevOps
- **Docker Compose** orchestrating 11 services with health checks and dependency ordering
- **GitHub Actions CI** running on every PR: dotnet restore/build/test + npm ci/lint/build
- **Environment validation** script (`scripts/check-env.sh`) for required variables
- **Seed data** with 6 users, 8 templates, 216 cases, 400+ documents across 2 tenants

---

## Missing Items

### Production Readiness
- **Real embedding model** -- MockEmbeddingAdapter generates deterministic vectors from SHA-256 hashes, not semantic embeddings. Needs OpenAI text-embedding-ada-002 or a local sentence-transformers model.
- **Cloud file storage** -- LocalFileStorageAdapter writes to the local filesystem. Needs Azure Blob Storage or S3 for production.
- **LLM-based case summaries** -- TemplateSummaryAdapter uses string templates. Needs an LLM to generate meaningful case narrative summaries.
- **Rate limiting** -- No rate limiting on API endpoints. Should add middleware or use a reverse proxy.
- **HTTPS** -- Docker Compose runs everything over HTTP. Production needs TLS termination (nginx or a load balancer).
- **Database migrations in production** -- EF Core auto-migrates on startup, which is not safe for production. Needs a migration pipeline.

### Features Not Implemented
- **User management UI** -- No admin interface for creating/editing users or managing roles. Users are seeded or created via the register endpoint.
- **Batch operations** -- No bulk upload, bulk review, or bulk status changes.
- **Notifications** -- No email or in-app notifications when documents are ready for review.
- **Document versioning** -- No support for re-uploading or versioning documents.
- **Pagination on all list endpoints** -- Most list endpoints are paginated, but some edge cases may not be.
- **Audit log export** -- Audit trail is viewable but not exportable (CSV, PDF).

### Test Gaps
- **No load/performance tests** -- No k6 or similar load testing to validate throughput under concurrent users.
- **No chaos testing** -- No verification of behavior when services crash mid-processing (e.g., OCR worker dies after consuming but before publishing).
- **Frontend unit test coverage is thin** -- Only ~10 Vitest tests covering auth store and utilities. Component-level unit tests are covered by Playwright isolation tests instead.

---

## Trade-offs

### Mock adapters instead of real AI services
The embedding adapter and case summary adapter are mocks. This was a deliberate choice to focus development time on the architecture, domain model, and integration patterns rather than on external API integration. The port/adapter pattern means swapping in real implementations requires only a new adapter class and a DI registration change -- no business logic changes.

### Single database for the API service
All API data (documents, cases, users, templates, audit logs) lives in one PostgreSQL database. In a production multi-service architecture, you might split these into separate databases per bounded context. For this project, the single database simplifies development and deployment while the code-level separation (repositories, aggregates) keeps the boundaries clean.

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

## AI Tool Usage

All development was done using **Claude Code** (Anthropic's CLI agent, powered by Claude Opus 4.6). The development process was iterative: I would describe what I wanted, Claude would implement it, and I would review the output and request changes.

### How AI was used

**Architecture and design**: I described the high-level requirements (microservices, hexagonal architecture, DDD, multi-tenancy) and Claude translated those into concrete code structures, project layouts, and dependency injection configurations.

**Test-driven development**: Claude followed strict TDD -- writing failing tests first, verifying they fail for the right reason, then writing minimal code to make them pass. The TDD skill plugin enforced this discipline throughout.

**Code generation**: All production code, tests, Docker configuration, CI pipeline, and documentation was generated by Claude based on my direction. I provided requirements and reviewed output.

**Debugging**: When tests failed or Docker services would not start, I described the symptoms and Claude traced through the code to identify root causes. Examples: tracing a Playwright test failure through the axios interceptor chain to find a missing API mock; identifying a unique constraint violation in seed data caused by random name collisions.

**Refactoring**: Claude performed architectural refactoring (e.g., extracting business logic from Infrastructure consumers into Application handlers, moving state-transition logic from Application into Domain aggregates) while keeping all tests green.

**Documentation**: README, architecture.md, handover.md, and this self-assessment were all written by Claude based on the actual codebase state.

### Example prompts

- "Set up the solution structure with hexagonal architecture and DDD patterns"
- "Implement document upload with local file storage and publish a DocumentUploadedEvent"
- "Add multi-tenancy with row-level isolation using EF Core global query filters"
- "Write Playwright isolation tests for the review workflow"
- "The Playwright navigation tests are failing -- the tests are isolated so they should not need a running backend. Investigate why."
- "Refactor FinalizeReviewHandler to use a domain method for the PendingReview-to-InReview auto-transition instead of doing the state check in the application layer"
- "Add 200+ seed data cases with realistic field values"
- "Run the full test suite and fix any failures"

### What worked well
- TDD enforcement caught bugs early and made refactoring safe
- The port/adapter pattern made it easy to swap between mock and real implementations
- Playwright isolation tests provided high-confidence UI testing without needing the full stack
- Claude's ability to trace through multi-layer failures (e.g., missing API mock causing 401 causing auth clear causing redirect) was effective

### What was challenging
- Context window limits required session continuations, which sometimes lost context about in-progress work
- Complex Docker Compose interactions (health checks, volume mounts, service dependencies) required trial and error
- Playwright E2E tests are flaky when the Docker stack is not fully healthy, requiring retries

---

## What Would Change

### If starting over

1. **Start with a monolith, extract services later.** The microservice boundaries are correct, but building three services from day one added complexity to development and testing that was not needed early on. A modular monolith with clear bounded contexts could have been split later when scaling requirements became clear.

2. **Use a real embedding model from the start.** The mock embedding adapter means similarity search results are meaningless. Starting with even a small local model (e.g., all-MiniLM-L6-v2) would have made the RAG feature demonstrably useful rather than architecturally correct but functionally fake.

3. **Add database migration tooling early.** EF Core auto-migration is fine for development, but having a proper migration pipeline (e.g., FluentMigrator or a CI step) from the start would prevent the "works on my machine" problem.

4. **Invest more in frontend unit tests.** The Playwright isolation tests are valuable but slow. A layer of fast React Testing Library tests for individual components would give quicker feedback during frontend development.

5. **Add structured error codes earlier.** The `ApiResponse<T>` envelope with error codes was added midway. Having it from the first endpoint would have avoided retrofitting error handling across the frontend.

---

## README and Architecture Review

### README.md
Reviewed and confirmed accurate as of the current codebase state. Key facts verified:
- Docker prerequisites, quick start steps, and Loki driver installation are correct
- Service URLs and port mappings match docker-compose.yml
- Demo credentials match DevelopmentDbSeeder
- Seed data counts (6 users, 8 templates, 216 cases, 400+ documents) match the seeder implementation
- Test commands and counts are accurate (506 .NET tests, isolation and E2E commands)
- Project structure matches the actual directory layout
- CI description matches the GitHub Actions workflow

### architecture.md
Reviewed and confirmed accurate as of the current codebase state. Key facts verified:
- System architecture diagram correctly shows all services and their connections
- UML component diagram reflects actual class names and relationships
- Document processing flow matches the real message flow through RabbitMQ
- Hexagonal architecture description matches the actual layer structure
- State machine transitions match IntakeDocument aggregate methods
- Multi-tenancy implementation details (global query filters, tenant resolution middleware, vector store isolation) are accurate
- Messaging topology (queue names, retry policy, message contracts) matches MassTransit configuration
- Real vs mocked services table accurately reflects current adapter implementations
- Testing pyramid counts are accurate (506 backend, 100 isolation, 50 E2E)
- Architecture decision rationales accurately describe the actual design choices

One minor note: the architecture.md references "~491" backend tests in the testing pyramid text but the actual count is 506. The table below it says "~491 across 8 test projects" and should be updated to 506. Fixing this now.
