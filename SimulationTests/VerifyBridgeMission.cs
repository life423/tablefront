using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Tablefront;
using UnityEngine;

var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL " + name);
    checks.Add("PASS " + name);
}
BattleSimulation Mission(bool empty = true, int seed = 919)
{
    var sim = new BattleSimulation(false, seed, BattlefieldId.BridgeGambit) { UseExternalAI = true };
    foreach (var army in sim.Armies) { army.IsAI = false; army.TurretTimer = 99999; }
    foreach (var unit in sim.Units) unit.WeaponTimer = 99999;
    if (empty) sim.Units.Clear();
    return sim;
}
Troop Unit(BattleSimulation sim, int id, int team, UnitKind kind, Vector2 at)
{
    var unit = new Troop { Id = id, Team = team, Kind = kind, Position = at, Destination = at,
        Health = 10000, MaxHealth = 10000, WeaponTimer = 99999 };
    sim.Units.Add(unit); return unit;
}
void Advance(BattleSimulation sim, float seconds)
{
    for (int i = 0; i < (int)MathF.Ceiling(seconds / .05f); i++) sim.Step(.05f);
}
bool Legal(BattleSimulation sim, Troop unit) => sim.Bridge.IsWalkable(unit.Position, BattleSimulation.UnitRadius(unit.Kind));
Vector2 Rotate(Vector2 v, float degrees)
{
    float angle = degrees * Mathf.Deg2Rad;
    return new Vector2(v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle), v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle));
}
string policyPath = args[0], resultPath = args[1];
byte[] initialPolicy = File.ReadAllBytes(policyPath);
NeuralCommander.SetPolicy(JsonSerializer.Deserialize<NeuralCommander.Policy>(File.ReadAllText(policyPath), new JsonSerializerOptions { IncludeFields = true }));

var start = Mission(false);
Check(TablefrontBattlefields.All.Length == 4 && TablefrontBattlefields.Get(BattlefieldId.BridgeGambit) == TablefrontBattlefields.BridgeMission,
    "Bridge Gambit is a separate mission without altering the four-map skirmish roster");
Check(start.MissionActive && start.Bridge != null && start.Armies.Count(a => a.Alive) == 2 && !start.Armies[2].Alive && start.UnitCount(2) == 0,
    "Mission initializes exactly two live armies with no phantom third force");
Check(start.Units.Count == 12 && start.Units.All(u => u.Kind == UnitKind.Scout || u.Kind == UnitKind.Rifle || u.Kind == UnitKind.Tank) && start.Units.All(u => Legal(start, u)),
    "Both sides start with six legal Scout/Rifle/Tank units");
Check(start.CoverZones.All(c => MathF.Abs(c.Position.x) - c.Radius >= TablefrontBridge.RiverHalfWidth && c.Position.magnitude - c.Radius >= 12.19f),
    "All mission cover lies on banks outside the bridge sweep");
foreach (int team in new[] { 0, 1 })
{
    start.Armies[team].Credits = 999; start.Armies[team].RecruitCooldown = 0;
    Check(!start.Recruit(team, UnitKind.Gunner) && !start.Recruit(team, UnitKind.Artillery),
        "Team " + team + " uses the same three-class mission roster");
}
Check(start.Bridge.CurrentRoute == BridgeRoute.North && start.Bridge.Phase == BridgePhase.Stable &&
    start.Bridge.CommandsRemaining(0) == 3 && start.Bridge.CommandsRemaining(1) == 3,
    "The shared bridge starts docked North with three commands per side");
Check(!start.Bridge.IsWalkable(new Vector2(0, 10), .48f) && start.Bridge.IsWalkable(Vector2.zero, .86f),
    "Water blocks movement while the actual bridge supports a tank-sized footprint");

foreach (var kind in new[] { UnitKind.Scout, UnitKind.Rifle, UnitKind.Tank })
foreach (var route in new[] { BridgeRoute.North, BridgeRoute.South })
foreach (int direction in new[] { -1, 1 })
{
    var sim = Mission();
    if (route == BridgeRoute.South) { sim.TryRequestBridge(0, route); Advance(sim, 5.1f); }
    // Deliberately ask for the opposite diagonal: direct steering would cross deep water.
    var unit = Unit(sim, 100, 0, kind, new Vector2(-12 * direction, 8 * direction));
    Vector2 goal = new Vector2(12 * direction, -8 * direction);
    sim.OrderMove(new[] { unit.Id }, goal, false);
    bool legal = true;
    for (int i = 0; i < 1000 && Vector2.Distance(unit.Position, goal) > .5f; i++)
    {
        sim.Step(.05f); legal &= Legal(sim, unit);
    }
    Check(legal && Vector2.Distance(unit.Position, goal) <= .5f,
        kind + " reaches the far bank in direction " + direction + " on " + route + " through legal graph waypoints");
}

