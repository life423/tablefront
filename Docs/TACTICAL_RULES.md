# Tablefront tactical mission rules

This iteration tests three gameplay proposals: dependable order planning, positional combat, and repeatable control of the bridge. They are implemented rules, not evidence that the game is fun or balanced. Windows PC remains the lead platform. Training and CudaRL integration remain paused.

## Orders and formation planning

`OrderMove(ids, destination, attackMove, append)` and `OrderAttack(ids, targetId, append)` accept an optional final `append` flag, defaulting to false. They return the number of units accepting the order. Existing calls retain replacement behavior. Appending to a busy unit adds a waypoint; appending to an idle unit starts immediately. Each unit can hold one active order plus **12 queued orders**. Full queues reject the extra order without deleting accepted orders; the controller uses the return value to report full or partially full selections.

Stop, Hold, and a replacement order clear the queue. A movement leg completes at its final formation position, never at an intermediate navigation corner. A focus order completes when its target dies; dead or invalid queued targets are skipped. Subsequent orders use the target's current position when activated. Orders and remaining waypoints survive bridge warnings and rotation. Focus orders still pursue moving targets; attack-move still engages visible enemies along its route.

`GetFormation` is the shared query used by both previews and accepted movement commands. Ordinary maps retain the existing 1.45-unit grid. The mission uses at least the largest selected base diameter plus 0.16 units, projects invalid water/wall slots to legal positions, and searches bounded nearby alternatives to avoid overlapping destination footprints. This is a destination formation, not a rigid marching formation: units separate while navigating a narrow bridge.

`BuildOrderRoute(unit, origin, destination, output, out waiting)` is a read-only query over the same movement geometry used by the simulation. It returns the origin and legal waypoint polyline. During an unavailable crossing it ends at a safe bank approach and reports waiting; it does not draw a straight line across blocked water. Queries do not spend commands, change active orders, advance queues, or change unit waiting state. A queued preview uses each preceding leg's destination as the next origin; later moving-target destinations remain predictions until execution.

## Real bank obstacles and directional cover

Only Bridge Gambit adds the following geometry. The four ordinary maps and tutorial keep their existing terrain, targeting and cover rules.

| Object | Centre x/z | Half-size x/z | Orientation | Rule |
| --- | --- | --- | --- | --- |
| West bank wall | (-12, -4) | (1.1, 2.6) | 0 degrees | Blocks movement and shots |
| East bank wall | (12, 4) | (1.1, 2.6) | 0 degrees | Blocks movement and shots |
| West rifle cover | (-10, -7) | Radius 2.2 | Facing (0.8, -0.6) | Front cone of 60 degrees each side |
| East rifle cover | (10, 7) | Radius 2.2 | Facing (-0.8, 0.6) | Front cone of 60 degrees each side |

Both walls are 2.1 units high in the shared art contract. Their positions leave the rotating bridge clear and keep both depots reachable by a Tank footprint. The cover areas are neutral; either army's Rifles can use either position. Each side starts one of its existing three Rifles in its forward cover, with the remaining five units near HQ. This is a symmetric initial deployment, not an extra garrison, scripted Hold behavior, or a new AI capability. Every unit remains commandable by the human or existing neural commander.

For a Rifle inside cover, incoming fire within the front cone deals **65% damage**. Side and rear fire receives no cover reduction. There is no additional flank damage multiplier, and Scouts/Tanks do not inherit Rifle cover protection. The mission's former six omnidirectional cover zones are replaced by these two directional positions. Ordinary-map cover remains unchanged.

The North route approaches the front of the forward cover; the South route provides a longer approach toward its exposed side/rear. The walls make that approach require actual navigation. This creates an opportunity to flank, not a guarantee that the enemy will remain in cover. The frozen commander may move any unit away.

Line of sight is symmetric and applies to automatic acquisition, explicit focus fire, troop-to-HQ shots, HQ turrets, hit resolution, and splash rays. A focus order with a blocked shot moves around the obstacle instead of firing through it. A target moving behind a wall after acquisition blocks the pending hit. Open water still blocks movement only; bullets can cross it. There is no fog of war, simulated vertical ballistics, destructible wall, or new scene-physics authority.

