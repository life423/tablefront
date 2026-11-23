using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only integration checks. All input is virtual; the user's player is never driven.
public static class VerifyExpandedControls
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        bool reduced = game.ReducedMotion;
        bool savedMap = PlayerPrefs.HasKey("Tablefront.Battlefield");
        int oldMap = PlayerPrefs.GetInt("Tablefront.Battlefield");
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront expanded controls QA");
        var mouse = InputSystem.AddDevice<Mouse>("Tablefront expanded pointer QA");
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }
        void Tick() => game.SendMessage("Update");
        void Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
            Tick();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        Vector3 Focus() => (Vector3)typeof(TablefrontGame).GetField("wantedFocus", Fields).GetValue(game);
        bool AttackMove(Troop unit) => (bool)typeof(Troop).GetField("AttackMove", Fields).GetValue(unit);
        void QuietBattle()
        {
            game.StartBattle(false);
            foreach (var army in game.Sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var unit in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
        }
        Vector2 ScreenPoint(Troop unit) => game.ViewCamera.WorldToScreenPoint(new Vector3(unit.Position.x, .6f, unit.Position.y));
        try
        {
            game.ReducedMotion = true;
            game.ReturnToMenu();
            foreach (var map in TablefrontBattlefields.All)
            {
                game.SetBattlefield(map.Id);
                Check(game.SelectedBattlefield == map.Id && game.Sim.Battlefield.Id == map.Id && game.InMenu,
                    map.Name + " can be previewed without starting a battle");
                QuietBattle();
                Check(game.Sim.ExpandedRoster && game.Sim.Battlefield.Id == map.Id &&
                    game.Sim.Armies.Select(a => a.BasePosition).SequenceEqual(map.BasePositions),
                    map.Name + " starts its actual expanded-roster layout");
                game.SendMessage("LateUpdate");
                Check(game.Sim.Armies.All(a => { var p = game.ViewCamera.WorldToViewportPoint(new Vector3(a.BasePosition.x, 2.4f, a.BasePosition.y)); return p.z > 0 && p.y > .24f && p.y < .86f; }),
                    map.Name + " opening camera keeps HQ tops between header and command deck");
                game.Rematch();
                Check(game.Sim.Battlefield.Id == map.Id && game.Sim.Elapsed == 0,
                    map.Name + " rematch preserves the battlefield and resets the simulation");
                game.ReturnToMenu();
            }
            game.SetBattlefield(BattlefieldId.DustBasin);
            game.StartTutorial();
            Check(game.Sim.Battlefield.Id == BattlefieldId.Greenfield && game.Sim.PracticeMode && !game.Sim.ExpandedRoster,
                "Tutorial uses its proven Greenfield setup independently of the chosen battle map");
            game.SkipTutorial();
            Check(game.SelectedBattlefield == BattlefieldId.DustBasin && game.Sim.Battlefield.Id == BattlefieldId.DustBasin,
                "Leaving practice restores the selected map preview");
            game.SetBattlefield(BattlefieldId.Greenfield);
            QuietBattle();
            var troop = game.Sim.Units.First(u => u.Team == 0 && u.Alive);
            game.Selected.Clear(); game.Selected.Add(troop.Id);
            Vector3 originalFocus = Focus();
            Press(Key.A);
            Check(game.AttackMoveArmed && Focus() == originalFocus,
                "A arms attack-move without panning the camera");
            game.SendMessage("LateUpdate");
            Vector3 attackPoint = game.ViewCamera.WorldToScreenPoint(new Vector3(-3, 0, -9));
            game.ClickWorld(attackPoint);
            Check(!game.AttackMoveArmed && troop.HasOrder && AttackMove(troop),
                "An armed attack-move click gives the destination order and returns to normal selection");
            Press(Key.A);
            Press(Key.Escape);
            Check(!game.AttackMoveArmed && !game.Paused,
                "Escape cancels a pending order before opening Pause");
            game.IssueOrder(new Vector2(-3, -9));
            Check(troop.HasOrder && !AttackMove(troop) && !troop.HoldPosition,
                "Ordinary movement stays distinct from attack-move");
            originalFocus = Focus();
            Press(Key.S);
            Check(!troop.HasOrder && troop.AttackTargetId < 0 && Focus() == originalFocus,
                "S stops selected troops without moving the camera");
            Press(Key.RightArrow);
            Check(Focus().x > originalFocus.x, "Arrow keys pan the camera independently of command keys");
            Press(Key.H);
            Check(troop.HoldPosition && !troop.HasOrder,
                "H holds position without leaving a movement order");
            game.IssueOrder(new Vector2(-4, -8));
            Check(!troop.HoldPosition && troop.HasOrder, "A new movement order releases Hold");
            game.IssueOrder(new Vector2(-5, -7), -1, true);
            Check(AttackMove(troop), "Explicit attack-move enables engagement on the route");
            Press(Key.A); game.TogglePause();
            Check(!game.AttackMoveArmed && game.Paused, "Pause cancels an armed order");
            game.TogglePause(); Press(Key.A); game.ToggleHelp();
            Check(!game.AttackMoveArmed && game.HelpOpen, "Help cancels an armed order");
            game.ToggleHelp();

            game.Sim.Armies[0].Credits = 999;
            game.Sim.Armies[0].RecruitCooldown = 0;
            int count = game.Sim.UnitCount(0); float credits = game.Sim.Armies[0].Credits;
            Press(Key.V);
            Check(game.Sim.UnitCount(0) == count + 1 && game.Sim.Units.Last(u => u.Team == 0).Kind == UnitKind.Gunner &&
                game.Sim.Armies[0].Credits <= credits - BattleSimulation.Cost(UnitKind.Gunner) + 1,
                "V recruits a paid Gunner from the same roster as the AI");
            game.Sim.Armies[0].RecruitCooldown = 0; credits = game.Sim.Armies[0].Credits;
            Press(Key.B);
            Check(game.Sim.Units.Last(u => u.Team == 0).Kind == UnitKind.Artillery &&
                game.Sim.Armies[0].Credits <= credits - BattleSimulation.Cost(UnitKind.Artillery) + 1,
                "B recruits paid Artillery");
            game.SelectKind(UnitKind.Gunner); Press(Key.LeftCtrl, Key.Digit5);
            game.SelectKind(UnitKind.Artillery); Press(Key.LeftShift, Key.Digit5); game.Selected.Clear(); Press(Key.Digit5);
            Check(game.Selected.Count == 2 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Gunner || u.Kind == UnitKind.Artillery),
                "Existing control groups save, extend and recall the new unit classes");

            game.SelectKind(UnitKind.Rifle);
            var rifles = game.Sim.Units.Where(u => u.Team == 0 && u.Kind == UnitKind.Rifle).ToArray();
            rifles[0].Position = rifles[0].Destination = new Vector2(-3, -10);
            rifles[1].Position = rifles[1].Destination = new Vector2(3, -10);
            rifles[2].Position = rifles[2].Destination = new Vector2(25, 25);
            foreach (var rifle in rifles) rifle.HasOrder = false;
            game.FocusBase(); game.SendMessage("LateUpdate");
            var visibleRifles = rifles.Where(u => { var p = game.ViewCamera.WorldToViewportPoint(new Vector3(u.Position.x, .6f, u.Position.y)); return p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1; }).ToArray();
            Check(visibleRifles.Length >= 2, "Double-click setup contains multiple visible same-class troops");
            var click = ScreenPoint(visibleRifles[0]);
            game.ClickWorld(click); game.ClickWorld(click);
            Check(game.Selected.SetEquals(visibleRifles.Select(u => u.Id)),
                "Double-click selects visible troops of the same class without selecting offscreen units");
            Check(!game.TryGround(new Vector2(-10, Screen.height / 2), out _),
                "Pointer positions outside the viewport cannot send world orders");

            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.Ridgeway); QuietBattle();
            game.Sim.Finished = true; game.Sim.Winner = 0; game.NextBattlefield();
            Check(game.Sim.Battlefield.Id == BattlefieldId.DustBasin && !game.Sim.Finished && game.Sim.Elapsed == 0,
                "Next battlefield starts a fresh match on the next level");
            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/expanded-controls-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            game.ReducedMotion = reduced;
            game.ReturnToMenu(); game.SetBattlefield(TablefrontBattlefields.Get((BattlefieldId)oldMap).Id);
            if (!savedMap) PlayerPrefs.DeleteKey("Tablefront.Battlefield"); else PlayerPrefs.SetInt("Tablefront.Battlefield", oldMap);
            PlayerPrefs.Save();
            await Task.Yield();
        }
    }
}