{
    var sim = Mission();
    var unit = Unit(sim, 100, 0, UnitKind.Rifle, new Vector2(-7, -8));
    sim.OrderMove(new[] { unit.Id }, new Vector2(12, 8), false);
    Check(sim.TryRequestBridge(0, BridgeRoute.South), "A valid player command starts a warning");
    bool noBoarding = true, retained = true;
    for (int i = 0; i < 100; i++)
    {
        sim.Step(.05f);
        if (sim.Bridge.Phase != BridgePhase.Stable) noBoarding &= unit.Position.x <= -TablefrontBridge.RiverHalfWidth - BattleSimulation.UnitRadius(unit.Kind) + .001f;
        retained &= unit.HasOrder;
    }
    Check(noBoarding && retained, "Warning and movement block new boardings without deleting the queued crossing order");
    Advance(sim, 45);
    Check(unit.Position.x > 5 && Vector2.Distance(unit.Position, unit.Destination) < .6f,
        "A waiting troop replans and finishes its original order after the other route docks");
}

{
    var sim = Mission();
    var held = Unit(sim, 100, 0, UnitKind.Rifle, new Vector2(1.2f, 1.6f));
    held.HoldPosition = true;
    var ordered = Unit(sim, 101, 1, UnitKind.Tank, new Vector2(-1.2f, -1.6f));
    sim.OrderMove(new[] { ordered.Id }, new Vector2(12, 8), false);
    sim.TryRequestBridge(0, BridgeRoute.South);
    Advance(sim, 3);
    Check(sim.Bridge.Phase == BridgePhase.Moving && sim.Bridge.IsRiding(held.Id) && sim.Bridge.IsRiding(ordered.Id),
        "Troops still aboard become passengers when the warned movement begins");
    Vector2 orderedStart = ordered.Position;
    Vector2 heldStart = held.Position;
    bool safe = true; float maxStep = 0;
    Vector2 last = held.Position;
    for (int i = 0; i < 40; i++)
    {
        sim.Step(.05f); safe &= held.Alive && ordered.Alive && Legal(sim, held) && Legal(sim, ordered);
        maxStep = MathF.Max(maxStep, Vector2.Distance(last, held.Position)); last = held.Position;
    }
    Check(safe && Vector2.Distance(held.Position, Rotate(heldStart, TablefrontBridge.SouthAngle - TablefrontBridge.NorthAngle)) < .2f && maxStep < .4f,
        "Passengers ride continuously with the deck and cannot fall or teleport across the river");
    Check(held.HoldPosition && held.Health == held.MaxHealth && ordered.HasOrder && ordered.Destination == new Vector2(12, 8) &&
        Vector2.Distance(ordered.Position, Rotate(orderedStart, TablefrontBridge.SouthAngle - TablefrontBridge.NorthAngle)) < .2f,
        "Rotation preserves health, Hold and existing travel destinations for both teams");
    Advance(sim, 35);
    Check(Vector2.Distance(ordered.Position, ordered.Destination) < .6f,
        "An unheld passenger resumes navigation to its pre-rotation destination");
}

{
    var sim = Mission();
    bool accepted = sim.TryRequestBridge(0, BridgeRoute.South);
    Check(accepted && sim.Bridge.CommandsRemaining(0) == 2 && sim.Bridge.CommandsRemaining(1) == 3 && sim.Bridge.ActorTeam == 0 && sim.Bridge.TimeToChange > 2.9f,
        "Accepted requests identify the actor and spend exactly that side's command");
    Check(!sim.TryRequestBridge(1, BridgeRoute.North) && !sim.TryRequestBridge(0, BridgeRoute.South) && sim.Bridge.CommandsRemaining(1) == 3,
        "Busy or duplicate requests are rejected without consuming resources");
    Advance(sim, 2.9f);
    Check(sim.Bridge.Phase == BridgePhase.Warning && MathF.Abs(sim.Bridge.AngleDegrees - TablefrontBridge.NorthAngle) < .001f,
        "The full warning is visible before geometry moves");
    Advance(sim, 2.2f);
    Check(sim.Bridge.Phase == BridgePhase.Stable && sim.Bridge.CurrentRoute == BridgeRoute.South &&
        !sim.TryRequestBridge(1, BridgeRoute.North) && sim.Bridge.CooldownRemaining(1) > 7,
        "Docking grace gives both sides time to cross before an opposing reversal");
    Advance(sim, 8);
    Check(sim.TryRequestBridge(1, BridgeRoute.North) && sim.Bridge.CommandsRemaining(1) == 2,
        "The enemy can use the exact same bridge request after the shared safety grace");
    Advance(sim, 20.1f); sim.TryRequestBridge(0, BridgeRoute.South);
    Advance(sim, 20.1f); sim.TryRequestBridge(1, BridgeRoute.North);
    Advance(sim, 20.1f); sim.TryRequestBridge(0, BridgeRoute.South);
    Advance(sim, 20.1f); sim.TryRequestBridge(1, BridgeRoute.North);
    Advance(sim, 20.1f);
    Check(sim.Bridge.CommandsRemaining(0) == 0 && sim.Bridge.CommandsRemaining(1) == 0 && !sim.TryRequestBridge(0, BridgeRoute.South),
        "Each side has a hard three-command budget; cooldown expiry cannot create extra charges");
    var crossing = Unit(sim, 100, 0, UnitKind.Tank, new Vector2(-12, 8));
    sim.OrderMove(new[] { crossing.Id }, new Vector2(12, -8), false); Advance(sim, 40);
    Check(crossing.Position.x > 5 && Legal(sim, crossing),
        "The final docked route remains traversable after both sides exhaust their bridge commands");
}

