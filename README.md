# Tablefront

A Windows tabletop RTS made in Unity. Command a miniature army against two neural commanders, or watch all three fight through self-play tactics.

The **0.3.0 tactical playtest** adds squads, combat feedback, recaps, a playable lesson and cover/role mechanics. [Download the preview installer](https://github.com/life423/tablefront/releases/download/v0.3.0-preview.1/Tablefront-Setup-v0.3.0.exe) or read [changes and verification](Docs/TACTICAL_UPDATE.md). The stable download below remains 0.2.1 while the new tactics receive human playtesting.

![Tablefront battlefield](Docs/Images/tablefront-battle.png)

## Play

1. [Download Tablefront for Windows](https://github.com/life423/tablefront/releases/download/v0.2.1/Tablefront-Setup-v0.2.1.exe).
2. Double-click **Tablefront-Setup-v0.2.1.exe** and choose **Install**.
3. Open **Tablefront** from the desktop icon or Start menu.
4. Choose **Play vs AI** to command Cobalt, or **Watch AI battle** to spectate.

The installer includes all the game files and creates desktop and Start menu shortcuts by default. It installs for your Windows account without an administrator prompt. You can remove it through **Settings → Apps → Installed apps → Tablefront → Uninstall**.

Unity, the .NET SDK, an account, and an internet connection are not required to play. The small launcher uses the .NET Framework included with Windows. A portable ZIP is also available on the [release page](https://github.com/life423/tablefront/releases/latest): extract it completely and launch **StartTablefront.exe**, keeping its adjacent files and folders together. The installer build instructions are in [Installer/README.md](Installer/README.md).

Your six starting units are selected. **Right-click a beacon** to advance. Keep units inside its ring to capture it; units fire at nearby enemies automatically. Beacons earn credits and victory points. Reach **350 points**, or destroy both enemy headquarters.

## Controls

| Input | Action |
| --- | --- |
| Left-click / drag a box | Select your units |
| Shift + select | Add or remove units from the selection |
| Right-click ground / enemy | Move in formation / focus fire |
| Ctrl+A | Select your whole army |
| 4 / 5 / 6 | Recall Alpha / Bravo / Charlie (0.3.0 preview) |
| Ctrl+4 / Ctrl+5 / Ctrl+6 | Assign selected troops to a group (preview) |
| 1 / 2 / 3 | Recruit Scout / Rifle / Tank at your HQ |
| WASD / arrows | Pan the camera |
| Mouse wheel | Zoom |
| Q / E | Rotate the view |
| Middle mouse drag | Pan with the mouse |
| Home | Focus your HQ |
| F | Focus the current HQ/beacon alert (preview) |
| Tab | Hand your army to its neural commander / take command back |
| Space / Escape | Pause |
| F1 | Open Help |
| F11 | Toggle fullscreen |

Scouts cost **55 credits**, rifles **85**, and tanks **170**. Recruitment shares a short cooldown; each army can field **26 units**. If your HQ falls, you can watch the remaining battle or restart. Switching away from the game pauses the match. Help includes volume and reduced-motion settings.

## Neural commanders

The opponents use a **40 → 32 tanh → 10 softmax** neural policy trained by **Proximal Policy Optimization (PPO)**. It learned through **2,048 three-faction self-play episodes**, sampling **726,762 decisions**. All factions share the trained weights with independent random streams.

The network chooses capture, assault, defense, recruitment, and holding orders. Movement, targeting, firing, damage, capture, and economy follow the same game rules for players and AI. There is no scripted strategic fallback. The policy is **frozen during gameplay**; it learns further when the included trainer runs again.

Held-out evaluation rotated the trained agent through all three starting positions:

| Matchup | Trained agent wins |
| --- | --- |
| Against two random-action opponents | 95 / 120 |
| Against two untouched, untrained neural opponents | 99 / 120 |
| Untrained-versus-random control | 36 / 120 |

These results show improvement over those baselines, not expert RTS play or proven balance against people. The included [training guide](Training/README.md), [evaluation](Training/unity-trained/evaluation.json), and [validation notes](Docs/VALIDATION.md) describe the method and limits.

## Graphics

The high-quality desktop preset targets a **2560 × 1440 display**, with supersampling, MSAA, detailed tabletop textures, anisotropic filtering, and sharp overlay UI. It is intended for capable desktop GPUs; no measured frame-rate guarantee is provided. See the [release notes](RELEASE_NOTES.md) for the graphics configuration included in the release.

## Edit or train

Open the project in **Unity 6000.6.10f1** using Unity Hub. The project uses **URP 17.6**, the Input System, Cinemachine, and TextMeshPro/uGUI.

| Location | Contents |
| --- | --- |
| `Assets/Scenes/Tablefront.unity` | Entry scene |
| `Assets/Scripts` | Runtime game and simulation |
| `Assets/Resources/commander-policy.json` | Shipped neural policy |
| `Training` | PPO trainer, checkpoint, metrics, and instructions |
| `AgentScripts` | Editor construction and verification tools |
| `Docs` | Art direction and verification records |

Training is optional. The small CPU trainer uses **.NET SDK 9.0.304** and the installed Unity Editor's math assembly. It compiles the exact `Assets/Scripts/BattleSimulation.cs` used by the game. The Windows build script currently assumes standard tool installation paths; update those paths if your installations differ. See [Training/README.md](Training/README.md) to reproduce or continue training, then replace the Resources policy and rebuild to play with new weights.
