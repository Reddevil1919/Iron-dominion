using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldEffects
{
    private readonly EffectBatch _combat;
    private readonly EffectBatch _dust;

    public BattlefieldEffects(Node3D battlefield)
    {
        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/battlefield_effect.gdshader") };
        _combat = new EffectBatch(battlefield, "CombatBursts", material, 96);
        _dust = new EffectBatch(battlefield, "TrackDust", material, 48);
    }

    public void Fire(ShotFired shot, Vector3 muzzle, Vector3 target, float targetRadius, bool heavy)
    {
        var direction = (target - muzzle).Normalized();
        var hit = target - direction * Mathf.Min(targetRadius, muzzle.DistanceTo(target) * 0.55f);
        var source = shot.Attacker;
        var destination = shot.Target;
        _combat.Emit(new Particle(source, destination, shot.Tick, heavy ? 3 : 2, muzzle, direction * 1.4f,
            heavy ? 0.65f : 0.31f, new Color("fff6bf"), 0.95f, true));
        _combat.Emit(new Particle(source, destination, shot.Tick, heavy ? 10 : 7, hit,
            -direction * 0.65f + Vector3.Up * 0.8f, heavy ? 0.55f : 0.30f, new Color("b09a7d"), 0.44f, false));
        _combat.Emit(new Particle(source, destination, shot.Tick, 4, hit,
            Vector3.Up * 2.75f - direction * 1.5f, heavy ? 0.38f : 0.19f, new Color("ffcc77"), 0.85f, true));
    }

    public void TrackDust(EntityId tank, long tick, Vector3 position) =>
        _dust.Emit(new Particle(tank, null, tick, 14, position, Vector3.Up * 0.73f,
            0.48f, new Color("a38d69"), 0.36f, false));

    public void Update(long tick, IReadOnlySet<EntityId> visibleActors)
    {
        _combat.Update(tick, visibleActors);
        _dust.Update(tick, visibleActors);
    }

    public void Reset()
    {
        _combat.Reset();
        _dust.Reset();
    }

    private readonly record struct Particle(EntityId Source, EntityId? Target, long Born, int Lifetime,
        Vector3 Origin, Vector3 Velocity, float Radius, Color Color, float Opacity, bool Flash);

    private sealed class EffectBatch
    {
        private readonly int _capacity;
        private readonly ImmediateMesh _mesh = new();
        private readonly MeshInstance3D _view;
        private readonly List<Particle> _particles = new();
        private long _lastTick = -1;

        public EffectBatch(Node3D battlefield, string name, ShaderMaterial material, int capacity)
        {
            _capacity = capacity;
            _view = new MeshInstance3D
            {
                Name = name, Mesh = _mesh, Visible = false,
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            battlefield.AddChild(_view);
        }

        public void Emit(Particle particle)
        {
            if (_particles.Count == _capacity)
                _particles.RemoveAt(0);
            _particles.Add(particle);
        }

        public void Update(long tick, IReadOnlySet<EntityId> visibleActors)
        {
            if (_lastTick == tick)
                return;
            _lastTick = tick;

            _particles.RemoveAll(particle => tick - particle.Born >= particle.Lifetime
                || !visibleActors.Contains(particle.Source)
                || (particle.Target is { } target && !visibleActors.Contains(target)));
            if (_particles.Count == 0)
            {
                if (_view.Visible)
                {
                    _mesh.ClearSurfaces();
                    _view.Hide();
                }
                return;
            }

            var camera = _view.GetViewport().GetCamera3D()?.GlobalBasis ?? Basis.Identity;
            _mesh.ClearSurfaces();
            // The shared material is held by the view. One surface batches every active quad.
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            foreach (var particle in _particles)
            {
                var age = tick - particle.Born;
                var progress = (float)age / particle.Lifetime;
                var opacity = particle.Opacity * (1f - progress);
                var radius = particle.Radius * (particle.Flash ? 1f - progress * 0.37f : 0.66f + progress * 1.05f);
                var center = particle.Origin + particle.Velocity * (age / (float)Engine.PhysicsTicksPerSecond);
                _mesh.SurfaceSetColor(new Color(particle.Color, opacity));
                Quad(center, camera.X * radius, camera.Y * radius, particle.Flash);
            }
            _mesh.SurfaceEnd();
            _view.Show();
        }

        private void Quad(Vector3 center, Vector3 x, Vector3 y, bool flash)
        {
            var low = flash ? 1f : 0f;
            var high = low + 1f;
            Vertex(center - x - y, low, 0f);
            Vertex(center + x - y, high, 0f);
            Vertex(center - x + y, low, 1f);
            Vertex(center - x + y, low, 1f);
            Vertex(center + x - y, high, 0f);
            Vertex(center + x + y, high, 1f);
        }

        private void Vertex(Vector3 position, float x, float y)
        {
            _mesh.SurfaceSetUV(new Vector2(x, y));
            _mesh.SurfaceAddVertex(position);
        }

        public void Reset()
        {
            _lastTick = -1;
            _particles.Clear();
            _mesh.ClearSurfaces();
            _view.Hide();
        }
    }
}
