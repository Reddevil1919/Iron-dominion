using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed record MoveCommand : GameCommand
{
    public MoveCommand(PlayerId player, long executeAtTick, long sequence,
        IReadOnlyList<EntityId> units, SimVector destination, bool attackMove = false)
        : base(player, executeAtTick, sequence)
    {
        Units = units.ToImmutableArray();
        Destination = destination;
        AttackMove = attackMove;
    }

    public ImmutableArray<EntityId> Units { get; }
    public SimVector Destination { get; }
    public bool AttackMove { get; }
}
