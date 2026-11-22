namespace IronDominion.Domain;

public sealed record SuppressionRules
{
    public SuppressionRules(int durationTicks, decimal movementMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationTicks);
        if (movementMultiplier is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(movementMultiplier), "The movement multiplier must be above zero and at most one.");

        DurationTicks = durationTicks;
        MovementMultiplier = movementMultiplier;
    }

    public int DurationTicks { get; }
    public decimal MovementMultiplier { get; }
}
