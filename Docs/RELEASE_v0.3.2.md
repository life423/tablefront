# Tablefront 0.3.2 — StarCraft-style control groups

[**Download the Windows playtest installer (.exe)**](https://github.com/life423/tablefront/releases/download/v0.3.2-preview.1/Tablefront-Setup-v0.3.2.exe)

This update adopts the control-group shortcuts requested after the 0.3.1 playtest:

- **Ctrl+0–9:** save or replace a group with your selected units.
- **Shift+0–9:** add selected units to that group without replacing its members.
- **0–9:** select the saved group.
- **Double-tap a number:** select that group and center the camera on it, keeping your zoom and the camera's normal board-edge limits.

For example, drag-select infantry and press **Ctrl+1**. Press **1** to recall that squad; select reinforcements and press **Shift+1** to add them.

Groups start empty each match, can share members, and remove casualties. New recruits are added explicitly rather than silently changing your saved squads.

The named **Scouts / Rifles / Tanks** buttons still select a whole troop class. Number keys are now reserved for groups, replacing the 0.3.1 class-selection and three-group mappings. Mouse selection/orders, **Z/X/C** recruitment, explicit AI handover, UI keyboard focus and the six-task practice tutorial are retained. Help's optional shortcut list describes the new controls.

The update also fixes two reproduced ways the attack lesson could appear stuck. Practice troops could destroy the opposing HQ, after which a lost practice target could not be recruited again. Separately, **Show target** could frame a distant friendly force so the enemy sat underneath the tutorial coach. Practice HQs are now protected, automatic HQ attacks and HQ turrets are inactive, and target creation can recover a destroyed target HQ. An uncommanded target that disappears is replaced and the view recenters. **Show target** centers the actual practice enemy in the visible play area even when friendly troops are far away. Ordinary battles keep their HQ damage and combat rules.

Run Setup and choose Install, then reopen **Tablefront** from the desktop or Start menu and restart the tutorial to load the fix. The installer retains the startup-fix launcher. Unity and ZIP extraction are not needed to play. The 0.3.1 to 0.3.2 upgrade passed with all 190 installed payload hashes matching and both shortcuts targeting the correct launcher. Its size is 29,613,004 bytes, with a SHA-256 checksum alongside the download.

Graphics, normal-battle combat rules and neural weights are unchanged. AI training and CudaRL integration remain paused; opponents do not learn during a match.

Fresh verification records **208 passing automated checks**: 50 control-group, 59 updated tutorial/control, 17 tutorial-recovery, 57 mechanics and 25 tactical checks. Recovery checks include ten repeated target losses, a destroyed target HQ, real automatic fire, distant-friendly camera framing, one minute near an opposing practice HQ and preserved HQ damage in ordinary battles.

The four-size layout matrix records **16 captures** with no reported TMP overflow. Representative review covered the shortcut list and narrow/ultrawide attack views; the target stayed visible outside the coach. The Windows player build succeeded in **9.915 seconds** with **zero errors and warnings**, reporting **107,330,060 bytes**. Installation and native launch are reported separately when available. These checks do not establish new-player comprehension, full accessibility or measured native performance.

[Controls and tutorial guide](https://github.com/life423/tablefront/blob/feature/tablefront-starcraft-groups/Docs/CONTROLS_TUTORIAL.md). Earlier 0.3.1 evidence is labeled historical.
