namespace SharedKernel;

/// <summary>
/// Represents the absence of a meaningful return value in a Result.
/// Equivalent to void but usable as a generic type argument.
/// </summary>
public sealed class Unit
{
    public static readonly Unit Value = new();
    private Unit() { }
}
