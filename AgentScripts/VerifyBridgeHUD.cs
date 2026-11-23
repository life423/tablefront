using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Editor-only actual-callback verification. Never ships or sends native input.
public static class VerifyBridgeHUD
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        bool enabled = game.enabled, reduced = game.ReducedMotion;
        var originalMap = game.SelectedBattlefield;
        bool hadMap = PlayerPrefs.HasKey("Tablefront.Battlefield");
        int originalPreference = PlayerPrefs.GetInt("Tablefront.Battlefield", 0);
        T Field<T>(string name) => (T)typeof(TablefrontHUD).GetField(name, Fields).GetValue(game.HUD);
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }
        void Click(UnityEngine.UI.Button button)
        {
            if (!button.gameObject.activeInHierarchy || !button.interactable) throw new Exception("Button unavailable: " + button.name);
            button.onClick.Invoke(); game.HUD.Refresh();
        }
        void Quiet()
        {
            foreach (var army in game.Sim.Armies)
            { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var unit in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
        }
        void AdvanceUntil(Func<bool> condition, float limit)
        {
            for (int i = 0; i < Mathf.CeilToInt(limit / .05f) && !condition() && !game.Sim.Finished; i++) game.Sim.Step(.05f);
            game.HUD.Refresh();
        }
        BridgeRoute Opposite() => game.Sim.Bridge.CurrentRoute == BridgeRoute.North ? BridgeRoute.South : BridgeRoute.North;
        try
        {
            game.enabled = false; game.ReducedMotion = true;
            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.RiverCrossing);
            var entry = Field<UnityEngine.UI.Button>("bridgeMissionButton");
            Check(entry.gameObject.activeInHierarchy && entry.interactable && entry.GetComponentInChildren<TextMeshProUGUI>().text.Contains("Try mission"),
                "Title exposes a labeled Bridge Gambit mission action");
            Click(entry); Quiet();
            Check(!game.InMenu && game.Sim.MissionActive && !game.TutorialActive && !game.Spectator,
                "The mission action starts the actual two-sided bridge rules");
            Check(game.SelectedBattlefield == BattlefieldId.RiverCrossing && TablefrontBattlefields.All.Length == 4,
                "Mission entry preserves the four-map skirmish choice");
            var cards = Field<RectTransform[]>("scoreCards");
            Check(cards[0].gameObject.activeInHierarchy && cards[1].gameObject.activeInHierarchy && !cards[2].gameObject.activeInHierarchy,
                "Mission header shows only its two active armies");
            var classes = Field<UnityEngine.UI.Button[]>("squadButtons");
            var recruits = Field<UnityEngine.UI.Button[]>("recruit");
            Check(classes.Take(3).All(b => b.gameObject.activeInHierarchy) && recruits.Take(3).All(b => b.gameObject.activeInHierarchy) &&
                classes.Skip(3).All(b => !b.gameObject.activeInHierarchy) && recruits.Skip(3).All(b => !b.gameObject.activeInHierarchy),
                "Mission presents Scouts, Rifles and Tanks without unsupported support-unit buttons");
            Check(Field<RectTransform>("bridgePanel").gameObject.activeInHierarchy && Field<TextMeshProUGUI>("bridgeGoal").text.Contains("45s") && Field<TextMeshProUGUI>("bridgeGoal").text.Contains("uncontested"),
                "The mission objective discloses uncontested holding beside the bridge controls");
            game.Selected.Clear(); game.HUD.Refresh();
            var change = Field<UnityEngine.UI.Button>("bridgeButton");
            Check(change.interactable, "Bridge control works without selecting a troop");
            int initial = game.Sim.Bridge.CommandsRemaining(0);
            BridgeRoute requested = Opposite();
            Click(change);
            var feedback = change.GetComponent<TablefrontButtonFeedback>();
            feedback.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Check(game.Sim.Bridge.CommandsRemaining(0) == initial - 1 && game.Sim.Bridge.Phase == BridgePhase.Warning && game.Sim.Bridge.TargetRoute == requested,
                "The visible crossing button spends one real command and starts the warned route change");
            Check(!change.interactable && Field<TextMeshProUGUI>("bridgeState").text.Contains("YOU") && Field<TextMeshProUGUI>("bridgeDetail").text.Contains("Riders") &&
                Field<TextMeshProUGUI>("bridgeButtonText").color == TablePalette.Muted,
                "Warning feedback identifies the actor, protects riders and keeps the disabled label readable even on hover");
            Check(Field<TextMeshProUGUI>("bridgeBudget").text.Contains("Your commands 2") && Field<TextMeshProUGUI>("bridgeBudget").text.Contains("Enemy 3"),
                "Both sides' remaining command budgets are disclosed");
            game.TogglePause(); game.HUD.Refresh();
            Check(!change.interactable && !game.RequestBridge(Opposite()) && game.Sim.Bridge.CommandsRemaining(0) == initial - 1,
                "Pause disables bridge commands without spending another charge");
            game.TogglePause();
            AdvanceUntil(() => game.Sim.Bridge.Phase == BridgePhase.Moving, 5);
            Check(game.Sim.Bridge.Phase == BridgePhase.Moving && Field<TextMeshProUGUI>("bridgeState").text.Contains("moving") && !change.interactable,
                "The moving phase is visible and remains locked");
            AdvanceUntil(() => game.Sim.Bridge.Phase == BridgePhase.Stable, 5);
            Check(game.Sim.Bridge.CurrentRoute == requested && game.Sim.Bridge.CooldownRemaining(0) > 0 && !change.interactable,
                "Docking updates the open crossing while the cooldown remains enforced");
            AdvanceUntil(() => game.Sim.Bridge.CooldownRemaining(0) <= 0, 25);
            feedback.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Check(change.interactable && Field<TextMeshProUGUI>("bridgeButtonText").text.Contains("Open ") && Field<TextMeshProUGUI>("bridgeButtonText").text.Contains("R") &&
                Field<TextMeshProUGUI>("bridgeButtonText").color == TablePalette.Ink,
                "The crossing button unlocks with readable ink on gold and advertises the R shortcut");
            for (int i = 0; i < 2; i++)
            {
                Click(change);
                AdvanceUntil(() => game.Sim.Bridge.Phase == BridgePhase.Stable, 7);
                AdvanceUntil(() => game.Sim.Bridge.CooldownRemaining(0) <= 0, 25);
            }
            Check(game.Sim.Bridge.CommandsRemaining(0) == 0 && !change.interactable && Field<TextMeshProUGUI>("bridgeButtonText").text.Contains("No bridge commands"),
                "Exhausted bridge commands have an explicit disabled state");
            Check(game.Sim.TryRequestBridge(1, Opposite()), "The enemy can request the same crossing change under the shared rules");
            game.HUD.Refresh();
            Check(Field<TextMeshProUGUI>("bridgeState").text.Contains("EMBER") && Field<TextMeshProUGUI>("bridgeBudget").text.Contains("Enemy 2"),
                "Enemy bridge commands disclose their actor and actual remaining budget");

            game.Rematch(); Quiet(); game.HUD.Refresh();
            Check(game.Sim.MissionActive && game.Sim.Bridge.CommandsRemaining(0) == 3 && game.Sim.Bridge.CommandsRemaining(1) == 3,
                "Mission rematch restores both command budgets and keeps the mission");
            game.ToggleHelp(); game.HUD.Refresh();
            Check(Field<TextMeshProUGUI>("helpIntro").text.Contains("45 seconds") && Field<TextMeshProUGUI>("helpIntro").text.Contains("reset the timer") && !Field<TextMeshProUGUI>("helpIntro").text.Contains("350") &&
                Field<TextMeshProUGUI>("helpFooter").text.Contains("Water blocks") && !Field<TextMeshProUGUI>("helpFooter").text.Contains("Gunners") && !change.interactable,
                "Mission Help explains the Depot objective and blocked water without skirmish-score or unsupported-unit instructions");
            game.ToggleHelp(); game.HUD.Refresh();
            game.Sim.Points[1].Owner = 0;
            game.Sim.Points[1].CapturingTeam = 0; game.Sim.Points[1].CaptureProgress = 1;
            var holding = game.Sim.Units.First(u => u.Alive && u.Team == 0 && u.Kind == UnitKind.Scout);
            holding.Position = holding.Destination = game.Sim.Points[1].Position; holding.HasOrder = false;
            for (int i = 0; i < 40; i++) game.Sim.Step(.05f);
            game.HUD.Refresh();
            Check(game.Sim.MissionHoldSeconds(0) > 0 && Field<TextMeshProUGUI[]>("scoreText")[0].text.Contains("/45s") && Field<RectTransform[]>("scoreFill")[0].anchorMax.x > 0,
                "Actual enemy-Depot hold progress drives the mission timer and progress bar");
            game.Sim.Finished = true; game.Sim.Winner = 0; game.HUD.Refresh();
            Check(!Field<UnityEngine.UI.Button>("nextBattlefieldButton").gameObject.activeInHierarchy && Field<TextMeshProUGUI>("modalTitle").text == "Mission accomplished",
                "Mission results offer retry instead of silently entering a skirmish level");
            Click(Field<UnityEngine.UI.Button>("restartButton")); Quiet();
            Check(game.Sim.MissionActive && !game.Sim.Finished && game.Sim.Bridge.CommandsRemaining(0) == 3,
                "The mission results retry button starts fresh bridge rules");
            await Task.Delay(180);
            Canvas.ForceUpdateCanvases();
            foreach (var text in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
            if (Screen.width >= 1024 && Screen.height >= 600)
                Check(!game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.isTextOverflowing),
                    "Active mission labels fit the current Game view without TMP overflow");
            game.ReturnToMenu(); game.StartBattle(false); game.HUD.Refresh();
            Check(!game.Sim.MissionActive && !Field<RectTransform>("bridgePanel").gameObject.activeInHierarchy && classes.All(b => b.gameObject.activeInHierarchy) && cards[2].gameObject.activeInHierarchy,
                "Returning to skirmish restores its five units and three-army HUD");
            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/bridge-hud-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            game.ReturnToMenu(); game.SetBattlefield(originalMap);
            game.enabled = enabled; game.ReducedMotion = reduced;
            if (hadMap) PlayerPrefs.SetInt("Tablefront.Battlefield", originalPreference); else PlayerPrefs.DeleteKey("Tablefront.Battlefield");
            PlayerPrefs.Save();
        }
    }
}
