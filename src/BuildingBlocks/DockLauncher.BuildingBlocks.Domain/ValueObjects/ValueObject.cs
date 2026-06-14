namespace DockLauncher.BuildingBlocks.Domain.ValueObjects;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other || GetType() != obj.GetType())
        {
            return false;
        }

        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(17, (current, value) => current * 31 + (value?.GetHashCode() ?? 0));
    }
}