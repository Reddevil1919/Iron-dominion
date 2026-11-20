namespace IronDominion.Domain;

public sealed class Sector(EntityId id, PlayerId? owner, SimVector position)
    : WorldEntity(id, owner, position);
