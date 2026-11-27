using Godot;

namespace IronDominion.Game;

internal sealed class UnitArt(Color identification, bool local)
{
    private InfantryArt? _engineers;

    public InfantryArt Infantry { get; } = new(identification, local);
    public TankArt Tank { get; } = new(identification, local);
    public InfantryArt Engineers => _engineers ??= new InfantryArt(identification, local, engineer: true);
}
