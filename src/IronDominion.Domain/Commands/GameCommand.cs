namespace IronDominion.Domain;

public abstract record GameCommand(PlayerId Player, long ExecuteAtTick, long Sequence);
