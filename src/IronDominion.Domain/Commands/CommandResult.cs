namespace IronDominion.Domain;

public sealed record CommandResult(bool Accepted, string? RejectionCode);
