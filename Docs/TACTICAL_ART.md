# Bridge Gambit tactical scenery

The two bank defenses are rendered from `BattleSimulation.Tactics`. The solid supply bunkers match their shared rectangular movement and shot-blocking geometry exactly: centers (−12, −4) and (12, 4), half-size (1.1, 2.6), height 2.1. Roofs, cast ribs and doors remain inside that silhouette. They add no physics colliders. The simulation owns picking, movement, line of sight and damage.

Each bank has one neutral, open-backed sandbag firing position. Its forward brass 120° arc matches the actual protected incoming direction at (−10, −7) and (10, 7). The full circle and four-sided barricades used on ordinary maps are intentionally absent here. Flanking removes rifle cover protection; it does not add arbitrary bonus damage. The North landing approaches the front. The South landing offers a route around the solid bunker toward an exposed side/rear firing position.

Selected or hovered rifles in these areas show a small forward arc around their physical gaming base. Actual `CombatEvent.TargetId`, `ProtectedHit` and `FlankedHit` drive short protected-front or exposed-rear hit arcs on the actual target. Rear feedback differs in shape as well as color. Reduced motion suppresses transient feedback and retains inspected protection direction. No new floating labels are added.

The rendered review found those small hit arcs easy to miss among unit and Depot rings. The existing HUD command area now also shows a 1.4-second selected-force message from actual protected/flanked combat events. It explicitly explains reduced frontal damage or bypassed cover and follows the relevant selected troop; it does not add a permanent panel or change damage.

ROTARY has two equal physical three-slot command capacitors. Raised colored caps show actual commands remaining for each team; a spent command leaves an empty dark socket, giving a shape cue as well as color. A small gauge shows only the owning team's actual uncontested partial recharge while below capacity. It vanishes at full capacity, loss or contest. Existing objective ownership cues and the HUD explain the mechanic. The display never invents a countdown or implies recharge while full.

Hovering or focusing the enabled bridge action shows a contextual blue outline of the other alignment before a command is spent. Leaving that action hides the outline; an actual warning overrides it with the actor's color and true destination. `ClearBattle` and map changes reset planning. The new accepted-order/formation preview owns all route lines. Old straight pooled guides are disabled, while the existing acceptance cue remains.

The walnut, brass, canvas, slate and ivory palette remains the same as the existing miniature table. Mission-only scenery keeps decorative rocks/trees clear of the logical obstacles, cover and bridge sweep. Ordinary map art and protection behavior are unchanged. One tactical-art instance, shared kit meshes/material batches and six fixed command indicators are reused across rematches. All generated meshes remain owned and released by `TablefrontWorld`.

## Verification

`AgentScripts/VerifyTacticalArt.Run()` passed [33 runtime checks](../TestResults/tactical-art-runtime.txt): shared poses and bounds, no extra colliders, supported palette materials, bounded batches, exact front/rear hit classification and feedback, reduced motion, real ROTARY navigation/capture/recharge, contextual bridge planning and resource reuse.

`AgentScripts/CaptureTacticalMission.Prepare(string)` prepares `opening` with the actual opening camera; `north`, `planning`, `front-cover`, `flank-hit`, `recharge`, `recharged`, and `moving-rider` for inspection. The two-rifle shot fixtures are explicitly staged on legal ground, then use actual shots and damage. The rear shooter navigates from the South landing around the real blocker. Bridge poses, passenger movement, ROTARY capture and recharge come from actual requests, orders and simulation steps. `Restore()` returns normal controls/effects and title without changing preferences. Only the root agent runs Editor commands and captures.

The eight 1280×720 art states were captured and the physical bridge/cover/recharge states independently reviewed; minimum-size and native-resolution frames were checked during integration. Actual button focus now stages the planning capture's HUD explanation and ghost together. The [release review](RELEASE_v0.7.0.md) records the size matrix, conservative craft scores, fix pass and remaining roof/gauge/readability concerns. Tablet layout is outside this Windows-first mission pass.
