using System;
using System.Linq;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// QA setup only. These are live rule-driven tutorial states, not shipped scene assets.
public static class CaptureControlsTutorial
{
    public static async Task<string> Prepare(string state)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open the Tablefront scene in Play Mode first.");
        void Tick() { game.SendMessage("Update"); }
        void Advance(int expected, float maximumSeconds)
        {
            int cap = Mathf.CeilToInt(maximumSeconds / .05f);
            for (int i = 0; i < cap && game.TutorialStep < expected; i++) { game.Sim.Step(.05f); Tick(); }
            if (game.TutorialStep != expected) throw new Exception("Tutorial setup expected step " + expected + ", got " + game.TutorialStep);
        }

        game.ReturnToMenu();
        if (state.StartsWith("tutorial"))
        {
            if (!int.TryParse(state.Substring("tutorial".Length), out int step) || step < 0 || step > 6)
                throw new ArgumentException("Use tutorial0 through tutorial6.");
            game.StartTutorial();
            if (step >= 1) { game.SelectAll(); Tick(); }
            if (step >= 2) { game.IssueOrder(game.TutorialTargetPosition); Advance(2, 30); }
            if (step >= 3) { game.IssueOrder(game.TutorialTargetPosition); Advance(3, 30); }
            if (step >= 4) { game.Recruit(UnitKind.Rifle); Tick(); Advance(4, 1); }
            if (step >= 5) { game.SelectAll(); game.IssueOrder(game.TutorialTargetPosition, game.TutorialTargetId); Advance(5, 60); }
            if (step >= 6) { game.SelectKind(UnitKind.Rifle); game.IssueOrder(game.TutorialTargetPosition); Advance(6, 45); }
            game.RecenterTutorial();
        }
        else if (state == "battle")
        {
            game.StartBattle(false); game.SelectKind(UnitKind.Rifle);
            game.IssueOrder(game.Sim.Points[0].Position);
        }
        else if (state == "paused" || state == "results" || state == "artillery")
        {
            game.StartBattle(false);
            if (state == "paused") game.TogglePause();
            else if (state == "results") { game.Sim.Finished = true; game.Sim.Winner = 0; }
            else
            {
                foreach (var army in game.Sim.Armies) army.IsAI = false;
                game.Sim.Armies[0].Credits = 300; game.Sim.Armies[0].RecruitCooldown = 0;
                game.Recruit(UnitKind.Artillery); game.SelectKind(UnitKind.Artillery);
            }
        }
        else if (state == "help" || state == "shortcuts")
        {
            game.ToggleHelp();
            game.HUD.Refresh();
            if (state == "shortcuts")
                game.HUD.GetComponentsInChildren<Button>(true).First(b => b.name == "Keyboard shortcuts").onClick.Invoke();
        }
        else if (state != "title") throw new ArgumentException("Unknown capture state: " + state);

        game.World.Render(game.Sim, game.Selected); game.HUD.Refresh();
        await Task.Delay(180);
        Canvas.ForceUpdateCanvases();
        foreach (var label in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
        var overflow = game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.text.Replace("\n", " / ")).ToArray();
        return "state=" + state + "; step=" + game.TutorialStep + "; practice=" + game.Sim.PracticeMode + "; size=" + Screen.width + "x" + Screen.height
            + "; frame=" + Time.frameCount + "; overflowing=" + string.Join(" | ", overflow);
    }
}
