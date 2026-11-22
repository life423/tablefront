# Tablefront validation — November 22,2026

## Delivered scope

An offline Windows tabletop RTS in a new sibling project alongside Breachline and Lumen Run. The user explicitly required neural networks and reinforcement learning; the first planned scripted commander was replaced before delivery. Runtime high-level strategy uses the trained neural policy, not the discarded scripted planner.

Three factions, scouts/rifle squads/tanks, paid reinforcements, unit selection and formation orders, capture economy, score/HQ victories, player or all-neural spectator modes, pause/help/settings, reset, camera pan/zoom/orbit, 1x/2x speed, and optional neural takeover of the player's army.

Unity 6000.6.10f1, URP 17.6, InputSystem 1.20.1, Cinemachine 3.1.7, uGUI 2.6/TMP, Pipeline 1.0.1. The existing Personal license and prior Editor-terms approval were reused. No Editor or plugin installation, sign-in, activation, cloud project, player account or publication was performed. Project creation installed its Pipeline/Cinemachine scaffolding and retained template diagnostics defaults; no application analytics code was added. Background-running was disabled for the finished player. No AGENTS.md/global agent instructions were written.

## Reinforcement learning evidence

Trainer compiles the exact runtime BattleSimulation source and references the installed Unity CoreModule for the same math. Final run: seed 20261122,2, 048 self-play episodes, 726,762 sampled decisions. PPO clipped surrogate, 40-input/32-tanh hidden/10-action softmax policy, shared linear value head, GAE, Adam and entropy regularization. Source, full optimizer checkpoint, progress CSV, initial policy and evaluation are included in Training/unity-trained.

Each of 120 held-out games rotates the trained agent's seat against two uniform-random or two untouched initial-network opponents. Final results 95/120 and 99/120 wins; initial-policy control 36/120 vs random. All trained-policy evaluation games reached normal game victory. The trainer supports finite-horizon ranking if a game times out; those trained held-out results did not require ranking. A separate 16-episode continuation proved checkpoint resume; it did not replace the 2048-episode shipped policy.

Runtime independently matched trainer probabilities on 10,000 normalized observations with maximum error 2.68221e-7 and zero seeded action mismatches. Twelve additional unseen all-neural battles completed normal victories with captures, combat and moving armies; this small sample had 2 Cobalt/3 Ember/7 Violet victories and does not establish balanced seats. Weight files reject malformed dimensions and nonfinite values; missing weights display an explicit unavailable status instead of silently using rules.

The model is trained on fully observed state and whole-army strategic commands. It does not learn raw pixels, individual motor control, pathfinding, fog-of-war, construction or tech trees. It does not learn during gameplay. Training used the CPU because the network is small; no GPU acceleration claim is made.

## Checks performed

- 57 isolated mechanics checks passed on actual Unity math: costs, cooldowns, caps, capture/contesting, economy, combat, death, HQ elimination, all 10 neural actions, 40-feature observation mapping, bounds, invalid values, deterministic resets and victory.
- 24 isolated neural runtime checks passed, including probability/action parity, invalid-weight rejection and 12 complete all-neural battles. Evidence and reproducible scripts are in SimulationTests.
- 28 Editor runtime checks passed, including real Resources policy load, menu listener, selected initial army, simulation liveness, pause/help freeze, recruitment spending/cooldown, click/box selection, virtual right-click formation commands, ground picking, neural takeover, speed, virtual W camera pan, paused input suppression, victory/reset and learned point capture. TestResults/runtime.txt contains the pass list.
- Virtual devices were local Input System devices; no Windows input was injected. Background Editor focus initially disabled the virtual mouse. The final test temporarily changes input routing and restores the original values in finally. An early settings-clone cleanup attempt failed because Unity destroys temporary settings on replacement; final verification uses saved/restored properties. No verification input settings are shipped.
- Runtime materials were scanned for missing/default materials. HDR, post camera flag, 4 Volume effects and camera Canvas were verified. Initial TMP/API compile errors and asynchronous font import were corrected. Final Editor console ground truth reported zero errors/warnings. Historical compilation entries were preserved rather than cleared.

## Rendered review

Reviewed actual URP captures at 1024×768,1280×720,1600×900 and 2560×1440 across title, player HUD, spectator HUD, unit closeup, help, pause and results. Screens are Windows desktop layouts; phone/tablet/touch are outside this demo's target. The fix pass enlarged objective/HQ names~1.57x, added dark slim nameplates, corrected TMP wrapping and the spectator textRectTransform, and verified no text collision in the 1024 HUD. Screenshots are staged representative states, not a claim of a physical human playthrough. Some staged notices remain visible because simulation time was advanced independently of UI wall-clock expiry; in live play they expire after 4 seconds.

