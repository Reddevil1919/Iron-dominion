using Godot;

namespace IronDominion.Game;

internal static class BattlefieldArt
{
    private static readonly Basis AlongZ = new(Vector3.Right, Mathf.Pi / 2f);

    public static Mesh Building(bool headquarters, Color identification, bool local, bool relayRoof)
    {
        var mesh = new ArtMesh();
        var wall = local ? "898e7d" : "9b8c74";
        var roof = local ? "505c51" : "65574b";

        mesh.Box(new(0f, 0.22f, 0f), new(10.9f, 0.44f, 8.9f), "696c62");
        mesh.Box(new(0f, 1.92f, 0f), new(10.4f, 3.4f, 8.4f), wall);
        mesh.Box(new(0f, 3.68f, 0f), new(10.9f, 0.32f, 8.9f), "73766c");
        mesh.Box(new(0f, 3.86f, 0f), new(10.58f, 0.14f, 8.58f), roof);
        var windowHeight = headquarters ? 2.41f : 2.81f;
        for (var x = -4; x <= 4; x += 2)
        {
            mesh.Box(new(x, windowHeight, 4.24f), new(1.32f, 0.74f, 0.09f), "414e51");
            mesh.Box(new(x, windowHeight - 0.20f, 4.31f), new(1.26f, 0.15f, 0.10f), "89978f");
            mesh.Box(new(x, windowHeight, -4.24f), new(1.32f, 0.74f, 0.09f), "414e51");
        }

        for (var side = -1; side <= 1; side += 2)
        {
            mesh.Box(new(side * 5.16f, 1.82f, 0f), new(0.14f, 1.55f, 2.52f), "484b40");
            mesh.Box(new(side * 5.25f, 3.10f, 0f), new(0.10f, 0.34f, 6.44f), identification);
            mesh.Box(new(0f, 3.17f, side * 4.20f), new(9.8f, 0.24f, 0.07f), "555c50");
        }

        if (headquarters)
        {
            mesh.Box(new(-0.9f, 4.28f, -0.52f), new(6.32f, 0.80f, 4.80f), "676d61");
            mesh.Box(new(-0.9f, 4.74f, -0.52f), new(6.54f, 0.24f, 5.04f), roof);
            mesh.Box(new(-0.88f, 4.885f, 0.70f), new(3.64f, 0.05f, 0.75f), identification);
            mesh.Cylinder(new(-2.21f, 5.08f, -1.33f), new(0.66f, 0.40f, 0.66f), new Color("535a52"));
            mesh.Cylinder(new(-2.21f, 5.57f, -1.33f), new(0.12f, 1.31f, 0.12f), new Color("a0a395"));
            mesh.Box(new(-2.21f, 5.67f, -1.33f), new(0.86f, 0.08f, 0.11f), "b3b5a5");
            mesh.Ellipsoid(new(3.63f, 4.40f, -1.84f), new(1.26f, 0.98f, 1.26f), "b2b4a6");
            mesh.Box(new(2.80f, 4.15f, 2.2f), new(3.3f, 0.40f, 0.87f), "414640");
        }
        else
        {
            for (var z = -3; z <= 3; z++)
                mesh.Box(new(0f, 4.03f + (3 - Mathf.Abs(z)) * 0.16f, z), new(10.56f, 0.49f, 1.04f), roof);
            mesh.Box(new(0f, 4.78f, 0f), new(6.44f, 0.12f, 0.55f), identification);
            DepotDetails.AddWorkshop(mesh);
        }

        if (relayRoof)
        {
            AddRelayMast(mesh);
            DepotDetails.AddRelayPower(mesh);
        }
        return mesh.Build();
    }

    private static void AddRelayMast(ArtMesh mesh)
    {
        mesh.Box(new Vector3(2.5f, 4.75f, -0.18f), new Vector3(1.42f, 0.24f, 0.96f), "6b6d63");
        mesh.Box(new Vector3(2.5f, 5.27f, -0.18f), new Vector3(0.22f, 1.25f, 0.20f), "545c57");
        for (var panel = -1; panel <= 1; panel++)
            mesh.Box(new Vector3(2.5f + panel * 0.35f, 5.67f, -0.27f), new Vector3(0.22f, 0.48f, 0.16f),
                new Color("9a9d8c"), new Basis(Vector3.Up, panel * 0.39f));
        mesh.Box(new Vector3(2.50f, 5.61f, -0.24f), new Vector3(0.83f, 0.10f, 0.10f), "6f7668");
    }

