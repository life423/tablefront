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

// Editor-only shell/input checks. No native player input and no training.
public static class VerifyBridgeMission
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode.");
        var passed = new List<string>();
        var selectedMap = game.SelectedBattlefield;
        bool reduced = game.ReducedMotion;
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        var keyboard = InputSystem.AddDevice<Keyboard>("Bridge mission QA keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("Bridge mission QA pointer");
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL " + message);
            passed.Add("PASS " + message);
        }
        void Tick() => game.SendMessage("Update");
        void Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); Tick();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        void Quiet()
        {
            foreach (var army in game.Sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var unit in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
        }
        void Advance(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { game.Sim.Step(.05f); Tick(); }
        }
        try
        {
            game.ReducedMotion = true;
            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.DustBasin);
            game.StartBridgeMission(); Quiet();
            Check(game.MissionActive && !game.TutorialActive && !game.InMenu && !game.Spectator,
                "Bridge Gambit opens a playable mission without starting the tutorial or spectator mode");
            Check(game.SelectedBattlefield == BattlefieldId.DustBasin && game.Sim.Battlefield.Id == BattlefieldId.BridgeGambit,
                "Mission entry preserves the player's skirmish choice");
            Check(game.Sim.Armies.Count(a => a.Alive) == 2 && !game.Sim.Units.Any(u => u.Team > 1),
                "Mission contains two opposing armies and no hidden third force");
            Check(!game.Sim.ExpandedRoster && game.Sim.Units.All(u => (int)u.Kind <= (int)UnitKind.Tank),
                "The experiment uses the three familiar unit classes");
            game.Sim.Armies[0].Credits = 999; game.Sim.Armies[0].RecruitCooldown = 0;
            int count = game.Sim.UnitCount(0); float credits = game.Sim.Armies[0].Credits;
            game.Recruit(UnitKind.Gunner); game.Recruit(UnitKind.Artillery);
            Check(game.Sim.UnitCount(0) == count && game.Sim.Armies[0].Credits == credits,
                "Hidden support-unit shortcuts cannot recruit outside the mission roster");
            Check(game.Sim.Bridge.CommandsRemaining(0) == 3 && game.Sim.Bridge.CommandsRemaining(1) == 3,
                "Both sides start with the same three bridge commands");
            Check(!game.IssueOrder(new Vector2(0, 12)) && !game.Sim.Units.Any(u => u.Team == 0 && u.HasOrder),
                "A click in open water is rejected instead of displaying a false destination marker");
            game.Selected.Clear(); Press(Key.R);
            Check(game.Sim.Bridge.Phase == BridgePhase.Warning && game.Sim.Bridge.TargetRoute == BridgeRoute.South && game.Sim.Bridge.ActorTeam == 0,
                "R requests the alternate crossing without requiring a troop selection");
            Check(game.Sim.Bridge.CommandsRemaining(0) == 2 && game.Sim.Bridge.CommandsRemaining(1) == 3,
                "A bridge request spends only its requesting side's command");
            Press(Key.R);
            Check(game.Sim.Bridge.CommandsRemaining(0) == 2, "Repeated input during the warning cannot spend another command");
            game.TogglePause(); float warning = game.Sim.Bridge.TimeToChange; Press(Key.R); Tick();
            Check(game.Paused && Mathf.Abs(game.Sim.Bridge.TimeToChange - warning) < .001f && game.Sim.Bridge.CommandsRemaining(0) == 2,
                "Pause freezes the bridge warning and suppresses the shortcut");
            game.TogglePause(); game.ToggleHelp(); warning = game.Sim.Bridge.TimeToChange; Press(Key.R); Tick();
            Check(game.HelpOpen && Mathf.Abs(game.Sim.Bridge.TimeToChange - warning) < .001f,
                "Help freezes the bridge and suppresses world control");
            game.ToggleHelp(); Advance(5.2f);
            Check(game.Sim.Bridge.Phase == BridgePhase.Stable && game.Sim.Bridge.CurrentRoute == BridgeRoute.South,
                "The requested warning and movement dock the bridge on the alternate route");
            Check(!game.ToggleBridge() && game.Sim.Bridge.CommandsRemaining(0) == 2,
                "The visible cooldown also governs the controller shortcut");
            game.Rematch(); Quiet();
            Check(game.MissionActive && game.Sim.Elapsed == 0 && game.Sim.Bridge.CurrentRoute == BridgeRoute.North && game.Sim.Bridge.CommandsRemaining(0) == 3,
                "Rematch resets mission routes, timers and commands rather than opening a skirmish");
            game.Sim.Finished = true; game.Sim.Winner = 0; game.NextBattlefield();
            Check(game.Sim.MissionActive && game.Sim.Finished, "Mission results cannot accidentally advance into a normal map");
            game.ReturnToMenu();
            Check(!game.MissionActive && game.SelectedBattlefield == BattlefieldId.DustBasin && game.Sim.Battlefield.Id == BattlefieldId.DustBasin,
                "Returning to the title restores the selected skirmish preview");
            game.StartTutorial();
            Check(game.TutorialActive && !game.Sim.MissionActive && game.Sim.PracticeMode && game.Sim.Battlefield.Id == BattlefieldId.Greenfield,
                "The existing six-step tutorial stays separate from the bridge mission");
            game.ReturnToMenu(); game.StartBattle(false);
            Check(!game.MissionActive && game.Sim.ExpandedRoster && game.Sim.Battlefield.Id == BattlefieldId.DustBasin,
                "Ordinary battles retain the five-unit roster and chosen battlefield");
            Check(!game.ToggleBridge(), "Bridge shortcuts are inert outside the mission");
            Directory.CreateDirectory("TestResults"); File.WriteAllLines("TestResults/bridge-mission-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            game.ReducedMotion = reduced; game.ReturnToMenu(); game.SetBattlefield(selectedMap);
            PlayerPrefs.Save(); await Task.Yield();
        }
    }
}
