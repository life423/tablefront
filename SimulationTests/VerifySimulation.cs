using Tablefront;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

int checks = 0;
var lines = new List<string>();
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++;
    lines.Add("PASS " + message);
}
void Advance(BattleSimulation simulation, float seconds)
{
    for (int i = 0; i < seconds * 20f; i++) simulation.Step(.05f);
}
BattleSimulation Manual(int seed = 42)
{
    var result = new BattleSimulation(false, seed) { UseExternalAI = true };
    foreach (Army army in result.Armies) army.IsAI = false;
    return result;
}

var sim = Manual();
Check(sim.Units.Count == 18 && sim.Armies.Count == 3 && sim.Points.Count == 3, "initial field has 3 armies, 18 miniatures, 3 control points");
Check(sim.Armies.All(a => sim.UnitCount(a.Id) == 6), "equal initial army counts");
Check(sim.Armies.All(a => a.BaseHealth == BattleSimulation.BaseMaxHealth && a.Credits == 170f), "equal initial health and credits");
Check(!sim.Finished && sim.Winner == -1 && sim.Elapsed == 0f, "fresh match reset state");
Check(!new BattleSimulation(false).Armies[0].IsAI && new BattleSimulation(true).Armies.All(a => a.IsAI), "human and spectator ownership");

foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
{
    sim = Manual();
    sim.Armies[0].Credits = Mathf.Max(sim.Armies[0].Credits, BattleSimulation.Cost(kind));
    float oldCredits = sim.Armies[0].Credits;
    int oldCount = sim.UnitCount(0);
    Check(sim.Recruit(0, kind), kind + " recruitment succeeds with full cost available");
    Check(Math.Abs(sim.Armies[0].Credits - oldCredits + BattleSimulation.Cost(kind)) < .001f && sim.UnitCount(0) == oldCount + 1, kind + " recruitment pays exact cost and creates one troop");
    Check(!sim.Recruit(0, UnitKind.Scout), kind + " recruitment cooldown blocks repeat purchase");
}
Check(!sim.Recruit(-1, UnitKind.Scout) && !sim.Recruit(8, UnitKind.Tank) && !sim.Recruit(0, (UnitKind)22), "invalid recruits rejected");
sim = Manual();
sim.Armies[0].Credits = 0f;
Check(!sim.Recruit(0, UnitKind.Scout), "unaffordable purchase rejected");
Advance(sim, 4);
Check(sim.Armies[0].Credits >= 23.99f, "passive income uses shared six credits per second");
while (sim.UnitCount(0) < BattleSimulation.UnitCap)
{
    sim.Armies[0].Credits = 999f; sim.Armies[0].RecruitCooldown = 0f;
    if (!sim.Recruit(0, UnitKind.Scout)) throw new Exception("Cap setup recruit failed");
}
sim.Armies[0].Credits = 999f; sim.Armies[0].RecruitCooldown = 0f;
Check(!sim.Recruit(0, UnitKind.Scout) && sim.UnitCount(0) == BattleSimulation.UnitCap, "unit cap26 enforced");

sim = Manual();
Troop troop = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
Vector2 origin = troop.Position;
sim.OrderMove(new[] { troop.Id }, new Vector2(0, -10));
Advance(sim, 2);
Check(Vector2.Distance(troop.Position, origin) > 5f, "movement responds to issued destination");
sim.OrderMove(new[] { troop.Id }, new Vector2(float.NaN, 10));
Check(!float.IsNaN(troop.Destination.x), "NaN order rejected");
sim.OrderMove(new[] { troop.Id }, new Vector2(float.PositiveInfinity, 10));
Check(!float.IsInfinity(troop.Destination.x), "infinite order rejected");
sim.OrderMove(new[] { troop.Id }, new Vector2(100, 100));
Check(troop.Destination.x <= 24f && troop.Destination.y <= 24f, "destinations clamped inside board");
Advance(sim, 25);
Check(sim.Units.All(u => Math.Abs(u.Position.x) <= 24.001f && Math.Abs(u.Position.y) <= 24.001f), "movement and separation remain inside board");

