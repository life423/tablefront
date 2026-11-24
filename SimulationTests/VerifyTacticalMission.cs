using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Tablefront;
using UnityEngine;

var checks = new List<string>();
void Check(bool condition, string name)
{ if (!condition) throw new Exception("FAIL " + name); checks.Add("PASS " + name); }
BattleSimulation Mission()
{
    var sim = new BattleSimulation(false, 713, BattlefieldId.BridgeGambit) { UseExternalAI = true };
    sim.Units.Clear();
    foreach (var army in sim.Armies) { army.IsAI = false; army.TurretTimer = 99999; }
    return sim;
}
Troop Unit(BattleSimulation sim, int id, int team, Vector2 at, UnitKind kind = UnitKind.Rifle)
{
    var unit = new Troop { Id = id, Team = team, Kind = kind, Position = at, Destination = at,
        Health = 10000, MaxHealth = 10000, WeaponTimer = 99999 };
    sim.Units.Add(unit); return unit;
}
void Advance(BattleSimulation sim, float seconds)
{ for (int i = 0; i < (int)MathF.Ceiling(seconds / .05f); i++) sim.Step(.05f); }
bool Legal(BattleSimulation sim, Troop unit) => sim.Bridge.IsWalkable(unit.Position, BattleSimulation.UnitRadius(unit.Kind));
var policyBytes = File.ReadAllBytes(args[0]);

