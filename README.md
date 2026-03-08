# Intake Document Processor

A microservices-based system for processing handwritten intake documents using OCR, field extraction, and vector search. Built with .NET 9, React 19, and Docker.

## Architecture

Three microservices communicating via RabbitMQ (MassTransit):

- **API Service** -- REST API, document upload, tenant management, JWT authentication
- **OCR Worker Service** -- Consumes uploaded documents, runs Tesseract OCR, extracts fields
- **RAG Service** -- Generates vector embeddings, stores in Qdrant for semantic search

Infrastructure: PostgreSQL, RabbitMQ, Qdrant (vector DB), Grafana + Prometheus + Loki + Tempo (observability).

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)

That is it. Everything runs in containers. No local .NET SDK or Node.js required to run the application.

For local development (running tests, debugging outside Docker), you will also need:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/wfhanna1/ai-case-management-system.git
cd ai-case-management-system
```

### 2. Create your `.env` file

```bash
cp .env.example .env
```

Open `.env` and set passwords. At minimum, change `POSTGRES_PASSWORD` and `JWT_SECRET`. The defaults will work for local development but are not secure.

### 3. Install the Loki Docker logging driver (first time only)

**Intel (amd64) / Windows (Docker Desktop):**
```bash
docker plugin install grafana/loki-docker-driver:3.6.0 --alias loki --grant-all-permissions
```

**Apple Silicon / ARM64:**
```bash
docker plugin install grafana/loki-docker-driver:3.6.0-arm64 --alias loki --grant-all-permissions
```

**Windows note:** Docker Desktop for Windows runs a Linux VM, so the amd64 plugin works. Native Windows containers are not supported.

**Linux users:** Set `LOKI_DRIVER_URL=http://localhost:3100/loki/api/v1/push` in `.env`. The default `host.docker.internal` only resolves on Docker Desktop (macOS/Windows).

### 4. Start all services

```bash
docker compose up --build
```

First build takes a few minutes. Subsequent starts are faster due to layer caching.

During the first build, the `bge-micro-v2` embedding model (~23 MB) is downloaded from Hugging Face and bundled into the RAG service container image. This is automatic and only happens once; subsequent builds use the cached layer.

**Re-seeding embeddings:** If you need to regenerate the Qdrant vector data (for example, after changing the embedding provider), wipe all volumes and rebuild:

```bash
docker compose down -v && docker compose up --build
```

### 5. Verify

Wait for all services to report healthy (about 30 seconds after initial build):

```bash
docker compose ps
```

All services should show `healthy` status. Then open:

- **Frontend:** http://localhost:3000
- **API health check:** http://localhost:5003/health

## Services and URLs

| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:3000 | React SPA served by nginx |
| API | http://localhost:5003 | REST API (Swagger at `/swagger`) |
| RabbitMQ Management | http://localhost:15672 | Username/password from `.env` |
| PostgreSQL | localhost:5432 | Connection details in `.env` |
| Qdrant Dashboard | http://localhost:6333/dashboard | Vector database UI |
| Grafana | http://localhost:3001 | Default login: admin / admin |
| Prometheus | http://localhost:9090 | Metrics |

## Demo Credentials

The system automatically seeds demo data on first startup in Development mode. All demo users share the password **`Demo123!`**.

| Email | Role | Tenant | What they can do |
|-------|------|--------|-----------------|
| admin@alpha.demo | Admin | Alpha Clinic | Upload, review, manage all |
| worker@alpha.demo | IntakeWorker | Alpha Clinic | Upload documents |
| reviewer@alpha.demo | Reviewer | Alpha Clinic | Review and finalize documents |
| admin@beta.demo | Admin | Beta Hospital | Upload, review, manage all |
| worker@beta.demo | IntakeWorker | Beta Hospital | Upload documents |
| reviewer@beta.demo | Reviewer | Beta Hospital | Review and finalize documents |

## Seed Data

On first startup, the seeder creates:

- **6 demo users** (3 per tenant, one per role)
- **8 form templates** (4 types per tenant: Child Welfare, Adult Protective, Housing Assistance, Mental Health Referral)
- **216 cases** with 400+ documents across both tenants, in various statuses (PendingReview, InReview, Finalized)
- Documents include extracted fields (client name, date of birth, case number, address, form type)

Data is only seeded once. To re-seed, delete the PostgreSQL volume:

```bash
docker compose down -v
docker compose up --build
```

## Walkthrough: Full Document Lifecycle

1. **Login** at http://localhost:3000 as `worker@alpha.demo` / `Demo123!`
2. **View templates** -- click Templates in the nav bar. Print or download any template.
3. **Upload a document** -- click Upload, select a template, choose an image or PDF file.
4. **View documents** -- click Documents to see the uploaded file. Status starts as "Submitted", then transitions to "Processing" and "PendingReview" as the OCR worker processes it.
5. **Review a document** -- log out and log in as `reviewer@alpha.demo`. Click Reviews to see documents awaiting review. Click a document to see extracted fields, correct any errors, then click Finalize.
6. **Check the dashboard** -- click Dashboard to see stats (pending reviews, processed today) and recent activity feed.
7. **Search and cases** -- use the Search page for full-text search across documents. The Cases page shows documents grouped by subject name.

## Local Development (without Docker)

### Backend

You need PostgreSQL and RabbitMQ running locally (or use the Docker services and just run the API natively).

```bash
dotnet build                    # Build all projects
dotnet test                     # Run all tests (~506 tests)
dotnet run --project src/ApiService/Api.WebApi  # Run API on port 5003
```

### Frontend

```bash
cd src/ClientApp
npm install
npm run dev       # Vite dev server on port 3000 (proxies /api to localhost:5003)
npm run lint      # ESLint (max-warnings 0)
npm run build     # Production build
```

### Running Tests

```bash
# .NET unit and integration tests
dotnet test

# Frontend lint + type check
cd src/ClientApp && npm run lint && npm run build

# Playwright isolation tests (mocked API, no running stack needed)
cd src/ClientApp && npx playwright test e2e/isolation/

# Playwright E2E tests (requires full Docker stack running)
cd src/ClientApp && npx playwright test --ignore-snapshots --grep-invert "isolation"
```

## Project Structure

```
src/
  ApiService/           # REST API microservice (hexagonal architecture)
    Api.Domain/         # Entities, value objects, port interfaces
    Api.Application/    # Use cases, commands, queries
    Api.Infrastructure/ # EF Core, MassTransit adapters
    Api.WebApi/         # Controllers, Program.cs, DI
  OcrWorkerService/     # OCR processing worker
  RagService/           # Embedding + vector search service
  SharedKernel/         # Base types (Entity, ValueObject, Result<T>)
  Messaging/            # Shared message contracts
  ClientApp/            # React 19 + TypeScript + Vite frontend
contracts/              # OpenAPI and AsyncAPI specs
tests/                  # xUnit test projects + Playwright tests
```

## CI

GitHub Actions runs on every PR to main:
- Backend: restore, build, test (all .NET projects)
- Frontend: npm ci, ESLint, Vite build

## Troubleshooting

**Services not starting?** Check `docker compose logs <service-name>` for errors. Common issues:
- Missing `.env` file -- run `cp .env.example .env`
- Missing Loki driver -- install with the command in step 3
- Port conflicts -- change ports in `.env` (e.g., `API_HOST_PORT=5050`)

**Database schema issues?** EF Core migrations run automatically on startup. If you see migration errors, try a clean volume:
```bash
docker compose down -v
docker compose up --build
```

**OCR not extracting text?** Tesseract works best with printed text. Handwritten text extraction is limited. Reviewers can manually correct extracted fields.
