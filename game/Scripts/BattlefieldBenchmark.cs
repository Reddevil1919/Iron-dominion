using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldBenchmark(BenchmarkOptions options, IReadOnlyList<Unit> units) : IDisposable
{
    private const int WarmupSeconds = 5;
    private const int OrderIntervalTicks = 80;
    private readonly long _createdAt = Stopwatch.GetTimestamp();
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly List<double> _frameMs = new();
    private readonly List<double> _tickMs = new();
    private long _startedAt;
    private long _lastFrame;
    private long _lastMemorySample;
    private long _firstPathRequests;
    private long _peakWorkingSet;
    private int _accepted;
    private int _rejected;
    private int _shots;
    private bool _finished;

    public static Unit[] CreateRoster(BenchmarkOptions options, PlayerId blue, PlayerId orange)
    {
        var count = options.Units / 2;
        var rows = (count + 3) / 4;
        var units = new Unit[options.Units];
        for (var team = 0; team < 2; team++)
        for (var i = 0; i < count; i++)
        {
            var x = (13 + 2 * (i % 4)) * (team == 0 ? -1 : 1);
            var z = -48f + i / 4 * 96f / (rows - 1);
            var kind = i % 5 == 0 ? UnitKind.Tank : UnitKind.InfantrySquad;
            units[team * count + i] = new Unit(new EntityId(100 + team * count + i),
                team == 0 ? blue : orange, new SimVector(x, z), kind, 1_000_000);
        }
        return units;
    }

    public void ScheduleOrders(GameSession session)
    {
        var tick = session.World.Tick + 1;
        if (tick % OrderIntervalTicks != 0)
            return;

        var advance = tick / OrderIntervalTicks % 2 == 1;
        foreach (var unit in units)
        {
            if (session.World.LivingUnitOwner(unit.Id) is not { } owner)
                continue;

            var start = unit.Position;
            var forward = Math.Abs(start.Z) >= 22 && advance;
            var destination = advance
                ? new SimVector(forward ? 6 * Math.Sign(start.X) : start.X, start.Z + 3)
                : start;
            session.Commands.ScheduleMove(owner, [unit.Id], destination);
        }
    }

    public void RecordTick(long startedAt, IReadOnlyList<IGameEvent> events)
    {
        if (_startedAt == 0 || _finished)
            return;

        _tickMs.Add(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        foreach (var gameEvent in events)
        {
            if (gameEvent is ShotFired)
                _shots++;
            if (gameEvent is not CommandProcessed order)
                continue;

            if (order.Result.Accepted)
                _accepted++;
            else
                _rejected++;
        }
    }

    public bool RecordFrame(int activeUnits, long pathRequests)
    {
        if (_finished)
            return false;

        var now = Stopwatch.GetTimestamp();
        if (_startedAt == 0)
        {
            if (Stopwatch.GetElapsedTime(_createdAt, now).TotalSeconds < WarmupSeconds)
                return false;

            _startedAt = _lastFrame = now;
            _firstPathRequests = pathRequests;
            SampleMemory(now);
            return false;
        }

        _frameMs.Add(Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds);
        _lastFrame = now;
        if (Stopwatch.GetElapsedTime(_lastMemorySample, now).TotalSeconds >= 1)
            SampleMemory(now);

        var duration = Stopwatch.GetElapsedTime(_startedAt, now).TotalSeconds;
        if (duration < options.Seconds)
            return false;

        _finished = true;
        SampleMemory(now);
        WriteReport(activeUnits, pathRequests - _firstPathRequests, duration);
        return true;
    }

    private void SampleMemory(long now)
    {
        _process.Refresh();
        _peakWorkingSet = Math.Max(_peakWorkingSet, _process.WorkingSet64);
        _lastMemorySample = now;
    }

    private void WriteReport(int activeUnits, long pathRequests, double wallSeconds)
    {
        var display = DisplayServer.GetName();
        var report = new
        {
            schemaVersion = 1,
            requestedUnits = options.Units,
            activeUnits,
            requestedSeconds = options.Seconds,
            warmupSeconds = WarmupSeconds,
            wallSeconds,
            display,
            adapter = RenderingServer.GetVideoAdapterName(),
            viewport = DisplayServer.WindowGetSize().ToString(),
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = System.Environment.ProcessorCount,
            godot = Engine.GetVersionInfo()["string"].AsString(),
            frameSamples = _frameMs.Count,
            renderedFps = display == "headless" ? (double?)null : _frameMs.Count / wallSeconds,
            frameTimeMs = Summarize(_frameMs),
            simulationSamples = _tickMs.Count,
            simulationTimeMs = Summarize(_tickMs),
            pathRequests,
            acceptedMoveOrders = _accepted,
            rejectedMoveOrders = _rejected,
            shots = _shots,
            peakWorkingSetBytes = _peakWorkingSet,
            managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        File.WriteAllText(options.ReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"Battlefield benchmark completed: {options.ReportPath}");
    }

    private static object Summarize(List<double> samples)
    {
        samples.Sort();
        return new
        {
            p50 = Percentile(samples, 0.50),
            p95 = Percentile(samples, 0.95),
            p99 = Percentile(samples, 0.99)
        };
    }

    private static double Percentile(List<double> sorted, double fraction) =>
        sorted.Count == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Count * fraction) - 1];

    public void Dispose() => _process.Dispose();
}

internal sealed record BenchmarkOptions(int Units, int Seconds, string ReportPath)
{
    public static BenchmarkOptions? Parse(string[] args)
    {
        var index = Array.IndexOf(args, "--benchmark");
        if (index < 0)
            return null;

        if (args.Length < index + 4
            || !int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var units)
            || units is not (50 or 100 or 200)
            || !int.TryParse(args[index + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0
            || string.IsNullOrWhiteSpace(args[index + 3]))
            throw new InvalidDataException("Usage: -- --benchmark <50|100|200> <seconds> <report.json>");

        try
        {
            return new BenchmarkOptions(units, seconds, Path.GetFullPath(args[index + 3]));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Invalid benchmark report path.", exception);
        }
    }
}
