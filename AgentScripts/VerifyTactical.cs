using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Editor-only integration checks. Virtual devices never send input to the desktop.
public static class VerifyTactical
{
    public static async Task<string> Run()
    {
        var g = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!g) throw new Exception("Open Assets/Scenes/Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        Action<bool,string> check = (ok, name) => { if (!ok) throw new Exception("FAIL " + name); passed.Add("PASS " + name); };
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        bool reduced = g.ReducedMotion;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront tactical QA keyboard");
        Button Button(string name) => g.HUD.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        void KeyPress(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); g.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        try
        {
            g.ReturnToMenu();
            Button("Learn to command").onClick.Invoke();
            check(g.TutorialActive && g.TutorialStep == 0 && g.Selected.Count == 0, "Lesson starts at selection with no precompleted step");
            check(g.Sim.Armies.All(a => !a.IsAI), "Opposing commanders wait throughout the lesson");
            g.SelectAll(); g.SendMessage("Update");
            check(g.TutorialStep == 1, "Actual selection advances lesson");
            check(!g.IssueOrder(new Vector2(float.NaN, 0)) && !g.IssueOrder(Vector2.zero, 99999), "Invalid orders are rejected without success feedback");
            check(g.IssueOrder(g.Sim.Points[0].Position), "Lesson issues a real formation move");
            g.SendMessage("Update");
            check(g.TutorialStep == 2 && g.Sim.Units.Where(u => g.Selected.Contains(u.Id)).All(u => u.HasOrder), "Moving to a beacon advances lesson and orders the simulation");
            for (int i = 0; i < 430 && g.Sim.OwnedPoints(0) == 0; i++) g.Sim.Step(.05f);
            g.SendMessage("Update");
            check(g.Sim.OwnedPoints(0) > 0 && g.TutorialStep == 3, "Lesson capture uses live movement and capture rules");
            int count = g.Sim.UnitCount(0); g.Recruit(UnitKind.Rifle); g.SendMessage("Update");
            check(g.TutorialStep == 4 && g.Sim.UnitCount(0) == count + 1, "Paid reinforcement completes the fourth lesson task");
            Button("Enter battle").onClick.Invoke();
            check(!g.TutorialActive && g.Sim.Armies[1].IsAI && g.Sim.Armies[2].IsAI && g.Sim.OwnedPoints(0) > 0, "Enter battle enables opponents without resetting the learned interaction");
            g.StartTutorial(); g.SkipTutorial();
            check(!g.TutorialActive && g.Sim.Armies[1].IsAI && g.Sim.Armies[2].IsAI, "Skip lesson returns to live opposing commanders");

            g.StartBattle(false);
            await Task.Delay(100); // Unity releases prior match objects at the end of the frame.
            check(g.GroupCount(0) == 2 && g.GroupCount(1) == 3 && g.GroupCount(2) == 1, "Initial Alpha/Bravo/Charlie groups separate scouts/rifles/tank");
            KeyPress(Key.Digit4);
            check(g.Selected.Count == 2 && g.Sim.Units.Where(u => g.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Scout), "Virtual 4 recalls scout force");
            g.IssueOrder(g.Sim.Points[1].Position);
            KeyPress(Key.Digit5); g.IssueOrder(g.Sim.Points[0].Position);
            check(g.Sim.Units.Where(u=>u.Team==0 && u.Kind==UnitKind.Scout).All(u=>Vector2.Distance(u.Destination,g.Sim.Points[1].Position)<4)
                && g.Sim.Units.Where(u=>u.Team==0 && u.Kind==UnitKind.Rifle).All(u=>Vector2.Distance(u.Destination,g.Sim.Points[0].Position)<4), "Two forces retain independent orders toward different beacons");
            check(g.Sim.Units.First(u=>u.Team==0 && u.Kind==UnitKind.Tank).HasOrder == false, "Split-force orders leave unselected tank orders unchanged");
            KeyPress(Key.Digit4);
            KeyPress(Key.LeftCtrl, Key.Digit6);
            KeyPress(Key.Digit5);
            check(g.Selected.Count == 3 && g.Sim.Units.Where(u => g.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Rifle), "Virtual 5 recalls rifle force");
            KeyPress(Key.Digit6);
            check(g.Selected.Count == 2 && g.GroupCount(2) == 2, "Ctrl+6 reassigns Charlie and virtual 6 recalls the assigned selection");
            g.Recruit(UnitKind.Scout);
            check(g.GroupCount(0) == 3, "Reinforcements join an uncustomized role group");
            int charlie = g.GroupCount(2); g.Sim.Armies[0].RecruitCooldown = 0; g.Sim.Armies[0].Credits = 200; g.Recruit(UnitKind.Tank);
            check(g.GroupCount(2) == charlie, "Recruitment preserves a manually assigned group's membership");
            g.HUD.Refresh();
            Button("Assign selected…").onClick.Invoke(); g.HUD.Refresh();
            var setAlpha = g.HUD.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<TextMeshProUGUI>().text == "Set Alpha");
            setAlpha.onClick.Invoke();
            check(g.GroupCount(0) == g.Selected.Count, "Pointer-accessible assignment stores the selected force");
            g.TogglePause(); var selection = g.Selected.ToArray(); g.RecallGroup(1);
            check(selection.SequenceEqual(g.Selected), "Pause suppresses squad commands"); g.TogglePause();

            g.RecallGroup(1); g.IssueOrder(g.Sim.Points[0].Position); g.World.Render(g.Sim, g.Selected);
            var guides = g.World.GetComponentsInChildren<LineRenderer>(true).Where(l => l.name.StartsWith("Selected force order")).ToArray();
            check(guides.Length == 8 && guides.Any(l => l.enabled), "Selected movement displays a bounded persistent order-guide pool");
            g.Selected.Clear(); g.World.Render(g.Sim, g.Selected);
            check(guides.All(l => !l.enabled), "Deselecting removes order overlays");
            check(g.World.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Low cover position")) == g.Sim.CoverZones.Count, "Visible cover matches all six mechanical cover positions");
            var rifle = g.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Rifle);
            rifle.Position = g.Sim.CoverZones[0].Position; rifle.HasOrder = false; g.Selected.Add(rifle.Id); g.World.Render(g.Sim, g.Selected);
            check(g.SelectedRoleSummary.Contains("protected by cover"), "Selected rifle explains its actual protected state");
            var cannon = g.World.GetComponentsInChildren<Transform>().First(t => t.name == "Recoiling cannon");
            var tank = g.Sim.Units.First(u => u.Kind == UnitKind.Tank && u.Team == 0);
            g.ReducedMotion = false; g.World.ReducedMotion = false;
            var shot = new CombatEvent { ShooterId = tank.Id, WeaponKind = UnitKind.Tank, Heavy = true, Team = 0, From = tank.Position, To = tank.Position + Vector2.up * 4 };
            g.World.PlayEvents(new[] { shot }); g.World.Render(g.Sim, g.Selected);
            check(cannon.localPosition.z < 0, "Tank event drives actual cannon recoil");
            g.ReducedMotion = true; g.World.ReducedMotion = true; g.World.Render(g.Sim, g.Selected);
            check(cannon.localPosition == Vector3.zero, "Reduced motion disables cannon movement");
            int poolBefore = g.World.GetComponentsInChildren<LineRenderer>(true).Length;
            for (int i = 0; i < 150; i++) g.World.PlayEvents(new[] { shot });
            check(g.World.GetComponentsInChildren<LineRenderer>(true).Length == poolBefore, "Repeated firing reuses fixed effects instead of growing the pool");
            check(g.World.GetComponentsInChildren<AudioSource>(true).Count(s => s.name.StartsWith("Battlefield audio voice")) == 8, "Battlefield audio uses exactly eight capped voices");

            g.StartBattle(false); foreach (Army army in g.Sim.Armies) army.IsAI = false;
            float frame = Time.frameCount, elapsed = g.Sim.Elapsed; await Task.Delay(150);
            check(Time.frameCount > frame && g.Sim.Elapsed > elapsed, "Updated game advances actual Editor frames and simulation time");
            g.Sim.Armies[0].BaseHealth -= 25; await Task.Delay(120);
            check(g.TacticalAlertAvailable && g.TacticalAlert.Contains("Headquarters"), "Actual HQ damage creates contextual danger feedback");
            KeyPress(Key.F);
            var focus = (Vector3)typeof(TablefrontGame).GetField("wantedFocus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g);
            check(Vector2.Distance(new Vector2(focus.x, focus.z), g.Sim.Armies[0].BasePosition) < 1.1f, "Virtual F focuses the threatened HQ within camera bounds");
            g.TogglePause(); elapsed = g.Sim.Elapsed; await Task.Delay(100);
            check(g.Sim.Elapsed == elapsed && !g.HUD.GetComponentsInChildren<Button>().Any(b => b.name == "Contextual battlefield alert"), "Pause freezes rules and hides contextual action overlays"); g.TogglePause();
            g.Sim.Armies[0].Score = BattleSimulation.VictoryScore; g.Sim.Step(.05f); g.HUD.Refresh();
            check(g.BattleRecap.Contains("beacon control") && g.BattleRecap.Contains("captures"), "Victory recap explains objective outcome and battle statistics");
            Button("Rematch").onClick.Invoke();
            check(g.PlayerMatch && !g.Spectator && g.Sim.Elapsed == 0 && g.Sim.Armies.All(a => a.UnitsLost == 0 && a.BeaconsCaptured == 0), "Rematch starts a clean player battle and resets recap counters");
            g.Sim.Armies[0].BaseHealth = 0; g.SendMessage("Update");
            check(g.Spectator && g.PlayerMatch, "HQ defeat permits observation while retaining original player match identity");
            g.Rematch(); check(!g.Spectator && g.PlayerMatch, "Rematch after HQ defeat restores player control");
            g.Sim.Armies[1].BaseHealth = g.Sim.Armies[2].BaseHealth = 0; g.Sim.Step(.05f);
            check(g.BattleRecap.Contains("last headquarters standing"), "HQ victory recap avoids claiming credit for another faction's destruction");
            g.Rematch();
            foreach (Army army in g.Sim.Armies) army.BaseHealth = 0;
            g.Sim.Step(.05f); g.SendMessage("Update");
            check(g.Sim.Finished && g.Sim.Winner < 0 && g.BattleRecap.Contains("Draw"), "Simultaneous HQ loss produces a safe draw result");
            g.StartBattle(true); g.Rematch();
            check(g.Spectator && !g.PlayerMatch && g.Sim.Armies.All(a => a.IsAI), "Spectator rematch retains spectator mode");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/tactical-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            g.ReducedMotion = reduced;
            g.ReturnToMenu();
        }
    }
}
