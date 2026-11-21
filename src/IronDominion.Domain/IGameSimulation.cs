namespace IronDominion.Domain;

public interface IGameSimulation
{
    IReadOnlyWorld World { get; }
    PlayerObservation Observe(PlayerId player);

    void Schedule(GameCommand command);

    // Advances the world by one fixed 50 ms step.
    IReadOnlyList<IGameEvent> Tick();
}
