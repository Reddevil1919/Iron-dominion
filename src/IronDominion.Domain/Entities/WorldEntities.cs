namespace IronDominion.Domain;

public sealed class WorldEntities
{
    private readonly Dictionary<EntityId, WorldEntity> _entities = new();
    private int _lastId;

    public WorldEntities(IEnumerable<WorldEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (!_entities.TryAdd(entity.Id, entity))
                throw new ArgumentException($"Duplicate entity ID {entity.Id.Value}.", nameof(entities));
            _lastId = Math.Max(_lastId, entity.Id.Value);
        }
    }

    public IEnumerable<Unit> Units => _entities.Values.OfType<Unit>();
    public IEnumerable<Building> Buildings => _entities.Values.OfType<Building>();
    public IEnumerable<Sector> Sectors => _entities.Values.OfType<Sector>();

    public WorldEntity? Find(EntityId id) => _entities.GetValueOrDefault(id);

    public WorldEntities Copy() => new(_entities.Values);

    public Unit SpawnUnit(PlayerId owner, SimVector position, UnitKind kind, int hitPoints)
    {
        var id = new EntityId(checked(_lastId + 1));
        var unit = new Unit(id, owner, position, kind, hitPoints);
        _entities.Add(id, unit);
        _lastId = id.Value;
        return unit;
    }
}
