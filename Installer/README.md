# Windows installer

The tactical preview includes **Tablefront-Setup-v0.3.0.exe**, a single Windows installer. It installs the complete x64 Unity player to `%LOCALAPPDATA%\Programs\Tablefront`, with desktop and Start menu shortcuts selected by default and an entry in Windows Installed apps. It needs no administrator access and never installs Unity or the .NET SDK.

Both shortcuts, and the optional **Play Tablefront** action on the finish page, launch **StartTablefront.exe**. Do not retarget them to `Tablefront.exe`: the launcher supplies the explicit Windows application identity required by the [startup fix](../Docs/STARTUP_FIX.md).

Uninstall removes installed files and installer-created shortcuts. It does not delete settings in Unity's LocalLow directory. Quit an installed running copy before reinstalling or upgrading; Setup does not force-close the game. A separately running portable copy can still trigger Unity's single-instance warning.

## Build

Use [Inno Setup 7](https://jrsoftware.org/inno-setup/downloads) or a compatible 6.7+ compiler. `BuildInstaller.ps1` accepts an explicit compiler path for portable installations. Build the Windows Unity player, then compile the launcher with `Tools/BuildLauncher.ps1` before packaging:

```powershell
.\Tools\BuildLauncher.ps1 -BuildDirectory 'C:\Builds\Tablefront'
.\Installer\BuildInstaller.ps1 -BuildDirectory 'C:\Builds\Tablefront' -OutputDirectory 'C:\Releases' -Version '0.3.0'
```

The compiler is a developer tool only. Players need only the resulting setup EXE. The script requires every runtime folder and excludes Unity's backup directory, build provenance and debug symbols. `AppId` in `Tablefront.iss` must stay stable for future upgrades. Increase the version and rebuild the installer when the game changes. The launcher startup fix is preserved in 0.3.0.

## Verification

The **0.2.1→0.3.0 upgrade** succeeded, all190 installed payload hashes matched the new build, registration displayed0.3.0 and both desktop/Start menu links targetedStartTablefront.exe. Launching the actual desktop shortcut produced a responding native window, completed scene initialization with no runtime exceptions, and the new title menu was visually inspected. No native gameplay input was sent. See [upgrade evidence](../TestResults/tactical-installer-verification.json) and [native startup](../TestResults/tactical-native-startup.json). The following preserves the original installer lifecycle verification; the user's installed game was not uninstalled during upgrade testing.

Verified on November 22, 2026 using Inno Setup 7.1.1 x64:

- Clean per-user install and same-version reinstall succeeded in a unique QA folder whose path contained spaces.
- All **190 shipped payload files** matched the original player build by SHA-256 after both installs.
- Unity backup files, provenance JSON and debug symbols were absent.
- Windows uninstall registration pointed to the QA install, with version 0.2.1.
- The installed Start menu shortcut targeted `StartTablefront.exe` with the install folder as its working directory.
- Silent installation skipped the finish-page game launch.
- Uninstall removed the QA payload, Start menu shortcut and uninstall registration.
- The existing desktop shortcut was unchanged throughout QA. The desktop task was disabled during isolated testing to preserve it; its target shares the same launcher declaration as the verified Start menu shortcut.

See [the machine-readable result](../TestResults/installer-verification.json) for installer hash and size. This verifies packaging and lifecycle, not a new gameplay test. The native wizard's visual layout and desktop-icon creation were not interactively verified. A Computer Use preview attempt did not expose a targetable wizard window; automated install/reinstall/uninstall completed successfully. The setup EXE is not code signed.

For isolated silent testing, assert there is no existing installation registration for the stable AppId before running Setup. A different `/DIR` alone does not isolate the uninstall registry. `/MERGETASKS="!desktopicon,!startmenuicon"` disables both shortcut tasks; `/NOICONS` alone does not disable the desktop task. Always pass `/NORESTART` and do not enable the finish-page launch during automated QA. Check the exact QA uninstall path before running its uninstaller.

Installer technology: [Inno Setup](https://jrsoftware.org/inno-setup).
