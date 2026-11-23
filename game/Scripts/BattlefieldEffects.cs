using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldEffects
{
    private static readonly Color RifleFlash = new("fff1db");
    private static readonly Color CannonFlash = new("fbc58c");
    private static readonly Color Smoke = new("74736b");
    private static readonly Color PaleSmoke = new("bcb8ac");
    private static readonly Color Dust = new("b09a7d");

    private readonly Node3D _battlefield;
    private readonly EffectBatch _rifles;
    private readonly EffectBatch _cannons;
    private readonly EffectBatch _shells;
    private readonly EffectBatch _dust;
    private EffectBatch? _explosions;
    private long _tick = -1;

    public BattlefieldEffects(Node3D battlefield)
    {
        _battlefield = battlefield;
        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/battlefield_effect.gdshader") };
        _rifles = new EffectBatch(battlefield, "RifleBursts", material, 144);
        _cannons = new EffectBatch(battlefield, "CannonBursts", material, 96);
        _shells = new EffectBatch(battlefield, "CannonShells", material, 24);
        _dust = new EffectBatch(battlefield, "TrackDust", material, 48);
    }

    public void Fire(ShotFired shot, UnitView attacker, Vector3 target, float targetRadius,
        bool sourceDestroyed, bool targetDestroyed)
    {
        var visibility = new Visibility(sourceDestroyed ? null : shot.Attacker, targetDestroyed ? null : shot.Target);
        if (attacker.Kind == UnitKind.Tank)
            FireCannon(visibility, shot.Tick, attacker.ShotOrigin(), target, targetRadius);
        else
            FireRifles(visibility, shot.Tick, attacker, target, targetRadius);
    }

    private void FireRifles(Visibility visibility, long tick, UnitView attacker, Vector3 target, float targetRadius)
    {
        var spacing = InfantryMotion.ShotIntervalSeconds * Engine.PhysicsTicksPerSecond;
        for (var pulse = 0; pulse < InfantryMotion.BurstShots; pulse++)
        {
            var born = tick + (double)spacing * pulse;
            var muzzle = attacker.ShotOrigin(InfantryMotion.Shooter(pulse));
            var direction = (target - muzzle).Normalized();
            var sideways = direction.Cross(Vector3.Up);
            var flash = muzzle + direction * 0.11f;
            _rifles.Emit(new Particle(visibility, born, 0.85f, flash, direction * 1.8f,
                0.34f, RifleFlash, 0.96f, Shape.Flash));
            if (pulse < 2)
                _rifles.Emit(new Particle(visibility, born + 0.24, 7.5f, flash,
                    direction * 0.82f + Vector3.Up * 0.60f, 0.31f, PaleSmoke, 0.40f, Shape.Cloud));

            var hit = ImpactPoint(muzzle, target, targetRadius) + sideways * (pulse - 1) * 0.43f;
            _rifles.Emit(new Particle(visibility, born + 0.8, 8.2f, hit,
                Vector3.Up * 1.2f - direction * 0.60f, 0.38f, Dust, 0.50f, Shape.Cloud));
        }
    }

    private void FireCannon(Visibility visibility, long tick, Vector3 muzzle, Vector3 target, float targetRadius)
    {
        var direction = (target - muzzle).Normalized();
        var sideways = direction.Cross(Vector3.Up).Normalized();
        var flash = muzzle + direction * 0.35f;
        _cannons.Emit(new Particle(visibility, tick, 1.85f, flash, direction * 4f,
            1.65f, CannonFlash, 0.98f, Shape.Blast));
        _cannons.Emit(new Particle(visibility, tick + 0.10, 17f, flash,
            direction * 2.1f + Vector3.Up * 1.5f, 1.55f, PaleSmoke, 0.72f, Shape.Cloud));
        _cannons.Emit(new Particle(visibility, tick + 0.45, 21f, flash + sideways * 0.34f,
            direction * 1.0f + Vector3.Up * 1.15f - sideways * 0.82f,
            1.18f, Smoke, 0.58f, Shape.Cloud));
        _cannons.Emit(new Particle(visibility, tick + 0.15, 13f, new Vector3(flash.X, 0.11f, flash.Z),
            direction * 1.8f, 2.6f, Dust, 0.48f, Shape.GroundDust));

        // This presentation flight follows the shot event. Simulation damage is already resolved.
        var hit = ImpactPoint(muzzle, target, targetRadius);
        var path = hit - muzzle;
        var travel = path.Length();
        var flight = Mathf.Clamp(travel * 0.18f, 1.15f, 2.7f);
        if (travel > 0.85f)
            _shells.Emit(new Particle(visibility, tick + 0.22, flight, muzzle,
                path * (Engine.PhysicsTicksPerSecond / flight), 0.22f, new Color("8c8571"), 1f, Shape.Shell));

        var arrival = tick + 0.22 + flight;
        var outward = -direction + Vector3.Up * 1.1f;
        _cannons.Emit(new Particle(visibility, arrival, 1.95f, hit, outward * 2.6f,
            1.12f, CannonFlash, 0.93f, Shape.Blast));
        _cannons.Emit(new Particle(visibility, arrival, 18f, hit, outward * 1.5f,
            1.42f, Dust, 0.71f, Shape.Cloud));
        _cannons.Emit(new Particle(visibility, arrival + 0.40, 21f, hit + sideways * 0.29f,
            outward + sideways, 1.30f, Smoke, 0.56f, Shape.Cloud));

        for (var fragment = 0; fragment < 4; fragment++)
        {
            var toss = outward * (3.5f + fragment * 0.4f) + sideways * (fragment - 1.5f) * 1.75f;
            _cannons.Emit(new Particle(visibility, arrival + fragment * 0.16, 5.0f + fragment * 0.65f,
                hit, toss, 0.18f - fragment * 0.023f, RifleFlash, 0.92f, Shape.Spark));
        }
    }

    private static Vector3 ImpactPoint(Vector3 muzzle, Vector3 target, float radius)
    {
        var span = target - muzzle;
        var distance = span.Length();
        return target - span.Normalized() * Mathf.Min(radius, distance * 0.67f);
    }

    public void ExplodeTank(long tick, Vector3 site)
    {
        if (_explosions is null)
        {
            using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/battlefield_effect.gdshader") };
            _explosions = new EffectBatch(_battlefield, "TankExplosions", material);
        }

        // This site was observed at destruction. The burst stays at that fixed location.
        var origin = site + Vector3.Up * 1.45f;
        _explosions.Emit(new Particle(default, tick, 5.2f, origin, Vector3.Up * 2.2f,
            3.7f, new Color("f98d48"), 0.94f, Shape.Blast));
        _explosions.Emit(new Particle(default, tick, 2.55f, origin + Vector3.Up * 0.45f, Vector3.Up * 3.0f,
            1.9f, CannonFlash, 1f, Shape.Blast));
        _explosions.Emit(new Particle(default, tick + 0.6, 26f, site + Vector3.Up * 0.1f, Vector3.Zero,
            4.35f, Dust, 0.68f, Shape.GroundDust));

        for (var lobe = 0; lobe < 5; lobe++)
        {
            var angle = lobe * 2.4f;
            var sideways = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            _explosions.Emit(new Particle(default, tick + 1.1 + lobe * 1.15, 39f + lobe * 2f,
                origin + sideways * (0.34f + lobe * 0.18f),
                sideways * 1.1f + Vector3.Up * (1.6f + lobe * 0.27f), 2.15f + lobe * 0.16f,
                new Color("393634"), 0.76f, Shape.Cloud));
        }
        for (var spark = 0; spark < 10; spark++)
        {
            var angle = spark * 2.4f;
            var velocity = new Vector3(Mathf.Cos(angle) * 6.6f, 4.1f + spark * 0.37f, Mathf.Sin(angle) * 6.6f);
            _explosions.Emit(new Particle(default, tick + spark * 0.14, 13f + spark * 0.42f,
                origin, velocity, 0.18f + spark % 3 * 0.035f, RifleFlash, 0.94f, Shape.Spark));
        }
    }

    public void TrackDust(EntityId tank, long tick, Vector3 position) =>
        _dust.Emit(new Particle(new Visibility(tank, null), tick, 14f, position, Vector3.Up * 0.73f,
            0.48f, new Color("a38d69"), 0.36f, Shape.Cloud));

    public void Advance(long tick, IReadOnlySet<EntityId> visibleActors)
    {
        _tick = tick;
        _rifles.Prune(tick, visibleActors);
        _cannons.Prune(tick, visibleActors);
        _shells.Prune(tick, visibleActors);
        _dust.Prune(tick, visibleActors);
        _explosions?.Prune(tick, visibleActors);
    }

    public void Render(double fraction)
    {
        if (_tick < 0)
            return;

        var time = _tick + fraction;
        _rifles.Render(time);
        _cannons.Render(time);
        _shells.Render(time);
        _dust.Render(time);
        _explosions?.Render(time);
    }

    public void Reset()
    {
        _tick = -1;
        _rifles.Reset();
        _cannons.Reset();
        _shells.Reset();
        _dust.Reset();
        _explosions?.Reset();
    }

    private enum Shape { Cloud, Flash, Spark, Blast, Shell, GroundDust }

    private readonly record struct Visibility(EntityId? Source, EntityId? Target)
    {
        public bool IsVisible(IReadOnlySet<EntityId> actors) =>
            (Source is not { } source || actors.Contains(source))
            && (Target is not { } target || actors.Contains(target));
    }

    private readonly record struct Particle(Visibility Visibility, double Born, float Lifetime,
        Vector3 Origin, Vector3 Velocity, float Radius, Color Color, float Opacity, Shape Shape);

    private sealed class EffectBatch
    {
        private readonly int? _capacity;
        private readonly ImmediateMesh _mesh = new();
        private readonly MeshInstance3D _view;
        private readonly List<Particle> _particles = new();
        private double _lastTime = -1;

        public EffectBatch(Node3D battlefield, string name, ShaderMaterial material, int? capacity = null)
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
                var seconds = age / Engine.PhysicsTicksPerSecond;
                var movement = particle.Velocity * seconds;
                if (particle.Shape == Shape.Spark)
                    movement.Y -= 7f * seconds * seconds;

                if (!drawing)
                {
                    _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                    drawing = true;
                }

                var progress = age / particle.Lifetime;
                var alpha = particle.Shape == Shape.Shell ? particle.Opacity : particle.Opacity * (1f - progress);
                _mesh.SurfaceSetColor(new Color(particle.Color, alpha));
                DrawParticle(particle.Origin + movement, particle, progress, camera);
            }

            if (drawing)
                _mesh.SurfaceEnd();
            _view.Visible = drawing;
        }

        private void DrawParticle(Vector3 center, Particle particle, float progress, Basis camera)
        {
            var scale = particle.Shape switch
            {
                Shape.Cloud => 0.56f + progress * 1.25f,
                Shape.GroundDust => 0.55f + progress * 1.64f,
                Shape.Blast => 0.79f + progress * 0.41f,
                Shape.Shell => 1f,
                _ => 1f - progress * 0.57f
            };
            var size = particle.Radius * scale;
            var x = camera.X;
            var y = camera.Y;
            if (particle.Shape == Shape.GroundDust)
            {
                x = Vector3.Right;
                y = Vector3.Back;
            }
            else if (particle.Shape is Shape.Flash or Shape.Shell)
            {
                var projected = particle.Velocity.Slide(camera.Z);
                if (projected.LengthSquared() > 0.001f)
                {
                    x = projected.Normalized() * 1.36f;
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
