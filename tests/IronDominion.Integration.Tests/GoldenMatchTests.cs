using System.Text.Json;
using IronDominion.AI;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Configuration;
using IronDominion.Simulation;
using Xunit;
using Xunit.Abstractions;
using static IronDominion.Integration.Tests.GoldenBattlefield;

namespace IronDominion.Integration.Tests;

public sealed class GoldenMatchTests(ITestOutputHelper output)
{
    private const int Seed = 4126;
    private const int TickLimit = 1200;
    private const string ExpectedDigest = "DD3E9A65A582A55322AEFA4A0B3C58E9EEF2B7F0A1FC2C5B03B5EF899B37819F";
    private static readonly GameConfiguration Configuration = GameConfigurationLoader.LoadDefault();

    [Fact]
    public void GoldenMatchCapturesTrainsFightsAndFinishes()
    {
        var match = PlayGoldenMatch();
        var events = match.Events;
        Assert.Contains(events.OfType<CommandProcessed>(), order => order.Result.RejectionCode == "not_owner");
        Assert.Contains(events.OfType<CommandProcessed>(), order => order.Result.RejectionCode == "unreachable_destination");
        Assert.Contains(events.OfType<CommandProcessed>(), order => order.Result.RejectionCode == "insufficient_credits");
        var sharedTurn = events.OfType<CommandProcessed>().Where(order => order.Tick == 41).ToArray();
        Assert.Equal<PlayerId>([Blue, Orange, Orange], sharedTurn.Select(order => order.Command.Player));
        Assert.Contains(events.OfType<SectorCaptured>(), sector => sector.Sector == Alpha && sector.Owner == Blue);
        Assert.Contains(events.OfType<SectorCaptured>(), sector => sector.Sector == Bravo && sector.Owner == Orange);

        var reinforcements = events.OfType<UnitSpawned>().ToArray();
        Assert.Equal(2, reinforcements.Count(spawn => spawn.Unit.Owner == Blue && spawn.Unit.Kind == UnitKind.InfantrySquad));
        Assert.Contains(reinforcements, spawn => spawn.Unit.Owner == Orange && spawn.Unit.Kind == UnitKind.Tank);
        Assert.Contains(events.OfType<CommandProcessed>(), order => order.Result.Accepted
            && order.Command is AttackCommand { Player: var owner, Target: var target } && owner == Blue && target == OrangeHq);
        Assert.Contains(events.OfType<ShotFired>(), shot => shot.Attacker == BlueTank && shot.Target == OrangeHq);

        var ending = Assert.Single(events.OfType<MatchEnded>());
        Assert.Equal(MatchResult.PlayerOneWon, ending.Result);
        Assert.Equal(855, ending.Tick);
        Assert.Equal(ending.Tick, match.Session.World.Tick);
        Assert.Equal(ExpectedDigest, match.Trace.Digest);
        Assert.True(match.Session.World.TryGetHitPoints(OrangeHq, out var hp));
        Assert.Equal(0, hp);
        Assert.True(match.Session.World.TryGetHitPoints(BlueHq, out hp) && hp > 0);
        Assert.Empty(match.Session.AdvanceOneTick());

        output.WriteLine($"Golden match ended at tick {ending.Tick} after {match.Recording.Orders.Length} processed orders.");
        output.WriteLine($"Timeline SHA-256 {match.Trace.Digest}");
    }

    [Fact]
    public void RecordedOrdersReplayToTheSameHashesInDifferentArrivalOrders()
    {
        var live = PlayGoldenMatch();
        Assert.Equal(ExpectedDigest, live.Trace.Digest);
        var json = JsonSerializer.Serialize(live.Recording);
        var recording = Assert.IsType<ReplayRecording>(JsonSerializer.Deserialize<ReplayRecording>(json));

        Assert.Equal(Configuration.SchemaVersion, recording.ConfigurationVersion);
        Assert.Equal(Seed, recording.Seed);
        var forwards = Replay(recording, recording.Orders);
        var backwards = Replay(recording, recording.Orders.Reverse());

        AssertSameTimeline(live.Trace, forwards, "recorded order");
        AssertSameTimeline(live.Trace, backwards, "reverse arrival");
        output.WriteLine($"Both replays matched all {live.Trace.Frames.Count} frames ({live.Trace.Digest}).");
    }

