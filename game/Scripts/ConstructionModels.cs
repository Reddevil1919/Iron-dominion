using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal static class ConstructionModels
{
    public static float Height(BuildingKind kind) => kind switch
    {
        BuildingKind.Headquarters => 6f,
        BuildingKind.TankFactory => 4.8f,
        BuildingKind.Airport => 3f,
        BuildingKind.ResearchCenter => 4.6f,
        _ => 3.4f
    };

    public static void AddSuperstructure(Node3D parent, BuildingDefinition definition, Material wall, Material? detail = null)
    {
        detail ??= wall;
        var plan = definition.Construction!;
        var w = plan.Width;
        var d = plan.Depth;
        var h = Height(definition.Kind);
        switch (definition.Kind)
        {
            case BuildingKind.Headquarters:
                Box(parent, new Vector3(0, 2, 0), new Vector3(w - 1, 4, d - 1), wall);
                Box(parent, new Vector3(0, 5, -0.8f), new Vector3(w * 0.55f, 2, d * 0.58f), detail);
                break;
            case BuildingKind.Barracks:
                foreach (var x in new[] { -w / 4, w / 4 })
                {
                    Box(parent, new Vector3(x, 1.5f, 0), new Vector3(w * 0.42f, 3, d - 1), wall);
                    Box(parent, new Vector3(x, 3.15f, 0), new Vector3(w * 0.44f, 0.3f, d - 0.7f), detail);
                }
                break;
            case BuildingKind.TankFactory:
                Box(parent, new Vector3(0, h / 2, -0.35f), new Vector3(w - 1, h, d - 1.4f), wall);
                Box(parent, new Vector3(0, 1.6f, d / 2 - 0.99f), new Vector3(w * 0.54f, 3.2f, 0.28f), detail);
                break;
            case BuildingKind.Airport:
                Box(parent, new Vector3(0, 0.13f, 0), new Vector3(w - 0.4f, 0.26f, d - 0.4f), wall);
                Box(parent, new Vector3(-w * 0.35f, h / 2, -d / 3), new Vector3(w * 0.23f, h, d / 4), detail);
                Box(parent, new Vector3(2, 0.29f, 1), new Vector3(w * 0.5f, 0.06f, 0.35f), detail);
                break;
            case BuildingKind.ResearchCenter:
                Box(parent, new Vector3(0, h / 2 - 0.6f, 0), new Vector3(w - 1.3f, h - 1.2f, d - 1.2f), wall);
                foreach (var x in new[] { -w / 4, w / 4 })
                    Box(parent, new Vector3(x, h - 0.6f, -1), new Vector3(w / 4, 1.2f, d * 0.42f), detail);
                break;
        }
    }

    public static void Box(Node3D parent, Vector3 position, Vector3 size, Material material)
    {
        parent.AddChild(new MeshInstance3D
        {
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            CastShadow = material is StandardMaterial3D { ShadingMode: BaseMaterial3D.ShadingModeEnum.Unshaded }
                ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On
        });
    }
}
