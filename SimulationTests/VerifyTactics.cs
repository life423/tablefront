using System;
using System.Collections.Generic;
using System.Linq;
using Tablefront;
using UnityEngine;

int checks = 0;
var lines = new List<string>();
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++; lines.Add("PASS " + message);
}
BattleSimulation Empty(bool ai = false)
{
    var sim = new BattleSimulation(false, 914) { UseExternalAI = true };
    foreach (var army in sim.Armies) army.IsAI = ai;
    sim.Units.Clear();
    return sim;
}
Troop Unit(BattleSimulation sim, int id, int team, UnitKind kind, Vector2 position, float health = 100)
{
    var unit = new Troop { Id = id, Team = team, Kind = kind, Position = position,
        Destination = position, Health = health, MaxHealth = health, WeaponTimer = 100 };
    sim.Units.Add(unit); return unit;
}
void Advance(BattleSimulation sim, float seconds)
{
    for (int i = 0; i < MathF.Ceiling(seconds / .05f); i++) sim.Step(.05f);
}
float RifleHit(bool cover, bool ai, UnitKind targetKind = UnitKind.Rifle)
{
    var sim = Empty(ai);
    Vector2 position = cover ? sim.CoverZones[4].Position : Vector2.zero;
    var attacker = Unit(sim, 1, 0, UnitKind.Rifle, position + new Vector2(2, 0));
    var victim = Unit(sim, 2, 1, targetKind, position);
    attacker.WeaponTimer = 0;
    sim.Step(.05f);
    return victim.MaxHealth - victim.Health;
}

var sim = Empty();
Check(sim.CoverZones.Count == 6 && sim.CoverZones.All(z => z.Radius == 2.2f), "six consistent passable cover zones flank the beacons");
sim.Recruit(0, UnitKind.Scout);
Check(sim.Armies[0].ReinforcementsDeployed == 1 && sim.Armies[1].ReinforcementsDeployed == 0, "reinforcement recap excludes initial deployment and records only paid recruits");
Check(sim.CoverZones.All(z => MathF.Abs(z.Position.x) + z.Radius < 24 && MathF.Abs(z.Position.y) + z.Radius < 24), "all cover zones fit inside the playable board");
Check(sim.IsInCover(sim.CoverZones[0].Position) && !sim.IsInCover(Vector2.zero), "cover membership uses actual terrain extent");
Check(!sim.IsInCover(new Vector2(float.NaN, 0)) && !sim.IsProtected(null), "invalid cover queries safely reject");
float open = RifleHit(false, false), protectedHit = RifleHit(true, false);
Check(MathF.Abs(open - 11.5f) < .0001f, "open-ground rifle hit retains original weapon damage");
Check(MathF.Abs(protectedHit / open - .65f) < .0001f, "rifle cover reduces resolved direct damage by35percent");
Check(MathF.Abs(RifleHit(true, false, UnitKind.Tank) - open) < .0001f && MathF.Abs(RifleHit(true, false, UnitKind.Scout) - open) < .0001f, "cover protection is the rifle role, not hidden universal armour");
Check(RifleHit(true, true) == protectedHit && RifleHit(false, true) == open, "human and AI ownership receive identical cover and weapon rules");

sim = Empty();
var tank = Unit(sim, 1, 0, UnitKind.Tank, Vector2.zero);
var otherTank = Unit(sim, 2, 1, UnitKind.Tank, new Vector2(4, 0));
tank.WeaponTimer = 0;
sim.Step(.05f);
Check(MathF.Abs(100 - otherTank.Health - 38.4f) < .0001f, "tank shell resolves20percent anti-armour bonus against tanks");
Check(sim.Events.Count == 1 && sim.Events[0].ShooterId == tank.Id && sim.Events[0].WeaponKind == UnitKind.Tank, "combat events identify the actual shooter and weapon for presentation");

float CaptureProgress(UnitKind kind, bool ai)
{
    var world = Empty(ai);
    Unit(world, 1, 0, kind, world.Points[0].Position);
    Advance(world, 2);
    return world.Points[0].CaptureProgress;
}
float rifleCapture = CaptureProgress(UnitKind.Rifle, false), scoutCapture = CaptureProgress(UnitKind.Scout, false);
Check(MathF.Abs(scoutCapture / rifleCapture - MathF.Sqrt(1.5f)) < .001f, "scout capture weight improves progress through shared square-root capture rule");
Check(CaptureProgress(UnitKind.Scout, true) == scoutCapture, "AI scouts gain the same capture role as player scouts");
sim = Empty();
Unit(sim, 1, 0, UnitKind.Scout, sim.Points[0].Position);
Advance(sim, 8);
Check(sim.Points[0].Owner == 0 && sim.Armies[0].BeaconsCaptured == 1 && sim.Armies[0].BeaconSeconds > 0, "capture and uncontested control time populate the battle recap");
Check(sim.LastTurningPoint == "COBALT captured WEST" && sim.LastTurningPointTime > 0, "recap records a real capture and its simulation time");
float ownedTime = sim.Armies[0].BeaconSeconds;
Unit(sim, 2, 1, UnitKind.Rifle, sim.Points[0].Position + new Vector2(2.8f, 0));
sim.Step(.05f);
Check(sim.Points[0].Contested && sim.Armies[0].BeaconSeconds == ownedTime, "contested time is excluded from productive beacon control");

