namespace IronDominion.Domain;

public interface IGameSimulation
{
    IReadOnlyWorld World { get; }

    // Advances the world by one fixed 50 ms step.
    IReadOnlyList<IGameEvent> Tick();
}
