namespace IronDominion.Domain;

public interface IConstructionNavigation : INavigationService
{
    void SetConstructionObstacles(IReadOnlyList<ConstructionBounds> footprints);
}
