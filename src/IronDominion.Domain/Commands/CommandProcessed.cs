namespace IronDominion.Domain;

public sealed record CommandProcessed(long Tick, GameCommand Command, CommandResult Result) : IGameEvent;