{
    var sim = Mission(); var unit = Unit(sim, 100, 0, new Vector2(-18, 0));
    sim.OrderMove(new[] { 100 }, new Vector2(-15, 0), false);
    Check(sim.OrderMove(new[] { 100 }, new Vector2(-15, 8), false, true) == 1 && unit.QueuedOrders.Count == 1 && unit.Destination == new Vector2(-15, 0),
        "Appending a waypoint retains the active destination and exposes the queued leg");
    Advance(sim, 9);
    Check(!unit.HasOrder && unit.QueuedOrders.Count == 0 && Vector2.Distance(unit.Position, new Vector2(-15, 8)) < .3f,
        "A unit completes two real movement legs in order");
    Check(sim.OrderMove(new[] { 100 }, new Vector2(-18, 8), false, true) == 1 && unit.HasOrder && unit.QueuedOrders.Count == 0,
        "Append on an idle unit immediately starts its first order");
    sim.OrderMove(new[] { 100 }, new Vector2(-18, 0), false, true);
    sim.OrderMove(new[] { 100 }, new Vector2(-16, 10), false);
    Check(unit.QueuedOrders.Count == 0 && unit.Destination == new Vector2(-16, 10), "A replacement order clears earlier waypoints");
    sim.OrderMove(new[] { 100 }, new Vector2(-16, 0), false, true); sim.OrderStop(new[] { 100 }, true);
    Check(!unit.HasOrder && unit.HoldPosition && unit.QueuedOrders.Count == 0, "Hold clears the entire queue without leaving a delayed move");
    sim.OrderMove(new[] { 100 }, new Vector2(-16, 0), false); sim.OrderMove(new[] { 100 }, new Vector2(-16, 4), false, true);
    sim.OrderStop(new[] { 100 }); Advance(sim, 2);
    Check(!unit.HasOrder && !unit.HoldPosition && unit.QueuedOrders.Count == 0, "Stop clears the entire queue");
    sim.OrderMove(new[] { 100 }, new Vector2(-16, 0), false);
    for (int i = 0; i < BattleSimulation.MaxQueuedOrders; i++) sim.OrderMove(new[] { 100 }, new Vector2(-15, i), false, true);
    Check(sim.OrderMove(new[] { 100 }, new Vector2(-14, 0), false, true) == 0 && unit.QueuedOrders.Count == BattleSimulation.MaxQueuedOrders,
        "A full queue rejects visibly through the accepted-count API and preserves all accepted legs");
    var target = Unit(sim, 101, 1, new Vector2(-10, 0));
    Check(sim.OrderAttack(new[] { 100 }, target.Id, true) == 0 && unit.AttackTargetId == -1, "A full queue also rejects focus orders consistently");
    sim.OrderStop(new[] { 100 });
    sim.OrderMove(new[] { 100, 100 }, new Vector2(-15, 0), false, true);
    Check(unit.QueuedOrders.Count == 0, "Duplicate selection IDs cannot append duplicate orders");
}
{
    var sim = Mission(); var unit = Unit(sim, 100, 0, new Vector2(-18, 0));
    var target = Unit(sim, 101, 1, new Vector2(-12, 0));
    sim.OrderMove(new[] { 100 }, new Vector2(-16, 0), false);
    sim.OrderAttack(new[] { 100 }, 101, true); sim.OrderMove(new[] { 100 }, new Vector2(-16, 6), false, true);
    target.Health = 0; Advance(sim, 8);
    Check(!unit.HasOrder && unit.QueuedOrders.Count == 0 && Vector2.Distance(unit.Position, new Vector2(-16, 6)) < .3f,
        "A dead queued focus target is skipped and subsequent movement completes");
}
{
    var sim = Mission(); var unit = Unit(sim, 100, 0, new Vector2(-15, 0));
    var target = Unit(sim, 101, 1, new Vector2(-11, 0)); target.Health = 5; target.HoldPosition = true;
    unit.WeaponTimer = 0;
    sim.OrderAttack(new[] { 100 }, 101); sim.OrderMove(new[] { 100 }, new Vector2(-15, 6), false, true);
    Advance(sim, 7);
    Check(!target.Alive && unit.QueuedOrders.Count == 0 && Vector2.Distance(unit.Position, new Vector2(-15, 6)) < .3f,
        "Completing an actual focus kill starts the following waypoint");
}
{
    var sim = Mission(); var unit = Unit(sim, 100, 0, new Vector2(-12, -10), UnitKind.Scout);
    var slots = new List<OrderFormationSlot>(); var path = new List<Vector2>(16);
    sim.OrderMove(new[] { 100 }, new Vector2(-14, 0), false); sim.OrderMove(new[] { 100 }, new Vector2(-14, 4), false, true);
    var destination = unit.Destination; int commands = sim.Bridge.CommandsRemaining(0), queued = unit.QueuedOrders.Count;
    sim.GetFormation(new[] { 100 }, new Vector2(12, 8), slots);
    sim.BuildOrderRoute(unit, unit.Position, slots[0].Destination, path, out bool waiting);
    Check(path.Count > 2 && path[0] == unit.Position && path[path.Count - 1] == slots[0].Destination && !waiting,
        "The preview contains actual bank/bridge turns and its legal formation endpoint");
    Check(unit.Destination == destination && unit.QueuedOrders.Count == queued && sim.Bridge.CommandsRemaining(0) == commands && !unit.WaitingForBridge,
        "Formation and route previews do not mutate active orders, queues, waiting state or resources");
    sim.OrderMove(new[] { 100 }, new Vector2(12, 8), false);
    Check(unit.Destination == slots[0].Destination, "The accepted order uses exactly the previewed formation destination");
    sim.OrderMove(new[] { 100 }, new Vector2(14, -10), false, true); sim.TryRequestBridge(0, BridgeRoute.South);
    sim.BuildOrderRoute(unit, unit.Position, unit.Destination, path, out waiting);
    Check(waiting && path.All(p => p.x < -TablefrontBridge.RiverHalfWidth), "A warning preview stops at a legal waiting approach instead of drawing a fictional crossing");
    bool legal = true;
    for (int i = 0; i < 1200; i++) { sim.Step(.05f); legal &= Legal(sim, unit); }
    Check(legal && !unit.HasOrder && unit.QueuedOrders.Count == 0 && Vector2.Distance(unit.Position, new Vector2(14, -10)) < .3f,
        "Queued legs survive a bridge change and complete through the new docked route");
}

