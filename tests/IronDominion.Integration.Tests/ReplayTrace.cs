using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IronDominion.Domain;

namespace IronDominion.Integration.Tests;

internal sealed class ReplayTrace(WorldEntities initialWorld)
{
    private static readonly PlayerId[] Players = [GoldenBattlefield.Blue, GoldenBattlefield.Orange];
    private readonly SortedSet<int> _actors =
        new(initialWorld.Units.Select(unit => unit.Id.Value).Concat(initialWorld.Buildings.Select(building => building.Id.Value)));
    private readonly List<string> _frames = new();

    public IReadOnlyList<string> Frames => _frames;
    public string Digest => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Concat(_frames))));

    public void Add(IReadOnlyWorld world, IReadOnlyList<IGameEvent> events)
    {
        foreach (var spawned in events.OfType<UnitSpawned>())
            _actors.Add(spawned.Unit.Id.Value);

        var frame = new
        {
            world.Tick,
            world.Result,
            Economy = Players.Select(player => new
            {
                Player = player.Value,
                Credits = world.Credits(player),
                Income = world.IncomePerSecond(player)
            }).ToArray(),
            Actors = _actors.Select(value =>
            {
                var id = new EntityId(value);
                return new
                {
                    Id = value,
                    Health = world.TryGetHitPoints(id, out var hp) ? hp : (int?)null,
                    Center = world.TryGetUnitPosition(id, out var position) ? Quantize(position) : null,
                    Owner = world.LivingUnitOwner(id)?.Value,
                    Cover = world.HasCover(id),
                    Queue = world.TrainingQueue(id)
                };
            }).ToArray(),
            Sectors = world.Sectors.OrderBy(sector => sector.Id.Value).Select(sector => new
            {
                Id = sector.Id.Value,
                Position = Quantize(sector.Position),
                Owner = sector.Owner?.Value,
                CapturingPlayer = sector.CapturingPlayer?.Value,
                sector.CaptureTicks,
                sector.Contested
            }).ToArray(),
            Events = events.Select(ProjectEvent).ToArray()
        };

        _frames.Add(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(frame))));
    }

    private static object ProjectEvent(IGameEvent gameEvent) => gameEvent switch
    {
        CommandProcessed order => new
        {
            Type = nameof(CommandProcessed), order.Tick, Command = RecordedOrder.Capture(order.Command), order.Result
        },
        UnitSpawned spawn => new
        {
            Type = nameof(UnitSpawned), spawn.Tick, Id = spawn.Unit.Id.Value, Owner = spawn.Unit.Owner?.Value,
            spawn.Unit.Kind, spawn.Unit.HitPoints, Position = Quantize(spawn.Unit.Position)
        },
        _ => new { Type = gameEvent.GetType().Name, Data = JsonSerializer.SerializeToElement(gameEvent, gameEvent.GetType()) }
    };

    private static int[] Quantize(SimVector position) =>
        [(int)Math.Round(position.X * 100.0, MidpointRounding.AwayFromZero),
            (int)Math.Round(position.Z * 100.0, MidpointRounding.AwayFromZero)];
}
