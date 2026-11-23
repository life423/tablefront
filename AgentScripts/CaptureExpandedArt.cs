using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Editor-only still composition. Never ships, and never sends native gameplay input.
public static class CaptureExpandedArt
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string SavedMapKey = "Tablefront.QA.PreviousBattlefield";

    public static async Task<string> Prepare(string shot)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new InvalidOperationException("Open Tablefront in Play mode.");
        if (!PlayerPrefs.HasKey(SavedMapKey)) PlayerPrefs.SetInt(SavedMapKey, (int)game.SelectedBattlefield);
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true);
        game.ReturnToMenu();
        bool mapShot = Enum.TryParse(shot, true, out BattlefieldId map) && Enum.IsDefined(typeof(BattlefieldId), map);
        game.SetBattlefield(mapShot ? map : BattlefieldId.Greenfield);
        game.StartBattle(shot == "combat");
        Vector3 centre = new Vector3(0, 0, -1.5f); float distance = 64, yaw = 0;
        bool showUI = true;
        if (shot == "lineup" || shot == "gunner" || shot == "artillery")
        {
            foreach (var army in game.Sim.Armies) army.IsAI = false;
            var home = game.Sim.Armies[0];
            foreach (var kind in new[] { UnitKind.Gunner, UnitKind.Artillery })
            { home.Credits = 999; home.RecruitCooldown = 0; game.Sim.Recruit(0, kind); }
            var display = BattleSimulation.UnitKinds.Select(kind => game.Sim.Units.First(u => u.Team == 0 && u.Kind == kind)).ToArray();
            var keep = display.Select(u => u.Id).ToHashSet();
            game.Sim.Units.RemoveAll(u => !keep.Contains(u.Id));
            for (int i = 0; i < display.Length; i++)
            {
                var unit = display[i]; unit.Position = unit.Destination = new Vector2(-5.6f + i * 2.8f, -8);
                unit.HasOrder = false; unit.Heading = 165;
            }
            centre = new Vector3(0, .5f, -8); distance = 19; yaw = -18; showUI = false;
            if (shot != "lineup")
            {
                var unit = display.First(u => u.Kind == (shot == "gunner" ? UnitKind.Gunner : UnitKind.Artillery));
                centre = new Vector3(unit.Position.x, .7f, unit.Position.y); distance = shot == "gunner" ? 5.6f : 7;
            }
            game.World.ClearBattle();
        }
        else if (shot == "combat")
        {
            game.enabled = false;
            for (int step = 0; step < 7200 && !game.Sim.Finished; step++)
            {
                game.Sim.Step(.05f);
                if (game.Sim.Events.Any(e => e.WeaponKind == UnitKind.Gunner || e.WeaponKind == UnitKind.Artillery)) break;
            }
            if (game.Sim.Events.Count == 0) throw new InvalidOperationException("No actual expanded combat moment found.");
            Vector2 p = Vector2.zero; foreach (var e in game.Sim.Events) p += e.To; p /= game.Sim.Events.Count;
            centre = new Vector3(p.x, 0, p.y); distance = 40;
        }
        else if (!mapShot) throw new ArgumentException("Use Greenfield, Ridgeway, DustBasin, RiverCrossing, lineup, gunner, artillery or combat.");

        game.enabled = false;
        typeof(TablefrontGame).GetField("wantedFocus", Flags).SetValue(game, centre);
        typeof(TablefrontGame).GetField("wantedDistance", Flags).SetValue(game, distance);
        typeof(TablefrontGame).GetField("wantedYaw", Flags).SetValue(game, yaw);
        typeof(TablefrontGame).GetMethod("UpdateCamera", Flags).Invoke(game, new object[] { 1f, true });
        bool motion = game.World.ReducedMotion; game.World.ReducedMotion = true;
        game.World.Render(game.Sim, game.Selected); game.World.ReducedMotion = motion;
        if (shot == "combat") { game.World.PlayEvents(game.Sim.Events); game.World.enabled = false; }
        game.HUD.Refresh(); game.HUD.gameObject.SetActive(showUI);
        if (!showUI) foreach (var label in game.World.GetComponentsInChildren<TextMeshPro>()) label.gameObject.SetActive(false);
        await Task.Delay(220);
        return "shot=" + shot + "; map=" + game.Sim.Battlefield.Name + "; size=" + Screen.width + "x" + Screen.height +
            "; frame=" + Time.frameCount + "; units=" + game.Sim.Units.Count + "; combatEvents=" + game.Sim.Events.Count;
    }

    public static string Restore()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        game.enabled = true; game.World.enabled = true; game.HUD.gameObject.SetActive(true);
        game.ReturnToMenu();
        if (PlayerPrefs.HasKey(SavedMapKey))
        {
            game.SetBattlefield((BattlefieldId)PlayerPrefs.GetInt(SavedMapKey));
            PlayerPrefs.DeleteKey(SavedMapKey); PlayerPrefs.Save();
        }
        return "Normal update loops, menu, map preference and HUD restored.";
    }
}
