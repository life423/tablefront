using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using TMPro;
using UnityEngine;

// Agent-owned Editor verification only. No native input and no shipped test components.
public static class VerifyExpandedHUD
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string BattlefieldKey = "Tablefront.Battlefield";

    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        var previousBattlefield = game.SelectedBattlefield;
        bool hadBattlefield = PlayerPrefs.HasKey(BattlefieldKey);
        int previousPreference = PlayerPrefs.GetInt(BattlefieldKey, 0);
        bool previousReduced = game.ReducedMotion;
        T Field<T>(string name) => (T)typeof(TablefrontHUD).GetField(name, Fields).GetValue(game.HUD);
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL " + description);
            passed.Add("PASS " + description);
        }
        UnityEngine.UI.Button NamedButton(string name) => game.HUD.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == name);
        void Click(UnityEngine.UI.Button button)
        {
            if (!button.gameObject.activeInHierarchy || !button.interactable) throw new Exception("UI action not available: " + button.name);
            button.onClick.Invoke(); game.HUD.Refresh();
        }
        int OfKind(UnitKind kind) => game.Sim.Units.Count(u => u.Alive && u.Team == 0 && u.Kind == kind);

        try
        {
            game.ReducedMotion = true;
            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.Greenfield); game.HUD.Refresh();
            var next = NamedButton("Next");
            for (int i = 1; i <= TablefrontBattlefields.All.Length; i++)
            {
                Click(next);
                var expected = TablefrontBattlefields.All[i % TablefrontBattlefields.All.Length];
                Check(game.SelectedBattlefield == expected.Id && game.Sim.Battlefield.Id == expected.Id &&
                    Field<TextMeshProUGUI>("battlefieldName").text == expected.Name &&
                    Field<TextMeshProUGUI>("battlefieldDescription").text == expected.Description,
                    "Next battlefield changes the selected rules, preview name and description to " + expected.Name);
            }
            Click(NamedButton("Previous"));
            Check(game.SelectedBattlefield == BattlefieldId.RiverCrossing,
                "Previous battlefield wraps from the first map to the final map");
            Click(Field<UnityEngine.UI.Button>("playButton"));
            Check(!game.InMenu && !game.TutorialActive && game.Sim.ExpandedRoster && game.Sim.Battlefield.Id == BattlefieldId.RiverCrossing,
                "Play vs AI starts the selected map with the expanded roster");

            var recruits = Field<UnityEngine.UI.Button[]>("recruit");
            var classes = Field<UnityEngine.UI.Button[]>("squadButtons");
            Check(recruits.Length == 5 && classes.Length == 5 && recruits.All(b => b.gameObject.activeInHierarchy) && classes.All(b => b.gameObject.activeInHierarchy),
                "Normal battle exposes all five labeled recruitment and class-selection actions");
            foreach (UnitKind kind in new[] { UnitKind.Gunner, UnitKind.Artillery })
            {
                game.Sim.Armies[0].Credits = 999; game.Sim.Armies[0].RecruitCooldown = 0; game.HUD.Refresh();
                int count = OfKind(kind); float credits = game.Sim.Armies[0].Credits;
                Click(recruits[(int)kind]);
                Check(OfKind(kind) == count + 1 && Mathf.Abs(game.Sim.Armies[0].Credits - (credits - BattleSimulation.Cost(kind))) < .01f,
                    "Recruit " + kind + " button deploys a real paid reinforcement");
                Click(classes[(int)kind]);
                Check(game.Selected.Count == OfKind(kind) && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.Kind == kind && u.Team == 0),
                    kind + " class button selects its own units without recruiting");
            }

            Check(Field<TextMeshProUGUI>("roleText").text.Contains("cannot fire at close targets"),
                "Selecting artillery explains its firing dead zone before the player gives an order");
            var coveredRifle = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Rifle);
            coveredRifle.Position = coveredRifle.Destination = game.Sim.CoverZones[0].Position;
            coveredRifle.HasOrder = false;
            game.Selected.Clear(); game.Selected.Add(coveredRifle.Id); game.HUD.Refresh();
            Check(Field<TextMeshProUGUI>("roleText").text == "1 rifle protected by cover",
                "A single protected rifle uses grammatical singular feedback");
            Click(Field<UnityEngine.UI.Button>("allArmyButton"));
            Check(game.Selected.Count == game.Sim.UnitCount(0), "Select whole army includes the new support units");
            game.IssueOrder(new Vector2(4, -11));
            Click(Field<UnityEngine.UI.Button>("holdButton"));
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.HoldPosition),
                "Visible Hold action applies the real hold-position order to the entire selection");
            Click(Field<UnityEngine.UI.Button>("attackMoveButton"));
            Check(game.AttackMoveArmed && Field<UnityEngine.UI.Button>("attackMoveButton").colors.normalColor == TablePalette.Gold,
                "Visible Attack-move action arms the order and displays the active state");
            Click(Field<UnityEngine.UI.Button>("stopButton"));
            Check(!game.AttackMoveArmed && game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => !u.HasOrder && !u.HoldPosition),
                "Visible Stop cancels the armed order and holds no stale movement order");
            game.TogglePause(); game.HUD.Refresh();
            Check(!Field<UnityEngine.UI.Button>("stopButton").interactable && !Field<UnityEngine.UI.Button>("holdButton").interactable &&
                !Field<UnityEngine.UI.Button>("attackMoveButton").interactable && recruits.All(b => !b.interactable),
                "Paused battle disables command and recruitment buttons");
            game.TogglePause(); game.HUD.Refresh();

            game.StartTutorial(); game.HUD.Refresh();
            Check(game.Sim.Battlefield.Id == BattlefieldId.Greenfield && !game.Sim.ExpandedRoster && game.SelectedBattlefield == BattlefieldId.RiverCrossing,
                "Tutorial uses its original teaching map without overwriting the player's battlefield choice");
            Check(classes.Take(3).All(b => b.gameObject.activeInHierarchy) && recruits.Take(3).All(b => b.gameObject.activeInHierarchy) &&
                classes.Skip(3).All(b => !b.gameObject.activeInHierarchy) && recruits.Skip(3).All(b => !b.gameObject.activeInHierarchy),
                "Tutorial progressively presents its original three unit classes");
            game.SkipTutorial(); game.HUD.Refresh();
            Check(game.InMenu && game.Sim.Battlefield.Id == BattlefieldId.RiverCrossing,
                "Exiting the tutorial restores the selected battlefield preview");

            game.StartBattle(false);
            game.Sim.Finished = true; game.Sim.Winner = 0; game.HUD.Refresh();
            Click(Field<UnityEngine.UI.Button>("nextBattlefieldButton"));
            Check(!game.Sim.Finished && !game.InMenu && game.Sim.Battlefield.Id == BattlefieldId.Greenfield,
                "Results Next battlefield starts a fresh battle and wraps through the level roster");

            await Task.Delay(180);
            Canvas.ForceUpdateCanvases();
            foreach (var text in game.HUD.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
            if (Screen.width >= 1024 && Screen.height >= 600)
            {
                var overflow = game.HUD.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing).Select(t => t.text).ToArray();
                Check(overflow.Length == 0, "Active expanded battle text fits the current " + Screen.width + "x" + Screen.height + " Game view: " + string.Join(" | ", overflow));
            }
            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/expanded-hud-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            game.ReturnToMenu(); game.SetBattlefield(previousBattlefield);
            if (hadBattlefield) PlayerPrefs.SetInt(BattlefieldKey, previousPreference); else PlayerPrefs.DeleteKey(BattlefieldKey);
            game.ReducedMotion = previousReduced; PlayerPrefs.Save();
        }
    }
}
