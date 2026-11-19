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
| T-006 | infantry in cover | incoming base 10 damage | 7 HP lost |
| T-007 | tank fires | before cooldown expires | no extra shot |
| T-008 | target behind LOS obstacle | attack issued | no damage |
| T-009 | target not visible | attack command issued | rejected |
| T-010 | 2 simultaneous lethal HQ hits | combat tick | draw |
| T-011 | blocked spawn | production completes | unit waits without duplication |
| T-012 | paused session | 10 render frames | world tick unchanged |
| T-013 | same seed, commands, fake nav | 2 headless replays | same canonical world hash |
| T-014 | enemy AI with fog | hidden enemy units | no target selected |
| T-015 | 50 active units | 5-min run | measure FPS/tick; record results |

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
Serialize initial config version, random seed and ordered commands; use fake deterministic navigation and LOS services; hash canonical world projections sorted by ID. Floating-point positions require quantization in canonical hash. Do not promise bit-identical replay across Godot/CPU versions without a stronger deterministic math design.

## Manual smoke checklist
Launch match; camera pan/zoom; select single/group; move around obstacle; fight; capture sector; produce infantry and tank; AI captures/attacks; destroy HQ; pause/restart; exit without errors.

## CI gates
`dotnet restore`, `dotnet build --configuration Release`, `dotnet test --configuration Release`, asset-license audit, optional Godot headless import/export job with pinned version. No fabricated pass reports: attach actual CI output.