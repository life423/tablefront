using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;

// Editor-only regression verification. Never sends native input or ships in Assets.
public static class VerifyTutorialRecovery
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string CompletionKey = "Tablefront.TutorialCompleted";

    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        bool reduced = game.ReducedMotion;
        bool hadCompletion = PlayerPrefs.HasKey(CompletionKey);
        int oldCompletion = PlayerPrefs.GetInt(CompletionKey, 0);
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }
        void Tick() { game.SendMessage("Update"); }
        void Advance(int expected, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f) && game.TutorialStep < expected; i++)
            { game.Sim.Step(.05f); Tick(); }
            Check(game.TutorialStep == expected, "Normal tutorial progression reaches task " + (expected + 1));
        }
        Troop Target() => game.Sim.Units.FirstOrDefault(u => u.Alive && u.Team == 1 && u.Id == game.TutorialTargetId);
        void SetWeapon(Troop unit, float timer) => typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, timer);
        void SetTurret(Army army, float timer) => typeof(Army).GetField("TurretTimer", Fields).SetValue(army, timer);
        void RemoveTarget()
        {
            int targetId = game.TutorialTargetId;
            game.Sim.Units.RemoveAll(u => u.Id == targetId);
        }
        void StartAttackLesson()
        {
            game.StartTutorial();
            game.SelectAll(); Tick();
            game.IssueOrder(game.TutorialTargetPosition); Advance(2, 30);
            game.IssueOrder(game.TutorialTargetPosition); Advance(3, 35);
            game.Recruit(UnitKind.Rifle); Tick();
            Check(game.TutorialStep == 4 && Target() != null, "Attack task initially has a live marked practice target");
        }

        try
        {
            game.ReducedMotion = true;
            StartAttackLesson();

            int firstTarget = game.TutorialTargetId;
            RemoveTarget(); Tick();
            Check(game.TutorialStep == 4 && Target() != null && game.TutorialTargetId != firstTarget,
                "A missing uncommanded target is replaced without skipping the attack lesson");

            game.Sim.Armies[1].BaseHealth = 0;
            RemoveTarget(); Tick();
            Check(game.Sim.Armies[1].Alive && game.TutorialStep == 4 && Target() != null,
                "Target creation recovers a destroyed target HQ instead of failing recruitment forever");
            Check(!Target().HasOrder && (float)typeof(Troop).GetField("WeaponTimer", Fields).GetValue(Target()) > 1000,
                "Recovered target remains stationary and unable to fire");

            bool replacementCycles = true;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                int missingId = game.TutorialTargetId;
                var target = Target();
                if (target == null) { replacementCycles = false; break; }
                RemoveTarget(); Tick();
                replacementCycles &= game.TutorialStep == 4 && Target() != null && game.TutorialTargetId != missingId &&
                    game.Sim.Units.Count(u => u.Alive && u.Team == 1) == 1;
            }
            Check(replacementCycles, "Ten repeated removals of live targets recover exactly one target without hitting the army cap");

            game.SelectAll();
            Check(game.IssueOrder(game.TutorialTargetPosition, game.TutorialTargetId),
                "Recovered target accepts an explicit attack order");
            int orderedTargetId = game.TutorialTargetId;
            RemoveTarget(); Tick();
            Check(game.TutorialStep == 4 && Target() != null && game.TutorialTargetId != orderedTargetId,
                "A missing live target after an attack order is replaced rather than counted as a kill");

            foreach (var unit in game.Sim.Units.Where(u => u.Alive && u.Team == 0))
            { unit.Position = unit.Destination = new Vector2(22, 18); unit.HasOrder = false; unit.AttackTargetId = -1; }
            game.RecenterTutorial();
            await Task.Delay(180);
            var viewport = game.ViewCamera.WorldToViewportPoint(new Vector3(game.TutorialTargetPosition.x, .6f, game.TutorialTargetPosition.y));
            Check(viewport.z > 0 && viewport.x > .1f && viewport.x < .68f && viewport.y > .3f && viewport.y < .75f,
                "Show target centers the enemy in visible play space even when friendly troops are far away");

            foreach (var army in game.Sim.Armies) SetTurret(army, 0);
            var hqHealth = game.Sim.Armies.Select(a => a.BaseHealth).ToArray();
            int hqShots = 0;
            foreach (var unit in game.Sim.Units.Where(u => u.Alive && u.Team == 0))
            {
                unit.Position = unit.Destination = game.Sim.Armies[1].BasePosition + new Vector2(-4, 0);
                unit.HasOrder = false; unit.AttackTargetId = -1; SetWeapon(unit, 0);
            }
            for (int i = 0; i < 1200; i++)
            {
                game.Sim.Step(.05f);
                hqShots += game.Sim.Events.Count(e => e.ShooterId < 0);
            }
            Tick();
            Check(game.Sim.Armies.Select(a => a.BaseHealth).SequenceEqual(hqHealth),
                "A minute near an opposing practice HQ cannot destroy the target's recruitment source");
            Check(hqShots == 0, "Practice HQ turrets stay inactive even when their timers are ready");
            Check(!game.Sim.Finished && game.TutorialStep == 4 && Target() != null,
                "Long practice remains recoverable with a live target and no victory pressure");

            var automaticTarget = Target();
            int automaticId = automaticTarget.Id;
            int troopIndex = 0;
            foreach (var unit in game.Sim.Units.Where(u => u.Alive && u.Team == 0))
            {
                unit.Position = automaticTarget.Position + new Vector2(-2.5f, (troopIndex++ - 3) * .5f);
                unit.Destination = unit.Position; unit.HasOrder = false; unit.AttackTargetId = -1;
                SetWeapon(unit, 0);
            }
            bool autoKilled = false;
            for (int i = 0; i < 100 && !autoKilled; i++)
            {
                game.Sim.Step(.05f);
                autoKilled = !game.Sim.Units.Any(u => u.Id == automaticId && u.Alive);
                Tick();
            }
            for (int i = 0; i < 20; i++) { game.Sim.Step(.05f); Tick(); }
            Check(autoKilled && game.TutorialStep == 5 && !game.Sim.Units.Any(u => u.Alive && u.Team == 1),
                "Automatic fire completes the attack lesson once without respawning a defeated target");
            game.SelectKind(UnitKind.Rifle);
            game.IssueOrder(game.TutorialTargetPosition); Advance(6, 60);
            Check(game.TutorialActive && game.Sim.PracticeMode,
                "Automatic-fire attack and cover lessons reach the safe tutorial completion screen");

            StartAttackLesson();
            game.SelectAll();
            var clickedTarget = Target();
            var selectedIds = game.Selected.ToArray();
            var overlappingFriendly = game.Sim.Units.First(u => u.Team == 0 && u.Alive);
            overlappingFriendly.Position = overlappingFriendly.Destination = clickedTarget.Position;
            var targetScreen = game.ViewCamera.WorldToScreenPoint(new Vector3(clickedTarget.Position.x, .6f, clickedTarget.Position.y));
            game.ClickWorld(targetScreen);
            Check(game.Selected.SetEquals(selectedIds) &&
                game.Sim.Units.Where(u => selectedIds.Contains(u.Id)).All(u => u.AttackTargetId == clickedTarget.Id),
                "Clicking the marked target orders an attack even when a friendly troop overlaps it");
            Advance(5, 70);
            game.SelectKind(UnitKind.Rifle);
            game.IssueOrder(game.TutorialTargetPosition); Advance(6, 60);
            game.CompleteTutorial();
            Check(game.TutorialCompleted && !game.TutorialActive && !game.InMenu && !game.Sim.PracticeMode,
                "Play a battle ends the completed tutorial and starts an ordinary match");

            var ordinary = new BattleSimulation(false, 491);
            ordinary.UseExternalAI = true;
            foreach (var army in ordinary.Armies) { army.IsAI = false; SetTurret(army, 99999); }
            var tank = ordinary.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Tank);
            ordinary.Units.RemoveAll(u => u.Id != tank.Id);
            tank.Position = tank.Destination = ordinary.Armies[1].BasePosition + new Vector2(-4, 0);
            tank.HasOrder = false; SetWeapon(tank, 0);
            float ordinaryHealth = ordinary.Armies[1].BaseHealth;
            for (int i = 0; i < 100; i++) ordinary.Step(.05f);
            Check(!ordinary.PracticeMode && ordinary.Armies[1].BaseHealth < ordinaryHealth,
                "Ordinary battles retain real HQ damage outside explicit practice mode");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/tutorial-target-recovery.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            game.ReducedMotion = reduced;
            if (hadCompletion) PlayerPrefs.SetInt(CompletionKey, oldCompletion); else PlayerPrefs.DeleteKey(CompletionKey);
            PlayerPrefs.Save();
            game.ReturnToMenu();
        }
    }
}
