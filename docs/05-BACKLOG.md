# MVP Backlog — prioritized

Story points are relative estimates; adjust after first sprint. Dependencies matter more than dates.

| ID | Priority | Story | Points | Depends on | Acceptance |
|---|---|---|---:|---|---|
| RTS-001 | P0 | Bootstrap solution, Godot .NET and CI | 3 | — | Clean build and tests in CI |
| RTS-002 | P0 | Domain entities, config validation | 5 | 001 | Config errors covered |
| RTS-003 | P0 | Fixed tick and command scheduler | 5 | 002 | Stable tick order |
| RTS-004 | P0 | Map and RTS camera | 3 | 001 | Pan/zoom bounds |
| RTS-005 | P0 | Click/drag selection, control groups | 5 | 002,004 | Own units only |
| RTS-006 | P0 | Move command and path adapter | 8 | 003,005 | Units navigate around obstacle |
| RTS-007 | P0 | Combat/health/death | 8 | 003,006 | LOS/cooldown/damage tests |
| RTS-008 | P0 | HQ/barracks and unit production | 8 | 003 | FIFO queue and spawn |
| RTS-009 | P0 | Passive economy and sector capture | 8 | 003,006 | Contest/capture/income |
| RTS-010 | P0 | Static infantry cover | 5 | 007 | Reduced incoming damage |
| RTS-011 | P0 | Enemy AI | 8 | 006–009 | Produces/captures/attacks |
| RTS-012 | P0 | HUD and feedback | 5 | 005,007–009 | Credits, HP, production, sectors |
| RTS-013 | P0 | Victory/pause/restart | 3 | 007 | Full match finishes |
| RTS-014 | P0 | Automated integration and replay tests | 8 | 003–013 | Golden scenario passes |
| RTS-015 | P0 | Windows build and performance profiling | 5 | 013 | Export and metrics report |
| RTS-016 | P1 | Fog of war rendering | 8 | 007,009 | Enemy hidden visually |
| RTS-017 | P1 | Attack-move, hold position | 5 | 006,007 | Order modes tested |
| RTS-018 | P2 | Save/load | 8 | 003 | Versioned restore |
| RTS-019 | P2 | Directional cover and suppression | 13 | 010 | Balance/test suite |
| RTS-020 | P2 | Multiplayer prototype | TBD | architecture review | Separate design required |

## Suggested delivery sequence
Sprint 0: 001–003. Sprint 1: 004–006. Sprint 2: 007,008. Sprint 3: 009,010,012. Sprint 4: 011,013,014,015. P1 items can be added after a working match. Avoid parallel edits to shared world-state APIs.

## Definition of ready
Story has explicit scenario, dependencies, expected API, tests and placeholder asset plan.

## Definition of done
Compiles; tests pass; no Godot reference in pure C# projects; demo in game; documentation updated; reviewed diff; no unlicensed assets; performance regression checked when relevant.