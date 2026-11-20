namespace IronDominion.Domain;

public sealed record CreditsChanged(long Tick, PlayerId Player, int Delta, int Balance) : IGameEvent;
