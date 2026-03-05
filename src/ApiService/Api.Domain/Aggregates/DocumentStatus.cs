namespace Api.Domain.Aggregates;

/// <summary>
/// Processing lifecycle states for an intake document.
/// </summary>
public enum DocumentStatus
{
    Submitted = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
