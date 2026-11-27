using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class UnitProduction(
    WorldEntities world,
    GameConfiguration configuration,
    INavigationService navigation,
    UnitMovement movement,
    UnitCombat combat,
    PlayerEconomy economy)
{
    private const int GroundMask = 1;
    private const float SpawnSeparationSquared = 9.5f * 9.5f;
    private static readonly CommandResult Accepted = new(true, null);

    private readonly Dictionary<UnitKind, UnitDefinition> _definitions =
        configuration.Units.ToDictionary(unit => unit.Kind);

    public ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>> Queues { get; private set; } =
        ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>>.Empty;

    public void Restore(ImmutableArray<BuildingCheckpoint> buildings) =>
        Queues = buildings.Where(building => !building.Queue.IsEmpty)
            .ToImmutableDictionary(building => building.Id, building => building.Queue.ToImmutableList());

    public CommandResult Enqueue(TrainUnitCommand command)
    {
        var producer = (Building)world.Find(command.Producer)!;
        if (!producer.ProductionExit.HasValue || !producer.CanTrain(command.Kind)
            || !_definitions.TryGetValue(command.Kind, out var definition))
            return new(false, "invalid_production_type");

        if (!economy.TrySpend(command.Player, definition.Cost))
            return new(false, "insufficient_credits");

        var queue = Queues.GetValueOrDefault(producer.Id, ImmutableList<TrainingOrder>.Empty);
        Queues = Queues.SetItem(producer.Id, queue.Add(new TrainingOrder(command.Kind, definition.BuildTicks)));
        return Accepted;
    }

    public void Advance(long tick, List<IGameEvent> events)
    {
        foreach (var id in Queues.Keys.OrderBy(id => id.Value).ToArray())
        {
            var producer = (Building)world.Find(id)!;
            var queue = Queues[id];

            if (combat.Health[producer.Id] == 0)
            {
                Queues = Queues.Remove(producer.Id);
                continue;
            }

            var order = queue[0];
            if (order.RemainingTicks > 0)
            {
                order = order with { RemainingTicks = order.RemainingTicks - 1 };
                queue = queue.SetItem(0, order);
                Queues = Queues.SetItem(producer.Id, queue);
            }

            var exit = producer.ProductionExit!.Value;
            if (order.RemainingTicks > 0 || !ExitIsFree(exit))
                continue;

            var definition = _definitions[order.Kind];
            var unit = world.SpawnUnit(producer.Owner!.Value, exit, order.Kind, definition.MaxHp);
            movement.Register(unit);
            combat.Register(unit);
            events.Add(new UnitSpawned(tick, unit));
            Queues = queue.Count == 1
                ? Queues.Remove(producer.Id)
                : Queues.SetItem(producer.Id, queue.RemoveAt(0));
        }
    }

    private bool ExitIsFree(SimVector exit)
    {
        foreach (var (id, position) in movement.Positions)
        {
            var x = position.X - exit.X;
            var z = position.Z - exit.Z;
            if (combat.Health[id] > 0 && x * x + z * z < SpawnSeparationSquared)
                return false;
        }

        return navigation.FindPath(exit, exit, GroundMask).Count > 0;
    }
}
