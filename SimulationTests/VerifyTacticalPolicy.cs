using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tablefront;

// Frozen inference only. No trainer, optimizer or policy writes are linked here.
string policyPath = Path.GetFullPath(args[0]);
string resultPath = Path.GetFullPath(args[1]);
byte[] before = File.ReadAllBytes(policyPath);
var policy = JsonSerializer.Deserialize<NeuralCommander.Policy>(Encoding.UTF8.GetString(before), new JsonSerializerOptions { IncludeFields = true });
NeuralCommander.SetPolicy(policy);
if (!NeuralCommander.Available) throw new Exception("Frozen policy rejected");
var games = new List<object>();
var wins = new int[3];
int finished = 0, timeouts = 0, steps = 0, invalidStates = 0;
for (int match = 0; match < 36; match++)
{
    // The first12 retain the original runtime regression seeds;24 fresh seeds follow.
    int seed = match < 12 ? 8101 + match : 927001 + match;
    var sim = new BattleSimulation(true, seed);
    bool finite = true;
    while (!sim.Finished && sim.Elapsed < 600f)
    {
        sim.Step(.05f); steps++;
        if (sim.Units.Any(u => !float.IsFinite(u.Position.x) || !float.IsFinite(u.Position.y) ||
            !float.IsFinite(u.Health) || MathF.Abs(u.Position.x) > 24.001f || MathF.Abs(u.Position.y) > 24.001f) ||
            sim.Armies.Any(a => !float.IsFinite(a.Score) || !float.IsFinite(a.Credits) || a.Credits < 0 || sim.UnitCount(a.Id) > BattleSimulation.UnitCap))
        { finite = false; invalidStates++; break; }
    }
    if (sim.Finished && sim.Winner >= 0) { finished++; wins[sim.Winner]++; }
    else if (finite) timeouts++;
    games.Add(new { seed, normallyFinished = sim.Finished, timedOut = !sim.Finished && finite,
        winner = sim.Finished ? sim.Winner : -1, seconds = sim.Elapsed, finiteThroughout = finite,
        scores = sim.Armies.Select(a => a.Score).ToArray(),
        captures = sim.Armies.Select(a => a.BeaconsCaptured).ToArray(),
        kills = sim.Armies.Select(a => a.UnitsDestroyed).ToArray(),
        losses = sim.Armies.Select(a => a.UnitsLost).ToArray() });
}
bool policyUnchanged = before.SequenceEqual(File.ReadAllBytes(policyPath));
var report = new {
    kind = "frozen-policy tactical-rules runtime regression; not training or a strength benchmark",
    policySha256 = Convert.ToHexString(SHA256.HashData(before)).ToLowerInvariant(),
    trainingEpisodes = policy.trainingEpisodes, observationCount = policy.observationCount, actionCount = policy.actionCount,
    gameCount = games.Count, commanderParticipations = games.Count * 3, normallyFinished = finished,
    timeouts, invalidStates, fixedSteps = steps, winsBySeat = wins, policyUnchanged,
    rules = new { scoutCaptureWeight = BattleSimulation.ScoutCaptureWeight, rifleCoverDamageMultiplier = BattleSimulation.RifleCoverDamageMultiplier, tankArmorDamageMultiplier = BattleSimulation.TankArmorDamageMultiplier },
    note = "All three seats use the same frozen policy through the actual runtime decision timers. Timeouts receive no invented winner. These samples show liveness and bounded state, not human fun, fair win rates or cover-aware neural tactics.", games
};
File.WriteAllText(resultPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n"));
Console.WriteLine($"Frozen tactical regression: {finished}/36 normal finishes, {timeouts} timeouts, {invalidStates} invalid states across {steps} steps; wins [{string.Join(",",wins)}]; policy unchanged={policyUnchanged}");
if (!policyUnchanged || invalidStates != 0 || finished != 36 || timeouts != 0) throw new Exception("Frozen tactical runtime regression failed; see report");
