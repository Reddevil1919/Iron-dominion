namespace IronDominion.Domain;

public sealed class Player
{
    public Player(PlayerId id, int credits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegative(credits);

        Id = id;
        Credits = credits;
    }

    public PlayerId Id { get; }
    public int Credits { get; }
}
