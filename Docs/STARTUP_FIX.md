# Desktop launch fix — v0.2.1

The desktop shortcut's target existed, but Windows shortcut launches could stall before Unity created its game window. Five old launches remained windowless. Their stale copies then caused the single-instance warning when the user clicked again.

Changing the Windows controller backend to XInput, testing Direct3D 11 and testing exclusive fullscreen did not fix the stall. A read-only native stack trace identified Unity waiting during `Windows.Gaming.UI.GameBar.dll` initialization, through `appresolver.dll`, Windows shell storage and a COM/RPC call. This matched [Microsoft's documented shortcut-context AppUserModelID deadlock](https://github.com/microsoft/cppwinrt/issues/1133).

`Tools/TablefrontLauncher.cs` starts the game with an explicit application identity supplied at process creation. `STARTUPINFO.Title` is `LumenWorkshop.Tablefront`; flags are `STARTF_TITLEISAPPID | STARTF_USESHOWWINDOW`. This avoids inheriting the problematic shortcut-context lookup. The launcher also reuses the game's existing window instead of spawning another copy. [Microsoft STARTUPINFO reference](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfow).

Build the launcher after a Windows Unity build:

```powershell
& .\Tools\BuildLauncher.ps1 -BuildDirectory '<folder containing Tablefront.exe>'
```

Run or create desktop shortcuts to **StartTablefront.exe**. Keep it beside Tablefront.exe and the adjacent game folders. It uses the .NET Framework included with current Windows; the .NET SDK is not needed to play.

Verification on the user's PC: invoking the actual repaired Desktop/Tablefront.lnk through Windows ShellExecute started the launcher and produced a responding Tablefront game window (PID24472, native window handle18875722). The player log progressed beyond `Using XInput` to scene initialization (`UnloadTime`). Previous direct shortcut trials remained windowless. This verifies the desktop shortcut launch path; no physical mouse double-click was claimed. The user pressed Escape during the follow-up screenshot attempt, so computer control was stopped and the running game was left alone.

The game retains its v0.2.0 graphics settings and trained policy. The patch uses the supported XInput backend but requires no special graphics launch flags, Windows service changes, registry changes, or system-wide Game Bar changes. The native stack diagnostic helper was kept outside the shipped project. The launcher source and build script are included; no process suspension or diagnostics code ships in the launcher.
