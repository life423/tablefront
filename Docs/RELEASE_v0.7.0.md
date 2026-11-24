# Tablefront 0.7.0 — Plan the route, change the fight

[Windows playtest installer](https://github.com/life423/tablefront/releases/download/v0.7.0-preview.1/Tablefront-Setup-v0.7.0.exe)

This iteration develops the user's approved priorities 1–3: dependable army planning, meaningful positioning, and a bridge that creates tactical opportunities. Its working identity is **change the battlefield to change the fight**. That is a testable direction, not proof of commercial distinctiveness, balanced difficulty or human fun.

## Try it

Open Tablefront using the desktop shortcut and choose **Bridge Gambit · Try mission**. Select blue troops, then hover ground to see their routes and formation destinations. Click to move, or click an enemy to attack. **Shift-click** adds the next order; release Shift to replace the plan. **Stop** clears it. The visible **Move** action can target ground occupied by a friendly unit; Escape cancels the pending action. Existing StarCraft-style numbered groups remain: Ctrl+number saves, Shift+number adds, number recalls, double-tap focuses.

Each troop can hold one active command and 12 pending orders. The group count reports the longest selected queue rather than counting one click six times. Preview and movement use the same footprint-safe geometry. Gold shows the proposed route; blue shows accepted selected-unit routes. A changing crossing shows an orange waiting route ending on the safe bank. Water and wall destinations are rejected visibly; the wall cross sits above the wall rather than disappearing inside it.

## A flank changes the outcome

Two solid bank bunkers block both movement and shots. Each side starts one existing Rifle in an open-backed forward sandbag position, with the remaining five troops near HQ. There are no additional units, health or weapon bonuses. Cover protects Rifles against shots within its marked front arc; side/rear fire bypasses the existing 35% reduction. It does not award extra flank damage.

The North landing approaches the cover front. The South landing offers a route around the bunker to an exposed firing position. Hover or keyboard-focus the enabled bridge action to inspect its alternate alignment and landings before spending a command. A short contextual message in the existing command area explains real protected or flanking hits for the selected force. These messages expire and do not create another permanent panel.

ROTARY now has a sustained purpose. Hold it uncontested for 20 seconds to regain one bridge command, capped at three. Contest, loss or an enemy capture attempt resets partial recharge; full capacity cannot bank more progress. Both sides share these rules, the 20-second personal cooldown, three-second warning, two-second movement and eight-second docking grace. Actual command caps, empty sockets and a small recharge gauge match the simulation. Riders keep their orders during rotation.

Win by holding the enemy Depot uncontested for 45 seconds, or destroying its HQ. The four normal skirmish maps retain their original cover/visibility rules and five-unit roster; they gain the same planning controls. The six-step tutorial and map choice remain intact.

## AI and scope

The strategic neural commander remains the frozen **40→32→10** policy. It can command the forward Rifle normally and does not have a scripted permanent garrison. The explicit route-cost controller still operates enemy bridge controls through the shared rules. It has not learned cover flanking, queued planning, bridge state or the new recharge objective. No training or CudaRL integration ran. A separate future training pass needs richer observations/actions before those tactics can be learned.

Current details: [rules and navigation](TACTICAL_RULES.md), [controls](TACTICAL_CONTROLS.md), [physical art and feedback](TACTICAL_ART.md), [product direction](PRODUCT_DIRECTION.md).

## Verification

| Check group | Passing checks |
| --- | ---: |
| New queue, formation, LOS, cover and recharge simulation | 56 |
| Bridge simulation regression | 57 |
| Ordinary mechanics / tactics / neural / expanded maps | 63 / 25 / 24 / 55 |
| New virtual-input controls | 49 |
| New HUD callbacks, real hit feedback and layout | 27 |
| New physical art and indicators | 33 |
| Bridge controller / bridge art / bridge HUD regressions | 20 / 27 / 25 |
| Existing art, controls, HUD, tutorial and groups | 227 |
| **Total** | **688** |

Evidence: [simulation](../TestResults/tactical-mission-simulation.json), [bridge](../TestResults/tactical-bridge-simulation.json), [controls](../TestResults/tactical-controls-runtime.txt), [HUD](../TestResults/tactical-hud-runtime.txt), [art](../TestResults/tactical-art-runtime.txt) and [runtime summary](../TestResults/maneuver-editor-checks.txt). The concrete firing test gives the same Rifle approximately **7.475 frontal damage versus 11.5 from the rear**, on both banks. Another test actually navigates from the South landing around the bunker before firing from the rear.

All 26 stress troops complete both queued legs legally over 70 simulated seconds. Ten thousand warmed route queries allocate zero managed bytes. These are standalone rules measurements, not native frame-rate claims. All 8/8 mission rollouts, 12/12 original matches and 24/24 expanded-map matches finish normally. The enemy uses its bridge controller in 3/8 mission rollouts. These small samples demonstrate liveness, not ideal balance. The trainer compiles but was never executed; the policy SHA256 remains `07ee559f4ae04a0023d0d451757a4bb2772e18c7aec1c52ca89eaefee3052181`.

Rendered captures cover **34 distinct state/size combinations**: 17 states at 1280×720, eight at 1024×768, seven at 2560×1440 and two at 3440×1440. The [capture record](../TestResults/maneuver-captures.txt) records empty text-overflow probes. Output dimensions for the added size matrix were explicitly requested and checked against capture metadata after correcting the tool's default 1280×720 output. Root and independent agents inspected the 1280 states; representative minimum/native-size controls, Help, shortcuts and combat outcomes were reviewed again. Some off-screen Pipeline frames retain rectangular missing-text artifacts; affected stills do not prove a native rendering defect or a clean native screen.

The visual fix pass raised the hidden wall cross, added selected-force combat outcome feedback and made spent command slots physically empty. The [baseline scan](../TestResults/maneuver-visual-baseline.txt) finds no missing/default materials, untinted non-button panels or default-white particles in its recorded scope. The planning pool is fixed at 105 renderers. Agent helpers live outside Assets, send no native input and do not ship.

The [steady-plan CPU probe](../TestResults/maneuver-preview-cost.json), with five warmups and 20 synchronous samples at 2560×1440, records median/p95 **0.539/0.560 ms for six troops** and **4.045/4.253 ms for 26 troops with 12 queued orders each**. The [uncached baseline](../TestResults/maneuver-preview-cost-before.json) was 19.642/20.156 ms for the full plan. These measurements cover the preview's Editor main-thread work, not GPU rendering, native FPS or the whole game. A changed plan/target/bridge must rebuild its affected cache; the steady result does not eliminate that cold cost. Managed-allocation calibration returned zero even for a 1024-byte allocation, so preview allocation zeros are explicitly untrusted. The standalone .NET route-query allocation result above is a separate measurement.

The initial long-running verification Editor accumulated virtual input devices because separate Pipeline script-run assemblies did not share the capture helper's static cleanup fields. That helper now persists only its restore metadata in Editor SessionState, reuses uniquely named devices and removes them across invocations. A fresh Editor reran the final controls/HUD/art gates; the CPU probe reports devices restored, and three separate capture preparations followed by Restore leave the device count **0→0**. The original run was retained in local logs and discarded for final cleanliness. The fresh final Editor reports zero current console errors/warnings or compile failure; its retained history has one expected first-Game-view warm-up error. No console was cleared. Both agent-owned Editors were restored and closed gracefully.

## Conservative review and remaining playtest questions

Interface scores are internal heuristics, not user measurements: usability 8/10, hierarchy 8, accessibility 7, consistency 8, responsiveness 8, feedback 8, performance 6 until native measurement. Priorities are keyboard/text scaling and contrast checks, stronger route strokes if players lose the lines on pale terrain, reducing HUD density, and measuring crowded native matches. Actual subjective input feel remains a human check.

Palette discipline: 4/5 — sage, walnut, brass and faction colors remain coherent; baseline material scan is clean.

Style uniformity: 4/5 — cast bank screens, sandbags and command caps fit the physical table.

Lighting depth: 3/5 — board relief remains readable; bunker roofs are too dark.

Silhouette: 4/5 — solid blockers and distinct unit classes remain recognizable.

UI intentionality: 3/5 — commands and state are clear; the HUD footprint and repeated objective compete with close views.

Feedback visibility: 4/5 — bridge forecasts, waiting markers and selected-force outcome text explain the new rules.

The bunker roofs remain visually heavy and partly hide the low Depot plinth from the closer composed camera; the flag, label and capture ring remain identifiable. The physical recharge gauge is small, with the HUD carrying the countdown. These are follow-up polish issues, not a claim of finished commercial graphics. No tablet port or store/payment system was added.

Skills used: Unity CLI/Pipeline, uGUI and Craft Game Floors (`playable-loop.md`, `game-feel.md`, `visual-scorecard.md`). Version-matched Unity 6000.6 references read: [LineRenderer.SetPositions](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/linerenderer/setpositions), [Camera.ScreenPointToRay](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/screenpointtoray) and [Application.runInBackground](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/runinbackground).

## Windows package and startup

The [Windows x64 build](../TestResults/maneuver-build-report.json) succeeded in **26.123 seconds**, reporting **108,023,892 build bytes**, zero errors and zero warnings. The **29,780,922-byte installer** upgraded the existing per-user installation from 0.6.0 to 0.7.0 with exit code zero. All **190 payload files** match the built files by SHA256. Desktop and Start menu shortcuts target the startup-fix launcher with the install directory as their working directory. [Installer evidence](../TestResults/maneuver-installer-verification.json).

Installer SHA256: `92b561c2b82dc80105669f358014e0820e8464062876e9c0702bfb64a0b9d19c`.

The desktop shortcut starts the installed player. Its log confirms completed scene initialization with Direct3D 12 on the RTX 5070, with zero script exceptions or shader errors; one running installed process was observed after three seconds. The [native startup record](../TestResults/maneuver-native-startup.json) is distinct from the Editor CPU measurements. Both desktop-control runtimes could not start because the chat's original workspace directory no longer exists (`os error 267`), so native screenshot/input verification and opening a paused mission were unavailable. No native inputs were sent. The original workspace was not recreated or altered to work around this tool failure.

Human testing should establish whether players deliberately switch routes to defeat a covered position, understand a contested ROTARY recharge, recover from mistakes, and voluntarily replay with a different plan.
