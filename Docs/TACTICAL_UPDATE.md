# Tablefront 0.3.0 tactical playtest

November 22, 2026. The user approved implementing all five fun improvements. The previous release is preserved at `f30e1c4`; work is on `feature/tablefront-tactical-update`. AI training and CudaRL integration remain paused. This is a playable first pass, not a claim of measured human fun or commercial readiness.

## The five changes

1. **Separate forces.** Alpha/Bravo/Charlie initially contain scouts/rifles/tanks. Recall with **4/5/6** or labeled buttons. Assign selected troops with **Ctrl+4/5/6**, or **Assign selected…** and a Set button. Reinforcements join their class group until customized; dead members are removed. Selected destinations persist through a fixed eight-guide pool. Recruitment stays on **1/2/3**.
2. **Miniature combat feel.** Tank barrels recoil, weapon classes have different traces/reports, impacts produce restrained fragments, and sixteen pooled fallen castings expire after 1.8 seconds. Eight view-panned voices cap battlefield sound. Recruitment/capture cues are distinct. Reduced motion disables recoil/gait; no camera shake or global hit-stop was introduced.
3. **Readable battle outcomes.** Expiring beacon-loss and HQ-danger actions focus the relevant location when clicked or with **F**. HQ alerts take priority. Results show the victory condition, captures, losses/destruction, paid reinforcements, combined beacon hold time and latest capture/HQ event. That event is not claimed as the sole cause of victory. Rematch retains the original player/spectator choice after defeat. Simultaneous HQ elimination produces a safe draw.
4. **Playable lesson.** **Learn to command** teaches selection, a beacon order, capture and recruitment. Opposing strategic commanders wait while ordinary movement, combat, capture and economy continue. **Enter battle** or **Skip lesson** activates them without resetting the army or captured point. Ordinary battles have no permanent tutorial card.
5. **Tactical positions and roles.** Six visible, passable cover patches flank the beacons. Rifles in cover take **35% less direct/splash damage**. Scouts contribute **1.5 capture weight** through the existing capped square-root formula: a lone scout captures about **22% faster**, not 50%. Tanks deal **20% extra damage to tanks**; the HQ gun does not receive that bonus. Humans and AIs share these rules. Cover does not secretly block movement or alter pathfinding.

The opening camera keeps NORTH and the complete player HQ clear of the header/deck. **Home** provides a closer HQ view. Native resolution, 1.5× supersampling, 8× MSAA and the existing miniature palette remain.

## Verification

- **57** existing mechanics checks and **25** focused tactical checks pass against the actual simulation and installed Unity math.
- **24** neural checks pass, including 10,000 probability comparisons (maximum error 2.68221e-7) and zero seeded action mismatches.
- **36/36** all-neural battles finished normally: **zero timeouts/invalid states over 142,358 fixed steps**. Seat wins were **8/8/20**. This is liveness evidence, not balanced difficulty or learned cover tactics.
- **31** original Editor runtime checks and **39** new integration checks pass. They cover the complete real lesson, virtual keyboard groups/assignment, independent split-force orders, recruitment, bounded effect/order pools, reduced motion, HQ alerts/focus, pause, recap, draw and clean rematches after defeat.
- Rendered review covers title, battle, assignment, tutorial start/completion, alert, Help, draw/results and a staged close-up across **1024×768, 1280×720, 2560×1440 and 3440×1440**. Fixed-resolution GameViews were verified; URP captures included the overlay Canvas. The recorded nine-state matrix reported no overflowing TMP labels. HQ framing and alert contrast were corrected after first review.
- Windows build: **Succeeded, zero errors/warnings**. The 0.2.1→0.3.0 installer upgrade verified all **190 payload hashes**, per-user registration and both shortcut targets. It did not auto-launch during silent installation.
- Policy SHA-256 remains `07ee559f4ae04a0023d0d451757a4bb2772e18c7aec1c52ca89eaefee3052181`. No optimizer, retraining or CudaRL integration ran.

Evidence: [new runtime checks](../TestResults/tactical-runtime.txt), [original runtime rerun](../TestResults/tactical-legacy-runtime.txt), [simulation/frozen-policy results](../SimulationTests/README.md#tactical-update-verification-frozen-policy), [layout matrix](../TestResults/tactical-layout-matrix.json), [build report](../TestResults/tactical-build-report.json), [installer upgrade](../TestResults/tactical-installer-verification.json).

Virtual devices/routing, temporary GameView sizes, background-run overrides and auto-tick were restored. QA scripts are outside Assets and are not shipped. The first legacy runtime invocation used an empty Editor scene and was discarded; final checks ran the real Tablefront scene. A cover-count assertion initially observed Unity's deferred-destruction objects in the same frame; it was corrected to wait for normal cleanup. Failed attempts are not reported as passes.

Unity saved the existing `InputSystem_Actions` asset as a preloaded asset while serializing player settings; no input package or new action asset was added. The installed desktop shortcut was launched and its native title menu visually inspected after scene initialization; no keyboard or mouse input was sent to the native game. See [native startup evidence](../TestResults/tactical-native-startup.json).

## Craft review and remaining work

Conservative internal frame-review heuristics, not measured product quality:

Palette discipline: 4/5 — established enamel, sage, walnut and brass throughout.
Style uniformity: 4/5 — cover and fallen castings read as physical tabletop pieces.
Lighting depth: 4/5 — warm/cool separation and miniature shadows remain visible.
Silhouette: 4/5 — three classes remain distinct; zoom helps close tactics at the wide opening.
UI intentionality: 4/5 — clear groups, lesson completion and Rematch; secondary text is small at 1024×768.
Feedback visibility: 4/5 — orders, cover, alerts and actual recoil verified; physical audio listening remains a human check.

On the user's 1–10 dimensions: usability8, hierarchy8, accessibility7, consistency8, responsiveness8, feedback8, performance7. Sub-9 priorities: **first** test human comprehension/rematches and difficulty; **next** add UI/text scaling and key rebinding; then listen on speakers/headphones and measure native frame times/VRAM. These scores are cautious review judgments, not benchmarks.

Skills/references used: Unity CLI/Pipeline, uGUI, craft playable-loop, game-feel, VFX, game-audio, game-shell, character-motion and visual-scorecard. Preserved project choices include procedural miniatures, the existing Canvas and synthesized gain-controlled audio rather than introducing a mixer asset.

Next human test: five new players, two rounds each. Observe first selection/order/capture without coaching, deliberate split-force play in round two, understanding of the result and voluntary rematches. Ask for one satisfying and one confusing moment. Native FPS, audible mix, ideal balance and human fun are not established by these checks. The frozen policy has not learned the new mechanics; further AI work remains a separate decision after the training pause.
