using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Keep shortcut metadata out of Unity's process startup information. Windows Game Bar can
// otherwise deadlock while resolving a shortcut caller's AppUserModelID (cppwinrt issue 1133).
internal static class TablefrontLauncher
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size;
        internal string Reserved, Desktop, Title;
        internal int X, Y, Width, Height, XChars, YChars, FillAttribute;
        internal int Flags;
        internal short ShowWindow, ReservedBytes;
        internal IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        internal IntPtr Process, Thread;
        internal int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string app, StringBuilder commandLine,
        IntPtr processSecurity, IntPtr threadSecurity, bool inheritHandles, int flags,
        IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    [STAThread]
    private static int Main(string[] args)
    {
        string directory = AppDomain.CurrentDomain.BaseDirectory;
        string game = Path.Combine(directory, "Tablefront.exe");
        try
        {
            if (!File.Exists(game)) throw new FileNotFoundException("Keep StartTablefront.exe next to Tablefront.exe and the game folders.");
            foreach (Process running in Process.GetProcessesByName("Tablefront"))
            {
                using (running)
                {
                    try
                    {
                        if (!string.Equals(running.MainModule.FileName, game, StringComparison.OrdinalIgnoreCase)) continue;
                        if (running.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(running.MainWindowHandle, 9); // Restore this game's own window.
                            SetForegroundWindow(running.MainWindowHandle);
                        }
                        // A launch already in progress should not produce Unity's second-instance dialog.
                        return 0;
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            var startup = new StartupInfo();
            startup.Size = Marshal.SizeOf(typeof(StartupInfo));
            startup.Title = "LumenWorkshop.Tablefront";
            startup.Flags = 0x1000 | 0x0001; // STARTF_TITLEISAPPID | STARTF_USESHOWWINDOW.
            startup.ShowWindow = 1;
            ProcessInfo child;
            // The opt-in local benchmark uses the same startup path as the desktop icon.
            // Do not forward arbitrary shell or Unity arguments through a shortcut.
            string benchmark = Array.IndexOf(args, "-tablefront-benchmark") >= 0 ? " -tablefront-benchmark" : "";
            if (!CreateProcessW(game, new StringBuilder("\"" + game + "\"" + benchmark), IntPtr.Zero,
                IntPtr.Zero, false, 0, IntPtr.Zero, directory, ref startup, out child))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(child.Thread); CloseHandle(child.Process);
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("Tablefront could not open.\n\n" + error.Message,
                "Tablefront", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
