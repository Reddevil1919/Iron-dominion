# GitHub Copilot Agent Playbook

## Working agreement
Read README, GDD, business rules, architecture and contracts before editing. Work in small branches/PRs, one issue per branch. Never invent a dependency or claim tests pass without running them. Preserve clean architecture; avoid broad refactors. Report files changed, assumptions, commands executed, actual outputs, remaining risks.

## Suggested specialist agents
1. **Architect**: ADRs, public contracts, dependency checks. Owns `docs/03-ARCHITECTURE.md`, reviews cross-module changes.
2. **Simulation**: domain state, command processing, combat, economy, sector logic. Owns `src/Domain`, `src/Simulation`.
3. **Godot**: scenes, camera, input, adapters, visual sync. Owns `game/`, adapter code.
4. **AI**: strategic and tactical policies respecting visible world. Owns `src/AI`.
5. **QA**: xUnit, replay, performance harness, CI. Owns `tests/`, CI files.

## Safe delegation order
Architect locks contracts → Simulation creates minimal world/tick → QA writes tests → Godot implements bridge → AI builds on snapshots → integration and profiling. Separate branches for nonoverlapping files; architect approves shared contracts. Avoid simultaneous modifications to `WorldState`.

## Ready-to-use prompts
**Architect:** "Read docs/01-GDD.md, 02-BUSINESS-RULES.md, 03-ARCHITECTURE.md and 04-CSHARP-CONTRACTS.md. Create the solution and project references for RTS-001–003 without implementing gameplay. Ensure Domain and Simulation have no Godot dependency. Explain any contract changes before making them. Run build/tests and report real output."

**Simulation:** "Implement RTS-002, RTS-003 and tests. Use fixed ticks and deterministic command ordering. Implement minimal world state and immutable read projections. No Godot types. Do not implement future features."

**Godot:** "Implement RTS-004 and RTS-005 in Godot 4 .NET using placeholder primitives. Bind UI input to application commands; never mutate simulation state directly. Keep camera and selection logic testable."

**AI:** "Implement RTS-011 using only IReadOnlyWorld visible projections and ICommandScheduler. Decisions every 40 ticks. Provide tests proving hidden enemy entities are not accessed."

**QA:** "Write xUnit tests T-001 through T-014 with deterministic fake navigation. Create a headless replay test and CI build/test workflow. Never report a test as passing unless executed."

## Pull request template
- Issue and scope
- Public API changes
- Gameplay behavior
- Tests run + actual result
- Screenshots/video if visual
- Performance implications
- Asset licenses
- Known limitations

## Review gates
Reject: direct Godot dependency in domain; wall-clock logic in simulation; mutation from presentation; hidden-information AI; silent catch-all exceptions; asset without license; undocumented API breaking change.