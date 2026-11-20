using Godot;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Configuration;

namespace IronDominion.Game;

public partial class Battlefield : Node3D
{
    private static readonly PlayerId LocalPlayer = new(1);
    private static readonly PlayerId Opponent = new(2);

    [Export] private UnitSelectionController _unitSelection = null!;
    [Export] private Node3D _unitRoot = null!;

    private readonly GameSession _session = new();

    public override void _Ready()
    {
        try
        {
            var configuration = GameConfigurationLoader.LoadDefault();
            Engine.PhysicsTicksPerSecond = configuration.TickRate;
            SetUpUnits(configuration);
        }
        catch (InvalidDataException exception)
        {
            GD.PushError(exception.Message);
            SetPhysicsProcess(false);
            GetTree().Quit(1);
        }
    }

    private void SetUpUnits(GameConfiguration configuration)
    {
        var infantry = configuration.Units.Single(unit => unit.Kind == UnitKind.InfantrySquad);
        var tank = configuration.Units.Single(unit => unit.Kind == UnitKind.Tank);
        Unit[] units =
        [
            CreateUnit(1, LocalPlayer, infantry, -20, 18),
            CreateUnit(2, LocalPlayer, infantry, -15, 29),
            CreateUnit(3, LocalPlayer, tank, -17, 7),
            CreateUnit(4, Opponent, infantry, 20, -18),
            CreateUnit(5, Opponent, infantry, 15, -29),
            CreateUnit(6, Opponent, tank, 17, -7)
        ];
        var blue = new StandardMaterial3D { AlbedoColor = new Color("3da9fc") };
        var orange = new StandardMaterial3D { AlbedoColor = new Color("f7793c") };
        var views = new List<UnitView>(units.Length);
        foreach (var unit in units)
        {
            var view = UnitView.Create(unit, unit.Owner == LocalPlayer ? blue : orange);
            _unitRoot.AddChild(view);
            views.Add(view);
        }

        _unitSelection.Initialize(new UnitSelection(LocalPlayer, new WorldEntities(units)), views);
    }

    private static Unit CreateUnit(int id, PlayerId player, UnitDefinition definition, float x, float z) =>
        new(new EntityId(id), player, new SimVector(x, z), definition.Kind, definition.MaxHp);

    public override void _PhysicsProcess(double delta)
    {
        _session.AdvanceOneTick();
    }
}
