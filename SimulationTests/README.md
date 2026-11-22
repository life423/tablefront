# Isolated simulation and learned-policy verification

Run `Run-Mechanics.ps1` and `Run-Neural.ps1` from PowerShell. Both compile the **actual runtime source** with the installed .NET 9 C# compiler and installed Unity 6000.6.10f1 `UnityEngine.CoreModule.dll`. No Vector2/Mathf shim or downloaded package is used. Paths in the runners target this computer's installed tools. These scripts run only isolated simulations; they never inject desktop input or launch the game.

`Run-Mechanics.ps1` passes 57 checks for recruitment costs, cooldowns, limits, resources, movement, bounds, capture/contest, combat, HQ targeting/elimination, strategic action mappings, observation features, normal victories, invalid input and clean reset. Its `NeuralCommanderStub` is used only because the mechanics tests supply their own actions. It makes no learned-policy claim. Recorded mechanics throughput is CPU simulation steps per second, **not rendered game FPS**.

`Run-Neural.ps1` links the actual `NeuralCommander.cs` plus CoreModule and JSONSerializeModule, loads the final native-trained 2,048-episode policy through `SetPolicy`, checks invalid weights/features, and compares the runtime inference with the trainer's math. Across 10,000 normalized observations, the maximum absolute probability difference was 2.68221e-7 and identical RNG seeds produced zero sampled-action differences. All 12 held-out seeds used the same trained policy for all three factions and reached a normal score/HQ victory, with captures, over 100 combat events, mobile armies and finite board bounds. No forced ranking was used in this verification.

The standalone probe bypasses Unity's `Resources.Load` native path by setting the deserialized trained policy directly. Resource import/loading and graphical interaction must be checked separately in the actual Unity Editor/player. The observed matches do not establish advanced RTS tactics or human balance.

Evidence: `mechanics-results.txt`, `neural-runtime-results.txt`, and per-match `neural-runtime-results.json`.