sim = Manual();
Troop capture = sim.Units.First(u => u.Team == 0);
capture.Position = sim.Points[0].Position; capture.Destination = capture.Position;
Advance(sim, 9);
Check(sim.Points[0].Owner == 0 && sim.OwnedPoints(0) == 1, "physical occupation captures a control point");
float oldScore = sim.Armies[0].Score, oldIncome = sim.Armies[0].Credits;
Advance(sim, 2);
Check(sim.Armies[0].Score > oldScore + 2f && sim.Armies[0].Credits > oldIncome + 17f, "controlled point adds score and extra income");
Troop challenger = sim.Units.First(u => u.Team == 1);
challenger.Position = sim.Points[0].Position + new Vector2(2.9f, 0); challenger.Destination = challenger.Position;
sim.Step(.05f);
Check(sim.Points[0].Contested, "enemy occupation contests a point");
oldScore = sim.Armies[0].Score;
sim.Step(.05f);
Check(sim.Armies[0].Score == oldScore, "contested point awards no victory score");

sim = Manual();
Troop shooter = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Tank);
Troop victim = sim.Units.First(u => u.Team == 1 && u.Kind == UnitKind.Scout);
shooter.Position = Vector2.zero; shooter.Destination = shooter.Position;
victim.Position = new Vector2(2, 0); victim.Destination = victim.Position;
float oldHealth = victim.Health;
Advance(sim, 2);
Check(victim.Health < oldHealth, "actual weapon resolution damages opposing troop");
Advance(sim, 5);
Check(!victim.Alive && !sim.Units.Contains(victim), "lethal damage removes dead troop");

for (int action = 0; action < 3; action++)
{
    sim = Manual();
    sim.ApplyAction(0, action);
    var friendly = sim.Units.Where(u => u.Team == 0).ToArray();
    Check(friendly.All(u => u.HasOrder && Vector2.Distance(u.Destination, sim.Points[action].Position) < 3f), "neural action " + action + " orders formation to matching point");
}
for (int action = 3; action <= 4; action++)
{
    sim = Manual();
    int targetTeam = action - 2;
    // Isolate HQ targeting from the legitimate low-level priority to shoot nearby defenders.
    sim.Units.RemoveAll(u => u.Team == targetTeam);
    shooter = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Tank);
    shooter.Position = sim.Armies[targetTeam].BasePosition + new Vector2(0, -7f); shooter.Destination = shooter.Position;
    sim.ApplyAction(0, action);
    oldHealth = sim.Armies[targetTeam].BaseHealth;
    Advance(sim, 3);
    Check(sim.Armies[targetTeam].BaseHealth < oldHealth, "neural HQ action " + action + " fires at correct enemy headquarters");
    sim.Armies[targetTeam].BaseHealth = 1f;
    sim.Points[0].Owner = targetTeam; sim.Points[0].CapturingTeam = targetTeam; sim.Points[0].CaptureProgress = 1f;
    Advance(sim, 2);
    Check(!sim.Armies[targetTeam].Alive && sim.UnitCount(targetTeam) == 0 && sim.Points[0].Owner == -1, "HQ elimination removes army and releases territory for action " + action);
}
sim = Manual();
sim.ApplyAction(0, 5);
Check(sim.Units.Where(u => u.Team == 0).All(u => Vector2.Distance(u.Destination, sim.Armies[0].BasePosition) < 7f), "neural defend action orders home formation");
foreach (int action in new[] { 6, 7, 8 })
{
    sim = Manual(); int previous = sim.UnitCount(0); float wallet = sim.Armies[0].Credits;
    sim.ApplyAction(0, action);
    UnitKind expected = (UnitKind)(action - 6);
    Check(sim.UnitCount(0) == previous + 1 && Math.Abs(wallet - sim.Armies[0].Credits - BattleSimulation.Cost(expected)) < .001f, "neural recruit action " + action + " respects shared purchase rules");
}
sim = Manual();
Vector2 destination = sim.Units[0].Destination;
sim.ApplyAction(0, 9);
Check(sim.Units[0].Destination == destination && !sim.Units[0].HasOrder, "neural hold action leaves current orders unchanged");

