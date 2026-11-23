using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed partial class BattlefieldEffects
{
    private static readonly TerrainLook Turf = new(new("ac916a"), new("58432e"), 0.68f, 0.63f, 16f);
    private static readonly TerrainLook Gravel = new(new("cbbda4"), new("615c4dcc"), 0.47f, 0.57f, 12f);

    public void TrackTerrain(UnitView tank, long tick, float overshoot, bool friendly)
    {
        var forward = tank.TrackForward;
        for (var side = -1; side <= 1; side += 2)
        {
            var ground = _surfaces.GroundAt(tank.RearTrack(side) - forward * overshoot);
            if (LooseTerrain(ground.Material) is not { } look)
                continue;

            _trackMarks.Stamp(ground.Point, forward, look.Tread, tick, friendly);
            var wake = -forward * 0.90f + Vector3.Up * 0.87f;
            _dust.Emit(new Particle(new Visibility(tank.Id, null), tick, look.DustTicks,
                ground.Point + Vector3.Up * 0.24f, wake, look.DustRadius,
                look.Dust, look.Opacity, Shape.Cloud));
        }
    }

    public void FootstepDust(EntityId squad, long tick, Vector3 ankle)
    {
        var ground = _surfaces.GroundAt(ankle);
        if (LooseTerrain(ground.Material) is not { } look)
            return;

        _footstepDust.Emit(new Particle(new Visibility(squad, null), tick, look.DustTicks * 0.65f,
            ground.Point + Vector3.Up * 0.14f, Vector3.Up * 0.40f, look.DustRadius * 0.53f,
            look.Dust, look.Opacity * 0.70f, Shape.Cloud));
    }

    private static TerrainLook? LooseTerrain(GroundMaterial material) => material switch
    {
        GroundMaterial.Soil => Turf,
        GroundMaterial.Gravel => Gravel,
        _ => null
    };

    private readonly record struct TerrainLook(Color Dust, Color Tread, float Opacity, float DustRadius, float DustTicks);

    private sealed class TrackMarkBatch
    {
        private const int Capacity = 768;
        private const int LifetimeSeconds = 12;
        private const int ClockSeconds = 16;
        private readonly int _lifetimeTicks = Engine.PhysicsTicksPerSecond * LifetimeSeconds;
        private readonly int _clockTicks = Engine.PhysicsTicksPerSecond * ClockSeconds;
        private readonly Mark[] _marks = new Mark[Capacity];
        private readonly MultiMesh _instances;
        private readonly ShaderMaterial _material;
        private int _cursor;
        private int _count;
        private long _latestExpiry;

        public TrackMarkBatch(Node3D battlefield)
        {
            using var quad = new PlaneMesh { Size = Vector2.One, SubdivideWidth = 0, SubdivideDepth = 0 };
            _instances = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true, UseCustomData = true, Mesh = quad,
                InstanceCount = Capacity, VisibleInstanceCount = 0
            };
            _material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Shaders/track_marks.gdshader"), RenderPriority = -2
            };
            battlefield.AddChild(new MultiMeshInstance3D
            {
                Name = "TrackMarks", Multimesh = _instances, MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }

        public void Stamp(Vector3 point, Vector3 forward, Color paint, long tick, bool friendly)
        {
            var basis = new Basis(forward.Cross(Vector3.Up) * 0.74f,
                Vector3.Up, -forward * (TankMotion.TreadMarkSpacing + 0.07f));
            _instances.SetInstanceTransform(_cursor, new Transform3D(basis, point + Vector3.Up * 0.024f));
            // Compatibility stores instance data at reduced precision, so wrap the phase in a short interval.
            _instances.SetInstanceCustomData(_cursor, new Color(ClockPhase(tick), 0f, 0f, 0f));
            _instances.SetInstanceColor(_cursor, paint);
            _latestExpiry = tick + _lifetimeTicks;
            _marks[_cursor] = new Mark(new SimVector(point.X, point.Z), paint, _latestExpiry, friendly, true);
            _cursor = (_cursor + 1) % Capacity;
            _count = Math.Min(Capacity, _count + 1);
        }

        public void Advance(long tick, BattlefieldFog fog)
        {
            if (_count == 0)
                return;
            if (tick >= _latestExpiry)
            {
                Reset();
                return;
            }

            _material.SetShaderParameter("clock_phase", ClockPhase(tick));
            var lastVisible = 0;
            for (var index = 0; index < _count; index++)
            {
                ref var mark = ref _marks[index];
                var visible = tick < mark.Expires && (mark.Friendly || fog.CanSeeGround(mark.Point));
                if (visible != mark.Visible)
                {
                    _instances.SetInstanceColor(index, new Color(mark.Paint, visible ? mark.Paint.A : 0f));
                    mark = mark with { Visible = visible };
                }
                if (visible)
                    lastVisible = index + 1;
            }
            _instances.VisibleInstanceCount = lastVisible;
        }

        public void Reset()
        {
            _instances.VisibleInstanceCount = 0;
            _cursor = 0;
            _count = 0;
            _latestExpiry = 0;
        }

        private float ClockPhase(long tick) => (tick % _clockTicks) / (float)_clockTicks;

        private readonly record struct Mark(SimVector Point, Color Paint, long Expires, bool Friendly, bool Visible);
    }
}
