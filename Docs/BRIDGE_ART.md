# Bridge Gambit visual treatment

The experimental mission turns the miniature tabletop into a mechanical play surface. A twenty-unit walnut-and-steel deck pivots around a central bearing, docking at two diagonal pairs of stone bank landings. Brass running edges, low truss sides, riveted uprights and cross-grain planks keep the existing painted tabletop language. Its rendered yaw is the negative of the simulation's x-z angle, so the visible endpoints agree with the actual North and South walkable routes.

The mission river is a recessed, blue-gray channel with a clearly defined bank at x = ±3.5. Supply roads remain on land and lead to both sets of landings; no painted road suggests a second water crossing. Groves, outcrops and grass clusters are excluded from the river and bridge sweep. Other battlefields keep their previous generated terrain and passability.

During the three-second warning and two-second movement, a hollow route footprint and direction arrow show the requested landing alignment. The requesting army supplies the blue or orange signal material; the HUD supplies explicit route, actor and timing text so color is not the only explanation. Active landings retain brass marks and inactive landings darken. Docking clears the preview rather than leaving a permanent overlay.

Troops render at deck height when crossing. Riders use the same interpolated deck pose during rotation and keep an idle gait, so smoothing cannot pull their feet into the channel or make them run on a moving bridge. Reduced motion removes additional visual easing without changing the actual movement, warnings or route outcome. The neutral ROTARY beacon stands on the pivot; the unused third headquarters is hidden in this two-army mission.

A quiet warning bell, two-second mechanism sound and docking latch are triggered once per observed phase transition. They use a dedicated low-gain source through the existing master listener volume, without a mixer or new package. Physical speaker/headphone listening remains a human check.

## Ownership and verification

The bridge is created once and reused on rematches. Static hardware and the moving deck are combined by their shared materials using the existing landmark mesh cache; landing indicators and two preview line renderers remain dynamic. Generated meshes remain owned by `TablefrontWorld` and are released with the world. There are no decorative colliders or scene-based navigation components; the simulation owns legal positions and bridge state.

The [27 passing art checks](../TestResults/bridge-art-runtime.txt) drive real routing, bridge requests and simulation time to check deck alignment, warning previews, actual riders, deck elevation, reduced-motion geometry, phase audio selection, enemy signal color, ordinary-map hiding and repeated mission resource bounds. `AgentScripts/CaptureBridgeMission.cs` prepares an actual opening camera plus North, South, Warning, Moving, rider and enemy-warning states. Helpers stay outside Assets, send no native input and do not ship.

Craft references consulted: `character-motion.md` and `visual-scorecard.md`. Unity 6000.6 scripting references checked: [Quaternion.Euler](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/quaternion/euler) and [LineRenderer.SetPositions](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/linerenderer/setpositions).

All seven 1280×720 states were independently inspected. The actual opening camera keeps both HQs and their starting units visible; closer composed views intentionally crop the battlefield. Warning outlines mark the correct future landings, player/enemy signals agree with their explicit HUD actor labels, and the selected Scout stays visibly on the planks during movement. The docked South frame clears the preview. The central beacon leaves the bridge silhouette readable. These stills do not establish human understanding of timing, physical audio quality or perceived fun.

Scoped craft review: palette discipline 4/5, style uniformity 4, lighting depth 3, silhouette 4, UI intentionality 3 and feedback visibility 4. Remaining priorities are human mission play, the wide-view contrast of the dark wooden deck, smaller-screen HUD readability, larger-army bridge congestion and physical audio listening. The bridge/control motion is the experiment; the score is a conservative frame-review heuristic, not a claim of finished commercial art.
