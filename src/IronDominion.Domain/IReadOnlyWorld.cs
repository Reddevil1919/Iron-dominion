using System.Collections.Immutable;

namespace IronDominion.Domain;

public interface IReadOnlyWorld
{
    long Tick { get; }
    MatchResult Result { get; }
    bool TryGetUnitPosition(EntityId id, out SimVector position);
    bool TryGetHitPoints(EntityId id, out int hitPoints);
    PlayerId? LivingUnitOwner(EntityId id);
    bool HasCover(EntityId id);
    bool IsSuppressed(EntityId id);
    int Credits(PlayerId player);
    long IncomePerSecond(PlayerId player);
    ImmutableArray<SectorState> Sectors { get; }
    IReadOnlyList<TrainingOrder> TrainingQueue(EntityId producer);
}
