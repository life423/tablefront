using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Editor-only capture preparation. Fixture setup is explicit; every shot, route, rotation,
// capture and recharge is evaluated by the actual game rules. Root owns all Editor capture calls.
public static class CaptureTacticalMission
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Timer(Troop troop, float value) => typeof(Troop).GetField("WeaponTimer", Fields).SetValue(troop, value);
    public static async Task<string> Prepare(string shot)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront in Play Mode.");
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true);
        game.StartBridgeMission(); var sim = game.Sim;
        sim.UseExternalAI = true;
        foreach (var army in sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
        foreach (var unit in sim.Units) Timer(unit, 99999);
        var world = game.World; bool originalReducedMotion = world.ReducedMotion; world.HoveredUnitId = -1;
        Vector3 focus = Vector3.zero; float distance = 32;
        string proof = ""; bool hitFrame = false;
        game.Selected.Clear();
        if (shot == "front-cover" || shot == "flank-hit")
        {
            // A legal two-rifle fixture isolates actual incoming-direction behavior from a full battle.
            var defender = sim.Units.First(u => u.Team == 1 && u.Kind == UnitKind.Rifle);
            var attacker = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Rifle);
            sim.Units.RemoveAll(u => u.Id != defender.Id && u.Id != attacker.Id);
            defender.Position = new Vector2(10, 7); defender.Destination = defender.Position; defender.HasOrder = false; defender.HoldPosition = true;
            if (shot == "flank-hit")
            {
                // Start from the genuine South landing, rotate through rules, then navigate around the solid wall.
                if (!sim.TryRequestBridge(0, BridgeRoute.South)) throw new Exception("South request rejected.");
                for (int i = 0; i < 102; i++) sim.Step(.05f);
                attacker.Position = new Vector2(6, -8); attacker.HasOrder = false;
                var rear = new Vector2(14, 6.8f); sim.OrderMove(new[] { attacker.Id }, rear, false);
                for (int i = 0; i < 1000 && Vector2.Distance(attacker.Position, rear) > .3f; i++) sim.Step(.05f);
                if (Vector2.Distance(attacker.Position, rear) > .3f || !sim.DestinationAllowed(attacker.Position, attacker.Kind))
                    throw new Exception("Actual South-to-rear navigation failed.");
            }
            else attacker.Position = new Vector2(6, 8);
            attacker.HoldPosition = true; attacker.HasOrder = false; attacker.AttackTargetId = defender.Id;
            if (!sim.DestinationAllowed(attacker.Position, attacker.Kind) || !sim.DestinationAllowed(defender.Position, defender.Kind))
                throw new Exception("Shot fixture is not legal mission ground.");
            Timer(attacker, 0); float health = defender.Health;
            sim.Step(.05f);
            var hit = sim.Events.FirstOrDefault(e => e.TargetId == defender.Id);
            if (hit.ShooterId != attacker.Id || hit.ProtectedHit != (shot == "front-cover") || hit.FlankedHit != (shot == "flank-hit"))
                throw new Exception("Actual shot did not match requested directional cover state.");
            game.Selected.Add(attacker.Id); world.HoveredUnitId = defender.Id;
            world.ClearBattle(); world.HoveredUnitId = defender.Id; world.ReducedMotion = true; world.Render(sim, game.Selected);
            world.ReducedMotion = false; world.PlayEvents(sim.Events); hitFrame = true;
            focus = new Vector3(10.4f, 0, 5.7f); distance = 29;
            proof = "; target=" + defender.Id + "; protected=" + hit.ProtectedHit + "; flanked=" + hit.FlankedHit + "; damage=" + (health - defender.Health);
        }
        else if (shot == "recharge" || shot == "recharged" || shot == "moving-rider")
        {
            var scout = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            var center = new Vector2(-1.2f, -1.6f); sim.OrderMove(new[] { scout.Id }, center, false);
            for (int i = 0; i < 1200 && Vector2.Distance(scout.Position, center) > .3f; i++) sim.Step(.05f);
            if (Vector2.Distance(scout.Position, center) > .3f) throw new Exception("Scout cannot reach ROTARY through actual navigation.");
            if (!sim.TryRequestBridge(0, BridgeRoute.South)) throw new Exception("Actual bridge request rejected.");
            if (shot == "moving-rider") { sim.Step(2); sim.Step(2); }
            else
            {
                for (int i = 0; i < 1000 && sim.BridgeRechargeSeconds(0) < 9.5f; i++) sim.Step(.05f);
                if (sim.Points[2].Owner != 0 || sim.BridgeRechargeSeconds(0) < 9.5f) throw new Exception("Actual uncontested ROTARY capture did not start recharge.");
                if (shot == "recharged") for (int i = 0; i < 500 && sim.Bridge.CommandsRemaining(0) < 3; i++) sim.Step(.05f);
            }
            game.Selected.Add(scout.Id); proof = "; rider=" + sim.Bridge.IsRiding(scout.Id) + "; rotaryOwner=" + sim.Points[2].Owner;
            focus = new Vector3(0, 0, -1); distance = shot == "moving-rider" ? 34 : 25;
        }
        else if (shot != "opening" && shot != "north" && shot != "planning")
            throw new ArgumentException("Use opening, north, planning, front-cover, flank-hit, recharge, recharged or moving-rider.");
        game.enabled = false;
        if (shot != "opening")
        {
            if (shot == "north" || shot == "planning") { focus = new Vector3(0, 0, -1); distance = 53; }
            typeof(TablefrontGame).GetField("wantedFocus", Fields).SetValue(game, focus);
            typeof(TablefrontGame).GetField("wantedDistance", Fields).SetValue(game, distance);
            typeof(TablefrontGame).GetField("wantedYaw", Fields).SetValue(game, 0f);
        }
        typeof(TablefrontGame).GetMethod("UpdateCamera", Fields).Invoke(game, new object[] { 1f, true });
        if (!hitFrame) world.ReducedMotion = true;
        world.Render(sim, game.Selected); world.ReducedMotion = originalReducedMotion;
        game.HUD.Refresh();
        // Stage actual keyboard focus on the real action, including its HUD explanation.
        if (shot == "planning")
        {
            var button = (UnityEngine.UI.Button)typeof(TablefrontHUD).GetField("bridgeButton", Fields).GetValue(game.HUD);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
            game.HUD.Refresh(); world.Render(sim, game.Selected);
        }
        world.enabled = false;
        await Task.Delay(180); Canvas.ForceUpdateCanvases();
        foreach (var label in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
        string overflow = string.Join(" | ", game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.text.Replace("\n", " / ")));
        return "shot=" + shot + "; phase=" + sim.Bridge.Phase + "; blueCommands=" + sim.Bridge.CommandsRemaining(0) +
            "; rechargeSeconds=" + sim.BridgeRechargeSeconds(0) + proof + "; size=" + Screen.width + "x" + Screen.height + "; overflow=" + overflow;
    }
    public static string Restore()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        game.World.SetBridgePlanning(false); game.World.HoveredUnitId = -1;
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true); game.ReturnToMenu();
        return "Normal input, world effects and title restored; no player preference changed.";
    }
}
