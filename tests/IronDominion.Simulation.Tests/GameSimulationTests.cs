using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class GameSimulationTests
{
    [Fact]
    public void Tick_AdvancesOneStepAndPreservesPreviousSnapshot()
    {
        IGameSimulation simulation = new GameSimulation();
        var initialWorld = simulation.World;

        Assert.Empty(simulation.Tick());
        var firstTickWorld = simulation.World;

        Assert.Empty(simulation.Tick());

        Assert.Equal(0L, initialWorld.Tick);
        Assert.Equal(1L, firstTickWorld.Tick);
        Assert.Equal(2L, simulation.World.Tick);
    }

    [Theory]
    [InlineData(typeof(IReadOnlyWorld))]
    [InlineData(typeof(GameSimulation))]
    public void CoreAssembly_DoesNotReferenceGodot(Type type)
    {
        Assert.DoesNotContain(
            type.Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Godot", StringComparison.OrdinalIgnoreCase) == true);
    }
}
