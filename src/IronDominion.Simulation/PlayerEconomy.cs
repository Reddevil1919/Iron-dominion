using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class PlayerEconomy
{
    private readonly GameConfiguration _configuration;
    private readonly Building[] _headquarters;
    private readonly PlayerId[] _players;
    private readonly Dictionary<PlayerId, long> _remainders = new();

    public PlayerEconomy(WorldEntities world, GameConfiguration configuration,
        IReadOnlyDictionary<EntityId, int> health, ImmutableArray<SectorState> sectors)
    {
        _configuration = configuration;
        _headquarters = world.Buildings.Where(building => building.Kind == BuildingKind.Headquarters).ToArray();
        _players = world.Units.Cast<WorldEntity>().Concat(world.Buildings).Concat(world.Sectors)
            .Select(entity => entity.Owner).OfType<PlayerId>().Distinct().OrderBy(player => player.Value).ToArray();
        Credits = _players.ToImmutableDictionary(player => player, _ => configuration.StartingCredits);
        Income = _players.ToImmutableDictionary(player => player, player => CalculateIncome(player, health, sectors));
    }

    public ImmutableDictionary<PlayerId, int> Credits { get; private set; }
    public ImmutableDictionary<PlayerId, long> Income { get; private set; }

    public ImmutableArray<PlayerCheckpoint> Capture() => _players.Select(player =>
        new PlayerCheckpoint(player, Credits[player], _remainders.GetValueOrDefault(player))).ToImmutableArray();

    public void Restore(ImmutableArray<PlayerCheckpoint> players,
        IReadOnlyDictionary<EntityId, int> health, ImmutableArray<SectorState> sectors)
    {
        Credits = players.ToImmutableDictionary(player => player.Id, player => player.Credits);
        Income = _players.ToImmutableDictionary(player => player, player => CalculateIncome(player, health, sectors));
        foreach (var player in players)
            _remainders.Add(player.Id, player.IncomeRemainder);
    }

    public bool TrySpend(PlayerId player, int cost)
    {
        var balance = Credits[player];
        if (balance < cost)
            return false;

        Credits = Credits.SetItem(player, balance - cost);
        return true;
    }

    public void Accrue(IReadOnlyDictionary<EntityId, int> health, ImmutableArray<SectorState> sectors)
    {
        foreach (var player in _players)
        {
            var rate = CalculateIncome(player, health, sectors);
            Income = Income.SetItem(player, rate);
            var total = _remainders.GetValueOrDefault(player) + rate;
            var wholeCredits = total / _configuration.TickRate;
            _remainders[player] = total % _configuration.TickRate;
            Credits = Credits.SetItem(player, (int)Math.Min(int.MaxValue, Credits[player] + wholeCredits));
        }
    }

    public void PublishChanges(long tick, ImmutableDictionary<PlayerId, int> before, List<IGameEvent> events)
    {
        if (before == Credits)
            return;

        foreach (var player in _players)
        {
            var balance = Credits[player];
            var delta = balance - before[player];
            if (delta != 0)
                events.Add(new CreditsChanged(tick, player, delta, balance));
        }
    }

    private long CalculateIncome(PlayerId player, IReadOnlyDictionary<EntityId, int> health,
        ImmutableArray<SectorState> sectors)
    {
        var headquarters = _headquarters.Count(building => building.Owner == player && health[building.Id] > 0);
        var ownedSectors = sectors.Count(sector => sector.Owner == player);
        return (long)headquarters * _configuration.HeadquartersIncomePerSecond
            + (long)ownedSectors * _configuration.SectorIncomePerSecond;
    }
}
