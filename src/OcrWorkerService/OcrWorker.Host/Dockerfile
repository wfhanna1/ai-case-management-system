# syntax=docker/dockerfile:1

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /repo

COPY IntakeDocumentProcessor.sln ./

COPY src/SharedKernel/SharedKernel.csproj                                     src/SharedKernel/
COPY src/ApiService/Api.Domain/Api.Domain.csproj                               src/ApiService/Api.Domain/
COPY src/ApiService/Api.Application/Api.Application.csproj                     src/ApiService/Api.Application/
COPY src/ApiService/Api.Infrastructure/Api.Infrastructure.csproj               src/ApiService/Api.Infrastructure/
COPY src/ApiService/Api.WebApi/Api.WebApi.csproj                               src/ApiService/Api.WebApi/
COPY src/OcrWorkerService/OcrWorker.Domain/OcrWorker.Domain.csproj             src/OcrWorkerService/OcrWorker.Domain/
COPY src/OcrWorkerService/OcrWorker.Application/OcrWorker.Application.csproj   src/OcrWorkerService/OcrWorker.Application/
COPY src/OcrWorkerService/OcrWorker.Infrastructure/OcrWorker.Infrastructure.csproj src/OcrWorkerService/OcrWorker.Infrastructure/
COPY src/OcrWorkerService/OcrWorker.Host/OcrWorker.Host.csproj                 src/OcrWorkerService/OcrWorker.Host/
COPY src/RagService/RagService.Domain/RagService.Domain.csproj                 src/RagService/RagService.Domain/
COPY src/RagService/RagService.Application/RagService.Application.csproj       src/RagService/RagService.Application/
COPY src/RagService/RagService.Infrastructure/RagService.Infrastructure.csproj src/RagService/RagService.Infrastructure/
COPY src/RagService/RagService.Host/RagService.Host.csproj                     src/RagService/RagService.Host/
COPY tests/SharedKernel.Tests/SharedKernel.Tests.csproj                        tests/SharedKernel.Tests/
COPY tests/Api.Domain.Tests/Api.Domain.Tests.csproj                            tests/Api.Domain.Tests/
COPY tests/Api.Application.Tests/Api.Application.Tests.csproj                  tests/Api.Application.Tests/
COPY tests/Api.Infrastructure.Tests/Api.Infrastructure.Tests.csproj            tests/Api.Infrastructure.Tests/
COPY tests/Api.WebApi.Tests/Api.WebApi.Tests.csproj                            tests/Api.WebApi.Tests/
COPY tests/OcrWorker.Tests/OcrWorker.Tests.csproj                              tests/OcrWorker.Tests/
COPY tests/RagService.Tests/RagService.Tests.csproj                            tests/RagService.Tests/

RUN dotnet restore src/OcrWorkerService/OcrWorker.Host/OcrWorker.Host.csproj

COPY src/ src/
RUN dotnet publish src/OcrWorkerService/OcrWorker.Host/OcrWorker.Host.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "OcrWorker.Host.dll"]
