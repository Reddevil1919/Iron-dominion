using Godot;

namespace IronDominion.Game;

internal enum ImpactMaterial { Soil, Concrete, Asphalt, Armor }
internal enum GroundMaterial { Soil, Gravel, Concrete, Asphalt }

internal readonly record struct GroundContact(Vector3 Point, GroundMaterial Material);

internal readonly record struct ShotSurface(Vector3 Center, MeshInstance3D? Mesh = null,
    ImpactMaterial Material = ImpactMaterial.Soil);

internal readonly record struct SurfaceImpact(Vector3 Point, Vector3 Normal, ImpactMaterial Material, MeshInstance3D? Anchor = null);

internal sealed class DepotImpactSurfaces
{
    private readonly Vector4[] _roads;
    private readonly Vector4[] _mirroredSlabs;
    private readonly Vector4[] _localSlabs;
    private readonly Aabb[] _gravel;
    private readonly float _groundHeight;

    public DepotImpactSurfaces(MeshInstance3D ground, IEnumerable<MeshInstance3D> gravel)
    {
        var material = (ShaderMaterial)ground.Mesh.SurfaceGetMaterial(0);
        _roads = ReadRectangles(material, "roads", 3);
        _mirroredSlabs = ReadRectangles(material, "mirrored_slabs", 2);
        _localSlabs = ReadRectangles(material, "local_slabs", 3);
        _gravel = gravel.Select(parcel => parcel.GlobalTransform * parcel.GetAabb()).ToArray();
        _groundHeight = (ground.GlobalTransform * ground.GetAabb()).End.Y;
    }

    public SurfaceImpact Resolve(ShotSurface target, Vector3 muzzle, float spread)
    {
        if (target.Mesh is { } mesh)
            return FaceOf(mesh, muzzle, target.Material, spread);

        var outward = (muzzle - target.Center) with { Y = 0f };
        outward = outward.Normalized();
        var point = target.Center + outward * 0.68f + outward.Cross(Vector3.Up) * spread;
        var ground = GroundAt(point);
        var material = ground.Material switch
        {
            GroundMaterial.Concrete => ImpactMaterial.Concrete,
            GroundMaterial.Asphalt => ImpactMaterial.Asphalt,
            _ => ImpactMaterial.Soil
        };
        return new SurfaceImpact(ground.Point, Vector3.Up, material);
    }

    public GroundContact GroundAt(Vector3 point)
    {
        foreach (var parcel in _gravel)
        {
            if (point.X >= parcel.Position.X && point.X <= parcel.End.X
                && point.Z >= parcel.Position.Z && point.Z <= parcel.End.Z)
                return new GroundContact(new Vector3(point.X, parcel.End.Y, point.Z), GroundMaterial.Gravel);
        }

        var local = new Vector2(point.X, point.Z);
        var mirrored = local.Abs();
        var material = Inside(_mirroredSlabs, mirrored) || Inside(_localSlabs, local)
            ? GroundMaterial.Concrete
            : Inside(_roads, mirrored) ? GroundMaterial.Asphalt : GroundMaterial.Soil;
        return new GroundContact(new Vector3(point.X, _groundHeight, point.Z), material);
    }

    public static SurfaceImpact WeldOnArmor(MeshInstance3D armor, Vector3 source, float seam) =>
        FaceOf(armor, source, ImpactMaterial.Armor, seam);

    private static SurfaceImpact FaceOf(MeshInstance3D mesh, Vector3 source, ImpactMaterial material, float spread)
    {
        var bounds = mesh.GetAabb();
        var half = bounds.Size * 0.5f;
        var center = bounds.GetCenter();
        var from = mesh.ToLocal(source) - center;
        // Aim the contact at the painted hull plate or lower wall, below rooftop hardware.
        if (material == ImpactMaterial.Armor)
            half = new Vector3(1.76f, half.Y, 2.52f);
        else
            half = new Vector3(5.22f, half.Y, 4.23f);

        var x = from.X / half.X;
        var z = from.Z / half.Z;
        var normal = Mathf.Abs(x) >= Mathf.Abs(z)
            ? Vector3.Right * (x < 0f ? -1f : 1f)
            : Vector3.Back * (z < 0f ? -1f : 1f);
        var scale = 1f / Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
        var hit = new Vector3(center.X + from.X * scale,
            material == ImpactMaterial.Armor ? 1.10f : 1.72f, center.Z + from.Z * scale);
        if (normal.X != 0f)
        {
            hit.X = center.X + normal.X * half.X;
            hit.Z = Mathf.Clamp(hit.Z + spread, center.Z - half.Z + 0.26f, center.Z + half.Z - 0.26f);
        }
        else
        {
            hit.Z = center.Z + normal.Z * half.Z;
            hit.X = Mathf.Clamp(hit.X + spread, center.X - half.X + 0.26f, center.X + half.X - 0.26f);
        }
        return new SurfaceImpact(mesh.ToGlobal(hit), (mesh.GlobalBasis * normal).Normalized(), material, mesh);
    }

    private static bool Inside(Vector4[] rectangles, Vector2 point)
    {
        foreach (var bounds in rectangles)
            if (Mathf.Abs(point.X - bounds.X) <= bounds.Z && Mathf.Abs(point.Y - bounds.Y) <= bounds.W)
                return true;
        return false;
    }

    private static Vector4[] ReadRectangles(ShaderMaterial material, string parameter, int count)
    {
        using var value = material.GetShaderParameter(parameter);
        var rectangles = value.AsVector4Array();
        if (rectangles.Length != count)
            throw new InvalidDataException($"Expected {count} rectangles for terrain shader parameter '{parameter}'.");
        return rectangles;
    }
}
