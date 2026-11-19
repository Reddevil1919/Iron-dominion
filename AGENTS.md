# Iron Dominion — Instructions for Codex

You are implementing a Godot 4 .NET / C# military RTS with a hybrid base-building and tactical squad design.

## Working agreements
- Read docs/ before implementation; document any assumption.
- Use small, reviewable increments; do not claim a game is playable without a validated runtime test.
- Keep pure C# domain and simulation projects free of Godot dependencies.
- Maintain deterministic fixed-tick simulation; use seeded randomness and ordered command processing.
- Separate simulation state from Godot scene nodes. Godot adapters render snapshots and translate user input into commands.
- Do not introduce Spring Boot, web APIs, databases, networking, or paid assets for the MVP.
- Write unit tests for commands, combat, production, victory, and determinism.
- Run dotnet test and Godot headless checks when available; report commands, failures, and missing dependencies.
- Commit no credentials, binaries, or unlicensed third-party art.
- Prefer primitive placeholder meshes to unblock gameplay.
- Keep IMPLEMENTATION_STATUS.md current with verified features, tests and remaining work.

## MVP
Windows single-player RTS: 128m x 128m map; human versus AI; infantry and tank; headquarters and barracks; passive credits; selection, move, attack, production, victory. Follow-on: cover, squads, capturable sectors, fog of war.

## Definition of done
Source compiles, tests pass, Godot scenes import in headless mode, the game launches and a human can finish a match. If graphical verification is unavailable, explicitly say so.