{
    var sim = Mission();
    var attacker = Unit(sim, 100, 0, UnitKind.Scout, new Vector2(-7, -8));
    var target = Unit(sim, 101, 1, UnitKind.Tank, new Vector2(1.2f, 1.6f)); target.HoldPosition = true;
    sim.OrderAttack(new[] { attacker.Id }, target.Id);
    sim.TryRequestBridge(0, BridgeRoute.South);
    bool legal = true;
    for (int i = 0; i < 600; i++) { sim.Step(.05f); legal &= sim.Units.All(u => Legal(sim, u)); }
    Check(legal && attacker.AttackTargetId == target.Id && Vector2.Distance(attacker.Position, target.Position) <= BattleSimulation.WeaponRange(attacker.Kind) + .3f,
        "An explicit attack replans toward a carried target without pursuing it through blocked water");
}

{
    var sim = Mission();
    var held = Unit(sim, 100, 0, UnitKind.Rifle, new Vector2(-1.2f, -1.6f));
    var doomed = Unit(sim, 101, 1, UnitKind.Rifle, new Vector2(1.2f, 1.6f));
    sim.OrderMove(new[] { held.Id }, new Vector2(12, 8), false);
    sim.TryRequestBridge(0, BridgeRoute.South); Advance(sim, 1);
    sim.OrderStop(new[] { held.Id }, true); sim.OrderStop(new[] { doomed.Id });
    Advance(sim, 2.05f);
    Check(sim.Bridge.IsRiding(held.Id) && held.HoldPosition && !held.HasOrder && sim.Bridge.IsRiding(doomed.Id),
        "Stop and Hold issued during warning keep existing passengers aboard safely");
    doomed.Health = 0; Advance(sim, 2.1f);
    Check(sim.Units.All(u => u.Id != doomed.Id) && held.Alive && Legal(sim, held) && !sim.Bridge.IsRiding(doomed.Id),
        "A passenger casualty is cleaned up during rotation without invalidating other riders");
}

{
    var sim = Mission(false);
    int id = 100;
    foreach (var kind in new[] { UnitKind.Scout, UnitKind.Rifle, UnitKind.Tank })
        for (int i = 0; i < 4; i++) Unit(sim, id++, 0, kind, new Vector2(-12 - i * .8f, -10 + (int)kind));
    sim.Units.RemoveAll(u => u.Team != 0);
    sim.OrderMove(sim.Units.Select(u => u.Id).ToArray(), new Vector2(14, 10), false);
    bool legal = true;
    for (int i = 0; i < 1400; i++)
    {
        if (i == 160) sim.TryRequestBridge(0, BridgeRoute.South);
        sim.Step(.05f);
        legal &= sim.Units.All(u => Legal(sim, u));
    }
    Check(legal && sim.Units.Count(u => u.Position.x > 5) >= sim.Units.Count - 1,
        "A congested mixed formation crosses and reroutes without separation pushing troops into water");
}

