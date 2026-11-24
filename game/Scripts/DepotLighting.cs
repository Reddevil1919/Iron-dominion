using Godot;

namespace IronDominion.Game;

internal static class DepotLighting
{
    public static void Configure(Node3D battlefield, WorldEnvironment lighting)
    {
        if (RenderingServer.GetCurrentRenderingMethod() != "forward_plus")
            return;

        var environment = lighting.Environment;
        environment.SsaoEnabled = true;
        environment.SsaoRadius = 0.78f;
        environment.SsaoIntensity = 1.22f;
        environment.SsaoDetail = 0.62f;
        environment.SsaoLightAffect = 0.12f;
        environment.VolumetricFogEnabled = true;
        environment.VolumetricFogDensity = 0f;
        environment.VolumetricFogLength = 240f;
        environment.VolumetricFogGIInject = 0f;

        using var haze = new FogMaterial
        {
            Albedo = new Color("a3afba"), Density = 0.009f, EdgeFade = 0.82f, HeightFalloff = 0.22f
        };
        battlefield.AddChild(new FogVolume
        {
            Name = "ApronHaze", Position = new Vector3(-4f, 1.95f, -18f),
            Size = new Vector3(68f, 5.8f, 19f), Shape = RenderingServer.FogVolumeShape.Box, Material = haze
        });
        battlefield.AddChild(new FogVolume
        {
            Name = "TreelineHaze", Position = new Vector3(-37f, 2.1f, -48f),
            Size = new Vector3(45f, 6.3f, 18f), Shape = RenderingServer.FogVolumeShape.Box, Material = haze
        });
    }
}
