using Godot;
using IronDominion.Application;
using IronDominion.Infrastructure.Configuration;

namespace IronDominion.Game;

public partial class Battlefield : Node3D
{
    private readonly GameSession _session = new();

    public override void _Ready()
    {
        try
        {
            var configuration = GameConfigurationLoader.LoadDefault();
            Engine.PhysicsTicksPerSecond = configuration.TickRate;
        }
        catch (InvalidDataException exception)
        {
            GD.PushError(exception.Message);
            SetPhysicsProcess(false);
            GetTree().Quit(1);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _session.AdvanceOneTick();
    }
}
