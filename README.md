# Tablefront

A local Windows tabletop RTS with neural commanders trained by reinforcement learning. Created in Unity 6000.6.10f1 / URP. You can command Cobalt against Ember and Violet, or watch three neural agents fight.

## Open and play

Double-click **Tablefront** on your desktop. Choose **Play vs AI**, or **Watch AI battle** for spectator mode.

The game executable is `C:\Users\aiand\Documents\programming\lumen-run\Builds\Tablefront\Tablefront.exe`. Keep the executable and its adjacent folders together. Unity itself is not needed to play the built game.

Your six starting units are already selected. **Right-click a beacon** to move them there. Units automatically fire at nearby enemies. Hold command posts to earn victory points and extra credits. Reach **350 points**, or eliminate both opposing headquarters.

| Input | Action |
| --- | --- |
| Left-click / drag a box | Select blue units |
| Shift + select | Add/remove units from selection |
| Right-click ground / enemy | Move in formation / focus fire |
| Ctrl+A | Select your whole army |
| 1 / 2 / 3 | Recruit Scout / Rifle / Tank at your HQ |
| WASD / arrows | Pan camera |
| Mouse wheel | Zoom |
| Q / E | Rotate the table view |
| Middle mouse drag | Pan with the mouse |
| Home | Focus your HQ |
| Tab | Hand your army to its neural commander / take command back |
| Space / Escape | Pause |
| F1 | Help |
| F11 | Toggle fullscreen |

Recruitment costs credits and has a short shared cooldown. Scouts cost 55, rifles 85, tanks 170. Each army can have 26 units. Capturing beacons does not require a special button: keep units inside their rings while opponents are absent. HQ loss eliminates an army; if yours falls first, you can watch the remaining battle or restart from the menu. Switching away from the window pauses a running match.

Help contains the controls, volume and reduced-motion settings. Volume/motion preferences persist; matches start fresh.

## The neural AI

The shipped policy was trained through **2,048 three-faction self-play episodes**, sampling **726,762 decisions**. It is a small **40 → 32 tanh → 10 softmax** neural network, optimized by **PPO** with a shared value baseline. All factions use the same trained weights and independent random streams. There is no scripted strategic fallback.

The policy chooses capture, assault, defense, recruitment and holding orders. Unit steering, aiming, damage, capture and economy are the game's shared deterministic mechanics. The policy is frozen while you play; continued training happens through the included trainer.

Held-out, rotated-seat evaluation: 95/120 wins against random-action opponents; 99/120 against the original untrained network. The untrained-versus-random control won 36/120. These establish an improvement over those baselines, not expert RTS skill or balanced human difficulty.

See [Training/README.md](Training/README.md) for the actual trainer, rewards, architecture, checkpoint and reproduction/resume commands. [Docs/VALIDATION.md](Docs/VALIDATION.md) records checks and limitations. The game and all training run offline; no accounts, purchases, server or model service are required.

## Project

Project folder: `C:\Users\aiand\Documents\programming\lumen-run\Tablefront`.

Scene: `Assets/Scenes/Tablefront.unity`. Runtime code: `Assets/Scripts`. Learned weights: `Assets/Resources/commander-policy.json`. Trainer: `Training`. Editor construction/verification tools are outside Assets in `AgentScripts` and do not ship as gameplay test modes.

Local Git branch `codex/tablefront` contains the template restore point and implementation. No remote repository was created or published.
