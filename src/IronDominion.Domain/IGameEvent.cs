namespace IronDominion.Domain;

public interface IGameEvent
{
    long Tick { get; }
}
