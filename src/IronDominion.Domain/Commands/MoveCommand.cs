using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed record MoveCommand : GameCommand
{
    public MoveCommand(PlayerId player, long executeAtTick, long sequence,
        IReadOnlyList<EntityId> units, SimVector destination)
        : base(player, executeAtTick, sequence)
    {
        Units = units.ToImmutableArray();
        Destination = destination;
    }

    public ImmutableArray<EntityId> Units { get; }
    public SimVector Destination { get; }
}
