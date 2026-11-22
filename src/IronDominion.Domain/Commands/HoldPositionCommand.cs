using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed record HoldPositionCommand : GameCommand
{
    public HoldPositionCommand(PlayerId player, long executeAtTick, long sequence, IReadOnlyList<EntityId> units)
        : base(player, executeAtTick, sequence)
    {
        Units = units.ToImmutableArray();
    }

    public ImmutableArray<EntityId> Units { get; }
}