foreach (int mirror in new[] { -1, 1 })
foreach (var kind in new[] { UnitKind.Scout, UnitKind.Rifle, UnitKind.Tank })
{
    var sim = Mission(); var unit = Unit(sim, 100, 0, new Vector2(8 * mirror, 4 * mirror), kind);
    var goal = new Vector2(16 * mirror, 4 * mirror); var path = new List<Vector2>(16);
    sim.BuildOrderRoute(unit, unit.Position, goal, path, out bool waiting);
    bool pathClear = !waiting && path.Count >= 4;
    for (int i = 1; i < path.Count; i++) pathClear &= sim.Tactics.SegmentClear(path[i - 1], path[i], BattleSimulation.UnitRadius(kind));
    sim.OrderMove(new[] { 100 }, goal, false); bool legal = true;
    for (int i = 0; i < 500; i++) { sim.Step(.05f); legal &= Legal(sim, unit); }
    Check(pathClear && legal && Vector2.Distance(unit.Position, goal) < .3f,
        kind + " actually routes around the footprint-expanded blocker on bank " + mirror);
}
{
    var sim = Mission(); var a = Unit(sim, 100, 0, new Vector2(10, 4)); var b = Unit(sim, 101, 1, new Vector2(14, 4));
    a.HoldPosition = b.HoldPosition = true; a.WeaponTimer = b.WeaponTimer = 0;
    Advance(sim, 3);
    Check(!sim.HasLineOfSight(a.Position, b.Position) && a.Health == a.MaxHealth && b.Health == b.MaxHealth,
        "The same wall blocks automatic target acquisition and fire for both teams");
    sim.OrderAttack(new[] { 100 }, 101); bool legal = true;
    for (int i = 0; i < 400 && b.Health == b.MaxHealth; i++) { sim.Step(.05f); legal &= Legal(sim, a); }
    Check(legal && b.Health < b.MaxHealth && sim.HasLineOfSight(a.Position, b.Position), "Explicit focus pursues around the wall until a real firing line opens");
}
{
    var sim = Mission(); var mover = Unit(sim, 101, 1, new Vector2(14, 6.65f), UnitKind.Scout);
    var shooter = Unit(sim, 100, 0, new Vector2(10, 6.65f)); shooter.HoldPosition = true; shooter.WeaponTimer = 0;
    sim.OrderMove(new[] { mover.Id }, new Vector2(14, 5), false);
    sim.Step(.05f);
    Check(shooter.WeaponTimer > .7f && mover.Health == mover.MaxHealth && sim.Events.Count == 0,
        "A target moving behind a wall after acquisition blocks the pending hit at resolution");
}
foreach (int team in new[] { 0, 1 })
{
    var sim = Mission(); int mirror = team == 0 ? 1 : -1;
    sim.Armies[1 - team].BasePosition = new Vector2(14 * mirror, 4 * mirror);
    sim.Armies[1 - team].TurretTimer = 0;
    var unit = Unit(sim, 100, team, new Vector2(10 * mirror, 4 * mirror)); unit.HoldPosition = true; unit.WeaponTimer = 0;
    Advance(sim, 2);
    Check(unit.Health == unit.MaxHealth && sim.Armies[1 - team].BaseHealth == BattleSimulation.BaseMaxHealth,
        "HQ turret and troop-to-HQ fire obey the same blocker for team " + team);
    unit.Position = unit.Destination = new Vector2(14 * mirror, 8 * mirror); Advance(sim, 1);
    Check(unit.Health < unit.MaxHealth && sim.Armies[1 - team].BaseHealth < BattleSimulation.BaseMaxHealth,
        "Both HQ and troop can fire after the same line of sight opens for team " + team);
}

