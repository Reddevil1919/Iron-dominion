using Godot;
using static IronDominion.Game.InfantryRig;

namespace IronDominion.Game;

internal sealed class InfantryArt
{
    private static readonly Basis AlongZ = new(Vector3.Right, Mathf.Pi / 2f);

    private readonly Color _fabric;
    private readonly Color _gear;
    private readonly Color _trim;
    private readonly Color _boot;
    private readonly Color _identification;

    public ArrayMesh Mesh { get; }
    public Skin Skin { get; }

    public InfantryArt(Color identification, bool local)
    {
        _fabric = Finish(local ? "647264" : "887054", 0.20f);
        _gear = Finish(local ? "535a48" : "705e45", 0.48f);
        _trim = Finish(local ? "353c35" : "504638", 0.48f);
        _boot = Finish(local ? "3c4039" : "554938", 0.48f);
        _identification = new Color(identification, 1f);

        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/infantry_material.gdshader") };
        material.SetShaderParameter("canvas_shade", local ? new Vector3(0.73f, 0.80f, 0.78f)
            : new Vector3(0.80f, 0.66f, 0.53f));
        var mesh = new ArtMesh();
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            mesh.BeginSkinCopy(Offset(soldier), soldier * BonesPerSoldier);
            AddBody(mesh, soldier, local);
            AddSleeve(mesh, LeftShoulder);
            AddSleeve(mesh, RightShoulder);
            AddLeg(mesh, LeftHip);
            AddLeg(mesh, RightHip);
            AddEquipment(mesh);
        }

