using Api.Application.Queries;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class GetDashboardStatsHandlerTests
{
    private readonly StubDocumentRepository _documentRepo = new();
    private readonly StubCaseRepository _caseRepo = new();
    private readonly GetDashboardStatsHandler _handler;

    public GetDashboardStatsHandlerTests()
    {
        _handler = new GetDashboardStatsHandler(_documentRepo, _caseRepo);
    }

    [Fact]
    public async Task HandleAsync_returns_stats_from_repositories()
    {
        _caseRepo.CaseCount = 12;
        _documentRepo.Stats = (5, 3, TimeSpan.FromMinutes(42));

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.TotalCases);
        Assert.Equal(5, result.Value.PendingReview);
        Assert.Equal(3, result.Value.ProcessedToday);
        Assert.Equal("42m", result.Value.AverageProcessingTime);
    }

    [Fact]
    public async Task HandleAsync_formats_hours_and_minutes()
    {
        _caseRepo.CaseCount = 0;
        _documentRepo.Stats = (0, 0, TimeSpan.FromMinutes(125));

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2h 5m", result.Value.AverageProcessingTime);
    }

    [Fact]
    public async Task HandleAsync_zero_processing_time_shows_dash()
    {
        _caseRepo.CaseCount = 0;
        _documentRepo.Stats = (0, 0, TimeSpan.Zero);

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("--", result.Value.AverageProcessingTime);
    }

    [Fact]
    public async Task HandleAsync_document_repo_failure_returns_error()
    {
        _documentRepo.StatsResult = Result<(int, int, TimeSpan)>.Failure(new Error("DB_ERROR", "timeout"));
        _caseRepo.CaseCount = 0;

        var result = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    private sealed class StubDocumentRepository : IDocumentRepository
    {
        public (int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime) Stats { get; set; }
        public Result<(int, int, TimeSpan)>? StatsResult { get; set; }

        public Task<Result<(int PendingReview, int ProcessedToday, TimeSpan AverageProcessingTime)>> GetStatsAsync(
            TenantId tenantId, CancellationToken ct = default)
        {
            if (StatsResult is { } r)
                return Task.FromResult(r);
            return Task.FromResult(Result<(int, int, TimeSpan)>.Success(Stats));
        }

        public Task<Result<IntakeDocument?>> FindByIdAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IntakeDocument?>> FindByIdUnfilteredAsync(DocumentId id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<IReadOnlyList<IntakeDocument>>> ListByStatusAsync(TenantId tenantId, DocumentStatus status, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> ListByStatusesAsync(TenantId tenantId, IReadOnlyList<DocumentStatus> statuses, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(IntakeDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<IntakeDocument> Items, int TotalCount)>> SearchAsync(
            TenantId tenantId, string? fileNameContains, DocumentStatus? status,
            DateTimeOffset? submittedAfter, DateTimeOffset? submittedBefore,
            string? extractedFieldContains, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class StubCaseRepository : ICaseRepository
    {
        public int CaseCount { get; set; }

        public Task<Result<int>> CountByTenantAsync(TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<int>.Success(CaseCount));

        public Task<Result<Case?>> FindByIdAsync(CaseId id, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Case?>> FindBySubjectNameAsync(string subjectName, TenantId tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(TenantId tenantId, string? query, DocumentStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
