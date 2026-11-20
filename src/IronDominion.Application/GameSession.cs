using IronDominion.Domain;
using IronDominion.Simulation;

namespace IronDominion.Application;

public sealed class GameSession
{
    public const int TicksPerSecond = GameSimulation.TicksPerSecond;

    private readonly IGameSimulation _simulation = new GameSimulation();

    public void AdvanceOneTick()
    {
        _simulation.Tick();
    }
}
