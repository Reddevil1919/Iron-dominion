# Test Strategy and Scenarios

## Levels
Unit tests (xUnit): command validation, income, capture, cooldown, damage, production. Integration tests: 20Hz tick ordering, AI decision pipeline, victory. Godot smoke tests: camera, selection, navmesh, export. Performance tests: 50/100/200 units; capture hardware, frame-time percentiles and simulation tick latency.

## Executable acceptance scenarios
| Test | Given | When | Then |
|---|---|---|---|
| T-001 | P1 owns U1 | P2 moves U1 | command rejected |
| T-002 | 0 credits | queue tank | no queue, no negative credits |
| T-003 | HQ income 5/s | 20 ticks | credits +5 |
| T-004 | neutral sector, P1 infantry inside | 200 uncontested ticks | sector belongs to P1 |
| T-005 | P1 and P2 infantry in sector | 200 ticks | no capture progress |
| T-006 | infantry in cover | 10 base damage from the protected side | 7 HP lost |
| T-007 | tank fires | before cooldown expires | no extra shot |
| T-008 | target behind LOS obstacle | attack issued | no damage |
| T-009 | target not visible | attack command issued | rejected |
| T-010 | 2 simultaneous lethal HQ hits | combat tick | draw |
| T-011 | blocked spawn | production completes | unit waits without duplication |
| T-012 | paused session | 10 render frames | world tick unchanged |
| T-013 | same seed, commands, fake nav | 2 headless replays | same canonical world hash |
| T-014 | enemy AI with fog | hidden enemy units | no target selected |
| T-015 | 50 active units | 5-min run | measure FPS/tick; record results |
| T-016 | hidden enemy unit and HQ | scout around the roadblock and withdraw | models, bars and picking follow contact visibility |
| T-017 | tank on an attack-move route | a hostile enters weapon range, then contact ends | pause to engage and resume the same route |
| T-018 | selected unit moving or focusing fire | issue hold position | the route and focus clear, and automatic fire continues |

## Example xUnit skeleton
```csharp
[Fact]
public void HeadquartersIncome_AfterTwentyTicks_AddsFiveCredits()
{
    var simulation = TestWorldFactory.CreateWithHeadquarters(initialCredits: 0);
    for (int i = 0; i < 20; i++) simulation.Tick();
    Assert.Equal(5, simulation.World.Credits(new PlayerId(1)));
}
```
`TestWorldFactory` is to be implemented; this is illustrative test code, not a runnable file yet.

## Deterministic replay protocol
RTS-014 runs a golden match through `GameSession`, including the shared
command scheduler and the orange AI. A fixture supplies two HQs,
production exits, neutral sectors, cover and a wall. Its seed chooses
one of two navigation detours. The orange HQ starts damaged so the
scenario reaches a result quickly.

The integration suite records the configuration schema version, seed
and processed orders as JSON, including rejected orders. It replays
them in fresh simulations with deterministic navigation and line of
sight, once in the recorded order and once in reverse arrival order.
Every simulation tick is hashed with SHA-256 using ID-sorted actors
and sectors, positions rounded to centimeters, balance, income,
health, training, capture state, result and the ordered events. The
golden match also checks a fixed timeline hash. When a gameplay
change affects the replay, review the scenario's checkpoints and
update the reference hash only after reviewing the new outcome.

Run the integration suite by itself with the following command.

```bash
dotnet test tests/IronDominion.Integration.Tests --configuration Release
```

The replay checks simulation behavior against the deterministic adapters.
Godot scene import, input and navigation have separate smoke tests.
Cross-platform bitwise equivalence with Godot navigation is outside this
test's guarantee.

## Performance scenario T-015

The Release benchmark runs 50 active units for five minutes after a five-second
warm-up. It sends move orders, records fire and reports frame and simulation
tick percentiles, process memory and path requests as JSON. The [performance
report](09-PERFORMANCE.md) gives the launch commands, workload and observed
results at 50, 100 and 200 units.

## Fog of war scenario T-016

The Godot smoke checks that a roadblock conceals an enemy tank within
sight range and that distant contacts are hidden. It uses normal mouse
input to distinguish a ground move from a targeted attack, reveal an
opposing squad and HQ, approach and fire on the squad with one attack click
from outside weapon range, and conceal the contacts again by withdrawing.
The xUnit AI visibility test covers scouting for a hidden enemy HQ.

## Tactical order scenarios

The simulation tests cover attack-move across a cooldown, resumption after
loss of sight or destruction, route rejection, hold during an engagement and
ownership validation. The integration test saves both order modes in a replay
recording and verifies the timeline after loading them in reverse order. The
Godot smoke sends keyboard and mouse input to confirm the same order modes
through the HUD and input shortcuts.

## Save and load scenario

The golden match saves and loads at three ticks covering active orders,
blocked production and combat cooldowns. The loaded matches finish at
the original tick and match the original per-tick digest. Additional
cases exercise invalid forms, incompatible versions and malformed
session state. The Godot scenario uses an isolated user directory and
checks the pause menu, resumed movement and production, and rejection
of an incompatible file without replacing the live session.

## Directional cover and suppression

The balance tests check protected and exposed angles, diagonal boundaries,
same-tick shots from different sides, the 24 reduced movement steps,
refresh and recovery. Integration checks restore active suppression
and read a format 1 save. The Godot cover scenario checks the map's
marked edge and the infantry and selection status during combat.

## Manual smoke checklist
Launch match; camera pan/zoom; select single/group; move around obstacle; fight; capture sector; produce infantry and tank; AI captures/attacks; destroy HQ; pause/restart; exit without errors.

## CI gates
GitHub Actions runs `dotnet restore`, `dotnet build --configuration Release` and `dotnet test --configuration Release`. The solution includes the golden match and replay suite.
A second job imports the project and launches the main scene headlessly with the pinned Godot .NET editor. The Windows x64 workflow exports the distributable ZIP and verifies the packaged game on a native Windows runner. Asset licenses are reviewed with the PR. See [Development setup](08-SETUP.md) for the Windows package.
