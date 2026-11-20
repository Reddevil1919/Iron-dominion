using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

public sealed class GameSimulation : IGameSimulation
{
    public const int TicksPerSecond = GameConfiguration.FixedTickRate;

    private readonly PriorityQueue<GameCommand, (long Tick, int Player, long Sequence)> _pending = new();
    private readonly HashSet<(PlayerId Player, long Sequence)> _scheduledSequences = new();
    private readonly CommandValidator _validator;
    private readonly UnitMovement? _movement;
    private readonly UnitCombat? _combat;
    private readonly ImmutableDictionary<EntityId, int> _initialHealth;
    private readonly ImmutableDictionary<EntityId, SimVector> _initialPositions;

    public GameSimulation() : this(new WorldEntities([]))
    {
    }

    public GameSimulation(WorldEntities entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _validator = new CommandValidator(entities);
        _initialPositions = entities.Units.ToImmutableDictionary(unit => unit.Id, unit => unit.Position);
        _initialHealth = UnitCombat.InitialHealth(entities);
        World = new WorldSnapshot(0, _initialPositions, _initialHealth);
    }

    public GameSimulation(WorldEntities entities, GameConfiguration configuration, INavigationService navigation)
        : this(entities)
    {
        _movement = new UnitMovement(entities, configuration, navigation);
    }

    public GameSimulation(WorldEntities entities, GameConfiguration configuration, INavigationService navigation,
        ILineOfSightService sight) : this(entities, configuration, navigation)
    {
        ArgumentNullException.ThrowIfNull(sight);
        _combat = new UnitCombat(entities, configuration, sight);
    }

    public IReadOnlyWorld World { get; private set; }

    public void Schedule(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ExecuteAtTick <= World.Tick)
            throw new ArgumentOutOfRangeException(nameof(command), "Commands must be scheduled for a future tick.");
        if (command.Player.Value <= 0 || command.Sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(command), "Player IDs and sequence numbers must be positive.");
        if (!_scheduledSequences.Add((command.Player, command.Sequence)))
            throw new ArgumentException("This sequence number is already scheduled for this player.", nameof(command));

        _pending.Enqueue(command, (command.ExecuteAtTick, command.Player.Value, command.Sequence));
    }

    public IReadOnlyList<IGameEvent> Tick()
    {
        var tick = checked(World.Tick + 1);
        List<IGameEvent>? events = null;
        while (_pending.TryPeek(out var command, out var priority) && priority.Tick == tick)
        {
            _pending.Dequeue();
            events ??= new List<IGameEvent>();
            var result = _validator.Validate(command, _combat?.Health ?? _initialHealth);
            if (result.Accepted && _movement is not null)
            {
                if (command is MoveCommand move)
                {
                    result = _movement.Plan(move);
                    if (result.Accepted)
                        _combat?.CancelOrders(move.Units);
                }
                else if (command is AttackCommand attack && _combat is not null)
                {
                    result = _combat.OrderAttack(attack, _movement.Positions);
                    if (result.Accepted)
                        _movement.Stop(attack.Units);
                }
            }

            events.Add(new CommandProcessed(tick, command, result));
        }

        _movement?.Advance();
        if (_combat is not null && _movement is not null)
        {
            events ??= new List<IGameEvent>();
            _combat.Resolve(tick, _movement.Positions, events, _movement);
        }

        World = new WorldSnapshot(tick, _movement?.Positions ?? _initialPositions, _combat?.Health ?? _initialHealth);
        return events is null ? Array.Empty<IGameEvent>() : events.AsReadOnly();
    }

    private sealed record WorldSnapshot(long Tick, ImmutableDictionary<EntityId, SimVector> Positions,
        ImmutableDictionary<EntityId, int> Health) : IReadOnlyWorld
    {
        public bool TryGetUnitPosition(EntityId id, out SimVector position) => Positions.TryGetValue(id, out position);
        public bool TryGetHitPoints(EntityId id, out int hitPoints) => Health.TryGetValue(id, out hitPoints);
    }
}
