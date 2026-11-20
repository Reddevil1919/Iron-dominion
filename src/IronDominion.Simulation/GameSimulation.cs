using IronDominion.Domain;

namespace IronDominion.Simulation;

public sealed class GameSimulation : IGameSimulation
{
    public const int TicksPerSecond = GameConfiguration.FixedTickRate;

    public IReadOnlyWorld World { get; private set; } = new WorldSnapshot(0);

    public IReadOnlyList<IGameEvent> Tick()
    {
        World = new WorldSnapshot(World.Tick + 1);
        return Array.Empty<IGameEvent>();
    }

    private sealed record WorldSnapshot(long Tick) : IReadOnlyWorld;
}
