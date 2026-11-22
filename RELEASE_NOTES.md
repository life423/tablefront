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

These are quality settings, not measured performance claims. Frame rate and VRAM use depend on the GPU, display mode, scene, and driver. See [Docs/VALIDATION.md](Docs/VALIDATION.md) for the checks completed for this release.

## Neural reinforcement learning

The shipped **40 → 32 → 10** policy was trained using PPO through **2,048 self-play episodes** and **726,762 sampled decisions**. Held-out, rotated-seat evaluation produced **95/120 wins against random opponents** and **99/120 against the untrained network**. The untrained-versus-random control won **36/120**.

This is a compact, learned high-level commander, not an expert RTS benchmark. Its weights stay frozen during matches. The trainer, optimizer checkpoint, training metrics, and evaluation are included for continuing learning locally.

## Install

Download the Windows ZIP from [Releases](https://github.com/life423/tablefront/releases/latest), choose **Extract All**, and launch `Tablefront.exe` from the extracted folder. Keep the executable with all its adjacent files and folders. No Unity installation is needed to play.

The source project uses **Unity 6000.6.10f1**. Optional training uses **.NET SDK 9.0.304**. See the [README](README.md) for controls and [Training/README.md](Training/README.md) for training instructions.
