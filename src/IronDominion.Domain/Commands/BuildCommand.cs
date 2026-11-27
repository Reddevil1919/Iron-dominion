namespace IronDominion.Domain;

public sealed record BuildCommand(PlayerId Player, long ExecuteAtTick, long Sequence,
    EntityId Headquarters, BuildingKind Kind, SimVector Position, int QuarterTurns = 0)
    : GameCommand(Player, ExecuteAtTick, Sequence);
