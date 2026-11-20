namespace IronDominion.Domain;

public readonly record struct PlayerId
{
    public PlayerId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }
}
