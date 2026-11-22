# Contemporary art direction

Iron Dominion takes place in a fictional present-day conflict. The first
battlefield is a temporary military logistics depot. Concrete approaches,
patched asphalt, gravel, conifers and weathered freight containers establish
the setting. Warm low-angle sunlight and cool ambient fill define the light.
Both forces use muted uniforms and paint. Blue and orange identification
panels and existing tactical overlays distinguish the forces at overview zoom.

| Subject | Visual language | Motion |
| --- | --- | --- |
| Infantry | Four helmeted soldiers with vests, packs and small identification panels. Olive kit for Blue and earth tones for Orange. | Leg and torso motion paced by ground travel, staggered per soldier. The body lowers under suppression and the arms react to visible shots. |
| Tracked armor | Low hull, exposed track wheels, rear grille, turret silhouette and spare equipment. | The hull follows its movement heading. The turret turns separately when a shot resolves and the barrel recoils and recovers. |
| Buildings | Concrete command posts with roof equipment. Barracks use low ribbed metal roofs and repeated doors. | Building damage and destruction retain the tactical feedback used in the original battlefield. |
| Ground | Service roads, slab yards and scrub transition through gravel verges. Directional cover has a distinct brown edge. | Surface wear and markings remain fixed in world space at every camera zoom. |

All geometry and ground shading in this pass are authored in the repository.
The models are sized for an RTS camera. The infantry uses procedural joint
motion on instanced mesh parts. Collision, visibility, navigation, combat
and saved game positions use the same simulation data. Factions, equipment
and identification markings are fictional.

The close-up assets are an initial pass. Detailed cloth and metal textures,
a skinned infantry rig with authored transitions, and final effects need a
separate production pass. Check any replacement model at maximum tactical
zoom and overview before adopting it. Preserve silhouette, friendly/enemy
identification and the frame budget at 50 and 100 units.
