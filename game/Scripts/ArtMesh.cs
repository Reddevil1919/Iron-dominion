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

    public void Box(Vector3 center, Vector3 size, string color) => Box(center, size, new Color(color));

    public void Box(Vector3 center, Vector3 size, Color color, Basis? rotation = null) =>
        Add(Cube, center, size, color, rotation ?? Basis.Identity);

    public void Cylinder(Vector3 center, Vector3 size, Color color, Basis? rotation = null) =>
        Add(Round, center, size, color, rotation ?? Basis.Identity);

    public void Ellipsoid(Vector3 center, Vector3 size, string color, Basis? rotation = null) =>
        Add(Sphere, center, size, new Color(color), rotation ?? Basis.Identity);

    public void Conifer(Vector3 center, Vector3 size, Color color) =>
        Add(Cone, center, size, color, Basis.Identity);

    private void Add(Geometry geometry, Vector3 center, Vector3 size, Color color, Basis rotation)
    {
        var firstVertex = _vertices.Count;
        var transform = rotation.ScaledLocal(size);
        var normalTransform = transform.Inverse().Transposed();
        for (var i = 0; i < geometry.Vertices.Length; i++)
        {
            _vertices.Add(center + transform * geometry.Vertices[i]);
            _normals.Add((normalTransform * geometry.Normals[i]).Normalized());
            _colors.Add(color);
        }

        foreach (var index in geometry.Indices)
            _indices.Add(firstVertex + index);
    }

    public ArrayMesh Build()
    {
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Surface);
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
