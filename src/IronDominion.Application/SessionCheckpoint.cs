using System.Collections.Immutable;
using IronDominion.Simulation;

namespace IronDominion.Application;

public sealed record SessionCheckpoint(SimulationCheckpoint Simulation, ImmutableArray<CommandSequence> Scheduler, bool Paused);