Conservative internal craft heuristics, not measured product quality:

Palette discipline:4/5 — walnut, brass, sage, ink and faction enamel share a palette; default materials absent.

Style uniformity:3/5 — consistent handcrafted compound miniature art; it remains prototype geometry.

Lighting depth:3/5 — warm studio key, cool ambient, soft directional shadows and restrained URP grading; no baked GI.

Silhouette:3/5 — tanks, recon buggies and paired rifle miniatures are distinct; a closer zoom helps identify infantry.

UI intentionality:4/5 — coherent type, clear Play/Watch hierarchy, compact score/command strips and focus states; bottom HUD remains relatively large.

Feedback visibility:3/5 — selection rings, orders, health, capture ownership, score and intent readouts plus pooled traces/audio; sound balance needs human listening.

User design-review dimensions, 1–10 internal heuristics, with actionable next recommendations:

| Dimension | Score | Evidence / prioritized next improvement |
| --- | --- | --- |
| Usability |8| Familiar drag/right-click orders and initial army selection verified. Human-test first capture and recruitment comprehension. |
| Hierarchy |8| Battlefield dominates; menu has one primary Play action. Consider a more compact spectator command strip after human feedback. |
| Accessibility |7| Keyboard menus, focus, volume and reduced motion exist. Add key rebinding, UI size options and a full non-mouse command method before broad release. |
| Consistency |8| Shared palette, buttons and common faction rules. Replace manual help-space alignment with structured columns if help expands. |
| Responsiveness |8| Four desktop sizes inspected without observed overlap. Native resize/fullscreen transitions and touch remain unvalidated. |
| Feedback |8| Selection, recruitment errors, orders and outcome respond. Test combat readability and sound mix with the user. |
| Performance |7| Static scenery combined by material and 128 VFX pool bounded. Profile renderedCPU/GPU frame times during long full armies; simulation throughput is not FPS. |

## Build and handoff

`unity build --target StandaloneWindows64` succeeded with exit 0. Artifact: `C:\Users\aiand\Documents\programming\lumen-run\Builds\Tablefront\Tablefront.exe`, 667,136 bytes plus adjacent data and runtime files. The build log has no compiler or shader errors. A nonblocking LicensingClient signature-validation line appeared; entitlement resolution and the build still succeeded.

The built player started with `-batchmode -nographics`, completed scene initialization (`UnloadTime`), and logged no runtime exceptions. This is a headless startup check, not a native graphical mouse/keyboard playthrough. Rendered review and input tests were performed in the Editor as described above. The test process was closed afterward.

Desktop `Tablefront.lnk` and parent-folder `Play Tablefront.lnk` point directly to the existing executable, with its folder as working directory, empty arguments, and the authored custom icon. Their properties were read back. A physical double-click launch was not tested.

The packaged trainer compiled from `Assets/Scripts/BattleSimulation.cs` and completed a separate 16-episode smoke run from the finished project folder. That run wrote only TestResults; the shipped 2048-episode policy was unchanged. Its Resources asset and saved training policy share SHA256 `07EE559F4AE04A0023D0D451757A4BB2772E18C7AEC1C52CA89EAEFEE3052181`.

All agent-owned Editor/startup-test processes were closed. Play Mode, background-running overrides, auto-tick and temporary test input routing were restored. No agent-owned game is left autoplaying or taking control of the user's input. Full physical input/audio listening, native resize/fullscreen transitions, long rendered performance profiling and broader human balance tests remain follow-up work.

## References and workflow

Applied new-unity-project, craft-game-floors, unity-cli, unity-package-management, unity-pipeline, ui/ui-ugui and URP postprocessing guidance. Read creation/visual baseline, CLI build/integration/play verification, and craft art, lighting, camera, motion, loop, feel, audio, VFX, shell and scorecard references. URP and Input System APIs were checked against installed package sources. Checked Unity 6000.6 WorldToScreenPoint and Cinemachine 3.1 HardLock documentation. PPO source:[Schulman etal., 2017](https://arxiv.org/abs/1707.06347). The title/diorama uses authored procedural art; no downloaded art or proprietary assets were introduced. Audio uses generated clips with a saved master gain rather than a physical mixer asset. Cloud/live-service floors do not apply to this offline demo.


