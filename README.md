# Iron Dominion — Technical dossier

Hybrid RTS combining base building and resource economy with squad tactics, cover, and territory control. Original game design; no third-party game assets or IP included.

## MVP baseline
Windows desktop; Godot 4 .NET; C#; single player versus AI; 128m × 128m arena; two symmetric factions; HQ, barracks, infantry squads and tanks; one currency (credits), passive income plus capturable sectors; cover for infantry; victory by enemy HQ destruction. Target 20 simulation ticks/sec and 60 FPS on target machine; both are measurable targets, not guarantees.

## Documents
- [Game design](docs/01-GDD.md)
- [Business rules](docs/02-BUSINESS-RULES.md)
- [Architecture](docs/03-ARCHITECTURE.md)
- [C# contracts](docs/04-CSHARP-CONTRACTS.md)
- [Backlog](docs/05-BACKLOG.md)
- [Test plan](docs/06-TESTS.md)
- [Copilot agent playbook](docs/07-COPILOT-AGENTS.md)
- [Development setup](docs/08-SETUP.md)

## Key architectural decisions
Simulation is pure C# and never references Godot. Godot presents world state and supplies user commands and pathfinding through an adapter. Single authoritative simulation tick; all state mutation happens inside the tick. MVP pathfinding need not be bitwise deterministic across platforms; reproducibility testing must use a deterministic fake navigation adapter. Networked lockstep is out of scope.

## Definition of done
Runnable Windows build; one full match from start to win/loss; unit, integration and smoke tests passing; no critical crash; performance baseline recorded; all required assets licensed and credited.