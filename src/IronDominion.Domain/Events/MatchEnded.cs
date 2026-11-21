namespace IronDominion.Domain;

public sealed record MatchEnded(long Tick, MatchResult Result) : IGameEvent;
