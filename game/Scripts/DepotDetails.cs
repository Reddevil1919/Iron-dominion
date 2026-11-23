using Godot;

namespace IronDominion.Game;

internal static class DepotDetails
{
    private static readonly Basis WheelAxis = new(Vector3.Back, Mathf.Pi / 2f);
    private static readonly Color Rubber = new("343933");
    private static readonly Color Iron = new("51564e");
    private static readonly Color LiftPaint = Weathered("c0a465");
    private static readonly Color Timber = Weathered("9b855d");
    private static readonly Color Strap = new("514e3d");

    public static Mesh CargoHandling()
    {
        const float deck = -1.04f;
        var mesh = new ArtMesh();
        Forklift(mesh, new Vector3(-0.09f, deck, 4.55f));
        Pallet(mesh, new Vector3(-0.27f, deck, 2.16f), Weathered("8b8065"));
        Pallet(mesh, new Vector3(2.26f, deck, -4.75f), Weathered("949b81"));
        Pallet(mesh, new Vector3(4.05f, deck, -4.97f), Weathered("777a6a"));
        for (var side = -1; side <= 1; side += 2)
        for (var stripe = 0; stripe < 5; stripe++)
            mesh.Box(new(side * 1.02f, deck + 0.012f, 1.68f + stripe * 0.93f),
                new(0.093f, 0.013f, 0.62f), Weathered("b29961"));
        return mesh.Build();
    }

    private static void Forklift(ArtMesh mesh, Vector3 site)
    {
        mesh.Box(site + new Vector3(0f, 0.31f, 0.12f), new Vector3(1.18f, 0.37f, 2.06f), Iron);
        mesh.Box(site + new Vector3(0f, 0.60f, 0.64f), new Vector3(1.23f, 0.56f, 0.88f), LiftPaint);
        mesh.Box(site + new Vector3(0f, 0.60f, -0.52f), new Vector3(1.08f, 0.49f, 0.44f), LiftPaint);
        mesh.Box(site + new Vector3(0f, 0.65f, 0.23f), new Vector3(0.39f, 0.15f, 0.32f), Rubber);
        mesh.Box(site + new Vector3(0f, 0.84f, 0.39f), new Vector3(0.38f, 0.34f, 0.10f), Rubber);
        mesh.Cylinder(site + new Vector3(0.02f, 0.86f, -0.31f), new Vector3(0.28f, 0.065f, 0.25f),
            Rubber, new Basis(Vector3.Right, 0.34f));

        for (var side = -1; side <= 1; side += 2)
        {
            var x = side * 0.59f;
            mesh.Cylinder(site + new Vector3(x, 0.26f, -0.71f), new Vector3(0.52f, 0.27f, 0.52f), Rubber, WheelAxis);
            mesh.Cylinder(site + new Vector3(x, 0.21f, 0.76f), new Vector3(0.42f, 0.24f, 0.42f), Rubber, WheelAxis);
            mesh.Cylinder(site + new Vector3(x + side * 0.14f, 0.26f, -0.71f),
                new Vector3(0.23f, 0.06f, 0.23f), LiftPaint, WheelAxis);
            mesh.Box(site + new Vector3(side * 0.50f, 1.18f, -0.55f), new Vector3(0.10f, 1.21f, 0.10f), Iron);
            mesh.Box(site + new Vector3(side * 0.50f, 1.16f, 0.64f), new Vector3(0.10f, 1.25f, 0.10f), Iron);
            mesh.Box(site + new Vector3(side * 0.32f, 0.99f, -1.02f), new Vector3(0.13f, 1.72f, 0.19f), Iron);
            mesh.Box(site + new Vector3(side * 0.29f, 0.14f, -1.48f), new Vector3(0.16f, 0.075f, 0.96f), Iron);
            mesh.Box(site + new Vector3(side * 0.29f, 0.34f, -1.02f), new Vector3(0.16f, 0.48f, 0.12f), Iron);
            mesh.Box(site + new Vector3(side * 0.48f, 0.61f, 1.09f), new Vector3(0.18f, 0.12f, 0.035f), "9a674e");
        }

        mesh.Box(site + new Vector3(0f, 1.76f, 0.05f), new Vector3(1.19f, 0.12f, 1.33f), LiftPaint);
        mesh.Box(site + new Vector3(0f, 1.69f, -1.02f), new Vector3(0.78f, 0.12f, 0.19f), Iron);
        mesh.Box(site + new Vector3(0f, 0.46f, -1.10f), new Vector3(0.90f, 0.14f, 0.18f), Iron);
        mesh.Cylinder(site + new Vector3(0.35f, 1.88f, 0.43f), new Vector3(0.16f, 0.14f, 0.16f), new Color("c48c50"));
    }

