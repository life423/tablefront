# Bridge Gambit interface

Bridge Gambit has its own **Try mission** action on the title screen. The existing four-map skirmish selector and six-step tutorial remain separate. Entering or retrying the mission preserves the player's chosen skirmish map.

The mission presents two armies and the original three unit classes: Scouts, Rifles and Tanks. Its header shows each army's continuous, uncontested hold time on the opposing Depot, out of 45 seconds, alongside HQ health. The compact goal names uncontested holding; Help explicitly explains that an enemy capture attempt resets the timer. Ordinary score victory is not presented as the mission goal.

The mission-only control strip sits below the header. It combines the goal, current or pending crossing, countdown, remaining commands for both sides and one large **Open North/South crossing · R** action. A request gives a three-second warning, then the bridge moves while carrying its riders. The same button stays disabled during warning, movement, cooldown, pause, Help, spectator mode, defeat and command exhaustion. A move needs no troop selection. The shared docking grace is included in the reported cooldown.

Warnings identify **YOU** or **EMBER** in words as well as faction color. The strip explains that troops wait to board during a change and that riders travel with the bridge. Help explains the blocked water, the opposing-Depot objective and the R shortcut. Mission results offer retry and return-to-title actions, without the ordinary **Next battlefield** shortcut.

The bridge button keeps opaque muted text while disabled, including hover and keyboard-focus updates; its active gold state retains ink text. This mission-specific feedback setting preserves the actual command lock and leaves other buttons' established disabled styling unchanged.

The existing ink/ivory/brass palette, uGUI Canvas, TextMeshPro font, visible button focus and disabled states remain. The title card stays 740 reference units high; the mission strip is 104 high. These are Canvas reference dimensions, not native pixels. No new UI framework, packages, icon-only commands or tablet support are introduced.

`AgentScripts/VerifyBridgeHUD.cs` passed [24 runtime checks](../TestResults/bridge-hud-runtime.txt), exercising the actual title and crossing callbacks, resources, warning/moving/ready/exhausted states, both actors, pause, mission retry, Help, goal progress and return to ordinary skirmish. It uses only the agent-owned Editor session, stays outside Assets and sends no native input.

Rendered review covered seven mission states at 1280×720, plus opening, player warning, riders and enemy warning at 1024×768, 2560×1440 and 3440×1440. Title, Help, Pause and mission results were also captured at 1024×768 and 2560×1440. Runtime text-overflow probes were empty. The final uncontested-hold wording was reviewed again at 1024×768 and 2560×1440; the disabled bridge command remains readable. Both headquarters and starting armies fit the actual opening camera. Closer composed captures deliberately crop the board.

Some Pipeline captures showed missing text rectangles or doubled glyphs, so those frames cannot establish visual correctness. Fresh captures resolved the reviewed minimum-size states, 1280 stable state, 2560 warning and opening, and ultrawide opening; other affected stills remain a capture limitation. Native packaged review is recorded in the [release evidence](RELEASE_v0.6.0.md).

Conservative interface review (heuristics, not user measurements): usability 8/10, hierarchy 7, accessibility 7, consistency 8, responsiveness 8, feedback 8 and performance 6. Priorities are human testing of route names and the uncontested objective, text scaling/contrast, reducing the persistent HUD footprint, and measuring mission frame times with crowded crossings. Tablet interaction is not implemented. These checks do not establish comprehension, balanced difficulty or fun.

Skills followed: `ui` framework routing and `ui-ugui`, preserving the existing hierarchy, style and input architecture.