    public static void Decorate(Node3D battlefield, ShaderMaterial surface)
    {
        var roadblock = battlefield.GetNode<MeshInstance3D>("Roadblock/Mesh");
        var bounds = BattlefieldNavigation.BoxBounds(battlefield.GetNode<CollisionShape3D>("Roadblock/Collision"));
        surface.SetShaderParameter("freight_yard", new Vector4(bounds.Position.X, bounds.Position.Z, bounds.End.X, bounds.End.Z));
        roadblock.Mesh = Depot();
        roadblock.MaterialOverride = surface;
        roadblock.GetParent().AddChild(new MeshInstance3D
        {
            Name = "CargoHandling", Mesh = DepotDetails.CargoHandling(), MaterialOverride = surface
        });
        var gates = new ArtMesh();
        AddPerimeterGate(gates, -61.8f);
        AddPerimeterGate(gates, 61.8f);
        battlefield.AddChild(new MeshInstance3D
        {
            Name = "PerimeterGates", Mesh = gates.Build(), MaterialOverride = surface
        });
        DepotWayfinding.AddGroundMarkings(battlefield);
        var perimeter = new MeshInstance3D
        {
            Name = "PerimeterVegetation",
            Mesh = Perimeter(),
            MaterialOverride = surface,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        battlefield.AddChild(perimeter);
    }

    private static Mesh Depot()
    {
        var mesh = new ArtMesh();
        // The roadblock node sits 1.5 m above the road. The props stay inside its footprint.
        mesh.Box(new(0f, -1.27f, 0f), new(11.9f, 0.46f, 15.9f), "75756c");
        AddContainer(mesh, -2.92f, -2.9f, "69736a");
        AddContainer(mesh, 2.82f, 2.85f, "8b684d");
        AddLoadingFrame(mesh);
        for (var i = -3; i <= 3; i++)
        {
            var x = i * 1.62f;
            mesh.Box(new(x, -0.88f, -7.25f), new(1.48f, 0.92f, 0.73f), "9a9a88");
            mesh.Box(new(x, -0.36f, -7.25f), new(1.40f, 0.19f, 0.54f), "a4a493");
            mesh.Box(new(x, -0.61f, 7.42f), new(1.42f, 1.38f, 0.51f), "83857a");
            mesh.Box(new(x, -0.61f, 7.70f), new(0.25f, 0.57f, 0.05f), "aea070");
        }

        foreach (var x in new[] { -5.56f, 5.56f })
        for (var z = -5; z <= 5; z += 2)
        {
            mesh.Box(new(x, -0.80f, z), new(0.42f, 0.96f, 1.86f), "8b8b7d");
            mesh.Box(new(x, -0.28f, z), new(0.24f, 0.12f, 1.73f), "a19f91");
        }

        for (var row = 0; row < 3; row++)
        {
            var barrel = new Vector3(-3.9f + row * 0.92f, -0.64f, 4.84f);
            mesh.Cylinder(barrel, new(0.64f, 0.86f, 0.64f), new Color("555c4a"));
            mesh.Cylinder(barrel + Vector3.Up * 0.29f, new(0.68f, 0.08f, 0.68f), new Color("72776a"));
        }
        return mesh.Build();
    }

    private static void AddLoadingFrame(ArtMesh mesh)
    {
        const float crossbar = 1.33f;
        foreach (var sign in new[] { -1f, 1f })
        {
            var x = sign * 4.93f;
            mesh.Box(new Vector3(x, -1.12f, 0.6f), new Vector3(0.75f, 0.21f, 2.02f), "575954");
            mesh.Box(new Vector3(x, -0.02f, 0.6f), new Vector3(0.26f, 2.33f, 0.40f), "625b47");
            foreach (var direction in new[] { -1f, 1f })
                mesh.Box(new Vector3(x, -0.63f, direction * 0.66f + 0.6f), new Vector3(0.16f, 1.68f, 0.14f),
                    new Color("746953"), new Basis(Vector3.Right, direction * 0.63f));
            mesh.Box(new Vector3(x, -0.05f, 0.82f), new Vector3(0.28f, 0.96f, 0.06f), "a68c5c");
        }

        mesh.Box(new Vector3(0f, crossbar, 0.6f), new Vector3(10.35f, 0.32f, 0.52f), "977d4f");
        for (var brace = -4; brace <= 4; brace++)
            mesh.Box(new Vector3(brace * 1.13f, crossbar + 0.01f, 0.87f), new Vector3(0.22f, 0.20f, 0.038f), "676049");

        mesh.Box(new Vector3(0f, crossbar - 0.20f, 0.6f), new Vector3(0.88f, 0.21f, 0.60f), "474942");
        mesh.Box(new Vector3(0f, 0.82f, 0.6f), new Vector3(0.045f, 0.39f, 0.045f), "50504a");
        mesh.Box(new Vector3(0f, 0.62f, 0.6f), new Vector3(0.30f, 0.17f, 0.24f), "79745d");
    }

    private static void AddContainer(ArtMesh mesh, float x, float z, string paint)
    {
        var center = new Vector3(x, -0.02f, z);
        var siding = DepotDetails.Weathered(paint);
        mesh.Box(center, new(3.34f, 2.48f, 6.48f), siding);
        for (var i = -7; i <= 7; i++)
        {
            var part = i * 0.41f;
            mesh.Box(center + new Vector3(1.69f, 0f, part), new(0.10f, 2.31f, 0.13f), siding);
            mesh.Box(center + new Vector3(-1.69f, 0f, part), new(0.10f, 2.31f, 0.13f), siding);
            mesh.Box(center + new Vector3(0f, 1.27f, part), new(3.23f, 0.07f, 0.13f), siding);
        }
        foreach (var offset in new[] { -1.53f, 1.53f })
        foreach (var y in new[] { -1.08f, 1.08f })
        {
            mesh.Box(center + new Vector3(offset, y, 0f), new(0.25f, 0.20f, 6.64f), "54584d");
            mesh.Cylinder(center + new Vector3(offset, y, 3.28f), new(0.17f, 0.13f, 0.17f),
                new Color("a9a895"), AlongZ);
        }

        for (var leaf = -1; leaf <= 1; leaf += 2)
        {
            mesh.Box(center + new Vector3(leaf * 0.72f, 0f, 3.25f), new(1.39f, 2.17f, 0.055f), siding.Darkened(0.12f));
            for (var rod = 0; rod <= 1; rod++)
                mesh.Box(center + new Vector3(leaf * (0.42f + rod * 0.51f), 0f, 3.30f),
                    new(0.055f, 2.02f, 0.059f), "9b9d8e");
            for (var hinge = -1; hinge <= 1; hinge++)
                mesh.Box(center + new Vector3(leaf * 1.42f, hinge * 0.75f, 3.30f),
                    new(0.24f, 0.08f, 0.084f), "5c6055");
            mesh.Box(center + new Vector3(leaf * 0.54f, -0.28f, 3.34f), new(0.36f, 0.075f, 0.065f), "63675c");
        }
        mesh.Box(center + new Vector3(0.99f, 0.69f, 3.293f), new(0.48f, 0.22f, 0.020f), "b6af97");
    }

    private static Mesh Perimeter()
    {
        var mesh = new ArtMesh();
        var random = new Random(46);
        var foliage = new[] { new Color("4f5944"), new Color("586247"), new Color("667052") };
        for (var cluster = 0; cluster < 22; cluster++)
        {
            var side = cluster % 4;
            var along = random.NextSingle() * 111f - 55.5f;
            var edge = 59.5f + random.NextSingle() * 1.5f;
            var point = side switch
            {
                0 => new Vector3(-edge, 0f, along),
                1 => new Vector3(edge, 0f, along),
                2 => new Vector3(along, 0f, -edge),
                _ => new Vector3(along, 0f, edge)
            };
            for (var i = 0; i < 2; i++)
            {
                var offset = random.NextSingle() * 4.5f - 2.25f;
                var p = point + (side < 2 ? Vector3.Back : Vector3.Right) * offset;
                if (side < 2 && Mathf.Abs(p.Z) < 8.6f)
                    continue;
                var h = 2.2f + random.NextSingle() * 3.8f;
                mesh.Cylinder(p + Vector3.Up * (h * 0.22f), new(0.22f, h * 0.45f, 0.22f), new Color("5c5746"));
                mesh.Conifer(p + Vector3.Up * (h * 0.44f), new(h * 0.55f, h * 0.54f, h * 0.55f), foliage[cluster % 3]);
                mesh.Conifer(p + Vector3.Up * (h * 0.63f), new(h * 0.43f, h * 0.48f, h * 0.43f), foliage[(cluster + 1) % 3]);
                mesh.Conifer(p + Vector3.Up * (h * 0.79f), new(h * 0.31f, h * 0.42f, h * 0.31f), foliage[cluster % 3]);
            }
        }
        return mesh.Build();
    }

    private static void AddPerimeterGate(ArtMesh mesh, float x)
    {
        DepotDetails.AddGateBooth(mesh, x);
        foreach (var z in new[] { -4.15f, 4.15f })
        {
            mesh.Box(new Vector3(x, 0.24f, z), new Vector3(1.30f, 0.48f, 1.30f), "7d7b6f");
            mesh.Box(new Vector3(x, 1.95f, z), new Vector3(0.31f, 3.53f, 0.34f), "63685f");
            mesh.Box(new Vector3(x, 2.7f, z * 0.81f), new Vector3(0.17f, 1.77f, 0.16f),
                new Color("72776a"), new Basis(Vector3.Right, Mathf.Sign(z) * 0.7f));
        }

        mesh.Box(new Vector3(x, 3.62f, 0f), new Vector3(0.36f, 0.28f, 8.57f), "747666");
        mesh.Box(new Vector3(x, 3.55f, 0f), new Vector3(0.41f, 0.43f, 2.22f), "96875c");
        for (var stripe = -1; stripe <= 1; stripe++)
            mesh.Box(new Vector3(x - Mathf.Sign(x) * 0.22f, 3.56f, stripe * 0.60f),
                new Vector3(0.047f, 0.30f, 0.21f), "605a46");
    }
}
