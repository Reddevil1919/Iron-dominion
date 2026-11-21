using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed record PlayerObservation(long Tick, PlayerId Player, int Credits,
    ImmutableArray<FriendlyUnit> Units, ImmutableArray<FriendlyBuilding> Buildings,
    ImmutableArray<EnemyContact> Contacts, ImmutableArray<SectorSite> Sectors);

public readonly record struct FriendlyUnit(EntityId Id, UnitKind Kind, SimVector Position, bool IsMoving);
public sealed record FriendlyBuilding(EntityId Id, BuildingKind Kind, SimVector Position,
    UnitKind Trains, SimVector? Exit, bool IsTraining);
public enum EnemyContactKind { Unit, Headquarters, Barracks }
public sealed record EnemyContact(EntityId Id, EnemyContactKind Kind, SimVector Position,
    ImmutableArray<EntityId> Observers);
public readonly record struct SectorSite(EntityId Id, SimVector Position, PlayerId? Owner);
