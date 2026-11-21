using IronDominion.Domain;
using Xunit;

namespace IronDominion.Application.Tests;

public sealed class MatchPauseTests
{
    [Fact]
    public void PausePreservesTheSnapshotAndExecutesPendingCommandsOnceAfterResume()
    {
        var player = new PlayerId(1);
        var tank = new EntityId(1);
        var session = new GameSession(new WorldEntities([new Unit(tank, player, default, UnitKind.Tank, 600)]));
        session.AdvanceOneTick();
        var command = session.Commands.ScheduleMove(player, [tank], new SimVector(10, 2));
        var before = session.World;

        session.SetPaused(true);
        Assert.True(session.IsPaused);
        for (var frame = 0; frame < 10; frame++)
            Assert.Empty(session.AdvanceOneTick());
        Assert.Same(before, session.World);

        session.SetPaused(false);
        Assert.False(session.IsPaused);
        var result = Assert.IsType<CommandProcessed>(Assert.Single(session.AdvanceOneTick()));
        Assert.Same(command, result.Command);
        Assert.True(result.Result.Accepted);
        Assert.Equal(before.Tick + 1, session.World.Tick);
        Assert.Empty(session.AdvanceOneTick());
    }
}
