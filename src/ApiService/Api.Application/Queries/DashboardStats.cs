using SharedKernel;

namespace Api.Application.Queries;

public sealed class DashboardStats : ValueObject
{
    public int TotalCases { get; }
    public int PendingReview { get; }
    public int ProcessedToday { get; }
    public TimeSpan AverageProcessingTime { get; }

    private DashboardStats(int totalCases, int pendingReview, int processedToday, TimeSpan averageProcessingTime)
    {
        TotalCases = totalCases;
        PendingReview = pendingReview;
        ProcessedToday = processedToday;
        AverageProcessingTime = averageProcessingTime;
    }

    public static DashboardStats Create(int totalCases, int pendingReview, int processedToday, TimeSpan averageProcessingTime)
    {
        return new DashboardStats(
            totalCases < 0 ? 0 : totalCases,
            pendingReview < 0 ? 0 : pendingReview,
            processedToday < 0 ? 0 : processedToday,
            averageProcessingTime < TimeSpan.Zero ? TimeSpan.Zero : averageProcessingTime);
    }

    public string FormattedProcessingTime
    {
        get
        {
            if (AverageProcessingTime == TimeSpan.Zero)
                return "--";

            var totalMinutes = (int)AverageProcessingTime.TotalMinutes;
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m";

            return $"{minutes}m";
        }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TotalCases;
        yield return PendingReview;
        yield return ProcessedToday;
        yield return AverageProcessingTime;
    }
}
