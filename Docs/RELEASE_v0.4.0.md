# Tablefront 0.4.0 — painted-tabletop visual playtest

[**Download the Windows playtest installer (.exe)**](https://github.com/life423/tablefront/releases/download/v0.4.0-preview.1/Tablefront-Setup-v0.4.0.exe)

This preview develops the painted miniature-tabletop direction in the existing URP renderer:

- Custom shared miniature geometry adds sloped/chamfered armor, track and wheel detail, and articulated infantry. Motion includes walking limbs, turning recon wheels and cannon recoil, with reduced-motion support.
- A continuous textured campaign board blends green/dry flock, worn earth routes and wheel ruts. Broadleaf groves, slate and clustered grass replace the earlier flat-patch/conical-tree look.
- Generated normal and metallic/smoothness maps distinguish enamel, rubber, walnut, brass and stone.
- More detailed HQs, pennants and sandbag cover retain the actual ownership and protection feedback.
- A captured studio reflection environment adds broad highlights while hiding the softbox cards from the playing camera.

Run Setup and choose Install, then reopen **Tablefront** from the desktop or Start menu. The installer uses the existing startup-fix launcher. Unity and ZIP extraction are not required to play.

Mouse commands, named class buttons, all ten StarCraft-style groups and the six-task tutorial remain. The missing-enemy recovery fix is retained. Gameplay rules and neural weights are unchanged; AI training and CudaRL integration remain paused.

Current evidence records **233 passing automated checks**: 25 visual-resource/behavior, 59 tutorial/control, 50 control-group, 17 tutorial-target recovery, 57 mechanics and 25 tactical checks. The art check observed 14 material-combined static renderers and 186 shared owned meshes with stable counts across three restarts. A four-size matrix records 12 title/battle/tutorial captures, supplemented by six detail views and an actual normal-policy combat still. The graphics configuration enables real-time reflection probes and Resources references retain normal/mask surface variants.

The Windows build completed with zero errors and warnings. Upgrade from 0.3.2 passed with all 190 installed payload hashes matching, and both shortcuts still use the correct launcher. The installed player opened from the actual desktop shortcut and its new graphics were visually inspected at 2560×1440 during the tutorial; scene initialization completed without runtime exceptions or shader errors. The installer is 29,751,527 bytes, with its SHA-256 checksum beside the download. Native frame rate has not been measured.

This is procedural stylized art, without external asset purchases, added packages, ray tracing or baked GI. It is a visual playtest, not a claim of AAA artwork, measured frame rate, complete accessibility or settled human readability.

[Visual update and evidence](https://github.com/life423/tablefront/blob/feature/tablefront-premium-tabletop/Docs/PREMIUM_TABLETOP.md) · [Controls and tutorial](https://github.com/life423/tablefront/blob/feature/tablefront-premium-tabletop/Docs/CONTROLS_TUTORIAL.md)
