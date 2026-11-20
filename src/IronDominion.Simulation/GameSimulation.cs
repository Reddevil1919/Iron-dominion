using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

public sealed class GameSimulation : IGameSimulation
{
    public const int TicksPerSecond = GameConfiguration.FixedTickRate;

    private readonly PriorityQueue<GameCommand, (long Tick, int Player, long Sequence)> _pending = new();
    private readonly HashSet<(PlayerId Player, long Sequence)> _scheduledSequences = new();
    private readonly WorldEntities _entities;
    private readonly CommandValidator _validator;
    private readonly UnitMovement? _movement;
    private readonly UnitCombat? _combat;
    private readonly UnitProduction? _production;
    private readonly PlayerEconomy? _economy;
    private readonly SectorControl? _sectorControl;
    private readonly ImmutableArray<SectorState> _initialSectors;
    private ImmutableDictionary<EntityId, PlayerId> _unitOwners;
    private readonly ImmutableDictionary<EntityId, int> _initialHealth;
    private readonly ImmutableDictionary<EntityId, SimVector> _initialPositions;

    public GameSimulation() : this(new WorldEntities([]))
    {
    }

    public GameSimulation(WorldEntities entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _entities = entities.Copy();
        _validator = new CommandValidator(_entities);
        _initialPositions = _entities.Units.ToImmutableDictionary(unit => unit.Id, unit => unit.Position);
        _initialHealth = UnitCombat.InitialHealth(_entities);
        _initialSectors = _entities.Sectors.OrderBy(sector => sector.Id.Value)
            .Select(sector => new SectorState(sector.Id, sector.Position, sector.Owner, null, 0, false))
            .ToImmutableArray();
        _unitOwners = _entities.Units.ToImmutableDictionary(unit => unit.Id, unit => unit.Owner!.Value);
        World = Snapshot(0);
    }

    public GameSimulation(WorldEntities entities, GameConfiguration configuration, INavigationService navigation)
        : this(entities)
    {
        _movement = new UnitMovement(_entities, configuration, navigation);
    }

    public GameSimulation(WorldEntities entities, GameConfiguration configuration, INavigationService navigation,
        ILineOfSightService sight) : this(entities, configuration, navigation)
    {
        ArgumentNullException.ThrowIfNull(sight);
        _combat = new UnitCombat(_entities, configuration, sight);
        _sectorControl = new SectorControl(_initialSectors, _entities, configuration);
        _economy = new PlayerEconomy(_entities, configuration, _combat.Health, _initialSectors);
        _production = new UnitProduction(_entities, configuration, navigation, _movement!, _combat, _economy);
        World = Snapshot(0);
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
        var startingBalances = _economy?.Credits ?? ImmutableDictionary<PlayerId, int>.Empty;
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
                else if (command is TrainUnitCommand train && _production is not null)
                {
                    result = _production.Enqueue(train);
                }
            }

            events.Add(new CommandProcessed(tick, command, result));
        }

        _movement?.Advance();
        if (_combat is not null && _movement is not null)
        {
            events ??= new List<IGameEvent>();
            _combat.Resolve(tick, _movement.Positions, events, _movement);

            if (_sectorControl is not null && _economy is not null && _production is not null)
            {
                _sectorControl.Advance(tick, _movement.Positions, _combat.Health, events);
                _economy.Accrue(_combat.Health, _sectorControl.Sectors);
                _production.Advance(tick, events);
                _economy.PublishChanges(tick, startingBalances, events);
                foreach (var spawn in events.OfType<UnitSpawned>())
                    _unitOwners = _unitOwners.Add(spawn.Unit.Id, spawn.Unit.Owner!.Value);
            }
        }

        World = Snapshot(tick);
        return events is not { Count: > 0 } ? Array.Empty<IGameEvent>() : events.AsReadOnly();
    }

    private WorldSnapshot Snapshot(long tick) => new(
        tick, _movement?.Positions ?? _initialPositions, _combat?.Health ?? _initialHealth, _unitOwners,
        _economy?.Credits ?? ImmutableDictionary<PlayerId, int>.Empty,
        _economy?.Income ?? ImmutableDictionary<PlayerId, long>.Empty,
        _sectorControl?.Sectors ?? _initialSectors,
        _production?.Queues ?? ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>>.Empty);

    private sealed record WorldSnapshot(long Tick, ImmutableDictionary<EntityId, SimVector> Positions,
        ImmutableDictionary<EntityId, int> Health, ImmutableDictionary<EntityId, PlayerId> UnitOwners,
        ImmutableDictionary<PlayerId, int> Balances,
        ImmutableDictionary<PlayerId, long> IncomeRates,
        ImmutableArray<SectorState> Sectors,
        ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>> Queues) : IReadOnlyWorld
    {
        public bool TryGetUnitPosition(EntityId id, out SimVector position) => Positions.TryGetValue(id, out position);
        public bool TryGetHitPoints(EntityId id, out int hitPoints) => Health.TryGetValue(id, out hitPoints);
        public PlayerId? LivingUnitOwner(EntityId id) =>
            Health.GetValueOrDefault(id) > 0 && UnitOwners.TryGetValue(id, out var owner) ? owner : null;
        public int Credits(PlayerId player) => Balances.GetValueOrDefault(player);
        public long IncomePerSecond(PlayerId player) => IncomeRates.GetValueOrDefault(player);
        public IReadOnlyList<TrainingOrder> TrainingQueue(EntityId producer) =>
            Queues.GetValueOrDefault(producer, ImmutableList<TrainingOrder>.Empty);
    }
}
