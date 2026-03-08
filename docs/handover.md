# Handover Instructions

This document covers everything needed to take over development of the Intake Document Processor system.

## Prerequisites

- Docker Desktop (macOS/Windows) or Docker Engine + Compose v2 (Linux)
- Git

No .NET SDK or Node.js is required to run the application. Everything runs in containers.

For local development (tests, debugging), you will also need:
- .NET 9 SDK
- Node.js 20+

## Environment Configuration

**The `.env` file is mandatory.** Docker Compose will fail to start without it because database passwords and JWT secrets have no default values.

### Setup

```bash
cp .env.example .env
```

Open `.env` and change at minimum:

| Variable | What to set | Why |
|----------|------------|-----|
| `POSTGRES_PASSWORD` | A strong password | PostgreSQL database access |
| `RABBITMQ_DEFAULT_PASS` | A strong password | RabbitMQ message broker access |
| `JWT_SECRET` | A random string, 32+ characters | Signs JWT authentication tokens |
| `GRAFANA_ADMIN_PASSWORD` | A password for Grafana | Observability dashboard access |

All other variables have sensible defaults. See `.env.example` for full documentation of every variable.

### .env.example Reference

The `.env.example` file is checked into the repository at the project root. It contains all available configuration variables with comments explaining each one. Key sections:

- **PostgreSQL** -- database name, user, password, host port
- **RabbitMQ** -- AMQP credentials and management UI port
- **Qdrant** -- vector database HTTP and gRPC ports
- **JWT** -- token signing secret
- **Service ports** -- host port mappings for API (5003), frontend (3000), OCR worker (5001), RAG service (5002)
- **Observability** -- Prometheus (9090), Grafana (3001), Loki (3100), Tempo (3200)
- **Loki driver URL** -- differs between Docker Desktop and Linux (see comments in file)

### Port Conflicts

If any default port is already in use on your machine, change it in `.env`. For example, if port 5432 is taken by a local PostgreSQL:

```bash
POSTGRES_HOST_PORT=5433
```

### Linux-Specific Configuration

Docker Desktop (macOS/Windows) resolves `host.docker.internal` automatically. On Linux with Docker Engine, set:

```bash
LOKI_DRIVER_URL=http://localhost:3100/loki/api/v1/push
```

## First-Time Setup

```bash
# 1. Clone
git clone https://github.com/wfhanna1/ai-case-management-system.git
cd ai-case-management-system

# 2. Create .env
cp .env.example .env
# Edit .env -- set passwords and JWT secret

# 3. Install the Loki logging driver (one-time)
# Intel (amd64):
docker plugin install grafana/loki-docker-driver:3.6.0 --alias loki --grant-all-permissions
# Apple Silicon / ARM64:
# docker plugin install grafana/loki-docker-driver:3.6.0-arm64 --alias loki --grant-all-permissions

# 4. Start everything
docker compose up --build
```

First build takes several minutes. Wait for all services to report healthy:

```bash
docker compose ps
```

## Demo Data

On first startup in Development mode, the system automatically seeds:

- **6 users** across 2 tenants (Alpha Clinic, Beta Hospital) -- one Admin, one IntakeWorker, one Reviewer each
- **8 form templates** (4 types per tenant)
- **216 cases** with 400+ documents at various lifecycle stages

All demo users share the password `Demo123!`. See the README for the full credential table.

To re-seed from scratch:

```bash
docker compose down -v    # removes all volumes including database
docker compose up --build
```

## Service Architecture

| Service | Internal Port | External Port | Health Check |
|---------|--------------|---------------|-------------|
| API | 8080 | 5003 | /health |
| Frontend | 80 | 3000 | curl localhost |
| OCR Worker | 8080 | 5001 | /health |
| RAG Service | 8080 | 5002 | /health |
| PostgreSQL | 5432 | 5432 | pg_isready |
| RabbitMQ | 5672 / 15672 | 5672 / 15672 | rabbitmq-diagnostics |
| Qdrant | 6333 / 6334 | 6333 / 6334 | HTTP /healthz |
| Grafana | 3000 | 3001 | curl |
| Prometheus | 9090 | 9090 | -- |
| Loki | 3100 | 3100 | /ready |
| Tempo | 3200 / 4317 | 3200 | /ready |

## Running Tests

```bash
# .NET unit + integration tests (506 tests)
dotnet test

# Frontend lint + build
cd src/ClientApp && npm run lint && npm run build

# Playwright isolation tests (mocked API, 100 tests)
cd src/ClientApp && npx playwright test e2e/isolation/

# Playwright E2E tests (requires running Docker stack, 49 tests)
cd src/ClientApp && npx playwright test --ignore-snapshots --grep-invert "isolation"
```

## Key Files and Directories

| Path | Purpose |
|------|---------|
| `.env.example` | Template for required environment variables |
| `docker-compose.yml` | All service definitions and dependencies |
| `CLAUDE.md` | AI assistant instructions and project conventions |
| `docs/architecture.md` | System architecture documentation |
| `contracts/` | OpenAPI and AsyncAPI contract specs |
| `src/ApiService/` | Main REST API (hexagonal architecture) |
| `src/OcrWorkerService/` | OCR processing worker |
| `src/RagService/` | Vector embedding and search service |
| `src/ClientApp/` | React 19 + TypeScript frontend |
| `tests/` | All test projects |

## Common Operations

**View logs for a specific service:**
```bash
docker compose logs -f api
```

**Restart a single service after code changes:**
```bash
docker compose up --build -d api
```

**Reset everything to clean state:**
```bash
docker compose down -v
docker compose up --build
```

**Access the database directly:**
```bash
docker compose exec postgres psql -U postgres -d intake_processor
```

**Access RabbitMQ management UI:**
Open http://localhost:15672 with the credentials from your `.env` file.
