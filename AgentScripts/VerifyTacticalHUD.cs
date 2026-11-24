using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Agent-owned Editor only. Exercises real UI callbacks and shared rules; no native input.
public static class VerifyTacticalHUD
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Enter Tablefront Play Mode first.");
        bool enabled = game.enabled, reduced = game.ReducedMotion;
        var map = game.SelectedBattlefield;
        bool hadPreference = PlayerPrefs.HasKey("Tablefront.Battlefield");
        int preference = PlayerPrefs.GetInt("Tablefront.Battlefield");
        var checks = new List<string>();
        T Field<T>(string name) => (T)typeof(TablefrontHUD).GetField(name, Fields).GetValue(game.HUD);
        void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL " + name); checks.Add("PASS " + name); }
        void Click(UnityEngine.UI.Button button)
        { if (!button.IsActive() || !button.interactable) throw new Exception("Unavailable button: " + button.name); button.onClick.Invoke(); game.HUD.Refresh(); }
        void Quiet()
        {
            game.Sim.UseExternalAI = true;
            foreach (var army in game.Sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var troop in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(troop, 99999f);
        }
        void Advance(float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) game.Sim.Step(.05f); game.HUD.Refresh(); }
        try
        {
            game.enabled = false; game.ReducedMotion = true;
            game.StartBridgeMission(); Quiet(); game.Selected.Clear(); game.HUD.Refresh();
            var move = Field<UnityEngine.UI.Button>("moveButton");
            Check(move.IsActive() && !move.interactable, "Move is a visible labeled action and needs a selection");
            Click(Field<UnityEngine.UI.Button>("allArmyButton"));
            Check(move.interactable && game.Selected.Count == 6, "Selecting the army enables movement without a keyboard shortcut");
            Click(move);
            Check(game.MoveArmed && !game.AttackMoveArmed && Field<TextMeshProUGUI>("orderText").text.Contains("destination"),
                "Move button arms a destination and explains the next action");
            Check(move.colors.normalColor == TablePalette.Gold, "Armed Move receives the established selected-action treatment");
            Check(game.IssueOrder(new Vector2(-18, -4)), "The pending movement accepts legal ground");
            Check(!game.MoveArmed, "Giving the destination exits Move mode");
            Check(game.IssueOrder(new Vector2(-20, 1), append: true), "An append request queues the next waypoint");
            game.HUD.Refresh();
            Check(game.QueuedCount == 1 && Field<TextMeshProUGUI>("roleText").text.Contains("1 order queued"),
                "One queued waypoint for six troops is described as one step rather than six clicks");
            Click(Field<UnityEngine.UI.Button>("stopButton"));
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.QueuedOrders.Count == 0 && !u.HasOrder),
                "The visible Stop action clears current and pending movement");
            Click(Field<UnityEngine.UI.Button>("attackMoveButton"));
            Check(game.AttackMoveArmed && !game.MoveArmed, "Attack-move and Move are mutually exclusive visible actions");
            Click(move); Check(game.MoveArmed && !game.AttackMoveArmed, "Move replaces an armed attack-move without issuing an unwanted order");
            game.CancelPlanning(); game.Selected.Clear(); game.HUD.Refresh();
            var crossing = Field<UnityEngine.UI.Button>("bridgeButton");
            Check(crossing.interactable && Field<TextMeshProUGUI>("bridgeDetail").text.Contains("flank"),
                "Bridge control stays independent of troop selection and names the alternative approach");
            Check(Field<TextMeshProUGUI>("bridgeBudget").text.Contains("Hold ROTARY"), "Unowned ROTARY explains the rechargeable bridge resource");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(crossing.gameObject);
            game.HUD.Refresh(); game.World.Render(game.Sim, game.Selected);
            var bridgeArt = (TablefrontBridgeArt)typeof(TablefrontWorld).GetField("bridgeArt", Fields).GetValue(game.World);
            Check(bridgeArt.Preview.gameObject.activeSelf && Field<TextMeshProUGUI>("bridgeState").text.Contains("preview South"),
                "Focusing the bridge button previews its other landing before committing");
            Check(game.Sim.Bridge.Phase == BridgePhase.Stable && game.Sim.Bridge.CommandsRemaining(0) == 3,
                "Inspecting the alternate crossing spends no command and does not move the bridge");
            Click(crossing); Advance(5.2f);
            var rotary = game.Sim.Points[2]; rotary.Owner = rotary.CapturingTeam = 0; rotary.CaptureProgress = 1;
            var holder = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            holder.Position = holder.Destination = Vector2.zero; game.Sim.OrderStop(new[] { holder.Id }, true);
            Advance(2);
            Check(game.Sim.BridgeRechargeSeconds(0) > 0 && Field<TextMeshProUGUI>("bridgeBudget").text.Contains("ROTARY: +1 in"),
                "The real uncontested ROTARY timer drives the recharge countdown");
            Advance(19);
            Check(game.Sim.Bridge.CommandsRemaining(0) == 3 && Field<TextMeshProUGUI>("bridgeBudget").text.Contains("commands full"),
                "Completing the hold restores a real command and presents the cap clearly");
            game.ToggleHelp(); game.HUD.Refresh();
            string basics = string.Join(" ", game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Select(t => t.text));
            Check(basics.Contains("Hold Shift") && basics.Contains("route and formation"), "Mouse Help explains queueing and the plan preview");
            Check(Field<TextMeshProUGUI>("helpFooter").text.Contains("stone screens block shots") && Field<TextMeshProUGUI>("helpFooter").text.Contains("20s"),
                "Mission Help explains physical sight blockers, flanking and the recharge condition");
            Check(!move.interactable && !crossing.interactable, "Help locks movement and bridge commands");
            game.ToggleHelp(); game.SelectAll(); game.TogglePause(); game.HUD.Refresh();
            Check(!move.interactable && !crossing.interactable, "Pause locks both forms of command");
            game.TogglePause(); game.SelectAll(); game.HUD.Refresh();
            await Task.Delay(180); Canvas.ForceUpdateCanvases();
            foreach (var text in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
            if (Screen.width >= 1024 && Screen.height >= 600)
                Check(!game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.isTextOverflowing), "Active tactical HUD text fits the verified Game view");
            var actions = move.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>();
            var corners = new Vector3[4]; var rectangles = new List<Rect>();
            foreach (var button in actions)
            {
                var rect = button.GetComponent<RectTransform>(); rect.GetWorldCorners(corners);
                rectangles.Add(new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y));
            }
            Check(rectangles.All(r => r.width > 0 && r.height > 0) && !rectangles.Where((r, i) => rectangles.Skip(i + 1).Any(s => r.Overlaps(s))).Any(),
                "The five labeled army actions have nonzero, nonoverlapping targets");
            game.StartBridgeMission(); Quiet();
            var attacker = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Rifle);
            var defender = game.Sim.Units.First(u => u.Team == 1 && u.Kind == UnitKind.Rifle);
            foreach (var troop in game.Sim.Units.Where(u => u != attacker && u != defender)) troop.Health = 0;
            attacker.Position = attacker.Destination = new Vector2(6, 8);
            defender.Position = defender.Destination = new Vector2(10, 7);
            game.Sim.OrderStop(new[] { attacker.Id, defender.Id }, true);
            game.Selected.Clear(); game.Selected.Add(attacker.Id);
            typeof(Troop).GetField("WeaponTimer", Fields).SetValue(attacker, 0f);
            game.Sim.Step(.001f); game.HUD.Refresh();
            Check(Field<TextMeshProUGUI>("roleText").text.Contains("Frontal cover reduced damage"),
                "An actual protected shot explains its outcome in the existing command area");
            attacker.Position = attacker.Destination = new Vector2(14, 6.8f);
            typeof(Troop).GetField("WeaponTimer", Fields).SetValue(attacker, 0f);
            game.Sim.Step(.001f); game.HUD.Refresh();
            Check(Field<TextMeshProUGUI>("roleText").text.Contains("Flank hit") && Field<TextMeshProUGUI>("roleText").color == TablePalette.Gold,
                "An actual rear shot visibly explains that the enemy cover was bypassed");
            game.Selected.Clear(); game.HUD.Refresh();
            Check(!Field<TextMeshProUGUI>("roleText").text.Contains("Flank hit"), "Combat outcome feedback stays with its selected force");
            game.ReturnToMenu(); game.StartBattle(false); game.HUD.Refresh();
            Check(!game.MissionActive && move.IsActive() && game.Sim.CoverZones.Count == 6 && !Field<RectTransform>("bridgePanel").gameObject.activeInHierarchy,
                "Ordinary skirmishes retain their original cover rules and gain the same planning controls");
            File.WriteAllLines("TestResults/tactical-hud-runtime.txt", checks);
            return checks.Count + " checks passed\n" + string.Join("\n", checks);
        }
        finally
        {
            game.ReturnToMenu(); game.SetBattlefield(map); game.enabled = enabled; game.ReducedMotion = reduced;
            if (hadPreference) PlayerPrefs.SetInt("Tablefront.Battlefield", preference); else PlayerPrefs.DeleteKey("Tablefront.Battlefield");
            PlayerPrefs.Save();
        }
    }
}
