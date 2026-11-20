using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Application;

public sealed class UnitSelection(PlayerId player, WorldEntities world)
{
    private readonly Dictionary<int, ImmutableArray<EntityId>> _groups = new();

    public ImmutableArray<EntityId> Selected { get; private set; } = [];

    public bool CanSelect(EntityId id) =>
        world.Find(id) is Unit { HitPoints: > 0 } unit && unit.Owner == player;

    public void Select(IEnumerable<EntityId> candidates, bool toggle = false)
    {
        var selection = candidates.Where(CanSelect).ToHashSet();
        if (toggle)
            selection.SymmetricExceptWith(Selected);

        Selected = selection.OrderBy(id => id.Value).ToImmutableArray();
    }

    public void AssignGroup(int number)
    {
        ValidateGroup(number);
        if (Selected.IsEmpty)
            _groups.Remove(number);
        else
            _groups[number] = Selected;
    }

    public void RecallGroup(int number)
    {
        ValidateGroup(number);
        if (_groups.TryGetValue(number, out var saved))
            Select(saved);
    }

    private static void ValidateGroup(int number)
    {
        if (number is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(number), "Control groups are numbered 1 to 9.");
    }
}
