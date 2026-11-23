using Godot;

namespace IronDominion.Game;

internal sealed partial class BattlefieldEffects
{
    private static readonly ImpactLook Soil = new(new("b6976b"), new("755b3e"), new("3e3022"), 1.66f, 23f, 5);
    private static readonly ImpactLook Concrete = new(new("c1bbab"), new("dfd9c8"), new("5f594b"), 0.98f, 12f, 7);
    private static readonly ImpactLook Asphalt = new(new("8f8a7f"), new("5c594f"), new("312e29"), 0.91f, 11f, 5);
    private static readonly ImpactLook Armor = new(new("646761"), new("ffd3a0"), new("372923"), 0.64f, 8.5f, 11);

    private void EmitSurfaceImpact(Visibility visibility, double time, SurfaceImpact hit, Vector3 direction, bool heavy)
    {
        if (!_impactBatches.TryGetValue(hit.Material, out var batch))
        {
            using var shader = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/battlefield_effect.gdshader") };
            batch = new EffectBatch(_battlefield, $"{hit.Material}Impacts", shader, 120);
            _impactBatches.Add(hit.Material, batch);
        }

        var look = hit.Material switch
        {
            ImpactMaterial.Armor => Armor,
            ImpactMaterial.Concrete => Concrete,
            ImpactMaterial.Asphalt => Asphalt,
            _ => Soil
        };
        var armor = hit.Material == ImpactMaterial.Armor;
        var soil = hit.Material == ImpactMaterial.Soil;
        var scale = heavy ? 1f : 0.32f;
        var lifetime = look.CloudTicks * (heavy ? 1f : 0.62f);
        var point = hit.Point + hit.Normal * 0.10f;
        var tangent = direction.Slide(hit.Normal).Normalized();
        var rise = hit.Normal * (soil ? 2.7f : 1.8f) + Vector3.Up * (armor ? 0.54f : 0.74f);
        var plume = soil && heavy ? point + hit.Normal * 0.43f : point;
        var opacity = armor ? 0.42f : soil && heavy ? 0.87f : 0.71f;
        batch.Emit(new Particle(visibility, time + 0.08, lifetime, plume,
            rise * (heavy ? 1f : 0.53f), look.CloudRadius * scale, look.Dust, opacity, Shape.Cloud));

        if (heavy && soil)
        {
            batch.Emit(new Particle(visibility, time + 1.4, lifetime + 3f, plume,
                Vector3.Up * 1.44f - tangent * 1.1f, 1.28f, new Color("8b6c4a"), 0.72f, Shape.Cloud));
        }
        else if (armor || heavy)
        {
            batch.Emit(new Particle(visibility, time, heavy ? 1.55f : 0.72f, point,
                hit.Normal * 2.0f, (armor ? 0.78f : 0.47f) * scale,
                armor ? RifleFlash : look.Chip, 0.93f, heavy ? Shape.Blast : Shape.Flash));
        }

        var across = hit.Normal.Cross(Mathf.Abs(hit.Normal.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        var along = hit.Normal.Cross(across);
        var count = heavy ? look.Fragments : armor ? 3 : 2;
        for (var index = 0; index < count; index++)
        {
            var angle = index * 2.399963f + (float)(time % 4d);
            var fan = across * Mathf.Cos(angle) + along * Mathf.Sin(angle);
            var velocity = hit.Normal * (heavy ? 3.2f : 1.5f)
                + fan * (1.45f + (index % 4) * 0.78f) + Vector3.Up * (soil ? 1.55f : 0.82f);
            var size = (0.135f + (index % 3) * 0.04f) * (heavy ? 1f : 0.50f);
            batch.Emit(new Particle(visibility, time + index * 0.067, (heavy ? 7f : 4f) + index % 3,
                point, velocity, size, look.Chip, 0.97f, armor ? Shape.Spark : Shape.Chip));
        }

        var mark = hit.Point + hit.Normal * (hit.Anchor is null ? 0.035f : 0.058f);
        var normal = hit.Normal;
        if (hit.Anchor is { } anchor)
        {
            mark = anchor.ToLocal(mark);
            normal = (anchor.GlobalBasis.Inverse() * normal).Normalized();
        }
        var markVisibility = new Visibility(null, hit.Anchor is null ? null : visibility.Target);
        var scar = (armor ? 0.53f : 0.96f) * (heavy ? 1f : 0.25f);
        batch.Emit(new Particle(markVisibility, time, Engine.PhysicsTicksPerSecond * (heavy ? 8f : 4.5f),
            mark, Vector3.Zero, scar, look.Scar, 0.87f, Shape.Scar, normal, hit.Anchor));
    }

    private readonly record struct ImpactLook(Color Dust, Color Chip, Color Scar, float CloudRadius, float CloudTicks,
        int Fragments);
}
