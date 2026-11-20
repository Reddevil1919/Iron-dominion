namespace IronDominion.Domain;

public sealed class WorldEntities
{
    private readonly Dictionary<EntityId, WorldEntity> _entities = new();

    public WorldEntities(IEnumerable<WorldEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (!_entities.TryAdd(entity.Id, entity))
                throw new ArgumentException($"Duplicate entity ID {entity.Id.Value}.", nameof(entities));
        }
    }

    public WorldEntity? Find(EntityId id) => _entities.GetValueOrDefault(id);
}
