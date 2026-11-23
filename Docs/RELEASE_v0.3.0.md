# Tablefront 0.3.0 — tactical playtest

[**Download the Windows installer (.exe)**](https://github.com/life423/tablefront/releases/download/v0.3.0-preview.1/Tablefront-Setup-v0.3.0.exe)

Run Setup and choose Install, then open **Tablefront** from the desktop or Start menu. It upgrades an existing per-user installation and preserves the repaired launcher. Unity and ZIP extraction are not needed.

- **Split forces:** 4/5/6 recall Alpha/Bravo/Charlie. Ctrl+4/5/6 or Assign selected sets a group. Destinations stay visible. Recruitment remains 1/2/3.
- **Combat feel:** tank recoil, distinct weapons/impacts, brief fallen pieces, recruitment/capture cues and reduced motion.
- **Battle story:** contextual alerts (click or F to focus), a compact recap, safe draw handling and Rematch that restores player control after defeat.
- **Learn to command:** four optional tasks using real movement/capture/recruitment. Opposing commanders wait until Enter battle or Skip; army position and captured points remain.
- **Terrain and roles:** visible passable cover protects rifles, scouts add capture strength and tanks deal extra damage to tanks. Humans and AIs share the rules.

The wider opening keeps the player HQ and northern objective clear of the HUD. Graphics quality and neural weights are unchanged. No new training or CudaRL integration ran.

Verification: 57 existing mechanics checks, 25 tactical checks, 24 neural checks, 31 original Editor runtime checks and 39 new integration checks pass. All 36 frozen-policy battles finished normally, with no timeouts/invalid states. Rendered review covers four desktop resolutions/aspects. The Windows build has zero errors/warnings; installer upgrade checks all 190 payload hashes and both shortcuts.

**Playtest limits:** this does not establish human fun, ideal balance, a stronger policy or measured native FPS. All-neural seat wins were 8/8/20. Try the lesson and a normal battle, then report a satisfying or confusing moment.

[Full changes and evidence](https://github.com/life423/tablefront/blob/feature/tablefront-tactical-update/Docs/TACTICAL_UPDATE.md).
