using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldFog : IDisposable
{
    private const int Resolution = 48;
    private const int MaskIntervalTicks = 4;
    private const byte Explored = 128;
    private const byte Visible = 255;
    private readonly byte[] _cells = new byte[Resolution * Resolution];
    private readonly HashSet<EntityId> _visibleActors = new();
    private readonly BattlefieldSight _sight;
    private readonly ImageTexture _texture;
    private readonly ShaderMaterial _overlay;
    public ShaderMaterial SceneryMaterial { get; }
    private readonly Vector2 _origin;
    private readonly Vector2 _size;
    private readonly Vector2 _cellSize;
    private SimVector[] _lastPositions = [];
    private long _lastMaskTick = -MaskIntervalTicks;

    public BattlefieldFog(Node3D battlefield, Aabb floor, BattlefieldSight sight)
    {
        _sight = sight;
        _origin = new Vector2(floor.Position.X, floor.Position.Z);
        _size = new Vector2(floor.Size.X, floor.Size.Z);
        _cellSize = _size / Resolution;
        using var image = CreateImage();
        _texture = ImageTexture.CreateFromImage(image);
        using var mask = Variant.From(_texture);
        var center = floor.GetCenter();
        _overlay = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://Shaders/battlefield_fog.gdshader"),
            RenderPriority = -1
        };
        _overlay.SetShaderParameter("visibility_mask", mask);
        battlefield.AddChild(new MeshInstance3D
        {
            Name = "FogOfWar",
            Position = new Vector3(center.X, floor.End.Y + 0.08f, center.Z),
            Mesh = new PlaneMesh { Size = _size },
            MaterialOverride = _overlay,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        SceneryMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/depot_scenery.gdshader") };
        SceneryMaterial.SetShaderParameter("visibility_mask", mask);
        SceneryMaterial.SetShaderParameter("floor_origin", _origin);
        SceneryMaterial.SetShaderParameter("floor_size", _size);
    }

    public void Dispose()
    {
        // Release both references while the renderer still exists, including in headless runs.
        _overlay.SetShaderParameter("visibility_mask", default);
        SceneryMaterial.SetShaderParameter("visibility_mask", default);
        _overlay.Dispose();
        SceneryMaterial.Dispose();
        _texture.Dispose();
    }

    public IReadOnlySet<EntityId> VisibleActors => _visibleActors;

    public bool CanSeeSector(SimVector position)
    {
        var cell = (new Vector2(position.X, position.Z) - _origin) / _cellSize;
        var x = Mathf.FloorToInt(cell.X);
        var z = Mathf.FloorToInt(cell.Y);
        return x >= 0 && x < Resolution && z >= 0 && z < Resolution && _cells[z * Resolution + x] == Visible;
    }

    public byte[] CaptureExplored() => _cells.Select(cell => cell == 0 ? (byte)0 : Explored).ToArray();

    public void RestoreExplored(byte[] explored)
    {
        if (explored.Length != _cells.Length || explored.Any(cell => cell is not (0 or Explored)))
            throw new InvalidDataException("Invalid saved battlefield exploration.");

        explored.CopyTo(_cells, 0);
        _lastPositions = [];
        _lastMaskTick = -MaskIntervalTicks;
        using var image = CreateImage();
        _texture.Update(image);
    }

    public void Update(PlayerObservation observation)
    {
        _visibleActors.Clear();
        _visibleActors.UnionWith(observation.Units.Select(unit => unit.Id));
        _visibleActors.UnionWith(observation.Buildings.Select(building => building.Id));
        _visibleActors.UnionWith(observation.Contacts.Select(contact => contact.Id));

        if (observation.Tick - _lastMaskTick < MaskIntervalTicks)
            return;

        _lastMaskTick = observation.Tick;
        var positions = observation.Units.Select(unit => unit.Position).ToArray();
        if (_lastPositions.AsSpan().SequenceEqual(positions))
            return;

        _lastPositions = positions;
        for (var z = 0; z < Resolution; z++)
        for (var x = 0; x < Resolution; x++)
        {
            var index = z * Resolution + x;
            var cell = _origin + new Vector2(x + 0.5f, z + 0.5f) * _cellSize;
            var point = new SimVector(cell.X, cell.Y);
            if (CanSeeGround(positions, point))
                _cells[index] = Visible;
            else if (_cells[index] == Visible)
                _cells[index] = Explored;
        }

        using var image = CreateImage();
        _texture.Update(image);
    }

    private bool CanSeeGround(SimVector[] positions, SimVector point)
    {
        foreach (var position in positions)
        {
            if (_sight.CanSeeGround(position, point))
                return true;
        }

        return false;
    }

    private Image CreateImage() => Image.CreateFromData(Resolution, Resolution, false, Image.Format.L8, _cells);
}