var damageProof = new List<object>();
foreach (int mirror in new[] { 1, -1 })
{
    float[] damage = new float[2];
    for (int flank = 0; flank < 2; flank++)
    {
        var sim = Mission(); var defender = Unit(sim, 101, 1, new Vector2(10 * mirror, 7 * mirror)); defender.HoldPosition = true;
        var at = flank == 0 ? new Vector2(6 * mirror, 8 * mirror) : new Vector2(14 * mirror, 6.8f * mirror);
        var shooter = Unit(sim, 100, 0, at); shooter.WeaponTimer = 0; shooter.HoldPosition = true;
        sim.Step(.05f); damage[flank] = defender.MaxHealth - defender.Health;
        var hit = sim.Events.FirstOrDefault(e => e.TargetId == defender.Id);
        Check(sim.HasLineOfSight(at, defender.Position) && hit.ShooterId == shooter.Id &&
            hit.ProtectedHit == (flank == 0) && hit.FlankedHit == (flank == 1),
            "Actual " + (flank == 0 ? "North frontal" : "South rear") + " shot reports truthful cover state on bank " + mirror);
    }
    Check(MathF.Abs(damage[0] - 11.5f * .65f) < .002f && MathF.Abs(damage[1] - 11.5f) < .002f,
        "The same rifle defender takes 7.475 frontal damage and 11.5 exposed rear damage on bank " + mirror);
    damageProof.Add(new { bank = mirror, northFrontal = damage[0], southRear = damage[1] });
}
{
    var sim = Mission(); sim.TryRequestBridge(0, BridgeRoute.South); Advance(sim, 5.1f);
    var shooter = Unit(sim, 100, 0, new Vector2(6, -8)); var rear = new Vector2(14, 6.8f); var path = new List<Vector2>(16);
    sim.BuildOrderRoute(shooter, shooter.Position, rear, path, out bool waiting);
    Check(!waiting && path.Count >= 3 && !sim.Tactics.SegmentClear(shooter.Position, rear, BattleSimulation.UnitRadius(shooter.Kind)),
        "The South landing's exposed rear firing position requires an actual wall detour");
    sim.OrderMove(new[] { 100 }, rear, false); bool legal = true;
    for (int i = 0; i < 700; i++) { sim.Step(.05f); legal &= Legal(sim, shooter); }
    var defender = Unit(sim, 101, 1, new Vector2(10, 7)); defender.HoldPosition = true; shooter.WeaponTimer = 0;
    shooter.HoldPosition = true; sim.Step(.05f);
    Check(legal && Vector2.Distance(shooter.Position, rear) < .3f && MathF.Abs(defender.MaxHealth - defender.Health - 11.5f) < .002f,
        "Infantry navigates from South landing around the wall and delivers an actual unprotected rear shot");
}

foreach (int team in new[] { 0, 1 })
{
    var sim = Mission(); sim.TryRequestBridge(team, BridgeRoute.South);
    sim.Points[2].Owner = sim.Points[2].CapturingTeam = team; sim.Points[2].CaptureProgress = 1;
    Advance(sim, 19.8f);
    Check(sim.Bridge.CommandsRemaining(team) == 2 && sim.BridgeRechargeSeconds(team) > 19.7f,
        "ROTARY cannot replenish early for team " + team);
    Advance(sim, .3f);
    Check(sim.Bridge.CommandsRemaining(team) == 3 && sim.BridgeRechargeSeconds(team) == 0,
        "Twenty seconds continuous ownership restores exactly one command for team " + team);
    Advance(sim, 24); sim.TryRequestBridge(team, BridgeRoute.North); sim.Step(.05f);
    Check(sim.Bridge.CommandsRemaining(team) == 2 && sim.BridgeRechargeSeconds(team) < .1f,
        "Full capacity cannot bank hidden recharge credit for team " + team);
    Advance(sim, 7); Unit(sim, 100, team, Vector2.zero).HoldPosition = true;
    Unit(sim, 101, 1 - team, new Vector2(.8f, 0)).HoldPosition = true; sim.Step(.05f);
    Check(sim.Points[2].Contested && sim.BridgeRechargeSeconds(team) == 0,
        "A genuine two-army contest resets partial charge progress for team " + team);
    sim.Units.Clear(); sim.Points[2].Owner = sim.Points[2].CapturingTeam = team; Advance(sim, 4);
    Unit(sim, 102, 1 - team, Vector2.zero).HoldPosition = true; sim.Step(.05f);
    Check(!sim.Points[2].Contested && sim.Points[2].Owner == team && sim.Points[2].CapturingTeam == 1 - team && sim.BridgeRechargeSeconds(team) == 0,
        "An unopposed hostile recapture attempt resets charging before the flag flips for team " + team);
    sim.Units.Clear(); sim.Points[2].Owner = sim.Points[2].CapturingTeam = -1; Advance(sim, 25);
    Check(sim.Bridge.CommandsRemaining(team) == 2 && sim.BridgeRechargeSeconds(team) == 0,
        "Cooldown or neutral ROTARY cannot regenerate commands for team " + team);
}

