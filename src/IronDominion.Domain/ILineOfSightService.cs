namespace IronDominion.Domain;

public interface ILineOfSightService
{
    bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId);
}
