# Tablefront 0.3.1 — mouse-first controls and tutorial

[**Download the Windows playtest installer (.exe)**](https://github.com/life423/tablefront/releases/download/v0.3.1-preview.1/Tablefront-Setup-v0.3.1.exe)

Run Setup and choose Install, then open **Tablefront** from the desktop or Start menu. Choose **Tutorial · Start here** to learn by playing. The per-user installer retains the repaired desktop launcher; Unity and ZIP extraction are not needed.

- **Click to command:** click a blue troop to select it, then click ground to move or an enemy to attack. Drag selection and alternative right-click orders remain available.
- **Named force buttons:** Scouts, Rifles, Tanks and Select whole army. Recruitment has separate labeled buttons with costs.
- **Safer keys:** 1/2/3 select classes; Z/X/C recruit. Tab/Shift+Tab focus UI controls. AI handover uses the explicit Let AI play / Take control button.
- **Six-task tutorial:** select, move, capture WEST, recruit rifles, destroy a practice target and move rifles into cover. Gold targets, one instruction at a time and actual completion gates replace a wall of shortcuts.
- **Separate practice table:** no opposing commander activity or score victory pressure; the attack target stays still and does not fire. Restart or exit whenever needed. After completion, Play a battle starts a fresh normal match.
- **Simpler Help:** three mouse steps first, with advanced shortcuts behind a labeled button. The volume slider has a thin track and clear handle.

The [runtime evidence](https://github.com/life423/tablefront/blob/feature/tablefront-intuitive-controls/TestResults/controls-tutorial-runtime.txt) records **59 passing checks**, including real input selection/orders, each tutorial task, negative cases, normal victory, restart/exit, fresh-match completion, protection against early recruitment filling the army cap, and Tab without accidental AI control. The 57 mechanics and 25 tactical checks also pass. The four-size, nine-state layout matrix records 36 captures with no reported TMP overflow; representative frames received visual review. The Windows build succeeded with zero errors and warnings. Automated checks do not establish that new players find the game intuitive; human playtesting remains necessary.

Graphics quality, neural weights and the normal battle rules are preserved. AI training and CudaRL integration remain paused. The opponents do not learn during gameplay.

The 0.3.0 to 0.3.1 per-user upgrade passed with all 190 installed payload hashes matching. Both desktop and Start menu shortcuts retain the startup-fix launcher. The actual desktop shortcut opened the installed game successfully; scene initialization completed without runtime exceptions or shader errors, and a read-only native screenshot showed the tutorial's first task. The installer is 29,610,466 bytes; its SHA-256 is provided alongside the download.

[Controls and tutorial guide](https://github.com/life423/tablefront/blob/feature/tablefront-intuitive-controls/Docs/CONTROLS_TUTORIAL.md).
