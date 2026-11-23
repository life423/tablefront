# Expanded skirmishes

This update adds three battlefields to the original Greenfield and two units to the three-unit roster. It preserves the 50×50 board, three equal starting armies, 26-unit cap, 350-point victory condition, paid recruitment and shared player/AI combat rules. The added battlefields are skirmish layouts, not a scripted story campaign.

## Battlefields

`TablefrontBattlefields` supplies the same HQ, beacon and cover coordinates to rules and art. New layouts use rotationally equivalent approach distances for all three seats. Comparable starting geometry is a fairness constraint, not evidence of perfectly balanced difficulty.

| Battlefield | Tactical difference |
| --- | --- |
| Greenfield | Original meadow: WEST/EAST/NORTH beacons, original HQs and six flanking cover patches. Tutorial and historical replay/training defaults retain these coordinates. |
| Ridgeway | Beacons sit farther apart, at a 16-unit radius. Holding a wide front and protecting the longer reinforcement routes matters. Cover flanks each beacon at a 4.8-unit offset. |
| Dust Basin | Beacons cluster near the centre, at a 7.5-unit radius. The armies meet earlier around a compact battle line. Cover sits closer to each beacon, at a 3.4-unit offset. |
| River Crossing | HQs and beacons rotate into a different approach pattern, with three shallow, passable ford positions at a 12.5-unit radius. Cover uses a 4.2-unit offset. |

All capture rings and cover fit within the board. Ground, routes and shallow ford scenery remain passable; decorative terrain does not introduce hidden navigation blockers. Holding a beacon uses the same real capture/scoring rules on every battlefield. There are no map-specific stat bonuses.

## Five-unit roster

| Unit | Cost | Health | Speed | Range | Direct weapon | Role |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Scout | 55 | 65 | 4.15 | 4.8 | 6.5 every 0.58s | Fast flanker; capture weight 1.5 |
| Rifle | 85 | 115 | 2.85 | 6.4 | 11.5 every 0.82s | Flexible infantry; 35% less damage in marked cover |
| Tank | 170 | 330 | 1.95 | 8.2 | 32 every 1.7s | Armoured anchor; 20% bonus against tanks and light splash |
| Gunner | 115 | 145 | 2.45 | 5.8 | 9 every 0.32s, modified by target | Shorter-range sustained fire against infantry; weak against tank armour |
| Artillery | 190 | 125 | 1.5 | 12.2 | 40 every 2.5s | Slow, fragile long-range support; wider splash and a 4.5-unit minimum range |

Gunner damage is multiplied by 1.25 against Scouts, Rifles and Gunners; 0.75 against Artillery; and 0.35 against Tanks and HQs. Gunners do not receive Rifle-only cover protection. Their shorter range, lack of cover protection and weakness against armour preserve reasons to field other units.

Artillery splash reaches 3.1 units around an impact, falling linearly from 60% of direct damage to zero. Tank/HQ-gun splash retains its existing 1.65-unit radius and 22% peak. Both splash rules affect opponents only, equally for humans and AIs. Artillery cannot shoot inside 4.5 units. Idle artillery does not automatically flee a close attacker. An explicit focus-fire order makes it seek firing distance, slowly enough that fast units can close the gap. A hold command prevents that movement. Artillery needs screening troops; it is not a stronger Tank.

All recruits use normal supply, capacity and cooldown checks. Scout/Rifle/Gunner recruitment has a two-second shared cooldown, Tank 3.5 seconds and Artillery four seconds. The starting army remains two Scouts, three Rifles and one Tank per faction; the added classes are paid reinforcements.

## Orders

- A normal movement order may explicitly choose `attackMove=false`, allowing travel to take priority until the destination is reached. The legacy default remains attack-move for existing call sites.
- Attack-move permits automatic engagement along a route. A direct attack order focuses the selected enemy.
- Stop cancels travel and explicit targets. Units resume ordinary idle reactions to nearby opponents.
- Hold cancels travel and explicit targets, then permits automatic fire only at enemies already in range. Held units do not chase or retreat. Soft separation can still resolve overlapping miniature bases.
- A subsequent move or direct attack releases hold. Dead and unknown unit IDs are ignored safely.

The game controller owns keyboard/button bindings; the simulation exposes `OrderStop(ids, hold)` and the existing movement/attack APIs so all input devices use the same rules.

## Neural compatibility and the training pause

The shipped 40→32→10 policy remains frozen. No optimizer, training episode or CudaRL integration runs as part of this update. New maps and unit counters have **not** been learned by those weights.

Current expanded skirmishes enable `ExpandedRoster`. The existing neural policy still chooses all strategic actions, including whether to recruit reconnaissance, infantry or heavy support. An explicit deterministic roster adapter maps infantry recruitment to a Gunner when there are at least two Rifles and fewer Gunners than half the Rifle count; heavy recruitment maps to Artillery when there is at least one Tank and fewer Artillery than half the Tank count. Otherwise the original unit is recruited. This adapter is game logic, not learned unit selection or a scripted replacement commander.

The 40 inputs continue to aggregate Gunners into infantry count and Artillery into heavy count. All ten outputs remain valid. This preserves inference compatibility; it does not prove the old policy understands the new tactical roles. A future approved training run should evaluate a richer action/observation vocabulary and train against the final new rules rather than calling the compatibility adapter “trained.”

`ExpandedRoster` defaults to false for historical harnesses and replays. Practice forces the original recruitment mapping even if that flag was enabled. Humans and AIs have access to the same five paid unit types and receive identical weapon/cover/capture mechanics.

## Verification

`SimulationTests/Run-ExpandedSkirmish.ps1` compiles the actual runtime rules, Unity maths and frozen neural inference. It checks map initialization, capture/cover, unit costs, Gunner counters, Artillery dead-zone and splash behavior, stop/hold transitions, observation compatibility and deterministic roster adaptation. It then evaluates six frozen-neural matches on each battlefield, checking completion, finite/bounded states and actual deployment of the expanded roster. It does not link the trainer or write policy weights.

The [expanded rules evidence](../TestResults/expanded-skirmish-simulation.json) records **55 passing checks** and **24/24 normally completed frozen-neural matches**, six per map, with zero timeouts or invalid states. All 24 matches actually deployed new units through the explicit roster adapter. The policy bytes remained unchanged.

The original-rule regression reruns also pass: [63 mechanics checks](../TestResults/expanded-mechanics.txt), [25 tactical checks](../TestResults/expanded-tactics.txt) and [24 neural compatibility checks](../TestResults/expanded-neural.txt). The neural comparison covered 10,000 probability samples with maximum absolute error 2.68221e-7 and zero seeded action mismatches; all 12 original-map rollouts finished, captured objectives and produced more than 100 combat events, with all 36 commanders moving away from their HQs. Historical baseline output files were restored byte-for-byte rather than relabelled as new results.

The optional [trainer compile check](../TestResults/expanded-trainer-compile.txt) succeeded with the added shared battlefield source. The trainer executable was not run, and neither training weights nor CudaRL state changed. Harness/project source includes were updated so existing verification and future approved training remain buildable.

These matches establish liveness and rules compatibility, not human fun, fair difficulty or commercial readiness. The [0.5.0 release record](RELEASE_v0.5.0.md) adds Editor, Windows build, installer, native startup and short performance evidence. Human playtesting and larger/longer performance samples remain necessary for release decisions.