    private static GoldenRun PlayGoldenMatch()
    {
        var battlefield = new GoldenBattlefield(Seed);
        var initial = CreateWorld();
        var session = new GameSession(initial, Configuration, battlefield, battlefield);
        var commander = new EnemyCommander(Configuration);
        var enemyOrders = session.Commands.ForPlayer(Orange);
        var selection = new UnitSelection(Blue, id => session.World.LivingUnitOwner(id));
        var trace = new ReplayTrace(initial);
        var events = new List<IGameEvent>();
        var orders = new List<RecordedOrder>();
        trace.Add(session.World, []);

        selection.Select([OrangeTank, BlueInfantry]);
        Assert.Equal(BlueInfantry, Assert.Single(selection.Selected));
        session.Commands.ScheduleMove(Blue, selection.Selected, AlphaPoint);
        session.Commands.ScheduleTraining(Blue, BlueBarracks, UnitKind.InfantrySquad);
        session.Commands.ScheduleTraining(Blue, BlueBarracks, UnitKind.InfantrySquad);
        session.Commands.ScheduleTraining(Blue, BlueHq, UnitKind.Tank);
        session.Commands.ScheduleMove(Blue, [OrangeTank], AlphaPoint);
        session.Commands.ScheduleMove(Blue, [BlueTank], BlockedGoal);
        session.Commands.ScheduleMove(Blue, [BlueTank], AcrossWall, 2);
        session.Commands.ScheduleMove(Blue, [BlueInfantry], AlphaPoint, 41);
        session.Commands.ScheduleMove(Blue, [BlueTank], StagingPoint, 220);
        session.Commands.ScheduleMove(Blue, [BlueTank], FiringPosition, 570);
        session.Commands.ScheduleAttack(Blue, [BlueTank], OrangeHq, 760);

        while (session.World.Tick < TickLimit && session.World.Result == MatchResult.Running)
        {
            var frame = session.AdvanceOneTick();
            trace.Add(session.World, frame);
            events.AddRange(frame);
            orders.AddRange(frame.OfType<CommandProcessed>().Select(order => RecordedOrder.Capture(order.Command)));
            if (session.World.Result == MatchResult.Running && EnemyCommander.IsDecisionTick(session.World.Tick))
                commander.Plan(session.Observe(Orange), enemyOrders);

            switch (session.World.Tick)
            {
                case 1:
                    Assert.Equal(300, session.World.Credits(Blue));
                    Assert.Equal(2, session.World.TrainingQueue(BlueBarracks).Count);
                    Assert.True(session.World.TryGetUnitPosition(BlueTank, out var start));
                    Assert.Equal(BlueTankStart, start);
                    break;
                case 100:
                    Assert.True(session.World.TryGetUnitPosition(BlueTank, out var detour));
                    Assert.True(detour.Z * battlefield.Waypoint.Z > 0, "The tank uses the seeded side of the wall.");
                    break;
                case 120:
                    var beforePause = session.World;
                    session.SetPaused(true);
                    for (var frameWhilePaused = 0; frameWhilePaused < 10; frameWhilePaused++)
                        Assert.Empty(session.AdvanceOneTick());
                    Assert.Same(beforePause, session.World);
                    session.SetPaused(false);
                    break;
                case 215:
                    Assert.Equal(7, session.World.IncomePerSecond(Blue));
                    Assert.True(session.World.HasCover(BlueInfantry));
                    break;
                case 330:
                    var waiting = Assert.Single(session.World.TrainingQueue(BlueBarracks));
                    Assert.Equal(0, waiting.RemainingTicks);
                    var blocker = Assert.Single(events.OfType<UnitSpawned>(), spawn => spawn.Unit.Owner == Blue);
                    Assert.True(session.World.TryGetUnitPosition(blocker.Unit.Id, out var exit));
                    Assert.Equal(BlueExit, exit);
                    selection.Select([blocker.Unit.Id]);
                    Assert.Equal(blocker.Unit.Id, Assert.Single(selection.Selected));
                    session.Commands.ScheduleMove(Blue, selection.Selected, new SimVector(-24, 36));
                    break;
            }
        }

        return new GoldenRun(session, trace, events, new ReplayRecording(Configuration.SchemaVersion, Seed, orders.ToArray()));
    }

    private static ReplayTrace Replay(ReplayRecording recording, IEnumerable<RecordedOrder> orders)
    {
        var battlefield = new GoldenBattlefield(recording.Seed);
        var initial = CreateWorld();
        var simulation = new GameSimulation(initial, Configuration, battlefield, battlefield);
        var trace = new ReplayTrace(initial);
        trace.Add(simulation.World, []);
        foreach (var order in orders)
            simulation.Schedule(order.Restore());

        while (simulation.World.Tick < TickLimit && simulation.World.Result == MatchResult.Running)
        {
            var events = simulation.Tick();
            trace.Add(simulation.World, events);
        }

        return trace;
    }

    private static void AssertSameTimeline(ReplayTrace expected, ReplayTrace replay, string arrival)
    {
        var commonFrames = Math.Min(expected.Frames.Count, replay.Frames.Count);
        for (var tick = 0; tick < commonFrames; tick++)
        {
            Assert.True(expected.Frames[tick] == replay.Frames[tick],
                $"Replay with {arrival} diverged at tick {tick}. Expected {expected.Frames[tick]}, got {replay.Frames[tick]}.");
        }
        Assert.Equal(expected.Frames.Count, replay.Frames.Count);
        Assert.Equal(expected.Digest, replay.Digest);
    }

    private sealed record GoldenRun(GameSession Session, ReplayTrace Trace, IReadOnlyList<IGameEvent> Events,
        ReplayRecording Recording);
}
