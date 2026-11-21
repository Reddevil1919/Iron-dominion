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
    private readonly EntityId[] _playerOneHeadquarters = [];
    private readonly EntityId[] _playerTwoHeadquarters = [];
    private ImmutableDictionary<EntityId, Unit> _units;
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
        _units = _entities.Units.ToImmutableDictionary(unit => unit.Id);
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
        var headquarters = _entities.Buildings.Where(building => building.Kind == BuildingKind.Headquarters).ToArray();
        _playerOneHeadquarters = headquarters.Where(building => building.Owner == new PlayerId(1))
            .Select(building => building.Id).ToArray();
        _playerTwoHeadquarters = headquarters.Where(building => building.Owner == new PlayerId(2))
            .Select(building => building.Id).ToArray();
        _sectorControl = new SectorControl(_initialSectors, _entities, configuration);
        _economy = new PlayerEconomy(_entities, configuration, _combat.Health, _initialSectors);
        _production = new UnitProduction(_entities, configuration, navigation, _movement!, _combat, _economy);
        World = Snapshot(0);
    }

    public IReadOnlyWorld World { get; private set; }

    public PlayerObservation Observe(PlayerId player)
    {
        var units = _entities.Units.Where(unit => World.LivingUnitOwner(unit.Id) == player)
            .OrderBy(unit => unit.Id.Value)
            .Select(unit =>
            {
                World.TryGetUnitPosition(unit.Id, out var position);
                return new FriendlyUnit(unit.Id, unit.Kind, position, _movement?.IsMoving(unit.Id) == true);
            }).ToImmutableArray();
        var buildings = _entities.Buildings.Where(building => building.Owner == player
                && World.TryGetHitPoints(building.Id, out var hp) && hp > 0)
            .OrderBy(building => building.Id.Value)
            .Select(building => new FriendlyBuilding(building.Id, building.Kind, building.Position,
                building.TrainedUnitKind, building.ProductionExit, World.TrainingQueue(building.Id).Count > 0))
            .ToImmutableArray();
        var contacts = _combat is not null && _movement is not null
            ? _combat.ObserveEnemies(player, _movement.Positions)
            : [];
        var sectors = World.Sectors.Select(sector => new SectorSite(sector.Id, sector.Position, sector.Owner))
            .ToImmutableArray();
        return new PlayerObservation(World.Tick, player, World.Credits(player), units, buildings, contacts, sectors);
    }

    public void Schedule(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (World.Result != MatchResult.Running)
            return;

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
        if (World.Result != MatchResult.Running)
            return [];

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
                    _units = _units.Add(spawn.Unit.Id, spawn.Unit);
            }
        }

        var resultAtEndOfTick = ResolveMatchResult();
        if (resultAtEndOfTick != MatchResult.Running)
        {
            _pending.Clear();
            _scheduledSequences.Clear();
            events ??= new List<IGameEvent>();
            events.Add(new MatchEnded(tick, resultAtEndOfTick));
        }

        World = Snapshot(tick, resultAtEndOfTick);
        return events is not { Count: > 0 } ? Array.Empty<IGameEvent>() : events.AsReadOnly();
    }

    private MatchResult ResolveMatchResult()
    {
        if (_combat is null || _playerOneHeadquarters.Length == 0 || _playerTwoHeadquarters.Length == 0)
            return MatchResult.Running;

        var health = _combat.Health;
        var playerOneAlive = _playerOneHeadquarters.Any(id => health[id] > 0);
        var playerTwoAlive = _playerTwoHeadquarters.Any(id => health[id] > 0);
        return (playerOneAlive, playerTwoAlive) switch
        {
            (true, true) => MatchResult.Running,
            (true, false) => MatchResult.PlayerOneWon,
            (false, true) => MatchResult.PlayerTwoWon,
            _ => MatchResult.Draw
        };
    }

    private WorldSnapshot Snapshot(long tick, MatchResult result = MatchResult.Running) => new(
        tick, result, _movement?.Positions ?? _initialPositions, _combat?.Health ?? _initialHealth, _units, _entities.CoverZones,
        _economy?.Credits ?? ImmutableDictionary<PlayerId, int>.Empty,
        _economy?.Income ?? ImmutableDictionary<PlayerId, long>.Empty,
        _sectorControl?.Sectors ?? _initialSectors,
        _production?.Queues ?? ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>>.Empty);

    private sealed record WorldSnapshot(long Tick, MatchResult Result, ImmutableDictionary<EntityId, SimVector> Positions,
        ImmutableDictionary<EntityId, int> Health, ImmutableDictionary<EntityId, Unit> Units,
        ImmutableArray<CoverZone> CoverZones,
        ImmutableDictionary<PlayerId, int> Balances,
        ImmutableDictionary<PlayerId, long> IncomeRates,
        ImmutableArray<SectorState> Sectors,
        ImmutableDictionary<EntityId, ImmutableList<TrainingOrder>> Queues) : IReadOnlyWorld
    {
        public bool TryGetUnitPosition(EntityId id, out SimVector position) => Positions.TryGetValue(id, out position);
        public bool TryGetHitPoints(EntityId id, out int hitPoints) => Health.TryGetValue(id, out hitPoints);
        public PlayerId? LivingUnitOwner(EntityId id) =>
            Health.GetValueOrDefault(id) > 0 && Units.TryGetValue(id, out var unit) ? unit.Owner : null;
        public bool HasCover(EntityId id) =>
            Health.GetValueOrDefault(id) > 0 && Units.TryGetValue(id, out var unit)
            && CoverZone.Protects(CoverZones, unit, Positions[id]);
        public int Credits(PlayerId player) => Balances.GetValueOrDefault(player);
        public long IncomePerSecond(PlayerId player) => IncomeRates.GetValueOrDefault(player);
        public IReadOnlyList<TrainingOrder> TrainingQueue(EntityId producer) =>
            Queues.GetValueOrDefault(producer, ImmutableList<TrainingOrder>.Empty);
    }
}
