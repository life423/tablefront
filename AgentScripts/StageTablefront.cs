using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;

public static class StageTablefront
{
    public static async Task<string> Set(string state)
    {
        var g=UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if(state=="title") g.ReturnToMenu();
        else if(state=="help") { if(!g.HelpOpen) g.ToggleHelp(); }
        else if(state=="pause") { if(g.InMenu) g.StartBattle(false); if(!g.Paused) g.TogglePause(); }
        else if(state=="result") { g.StartBattle(false); g.Sim.Armies[0].Score=BattleSimulation.VictoryScore; g.Sim.Step(.05f); }
        else
        {
            g.StartBattle(state=="spectator");
            g.TogglePause();
            if(state=="action" || state=="spectator" || state=="hud")
            {
                if(state!="hud") for(int i=0;i<1000&&!g.Sim.Finished;i++) g.Sim.Step(.05f);
                // Pause the match clock for consistent camera/UI comparisons, then hide pause modal.
                await Task.Delay(400); // Let render transforms settle to the staged simulation state.
                g.TogglePause(); g.enabled=false;
            }
            if(state=="closeup")
            {
                SetField(g,"wantedFocus",new Vector3(0,0,-17));SetField(g,"wantedDistance",26f);
                SetField(g,"focus",new Vector3(0,0,-17));SetField(g,"distance",26f);
                g.TogglePause();
            }
        }
        typeof(TablefrontGame).GetMethod("UpdateCamera",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,new object[]{1f,true});
        g.World.Render(g.Sim,g.Selected);g.HUD.Refresh();
        await Task.Delay(600);
        return state+" staged; elapsed="+g.Sim.Elapsed+" neural="+NeuralCommander.Available;
    }
    static void SetField(object o,string name,object value) => o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    public static string Restore()
    { var g=UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();g.enabled=true;g.ReturnToMenu();return "Restored idle title"; }
}
