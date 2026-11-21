using IronDominion.Domain;
using IronDominion.Simulation;

namespace IronDominion.Application;

public sealed class GameSession
{
    public const int TicksPerSecond = GameSimulation.TicksPerSecond;

    private readonly IGameSimulation _simulation;

    public GameSession() : this(new GameSimulation())
    {
    }

    public GameSession(WorldEntities entities) : this(new GameSimulation(entities))
    {
    }

    public GameSession(WorldEntities entities, GameConfiguration configuration, INavigationService navigation)
        : this(new GameSimulation(entities, configuration, navigation))
    {
    }

    public GameSession(WorldEntities entities, GameConfiguration configuration, INavigationService navigation,
        ILineOfSightService sight) : this(new GameSimulation(entities, configuration, navigation, sight))
    {
    }

    private GameSession(IGameSimulation simulation)
    {
        _simulation = simulation;
        Commands = new CommandScheduler(simulation);
    }

    public CommandScheduler Commands { get; }
    public IReadOnlyWorld World => _simulation.World;
    public bool IsPaused { get; private set; }
    public PlayerObservation Observe(PlayerId player) => _simulation.Observe(player);

    public void SetPaused(bool paused)
    {
        if (World.Result == MatchResult.Running)
            IsPaused = paused;
    }

    public IReadOnlyList<IGameEvent> AdvanceOneTick() => IsPaused ? [] : _simulation.Tick();
}
