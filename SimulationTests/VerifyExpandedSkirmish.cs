using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tablefront;
using UnityEngine;

// Rules and frozen-inference verification only. No trainer, optimizer or policy writes.
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL " + name);
    checks.Add("PASS " + name);
}
BattleSimulation Empty(BattlefieldId map = BattlefieldId.Greenfield)
{
    var sim = new BattleSimulation(false, 7654, map) { UseExternalAI = true, PracticeMode = true };
    foreach (var army in sim.Armies) { army.IsAI = false; army.TurretTimer = 99999; }
    sim.Units.Clear();
    return sim;
}
Troop Unit(BattleSimulation sim, int id, int team, UnitKind kind, Vector2 at, float health = 1000)
{
    var unit = new Troop { Id = id, Team = team, Kind = kind, Position = at, Destination = at,
        Health = health, MaxHealth = health, WeaponTimer = 1000 };
    sim.Units.Add(unit); return unit;
}
void Advance(BattleSimulation sim, float seconds)
{
    for (int i = 0; i < MathF.Ceiling(seconds / .05f); i++) sim.Step(.05f);
}
float GunnerHit(UnitKind kind, bool ai)
{
    var sim = Empty(); sim.Armies[0].IsAI = ai;
    var gunner = Unit(sim, 1, 0, UnitKind.Gunner, Vector2.zero);
    var victim = Unit(sim, 2, 1, kind, new Vector2(4, 0));
    gunner.WeaponTimer = 0;
    sim.Step(.05f);
    return victim.MaxHealth - victim.Health;
}

Check(TablefrontBattlefields.All.Length == 4, "Four selectable battlefields are defined");
Check(TablefrontBattlefields.All.Select(m => m.Id).Distinct().Count() == 4 &&
    TablefrontBattlefields.All.Select(m => string.Join(";", m.PointPositions.Select(p => p.ToString()))).Distinct().Count() == 4,
    "Battlefields have unique identities and objective layouts");
Check(TablefrontBattlefields.Get((BattlefieldId)99).Id == BattlefieldId.Greenfield, "An invalid saved map falls back to Greenfield");
foreach (var map in TablefrontBattlefields.All)
{
    var sim = new BattleSimulation(false, 7654, map.Id);
    Check(sim.Battlefield == map && sim.Points.Count == 3 && sim.Armies.Count == 3 && sim.CoverZones.Count == 6,
        map.Name + " initializes its own landmarks and six cover positions");
    Check(sim.Armies.Select(a => a.BasePosition).SequenceEqual(map.BasePositions) &&
        sim.Points.Select(p => p.Position).SequenceEqual(map.PointPositions), map.Name + " rules consume the shared art layout");
    Check(sim.Units.Count == 18 && sim.Armies.All(a => sim.UnitCount(a.Id) == 6) &&
        sim.Units.All(u => MathF.Abs(u.Position.x) <= 24 && MathF.Abs(u.Position.y) <= 24),
        map.Name + " gives each army six live, bounded starting units");
    Check(sim.CoverZones.All(z => MathF.Abs(z.Position.x) + z.Radius < 24 && MathF.Abs(z.Position.y) + z.Radius < 24) &&
        sim.Points.All(p => MathF.Abs(p.Position.x) + BattleSimulation.CaptureRadius < 24 && MathF.Abs(p.Position.y) + BattleSimulation.CaptureRadius < 24),
        map.Name + " capture rings and cover fit on the board");
    var nearestApproaches = map.BasePositions.Select(b => map.PointPositions.Min(p => Vector2.Distance(b, p))).ToArray();
    Check(nearestApproaches.Max() - nearestApproaches.Min() < .35f,
        map.Name + " gives all three seats comparable nearest-objective approach distances");
    var capture = Empty(map.Id);
    var scout = Unit(capture, 1, 0, UnitKind.Scout, capture.Points[0].Position);
    Advance(capture, 8);
    Check(capture.Points[0].Owner == 0 && capture.Armies[0].Score > 0 && capture.IsInCover(capture.CoverZones[0].Position),
        map.Name + " supports real capture, scoring and mechanical cover");
}
var original = new BattleSimulation(false);
Check(original.Points[0].Position == new Vector2(-10, -6) && original.Points[0].Name == "WEST" &&
    original.Armies[0].BasePosition == new Vector2(0, -21) && !original.ExpandedRoster,
    "Default replay/training constructor retains the original map and three-unit recruitment actions");
Check((int)UnitKind.Scout == 0 && (int)UnitKind.Rifle == 1 && (int)UnitKind.Tank == 2 &&
    BattleSimulation.UnitKinds.Length == 5, "New unit kinds append without renumbering the original roster");
