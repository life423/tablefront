# Tablefront v0.2.1 — Windows installer and reliable desktop launch

## Download and install

[**Download Tablefront for Windows (.exe)**](https://github.com/life423/tablefront/releases/download/v0.2.1/Tablefront-Setup-v0.2.1.exe)

Double-click **Tablefront-Setup-v0.2.1.exe**, choose **Install**, then open **Tablefront** from your desktop or Start menu. No ZIP extraction or Unity installation is needed. The installer includes the complete game, installs for your Windows account, and adds an entry under **Settings → Apps → Installed apps** for uninstalling it.

Installer verification: clean install and same-version reinstall to an isolated folder containing spaces; all **190 payload files** matched the original build by SHA-256; Start menu shortcut targeted the repaired launcher; uninstall removed the test payload, shortcut and registration. The existing desktop shortcut was preserved during testing. See [installer verification](https://github.com/life423/tablefront/blob/main/Installer/README.md#verification).

## Startup fix

- Fixed a Windows shortcut startup stall that left invisible game copies and caused the "another instance is already running" warning.
- Launch **StartTablefront.exe**, or create your desktop shortcut to that file. It starts the game with an explicit Windows application identity and reuses an existing game window.
- Retained native-resolution output, 1.5x supersampling, 8x MSAA, sharp overlay text, the trained neural commanders and all gameplay rules.
- Verified the repaired desktop shortcut opened a responding Tablefront window and reached scene initialization on the user's PC. No system services, registry settings or global Game Bar settings were changed.

The portable ZIP remains available as an alternative: extract it completely and launch **StartTablefront.exe**. Keep the launcher beside Tablefront.exe and the game folders. Unity and the .NET SDK are not needed to play.

See [startup verification](https://github.com/life423/tablefront/blob/main/Docs/STARTUP_FIX.md) for the diagnosis and launcher source.

---

# Tablefront — high-quality desktop build

A playable Windows tabletop RTS with a player-commanded army, two neural opponents, and a three-AI spectator mode.

## In this build

- Three factions compete for three beacons, credits, reinforcements, and a 350-point victory. Destroying both opposing headquarters also wins.
- Scouts, rifles, and tanks share the same economy and combat rules across human and AI armies.
- Click and box selection, formation movement, focus fire, camera pan/orbit/zoom, and optional neural control of your own army.
- Pause, restart, results, concise Help, saved volume, and reduced-motion settings.

## Graphics preset

The graphics update targets a 2560 × 1440 desktop display, including an RTX 5070 with 12 GB VRAM:

- Native 1440p fullscreen output with a **1.5× internal render scale**.
- **8× MSAA** and no FXAA softening.
- Native-resolution overlay HUD text and controls.
- Higher-resolution **512 / 1024** tabletop textures and **16× anisotropic filtering**.
- **8K main-light shadow atlas** with four cascades.

These are quality settings, not measured performance claims. Frame rate and VRAM use depend on the GPU, display mode, scene, and driver. See [Docs/VALIDATION.md](https://github.com/life423/tablefront/blob/main/Docs/VALIDATION.md) for the checks completed for this release.

## Neural reinforcement learning

The shipped **40 → 32 → 10** policy was trained using PPO through **2,048 self-play episodes** and **726,762 sampled decisions**. Held-out, rotated-seat evaluation produced **95/120 wins against random opponents** and **99/120 against the untrained network**. The untrained-versus-random control won **36/120**.

This is a compact, learned high-level commander, not an expert RTS benchmark. Its weights stay frozen during matches. The trainer, optimizer checkpoint, training metrics, and evaluation are included for continuing learning locally.

## Install

Use the **Tablefront-Setup-v0.2.1.exe** installer linked above. If you choose the optional portable ZIP, select **Extract All** and launch **StartTablefront.exe** from the extracted folder. Keep its adjacent files and folders together.

The source project uses **Unity 6000.6.10f1**. Optional training uses **.NET SDK 9.0.304**. See the [README](https://github.com/life423/tablefront/blob/main/README.md) for controls and [Training/README.md](https://github.com/life423/tablefront/blob/main/Training/README.md) for training instructions.
