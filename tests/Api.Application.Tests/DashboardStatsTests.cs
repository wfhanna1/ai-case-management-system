using Api.Application.Queries;

namespace Api.Application.Tests;

public sealed class DashboardStatsTests
{
    [Fact]
    public void Create_sets_all_properties()
    {
        var stats = DashboardStats.Create(10, 3, 2, TimeSpan.FromMinutes(45));

        Assert.Equal(10, stats.TotalCases);
        Assert.Equal(3, stats.PendingReview);
        Assert.Equal(2, stats.ProcessedToday);
        Assert.Equal(TimeSpan.FromMinutes(45), stats.AverageProcessingTime);
    }

    [Fact]
    public void Create_clamps_negative_values_to_zero()
    {
        var stats = DashboardStats.Create(-5, -1, -3, TimeSpan.FromMinutes(-10));

        Assert.Equal(0, stats.TotalCases);
        Assert.Equal(0, stats.PendingReview);
        Assert.Equal(0, stats.ProcessedToday);
        Assert.Equal(TimeSpan.Zero, stats.AverageProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_returns_dash_for_zero()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.Zero);

        Assert.Equal("--", stats.FormattedProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_returns_minutes_only_when_under_one_hour()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.FromMinutes(45));

        Assert.Equal("45m", stats.FormattedProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_returns_hours_and_minutes_when_over_one_hour()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.FromMinutes(90));

        Assert.Equal("1h 30m", stats.FormattedProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_returns_exact_hours_with_zero_minutes()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal("2h 0m", stats.FormattedProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_truncates_seconds()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.FromSeconds(90));

        Assert.Equal("1m", stats.FormattedProcessingTime);
    }

    [Fact]
    public void FormattedProcessingTime_returns_zero_minutes_for_sub_minute()
    {
        var stats = DashboardStats.Create(0, 0, 0, TimeSpan.FromSeconds(30));

        Assert.Equal("0m", stats.FormattedProcessingTime);
    }

    [Fact]
    public void Equality_holds_for_same_values()
    {
        var a = DashboardStats.Create(10, 3, 2, TimeSpan.FromMinutes(45));
        var b = DashboardStats.Create(10, 3, 2, TimeSpan.FromMinutes(45));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_fails_for_different_values()
    {
        var a = DashboardStats.Create(10, 3, 2, TimeSpan.FromMinutes(45));
        var b = DashboardStats.Create(10, 3, 2, TimeSpan.FromMinutes(50));

        Assert.NotEqual(a, b);
    }
}
