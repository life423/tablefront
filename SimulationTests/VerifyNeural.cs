using Tablefront;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Random = System.Random;

string policyPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "Training", "trained", "commander-policy.json");
var json = new JsonSerializerOptions { IncludeFields = true };
string policyJson = File.ReadAllText(policyPath);
NeuralCommander.Policy Load() => JsonSerializer.Deserialize<NeuralCommander.Policy>(policyJson, json);
var policy = Load();
int checks = 0;
var lines = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED " + name);
    checks++; lines.Add("PASS " + name);
}
float[] TrainerProbabilities(float[] x)
{
    var h = new float[policy.hiddenCount]; var p = new float[policy.actionCount];
    for (int j = 0; j < h.Length; j++)
    {
        float sum = policy.hiddenBias[j];
        for (int i = 0; i < x.Length; i++) sum += policy.hiddenWeights[j * x.Length + i] * x[i];
        h[j] = MathF.Tanh(sum);
    }
    float max = float.NegativeInfinity;
    for (int a = 0; a < p.Length; a++)
    {
        float sum = policy.outputBias[a];
        for (int j = 0; j < h.Length; j++) sum += policy.outputWeights[a * h.Length + j] * h[j];
        p[a] = sum; max = MathF.Max(max, sum);
    }
    float total = 0;
    for (int a = 0; a < p.Length; a++) { p[a] = MathF.Exp(p[a] - max); total += p[a]; }
    for (int a = 0; a < p.Length; a++) p[a] /= total;
    return p;
}
int TrainerSample(float[] p, Random random)
{
    double sample = random.NextDouble();
    for (int a = 0; a < p.Length; a++) { sample -= p[a]; if (sample <= 0d) return a; }
    return p.Length - 1;
}

NeuralCommander.SetPolicy(policy);
Check(NeuralCommander.Available && NeuralCommander.TrainingEpisodes > 0, "exported learned policy loads in actual runtime inference");
Check(policy.observationCount == 40 && policy.hiddenCount == 32 && policy.actionCount == 10, "runtime model schema40x32x10");
float maxError = 0f;
int mismatches = 0;
var observations = new Random(89483); var runtimeRng = new Random(1301); var trainerRng = new Random(1301);
for (int sample = 0; sample < 10000; sample++)
{
    var x = new float[40];
    for (int i = 0; i < x.Length; i++) x[i] = (float)(observations.NextDouble() * 2d - 1d);
    var expected = TrainerProbabilities(x); var actual = NeuralCommander.PredictProbabilities(x);
    for (int a = 0; a < actual.Length; a++) maxError = Math.Max(maxError, Math.Abs(expected[a] - actual[a]));
    if (TrainerSample(expected, trainerRng) != NeuralCommander.Decide(x, runtimeRng)) mismatches++;
}
Check(maxError < .00001f, "trainer and runtime probability error below1e-5 over10k observations");
Check(mismatches == 0, "trainer and runtime sample identical actions over10k sharedRNG draws");
Check(NeuralCommander.PredictProbabilities(new float[39]) == null && NeuralCommander.Decide(null, runtimeRng) == -1, "invalid observation size rejected");
var badObs = new float[40]; badObs[5] = float.NaN;
Check(NeuralCommander.Decide(badObs, runtimeRng) == -1, "nonfinite observation rejected");
var badPolicy = Load(); badPolicy.hiddenWeights[2] = float.NaN; NeuralCommander.SetPolicy(badPolicy);
Check(!NeuralCommander.Available, "NaN policy weights rejected");
badPolicy = Load(); badPolicy.outputBias[0] = float.PositiveInfinity; NeuralCommander.SetPolicy(badPolicy);
Check(!NeuralCommander.Available, "infinite policy weights rejected");
badPolicy = Load(); badPolicy.outputWeights = new float[3]; NeuralCommander.SetPolicy(badPolicy);
Check(!NeuralCommander.Available, "malformed policy dimensions rejected");
NeuralCommander.SetPolicy(policy);

int games = 12, completed = 0, captures = 0, combats = 0, mobileCommanders = 0;
var rollouts = new List<object>();
for (int game = 0; game < games; game++)
{
    var sim = new BattleSimulation(true, 8101 + game);
    var maxDistance = new float[3];
    int events = 0;
    while (!sim.Finished && sim.Elapsed < 600f)
    {
        sim.Step(.05f); events += sim.Events.Count;
        if (((int)(sim.Elapsed * 20f)) % 20 == 0)
        {
            for (int team = 0; team < 3; team++)
            {
                Troop[] armyUnits = sim.Units.Where(u => u.Team == team).ToArray();
                if (armyUnits.Length == 0) continue;
                Vector2 centre = Vector2.zero;
                foreach (Troop u in armyUnits) centre += u.Position;
                centre /= armyUnits.Length;
                maxDistance[team] = Math.Max(maxDistance[team], Vector2.Distance(centre, sim.Armies[team].BasePosition));
            }
        }
    }
    if (sim.Finished) completed++;
    if (sim.Points.Any(p => p.Owner >= 0)) captures++;
    if (events > 100) combats++;
    mobileCommanders += maxDistance.Count(d => d > 6f);
    rollouts.Add(new
    {
        seed = 8101 + game, finished = sim.Finished, winner = sim.Winner, seconds = sim.Elapsed,
        score = sim.Armies.Select(a => a.Score).ToArray(), units = sim.Armies.Select(a => sim.UnitCount(a.Id)).ToArray(),
        combatEvents = events, maxArmyCentroidDistanceFromHQ = maxDistance
    });
    Check(sim.Units.All(u => float.IsFinite(u.Position.x) && Math.Abs(u.Position.x) <= 24.001f && Math.Abs(u.Position.y) <= 24.001f), "runtime all-neural rollout" + (game + 1) + " maintains board bounds");
}
Check(completed == games, "all12 same-policy neural battles reach normal victory without forced ranking");
Check(captures == games && combats == games, "all12 neural battles capture ground and produce100+ combat events");
Check(mobileCommanders == games * 3, "all36 neural commanders move army centroid6+ units fromHQ");

foreach (string line in lines) Console.WriteLine(line);
Console.WriteLine($"Probability maximum absolute error {maxError:E5}; sampled action mismatches {mismatches}/10000");
Console.WriteLine($"Actual runtime neural rollouts: {completed}/{games} finished, {captures} capture, {combats} combat, {mobileCommanders}/36 mobile commanders");
Console.WriteLine($"PASS {checks} checks");
var report = new { policyPath, trainingEpisodes = policy.trainingEpisodes, checks, maxProbabilityError = maxError, sampledActionMismatches = mismatches, rollouts };
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "neural-runtime-results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
