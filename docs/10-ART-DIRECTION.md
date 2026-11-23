# Contemporary art direction

Iron Dominion takes place in a fictional present-day conflict. The first
battlefield is a temporary military logistics depot. Concrete approaches,
patched asphalt, gravel, conifers and weathered freight containers establish
the setting. Warm low-angle sunlight and cool ambient fill define the light.
Both forces use muted uniforms and paint. Blue and orange identification
panels and existing tactical overlays distinguish the forces at overview zoom.

| Subject | Visual language | Motion |
| --- | --- | --- |
| Infantry | Four helmeted soldiers with vests, packs and small identification panels. Cool olive-pattern fabric and a deeper pack for Blue. Warm clay tones and a flatter pack for Orange. | Weighted joints with staggered steps, planted stances, and foot lift. The torso lowers under suppression, and both hands follow the equipment through aim and recovery. |
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
from movement speed, fire events and suppression. The planted part of the
gait moves the boot rearward to counter ground travel on straight runs.
On turns and during transitions there can still be short foot slides.

The uniform shader uses bind-space coordinates so the camouflage moves with
the fabric. Pouches, webbing and boots use muted rough surfaces. Metal
equipment has a separate roughness and reflectance response. Team markings
are placed on the pack and sleeves and stay readable at tactical zoom.
The look uses original programmatic geometry and materials. Motion uses
authored procedural poses and IK.

The target for a later art pass is finer cloth detail and authored
transitions for turns, kneeling and going prone. Check replacement models
at tactical zoom and overview. Preserve silhouette, faction identification
and the frame budget at 50 and 100 units.

## Tracked armor and combat feedback

Each belt follows a closed mesh around the running gear. A shared shader
draws tread links with separate phases for left and right. Travel and
steering drive the phases. The sprung armor above the belts responds to
acceleration and braking. The paint finish and faction marks stay on the
same panels while the suspension moves.

A visible shot starts with a brief, directional flash and drifting smoke
at the muzzle. Tank rounds and occasional infantry rounds show a short
moving tracer. A local flash and expanding dust puff mark arrival, with
short-lived sparks on tank impacts. The flight time belongs to the effect
and damage still resolves on the simulation tick.

Tank movement leaves a low dust wake. Bursts, tracers and track dust use
three bounded batches of camera-facing quads. The effects respect fog of
war, with a final impact allowed at a position where a destroyed actor was
visible. Loading a match clears the effect batches.

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
