# Codex cloud execution plan

## Scope
Iron Dominion is a Godot 4 .NET / C# RTS inspired by base construction and tactical squad positioning. First build a **vertical slice**, not an entire commercial game.

## Phase 0 — Environment
1. Pin a compatible stable Godot 4 .NET version and .NET SDK in docs/08-SETUP.md.
2. Install Godot headless executable and matching export templates in the Codex cloud environment.
3. Add a Godot project with a simple battlefield scene, and a pure .NET simulation solution.
4. Confirm dotnet test, Godot --headless --editor --path game --quit and export steps.

## Phase 1 — Playable movement
- Isometric RTS camera, zoom and pan.
- Unit selection and box selection.
- Right-click move order.
- Basic navigable terrain and obstacle avoidance.
- Test with 10 units.

## Phase 2 — Combat
- Infantry and tank stats, health, target acquisition, attack orders and death.
- Tick-based attack cooldown and deterministic resolution.
- Target validation and tests.

## Phase 3 — Economy
- HQ, barracks, passive credits, build queue, unit spawn and HUD.
- Prevent overspending, invalid production and blocked spawn.

## Phase 4 — Enemy and endgame
- Basic AI produces units, defends, attacks.
- HQ destruction ends the match.
- Restart and victory/defeat UI.

## Phase 5 — Quality gates
- xUnit tests and repeatability tests.
- Godot headless import.
- Linux CI, Windows export artifact.
- Playtest on Windows and document observed defects.

## Delivery rules
Work in feature branches and PRs. Each PR must include changed behavior, tests run, screenshots if available, known limitations and a clear next step. Do not bypass failed checks.
