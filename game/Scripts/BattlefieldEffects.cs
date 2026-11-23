using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldEffects
{
    private static readonly Color WarmFlash = new("ffe0a0");
    private static readonly Color Smoke = new("73746d");
    private static readonly Color Dust = new("b09a7d");
    private static readonly Color Ember = new("ffa451");

    private readonly EffectBatch _combat;
    private readonly EffectBatch _tracers;
    private readonly EffectBatch _dust;
    private long _tick = -1;

    public BattlefieldEffects(Node3D battlefield)
    {
        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/battlefield_effect.gdshader") };
        _combat = new EffectBatch(battlefield, "CombatBursts", material, 120);
        _tracers = new EffectBatch(battlefield, "CombatTracers", material, 32);
        _dust = new EffectBatch(battlefield, "TrackDust", material, 48);
    }

    public void Fire(ShotFired shot, Vector3 muzzle, Vector3 target, float targetRadius, bool heavy,
        bool sourceDestroyed, bool targetDestroyed)
    {
        var span = target - muzzle;
        var distance = span.Length();
        var direction = span.Normalized();
        var hit = target - direction * Mathf.Min(targetRadius, distance * 0.55f);
        var path = hit - muzzle;
        var travel = path.Length();
        var visibility = new Visibility(sourceDestroyed ? null : shot.Attacker, targetDestroyed ? null : shot.Target);
        var tickRate = Engine.PhysicsTicksPerSecond;

        // The flight is visual timing. Damage has already resolved in the simulation.
        var flight = heavy ? Mathf.Clamp(travel * 0.21f, 1.45f, 3.1f) : Mathf.Clamp(travel * 0.12f, 0.65f, 1.65f);
        var arrival = (double)shot.Tick + flight;
        var flashPosition = muzzle + direction * (heavy ? 0.17f : 0.09f);
        _combat.Emit(new Particle(visibility, shot.Tick, heavy ? 2.05f : 1.0f, flashPosition,
            direction * 2.2f, heavy ? 0.74f : 0.30f, WarmFlash, 0.93f, Shape.Flash));
        _combat.Emit(new Particle(visibility, shot.Tick + 0.12, heavy ? 14f : 7f, flashPosition,
            direction * (heavy ? 1.10f : 0.62f) + Vector3.Up * 0.94f,
            heavy ? 0.80f : 0.36f, Smoke, heavy ? 0.47f : 0.24f, Shape.Cloud));

        if (travel > 0.45f && (heavy || DrawInfantryTracer(shot)))
            _tracers.Emit(new Particle(visibility, shot.Tick, flight, muzzle, path * (tickRate / flight),
                heavy ? 0.112f : 0.061f, WarmFlash, 0.92f, Shape.Glow, heavy ? 0.82f : 0.49f));

        var outward = -direction * 1.15f + Vector3.Up * 0.88f;
        _combat.Emit(new Particle(visibility, arrival, heavy ? 16f : 10f, hit, outward,
            heavy ? 0.86f : 0.33f, Dust, heavy ? 0.56f : 0.33f, Shape.Cloud));
        _combat.Emit(new Particle(visibility, arrival, heavy ? 2.3f : 1.25f, hit,
            outward, heavy ? 0.56f : 0.23f, Ember, 0.81f, Shape.Flash));
        if (heavy)
        {
            var sideways = direction.Cross(Vector3.Up).Normalized() * 1.8f;
            _combat.Emit(new Particle(visibility, arrival, 4.2f, hit, outward * 2.8f + sideways,
                0.17f, Ember, 0.90f, Shape.Glow));
            _combat.Emit(new Particle(visibility, arrival + 0.24, 4.7f, hit, outward * 2.1f - sideways,
                0.135f, WarmFlash, 0.78f, Shape.Glow));
        }
    }

    public void TrackDust(EntityId tank, long tick, Vector3 position) =>
        _dust.Emit(new Particle(new Visibility(tank, null), tick, 14f, position, Vector3.Up * 0.73f,
            0.48f, new Color("a38d69"), 0.36f, Shape.Cloud));

    public void Advance(long tick, IReadOnlySet<EntityId> visibleActors)
    {
        _tick = tick;
        _combat.Prune(tick, visibleActors);
        _tracers.Prune(tick, visibleActors);
        _dust.Prune(tick, visibleActors);
    }

    public void Render(double fraction)
    {
        if (_tick < 0)
            return;

        var time = _tick + fraction;
        _combat.Render(time);
        _tracers.Render(time);
        _dust.Render(time);
    }

    public void Reset()
    {
        _tick = -1;
        _combat.Reset();
        _tracers.Reset();
        _dust.Reset();
    }

    private static bool DrawInfantryTracer(ShotFired shot)
    {
        // Mix the event tick with the squad ID so a fixed fire cadence still varies the accents.
        var value = unchecked((uint)shot.Tick * 1103515245u + (uint)shot.Attacker.Value * 2654435761u);
        return (value >> 29) == 0;
    }

    private enum Shape { Cloud, Flash, Glow }

    private readonly record struct Visibility(EntityId? Source, EntityId? Target)
    {
        public bool IsVisible(IReadOnlySet<EntityId> actors) =>
            (Source is not { } source || actors.Contains(source))
            && (Target is not { } target || actors.Contains(target));
    }

    private readonly record struct Particle(Visibility Visibility, double Born, float Lifetime,
        Vector3 Origin, Vector3 Velocity, float Radius, Color Color, float Opacity, Shape Shape,
        float Length = 0f);

    private sealed class EffectBatch
    {
        private readonly int _capacity;
        private readonly ImmediateMesh _mesh = new();
        private readonly MeshInstance3D _view;
        private readonly List<Particle> _particles = new();
        private double _lastTime = -1;

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
            _lastTime = -1;
        }

        public void Prune(long tick, IReadOnlySet<EntityId> actors)
        {
            if (_particles.Count == 0)
                return;

            var removed = _particles.RemoveAll(particle => tick - particle.Born >= particle.Lifetime
                || !particle.Visibility.IsVisible(actors));
            if (removed > 0)
                _lastTime = -1;
        }

        public void Render(double time)
        {
            if (_lastTime == time || (_particles.Count == 0 && !_view.Visible))
                return;
            _lastTime = time;

            var camera = _view.GetViewport().GetCamera3D()?.GlobalBasis ?? Basis.Identity;
            _mesh.ClearSurfaces();
            var drawing = false;
            foreach (var particle in _particles)
            {
                var age = (float)(time - particle.Born);
                if (age < 0f || age >= particle.Lifetime)
                    continue;
                var movement = particle.Velocity * (age / Engine.PhysicsTicksPerSecond);
                if (particle.Length > 0f && movement.LengthSquared() < 0.001f)
                    continue;

                if (!drawing)
                {
                    _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                    drawing = true;
                }

                var progress = age / particle.Lifetime;
                _mesh.SurfaceSetColor(new Color(particle.Color, particle.Opacity * (1f - progress)));
                var center = particle.Origin + movement;
                if (particle.Length > 0f)
                    Streak(center, movement, particle, camera);
                else
                    Burst(center, particle, progress, camera);
            }

            if (drawing)
                _mesh.SurfaceEnd();
            _view.Visible = drawing;
        }

        private void Streak(Vector3 head, Vector3 movement, Particle particle, Basis camera)
        {
            var direction = movement.Normalized();
            var along = direction * (Mathf.Min(movement.Length(), particle.Length) * 0.5f);
            var across = camera.Z.Cross(direction);
            var width = across.LengthSquared() > 0.001f ? across.Normalized() : camera.Y;
            Quad(head - along, along, width * particle.Radius, particle.Shape);
        }

        private void Burst(Vector3 center, Particle particle, float progress, Basis camera)
        {
            var scale = particle.Shape == Shape.Cloud ? 0.63f + progress * 1.25f : 1f - progress * 0.63f;
            var size = particle.Radius * scale;
            var x = camera.X;
            var y = camera.Y;
            if (particle.Shape == Shape.Flash && particle.Velocity.LengthSquared() > 0.001f)
            {
                var projected = particle.Velocity.Slide(camera.Z);
                if (projected.LengthSquared() > 0.001f)
                {
                    x = projected.Normalized() * 1.35f;
                    y = camera.Z.Cross(x).Normalized();
                }
            }

            Quad(center, x * size, y * size, particle.Shape);
        }

        private void Quad(Vector3 center, Vector3 x, Vector3 y, Shape shape)
        {
            var low = (int)shape;
            const float edge = 0.0001f;
            var high = low + 1f - edge;
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
            _lastTime = -1;
            _particles.Clear();
            _mesh.ClearSurfaces();
            _view.Hide();
        }
    }
}
