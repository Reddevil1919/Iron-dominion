# Business Rules — v0.1

All values are configurable and versioned. Rules run only inside the simulation tick.

| ID | Rule | Verification |
|---|---|---|
| BR-001 | Every entity has a unique positive EntityId and an owner (neutral allowed for sectors). | Duplicate ID rejected |
| BR-002 | Commands must originate from the owner of selected entities. | Enemy unit command ignored |
| BR-003 | Dead units/buildings cannot receive orders or act. | Zero-HP action rejected |
| BR-004 | Move destinations must be in map bounds; unreachable path returns failure without teleport. | Blocked path test |
| BR-005 | Attack target must be alive, hostile and currently visible to owner. | Fog target rejected |
| BR-006 | Fire requires weapon cooldown elapsed, range and line-of-sight. | Cooldown/LOS tests |
| BR-007 | Damage reduces HP, clamped to [0, MaxHp]; destruction event once. | Duplicate death prevented |
| BR-008 | Infantry in cover multiplies damage by 0.7 for attacks from the protected arc. | Front 10→7 and flank 10→10 |
| BR-009 | Currency cannot become negative. Production reserves full cost on acceptance. | Insufficient funds test |
| BR-010 | Queue processed FIFO; spawn requires valid free location; blocked spawn waits. | Queue/spawn test |
| BR-011 | HQ +5 credits/s; owned sector +2 credits/s, credited at fixed tick intervals. | 20 ticks = 5 credits from HQ |
| BR-012 | Only infantry captures sectors; capture radius 6m; time 10s uncontested. | Capture timing test |
| BR-013 | Opposing infantry contest sector and pause capture; ownership changes only after full capture. | Contest test |
| BR-014 | Vision is player-specific; hidden enemy positions are not leaked to AI or UI. | Visibility isolation test |
| BR-015 | Simulation uses fixed 50ms ticks; pause prevents ticks; rendering does not mutate domain. | Pause test |
| BR-016 | HQ destroyed => defeat; simultaneous HQ destruction => draw. | Victory tests |
| BR-017 | Commands are ordered by (scheduled tick, player id, sequence number) to avoid nondeterministic arrival order. | Replay test |
| BR-018 | Config loading validates all costs, rates, ranges and HP (nonnegative; max HP > 0). | Invalid config test |
| BR-019 | AI uses same command validation and visible-world query as human. | AI cheating test |
| BR-020 | Match ends atomically; later commands are ignored. | Post-game command test |
| BR-021 | A damaging hit suppresses surviving infantry and refreshes the movement penalty. | Speed is 65% for the next 24 ticks |

## State machines
Unit: Idle → Moving → Attacking → Idle; any live state → Dead. AttackMove is a movement mode. Production: Queued → Active → ReadyToSpawn → Completed; invalid enqueue rejected. Sector: Neutral / Owned(P1) / Owned(P2), plus capturing team/progress.

## Event semantics
Events are immutable records published after system processing in tick order: UnitSpawned, UnitDamaged, EntityDestroyed, SectorCaptured, CreditsChanged, ProductionCompleted, MatchEnded. Every event includes TickNumber and event-specific IDs. UI events must not be authoritative game state.

## Edge cases
Unreachable target, target dies during move, blocked spawn, two players attack HQ on same tick, units at map edge, capture contested on completion tick, paused game with pending commands, queue cancellation (post-MVP). Tick order and tests define resolution.
## Construction

A live friendly headquarters submits the order. The map supplies bounds,
one build zone per participating player, and static exclusion rectangles.
The entire rotated footprint must fit inside the map and the player's zone.
Buildings, pending sites, exits and units can block placement. Edge contact
between footprints is allowed. The simulation charges accepted orders once,
in tick, player and sequence order. The acceptance tick counts toward the
duration, and completion assigns an entity ID from the shared allocator.

| Building | Credits | Time at 20 Hz | HP | Footprint (m) |
|---|---:|---:|---:|---:|
| Headquarters | 650 | 25 s | 1,500 | 11 × 9 |
| Barracks | 250 | 12 s | 700 | 11 × 9 |
| Tank factory | 450 | 20 s | 1,000 | 14 × 10 |
| Airport | 500 | 22 s | 900 | 16 × 14 |
| Research center | 350 | 18 s | 750 | 10 × 8 |

A player can have one living or pending reserve headquarters at a time.
The slot opens again when a completed reserve is destroyed. Base income is
paid once per player with any living headquarters. Defeat is evaluated after
combat, before a site due on that tick can complete. Living completed
buildings take damage. The barracks and tank factory have production exits.
Airport production and research use their own later milestones.
