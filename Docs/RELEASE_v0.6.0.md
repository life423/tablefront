# Tablefront 0.6.0 — Bridge Gambit experiment

[**Download the Windows playtest installer (.exe)**](https://github.com/life423/tablefront/releases/download/v0.6.0-preview.1/Tablefront-Setup-v0.6.0.exe)

Bridge Gambit tests the idea that changing a physical tabletop battlefield can produce interesting tactical decisions. It is a separate mission, with the four normal skirmish maps, five-unit skirmish roster and six-step tutorial preserved. Windows PC remains the primary platform; tablet support is a later task. This free preview does not establish commercial readiness or proven human fun.

## Try the mission

1. Run Setup, then open **Tablefront** from the desktop or Start menu.
2. Choose the gold **Bridge Gambit · Try mission** action on the title screen.
3. Select your blue troops and click a bank or the bridge to move. Water blocks movement elsewhere.
4. Use **Open North/South crossing · R**, or press **R**, to request the other crossing. You do not need troops selected.
5. Capture and continuously hold **EMBER DEPOT for 45 seconds**, or destroy the enemy HQ.

You and EMBER use Scouts, Rifles and Tanks under the same recruitment, movement, damage and victory rules. EMBER wins by holding your depot or destroying your HQ. A contested depot or an enemy recapture attempt breaks continuous control and resets the hold timer. Normal 350-point scoring cannot bypass the mission objective.

**Retry Bridge Gambit** resets the mission, bridge and command budgets. Entering or retrying it preserves the selected normal skirmish map. Ordinary matches still start through **Play vs AI** and use their existing victory rules.

## The shared bridge

The bridge pivots between a southwest-to-northeast **North** crossing and a northwest-to-southeast **South** crossing. Each side has **three bridge commands** and a **20-second personal cooldown**. A request gives a visible **three-second warning**, followed by a **two-second rotation**. An **eight-second shared docking grace** gives troops time to use the new route before it can be reversed. The displayed cooldown includes that grace.

New arrivals wait on their own bank during a change. Troops already aboard can leave during the warning; those still on the beam ride with its rotation. They do not walk or fire while being carried. Rotation itself causes no falls, health loss or instant kills; ordinary enemy fire can still hit riders. Travel, focus-fire and Hold orders survive and replan after docking. The last docked route remains usable after both sides spend all their commands.

Both sides use the same request checks, budgets, timing and movement permissions. The warning names **YOU** or **EMBER**, and the mission strip shows the current/pending crossing and both remaining command budgets. The crossing button disables during warning, movement and cooldown; ordinary troop orders remain available. Pause and Help suspend gameplay and disable mission commands. Mission results offer retry and return to title rather than advancing the normal map sequence.

## Navigation and AI

Mission-only deterministic navigation connects the two banks through radius-safe bridge portals. Exact segment clipping prevents water cuts, and actual movement plus separation obeys the same walkable geometry. Formation destinations project to legal ground; unavailable crossings retain their orders and wait safely. Existing skirmishes keep their original movement rules. No NavMesh package, new physics authority or external dependency is introduced.

The existing **40→32→10 neural policy remains frozen**. It chooses strategic orders and recruitment, but has not learned the bridge or new objective. A separate route-cost controller may spend the enemy's bridge commands when switching would shorten its existing cross-bank orders. This is explicit prototype terrain-control logic, not newly learned bridge strategy. No training or CudaRL integration ran.

See [exact mission rules](BRIDGE_RULES.md), [interface behavior](BRIDGE_UI.md), [bridge artwork](BRIDGE_ART.md) and [product direction](PRODUCT_DIRECTION.md).

## Verification recorded during implementation

| Check group | Passing checks |
| --- | ---: |
| Bridge mission rules, routes and frozen inference | 57 |
| Original mechanics regressions | 63 |
| Tactical regressions | 25 |
| Neural compatibility | 24 |
| Four-map expanded-skirmish regressions | 55 |
| Mission controller integration | 20 |
| Bridge art integration | 27 |
| Bridge HUD integration | 24 |
| Existing art, controls, HUD, tutorial and control-group runtime regressions | 227 |

The [focused mission record](../TestResults/bridge-mission-simulation.json) covers all three classes in both directions and both dock states, blocked-water navigation, warning/moving boarding restrictions, passengers, resumed orders, direct pursuit of a carried target, Stop/Hold, casualties, exhausted commands, mixed formations, capture-to-victory, recapture resets and defeat. All **8/8 frozen-neural mission rollouts** finished normally with legal finite troop positions; their wins were **3 COBALT / 5 EMBER**, and the enemy spent bridge commands in **4/8**. This small sample establishes liveness, not balanced difficulty.

[Mechanics](../TestResults/bridge-mechanics.txt), [tactics](../TestResults/bridge-tactics.txt), [neural compatibility](../TestResults/bridge-neural.txt) and [expanded-skirmish evidence](../TestResults/bridge-expanded-regression.json) also pass. The neural rerun completed 10,000 probability comparisons with zero sampled-action mismatches and **12/12 original-map matches**; the four-map rerun completed **24/24 matches**. Historical baseline outputs were restored byte-for-byte. The optional trainer compiled but was never executed; shipped policy bytes remained unchanged.

All **522 checks passed**. Editor evidence is recorded in [mission controls](../TestResults/bridge-mission-runtime.txt), [bridge art](../TestResults/bridge-art-runtime.txt), [mission HUD](../TestResults/bridge-hud-runtime.txt) and [existing runtime regressions](../TestResults/bridge-runtime-regressions.txt). The controls regression helper now waits for the actual Cinemachine update before asserting screen positions; it also proves that its selection fixture has two visible units and one offscreen unit. No gameplay timing was altered for that check.

Rendered review covered seven mission states at 1280×720 and four states at 1024×768, 2560×1440 and 3440×1440, with additional title, Help, Pause and results captures at 1024 and 2560. The [interface record](BRIDGE_UI.md) documents capture artifacts and the follow-up priorities. Final compilation succeeded, and the Editor console reported zero current errors or warnings. The retained history contains two first-capture warm-up errors from opening an unrendered Game view; subsequent camera captures succeeded. The agent-owned Editor was restored to stopped Play mode, background execution disabled, auto-tick disabled and closed gracefully. Other Editors were left alone.

The Windows x64 build succeeded in **22.149 seconds**, with **107,993,652 reported build bytes**, zero build errors and zero warnings. [Build summary](../TestResults/bridge-build-report.json). The **29,767,624-byte Windows installer** upgraded the existing per-user installation from 0.5.0 to 0.6.0 with exit code zero. All **190 payload files** match the built files by SHA256. Both desktop and Start menu shortcuts target the startup-fix launcher in the install directory. [Installer evidence](../TestResults/bridge-installer-verification.json).

The actual desktop shortcut launched the installed player. Its log confirms Unity 6000.6.10f1, Direct3D 12, the RTX 5070 and completed scene initialization, without script exceptions or shader errors. A Windows permission prompt labeled **Windows installer** covered the desktop during the native screenshot check; no permission input was sent. Native title/mission visual confirmation is therefore pending that prompt being handled by the user. The player remains open, and the Editor captures above establish the reviewed mission appearance, not that blocked native screenshot. [Startup record](../TestResults/bridge-native-startup.json).

Mission frame-time measurements and physical audio listening remain unperformed; the historical 0.5.0 FPS benchmark does not measure this mission. Human fun and difficulty remain to be tested.