sim = Empty();
tank = Unit(sim, 1, 0, UnitKind.Tank, Vector2.zero);
var primary = Unit(sim, 2, 1, UnitKind.Scout, new Vector2(4, 0), 1);
var splash = Unit(sim, 3, 1, UnitKind.Rifle, new Vector2(4, 1), 1);
tank.AttackTargetId = primary.Id; tank.WeaponTimer = 0;
sim.Step(.05f);
Check(!primary.Alive && !splash.Alive && sim.Armies[0].UnitsDestroyed == 2 && sim.Armies[1].UnitsLost == 2, "direct and splash kills are counted once each");
sim.Step(.05f);
Check(sim.Armies[0].UnitsDestroyed == 2 && sim.Armies[1].UnitsLost == 2, "removed casualties are not counted a second time");

float SplashHit(bool cover)
{
    var world = Empty();
    Vector2 position = cover ? world.CoverZones[4].Position : Vector2.zero;
    var gun = Unit(world, 1, 0, UnitKind.Tank, position + new Vector2(5, 0));
    var aimed = Unit(world, 2, 1, UnitKind.Scout, position + new Vector2(0, -1));
    var secondary = Unit(world, 3, 1, UnitKind.Rifle, position);
    gun.AttackTargetId = aimed.Id; gun.WeaponTimer = 0;
    world.Step(.05f);
    return secondary.MaxHealth - secondary.Health;
}
Check(MathF.Abs(SplashHit(true) / SplashHit(false) - .65f) < .001f, "rifle cover also reduces splash damage through the same resolution path");

sim = Empty();
Unit(sim, 1, 0, UnitKind.Scout, sim.Points[0].Position);
Unit(sim, 2, 1, UnitKind.Tank, sim.Points[0].Position + new Vector2(2, 0));
sim.Step(.05f);
Check(sim.Points[0].Contested && sim.Points[0].CaptureProgress == 0, "extra scout capture weight cannot override an enemy contest");

sim = Empty();
tank = Unit(sim, 1, 0, UnitKind.Tank, sim.Armies[1].BasePosition + new Vector2(0, -7));
var survivor = Unit(sim, 2, 1, UnitKind.Rifle, new Vector2(-10, 12));
sim.Armies[1].BaseHealth = 1;
tank.WeaponTimer = 0;
sim.Step(.05f);
Check(!sim.Armies[1].Alive && !survivor.Alive && sim.Armies[1].UnitsLost == 1 && sim.Armies[0].UnitsDestroyed == 0, "HQ collapse counts disbanded losses without inventing enemy troop kills");
Check(sim.LastTurningPoint == "COBALT destroyed EMBER HQ", "HQ destruction supersedes an earlier turning point");

sim = Empty();
var mover = Unit(sim, 1, 0, UnitKind.Rifle, sim.CoverZones[0].Position - new Vector2(4, 0));
Vector2 destination = sim.CoverZones[0].Position + new Vector2(4, 0);
sim.OrderMove(new[] { mover.Id }, destination, false);
Advance(sim, 5);
Check(Vector2.Distance(mover.Position, destination) < .3f, "cover terrain remains traversable without invisible barriers");
var reset = new BattleSimulation(false);
Check(reset.Armies.All(a => a.UnitsLost == 0 && a.UnitsDestroyed == 0 && a.BeaconsCaptured == 0 && a.BeaconSeconds == 0 && a.ReinforcementsDeployed == 0) && reset.LastTurningPointTime == 0, "all recap counters reset with a fresh battle");
Check(BattleSimulation.ObservationCount == 40 && BattleSimulation.ActionCount == 10, "frozen neural interface remains40 observations and10 actions");

Console.WriteLine("Tactical mechanics verification against actual Unity CoreModule math; no training");
foreach (string line in lines) Console.WriteLine(line);
Console.WriteLine($"PASS: {checks} tactical checks");
