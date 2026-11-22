using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class VerifyTablefront
{
    public static async Task<string> Run()
    {
        var g = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        var passed = new List<string>();
        Action<bool,string> check = (condition, name) => { if (!condition) throw new Exception("FAIL " + name); passed.Add("PASS " + name); };
        // Temporarily route virtual events to this agent-owned background test session.
        var originalBackground=InputSystem.settings.backgroundBehavior;
        var originalEditorRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront verification keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("Tablefront verification mouse");
        InputSystem.EnableDevice(mouse);InputSystem.EnableDevice(keyboard);
        try
        {
            g.ReturnToMenu(); await Task.Delay(150);
            check(g.InMenu && g.Sim.Elapsed == 0, "Title is idle, with no autoplay");
            check(NeuralCommander.Available && NeuralCommander.TrainingEpisodes > 0, "Trained neural policy loaded from Resources");
            check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
            check(g.HUD.GetComponentInChildren<Canvas>().renderMode == RenderMode.ScreenSpaceCamera, "Capture-compatible camera canvas");
            check(g.ViewCamera.GetUniversalAdditionalCameraData().renderPostProcessing, "URP post-processing enabled");
            var volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            check(volume != null && volume.profile.components.Count >= 4, "Four authored Volume overrides");
            check(!UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r => r.sharedMaterial == null || r.sharedMaterial.name == "Default-Material"), "No default or missing renderer materials");
            var play = g.HUD.GetComponentsInChildren<Button>(true).First(b => b.name == "Play vs AI");
            play.onClick.Invoke(); await Task.Delay(150);
            check(!g.InMenu && !g.Spectator && g.Selected.Count == 6 && g.Sim.UnitCount(0) == 6, "Play menu listener starts selected army");
            float elapsed = g.Sim.Elapsed; await Task.Delay(250);
            check(g.Sim.Elapsed > elapsed, "Live match advances frames and simulation time");
            g.TogglePause(); elapsed = g.Sim.Elapsed; await Task.Delay(200);
            check(g.Paused && g.Sim.Elapsed == elapsed, "Pause freezes simulation");
            g.TogglePause(); g.ToggleHelp(); elapsed = g.Sim.Elapsed; await Task.Delay(150);
            check(g.HelpOpen && g.Sim.Elapsed == elapsed, "Help freezes simulation"); g.ToggleHelp();
            int count = g.Sim.UnitCount(0); float credits = g.Sim.Armies[0].Credits;
            var scout = g.HUD.GetComponentsInChildren<Button>(true).First(b => b.GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("1  Scout"));
            scout.onClick.Invoke();
            check(g.Sim.UnitCount(0) == count + 1 && g.Sim.Armies[0].Credits < credits, "Recruit button spends credits and deploys scout");
            g.Recruit(UnitKind.Tank); check(g.Sim.UnitCount(0) == count + 1, "Recruit cooldown prevents duplicate spawn");
            g.SelectAll(); check(g.Selected.Count == count + 1, "Select all includes reinforcements");
            await Task.Delay(200);
            var unit = g.Sim.Units.First(u => u.Team == 0 && u.Alive);
            Vector2 screen = g.ViewCamera.WorldToScreenPoint(new Vector3(unit.Position.x,.6f,unit.Position.y));
            g.SelectAt(screen,screen,false,false); check(g.Selected.Count == 1, "Screen-space click selection");
            g.SelectAt(new Vector2(-10000,-10000),new Vector2(10000,10000),true,false);
            check(g.Selected.Count == count + 1, "Box selection selects friendly army");
            Vector2 orderScreen = g.ViewCamera.WorldToScreenPoint(new Vector3(0,0,-6));
            InputSystem.QueueStateEvent(mouse,new MouseState {position=orderScreen});InputSystem.Update();await Task.Delay(80);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=orderScreen}.WithButton(MouseButton.Right));
            await Task.Delay(100);
            check(g.Sim.Units.Where(u=>g.Selected.Contains(u.Id)).All(u=>u.HasOrder) && g.Notice=="Advance to position","Virtual right-click issues world formation order (UI="+g.HUD.PointerOverUI+",current="+(Mouse.current==mouse)+",held="+mouse.rightButton.isPressed+",enabled="+mouse.enabled+",screen="+orderScreen+",notice="+g.Notice+")");
            InputSystem.QueueStateEvent(mouse,new MouseState {position=orderScreen});InputSystem.Update();
            g.Sim.OrderMove(g.Selected,new Vector2(0,-6));
            Vector2 before = unit.Position;
            for(int i=0;i<20;i++) g.Sim.Step(.05f);
            check((unit.Position-before).magnitude > .5f, "Formation order moves troops toward objective");
            check(g.TryGround(new Vector2(Screen.width/2,Screen.height/2),out var ground) && Mathf.Abs(ground.x) <= 24 && Mathf.Abs(ground.y) <= 24, "Ground picking is bounded to board");
            bool control = g.Sim.Armies[0].IsAI; g.ToggleCommander(); check(g.Sim.Armies[0].IsAI != control, "Neural commander handoff toggles"); g.ToggleCommander();
            g.CycleSpeed(); check(g.Speed==2,"2x simulator speed"); g.CycleSpeed();
            // Virtual Input System state changes do not send input to the user's desktop.
            var focusField=typeof(TablefrontGame).GetField("wantedFocus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            Vector3 camBefore = (Vector3)focusField.GetValue(g);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); InputSystem.Update();
            g.SendMessage("Update");
            check(Vector3.Distance(camBefore,(Vector3)focusField.GetValue(g))>0,"Virtual W pans camera");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            await Task.Delay(200);
            g.TogglePause(); camBefore=(Vector3)focusField.GetValue(g);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); InputSystem.Update();g.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            check(Vector3.Distance(camBefore,(Vector3)focusField.GetValue(g))==0,"Paused camera ignores gameplay input"); g.TogglePause();
            g.Sim.Armies[0].Score = BattleSimulation.VictoryScore; g.Sim.Step(.05f); g.HUD.Refresh();
            check(g.Sim.Finished && g.Sim.Winner==0,"Victory reaches result state");
            g.StartBattle(true); check(g.Spectator && g.Sim.Armies.All(a=>a.IsAI) && g.Sim.Elapsed==0 && g.Sim.Units.Count==18,"Spectator restart resets all match state");
            g.TogglePause();
            for(int i=0;i<1200&&!g.Sim.Finished;i++) g.Sim.Step(.05f);
            check(g.Sim.Points.Any(p=>p.Owner>=0),"Learned commanders capture territory");
            check(g.Sim.Armies.Any(a=>a.Score>0),"Learned commanders accrue objective score");
            check(g.Sim.Units.All(u=>float.IsFinite(u.Position.x)&&float.IsFinite(u.Health)),"Neural match state stays finite");
            Directory.CreateDirectory("TestResults"); File.WriteAllLines("TestResults/runtime.txt",passed);
            return string.Join("\n",passed);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorRouting;g.ReturnToMenu();
        }
    }
}
