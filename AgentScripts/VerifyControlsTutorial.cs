using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Editor-only integration verification. Virtual input stays inside Unity; no native input is sent.
public static class VerifyControlsTutorial
{
    const string CompletionKey = "Tablefront.TutorialCompleted";

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
        bool hadCompletion = PlayerPrefs.HasKey(CompletionKey);
        int oldCompletion = PlayerPrefs.GetInt(CompletionKey, 0);
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront controls QA keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("Tablefront controls QA mouse");
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        game.ReducedMotion = true;
        PlayerPrefs.SetInt(CompletionKey, 0);

        Button Button(string name) => game.HUD.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        Vector2 ScreenPoint(Vector2 point, float height = .6f) => game.ViewCamera.WorldToScreenPoint(new Vector3(point.x, height, point.y));
        void Tick() { game.SendMessage("Update"); }
        void KeyPress(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); Tick();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        async Task Pointer(Vector2 point, MouseButton? held = null)
        {
            var state = new MouseState { position = point };
            if (held.HasValue) state = state.WithButton(held.Value);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update(); Tick();
            await Task.Delay(40);
        }
        async Task Click(Vector2 point, MouseButton button = MouseButton.Left)
        {
            await Pointer(point);
            await Pointer(point, button);
            await Pointer(point);
        }
        async Task Drag(Vector2 from, Vector2 to)
        {
            await Pointer(from);
            await Pointer(from, MouseButton.Left);
            await Pointer(to, MouseButton.Left);
            await Pointer(to);
        }
        void AdvanceUntil(Func<bool> condition, float maximumSeconds)
        {
            int steps = Mathf.CeilToInt(maximumSeconds / .05f);
            for (int i = 0; i < steps && !condition(); i++)
            {
                game.Sim.Step(.05f);
                Tick();
            }
        }
        void QuietBattle()
        {
            game.StartBattle(false);
            foreach (Army army in game.Sim.Armies) army.IsAI = false;
        }

