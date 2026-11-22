using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.AI;

public sealed record CommanderCheckpoint(ImmutableArray<ApproachCheckpoint> Approaches,
    EntityId? CapturingUnit, EntityId? CapturingSector, EntityId? ScoutingSector);

public readonly record struct ApproachCheckpoint(EntityId Unit, EntityId Target, SimVector Position, SimVector Destination);
