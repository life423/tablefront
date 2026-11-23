using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// QA-only scene setup; compiled in memory by Pipeline, never shipped with the game.
public static class CaptureTactical
{
    public static async Task<string> Prepare(string state)
    {
        var g = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!g) throw new Exception("Open the Tablefront scene in Play Mode.");
        g.ReturnToMenu();
        if (state != "title")
        {
            if (state.StartsWith("tutorial"))
            {
                g.StartTutorial();
                if (state == "tutorial-done")
                {
                    g.SelectAll(); g.SendMessage("Update"); g.IssueOrder(g.Sim.Points[0].Position); g.SendMessage("Update");
                    for (int i = 0; i < 440 && g.Sim.OwnedPoints(0) == 0; i++) g.Sim.Step(.05f);
                    g.SendMessage("Update"); g.Recruit(UnitKind.Rifle); g.SendMessage("Update");
                }
            }
            else
            {
                g.StartBattle(false);
                foreach (var army in g.Sim.Armies) army.IsAI = false;
                g.RecallGroup(1); g.IssueOrder(g.Sim.CoverZones[0].Position);
                for (int i = 0; i < 130; i++) g.Sim.Step(.05f);
                if (state == "assignment")
                {
                    g.HUD.Refresh();
                    g.HUD.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Assign selected…").onClick.Invoke();
                }
                if (state == "alert")
                {
                    g.Sim.Armies[0].BaseHealth -= 100; await Task.Delay(100);
                }
                if (state == "results" || state == "draw")
                {
                    if (state == "draw") foreach (var army in g.Sim.Armies) army.BaseHealth = 0;
                    else g.Sim.Armies[0].Score = BattleSimulation.VictoryScore;
                    g.Sim.Step(.05f);
                }
                if (state == "pause") g.TogglePause();
                if (state == "help") g.ToggleHelp();
                if (state == "busy")
                {
                    var tank = g.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Tank);
                    var rifle = g.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Rifle);
                    rifle.Position = g.Sim.CoverZones[0].Position; rifle.HasOrder = false;
                    tank.Position = rifle.Position + Vector2.right * 2.5f;
                    g.Selected.Clear(); g.Selected.Add(rifle.Id); g.Selected.Add(tank.Id);
                    typeof(TablefrontGame).GetField("wantedFocus", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g, new Vector3(rifle.Position.x, 0, rifle.Position.y));
                    typeof(TablefrontGame).GetField("wantedDistance", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g, 30f);
                    g.World.Render(g.Sim, g.Selected);
                    g.World.PlayEvents(new[] { new CombatEvent { ShooterId=tank.Id, WeaponKind=UnitKind.Tank, Heavy=true, Team=0, From=tank.Position, To=tank.Position+Vector2.up*5 } });
                }
            }
        }
        g.World.Render(g.Sim, g.Selected); g.HUD.Refresh();
        await Task.Delay(160);
        Canvas.ForceUpdateCanvases();
        var overflow = g.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t=>t.text.Replace("\n"," / ")).ToArray();
        return "state="+state+"; size="+Screen.width+"x"+Screen.height+"; frame="+Time.frameCount+"; elapsed="+g.Sim.Elapsed+"; overflowing="+string.Join(" | ",overflow);
    }
}
