# Tablefront

A Windows tabletop RTS made in Unity. Command a miniature army against two neural commanders, or watch all three fight through self-play tactics.

The **0.3.1 controls playtest** makes commanding mouse-first and adds a separate six-task tutorial. [Download the preview installer](https://github.com/life423/tablefront/releases/download/v0.3.1-preview.1/Tablefront-Setup-v0.3.1.exe) or read [controls and tutorial](Docs/CONTROLS_TUTORIAL.md). It also includes the [0.3.0 tactical improvements](Docs/TACTICAL_UPDATE.md). The stable release remains 0.2.1 while these changes receive human playtesting.

![Tablefront battlefield](Docs/Images/tablefront-battle.png)

## Play

1. [Download the 0.3.1 Windows playtest](https://github.com/life423/tablefront/releases/download/v0.3.1-preview.1/Tablefront-Setup-v0.3.1.exe).
2. Double-click **Tablefront-Setup-v0.3.1.exe** and choose **Install**.
3. Open **Tablefront** from the desktop icon or Start menu.
4. Choose **Tutorial · Start here** to learn by playing. After completing it, choose **Play a battle** for a fresh match. **Play vs AI** and **Watch AI battle** are also available on the title screen.

The installer includes all the game files and creates desktop and Start menu shortcuts by default. It installs for your Windows account without an administrator prompt. You can remove it through **Settings → Apps → Installed apps → Tablefront → Uninstall**.

Unity, the .NET SDK, an account, and an internet connection are not required to play. The small launcher uses the .NET Framework included with Windows. A portable ZIP is also available on the [release page](https://github.com/life423/tablefront/releases/latest): extract it completely and launch **StartTablefront.exe**, keeping its adjacent files and folders together. The installer build instructions are in [Installer/README.md](Installer/README.md).

**Click a blue unit** to select it, or use **Scouts**, **Rifles**, **Tanks** or **Select whole army** below the table. Then **click the ground to move** or **click an enemy to attack**. Keep troops inside a beacon's ring to capture it; units fire automatically when in range. Beacons earn credits and victory points. Reach **350 points**, or destroy both enemy headquarters.

Prefer the older stable build? [Download 0.2.1](https://github.com/life423/tablefront/releases/download/v0.2.1/Tablefront-Setup-v0.2.1.exe). Its controls differ: right-click gives orders, 1/2/3 recruit and Tab switches the commander. The controls below describe 0.3.1.

## Controls

| Input | Action |
| --- | --- |
| Left-click a blue unit / drag a box | Select your units |
| Left-click ground / enemy with troops selected | Move in formation / attack that enemy |
| Scouts / Rifles / Tanks buttons | Select that troop class |
| Recruit buttons | Buy reinforcements at your HQ |
| Shift + select | Add or remove units from the selection |
| Right-click ground / enemy | Alternative move / attack |
| Select whole army / Ctrl+A | Select your whole army |
| 1 / 2 / 3 | Select Scouts / Rifles / Tanks; does not spend credits |
| Z / X / C | Recruit Scout / Rifle / Tank at your HQ |
| WASD / arrows | Pan the camera |
| Mouse wheel | Zoom |
| Q / E | Rotate the view |
| Middle mouse drag | Pan with the mouse |
| Home | Focus your HQ |
| F | Focus the current HQ/beacon alert |
| Let AI play / Take control button | Explicitly hand over / reclaim command |
| Tab / Shift+Tab | Focus the next / previous visible UI control |
| Enter | Activate the focused button |
| Space / Escape | Pause; Space activates a focused UI control instead |
| Help / F1 | Open Help during a battle |
| F11 | Toggle fullscreen |

Scouts cost **55 credits**, rifles **85**, and tanks **170**. Recruitment shares a short cooldown; each army can field **26 units**. If your HQ falls, you can watch the remaining battle or restart. Switching away from the game pauses the match. Help includes volume and reduced-motion settings.

Optional custom groups remain on **4/5/6** to recall and **Ctrl+4/5/6** to assign. You can play without shortcuts; **Help → Keyboard shortcuts** lists the advanced controls.

## Tutorial

Choose **Tutorial** on the title screen, **Start tutorial** in Help, or **Tutorial** while paused. Gold markers and one instruction at a time guide you through selecting troops, moving, capturing WEST, recruiting rifles, destroying a practice target and protecting rifles in cover. Each task waits for the action to happen in the real simulation.

Practice has no opposing commander activity or score victory pressure. The attack target stays still and does not fire. **Show target** recenters the view; **Restart tutorial** starts the lesson over; **Exit tutorial** returns to the title. Finishing does not activate enemies on the practice table: **Play a battle** explicitly starts a fresh normal match. See the [tutorial guide](Docs/CONTROLS_TUTORIAL.md).

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
