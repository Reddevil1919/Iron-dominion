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
    private readonly ImmutableDictionary<EntityId, SimVector> _initialPositions;

    public GameSimulation() : this(new WorldEntities([]))
    {
    }

    public GameSimulation(WorldEntities entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _validator = new CommandValidator(entities);
        _initialPositions = entities.Units.ToImmutableDictionary(unit => unit.Id, unit => unit.Position);
        World = new WorldSnapshot(0, _initialPositions);
    }

    public GameSimulation(WorldEntities entities, GameConfiguration configuration, INavigationService navigation)
        : this(entities)
    {
        _movement = new UnitMovement(entities, configuration, navigation);
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
            var result = _validator.Validate(command);
            if (result.Accepted && command is MoveCommand move && _movement is not null)
                result = _movement.Plan(move);

            events.Add(new CommandProcessed(tick, command, result));
        }

        _movement?.Advance();
        World = new WorldSnapshot(tick, _movement?.Positions ?? _initialPositions);
        return events is null ? Array.Empty<IGameEvent>() : events.AsReadOnly();
    }

    private sealed record WorldSnapshot(long Tick, ImmutableDictionary<EntityId, SimVector> Positions) : IReadOnlyWorld
    {
        public bool TryGetUnitPosition(EntityId id, out SimVector position) => Positions.TryGetValue(id, out position);
    }
}
