namespace IronDominion.Domain;

public interface INavigationService
{
    // Returns waypoints ending at the requested destination, or an empty path if unreachable.
    IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask);
}
