using Godot;

namespace IronDominion.Game;

internal sealed class UnitArt
{
    private static readonly Basis AlongZ = new(Vector3.Right, Mathf.Pi / 2f);
    private static readonly Basis AlongX = new(Vector3.Forward, Mathf.Pi / 2f);

    public InfantryArt Infantry { get; }
    public Mesh TankHull { get; }
    public Mesh TankTurret { get; }
    public Mesh TankGun { get; }

    public UnitArt(Color identification, bool local)
    {
        Infantry = new InfantryArt(identification, local);
        TankHull = VehicleHull(identification, local ? "626b50" : "8c7861");
        TankTurret = VehicleTurret(identification, local ? "586346" : "7c6a53");
        TankGun = VehicleGun(local ? "586346" : "7c6a53");
    }

    private static Mesh VehicleHull(Color identification, string armor)
    {
        var mesh = new ArtMesh();
        for (var side = -1; side <= 1; side += 2)
        {
            var x = side * 1.42f;
            mesh.Box(new(x, 0.26f, 0), new(0.68f, 0.28f, 4.58f), "303330");
            for (var wheel = -2; wheel <= 2; wheel++)
                mesh.Cylinder(new(x, 0.54f, wheel * 0.85f), new(0.81f, 0.74f, 0.81f), new Color("525449"), AlongX);
            mesh.Box(new(x, 0.86f, 0), new(0.72f, 0.26f, 5.04f), "343831");
            for (var panel = -2; panel <= 2; panel++)
                mesh.Box(new(x, 0.91f, panel * 0.99f), new(0.75f, 0.38f, 0.92f), armor);
        }

        mesh.Box(new(0, 0.89f, 0.12f), new(2.48f, 0.93f, 4.96f), armor);
        mesh.Box(new(0, 1.28f, 0.05f), new(2.90f, 0.29f, 4.56f), armor);
        mesh.Box(new(0, 1.15f, -2.26f), new(2.7f, 0.48f, 0.91f), new Color(armor), new Basis(Vector3.Right, -0.28f));
        mesh.Box(new(0, 1.43f, 1.94f), new(2.38f, 0.08f, 0.55f), "393e35");
        for (var slat = -4; slat <= 4; slat++)
            mesh.Box(new(slat * 0.23f, 1.48f, 1.94f), new(0.07f, 0.03f, 0.51f), "767b6b");

        mesh.Box(new(0, 1.47f, 1.13f), new(1.26f, 0.037f, 0.27f), identification);
        mesh.Box(new(-1.02f, 1.05f, -2.57f), new(0.33f, 0.18f, 0.14f), "b7b59d");
        mesh.Box(new(1.02f, 1.05f, -2.57f), new(0.33f, 0.18f, 0.14f), "b7b59d");
        return mesh.Build();
    }

    private static Mesh VehicleTurret(Color identification, string armor)
    {
        var mesh = new ArtMesh();
        mesh.Cylinder(new(0, 0.03f, 0.08f), new(2.28f, 0.20f, 2.16f), new Color("343a30"));
        mesh.Box(new(0, 0.36f, 0.08f), new(2.41f, 0.64f, 2.62f), armor);
        mesh.Box(new(0, 0.61f, 0.08f), new(2.18f, 0.23f, 2.43f), armor);
        for (var side = -1; side <= 1; side += 2)
            mesh.Box(new(side * 0.84f, 0.37f, -1.01f), new(0.84f, 0.59f, 1.16f), new Color(armor),
                new Basis(Vector3.Up, side * 0.19f));

        mesh.Box(new(0, 0.39f, 1.5f), new(2.19f, 0.50f, 0.40f), "4b5141");
        mesh.Box(new(0, 0.66f, 1.36f), new(1.34f, 0.06f, 0.29f), identification);
        mesh.Cylinder(new(-0.59f, 0.73f, 0.23f), new(0.64f, 0.15f, 0.64f), new Color("41463a"));
        mesh.Cylinder(new(0.63f, 0.74f, -0.08f), new(0.60f, 0.14f, 0.60f), new Color("41463a"));
        mesh.Box(new(0.64f, 0.86f, -0.18f), new(0.34f, 0.21f, 0.28f), "343936");
        mesh.Cylinder(new(-0.76f, 1.16f, 1.04f), new(0.043f, 1.11f, 0.043f), new Color("41473e"),
            new Basis(Vector3.Right, -0.14f));
        return mesh.Build();
    }

    private static Mesh VehicleGun(string armor)
    {
        var mesh = new ArtMesh();
        mesh.Box(new(0, 0, 0.03f), new(0.66f, 0.52f, 0.57f), armor);
        mesh.Cylinder(new(0, 0, -1.28f), new(0.21f, 2.69f, 0.21f), new Color("515b49"), AlongZ);
        mesh.Cylinder(new(0, 0, -2.45f), new(0.28f, 0.43f, 0.28f), new Color("3d4438"), AlongZ);
        mesh.Cylinder(new(0, 0, -2.68f), new(0.16f, 0.027f, 0.16f), new Color("222722"), AlongZ);
        return mesh.Build();
    }
}