Movement uses an exact visibility graph with origin/destination, two bridge portals and eight radius-expanded obstacle corners (maximum 12 nodes). Exact interval clipping checks the bank/deck union; slab intersections reject wall crossings. Movement and soft separation are constrained by the same geometry. Graph work and corner count are bounded. Intermediate corners require closer arrival than final destinations so units do not stall just outside a turn. Edge passengers are captured slightly inside deck tolerance to keep floating-point rotation legal.

`Sim.Tactics.Obstacles` and `.Covers` expose the geometry consumed by art. `HasLineOfSight`, `IsProtected(troop, attackerPosition)` and `IsFlanked` expose actual rules. `CombatEvent.TargetId`, `ProtectedHit` and `FlankedHit` report the direct target's actual incoming-fire classification, including a lethal hit; they are not claims about every splash victim.

## Renewable bridge commands

Both armies start with **three commands**, and the capacity remains three. Continuously controlling uncontested ROTARY for **20 seconds** restores one command. Partial progress resets immediately on a two-army contest, an enemy recapture attempt even before the owner flag flips, loss of ownership, or reaching capacity. Full capacity cannot bank hidden recharge credit. Ownership continues while an uncontested captured objective is empty, following existing beacon ownership rules. Merely waiting for the personal cooldown does not restore a command.

`BridgeRechargeSeconds(team)` exposes current progress; `BridgeRechargeRequired` is 20. Recharge does not bypass the existing personal 20-second cooldown, global busy check, eight-second shared docking grace, three-second warning, two-second rotation, or passenger/boarding rules. Both sides pay and earn through the same APIs. The final docked bridge remains traversable even when both sides have zero commands.

The existing frozen 40→32→10 neural policy still chooses strategic actions. The separate deterministic bridge controller still compares travel costs of existing cross-bank orders and requests a route through the ordinary resource/timing checks. It can spend earned commands, but has not learned when to earn, preserve or spend them. No model training or online learning occurred in this iteration.

## Verification and limits

Run `SimulationTests/Run-TacticalMission.ps1` for [56 focused checks](../TestResults/tactical-mission-simulation.json). These cover queue sequencing/clearing/capacity, focus-kill continuation, invalid targets, pure previews, both-bank routing for all three classes, line of sight on both sides and both HQ directions, a pending hit blocked by target movement, actual directional damage, recharge parity/reset/cap, constrained formations, and 26 units completing both queued legs across a changing bridge and obstacles.

The concrete firing proof uses an identical Rifle at (10,7): a real North/frontal shot from (6,8) deals approximately **7.475**, while a real rear shot from (14,6.8) deals **11.5**. The mirrored bank gives the same result. Another check actually moves infantry from the South landing (6,-8), around the bank wall, to that rear firing position, then fires. These are gameplay transactions rather than a dot-product-only assertion.

The recorded 26-unit stress simulates 70 seconds; all units finish their active and queued orders legally. The standalone rules timing is recorded in the result, not claimed as native game performance. Ten thousand warmed obstacle-route queries with a reusable output list allocate **zero managed bytes**. Formation queries use one bounded temporary selection list; the rendered preview and native-player performance still need their own measurements.

The updated [bridge regression record](../TestResults/tactical-bridge-simulation.json) passes 57 checks and completes 8/8 frozen-policy mission rollouts with legal finite positions. The cover fixture now checks legal centres, actual front-wall sweep clearance and landing clearance; its old circular six-cover exclusion no longer describes the new geometry. Rollouts exercise enemy bridge use in 3/8 cases. This is liveness evidence, not a human difficulty or enjoyment benchmark.

Ordinary-map regressions pass [63 mechanics checks](../TestResults/tactical-mechanics.txt), [25 tactical checks](../TestResults/tactical-legacy-tactics.txt), [24 neural checks with 12/12 matches and 10,000 probability comparisons](../TestResults/tactical-neural.txt), and [55 expanded checks with 24/24 matches](../TestResults/tactical-expanded-regression.json). Original historical output files were restored byte-for-byte. The optional trainer was [compiled only](../TestResults/tactical-trainer-compile.txt); it was not executed. Policy SHA256 remains `07ee559f4ae04a0023d0d451757a4bb2772e18c7aec1c52ca89eaefee3052181`.

Editor, controls, rendering, installer and native-player verification are separate release gates. These rules tests do not establish that the flanking choice or recharge rate is enjoyable; a PC playtest is the next design test.
