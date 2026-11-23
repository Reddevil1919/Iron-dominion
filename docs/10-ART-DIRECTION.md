# Contemporary art direction

Iron Dominion takes place in a fictional present-day conflict. The first
battlefield is a temporary military logistics depot. Concrete approaches,
patched asphalt, gravel, conifers and weathered freight containers establish
the setting. Warm low-angle sunlight and cool ambient fill define the light.
Both forces use muted uniforms and paint. Blue and orange identification
panels and existing tactical overlays distinguish the forces at overview zoom.

| Subject | Visual language | Motion |
| --- | --- | --- |
| Infantry | Four helmeted soldiers with vests, packs and small identification panels. Cool olive-pattern fabric and a deeper pack for Blue. Warm clay tones and a flatter pack for Orange. | Weighted joints with staggered steps, planted stances, and foot lift. The squad lowers in cover, takes staggered positions at the halt and recovers into a stride when leaving. |
| Tracked armor | Low hull, exposed track wheels, weathered armor panels, rear grille and contrasting identification marks. | Each belt follows travel and steering. The armor pitches at launch and braking. The turret turns separately and the gun recoils and recovers. |
| Buildings | Concrete command posts with roof equipment. Barracks use low ribbed metal roofs and repeated doors. | Building damage and destruction retain the tactical feedback used in the original battlefield. |
| Ground | Service roads, slab yards and scrub transition through gravel verges. Directional cover uses a compacted gravel surface and a dark protective edge with ochre diagonal markings. | Surface wear and markings remain fixed in world space at every camera zoom. |

All geometry and ground shading in this pass are authored in the repository.
The models are sized for an RTS camera. Collision, visibility, navigation,
combat and saved game positions use the same simulation data. Factions,
equipment and identification markings are fictional.

## Infantry reference

One skinned mesh and one Godot skeleton render the four members of each
squad. Each soldier uses sixteen joints. The meshes for each faction and
their skin binds are shared by its squads. The view generates the final pose
from movement speed, cover, fire events and suppression. The planted part of the
gait moves the boot rearward to counter ground travel on straight runs.
On turns and during transitions there can still be short foot slides.

On marked gravel, movement uses a shorter, lower stride. At a halt the
front pair settle onto opposite knees while the rear pair crouch and scan
outward. Their boots reposition in short lifts with staggered timing.
The front pair can shoulder and fire their rifles from the low stance.
Suppression adds a deeper tuck. A renewed move brings the squad back into
a stride. A loaded match restores the resting pose from the cover and
suppression state.

The uniform shader uses bind-space coordinates so the camouflage moves with
the fabric. Pouches, webbing and boots use muted rough surfaces. Metal
equipment has a separate roughness and reflectance response. Team markings
are placed on the pack and sleeves and stay readable at tactical zoom.
The look uses original programmatic geometry and materials. Motion uses
authored procedural poses and IK.

The target for a later art pass is finer cloth detail and authored
transitions for turns and going prone. Check replacement models
at tactical zoom and overview. Preserve silhouette, faction identification
and the frame budget at 50 and 100 units.

## Tracked armor and combat feedback

Each belt follows a closed mesh around the running gear. A shared shader
draws tread links with separate phases for left and right. Travel and
steering drive the phases. The sprung armor above the belts responds to
acceleration and braking. The paint finish and faction marks stay on the
same panels while the suspension moves.

Infantry fire poses the front pair's rifles and staggers three brief
recoil pulses across them. Flashes stay at the rifle muzzles. Small
smoke wisps and impact puffs follow the burst cadence.

A cannon shot produces one larger, irregular flash with gun recoil,
layered smoke and low dust kicked up under the muzzle. A compact shell
passes between the muzzle and the target. The heavier impact adds sparks
and debris that fall and fade near the hit. Effects follow the existing
shot event. Damage resolves on the simulation tick.

Rifle flashes, cannon flashes, shells, impacts and track dust use bounded
mesh batches. They respect fog of war and can finish a lethal impact
at a target's last visible position. Loading a match clears the batches.

## Surface impacts

Hits near infantry strike the ground in front of the squad. Dirt and
gravel kick up warm dust, grit and a dark mark. Concrete walls and
slabs give off pale dust and angular chips. Asphalt throws darker
aggregate. Tank hulls give a brief fan of sparks and retain a soot mark
on the struck panel as the tank moves.

The ground shader and the impact system use the same paved areas authored
in the main scene. Gravel parcels take precedence over the pavement under
them. Rifle bursts use smaller impacts than cannon fire. Marks fade over a
few seconds. Visible impacts follow the existing combat events.

## Tracks and footstep dust

Each belt leaves an independent tread pattern on loose soil and gravel.
The soil shows darker compressed tracks and warmer dust. The gravel has
lighter, shorter-lived dust and a weaker imprint. The authored asphalt
and concrete remain clean. A small cloud starts at the leading soldier's
boot during a footfall on loose ground.

The tread marks stay in place as the tank moves on. They fade between
7.5 and 12 seconds on the match clock. Enemy marks render in visible
ground cells after a visible pass. Loading clears the earlier trails.
The tread mesh uses a bounded instance pool, and puffs use bounded
particle batches.

## Damage on living tanks

At 65% health or below, chips and abrasions expose metal on the hull and
turret. At 30% or below, the panels show deeper soot and the rear grille
emits dark smoke. The faction markings remain visible. The smoke rises
and spreads behind a moving tank. Emission follows match time and visible
actors, and destruction hands the effect over to the existing blast.
The thresholds are presentation rules derived from the match's health data.
The same armor finish is restored when loading a save.

## Tank destruction and infantry casualties

The tank blast throws the turret and several armor and track fragments
through the flash, dust and smoke. Fragments tumble, settle individually
and stay on the ground for three seconds each.

Infantry still has one shared health pool per squad. On defeat, each of
its four soldiers blends from their current pose through a buckling pose
into a prone position. The timing and limb poses vary within the squad.
The fallen uniforms have localized stains, and the bodies stay down for
three seconds. Ground stains start on contact, stay red for three seconds,
dry toward brown until ten seconds and fade to clear by thirteen seconds.

Enemy effects start at a visible loss. Bodies, stains and debris respect
ground visibility as units move. These effects use the match clock and
clear when a save is loaded.

## Depot landmarks and wayfinding

The central loading frame is built into the freight yard footprint. The
relay mast belongs to the western barracks roof. The two perimeter gates
stand outside the existing navigable edge. Road arrows and stop lines
mark the approaches to the gates.

The three capture points have ground names and distinct concrete aprons.
A marks Relay, B marks Service, and C marks Checkpoint. The segmented
capture outline follows the configured radius and changes color with
capture state. The floating sector letter becomes smaller at close zoom
to leave space for units, health bars and orders.

Gravel indicates directional cover. Ochre stripes on the dark edge mark
the protected side. Dashes mark the opposite boundary. The annotations
follow the authored parcel bounds and sit below the fog plane.
