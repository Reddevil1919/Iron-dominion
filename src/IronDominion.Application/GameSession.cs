using IronDominion.Domain;
using IronDominion.Simulation;

namespace IronDominion.Application;

public sealed class GameSession
{
    public const int TicksPerSecond = GameSimulation.TicksPerSecond;

    private readonly GameSimulation _simulation;

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

    private GameSession(GameSimulation simulation)
    {
        _simulation = simulation;
        Commands = new CommandScheduler(simulation);
    }

    public CommandScheduler Commands { get; }
    public IReadOnlyWorld World => _simulation.World;
    public bool IsPaused { get; private set; }
    public PlayerObservation Observe(PlayerId player) => _simulation.Observe(player);

    public SessionCheckpoint Capture() => new(_simulation.Capture(), Commands.Capture(), IsPaused);

    public static GameSession Restore(WorldEntities map, GameConfiguration configuration,
        INavigationService navigation, ILineOfSightService sight, SessionCheckpoint checkpoint)
    {
        var session = new GameSession(GameSimulation.Restore(map, configuration, navigation, sight, checkpoint.Simulation));
        session.Commands.Restore(checkpoint.Scheduler);
        if (session.World.Result == MatchResult.Running)
        {
            var lastSequences = checkpoint.Simulation.UsedSequences.GroupBy(sequence => sequence.Player)
                .OrderBy(group => group.Key.Value).Select(group => new CommandSequence(group.Key, group.Max(item => item.Sequence)));
            if (!session.Commands.Capture().SequenceEqual(lastSequences))
                throw new InvalidDataException("The saved scheduler disagrees with the simulation.");
        }
        else if (checkpoint.Paused)
        {
            throw new InvalidDataException("A finished match cannot be paused.");
        }
        session.SetPaused(checkpoint.Paused);
        return session;
    }

    public void SetPaused(bool paused)
    {
        if (World.Result == MatchResult.Running)
            IsPaused = paused;
    }

    public IReadOnlyList<IGameEvent> AdvanceOneTick() => IsPaused ? [] : _simulation.Tick();
}
