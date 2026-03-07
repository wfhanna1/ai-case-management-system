namespace Api.Application.DTOs;

public sealed record DashboardStatsDto(
    int TotalCases,
    int PendingReview,
    int ProcessedToday,
    string AverageProcessingTime);
