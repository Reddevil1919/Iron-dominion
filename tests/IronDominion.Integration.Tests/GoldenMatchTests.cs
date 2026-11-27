using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using IronDominion.AI;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Configuration;
using IronDominion.Infrastructure.Persistence;
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
    private static readonly GameConfiguration DefaultConfiguration = GameConfigurationLoader.LoadDefault();
    // Retain the original fixture balance for the historical golden replay digest.
    private static readonly GameConfiguration Configuration = new(DefaultConfiguration.SchemaVersion,
        DefaultConfiguration.TickRate, DefaultConfiguration.StartingCredits, DefaultConfiguration.HeadquartersIncomePerSecond,
        DefaultConfiguration.SectorIncomePerSecond, DefaultConfiguration.CaptureSeconds, DefaultConfiguration.CaptureRadiusMeters,
        DefaultConfiguration.InfantryCoverMultiplier, DefaultConfiguration.Units, DefaultConfiguration.Buildings);

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

    [Fact]
    public void TacticalOrdersRoundTripAndReplayInReverseArrivalOrder()
    {
        const int ticks = 50;
        var battlefield = new GoldenBattlefield(Seed);
        var initial = CreateWorld();
        var session = new GameSession(initial, Configuration, battlefield, battlefield);
        var selected = new[] { BlueTank };
        session.Commands.ScheduleAttackMove(Blue, selected, AcrossWall);
        session.Commands.ScheduleHoldPosition(Blue, selected, 35);
        selected[0] = OrangeTank;

        var trace = new ReplayTrace(initial);
        trace.Add(session.World, []);
        var orders = new List<RecordedOrder>();
        while (session.World.Tick < ticks)
        {
            var events = session.AdvanceOneTick();
            trace.Add(session.World, events);
            foreach (var order in events.OfType<CommandProcessed>())
            {
                Assert.True(order.Result.Accepted);
                orders.Add(RecordedOrder.Capture(order.Command));
            }
        }

        Assert.Equal([OrderKind.AttackMove, OrderKind.HoldPosition], orders.Select(order => order.Kind));
        var json = JsonSerializer.Serialize(new ReplayRecording(Configuration.SchemaVersion, Seed, orders.ToArray()));
        var recording = Assert.IsType<ReplayRecording>(JsonSerializer.Deserialize<ReplayRecording>(json));
        AssertSameTimeline(trace, Replay(recording, recording.Orders.Reverse(), ticks), "reverse tactical arrival");
    }

    [Theory]
    [InlineData(213)]
    [InlineData(331)]
    [InlineData(795)]
    public void SavingAndLoadingDuringTheGoldenMatchPreservesItsTimeline(int saveTick)
    {
        using var slot = new TemporarySaveSlot();
        var match = PlayGoldenMatch(saveTick, slot.File);

        Assert.Equal(MatchResult.PlayerOneWon, match.Session.World.Result);
        Assert.Equal(855, match.Session.World.Tick);
        Assert.Equal(ExpectedDigest, match.Trace.Digest);
    }

    [Theory]
    [InlineData("{", "invalid data")]
    [InlineData("""{"schemaVersion":9}""", "format 9")]
    [InlineData("""{"schemaVersion":1}""", "invalid data")]
    public void InvalidSaveFilesFailWithAControlledError(string contents, string reason)
    {
        using var slot = new TemporarySaveSlot();
        File.WriteAllText(slot.Path, contents);

        var failure = Assert.Throws<InvalidDataException>(() => slot.File.Read<SessionCheckpoint>());
        Assert.Contains(reason, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveSlotReplacesTheFileAndRejectsADifferentBattlefield()
    {
        using var slot = new TemporarySaveSlot();
        var battlefield = new GoldenBattlefield(Seed);
        var session = new GameSession(CreateWorld(), Configuration, battlefield, battlefield);
        slot.File.Write(session.Capture());
        session.AdvanceOneTick();
        slot.File.Write(session.Capture());

        Assert.Equal(1, slot.File.Read<SessionCheckpoint>().Simulation.Tick);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(slot.Path)!));
        var wrongMap = new MatchSaveFile(slot.Path, "another-battlefield", Configuration);
        Assert.Throws<InvalidDataException>(() => wrongMap.Read<SessionCheckpoint>());

        var document = JsonNode.Parse(File.ReadAllText(slot.Path))!.AsObject();
        document["state"] = null;
        File.WriteAllText(slot.Path, document.ToJsonString());
        Assert.Throws<InvalidDataException>(() => slot.File.Read<SessionCheckpoint>());
    }

    [Fact]
    public void ACheckpointWithInvalidSessionStateCannotBeLoaded()
    {
        var battlefield = new GoldenBattlefield(Seed);
        var world = CreateWorld();
        var session = new GameSession(world, Configuration, battlefield, battlefield);
        session.Commands.ScheduleAttackMove(Blue, [BlueTank], AcrossWall);
        var original = session.Capture();
        Assert.Throws<InvalidDataException>(() => GameSession.Restore(world, Configuration, battlefield, battlefield,
            original with { Scheduler = [] }));
        var living = original.Simulation.Units[0];
        var malformed = original with
        {
            Simulation = original.Simulation with { Units = original.Simulation.Units.SetItem(0, living with { Position = null }) }
        };
        Assert.Throws<InvalidDataException>(() => GameSession.Restore(world, Configuration, battlefield, battlefield, malformed));
        var forgedSpawn = original with
        {
            Simulation = original.Simulation with
            {
                Units = original.Simulation.Units.Add(living with { Id = new EntityId(int.MaxValue) })
            }
        };
        Assert.Throws<InvalidDataException>(() => GameSession.Restore(world, Configuration, battlefield, battlefield, forgedSpawn));
        Assert.True(Assert.Single(session.AdvanceOneTick().OfType<CommandProcessed>()).Result.Accepted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OlderSavesLoadUnderTheNewBalance(int version)
    {
        using var slot = new TemporarySaveSlot();
        var legacy = new GameSession(CreateWorld(), Configuration, new GoldenBattlefield(Seed), new GoldenBattlefield(Seed));
        legacy.Commands.ScheduleMove(Blue, [BlueTank], AcrossWall);
        legacy.AdvanceOneTick();
        slot.File.Write(legacy.Capture());

        var document = JsonNode.Parse(File.ReadAllText(slot.Path))!.AsObject();
        document["schemaVersion"] = version;
        var earlierBalance = new GameConfiguration(1, 20, 500, 5, 2, 10, 6, 0.7m,
            [new(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20), new(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)],
            [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)], new SuppressionRules(24, 0.65m));
        var earlierBytes = JsonSerializer.SerializeToUtf8Bytes(earlierBalance, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // The first hash was recorded from a save generated by RTS-018.
        document["configurationHash"] = version == 1 ? "01F4AD790864375A5B6DA5CE06A52E411B89657238FA340322C1C12E3BB19FB7"
            : Convert.ToHexString(SHA256.HashData(earlierBytes));
        foreach (var unit in document["state"]!["simulation"]!["units"]!.AsArray())
        {
            unit!.AsObject().Remove("repairTarget");
            if (version == 1)
                unit.AsObject().Remove("suppressedThrough");
        }
        File.WriteAllText(slot.Path, document.ToJsonString());

        var terrain = new GoldenBattlefield(Seed);
        var newest = new MatchSaveFile(slot.Path, "golden-battlefield", DefaultConfiguration);
        var restored = GameSession.Restore(CreateWorld(), DefaultConfiguration, terrain, terrain, newest.Read<SessionCheckpoint>());
        Assert.Equal(1, restored.World.Tick);
        Assert.False(restored.World.IsSuppressed(BlueTank));
        Assert.True(legacy.World.TryGetUnitPosition(BlueTank, out var originalPosition));
        Assert.True(restored.World.TryGetUnitPosition(BlueTank, out var loadedPosition));
        Assert.Equal(originalPosition, loadedPosition);
    }

    [Fact]
    public void QueuedAndActiveRepairsSurviveARealSaveRoundTrip()
    {
        using var slot = new TemporarySaveSlot();
        var file = new MatchSaveFile(slot.Path, "engineering-test", DefaultConfiguration);
        var engineer = new EntityId(1);
        var world = new WorldEntities([
            new Unit(engineer, Blue, new SimVector(-24, -10), UnitKind.EngineerSquad, 300),
            new Unit(BlueTank, Blue, new SimVector(-15, -10), UnitKind.Tank, 600)
        ]);
        var terrain = new GoldenBattlefield(Seed);
        GameSession Restore(SessionCheckpoint saved) => GameSession.Restore(world, DefaultConfiguration, terrain, terrain, saved);
        var initial = new GameSession(world, DefaultConfiguration, terrain, terrain).Capture();
        var damaged = initial.Simulation.Units[1] with { HitPoints = 555 };
        var session = Restore(initial with
        {
            Simulation = initial.Simulation with { Units = initial.Simulation.Units.SetItem(1, damaged) }
        });
        var command = session.Commands.ScheduleRepair(Blue, [engineer], BlueTank, 3);
        var recorded = JsonSerializer.Deserialize<RecordedOrder>(JsonSerializer.Serialize(RecordedOrder.Capture(command)))!;
        var replay = Assert.IsType<RepairCommand>(recorded.Restore());
        Assert.Equal(command.Target, replay.Target);
        Assert.Equal(command.Units.ToArray(), replay.Units.ToArray());

        file.Write(session.Capture());
        session = Restore(file.Read<SessionCheckpoint>());
        for (var tick = 0; tick < 60 && session.World.RepairingTank(engineer) is null; tick++)
            session.AdvanceOneTick();
        Assert.Equal(BlueTank, session.World.RepairingTank(engineer));

        file.Write(session.Capture());
        var loaded = Restore(file.Read<SessionCheckpoint>());
        Assert.Equal(BlueTank, loaded.World.RepairingTank(engineer));
        var controlPulses = Enumerable.Range(0, 40).SelectMany(_ => session.AdvanceOneTick()).OfType<TankRepaired>().ToArray();
        var restoredPulses = Enumerable.Range(0, 40).SelectMany(_ => loaded.AdvanceOneTick()).OfType<TankRepaired>().ToArray();
        Assert.NotEmpty(controlPulses);
        Assert.Equal(controlPulses, restoredPulses);
        Assert.True(loaded.World.TryGetHitPoints(BlueTank, out var hitPoints));
        Assert.Equal(600, hitPoints);
    }

    [Fact]
    public void AnActiveSuppressionTimerAndItsMovementResumeAfterLoading()
    {
        using var slot = new TemporarySaveSlot();
        var map = new WorldEntities([
            new Unit(BlueInfantry, Blue, new SimVector(5, 0), UnitKind.InfantrySquad, 400),
            new Unit(OrangeTank, Orange, new SimVector(14, 0), UnitKind.Tank, 600)
        ]);
        var terrain = new GoldenBattlefield(Seed);
        var session = new GameSession(map, DefaultConfiguration, terrain, terrain);
        session.Commands.ScheduleMove(Blue, [BlueInfantry], new SimVector(5, 26));
        while (session.World.Tick < 15)
            session.AdvanceOneTick();
        Assert.True(session.World.IsSuppressed(BlueInfantry));

        var file = new MatchSaveFile(slot.Path, "suppression-test", DefaultConfiguration);
        file.Write(session.Capture());
        var saved = file.Read<SessionCheckpoint>();
        Assert.Equal(25L, saved.Simulation.Units.Single(unit => unit.Id == BlueInfantry).SuppressedThrough);
        var restored = GameSession.Restore(map, DefaultConfiguration, terrain, terrain, saved);

        for (var i = 0; i < 57; i++)
        {
            Assert.Equal(session.World.IsSuppressed(BlueInfantry), restored.World.IsSuppressed(BlueInfantry));
            Assert.True(session.World.TryGetUnitPosition(BlueInfantry, out var oldPosition));
            Assert.True(restored.World.TryGetUnitPosition(BlueInfantry, out var newPosition));
            Assert.Equal(oldPosition, newPosition);
            Assert.Equal(session.AdvanceOneTick(), restored.AdvanceOneTick());
        }
        Assert.False(restored.World.IsSuppressed(BlueInfantry));
    }

    private static GoldenRun PlayGoldenMatch(int? saveTick = null, MatchSaveFile? save = null)
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
            if (session.World.Tick == saveTick && save is not null)
            {
                session.SetPaused(true);
                save.Write(new SavedRun(session.Capture(), commander.Capture()));
                var saved = save.Read<SavedRun>();
                var afterLoad = new GoldenBattlefield(Seed);
                session = GameSession.Restore(initial, Configuration, afterLoad, afterLoad, saved.Session);
                Assert.True(session.IsPaused);
                Assert.Empty(session.AdvanceOneTick());
                Assert.Equal(saved.Session.Simulation.Tick, session.World.Tick);

                var state = saved.Session.Simulation;
                commander = EnemyCommander.Restore(Configuration, saved.Enemy,
                    state.Units.Where(unit => unit.Owner == Orange).Select(unit => unit.Id).ToHashSet(),
                    state.Units.Select(unit => unit.Id).Concat(state.Buildings.Select(building => building.Id)).ToHashSet(),
                    state.Sectors.Select(sector => sector.Id).ToHashSet());
                enemyOrders = session.Commands.ForPlayer(Orange);
                session.SetPaused(false);
            }
        }

        return new GoldenRun(session, trace, events, new ReplayRecording(Configuration.SchemaVersion, Seed, orders.ToArray()));
    }

    private static ReplayTrace Replay(ReplayRecording recording, IEnumerable<RecordedOrder> orders, int tickLimit = TickLimit)
    {
        var battlefield = new GoldenBattlefield(recording.Seed);
        var initial = CreateWorld();
        var simulation = new GameSimulation(initial, Configuration, battlefield, battlefield);
        var trace = new ReplayTrace(initial);
        trace.Add(simulation.World, []);
        foreach (var order in orders)
            simulation.Schedule(order.Restore());

        while (simulation.World.Tick < tickLimit && simulation.World.Result == MatchResult.Running)
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

    private sealed record SavedRun(SessionCheckpoint Session, CommanderCheckpoint Enemy);

    private sealed class TemporarySaveSlot : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("iron-dominion-save-");
        public string Path => System.IO.Path.Combine(_directory.FullName, "save.json");
        public MatchSaveFile File => new(Path, "golden-battlefield", Configuration);
        public void Dispose() => _directory.Delete(recursive: true);
    }

    private sealed record GoldenRun(GameSession Session, ReplayTrace Trace, IReadOnlyList<IGameEvent> Events,
        ReplayRecording Recording);
}
