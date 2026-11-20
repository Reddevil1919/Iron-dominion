namespace IronDominion.Domain;

public abstract class WorldEntity
{
    private protected WorldEntity(EntityId id, PlayerId? owner, SimVector position)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id.Value, nameof(id));
        if (owner is { } player)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(player.Value, nameof(owner));

        Id = id;
        Owner = owner;
        Position = position;
    }

    public EntityId Id { get; }
    public PlayerId? Owner { get; }
    public SimVector Position { get; }
}
