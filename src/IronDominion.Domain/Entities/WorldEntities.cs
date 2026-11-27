using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed class WorldEntities
{
    private readonly Dictionary<EntityId, WorldEntity> _entities = new();
    private int _lastId;

    public WorldEntities(IEnumerable<WorldEntity> entities, IEnumerable<CoverZone>? coverZones = null,
        ConstructionLayout? construction = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        Construction = construction;
        CoverZones = coverZones?.ToImmutableArray() ?? [];

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
    public ImmutableArray<CoverZone> CoverZones { get; }

    public ConstructionLayout? Construction { get; }

    public WorldEntity? Find(EntityId id) => _entities.GetValueOrDefault(id);

    public WorldEntities Copy() => new(_entities.Values, CoverZones, Construction);

    public Building SpawnBuilding(BuildingPlacement placement, int hitPoints, SimVector? exit)
    {
        var id = new EntityId(checked(_lastId + 1));
        var building = new Building(id, placement.Owner, placement.Position, placement.Kind, hitPoints, exit, placement);
        AddSpawn(building);
        return building;
    }

    public Unit SpawnUnit(PlayerId owner, SimVector position, UnitKind kind, int hitPoints)
    {
        var id = new EntityId(checked(_lastId + 1));
        var unit = new Unit(id, owner, position, kind, hitPoints);
        AddSpawn(unit);
        return unit;
    }

    private void AddSpawn(WorldEntity entity)
    {
        _entities.Add(entity.Id, entity);
        _lastId = entity.Id.Value;
    }
}