    private static void Pallet(ArtMesh mesh, Vector3 site, Color casing)
    {
        for (var slat = -1; slat <= 1; slat++)
        {
            mesh.Box(site + new Vector3(slat * 0.54f, 0.07f, 0f), new Vector3(0.13f, 0.14f, 1.27f), Timber);
            mesh.Box(site + new Vector3(0f, 0.15f, slat * 0.49f), new Vector3(1.46f, 0.075f, 0.24f), Timber);
        }

        mesh.Box(site + new Vector3(0f, 0.47f, 0f), new Vector3(1.29f, 0.57f, 1.09f), casing);
        mesh.Box(site + new Vector3(0f, 0.78f, 0f), new Vector3(1.36f, 0.09f, 1.15f), casing.Lightened(0.12f));
        for (var band = -1; band <= 1; band += 2)
        {
            var x = band * 0.38f;
            mesh.Box(site + new Vector3(x, 0.831f, 0f), new Vector3(0.067f, 0.018f, 1.16f), Strap);
            mesh.Box(site + new Vector3(x, 0.51f, 0.55f), new Vector3(0.067f, 0.63f, 0.018f), Strap);
            mesh.Box(site + new Vector3(x, 0.51f, -0.55f), new Vector3(0.067f, 0.63f, 0.018f), Strap);
        }
        mesh.Box(site + new Vector3(-0.12f, 0.62f, 0.551f), new Vector3(0.26f, 0.13f, 0.018f), "cdc3a0");
    }

    public static void AddWorkshop(ArtMesh mesh)
    {
        for (var x = -3; x <= 3; x += 3)
        {
            mesh.Box(new(x, 1.25f, 4.25f), new(1.93f, 2.18f, 0.13f), "3f4844");
            mesh.Box(new(x, 1.26f, 4.33f), new(1.62f, 1.98f, 0.09f), "626963");
            for (var rib = 0; rib < 9; rib++)
                mesh.Box(new(x, 0.37f + rib * 0.20f, 4.38f), new(1.59f, 0.037f, 0.034f), "4e564f");
            mesh.Box(new(x, 2.37f, 4.32f), new(1.91f, 0.15f, 0.20f), "8f9382");
            mesh.Box(new(x - 1.02f, 0.63f, 4.37f), new(0.17f, 0.99f, 0.20f), "aa925b");
            mesh.Box(new(x + 1.04f, 1.31f, 4.27f), new(0.25f, 0.32f, 0.17f), "626e66");
        }

        mesh.Box(new(3.45f, 4.53f, 1.73f), new(2.45f, 0.13f, 1.27f), "75786c");
        mesh.Box(new(3.45f, 4.79f, 1.73f), new(2.11f, 0.43f, 0.97f), "919589");
        foreach (var x in new[] { 2.94f, 3.96f })
        {
            mesh.Cylinder(new(x, 5.015f, 1.73f), new(0.70f, 0.065f, 0.70f), Iron);
            for (var grille = -2; grille <= 2; grille++)
                mesh.Box(new(x + grille * 0.115f, 5.052f, 1.73f), new(0.042f, 0.024f, 0.50f), "81857a");
        }
    }

    public static void AddRelayPower(ArtMesh mesh)
    {
        var face = new Basis(Vector3.Right, -0.19f);
        for (var panel = 0; panel < 3; panel++)
        {
            var x = -4.12f + panel * 1.36f;
            var center = new Vector3(x, 4.93f, -0.82f);
            mesh.Box(center - Vector3.Up * 0.16f, new(1.05f, 0.37f, 0.68f), "545b57");
            mesh.Box(center, new(1.27f, 0.10f, 1.55f), new Color("8a978f"), face);
            mesh.Box(center + face * new Vector3(0f, 0.054f, 0f), new(1.17f, 0.027f, 1.44f), new Color("394f59"), face);
            for (var cell = -2; cell <= 2; cell++)
                mesh.Box(center + face * new Vector3(0f, 0.073f, cell * 0.24f),
                    new(1.15f, 0.012f, 0.025f), new Color("68848a"), face);
        }
        mesh.Box(new(0.89f, 4.83f, -0.82f), new(0.73f, 0.47f, 0.65f), "7a8277");
    }

    public static void AddGateBooth(ArtMesh mesh, float x)
    {
        var booth = new Vector3(x, 0f, -Mathf.Sign(x) * 6.22f);
        mesh.Box(booth + new Vector3(0f, 0.10f, 0f), new(2.49f, 0.20f, 2.55f), Weathered("818276"));
        mesh.Box(booth + new Vector3(0f, 1.22f, 0f), new(2.06f, 2.26f, 2.16f), Weathered("898f82"));
        mesh.Box(booth + new Vector3(0.09f, 2.45f, 0f), new(2.49f, 0.21f, 2.56f), Weathered("60695f"));
        foreach (var side in new[] { -1f, 1f })
        {
            mesh.Box(booth + new Vector3(side * 1.05f, 1.65f, 0f), new(0.075f, 0.63f, 1.66f), "405658");
            mesh.Box(booth + new Vector3(side * 1.10f, 1.66f, -0.32f), new(0.07f, 0.65f, 0.045f), "868f82");
        }
        var lane = Mathf.Sign(x);
        mesh.Box(booth + new Vector3(-0.27f, 1.18f, lane * 1.11f), new(0.72f, 1.72f, 0.11f), Weathered("657569"));
        mesh.Box(booth + new Vector3(0.04f, 1.13f, lane * 1.18f), new(0.15f, 0.06f, 0.07f), Iron);
        mesh.Cylinder(booth + new Vector3(0.67f, 2.63f, lane * 0.74f), new(0.20f, 0.18f, 0.20f), new Color("c89257"));
    }

    // The scenery shader uses vertex alpha to control wear on otherwise opaque surfaces.
    public static Color Weathered(string hex) => new(hex) { A = 0.32f };
}