var stress = Mission();
for (int i = 0; i < 26; i++) Unit(stress, 100 + i, 0, new Vector2(-20 + (i % 4) * 1.9f, -18 + (i / 4) * 2f), (UnitKind)(i % 3));
var ids = stress.Units.Select(u => u.Id).ToArray(); var formation = new List<OrderFormationSlot>(30);
stress.GetFormation(ids, new Vector2(12, 4), formation);
bool formationLegal = formation.All(s => stress.Bridge.IsWalkable(s.Destination, BattleSimulation.UnitRadius(stress.Units.First(u => u.Id == s.UnitId).Kind)));
for (int a = 0; a < formation.Count; a++)
for (int b = a + 1; b < formation.Count; b++)
{
    float radius = BattleSimulation.UnitRadius(stress.Units[a].Kind) + BattleSimulation.UnitRadius(stress.Units[b].Kind);
    formationLegal &= Vector2.Distance(formation[a].Destination, formation[b].Destination) >= radius;
}
Check(formation.Count == 26 && formationLegal, "A 26-unit mixed formation at a wall gets distinct legal base footprints");
foreach (var target in new[] { Vector2.zero, new Vector2(24, 24), new Vector2(-24, -24) })
{
    stress.GetFormation(ids, target, formation); bool distinct = true;
    for (int a = 0; a < formation.Count; a++)
    {
        distinct &= stress.Bridge.IsWalkable(formation[a].Destination, BattleSimulation.UnitRadius(stress.Units[a].Kind));
        for (int b = a + 1; b < formation.Count; b++)
            distinct &= Vector2.Distance(formation[a].Destination, formation[b].Destination) >=
                BattleSimulation.UnitRadius(stress.Units[a].Kind) + BattleSimulation.UnitRadius(stress.Units[b].Kind);
    }
    Check(distinct, "A mixed 26-unit formation remains distinct and legal at constrained target " + target);
}
stress.OrderMove(ids, new Vector2(16, 4), false); stress.OrderMove(ids, new Vector2(16, -8), false, true);
var clock = Stopwatch.StartNew(); bool allLegal = true;
for (int step = 0; step < 1400; step++)
{
    if (step == 180) stress.TryRequestBridge(0, BridgeRoute.South);
    stress.Step(.05f); allLegal &= stress.Units.All(u => Legal(stress, u));
}
clock.Stop();
Check(allLegal && stress.Units.All(u => u.Position.x > 4 && !u.HasOrder && u.QueuedOrders.Count == 0),
    "All 26 units finish both queued legs through a changing bridge and obstacles without water or wall clipping");
var reusablePath = new List<Vector2>(16); var queryUnit = stress.Units[0];
for (int i = 0; i < 100; i++) stress.BuildOrderRoute(queryUnit, new Vector2(8, 4), new Vector2(16, 4), reusablePath, out _);
long before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++) stress.BuildOrderRoute(queryUnit, new Vector2(8, 4), new Vector2(16, 4), reusablePath, out _);
long routeAllocations = GC.GetAllocatedBytesForCurrentThread() - before;
Check(routeAllocations == 0, "Ten thousand warmed obstacle-route queries allocate no managed memory with a reusable output list");
Check(policyBytes.SequenceEqual(File.ReadAllBytes(args[0])), "No policy bytes change and no training is started");
var result = new { checks = checks.Count, passed = checks, damageProof, stressSimulatedSeconds = stress.Elapsed,
    stressWallMilliseconds = clock.Elapsed.TotalMilliseconds, warmedRouteQueries = 10000, routeAllocatedBytes = routeAllocations,
    stressPendingOrders = stress.Units.Count(u => u.HasOrder), stressQueuedLegs = stress.Units.Sum(u => u.QueuedOrders.Count),
    policySha256 = Convert.ToHexString(SHA256.HashData(policyBytes)).ToLowerInvariant(), trainingStarted = false };
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(checks.Count + " tactical mission checks passed; 26-unit/70-second stress " + clock.Elapsed.TotalMilliseconds.ToString("F1") + "ms; route allocations " + routeAllocations);
foreach (var check in checks) Console.WriteLine(check);
