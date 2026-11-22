using Godot;

namespace IronDominion.Game;

internal sealed class UnitArt
{
    public InfantryArt Infantry { get; }
    public TankArt Tank { get; }

    public UnitArt(Color identification, bool local)
    {
        Infantry = new InfantryArt(identification, local);
        Tank = new TankArt(identification, local);
    }
}
