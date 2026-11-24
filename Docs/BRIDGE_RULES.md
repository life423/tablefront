# Bridge Gambit experiment

**Historical 0.6.0 record.** For 0.7.0's queued orders, bank blockers, directional cover, forward deployment and renewable bridge commands, read [current tactical rules](TACTICAL_RULES.md). The hard budget, six cover zones and four-node navigation below describe the original experiment.

This is a separate Windows mission built to test whether changing the physical battlefield creates interesting decisions. It keeps the four ordinary skirmish maps unchanged. Passing tests establishes the rules and routes; human play is still needed to judge the fun, clarity and difficulty.

## Mission

COBALT and EMBER each start with two Scouts, three Rifles and one Tank. Both can buy those same three classes at the ordinary costs, cooldowns and capacity. Gunner and Artillery recruitment is disabled for this experiment. VIOLET is not deployed.

Win by capturing and continuously controlling the enemy depot for **45 seconds**, or by destroying the enemy HQ. EMBER has the same victory conditions. The normal 350-point victory threshold is disabled only in this mission. Owning beacons still supplies normal credits and points. A contested depot or an enemy recapture attempt resets the continuous hold, including an unopposed recapture before the flag has fully changed ownership. Merely clicking the depot does not complete it: troops must navigate there, capture it, and keep control.

The three objectives are COBALT DEPOT at (-12,-8), EMBER DEPOT at (12,8), and ROTARY at the centre. Six rifle-cover areas sit on the banks outside the bridge's sweep.

## One shared movable bridge

The river is an actual movement boundary: the strip `|x| < 3.5` cannot be crossed away from the bridge. The central beam has half-length 10 and half-width 1.8. Its two docked directions are:

- **North:** southwest landing (-6,-8) to northeast landing (6,8).
- **South:** northwest landing (-6,8) to southeast landing (6,-8).

Each side receives **three bridge commands** and a **20-second personal cooldown** after an accepted command. An accepted request announces its actor and destination route, waits **three seconds**, and then rotates for **two seconds**. Duplicate, busy, exhausted, invalid-team and cooldown requests do not consume another command. The bridge also grants an **eight-second shared docking grace** so it cannot be immediately reversed before troops get an opportunity to cross. The exposed cooldown is the greater of the side's personal cooldown and this shared grace; both sides follow the same rule.

New troops cannot board during warning or movement. Troops already aboard can keep moving during the warning. Those still on the beam when rotation begins ride with its actual rotation, using their local deck coordinates. They do not walk or fire while being carried; rotation itself causes no fall, instant kill, health loss or teleport. Ordinary enemy fire may still hit them. Existing destinations, focus targets and Hold orders survive the move, and navigation replans after docking. Stop/Hold does not cancel the physical bridge underneath a passenger. The final docked bridge remains usable after all commands are spent.

## Navigation and scope

The game already uses a deterministic C# simulation shared with its offline verification and optional trainer. This experiment extends that system rather than introducing a second scene-based movement authority or a NavMesh package. It adds no packages, gameplay colliders, new physics controller or training run.

Mission navigation uses a four-node visibility graph: the troop's current position, the destination and two radius-safe bank/bridge portals. Legal movement is the union of the two banks and the rotated, footprint-inset deck. Segment validity uses exact interval clipping against those convex regions, including their overlaps, so routes cannot cut through a thin illegal corner between samples. The actual movement and soft-separation result is constrained by the same geometry. Scouts, Rifles and Tanks use their existing different base radii.

Orders into invalid water project to the closest legal position inside the simulation; the player controller can reject a water click with visible feedback before issuing it. Formation offsets also project legally. An unavailable cross-bank route sends a troop to a safe bank approach and preserves its order with `WaitingForBridge`, then replans after docking. Explicit pursuit of a moving passenger uses the same route rules. Water blocks movement, not bullets; this mission does not add fog, line-of-sight obstacles or ballistic blocking.

## AI and learning

The neural policy remains the existing frozen **40→32→10** strategic commander. It still chooses strategic destinations, attacks, defence and paid recruitment. It has not been trained to understand the river, bridge or new mission victory condition, and it does not learn during this mission.

A separate, explicit prototype bridge controller evaluates the AI's own existing cross-bank orders every three seconds after the opening eight seconds. When changing the route would reduce average travel cost by more than three units, it may request that route through the same command-budget, timing and grace checks as the player. This is deterministic terrain-control logic, not newly learned bridge strategy or a replacement strategic commander. It does not give the AI extra bridge commands, movement permissions or weapon bonuses. Training and CudaRL integration remain paused.

## Recorded verification

The [focused rules record](../TestResults/bridge-mission-simulation.json) contains **57 passing checks**. It covers all three unit classes in both crossing directions and both dock states, footprint legality, warning/moving boarding restrictions, continued queued orders, continuous passenger rotation, retained destinations and Hold, direct pursuit of a carried target, passenger casualties, exhausted resources, mixed-formation congestion, real movement/capture/hold victory, recapture resets, HQ defeat and AI command spending.

All **8/8 frozen-neural mission rollouts** finished normally with finite, legal troop positions; the enemy used its bridge controller in **4/8**. This is liveness evidence, not a difficulty or human-fun benchmark. Policy SHA256 stayed `07ee559f4ae04a0023d0d451757a4bb2772e18c7aec1c52ca89eaefee3052181`.

Historical regressions also pass: [63 mechanics checks](../TestResults/bridge-mechanics.txt), [25 tactical checks](../TestResults/bridge-tactics.txt), [24 neural checks](../TestResults/bridge-neural.txt) including 10,000 probability comparisons and 12/12 original-map matches, plus [55 expanded-skirmish checks and 24/24 four-map matches](../TestResults/bridge-expanded-regression.json). Historical output files were restored byte-for-byte. The optional [trainer compile check](../TestResults/bridge-trainer-compile.txt) succeeded; its executable was never run.

Editor controls, rendering and packaged-player checks are recorded in the [release evidence](RELEASE_v0.6.0.md). These simulation results do not substitute for a human playtest.
