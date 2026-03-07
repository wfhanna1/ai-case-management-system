# Contract-First API Design

Date: 2026-03-07
Branch: feature/swagger-docs (builds on existing Swagger/OpenAPI work from issue #31)

## Decision

Adopt contract-first API development. YAML spec files checked into the repo are the source of truth for all service boundaries. Runtime output is validated against these specs in CI; any drift fails the build.

## File Layout

```
contracts/
  api-service.openapi.yaml       # OpenAPI 3.0.3 -- ApiService (18 REST endpoints)
  rag-service.openapi.yaml       # OpenAPI 3.0.3 -- RagService (2 REST endpoints)
  messaging.asyncapi.yaml        # AsyncAPI 2.6.0 -- all MassTransit events (4 events)
```

OcrWorkerService has no HTTP API (only a health check), so no OpenAPI spec. Its events are covered in the shared AsyncAPI spec.

## Spec Versions

- OpenAPI 3.0.3: matches Swashbuckle output, broad tooling support.
- AsyncAPI 2.6.0: stable, supports AMQP protocol binding for RabbitMQ.
- YAML format for readability and cleaner PR diffs.

## Enforcement: Test-Time Validation

No code generation. Hand-written C# types remain the implementation. Tests validate that runtime behavior matches the checked-in specs.

### OpenAPI Validation

Extend the existing `SwaggerContractTests` pattern:
- Boot each service via `WebApplicationFactory`
- Fetch the generated `/swagger/v1/swagger.json` at runtime
- Parse and compare against the checked-in YAML spec
- Assert: all paths, methods, request/response schemas, and status codes match
- Any divergence fails the test

### AsyncAPI Validation

- Reflection-based tests verify each message contract record matches the schemas defined in `messaging.asyncapi.yaml`
- Property names, types, required fields, and channel/operation bindings are checked
- Extends the existing `ContractSerializationTests` pattern

### CI Integration

Existing CI (`dotnet test`) runs these tests on every PR. No new pipeline steps needed.

## Scope

### api-service.openapi.yaml (18 endpoints)

- POST/GET /api/auth/register, login, refresh
- POST/GET /api/documents, GET /api/documents/{id}, GET /api/documents/{id}/file, GET /api/documents/search, GET /api/documents/stats
- GET /api/cases, GET /api/cases/{id}, GET /api/cases/search
- POST/GET /api/form-templates, GET /api/form-templates/{id}
- GET /api/reviews/pending, GET /api/reviews/{documentId}, POST /api/reviews/{documentId}/start, POST /api/reviews/{documentId}/correct-field, POST /api/reviews/{documentId}/finalize, GET /api/reviews/{documentId}/audit, GET /api/reviews/{documentId}/similar-cases

### rag-service.openapi.yaml (2 endpoints)

- GET /api/similar?documentId=&tenantId=&topK=
- POST /api/similar-by-text

### messaging.asyncapi.yaml (4 events)

| Event | Publisher | Subscriber |
|-------|-----------|------------|
| DocumentUploadedEvent | ApiService | OcrWorkerService |
| DocumentProcessedEvent | OcrWorkerService | ApiService |
| EmbeddingRequestedEvent | ApiService | RagService |
| EmbeddingCompletedEvent | RagService | (none currently) |

## What This Does Not Include

- No code generation from specs
- No HTTP client generation
- No AsyncAPI tooling beyond test validation
- OcrWorkerService health endpoint is not spec'd (infrastructure, not a business contract)
