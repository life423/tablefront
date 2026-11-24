using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Capture setup in the agent-owned Editor. Virtual pointer state never reaches Windows.
public static class CapturePlanningControls
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string State = "Tablefront.PlanningCapture.";
    const string MouseName = "TablefrontPlanningCaptureMouse";
    const string KeyboardName = "TablefrontPlanningCaptureKeyboard";

    public static async Task<string> Prepare(string shot)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Enter Play Mode first.");
        // Each Pipeline script-run compiles a new ephemeral assembly. SessionState and
        // uniquely named devices deliberately survive across Prepare/Restore invocations.
        if (!UnityEditor.SessionState.GetBool(State + "Active", false))
        {
            UnityEditor.SessionState.SetInt(State + "Mouse", Mouse.current != null ? Mouse.current.deviceId : -1);
            UnityEditor.SessionState.SetInt(State + "Keyboard", Keyboard.current != null ? Keyboard.current.deviceId : -1);
            UnityEditor.SessionState.SetInt(State + "Background", (int)InputSystem.settings.backgroundBehavior);
            UnityEditor.SessionState.SetInt(State + "Routing", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
            UnityEditor.SessionState.SetBool(State + "Active", true);
        }
        var mouse = InputSystem.devices.OfType<Mouse>().FirstOrDefault(d => d.name.StartsWith(MouseName, StringComparison.Ordinal))
            ?? InputSystem.AddDevice<Mouse>(MouseName);
        var keyboard = InputSystem.devices.OfType<Keyboard>().FirstOrDefault(d => d.name.StartsWith(KeyboardName, StringComparison.Ordinal))
            ?? InputSystem.AddDevice<Keyboard>(KeyboardName);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(0, 0) }); InputSystem.Update();
        game.enabled = false; game.World.enabled = true; game.HUD.gameObject.SetActive(true);
        game.StartBridgeMission(); game.Sim.UseExternalAI = true;
        foreach (var army in game.Sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
        foreach (var troop in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(troop, 99999f);
        game.SelectAll();
        if (shot == "title") game.ReturnToMenu();
        else if (shot == "help" || shot == "shortcuts")
        {
            game.ToggleHelp(); game.HUD.Refresh();
            if (shot == "shortcuts") typeof(TablefrontHUD).GetField("advancedShortcuts", Fields).SetValue(game.HUD, true);
        }
        else if (shot == "paused") game.TogglePause();
        else if (shot == "queued")
        {
            if (!game.IssueOrder(new Vector2(-7, -12))) throw new Exception("First real waypoint was rejected.");
            if (!game.IssueOrder(new Vector2(-7, 10), append: true)) throw new Exception("Second real waypoint was rejected.");
        }
        else if (shot == "waiting")
        {
            if (!game.RequestBridge(BridgeRoute.South)) throw new Exception("The real bridge request failed.");
        }
        else if (shot != "formation" && shot != "wall" && shot != "invalid")
            throw new ArgumentException("Use formation, queued, waiting, wall, invalid, title, help, shortcuts or paused.");
        typeof(TablefrontGame).GetMethod("UpdateCamera", Fields).Invoke(game, new object[] { 1f, true });
        await Task.Delay(160);
        if (!game.InMenu && !game.HelpOpen && !game.Paused)
        {
            Vector2 point = shot == "wall" ? game.Sim.Tactics.Obstacles[1].Center : shot == "invalid" ? new Vector2(0, 10) : new Vector2(17, -2);
            Vector2 screen = game.ViewCamera.WorldToScreenPoint(new Vector3(point.x, 0, point.y));
            InputSystem.QueueStateEvent(keyboard, shot == "queued" ? new KeyboardState(Key.LeftShift) : new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); InputSystem.Update();
        }
        game.HUD.Refresh(); game.World.Render(game.Sim, game.Selected); game.OrderPreview.Render(game); game.HUD.Refresh();
        game.World.enabled = false;
        await Task.Delay(120); Canvas.ForceUpdateCanvases();
        foreach (var label in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
        string overflow = string.Join(" | ", game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.text.Replace("\n", " / ")));
        return "shot=" + shot + "; size=" + Screen.width + "x" + Screen.height + "; preview=" + game.OrderPreview.PreviewUnitCount +
            "; valid=" + game.OrderPreview.PreviewValid + "; waiting=" + game.OrderPreview.PreviewWaiting + "; queued=" + game.QueuedCount + "; overflow=" + overflow;
    }

    public static string Restore()
    {
        foreach (var device in InputSystem.devices.Where(d => d.name.StartsWith(MouseName, StringComparison.Ordinal) || d.name.StartsWith(KeyboardName, StringComparison.Ordinal)).ToArray())
            InputSystem.RemoveDevice(device);
        if (UnityEditor.SessionState.GetBool(State + "Active", false))
        {
            var priorMouse = InputSystem.GetDeviceById(UnityEditor.SessionState.GetInt(State + "Mouse", -1)) as Mouse;
            var priorKeyboard = InputSystem.GetDeviceById(UnityEditor.SessionState.GetInt(State + "Keyboard", -1)) as Keyboard;
            if (priorMouse != null) priorMouse.MakeCurrent();
            if (priorKeyboard != null) priorKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)UnityEditor.SessionState.GetInt(State + "Background", 0);
            InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)UnityEditor.SessionState.GetInt(State + "Routing", 0);
            UnityEditor.SessionState.EraseBool(State + "Active");
            foreach (string key in new[] { "Mouse", "Keyboard", "Background", "Routing" }) UnityEditor.SessionState.EraseInt(State + key);
        }
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        game.enabled = game.World.enabled = true; game.ReturnToMenu();
        return "Virtual devices removed; original input settings restored; title returned.";
    }
}
