using Godot;

namespace IronDominion.Game;

internal sealed class ArtMesh
{
    private static readonly StandardMaterial3D Surface = new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 0.86f
    };

    private static readonly Geometry Cube = Geometry.From(new BoxMesh());
    private static readonly Geometry Round = Geometry.From(new CylinderMesh { Height = 1f, RadialSegments = 12, Rings = 1 });
    private static readonly Geometry Sphere = Geometry.From(new SphereMesh { RadialSegments = 10, Rings = 5 });
    private static readonly Geometry Cone = Geometry.From(new CylinderMesh
    {
        Height = 1f, TopRadius = 0f, RadialSegments = 7, Rings = 1
    });

    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<Color> _colors = new();
    private readonly List<int> _indices = new();
    private readonly List<Vector2> _clothCoordinates = new();
    private readonly List<int> _bones = new();
    private readonly List<float> _weights = new();
    private Vector3 _origin;
    private int _boneOffset = -1;
    private int _bone;

    public void BeginSkinCopy(Vector3 origin, int boneOffset)
    {
        _origin = origin;
        _boneOffset = boneOffset;
    }

    public void BindTo(int bone) => _bone = bone;

    public void Box(Vector3 center, Vector3 size, string color) => Box(center, size, new Color(color));

    public void Box(Vector3 center, Vector3 size, Color color, Basis? rotation = null) =>
        Add(Cube, center, size, color, rotation ?? Basis.Identity);

    public void Cylinder(Vector3 center, Vector3 size, Color color, Basis? rotation = null) =>
        Add(Round, center, size, color, rotation ?? Basis.Identity);

    public void Ellipsoid(Vector3 center, Vector3 size, string color, Basis? rotation = null) =>
        Add(Sphere, center, size, new Color(color), rotation ?? Basis.Identity);

    public void Ellipsoid(Vector3 center, Vector3 size, Color color, Basis? rotation = null) =>
        Add(Sphere, center, size, color, rotation ?? Basis.Identity);

    // Rings are ordered from top to bottom. Blends follow the cloth across a joint.
    public void SkinnedShell(Color color, int bone, int nextBone, params Contour[] rings)
    {
        const int sides = 10;
        var first = _vertices.Count;
        foreach (var ring in rings)
        {
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Mathf.Tau / sides;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var point = ring.Center + new Vector3(radial.X * ring.Radius.X, 0f, radial.Z * ring.Radius.Y);
                var normal = new Vector3(radial.X / ring.Radius.X, 0f, radial.Z / ring.Radius.Y).Normalized();
                AddVertex(point, normal, color, bone, nextBone, ring.Blend);
            }
        }

        for (var row = 0; row < rings.Length - 1; row++)
        for (var side = 0; side < sides; side++)
        {
            var a = first + row * sides + side;
            var b = first + row * sides + (side + 1) % sides;
            _indices.AddRange([a, b, a + sides, b, b + sides, a + sides]);
        }
    }

    public readonly record struct Contour(Vector3 Center, Vector2 Radius, float Blend);

    public void Conifer(Vector3 center, Vector3 size, Color color) =>
        Add(Cone, center, size, color, Basis.Identity);

    private void Add(Geometry geometry, Vector3 center, Vector3 size, Color color, Basis rotation)
    {
        var firstVertex = _vertices.Count;
        var transform = rotation.ScaledLocal(size);
        var normalTransform = transform.Inverse().Transposed();
        for (var i = 0; i < geometry.Vertices.Length; i++)
        {
            var point = center + transform * geometry.Vertices[i];
            var normal = (normalTransform * geometry.Normals[i]).Normalized();
            AddVertex(point, normal, color, _bone, _bone, 0f);
        }

        foreach (var index in geometry.Indices)
            _indices.Add(firstVertex + index);
    }

    private void AddVertex(Vector3 point, Vector3 normal, Color color, int bone, int nextBone, float blend)
    {
        _vertices.Add(point + _origin);
        _normals.Add(normal);
        _colors.Add(color);
        if (_boneOffset < 0)
            return;

        _clothCoordinates.Add(new Vector2(point.X + point.Z * 0.78f, point.Y + point.Z * 0.33f));
        _bones.AddRange([_boneOffset + bone, _boneOffset + nextBone, 0, 0]);
        _weights.AddRange([1f - blend, blend, 0f, 0f]);
    }

    public ArrayMesh Build(Material? material = null)
    {
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
        if (_boneOffset >= 0)
        {
            arrays[(int)Mesh.ArrayType.TexUV] = _clothCoordinates.ToArray();
            arrays[(int)Mesh.ArrayType.Bones] = _bones.ToArray();
            arrays[(int)Mesh.ArrayType.Weights] = _weights.ToArray();
        }

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material ?? Surface);
        return mesh;
    }

    private sealed record Geometry(Vector3[] Vertices, Vector3[] Normals, int[] Indices)
    {
        public static Geometry From(PrimitiveMesh mesh)
        {
            using (mesh)
            {
                using var arrays = mesh.GetMeshArrays();
                return new Geometry(arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
                    arrays[(int)Mesh.ArrayType.Normal].AsVector3Array(),
                    arrays[(int)Mesh.ArrayType.Index].AsInt32Array());
            }
        }
    }
}
