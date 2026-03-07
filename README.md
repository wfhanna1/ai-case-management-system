# Intake Document Processor

A microservices-based system for processing handwritten intake documents using OCR, field extraction, and vector search. Built with .NET 9, React 19, and Docker.

## Architecture

Three microservices communicating via RabbitMQ (MassTransit):

- **API Service** -- REST API, document upload, tenant management
- **OCR Worker Service** -- Consumes uploaded documents, runs OCR, extracts fields
- **RAG Service** -- Generates vector embeddings, stores in Qdrant for semantic search

Infrastructure: PostgreSQL, RabbitMQ, Qdrant (vector DB).

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (for local development)
- [Node.js 20+](https://nodejs.org/) (for frontend development)

## Getting Started

### 1. Create your `.env` file

```bash
cp .env.example .env
```

Edit `.env` and set passwords for PostgreSQL and RabbitMQ. The `.env.example` file documents all available configuration variables. **The `.env` file is required** -- Docker Compose will not start without it because passwords have no default values.

### 2. Start all services

```bash
docker compose up --build
```

This starts all 7 services:

| Service | URL |
|---------|-----|
| API | http://localhost:5003 |
| Frontend | http://localhost:3000 |
| RabbitMQ Management | http://localhost:15672 |
| PostgreSQL | localhost:5432 |
| Qdrant | http://localhost:6333 |

### 3. Verify

- API health check: `curl http://localhost:5003/health`
- Frontend: open http://localhost:3000 in a browser
- RabbitMQ UI: open http://localhost:15672 (credentials from your `.env`)

## Local Development (without Docker)

### Backend

```bash
dotnet build                    # Build all projects
dotnet test                     # Run all tests (50 tests)
dotnet run --project src/ApiService/Api.WebApi  # Run API on port 5003
```

### Frontend

```bash
cd src/ClientApp
npm install
npm run dev       # Vite dev server on port 3000
npm run lint      # ESLint
npm run build     # Production build
```

## Project Structure

```
src/
  ApiService/           # REST API microservice (hexagonal architecture)
    Api.Domain/         # Entities, value objects, port interfaces
    Api.Application/    # Use cases, commands, queries
    Api.Infrastructure/ # EF Core, MassTransit adapters
    Api.WebApi/         # Controllers, Program.cs
  OcrWorkerService/     # OCR processing worker
  RagService/           # Embedding + vector search service
  SharedKernel/         # Base types (Entity, ValueObject, Result<T>, TenantId)
  Messaging/            # Shared message contracts
  ClientApp/            # React 19 + TypeScript + Vite frontend
tests/                  # xUnit test projects
```

## CI

GitHub Actions runs on every PR to main:
- Backend: restore, build, test (all .NET projects)
- Frontend: npm ci, ESLint, Vite build