foreach (var kind in BattleSimulation.UnitKinds)
{
    var sim = Empty(); sim.Armies[0].Credits = 999;
    Check(sim.Recruit(0, kind) && sim.Units.Single().Kind == kind &&
        MathF.Abs(sim.Armies[0].Credits - (999 - BattleSimulation.Cost(kind))) < .001f &&
        sim.Armies[0].ReinforcementsDeployed == 1 && sim.Armies[0].RecruitCooldown > 0,
        kind + " recruitment pays the real cost, cooldown and reinforcement count");
}
var invalid = Empty();
Check(!invalid.Recruit(0, (UnitKind)5) && !invalid.Recruit(0, (UnitKind)(-1)), "Invalid unit kinds cannot recruit");
Check(GunnerHit(UnitKind.Rifle, false) > GunnerHit(UnitKind.Tank, false) * 3,
    "Gunners specialize against infantry rather than replacing anti-armour weapons");
Check(MathF.Abs(GunnerHit(UnitKind.Rifle, false) - GunnerHit(UnitKind.Rifle, true)) < .001f,
    "AI and player gunners share the same resolved weapon damage");
Check(BattleSimulation.WeaponRange(UnitKind.Artillery) > BattleSimulation.WeaponRange(UnitKind.Tank) &&
    BattleSimulation.MinimumWeaponRange(UnitKind.Artillery) == 4.5f, "Artillery exposes its longer range and firing dead zone");

var artillerySim = Empty();
var artillery = Unit(artillerySim, 1, 0, UnitKind.Artillery, Vector2.zero);
var direct = Unit(artillerySim, 2, 1, UnitKind.Rifle, new Vector2(10, 0));
var splash = Unit(artillerySim, 3, 1, UnitKind.Rifle, new Vector2(10, 2.4f));
var friendly = Unit(artillerySim, 4, 0, UnitKind.Rifle, new Vector2(10, -2.4f));
artillery.WeaponTimer = 0; artillerySim.Step(.05f);
Check(direct.Health < direct.MaxHealth && splash.Health < splash.MaxHealth && friendly.Health == friendly.MaxHealth,
    "Artillery resolves a long-range hit and wider enemy-only splash");
var closeSim = Empty();
var closeArtillery = Unit(closeSim, 1, 0, UnitKind.Artillery, Vector2.zero);
var closeScout = Unit(closeSim, 2, 1, UnitKind.Scout, new Vector2(2, 0));
closeArtillery.WeaponTimer = 0; Advance(closeSim, 1);
Check(closeScout.Health == closeScout.MaxHealth && closeArtillery.Position.sqrMagnitude < .001f,
    "Idle artillery cannot fire at or automatically flee a close attacker");
closeSim.OrderAttack(new[] { closeArtillery.Id }, closeScout.Id); Advance(closeSim, .5f);
Check(closeArtillery.Position.x < -.1f && closeScout.Health == closeScout.MaxHealth,
    "Explicit artillery focus orders seek firing distance without firing through the dead zone");

var holdSim = Empty();
var rifle = Unit(holdSim, 1, 0, UnitKind.Rifle, Vector2.zero);
var enemy = Unit(holdSim, 2, 1, UnitKind.Scout, new Vector2(7.3f, 0));
rifle.WeaponTimer = 0;
holdSim.OrderMove(new[] { rifle.Id }, new Vector2(12, 0));
holdSim.OrderStop(new[] { rifle.Id }, true); Advance(holdSim, 1);
Check(rifle.HoldPosition && !rifle.HasOrder && rifle.AttackTargetId == -1 && rifle.Position.sqrMagnitude < .001f && enemy.Health == enemy.MaxHealth,
    "Hold cancels travel and does not pursue or shoot an out-of-range enemy");
enemy.Position = enemy.Destination = new Vector2(5, 0); Advance(holdSim, .1f);
Check(enemy.Health < enemy.MaxHealth && rifle.Position.sqrMagnitude < .001f,
    "Held units automatically fire at an enemy already in range without chasing");
holdSim.OrderMove(new[] { rifle.Id }, new Vector2(-8, 0), false); Advance(holdSim, .1f);
Check(!rifle.HoldPosition && rifle.HasOrder && rifle.Position.x < -.1f,
    "An ordinary move releases hold and prioritizes travel");
holdSim.OrderAttack(new[] { rifle.Id }, enemy.Id); holdSim.OrderStop(new[] { rifle.Id });
Check(!rifle.HoldPosition && !rifle.HasOrder && rifle.AttackTargetId == -1 && rifle.BaseTargetTeam == -1 && rifle.Destination == rifle.Position,
    "Stop clears movement and explicit targets without leaving a hold lock");
holdSim.OrderStop(new[] { rifle.Id }, true); holdSim.OrderAttack(new[] { rifle.Id }, enemy.Id);
Check(!rifle.HoldPosition && rifle.AttackTargetId == enemy.Id, "An explicit attack releases hold");

