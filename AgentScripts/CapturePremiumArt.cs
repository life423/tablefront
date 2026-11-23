using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Editor-only composition helper. Never included in the Windows player.
public static class CapturePremiumArt
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static async Task<string> Prepare(string shot)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open the Tablefront scene in Play Mode.");
        game.enabled = true; game.HUD.gameObject.SetActive(true);
        game.World.enabled = true;
        if (shot == "combat")
        {
            game.StartBattle(true); game.enabled = false;
            int step = 0;
            for (; step < 3600 && !game.Sim.Finished; step++)
            { game.Sim.Step(.05f); if (game.Sim.Events.Count >= 4) break; }
            if (game.Sim.Events.Count < 4) throw new Exception("No busy combat moment found in the normal simulation.");
            Vector2 engagement = Vector2.zero;
            foreach (var action in game.Sim.Events) engagement += action.To;
            engagement /= game.Sim.Events.Count;
            typeof(TablefrontGame).GetField("wantedFocus", Flags).SetValue(game, new Vector3(engagement.x, 0, engagement.y));
            typeof(TablefrontGame).GetField("wantedDistance", Flags).SetValue(game, 44f);
            typeof(TablefrontGame).GetField("wantedYaw", Flags).SetValue(game, 0f);
            typeof(TablefrontGame).GetMethod("UpdateCamera", Flags).Invoke(game, new object[] { 1f, true });
            bool motion = game.World.ReducedMotion; game.World.ReducedMotion = true;
            game.World.Render(game.Sim, game.Selected); game.World.ReducedMotion = motion;
            game.World.PlayEvents(game.Sim.Events); game.World.enabled = false;
            game.HUD.Refresh();
            await Task.Delay(200);
            return "shot=combat; actualElapsed=" + game.Sim.Elapsed + "; resolvedCombatEvents=" + game.Sim.Events.Count + "; liveUnits=" + game.Sim.Units.Count + "; frozenForStillCapture=true";
        }
        game.StartTutorial();
        var positions = new[] { new Vector2(-4, -10), new Vector2(-2.3f, -10.2f), new Vector2(-.5f, -10), new Vector2(1.1f, -10), new Vector2(2.6f, -10), new Vector2(4.7f, -10) };
        int i = 0;
        foreach (var unit in game.Sim.Units.Where(u => u.Team == 0))
        { unit.Position = unit.Destination = positions[i++]; unit.Heading = 155; unit.HasOrder = false; }
        Vector3 centre = new Vector3(0, .5f, -10); float distance = 15;
        if (shot == "tank" || shot == "rifles" || shot == "scout")
        {
            var kind = shot == "tank" ? UnitKind.Tank : shot == "rifles" ? UnitKind.Rifle : UnitKind.Scout;
            var unit = game.Sim.Units.First(u => u.Team == 0 && u.Kind == kind);
            centre = new Vector3(unit.Position.x, .65f, unit.Position.y); distance = shot == "rifles" ? 4.7f : 5.5f;
        }
        else if (shot == "hq") { centre = new Vector3(0, 1.1f, -21); distance = 11; }
        else if (shot == "terrain") { centre = new Vector3(-18, .7f, -14); distance = 14; }
        else if (shot != "lineup") throw new ArgumentException("Use lineup, tank, rifles, scout, hq or terrain.");
        game.enabled = false;
        typeof(TablefrontGame).GetField("wantedFocus", Flags).SetValue(game, centre);
        typeof(TablefrontGame).GetField("wantedDistance", Flags).SetValue(game, distance);
        typeof(TablefrontGame).GetField("wantedYaw", Flags).SetValue(game, -24f);
        typeof(TablefrontGame).GetMethod("UpdateCamera", Flags).Invoke(game, new object[] { 1f, true });
        bool worldMotion = game.World.ReducedMotion;
        game.World.ReducedMotion = true;
        game.World.Render(game.Sim, game.Selected);
        game.World.ReducedMotion = worldMotion;
        game.HUD.gameObject.SetActive(false);
        foreach (var label in game.World.GetComponentsInChildren<TextMeshPro>()) label.gameObject.SetActive(false);
        await Task.Delay(300);
        return "shot=" + shot + "; screen=" + Screen.width + "x" + Screen.height + "; frame=" + Time.frameCount + "; cameraDistance=" + distance;
    }

    public static string Restore()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true); game.ReturnToMenu();
        return "Normal game updates and HUD restored; QA lineup discarded.";
    }
}
