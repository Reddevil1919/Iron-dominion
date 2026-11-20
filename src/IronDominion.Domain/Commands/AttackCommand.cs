using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed record AttackCommand : GameCommand
{
    public AttackCommand(PlayerId player, long executeAtTick, long sequence,
        IReadOnlyList<EntityId> units, EntityId target)
        : base(player, executeAtTick, sequence)
    {
        Units = units.ToImmutableArray();
        Target = target;
    }

    public ImmutableArray<EntityId> Units { get; }
    public EntityId Target { get; }
}
