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
| Tracked armor | Low hull, exposed track wheels, rear grille, turret silhouette and spare equipment. | The hull follows its movement heading. The turret turns separately when a shot resolves and the barrel recoils and recovers. |
| Buildings | Concrete command posts with roof equipment. Barracks use low ribbed metal roofs and repeated doors. | Building damage and destruction retain the tactical feedback used in the original battlefield. |
| Ground | Service roads, slab yards and scrub transition through gravel verges. Directional cover has a distinct brown edge. | Surface wear and markings remain fixed in world space at every camera zoom. |

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