{
    var sim = Mission(); sim.Armies[0].Score = sim.Armies[1].Score = 999;
    Advance(sim, 3);
    Check(!sim.Finished, "Ordinary 350-point scoring cannot bypass the mission objective");
    sim.Points[1].Owner = sim.Points[1].CapturingTeam = 0; sim.Points[1].CaptureProgress = 1;
    Advance(sim, 44);
    Check(!sim.Finished && sim.MissionHoldSeconds(0) > 43.9f, "The depot objective waits for the actual continuous hold");
    var friendly = Unit(sim, 100, 0, UnitKind.Rifle, sim.Points[1].Position + new Vector2(-.5f, 0));
    var hostile = Unit(sim, 101, 1, UnitKind.Rifle, sim.Points[1].Position + new Vector2(.5f, 0));
    sim.Step(.05f);
    Check(sim.Points[1].Contested && sim.MissionHoldSeconds(0) == 0,
        "Contesting the depot resets the continuous mission hold");
    sim.Units.Remove(hostile); Advance(sim, 45.1f);
    Check(sim.Finished && sim.Winner == 0 && sim.MissionWinReason.Contains("45"), "The player wins by securing the enemy depot for 45 seconds");
    var enemy = Mission(); enemy.Points[0].Owner = enemy.Points[0].CapturingTeam = 1; enemy.Points[0].CaptureProgress = 1;
    Advance(enemy, 45.1f);
    Check(enemy.Finished && enemy.Winner == 1, "The enemy has the same depot-hold victory rule");
    var recapture = Mission(); recapture.Points[1].Owner = recapture.Points[1].CapturingTeam = 0; recapture.Points[1].CaptureProgress = 1;
    Advance(recapture, 40); Unit(recapture, 100, 1, UnitKind.Scout, recapture.Points[1].Position); recapture.Step(.05f);
    Check(recapture.MissionHoldSeconds(0) == 0 && !recapture.Finished,
        "An unopposed enemy recapture attempt also breaks continuous control before ownership changes");
    var actual = Mission(); var scout = Unit(actual, 100, 0, UnitKind.Scout, actual.Points[0].Position);
    actual.OrderMove(new[] { scout.Id }, actual.Points[1].Position, false); Advance(actual, 90);
    Check(actual.Finished && actual.Winner == 0 && actual.Points[1].Owner == 0,
        "Real navigation, capture and a full hold complete the player objective without editing ownership");
    var hq = Mission(); hq.Armies[0].BaseHealth = 0; hq.Step(.05f);
    Check(hq.Finished && hq.Winner == 1 && hq.MissionWinReason.Contains("headquarters"), "Losing a headquarters still ends the mission clearly");
}

{
    var sim = Mission(); sim.UseExternalAI = false;
    sim.Armies[1].IsAI = true; sim.Armies[1].DecisionTimer = 99999;
    sim.TryRequestBridge(0, BridgeRoute.South); Advance(sim, 13.1f);
    Unit(sim, 100, 1, UnitKind.Tank, new Vector2(20, 20));
    sim.OrderMove(sim.Units.Where(u => u.Team == 1).Select(u => u.Id).ToArray(), sim.Points[0].Position, false);
    Advance(sim, 3);
    Check(sim.Bridge.CommandsRemaining(1) == 2 && sim.Bridge.ActorTeam == 1,
        "The prototype AI spends its own charge to shorten a real ordered cross-bank route");
}

var rollouts = new List<object>();
int normallyFinished = 0, aiUsedBridge = 0;
for (int seed = 1; seed <= 8; seed++)
{
    var sim = new BattleSimulation(true, 4100 + seed, BattlefieldId.BridgeGambit);
    bool finite = true, rotated = false;
    for (int step = 0; step < 12000 && !sim.Finished; step++)
    {
        if (!rotated && sim.Elapsed >= 8) rotated = sim.TryRequestBridge(0, BridgeRoute.South);
        sim.Step(.05f);
        finite &= sim.Units.All(u => Legal(sim, u) && !float.IsNaN(u.Health));
    }
    if (sim.Finished) normallyFinished++;
    if (sim.Bridge.CommandsRemaining(1) < 3) aiUsedBridge++;
    rollouts.Add(new { seed, sim.Finished, sim.Winner, sim.Elapsed, finite, enemyCommandsUsed = 3 - sim.Bridge.CommandsRemaining(1) });
    Check(finite, "Frozen-neural mission seed " + seed + " retains legal finite units through combat and bridge changes");
}
Check(aiUsedBridge > 0, "Frozen-neural mission rollouts actually exercise the enemy bridge controller");
Check(initialPolicy.SequenceEqual(File.ReadAllBytes(policyPath)), "Mission verification leaves neural policy bytes unchanged");
var result = new { kind = "Bridge Gambit deterministic mission and frozen inference; no training", checks = checks.Count, passed = checks,
    rollouts, normallyFinished, aiUsedBridge, policySha256 = Convert.ToHexString(SHA256.HashData(initialPolicy)).ToLowerInvariant(), trainingStarted = false };
Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(checks.Count + " checks passed; " + normallyFinished + "/8 frozen-neural mission rollouts finished; enemy bridge used in " + aiUsedBridge + "/8.");
foreach (var check in checks) Console.WriteLine(check);
