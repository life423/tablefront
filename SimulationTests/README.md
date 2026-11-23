# Isolated simulation and learned-policy verification

Run `Run-Mechanics.ps1` and `Run-Neural.ps1` from PowerShell. Both compile the **actual runtime source** with the installed .NET 9 C# compiler and installed Unity 6000.6.10f1 `UnityEngine.CoreModule.dll`. No Vector2/Mathf shim or downloaded package is used. Paths in the runners target this computer's installed tools. These scripts run only isolated simulations; they never inject desktop input or launch the game.

`Run-Mechanics.ps1` passes 57 checks for recruitment costs, cooldowns, limits, resources, movement, bounds, capture/contest, combat, HQ targeting/elimination, strategic action mappings, observation features, normal victories, invalid input and clean reset. Its `NeuralCommanderStub` is used only because the mechanics tests supply their own actions. It makes no learned-policy claim. Recorded mechanics throughput is CPU simulation steps per second, **not rendered game FPS**.

`Run-Neural.ps1` links the actual `NeuralCommander.cs` plus CoreModule and JSONSerializeModule, loads the final native-trained 2,048-episode policy through `SetPolicy`, checks invalid weights/features, and compares the runtime inference with the trainer's math. Across 10,000 normalized observations, the maximum absolute probability difference was 2.68221e-7 and identical RNG seeds produced zero sampled-action differences. All 12 held-out seeds used the same trained policy for all three factions and reached a normal score/HQ victory, with captures, over 100 combat events, mobile armies and finite board bounds. No forced ranking was used in this verification.

The standalone probe bypasses Unity's `Resources.Load` native path by setting the deserialized trained policy directly. Resource import/loading and graphical interaction must be checked separately in the actual Unity Editor/player. The observed matches do not establish advanced RTS tactics or human balance.

Evidence: `mechanics-results.txt`, `neural-runtime-results.txt`, and per-match `neural-runtime-results.json`.

## Tactical update verification (frozen policy)

`Run-Tactics.ps1` runs25 targeted mechanical checks using the actual simulation and Unity math. `Run-FrozenTactics.ps1` links the actual runtime inference and runs36 complete all-neural matches, with12 retained regression seeds and24 fresh seeds. Neither script links an optimizer or starts training. Earlier release evidence above remains unchanged; tactical results have separate names.

The new shared rules are:

- Six nonblocking cover zones flank the three beacons, each with radius2.2. Rifle squads in cover take65% of normal direct and splash damage. Scouts and tanks receive no cover reduction.
- Scouts contribute1.5 capture weight through the existing capped square-root capture formula. One scout therefore captures about22% faster than one rifle or tank, rather than50% faster. Enemy presence still contests capture regardless of weight.
- Shells from actual tank units deal20% extra damage to tanks. The HQ turret does not receive this bonus. Existing tank splash remains.
- These rules apply equally to human and neural factions. Costs, unit caps,40 observation inputs,10 actions and trained policy bytes are unchanged.

The frozen check reports **36/36 normal victories, zero timeouts, zero invalid states across142,358 fixed steps**, covering108 commander participations. It checks finite health/positions/economy and board/unit limits throughout every rollout. Wins by seat were8/8/20; this is not evidence of balanced faction win rates, cover-aware learned tactics or human fun. No forced ranking invents a winner when the600-second guard is reached. See `frozen-tactical-results.json` for exact seeds, policy SHA-256 and per-match outcomes.

`tactical-neural-runtime-results.json` and its text companion retain the original12-seed inference-suite rerun under these rules: maximum probability difference2.68221e-7, zero sampled-action disagreements across10,000 observations, and12 normal battles. `tactical-mechanics-results.txt` records all57 existing mechanics checks passing. `tactical-results.txt` records the25 focused checks.

Recap statistics count direct/splash weapon kills once each, include disbanded survivors among the defeated army's losses without awarding fictitious enemy kills, distinguish paid reinforcements from initial deployment, and accumulate uncontested beacon-seconds. The latest capture or HQ-destruction event is recorded with simulation time; it is an observed event, not proof of the sole cause of victory. All counters reset on a new simulation.
