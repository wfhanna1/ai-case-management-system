namespace SharedKernel;

/// <summary>
/// Base class for all domain entities. Equality is determined by identity (Id), not by value.
/// </summary>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; }

    protected Entity(TId id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (obj.GetType() != GetType()) return false;
        if (obj is not Entity<TId> other) return false;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
