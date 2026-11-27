using System.Collections.Immutable;
using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class Battlefield
{
    private readonly Dictionary<BuildingPlacement, Node3D> _siteViews = new();
    private readonly HashSet<EntityId> _constructedBuildings = [];
    private IReadOnlyDictionary<BuildingKind, BuildingDefinition> _buildingDefinitions = null!;
    private ConstructionMenu? _constructionMenu;
    private float _constructionGround;

    private ConstructionLayout CreateConstructionLayout(Aabb floor, CollisionShape3D roadblock,
        IEnumerable<Sector> sectors, float radius)
    {
        _constructionGround = floor.End.Y;
        var bounds = FromBox(floor);
        var rectangle = new Vector2(floor.Size.X, floor.Size.Z);
        var inset = BattlefieldNavigation.UnitClearance;
        // Each faction builds in its own half with a central maneuver lane.
        var left = new SimVector(bounds.Minimum.X + inset, bounds.Minimum.Z + inset);
        var right = new SimVector(bounds.Maximum.X - inset, bounds.Maximum.Z - inset);
        var middle = bounds.Minimum.X + rectangle.X / 2;
        var zones = ImmutableDictionary<PlayerId, ConstructionBounds>.Empty
            .Add(LocalPlayer, new ConstructionBounds(left, new SimVector(middle - 8, right.Z)))
            .Add(Opponent, new ConstructionBounds(new SimVector(middle + 8, left.Z), right));
        var exclusions = sectors.Select(sector => new ConstructionBounds(
            new SimVector(sector.Position.X - radius, sector.Position.Z - radius),
            new SimVector(sector.Position.X + radius, sector.Position.Z + radius)))
            .Prepend(FromBox(BattlefieldNavigation.BoxBounds(roadblock))).ToImmutableArray();
        return new ConstructionLayout(bounds, zones, exclusions, inset);
    }

    private static ConstructionBounds FromBox(Aabb box) =>
        new(new SimVector(box.Position.X, box.Position.Z), new SimVector(box.End.X, box.End.Z));

    private Vector3 ConstructionPosition(BuildingPlacement placement) =>
        new(placement.Position.X, _constructionGround, placement.Position.Z);

    private void UpdateConstructionSites(IReadOnlyWorld world, BattlefieldFog fog)
    {
        if (world.ConstructionSites.IsEmpty && _siteViews.Count == 0)
            return;

        foreach (var placement in _siteViews.Keys.ToArray())
        {
            if (world.ConstructionSites.Any(site => site.Placement == placement))
                continue;
            _siteViews[placement].Hide();
            _siteViews[placement].QueueFree();
            _siteViews.Remove(placement);
        }
        foreach (var site in world.ConstructionSites)
        {
            var placement = site.Placement;
            var definition = _buildingDefinitions[placement.Kind];
            var plan = definition.Construction!;
            if (!_siteViews.TryGetValue(placement, out var view))
            {
                view = new Node3D { Name = "ConstructionSite", Position = ConstructionPosition(placement),
                    Rotation = new Vector3(0, placement.QuarterTurns * Mathf.Pi / 2, 0) };
                AddChild(view, forceReadableName: true);
                ConstructionModels.Box(view, new Vector3(0, 0.12f, 0), new Vector3(plan.Width, 0.24f, plan.Depth),
                    new StandardMaterial3D { AlbedoColor = new Color("666357") });
                foreach (var x in new[] { -0.43f, 0.43f })
                foreach (var z in new[] { -0.43f, 0.43f })
                    ConstructionModels.Box(view, new Vector3(x * plan.Width, 0.72f, z * plan.Depth),
                        new Vector3(0.3f, 1.4f, 0.3f), _teamMaterials[placement.Owner]);
                var progress = new Label3D
                {
                    Name = "Progress", Position = Vector3.Up * 2.2f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    PixelSize = 0.04f, FontSize = 25, Modulate = HudStyle.Amber
                };
                view.AddChild(progress);
                _siteViews.Add(placement, view);
            }
            view.Visible = placement.Owner == LocalPlayer || fog.CanSeeGround(placement.Position);
            var label = view.GetNode<Label3D>("Progress");
            var percent = (plan.BuildTicks - site.RemainingTicks) * 100L / plan.BuildTicks;
            var text = $"Construction {percent}%";
            if (label.Text != text)
                label.Text = text;
        }
    }

    private void AddConstructedBuilding(EntityId id, BuildingPlacement placement, int hitPoints)
    {
        var definition = _buildingDefinitions[placement.Kind];
        var plan = definition.Construction!;
        var height = ConstructionModels.Height(placement.Kind);
        var body = new StaticBody3D
        {
            Name = $"Building{id.Value}", Position = ConstructionPosition(placement),
            Rotation = new Vector3(0, placement.QuarterTurns * Mathf.Pi / 2, 0), CollisionLayer = 6
        };
        var mesh = new MeshInstance3D { Name = "Base", Mesh = new BoxMesh { Size = new Vector3(plan.Width, 0.22f, plan.Depth) },
            Position = Vector3.Up * 0.11f, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("6b746e") } };
        body.AddChild(mesh);
        ConstructionModels.AddSuperstructure(body, definition,
            new StandardMaterial3D { AlbedoColor = new Color("747d70"), Roughness = 0.92f }, _teamMaterials[placement.Owner]);
        body.AddChild(new CollisionShape3D { Name = "Collision", Position = Vector3.Up * height / 2,
            Shape = new BoxShape3D { Size = new Vector3(plan.Width, height, plan.Depth) } });
        AddChild(body);
        _constructedBuildings.Add(id);
        _buildings.Add(id, body);
        if (placement.Owner == Opponent)
            _enemyBuildings.Add(body, id);
        var bar = new HealthBar(body, definition.MaxHp, height + 1.2f, 0.115f);
        bar.SetHitPoints(hitPoints);
        _healthBars.Add(id, bar);
    }

    private bool RemoveConstructedBuilding(EntityId id)
    {
        if (!_constructedBuildings.Remove(id))
            return false;
        var body = _buildings[id];
        _buildings.Remove(id);
        _enemyBuildings.Remove(body);
        _healthBars.Remove(id);
        RemoveChild(body);
        body.QueueFree();
        return true;
    }
}
