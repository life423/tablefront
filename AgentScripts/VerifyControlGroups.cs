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
using UnityEngine.UI;

// Editor-only integration verification. These virtual devices never send native Windows input.
public static class VerifyControlGroups
{
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Assets/Scenes/Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }

        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        bool reduced = game.ReducedMotion;
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront group QA keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("Tablefront group QA mouse");
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        game.ReducedMotion = true;
        var focusField = typeof(TablefrontGame).GetField("wantedFocus", BindingFlags.Instance | BindingFlags.NonPublic);
        if (focusField == null) throw new Exception("Camera target field unavailable");
        var digits = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        string Number(int slot) => ((slot + 1) % 10).ToString();
        void Tick() => game.SendMessage("Update");
        void KeyPress(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); Tick();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        void Select(params int[] ids) { game.Selected.Clear(); game.Selected.UnionWith(ids); }
        Vector3 Focus() => (Vector3)focusField.GetValue(game);
        void SetFocus(Vector3 value) => focusField.SetValue(game, value);
        bool At(Vector3 expected) => Vector2.Distance(new Vector2(Focus().x, Focus().z), new Vector2(expected.x, expected.z)) < .05f;
        void QuietBattle(bool spectator = false)
        {
            game.StartBattle(spectator);
            var turretTimer = typeof(Army).GetField("TurretTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var weaponTimer = typeof(Troop).GetField("WeaponTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (turretTimer == null || weaponTimer == null) throw new Exception("Combat cooldown fields unavailable for isolated group QA");
            foreach (var army in game.Sim.Armies) { army.IsAI = false; turretTimer.SetValue(army, 99999f); }
            foreach (var troop in game.Sim.Units) weaponTimer.SetValue(troop, 99999f);
        }

        try
        {
            QuietBattle();
            var friendly = game.Sim.Units.Where(u => u.Team == 0 && u.Alive).ToArray();
            Check(Enumerable.Range(0, 10).All(slot => game.GroupCount(slot) == 0), "All ten groups start empty rather than silently representing troop classes");
            for (int slot = 0; slot < digits.Length; slot++)
            {
                var troop = friendly[slot % friendly.Length];
                Select(troop.Id); KeyPress(Key.LeftCtrl, digits[slot]);
                Check(game.GroupCount(slot) == 1, "Ctrl+" + Number(slot) + " saves its own group");
            }
            for (int slot = 0; slot < digits.Length; slot++)
            {
                Select(); KeyPress(digits[slot]);
                Check(game.Selected.SetEquals(new[] { friendly[slot % friendly.Length].Id }), Number(slot) + " recalls its exact saved selection");
            }

            Select(friendly[0].Id, friendly[1].Id); KeyPress(Key.LeftCtrl, Key.Digit1);
            Select(friendly[2].Id); KeyPress(Key.LeftCtrl, Key.Digit1);
            Select(); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[2].Id }), "Ctrl+number replaces the previous group rather than merging it");
            Select(friendly[3].Id); KeyPress(Key.LeftShift, Key.Digit1);
            Select(); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[2].Id, friendly[3].Id }), "Shift+number adds selected troops without losing existing members");
            KeyPress(Key.LeftShift, Key.Digit1);
            Check(game.GroupCount(0) == 2, "Adding existing members never duplicates a troop");
            Select(friendly[4].Id); KeyPress(Key.RightShift, Key.Digit1);
            Select(); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[2].Id, friendly[3].Id, friendly[4].Id }), "Right Shift supports group addition");
            Select(friendly[5].Id); KeyPress(Key.RightCtrl, Key.Digit2);
            Select(); KeyPress(Key.Digit2);
            Check(game.Selected.SetEquals(new[] { friendly[5].Id }), "Right Ctrl supports group assignment");
            Select(friendly[0].Id); KeyPress(Key.LeftCtrl, Key.LeftShift, Key.Digit1);
            Select(); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[0].Id }), "Ctrl takes precedence over Shift when both modifiers are held");
            Select(friendly[0].Id); KeyPress(Key.LeftCtrl, Key.Digit2);
            Check(game.GroupCount(0) == 1 && game.GroupCount(1) == 1, "One troop may belong to multiple control groups");
            Select(); KeyPress(Key.LeftCtrl, Key.Digit2); KeyPress(Key.Digit2);
            Check(game.Selected.SetEquals(new[] { friendly[0].Id }), "An empty assignment leaves an existing group intact");

            QuietBattle(); friendly = game.Sim.Units.Where(u => u.Team == 0 && u.Alive).ToArray();
            var enemy = game.Sim.Units.First(u => u.Team != 0 && u.Alive);
            Select(friendly[0].Id, friendly[1].Id, enemy.Id, int.MaxValue); KeyPress(Key.LeftCtrl, Key.Digit4);
            Select(); KeyPress(Key.Digit4);
            Check(game.Selected.SetEquals(new[] { friendly[0].Id, friendly[1].Id }), "Assignment filters enemy and stale selection identifiers");
            friendly[0].Health = 0;
            Tick(); Select(); KeyPress(Key.Digit4);
            Check(game.Selected.SetEquals(new[] { friendly[1].Id }) && game.GroupCount(3) == 1, "Casualties disappear from the group and cannot be recalled");
            Select(enemy.Id, int.MaxValue); KeyPress(Key.LeftShift, Key.Digit4);
            Check(game.GroupCount(3) == 1, "Group addition ignores enemy and stale identifiers");
            Select(friendly[2].Id); KeyPress(Key.RightShift, Key.Digit0); Select(); KeyPress(Key.Digit0);
            Check(game.Selected.SetEquals(new[] { friendly[2].Id }) && game.GroupCount(9) == 1, "Shift+0 can create an empty group using the same addition rule");
            Select(friendly[2].Id); KeyPress(Key.LeftCtrl, Key.Digit1);
            Select(friendly[3].Id); KeyPress(Key.LeftCtrl, Key.Digit2);
            game.TogglePause(); Select(friendly[4].Id);
            KeyPress(Key.LeftCtrl, Key.Digit4); KeyPress(Key.LeftShift, Key.Digit4); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[4].Id }) && game.GroupCount(3) == 1, "Pause suppresses assignment, addition and recall");
            game.TogglePause(); game.ToggleHelp();
            KeyPress(Key.LeftCtrl, Key.Digit4); KeyPress(Key.LeftShift, Key.Digit4); KeyPress(Key.Digit1);
            Check(game.Selected.SetEquals(new[] { friendly[4].Id }) && game.GroupCount(3) == 1, "Help suppresses assignment, addition and recall");
            game.ToggleHelp();
            game.SelectKind(UnitKind.Rifle);
            var rifles = game.Selected.ToArray(); KeyPress(Key.LeftCtrl, Key.Digit7);
            game.Sim.Armies[0].Credits = 999; game.Sim.Armies[0].RecruitCooldown = 0;
            game.Recruit(UnitKind.Rifle);
            Select(); KeyPress(Key.Digit7);
            Check(game.Selected.SetEquals(rifles), "New recruits do not silently join an existing custom group");
            var rifleButton = game.HUD.GetComponentsInChildren<Button>(true).First(b => b.name == "Rifles");
            rifleButton.onClick.Invoke();
            Check(game.Selected.Count == rifles.Length + 1 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Rifle), "Named Rifles button still selects every live rifle including new recruits");
            int troopCount = game.Sim.UnitCount(0); float credits = game.Sim.Armies[0].Credits;
            foreach (var key in digits) KeyPress(key);
            Check(game.Sim.UnitCount(0) == troopCount && game.Sim.Armies[0].Credits >= credits, "Number keys only recall groups and never buy troops");

            QuietBattle(); friendly = game.Sim.Units.Where(u => u.Team == 0 && u.Alive).ToArray();
            friendly[0].Position = friendly[0].Destination = new Vector2(7, 6);
            friendly[1].Position = friendly[1].Destination = new Vector2(11, 10);
            Select(friendly[0].Id, friendly[1].Id); KeyPress(Key.LeftCtrl, Key.Digit9);
            Vector3 sentinel = new Vector3(-18, 0, -19), centre = new Vector3(9, 0, 8);
            SetFocus(sentinel); KeyPress(Key.Digit9);
            Check(At(sentinel), "A single number press selects without moving the camera");
            KeyPress(Key.Digit9);
            Check(At(centre), "Double-tapping a group number centers the camera on its live troop centroid");
            SetFocus(sentinel); KeyPress(Key.LeftShift, Key.Digit9);
            Check(At(sentinel), "Shift group addition never triggers camera centering");
            SetFocus(sentinel); KeyPress(Key.LeftCtrl, Key.Digit9); KeyPress(Key.Digit9);
            Check(At(sentinel), "Saving a group resets double-tap detection rather than counting as a recall");
            await Task.Delay(450);
            SetFocus(sentinel); KeyPress(Key.Digit9);
            Check(At(sentinel), "Two number presses outside the double-tap interval do not center the camera");
            KeyPress(Key.Digit8); SetFocus(sentinel); KeyPress(Key.Digit9);
            Check(At(sentinel), "Pressing a different group breaks the double-tap sequence");
            friendly[1].Health = 0; Tick();
            SetFocus(sentinel); KeyPress(Key.Digit9); KeyPress(Key.Digit9);
            Check(At(new Vector3(7, 0, 6)), "Camera centering ignores dead members");
            game.AssignGroup(-1); game.AssignGroup(10); game.AddToGroup(-1); game.AddToGroup(10); game.RecallGroup(-1); game.RecallGroup(10); game.FocusGroup(-1); game.FocusGroup(10);
            Check(game.GroupCount(-1) == 0 && game.GroupCount(10) == 0, "Invalid group indices safely reject every operation");

            QuietBattle(true); game.Selected.Add(game.Sim.Units.First(u => u.Team == 0).Id);
            KeyPress(Key.LeftCtrl, Key.Digit1); KeyPress(Key.LeftShift, Key.Digit2); KeyPress(Key.Digit3);
            Check(Enumerable.Range(0, 10).All(slot => game.GroupCount(slot) == 0), "Spectator mode cannot create or add command groups");
            game.ReturnToMenu(); KeyPress(Key.LeftCtrl, Key.Digit1); KeyPress(Key.LeftShift, Key.Digit2);
            Check(Enumerable.Range(0, 10).All(slot => game.GroupCount(slot) == 0), "Title menus ignore gameplay group shortcuts");
            QuietBattle(); game.SelectAll(); KeyPress(Key.LeftCtrl, Key.Digit1); game.StartBattle(false);
            Check(Enumerable.Range(0, 10).All(slot => game.GroupCount(slot) == 0), "A fresh battle clears all custom groups");
            game.SelectAll(); KeyPress(Key.LeftCtrl, Key.Digit0); game.StartTutorial();
            Check(Enumerable.Range(0, 10).All(slot => game.GroupCount(slot) == 0), "Starting tutorial practice clears previous battle groups");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/control-groups-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            game.ReducedMotion = reduced;
            game.ReturnToMenu();
        }
    }
}
