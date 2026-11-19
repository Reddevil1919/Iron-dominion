# Game Design Document (GDD) — v0.1

## Vision and pillars
Iron Dominion is an original near-future military RTS. **Pillars:** (1) meaningful base/economic choices, (2) squad-level tactical positioning, (3) clear counterplay and readable information, (4) responsive controls.

## Audience, platform, camera
PC Windows, keyboard/mouse, single-player. Isometric 3D camera with edge/WASD pan, wheel zoom, optional Q/E rotation. 15–25 minute match aspiration.

## Core loop
Scout → capture sectors → gain credits → build/produce → maneuver behind cover → contest territory → assault HQ.

## Match setup and victory
Two symmetric factions; 128×128m arena, three neutral sectors, each player has one HQ, one barracks, two infantry squads and one tank. Starting credits: 500. HQ destruction ends match; draw if both HQs die in the same combat resolution tick. Pause/restart supported.

## Player interactions
Left click selects entity; drag selects units; Shift adds/removes; Ctrl+1–9 assigns control groups; right-click ground moves; right-click visible enemy attacks; A then click gives attack-move; S stops; H holds position; B opens construction menu. Orders can be queued with Shift after MVP.

## Units
- Infantry squad: 4 soldiers rendered together, **one gameplay entity** with shared HP (400), speed 4m/s, range 12m, 10 damage/shot, 1 shot/s, 100 credits, 8s training. Benefits from cover.
- Tank: 600 HP, speed 3m/s, range 16m, 45 damage/shot, 0.5 shots/s, 500 credits, 20s training. Ignores infantry cover bonus only if attack has an explicit anti-cover tag (not in MVP).
- Units auto-acquire enemies in vision within range unless on Hold Fire; targeting prioritizes ordered target, then nearest visible enemy.

## Buildings
HQ: 1500 HP, passive +5 credits/s, sector capture not available to buildings. Barracks: 700 HP, trains infantry; HQ trains tanks for MVP simplification. Both buildings are preplaced in MVP; freeform construction postponed until post-MVP.

## Economy and territory
Three capturable sectors. Infantry captures an uncontested sector by standing within 6m for 10s; enemy presence pauses capture and can reverse progress. Captured sector grants +2 credits/s. Income accrues in integer credits using fixed-point accumulator; no floating-point currency. Sector ownership shown on HUD and minimap (minimap post-MVP).

## Cover and combat
Static cover zones with a 0.7 incoming-damage multiplier for infantry squads positioned inside. No directional cover in MVP; directional cover, suppression, flanking bonuses, and destructible cover are post-MVP. Line of sight blocked by marked solid obstacles; shots require visibility and line of sight. No friendly fire in MVP. Cooldown is tick-based. Damage rounded using an explicit deterministic rule (round half away from zero).

## AI
Rules/utility hybrid: produce units when affordable, capture nearest neutral sector, defend HQ when threatened, attack enemy HQ when force threshold met. Decision every 40 ticks. AI must respect fog-of-war visibility.

## Presentation
Prototype uses primitive meshes and original placeholder icons. Feedback: selection ring, health bars, attack effects, capture progress, build queue, economy, end-game overlay. Sounds optional for MVP.

## Explicit exclusions
Online multiplayer, aircraft, naval units, tech tree, hero abilities, physics destruction, realistic ballistics, advanced formations, procedural campaigns, monetization, historical factions.

## Balance and analytics
Track match length, income by source, units produced/lost, sector control duration, damage dealt, AI victory rate. Tune from telemetry rather than assumptions.