        try
        {
            game.ReturnToMenu();
            Button("Tutorial").onClick.Invoke();
            await Task.Delay(100);
            Check(game.TutorialActive && game.TutorialStep == 0 && !game.InMenu, "Title Tutorial action opens a dedicated practice session");
            Check(game.Sim.PracticeMode && game.Sim.Armies.All(a => !a.IsAI), "Tutorial explicitly disables victory pressure and opposing strategic commanders");
            Check(game.Selected.Count == 0, "Tutorial begins with an actual selection task");
            int startingCount = game.Sim.UnitCount(0);
            for (int i = 0; i < BattleSimulation.UnitCap; i++) { game.Sim.Armies[0].RecruitCooldown = 0; game.Recruit(UnitKind.Scout); }
            Check(game.Sim.UnitCount(0) == startingCount, "Early recruitment cannot fill the army and block the required rifle lesson");
            game.CompleteTutorial();
            Check(game.TutorialActive && game.TutorialStep == 0 && !game.TutorialCompleted, "Incomplete lesson cannot mark completion or launch a match");
            Check(!game.IssueOrder(game.TutorialTargetPosition), "An unselected force cannot bypass the selection task with an order");
            game.Sim.Armies[0].Score = BattleSimulation.VictoryScore + 1;
            game.Sim.Step(.05f);
            Check(!game.Sim.Finished, "Practice remains playable beyond the normal score victory threshold");
            game.Sim.Armies[0].Score = 0;

            var first = game.Sim.Units.First(u => u.Team == 0 && u.Alive);
            await Click(ScreenPoint(first.Position));
            Check(game.Selected.Count == 1 && game.Selected.Contains(first.Id), "One left click selects a friendly miniature through actual input");
            Check(game.TutorialStep == 1, "Selecting a real friendly advances the first tutorial task");
            Vector2 moveMarker = game.TutorialTargetPosition;
            await Click(ScreenPoint(moveMarker, 0));
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.HasOrder), "One left click on open ground gives a movement order");
            Check(game.TutorialStep == 1, "The movement lesson waits for actual arrival rather than merely accepting an order");
            AdvanceUntil(() => game.TutorialStep >= 2, 25);
            Check(game.TutorialStep == 2, "Actual movement to the marked location completes the movement lesson");
            Check(game.Sim.Units.Any(u => u.Team == 0 && Vector2.Distance(u.Position, moveMarker) < 4), "Tutorial movement completion corresponds to a physically arrived friendly");
            await Task.Delay(80);
            await Click(ScreenPoint(game.TutorialTargetPosition, 0));
            Check(game.TutorialStep == 2, "Clicking the beacon does not substitute for capturing it");
            AdvanceUntil(() => game.TutorialStep >= 3, 30);
            Check(game.TutorialStep == 3 && game.Sim.Points[0].Owner == 0, "Beacon task completes through normal movement and capture rules");

            KeyPress(Key.Z); Tick();
            Check(game.TutorialStep == 3, "Recruiting the wrong class does not bypass the rifle recruitment task");
            AdvanceUntil(() => game.Sim.Armies[0].RecruitCooldown <= 0, 5);
            int rifles = game.Sim.Units.Count(u => u.Team == 0 && u.Kind == UnitKind.Rifle && u.Alive);
            float credits = game.Sim.Armies[0].Credits;
            KeyPress(Key.X); Tick();
            Check(game.TutorialStep == 4 && game.Sim.Units.Count(u => u.Team == 0 && u.Kind == UnitKind.Rifle && u.Alive) == rifles + 1,
                "X deploys a paid rifle and advances the recruitment task");
            Check(game.Sim.Armies[0].Credits < credits, "Tutorial recruitment uses the game's actual supply cost");
            Check(game.TutorialTargetId >= 0 && game.Sim.Units.Any(u => u.Id == game.TutorialTargetId && u.Team != 0 && u.Alive),
                "Attack task presents a live marked enemy target");
            int enemyId = game.TutorialTargetId;
            await Task.Delay(80);
            game.SelectAll();
            var enemy = game.Sim.Units.First(u => u.Id == enemyId);
            await Click(ScreenPoint(enemy.Position));
            Check(game.Sim.Units.Any(u => u.Team == 0 && u.AttackTargetId == enemyId), "One left click on an enemy issues a real attack order");
            Check(game.TutorialStep == 4, "Attack task waits for actual target destruction");
            AdvanceUntil(() => game.TutorialStep >= 5, 60);
            Check(game.TutorialStep == 5 && !game.Sim.Units.Any(u => u.Id == enemyId && u.Alive), "Normal combat destroys the marked enemy before advancing the attack task");

            Button("Scouts").onClick.Invoke();
            Check(game.Selected.Count > 0 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Scout),
                "Scouts button selects scouts without spending supplies");
            await Task.Delay(80);
            await Click(ScreenPoint(game.TutorialTargetPosition, 0));
            AdvanceUntil(() => game.Sim.Units.Any(u => u.Team == 0 && u.Kind == UnitKind.Scout && game.Sim.IsInCover(u.Position)), 30);
            Check(game.TutorialStep == 5, "A scout in cover cannot bypass the rifle cover lesson");
            Button("Rifles").onClick.Invoke();
            Check(game.Selected.Count > 0 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Rifle),
                "Rifles button selects the rifle force");
            await Click(ScreenPoint(game.TutorialTargetPosition, 0));
            Check(game.TutorialStep == 5, "Cover task waits for actual rifle protection");
            AdvanceUntil(() => game.TutorialStep >= 6, 45);
            Check(game.TutorialStep == 6 && game.Sim.Units.Any(u => u.Team == 0 && game.Sim.IsProtected(u)),
                "Moving a rifle into mechanical cover completes the six-task tutorial");
            Check(game.Sim.PracticeMode && game.Sim.Armies.All(a => !a.IsAI), "Completing tasks does not silently start an enemy attack");
            game.HUD.Refresh();
            Button("Play a battle").onClick.Invoke();
            Check(game.TutorialCompleted && PlayerPrefs.GetInt(CompletionKey, 0) == 1, "Finishing explicitly records tutorial completion");
            Check(!game.TutorialActive && !game.Sim.PracticeMode && !game.Sim.Finished && game.PlayerMatch,
                "Play a battle leaves practice mode for a real player match");
            Check(game.Sim.Elapsed < .2f && game.Sim.OwnedPoints(0) == 0 && game.Sim.UnitCount(0) == 6 && game.Sim.Armies[1].IsAI && game.Sim.Armies[2].IsAI,
                "The live battle starts fresh with the normal army, score and opposing commanders");

            game.StartTutorial(); game.SelectAll(); Tick(); game.Rematch();
            Check(game.TutorialActive && game.TutorialStep == 0 && game.Selected.Count == 0 && game.Sim.PracticeMode,
                "Restart during a tutorial restarts practice rather than launching a live battle");
            game.SkipTutorial();
            Check(game.InMenu && !game.TutorialActive && !game.Sim.PracticeMode, "Exit tutorial returns safely to the title");

            QuietBattle(); await Task.Delay(80);
            bool priorCommander = game.Sim.Armies[0].IsAI;
            KeyPress(Key.Tab);
            Check(game.Sim.Armies[0].IsAI == priorCommander, "Tab navigation cannot accidentally hand the player's army to AI");
            Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null,
                "Tab gives visible UI controls keyboard focus");
            KeyPress(Key.LeftShift, Key.Tab);
            Check(game.Sim.Armies[0].IsAI == priorCommander, "Shift+Tab reverses UI focus without changing commander mode");
            float initialCredits = game.Sim.Armies[0].Credits;
            int initialUnits = game.Sim.UnitCount(0);
            Button("Scouts").onClick.Invoke(); KeyPress(Key.LeftCtrl, Key.Digit1); game.Selected.Clear(); KeyPress(Key.Digit1);
            Check(game.Selected.Count == 2 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Scout), "Ctrl+1 saves and 1 recalls a chosen scout group");
            Button("Rifles").onClick.Invoke(); KeyPress(Key.LeftCtrl, Key.Digit2); game.Selected.Clear(); KeyPress(Key.Digit2);
            Check(game.Selected.Count == 3 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == UnitKind.Rifle), "Ctrl+2 saves and 2 recalls a chosen rifle group");
            Button("Tanks").onClick.Invoke(); KeyPress(Key.LeftCtrl, Key.Digit3); game.Selected.Clear(); KeyPress(Key.Digit3);
            Check(game.Selected.Count == 1 && game.Sim.Units.Single(u => game.Selected.Contains(u.Id)).Kind == UnitKind.Tank, "Ctrl+3 saves and 3 recalls a chosen tank group");
            foreach (var role in new[] { ("Scouts", UnitKind.Scout), ("Rifles", UnitKind.Rifle), ("Tanks", UnitKind.Tank) })
            {
                Button(role.Item1).onClick.Invoke();
                Check(game.Selected.Count > 0 && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == role.Item2),
                    "Labeled " + role.Item1 + " button selects its own class without shortcuts");
            }
            Button("Select whole army").onClick.Invoke();
            Check(game.Selected.Count == game.Sim.UnitCount(0), "Labeled whole-army button selects all friendly troops");
            Check(game.Sim.UnitCount(0) == initialUnits && game.Sim.Armies[0].Credits >= initialCredits, "Class selection never recruits or spends supplies");
            game.Sim.Armies[0].Credits = 999;
            foreach (var binding in new[] { (Key.Z, UnitKind.Scout), (Key.X, UnitKind.Rifle), (Key.C, UnitKind.Tank) })
            {
                AdvanceUntil(() => game.Sim.Armies[0].RecruitCooldown <= 0, 5);
                int prior = game.Sim.Units.Count(u => u.Team == 0 && u.Kind == binding.Item2 && u.Alive);
                KeyPress(binding.Item1);
                Check(game.Sim.Units.Count(u => u.Team == 0 && u.Kind == binding.Item2 && u.Alive) == prior + 1,
                    binding.Item1 + " recruits the labeled " + binding.Item2 + " class");
            }
            AdvanceUntil(() => game.Sim.Armies[0].RecruitCooldown <= 0, 5);
            game.HUD.Refresh();
            var recruitButton = game.HUD.GetComponentsInChildren<Button>().First(b =>
                b.GetComponentInChildren<TextMeshProUGUI>() != null && b.GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("Recruit Rifles"));
            int beforeRecruitButton = game.Sim.UnitCount(0);
            Check(recruitButton.interactable, "Visible rifle recruitment action is enabled when affordable and ready");
            recruitButton.onClick.Invoke();
            Check(game.Sim.UnitCount(0) == beforeRecruitButton + 1, "Labeled Recruit Rifles action deploys a squad without a keyboard shortcut");

            QuietBattle(); await Task.Delay(80);
            game.Selected.Clear();
            await Click(ScreenPoint(new Vector2(-6, -10), 0));
            Check(game.Selected.Count == 0 && game.Sim.Units.Where(u => u.Team == 0).All(u => !u.HasOrder), "An empty selection cannot accidentally command an army");
            first = game.Sim.Units.First(u => u.Team == 0 && u.Alive);
            await Click(ScreenPoint(first.Position));
            Check(game.Selected.SetEquals(new[] { first.Id }), "Single-click selection remains exact in a normal battle");
            Vector2 destination = new Vector2(-6, -10);
            await Click(ScreenPoint(destination, 0));
            Check(first.HasOrder && Vector2.Distance(first.Destination, destination) < 2,
                "Left-click destination moves the selected miniature and retains selection");
            Check(game.Selected.Contains(first.Id), "Moving does not deselect the commanded force");
            enemy = game.Sim.Units.First(u => u.Team == 1 && u.Kind == UnitKind.Scout);
            enemy.Position = new Vector2(6, -5); enemy.HasOrder = false;
            game.World.Render(game.Sim, game.Selected);
            await Click(ScreenPoint(enemy.Position));
            Check(first.AttackTargetId == enemy.Id, "Enemy click targets an opponent without requiring a different mouse button");
            destination = new Vector2(0, -8);
            await Click(ScreenPoint(destination, 0), MouseButton.Right);
            Check(first.HasOrder && first.AttackTargetId < 0 && Vector2.Distance(first.Destination, destination) < 2,
                "Existing right-click movement remains available as a secondary control");

            var friendlyPoints = game.Sim.Units.Where(u => u.Team == 0 && u.Alive).Select(u => ScreenPoint(u.Position)).ToArray();
            var low = new Vector2(friendlyPoints.Min(p => p.x) - 12, friendlyPoints.Min(p => p.y) - 12);
            var high = new Vector2(friendlyPoints.Max(p => p.x) + 12, friendlyPoints.Max(p => p.y) + 12);
            await Drag(low, high);
            Check(game.Selected.SetEquals(game.Sim.Units.Where(u => u.Team == 0 && u.Alive).Select(u => u.Id)),
                "Dragging selects the boxed friendly force instead of sending a ground order");
            game.TogglePause();
            var pausedSelection = game.Selected.ToArray();
            var pausedOrders = game.Sim.Units.Where(u => u.Team == 0).Select(u => u.Destination).ToArray();
            // Use the scrim outside the central pause card, avoiding its Resume/Help controls.
            await Click(new Vector2(30, Screen.height * .5f));
            Check(game.Paused && game.Selected.SetEquals(pausedSelection) && pausedOrders.SequenceEqual(game.Sim.Units.Where(u => u.Team == 0).Select(u => u.Destination)),
                "Pause suppresses smart-click world commands");
            game.TogglePause();
            KeyPress(Key.LeftCtrl, Key.A);
            Check(game.Selected.Count == game.Sim.UnitCount(0), "Ctrl+A remains a discoverable all-army shortcut");
            game.Sim.Armies[0].Score = BattleSimulation.VictoryScore;
            game.Sim.Step(.05f);
            Check(game.Sim.Finished && game.Sim.Winner == 0, "Normal matches still use normal victory rules outside the tutorial");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/controls-tutorial-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            game.ReducedMotion = reduced;
            if (hadCompletion) PlayerPrefs.SetInt(CompletionKey, oldCompletion); else PlayerPrefs.DeleteKey(CompletionKey);
            PlayerPrefs.Save();
            game.ReturnToMenu();
        }
    }
}
