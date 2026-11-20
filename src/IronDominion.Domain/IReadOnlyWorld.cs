namespace IronDominion.Domain;

public interface IReadOnlyWorld
{
    long Tick { get; }
    bool TryGetUnitPosition(EntityId id, out SimVector position);
}
