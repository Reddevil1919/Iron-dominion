namespace IronDominion.Domain;

public sealed record TrainUnitCommand(PlayerId Player, long ExecuteAtTick, long Sequence,
    EntityId Producer, UnitKind Kind) : GameCommand(Player, ExecuteAtTick, Sequence);
