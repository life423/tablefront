# Tablefront reinforcement-learning commanders

These opponents use a neural policy trained by **Proximal Policy Optimization (PPO)** through multi-agent self-play. Strategic actions come from the policy. There is no scripted strategic fallback, imitation dataset, external language model, or network service. Per-unit movement, targeting, firing, economy, capture, and damage remain ordinary game rules shared by the human and neural commanders.

The standalone C# trainer compiles the **same `Runtime/BattleSimulation.cs`** used by the game, and references the installed Unity `UnityEngine.CoreModule.dll` for actual `Vector2`/`Mathf` operations. It never rewrites the rules in another environment. `ExternalPolicyStub.cs` only satisfies the runtime policy-loading dependency; the trainer sets `UseExternalAI=true` and explicitly supplies every strategic action. It cannot fall back to the stub for decisions.

## Model and learning

- Input: 40 normalized observations: own economy/army/HQ/score, both opponents, three beacon ownership/capture/proximity features, nearby threats and army centroid. Exact feature order is documented by `GetObservation` in the simulation.
- Policy: fully connected **40 → 32 tanh → 10 softmax**; output-major weight arrays. A separate linear value head uses the same hidden features during training and is excluded from the shipped inference JSON.
- Actions: capture West/East/North; attack either enemy HQ; defend own HQ; recruit Scout/Rifle/Tank; hold current orders. These actions use the same cost, cooldown, movement and combat rules as the player. Unit targeting and motion are deterministic low-level game mechanics, not learned neural motor control.
- Rollout: all three seats sample the same current policy against one another. The simulation advances 1.65 seconds per decision through its existing 0.05-second substeps. No rendering, time acceleration cheats, extra income or hidden handicaps.
- Optimization: clipped PPO surrogate (epsilon 0.2), GAE lambda 0.95, gamma 0.995, entropy coefficient 0.015, shared value-loss coefficient 0.5. Four shuffled epochs, minibatches of 128, Adam, gradient norm capped at 0.5, learning rate decaying from 0.0007 to 0.00028.
- Reward: own score gain, enemy score pressure, changes in beacon ownership/capture and proximity potential, HQ damage, and terminal win/loss. Episodes stop on the normal game outcome or after 180 decisions (297 simulated seconds), where score ranking determines the finite-horizon training outcome. Evaluation permits 240 decisions (396 seconds) before any such ranking.
- Runtime sampling: seeded softmax sampling, matching the optimized stochastic policy. The network can interleave orders and recruitment without a hard-coded schedule. The model is frozen while playing; it learns when the trainer is run again.

Algorithm reference: Schulman et al., [Proximal Policy Optimization Algorithms](https://arxiv.org/abs/1707.06347). This is a small local implementation, not an official Unity ML-Agents package or a benchmark claim.

## Reproduce or continue training

Prerequisites already on this computer: .NET SDK 9.0.304 and Unity 6000.6.10f1. No NuGet packages, Python environment, cloud services or GPU drivers are required by this small CPU trainer. The direct SDK compiler script avoids restoring packages or changing the user's global .NET/NuGet configuration.

From the project directory in PowerShell:

```powershell
& .\Training\BuildTrainer.ps1
dotnet .\Training\bin\Tablefront.Training.dll --episodes 2048 --seed 20261122 --batch 16 --horizon 180 --eval-games 120 --output .\Training\my-run
```

The source layout may also be staged under `TablefrontSource`; the same scripts work there. The runtime simulation source is resolved relative to the Training folder.

Continue from the shipped checkpoint, preserving network weights, Adam moments and optimizer update count:

```powershell
dotnet .\Training\bin\Tablefront.Training.dll --resume .\Training\unity-trained\checkpoint.json --episodes 2048 --seed 20263170 --output .\Training\continued
```

`--episodes` is the number of additional episodes. `--seed` initializes this run's rollout/action RNG. `checkpoint.json` preserves learned weights and optimizer state; it does not preserve the random generator's internal state or current episode. Continuing is reproducible from the same checkpoint, seed and arguments, but is not a bitwise continuation of an interrupted rollout.

The output directory contains:

- `commander-policy.json`: the inference-only policy, copied to `Assets/Resources/commander-policy.json` before rebuilding the game.
- `checkpoint.json`: full actor/value weights and Adam state for continuing training.
- `initial-policy.json`: untouched random-initialized policy for the evaluation control.
- `training.csv`: per-batch progress, losses, entropy, sampled decisions and timing.
- `evaluation.json`: held-out, rotated-seat match results against uniform random actions and the untouched initial neural policy, plus the untrained-versus-random control.

The runtime reads `Resources/commander-policy` once when first needed. To use new weights, replace that asset in the Unity project and rebuild; restart an Editor play session to reload it. A training run does not silently alter an already-running game.

## Scope and limitations

This is a compact prototype policy with a high-level, fully observed command interface. It does not learn raw pixels, neural pathfinding, individual unit tactics, fog of war, construction, tech trees or human preferences. There is no live learning during a match. All factions use the same trained weights with independent random streams. Results against random and untrained controls are evidence of local learning, not proof of expert RTS ability. See the included final evaluation for actual measured numbers and sample sizes.
