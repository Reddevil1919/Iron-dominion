namespace IronDominion.Domain;

public readonly record struct EntityId
{
    public EntityId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }
}
