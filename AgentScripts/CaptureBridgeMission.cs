using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Editor-only capture setup. All bridge poses and the rider come from real mission rules.
public static class CaptureBridgeMission
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static async Task<string> Prepare(string shot)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront in Play Mode first.");
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true);
        game.StartBridgeMission();
        var sim = game.Sim;
        foreach (var army in sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
        foreach (var unit in sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
        sim.UseExternalAI = true;
        int riderId = -1;
        if (shot == "moving" || shot == "rider")
        {
            var rider = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            var destination = new Vector2(-1.8f, -2.4f);
            sim.OrderMove(new[] { rider.Id }, destination, false);
            for (int i = 0; i < 1200 && Vector2.Distance(rider.Position, destination) > .35f; i++) sim.Step(.05f);
            if (Vector2.Distance(rider.Position, destination) > .35f) throw new Exception("Normal routing did not reach the bridge.");
            riderId = rider.Id; game.Selected.Clear(); game.Selected.Add(rider.Id);
        }
        if (shot == "warning" || shot == "moving" || shot == "rider" || shot == "south" || shot == "enemy-warning")
        {
            if (!sim.TryRequestBridge(shot == "enemy-warning" ? 1 : 0, BridgeRoute.South)) throw new Exception("Bridge request rejected.");
            if (shot == "moving" || shot == "rider") { sim.Step(2); sim.Step(2); }
            else if (shot == "south")
                for (int i = 0; i < 120 && (sim.Bridge.Phase != BridgePhase.Stable || sim.Bridge.CurrentRoute != BridgeRoute.South); i++) sim.Step(.05f);
        }
        else if (shot != "opening" && shot != "north") throw new ArgumentException("Use opening, north, warning, moving, rider, south or enemy-warning.");
        game.enabled = false;
        if (shot != "opening")
        {
            typeof(TablefrontGame).GetField("wantedFocus", Fields).SetValue(game, new Vector3(0, 0, -1));
            typeof(TablefrontGame).GetField("wantedDistance", Fields).SetValue(game, shot == "rider" ? 32f : 53f);
            typeof(TablefrontGame).GetField("wantedYaw", Fields).SetValue(game, 0f);
        }
        typeof(TablefrontGame).GetMethod("UpdateCamera", Fields).Invoke(game, new object[] { 1f, true });
        bool reduced = game.World.ReducedMotion; game.World.ReducedMotion = true;
        game.World.Render(sim, game.Selected); game.World.ReducedMotion = reduced;
        game.HUD.Refresh();
        await Task.Delay(180); Canvas.ForceUpdateCanvases();
        foreach (var label in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
        var overflow = game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.text.Replace("\n", " / "));
        return "shot=" + shot + "; phase=" + sim.Bridge.Phase + "; angle=" + sim.Bridge.AngleDegrees + "; rider=" + riderId +
            "; size=" + Screen.width + "x" + Screen.height + "; overflowing=" + string.Join(" | ", overflow);
    }
    public static string Restore()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true); game.ReturnToMenu();
        return "Normal game/world update loops and title restored; no map preference changed.";
    }
}