sim = Manual();
sim.Points[0].Owner = 0; sim.Points[1].Owner = 1; sim.Points[2].Owner = 2;
sim.Points[0].CapturingTeam = 0; sim.Points[0].CaptureProgress = .6f;
sim.Armies[0].Credits = 300f;
float[] obs = sim.GetObservation(0);
Check(obs.Length == 40 && BattleSimulation.ActionCount == 10, "neural dimensions40 observations10 actions");
Check(obs.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && x >= -1.001f && x <= 1.001f), "all observation components finite and normalized");
Check(obs[0] == 1f && obs[1] == 1f && Math.Abs(obs[3] - 6f / 26f) < .0001f && obs[7] == 1f, "own army feature indices match trainer contract");
Check(obs[16] == 1f && obs[24] == 1f && obs[32] == 1f && Math.Abs(obs[19] - .6f) < .0001f, "owner flags and signed capture indices match trainer contract");
Check(sim.GetObservation(-1).Length == 40 && sim.GetObservation(-1).All(x => x == 0f), "invalid observer has safe zero vector");

sim = Manual(); sim.Armies[0].Score = BattleSimulation.VictoryScore;
sim.Step(.05f);
Check(sim.Finished && sim.Winner == 0, "350score produces victory");
float frozen = sim.Elapsed; sim.Step(1f);
Check(sim.Elapsed == frozen, "finished simulation freezes");
sim = Manual(); sim.Armies[1].BaseHealth = 0f; sim.Armies[2].BaseHealth = 0f; sim.Step(.05f);
Check(sim.Finished && sim.Winner == 0, "last headquarters produces victory");
sim = Manual(); sim.Step(float.NaN); sim.Step(float.PositiveInfinity); sim.Step(-1f);
Check(sim.Elapsed == 0f && !sim.Finished, "invalid time deltas do not corrupt state");

var a = Manual(19); var b = Manual(19);
var timer = Stopwatch.StartNew();
int totalSteps = 0, totalEvents = 0;
for (int tick = 0; tick < 2400; tick++)
{
    if (tick % 160 == 0)
    {
        for (int team = 0; team < 3; team++)
        {
            int action = (tick / 160 + team) % 3;
            a.ApplyAction(team, action); b.ApplyAction(team, action);
        }
    }
    if (tick % 80 == 0)
        for (int team = 0; team < 3; team++)
        { int recruit = 6 + (tick / 80 % 3); a.ApplyAction(team, recruit); b.ApplyAction(team, recruit); }
    a.Step(.05f); b.Step(.05f); totalSteps += 2; totalEvents += a.Events.Count;
}
timer.Stop();
Check(a.Units.Count == b.Units.Count && a.Units.Zip(b.Units).All(pair => pair.First.Position == pair.Second.Position && pair.First.Health == pair.Second.Health), "seeded exact simulation with identical strategic actions is deterministic");
Check(a.Armies.Zip(b.Armies).All(pair => pair.First.Score == pair.Second.Score && pair.First.BaseHealth == pair.Second.BaseHealth), "deterministic army score and HQ damage");
Check(totalEvents > 30, "120second external-actions soak contains real combat");
Check(a.Units.All(u => Math.Abs(u.Position.x) <= 24.001f && Math.Abs(u.Position.y) <= 24.001f && !float.IsNaN(u.Position.x)), "soak maintains finite bounded troop positions");
Check(a.Armies.All(army => a.UnitCount(army.Id) <= BattleSimulation.UnitCap && army.Credits >= 0f), "soak maintains army caps and nonnegative credits");
Check(a.Armies.All(army => a.GetObservation(army.Id).All(x => float.IsFinite(x))), "soak observations remain finite");
var reset = new BattleSimulation(false, 19);
Check(reset.Units.Count == 18 && reset.Armies.All(army => army.Score == 0f && army.Credits == 170f) && reset.Points.All(p => p.Owner == -1), "new match cleanly resets spent, contested, and damaged run state");

Console.WriteLine("Exact Unity-math mechanics verification (CoreModule.dll; no shim, no learned-policy claims)");
foreach (string line in lines) Console.WriteLine(line);
Console.WriteLine($"Throughput: {totalSteps} fixed .05s steps in {timer.Elapsed.TotalSeconds:F3}s = {totalSteps / timer.Elapsed.TotalSeconds:F0} steps/s; combat events {totalEvents}; final unit count {a.Units.Count}");
Console.WriteLine($"PASS: {checks} checks");
