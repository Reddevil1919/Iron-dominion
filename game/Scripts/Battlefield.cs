using Godot;
using IronDominion.Application;

namespace IronDominion.Game;

public partial class Battlefield : Node3D
{
    private readonly GameSession _session = new();

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = GameSession.TicksPerSecond;
    }

    public override void _PhysicsProcess(double delta)
    {
        _session.AdvanceOneTick();
    }
}