var adapter = new BattleSimulation(false) { UseExternalAI = true };
adapter.Armies[0].Credits = 999; adapter.ApplyAction(0, 7);
Check(adapter.Units.Last().Kind == UnitKind.Rifle, "Original frozen-policy recruitment actions retain their legacy unit mapping by default");
adapter.ExpandedRoster = true; adapter.Armies[0].RecruitCooldown = 0; adapter.ApplyAction(0, 7);
Check(adapter.Units.Last().Kind == UnitKind.Gunner, "Expanded infantry recruitment roles can deploy a paid gunner");
adapter.Armies[0].RecruitCooldown = 0; adapter.ApplyAction(0, 8);
Check(adapter.Units.Last().Kind == UnitKind.Artillery, "Expanded heavy recruitment roles can deploy paid artillery");
adapter.PracticeMode = true; adapter.Armies[0].RecruitCooldown = 0; adapter.ApplyAction(0, 7);
Check(adapter.Units.Last().Kind == UnitKind.Rifle, "Practice keeps the original recruitment vocabulary even if the roster flag was enabled");
float[] observation = adapter.GetObservation(0);
Check(observation.Length == 40 && observation.All(float.IsFinite) && BattleSimulation.ActionCount == 10 &&
    MathF.Abs(observation[5] * BattleSimulation.UnitCap - adapter.Units.Count(u => u.Team == 0 && (u.Kind == UnitKind.Rifle || u.Kind == UnitKind.Gunner))) < .001f,
    "Expanded units map to existing role observations while keeping 40 inputs and 10 outputs");

string policyPath = Path.GetFullPath(args[0]);
byte[] policyBytes = File.ReadAllBytes(policyPath);
var trained = JsonSerializer.Deserialize<NeuralCommander.Policy>(Encoding.UTF8.GetString(policyBytes), new JsonSerializerOptions { IncludeFields = true });
NeuralCommander.SetPolicy(trained);
Check(NeuralCommander.Available && NeuralCommander.PredictProbabilities(observation).Length == 10,
    "The actual shipped neural policy still accepts the expanded game's observation contract");
var battles = new List<object>();
int finished = 0, timeouts = 0, invalidStates = 0, newUnitsDeployed = 0;
foreach (var map in TablefrontBattlefields.All)
{
    for (int match = 0; match < 6; match++)
    {
        int seed = 85001 + (int)map.Id * 100 + match;
        var sim = new BattleSimulation(true, seed, map.Id) { ExpandedRoster = true };
        bool finite = true;
        bool sawNewUnit = false;
        while (!sim.Finished && sim.Elapsed < 600)
        {
            sim.Step(.05f);
            sawNewUnit |= sim.Units.Any(u => u.Kind == UnitKind.Gunner || u.Kind == UnitKind.Artillery);
            if (sim.Units.Any(u => !float.IsFinite(u.Health) || !float.IsFinite(u.Position.x) || !float.IsFinite(u.Position.y) ||
                MathF.Abs(u.Position.x) > 24.001f || MathF.Abs(u.Position.y) > 24.001f) ||
                sim.Armies.Any(a => !float.IsFinite(a.Score) || !float.IsFinite(a.Credits) || a.Credits < 0 || sim.UnitCount(a.Id) > BattleSimulation.UnitCap))
            { finite = false; invalidStates++; break; }
        }
        if (sim.Finished && sim.Winner >= 0) finished++; else if (finite) timeouts++;
        if (sawNewUnit) newUnitsDeployed++;
        battles.Add(new { map = map.Name, seed, sim.Finished, sim.Winner, sim.Elapsed, finite, sawNewUnit });
    }
}
Check(finished == 24 && timeouts == 0 && invalidStates == 0, "All 24 frozen-neural battles across four maps finish without timeout or invalid state");
Check(newUnitsDeployed == 24, "All 24 frozen-neural battles actually deploy the expanded roster through the explicit role adapter");
Check(policyBytes.SequenceEqual(File.ReadAllBytes(policyPath)), "Frozen neural evaluation leaves policy bytes unchanged");
var report = new {
    kind = "Expanded skirmish rules and frozen inference; not training or a difficulty/balance benchmark",
    checks = checks.Count, passed = checks, neuralBattles = battles,
    normallyFinished = finished, timeouts, invalidStates, battlesWithExpandedUnits = newUnitsDeployed,
    policySha256 = Convert.ToHexString(SHA256.HashData(policyBytes)).ToLowerInvariant(), trainingStarted = false
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(checks.Count + " checks passed; " + finished + "/24 frozen-neural battles completed.");
Console.WriteLine(string.Join("\n", checks));
