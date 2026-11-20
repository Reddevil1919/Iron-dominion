namespace IronDominion.Domain;

public interface IReadOnlyWorld
{
    long Tick { get; }
}