        Mesh = mesh.Build(material);
        Skin = CreateSkin();
    }

    private static Color Finish(string color, float material) => new(new Color(color), material);

    private void AddBody(ArtMesh mesh, int soldier, bool local)
    {
        mesh.SkinnedShell(_fabric, Pelvis, Chest,
            Ring(0f, 1.49f, 0.01f, 0.165f, 0.094f, 1f),
            Ring(0f, 1.425f, 0f, 0.228f, 0.116f, 1f),
            Ring(0f, 1.27f, 0.015f, 0.188f, 0.116f, 0.90f),
            Ring(0f, 1.10f, 0.018f, 0.164f, 0.105f, 0.30f),
            Ring(0f, 0.965f, 0.01f, 0.174f, 0.103f, 0f),
            Ring(0f, 0.875f, 0f, 0.155f, 0.083f, 0f));

        mesh.BindTo(Pelvis);
        mesh.Ellipsoid(new(0f, 1.038f, 0.009f), new(0.352f, 0.088f, 0.244f), _trim);
        mesh.Box(new(0.127f, 1.01f, 0.10f), new(0.10f, 0.13f, 0.083f), _gear);

        mesh.BindTo(Chest);
        mesh.Ellipsoid(new(0f, 1.318f, -0.092f), new(0.39f, 0.35f, 0.156f), _gear);
        mesh.Ellipsoid(new(0f, 1.335f, 0.101f), new(0.385f, 0.33f, 0.151f), _gear);
        foreach (var x in new[] { -0.102f, 0.102f })
        {
            mesh.Ellipsoid(new(x, 1.428f, -0.005f), new(0.064f, 0.161f, 0.263f), _trim);
            mesh.Box(new(x, 1.238f, -0.168f), new(0.088f, 0.138f, 0.058f), _trim);
            mesh.Box(new(x, 1.296f, -0.201f), new(0.080f, 0.036f, 0.013f), _gear);
        }

        var packDepth = local ? 0.152f : 0.115f;
        mesh.Ellipsoid(new(0f, 1.285f, 0.157f), new(0.29f, 0.325f, packDepth * 2f), _gear);
        mesh.Box(new(0f, 1.325f, 0.151f + packDepth), new(0.17f, 0.072f, 0.017f), _identification);
        mesh.Box(new(-0.143f, 1.297f, 0.193f), new(0.058f, 0.155f, 0.056f), _trim);
        if (soldier % 2 == 0)
            mesh.Cylinder(new(0.115f, 1.297f, 0.269f), new(0.076f, 0.116f, 0.080f), _gear);

        mesh.BindTo(Head);
        var skin = Finish(soldier % 2 == 0 ? "af8d73" : "8f6e57", 0.94f);
        mesh.Ellipsoid(new(0f, 1.574f, -0.014f), new(0.122f, 0.166f, 0.136f), skin);
        mesh.Ellipsoid(new(0f, 1.655f, -0.016f), new(0.169f, 0.203f, 0.188f), skin);
        mesh.Ellipsoid(new(0f, 1.653f, -0.104f), new(0.045f, 0.056f, 0.043f), skin);
        mesh.Ellipsoid(new(0f, 1.737f, 0.002f), new(0.212f, 0.171f, 0.238f), _gear);
        mesh.Ellipsoid(new(0f, 1.704f, -0.022f), new(0.219f, 0.078f, 0.229f), _trim);
        mesh.Ellipsoid(new(0f, 1.684f, -0.099f), new(0.158f, 0.034f, 0.090f), Finish("343b39", 0.77f));
    }

    private void AddSleeve(ArtMesh mesh, int shoulder)
    {
        var x = Joints[shoulder].BindPosition.X;
        var y = ShoulderHeight;
        mesh.SkinnedShell(_fabric, shoulder, shoulder + 1,
            Ring(x, y + 0.055f, 0f, 0.060f, 0.066f, 0f),
            Ring(x, y - 0.065f, 0f, 0.067f, 0.064f, 0f),
            Ring(x, y - 0.20f, 0f, 0.050f, 0.051f, 0.1f),
            Ring(x, y - UpperArmLength, 0f, 0.050f, 0.048f, 0.5f),
            Ring(x, y - 0.36f, 0f, 0.048f, 0.043f, 0.9f),
            Ring(x, y - 0.51f, 0f, 0.043f, 0.034f, 1f),
            Ring(x, y - 0.554f, 0f, 0.034f, 0.030f, 1f));
        mesh.BindTo(shoulder);
        mesh.Ellipsoid(new(x + Mathf.Sign(x) * 0.056f, y - 0.08f, 0f), new(0.032f, 0.082f, 0.075f), _identification);
        mesh.BindTo(shoulder + 2);
        mesh.Ellipsoid(new(x, y - 0.563f, -0.012f), new(0.072f, 0.113f, 0.073f), _trim);
    }

    private void AddLeg(ArtMesh mesh, int joint)
    {
        var x = Joints[joint].BindPosition.X;
        mesh.SkinnedShell(_fabric, joint, joint + 1,
            Ring(x * 0.88f, 1.00f, 0.009f, 0.091f, 0.100f, 0f),
            Ring(x, 0.824f, 0.014f, 0.094f, 0.103f, 0f),
            Ring(x, 0.65f, 0.009f, 0.073f, 0.078f, 0.08f),
            Ring(x, 0.51f, -0.004f, 0.066f, 0.068f, 0.50f),
            Ring(x, 0.38f, 0.012f, 0.065f, 0.072f, 0.96f),
            Ring(x, 0.255f, 0.018f, 0.061f, 0.060f, 1f),
            Ring(x, 0.142f, 0.008f, 0.048f, 0.054f, 1f));

        mesh.BindTo(joint);
        mesh.Ellipsoid(new(x * 1.54f, 0.783f, -0.025f), new(0.083f, 0.179f, 0.106f), _gear);
        mesh.BindTo(joint + 1);
        mesh.Ellipsoid(new(x, 0.486f, -0.069f), new(0.124f, 0.149f, 0.057f), _trim);
        mesh.BindTo(joint + 2);
        mesh.Ellipsoid(new(x, 0.089f, -0.065f), new(0.124f, 0.157f, 0.257f), _boot);
        mesh.Ellipsoid(new(x, 0.121f, 0.009f), new(0.112f, 0.188f, 0.131f), _boot);
        mesh.Box(new(x, 0.027f, -0.065f), new(0.117f, 0.038f, 0.231f), _trim);
    }

    private void AddEquipment(ArtMesh mesh)
    {
        mesh.BindTo(Equipment);
        var origin = EquipmentPosition;
        var metal = Finish("343a37", 0.77f);
        mesh.Box(origin + new Vector3(0f, 0f, -0.105f), new(0.058f, 0.087f, 0.333f), metal);
        mesh.Box(origin + new Vector3(0f, -0.016f, 0.169f), new(0.060f, 0.075f, 0.236f), _trim);
        mesh.Ellipsoid(origin + new Vector3(0f, -0.059f, -0.015f), new(0.056f, 0.127f, 0.080f), _trim);
        mesh.Cylinder(origin + new Vector3(0f, 0.005f, -0.330f), new(0.035f, 0.138f, 0.035f), metal, AlongZ);
        mesh.Box(origin + new Vector3(0f, 0.068f, -0.075f), new(0.038f, 0.046f, 0.075f), metal);
    }

    private static ArtMesh.Contour Ring(float x, float y, float z, float radiusX, float radiusZ, float blend) =>
        new(new Vector3(x, y, z), new Vector2(radiusX, radiusZ), blend);
}
