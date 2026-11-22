using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Tablefront;
using UnityEngine;
using Random = System.Random;

// PPO self-play trains against the exact linked runtime BattleSimulation.cs.
// No imitation labels, scripted decisions, engine rendering, or external package is used.
int episodes = ReadInt("--episodes", 1024), seed = ReadInt("--seed", 20261122);
string output = ReadString("--output", Path.Combine(AppContext.BaseDirectory, "training-output"));
string resume = ReadString("--resume", "");
int batchEpisodes = ReadInt("--batch", 16), maxDecisions = ReadInt("--horizon", 180);
int evalGames = ReadInt("--eval-games", 24);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
var random = new Random(seed);
var model = string.IsNullOrWhiteSpace(resume) ? new Model(random) : JsonSerializer.Deserialize<Model>(File.ReadAllText(resume), json)!;
model.EnsureOptimizer();
var untrained = new Model(new Random(seed));
File.WriteAllText(Path.Combine(output, "initial-policy.json"), JsonSerializer.Serialize(untrained.Export(), json));
var clock = Stopwatch.StartNew();
var metricsPath = Path.Combine(output, "training.csv");
using var metrics = new StreamWriter(metricsPath, false);
metrics.WriteLine("episodes,decisions,mean_episode_reward,mean_score,mean_units,mean_entropy,policy_loss,value_loss,approx_kl,seconds");
long totalDecisions = 0;
float bestReward = float.NegativeInfinity;
Console.WriteLine($"PPO self-play: seed={seed}, episodes={episodes}, horizon={maxDecisions}, batch={batchEpisodes}, network={Model.I}-{Model.H}-{Model.A}");

for (int first = 0; first < episodes; first += batchEpisodes)
{
    var samples = new List<Transition>(batchEpisodes * maxDecisions * 3);
    double rewardSum = 0, scoreSum = 0, unitsSum = 0;
    int count = Math.Min(batchEpisodes, episodes - first);
    for (int ep = 0; ep < count; ep++)
    {
        var sim = new BattleSimulation(true, seed + first + ep) { UseExternalAI = true };
        var paths = new[] { new List<Transition>(), new List<Transition>(), new List<Transition>() };
        for (int decision = 0; decision < maxDecisions && !sim.Finished; decision++)
        {
            var before = Snapshot(sim);
            for (int team = 0; team < 3; team++)
            {
                if (!sim.Armies[team].Alive) continue;
                var obs = sim.GetObservation(team);
                var prediction = model.Forward(obs);
                int action = Sample(prediction.P, random);
                paths[team].Add(new Transition { X = obs, Action = action, OldLog = MathF.Log(MathF.Max(1e-9f, prediction.P[action])), OldValue = prediction.Value });
                sim.ApplyAction(team, action);
            }
            sim.Step(1.65f);
            var after = Snapshot(sim);
            bool done = sim.Finished || decision == maxDecisions - 1;
            int winner = sim.Finished ? sim.Winner : RankWinner(sim);
            for (int team = 0; team < 3; team++)
            {
                if (paths[team].Count == 0 || !before[team].Alive) continue;
                var step = paths[team][^1];
                step.Reward = ShapedReward(before, after, team);
                if (done) step.Reward += winner == team ? 4f : -2f;
                if (before[team].Alive && !after[team].Alive) step.Reward -= 2f;
                rewardSum += step.Reward;
            }
        }
        for (int team = 0; team < 3; team++)
        {
            // GAE uses zero bootstrap at episode truncation: finite-horizon ranking is part of this training task.
            float gae = 0, nextValue = 0;
            for (int k = paths[team].Count - 1; k >= 0; k--)
            {
                var sample = paths[team][k];
                float delta = sample.Reward + .995f * nextValue - sample.OldValue;
                gae = delta + .995f * .95f * gae;
                sample.Advantage = gae;
                sample.Return = gae + sample.OldValue;
                nextValue = sample.OldValue;
                samples.Add(sample);
            }
            scoreSum += sim.Armies[team].Score;
            unitsSum += sim.UnitCount(team);
        }
    }
    totalDecisions += samples.Count;
    NormalizeAdvantages(samples);
    float progress = (first + count) / (float)episodes;
    var loss = model.Optimize(samples, random, .0007f * (1f - .6f * progress));
    model.TrainingEpisodes += count;
    float meanReward = (float)(rewardSum / (count * 3));
    if (meanReward > bestReward) bestReward = meanReward;
    metrics.WriteLine(string.Join(",", model.TrainingEpisodes, totalDecisions, F(meanReward), F(scoreSum / (count * 3)), F(unitsSum / (count * 3)), F(loss.Entropy), F(loss.Policy), F(loss.Value), F(loss.Kl), F(clock.Elapsed.TotalSeconds)));
    metrics.Flush();
    File.WriteAllText(Path.Combine(output, "checkpoint.json"), JsonSerializer.Serialize(model, json));
    File.WriteAllText(Path.Combine(output, "commander-policy.json"), JsonSerializer.Serialize(model.Export(), json));
    Console.WriteLine($"episodes {model.TrainingEpisodes}, decisions {totalDecisions}, score {scoreSum / (count * 3):F1}, reward {meanReward:F2}, entropy {loss.Entropy:F2}, {clock.Elapsed.TotalSeconds:F1}s");
}

Console.WriteLine("Evaluating held-out seeds with seats rotated; policies sample actions identically to runtime.");
var evalRandom = Evaluate(model, untrained, evalGames, seed + 1000000, true);
var evalInitial = Evaluate(model, untrained, evalGames, seed + 2000000, false);
var initialVsRandom = Evaluate(untrained, untrained, evalGames, seed + 3000000, true);
var report = new
{
    algorithm = "PPO clipped surrogate + shared neural value baseline, GAE(lambda=.95), Adam, self-play",
    seed, trainedEpisodes = model.TrainingEpisodes, thisRunEpisodes = episodes, totalDecisions,
    observationCount = Model.I, hiddenCount = Model.H, actionCount = Model.A,
    gamma = .995, clip = .2, entropyCoefficient = .015, valueCoefficient = .5,
    batchEpisodes, maxDecisions, timestep = .05, decisionInterval = 1.65,
    trainingSeconds = clock.Elapsed.TotalSeconds,
    trainedVersusRandom = evalRandom, trainedVersusUntrainedNetwork = evalInitial, untrainedVersusRandom = initialVsRandom,
    note = "Evaluation uses unseen simulation seeds and rotated seats, not training outcomes. A finite horizon ranks scores if no normal win occurred. These small samples do not establish strong RTS play."
};
File.WriteAllText(Path.Combine(output, "evaluation.json"), JsonSerializer.Serialize(report, json));
Console.WriteLine(JsonSerializer.Serialize(report, json));

int ReadInt(string key, int fallback) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1], CultureInfo.InvariantCulture) : fallback; }
string ReadString(string key, string fallback) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
static string F(double x) => x.ToString("0.00000", CultureInfo.InvariantCulture);
static int Sample(float[] p, Random random) { double value = random.NextDouble(); for (int i = 0; i < p.Length; i++) { value -= p[i]; if (value <= 0) return i; } return p.Length - 1; }
static int RankWinner(BattleSimulation sim)
{
    return Enumerable.Range(0, 3).OrderByDescending(i => sim.Armies[i].Score + (sim.Armies[i].Alive ? .001f : 0)).First();
}
static Frame[] Snapshot(BattleSimulation sim)
{
    var frames = new Frame[3];
    for (int i = 0; i < 3; i++)
    {
        var a = sim.Armies[i];
        float progress = 0, proximity = 0;
        foreach (var p in sim.Points)
        {
            if (p.CapturingTeam == i) progress += p.CaptureProgress;
            float nearest = 45;
            foreach (var u in sim.Units) if (u.Alive && u.Team == i) nearest = MathF.Min(nearest, Vector2.Distance(u.Position, p.Position));
            proximity += MathF.Max(0, 20 - nearest) / 20f;
        }
        frames[i] = new Frame { Score = a.Score, BaseHp = a.BaseHealth, Points = sim.OwnedPoints(i), Progress = progress, Proximity = proximity, Alive = a.Alive };
    }
    return frames;
}
static float ShapedReward(Frame[] before, Frame[] after, int team)
{
    float ownScore = after[team].Score - before[team].Score;
    float enemyScore = 0, damage = 0;
    for (int i = 0; i < 3; i++) if (i != team) { enemyScore += after[i].Score - before[i].Score; damage += MathF.Max(0, before[i].BaseHp - after[i].BaseHp); }
    // Dense reward is a difference of territory/proximity potentials, plus objective progress.
    return .035f * ownScore - .012f * enemyScore
        + .3f * (after[team].Points - before[team].Points)
        + .12f * (after[team].Progress - before[team].Progress)
        + .1f * (after[team].Proximity - before[team].Proximity)
        + .0005f * damage - .001f * MathF.Max(0, before[team].BaseHp - after[team].BaseHp);
}
static void NormalizeAdvantages(List<Transition> samples)
{
    float mean = samples.Average(s => s.Advantage);
    float deviation = MathF.Sqrt(samples.Average(s => (s.Advantage - mean) * (s.Advantage - mean)) + 1e-7f);
    foreach (var sample in samples) sample.Advantage = (sample.Advantage - mean) / deviation;
}
static Evaluation Evaluate(Model trained, Model initial, int games, int seed, bool randomOpponents)
{
    int wins = 0, actualEnds = 0;
    double ownScore = 0, opponentScore = 0;
    var decisions = new int[Model.A];
    for (int match = 0; match < games; match++)
    {
        int seat = match % 3;
        var rng = new Random(seed + match);
        var sim = new BattleSimulation(true, seed + match) { UseExternalAI = true };
        for (int step = 0; step < 240 && !sim.Finished; step++)
        {
            for (int team = 0; team < 3; team++)
            {
                if (!sim.Armies[team].Alive) continue;
                int action = team == seat ? Sample(trained.Forward(sim.GetObservation(team)).P, rng) : randomOpponents ? rng.Next(Model.A) : Sample(initial.Forward(sim.GetObservation(team)).P, rng);
                if (team == seat) decisions[action]++;
                sim.ApplyAction(team, action);
            }
            sim.Step(1.65f);
        }
        int winner = sim.Finished ? sim.Winner : RankWinner(sim);
        if (winner == seat) wins++;
        if (sim.Finished) actualEnds++;
        ownScore += sim.Armies[seat].Score;
        opponentScore += sim.Armies.Where(a => a.Id != seat).Average(a => a.Score);
    }
    return new Evaluation { games = games, wins = wins, winRate = wins / (float)games, meanScore = (float)(ownScore / games), meanOpponentScore = (float)(opponentScore / games), normallyFinished = actualEnds, actionCounts = decisions };
}

sealed class Frame { public float Score, BaseHp, Progress, Proximity; public int Points; public bool Alive; }
sealed class Transition { public float[] X; public int Action; public float OldLog, OldValue, Reward, Advantage, Return; }
sealed class Evaluation { public int games, wins, normallyFinished; public float winRate, meanScore, meanOpponentScore; public int[] actionCounts; }
sealed class Prediction { public float[] Hidden, P; public float Value; }
sealed class Loss { public float Policy, Value, Entropy, Kl; }

public sealed class PolicyFile
{
    public int observationCount = Model.I, hiddenCount = Model.H, actionCount = Model.A, trainingEpisodes;
    public float[] hiddenWeights, hiddenBias, outputWeights, outputBias;
    public string algorithm = "PPO self-play";
}

public sealed class Model
{
    public const int I = 40, H = 32, A = 10;
    const int HW = 0, HB = H * I, OW = HB + H, OB = OW + A * H, VW = OB + A, VB = VW + H, N = VB + 1;
    public float[] Parameters = new float[N], FirstMoment = new float[N], SecondMoment = new float[N];
    public int Updates, TrainingEpisodes;
    public Model() { }
    public Model(Random random)
    {
        for (int i = HW; i < HB; i++) Parameters[i] = Uniform(random) * MathF.Sqrt(6f / (I + H));
        for (int i = OW; i < OB; i++) Parameters[i] = Uniform(random) * .025f;
        for (int i = VW; i < VB; i++) Parameters[i] = Uniform(random) * .1f;
    }
    static float Uniform(Random random) => (float)random.NextDouble() * 2 - 1;
    public void EnsureOptimizer() { if (FirstMoment?.Length != N) FirstMoment = new float[N]; if (SecondMoment?.Length != N) SecondMoment = new float[N]; }
    internal Prediction Forward(float[] x)
    {
        var h = new float[H];
        var p = new float[A];
        for (int j = 0; j < H; j++) { float sum = Parameters[HB + j]; for (int i = 0; i < I; i++) sum += Parameters[HW + j * I + i] * x[i]; h[j] = MathF.Tanh(sum); }
        float max = float.NegativeInfinity;
        for (int a = 0; a < A; a++) { float sum = Parameters[OB + a]; for (int j = 0; j < H; j++) sum += Parameters[OW + a * H + j] * h[j]; p[a] = sum; max = MathF.Max(max, sum); }
        float total = 0;
        for (int a = 0; a < A; a++) { p[a] = MathF.Exp(p[a] - max); total += p[a]; }
        for (int a = 0; a < A; a++) p[a] /= total;
        float value = Parameters[VB]; for (int j = 0; j < H; j++) value += Parameters[VW + j] * h[j];
        return new Prediction { Hidden = h, P = p, Value = value };
    }
    internal Loss Optimize(List<Transition> samples, Random random, float rate)
    {
        var result = new Loss();
        var gradient = new float[N];
        int total = 0;
        var order = Enumerable.Range(0, samples.Count).ToArray();
        for (int epoch = 0; epoch < 4; epoch++)
        {
            for (int i = order.Length - 1; i > 0; i--) { int j = random.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int first = 0; first < order.Length; first += 128)
            {
                Array.Clear(gradient);
                int count = Math.Min(128, order.Length - first);
                for (int n = 0; n < count; n++)
                {
                    var sample = samples[order[first + n]];
                    var prediction = Forward(sample.X);
                    float log = MathF.Log(MathF.Max(1e-9f, prediction.P[sample.Action]));
                    float ratio = MathF.Exp(Math.Clamp(log - sample.OldLog, -10, 10));
                    float clippedRatio = Math.Clamp(ratio, .8f, 1.2f);
                    bool clipped = sample.Advantage > 0 ? ratio > 1.2f : ratio < .8f;
                    float actor = clipped ? 0 : -sample.Advantage * ratio;
                    float entropy = 0;
                    for (int a = 0; a < A; a++) entropy -= prediction.P[a] * MathF.Log(MathF.Max(1e-9f, prediction.P[a]));
                    var dh = new float[H];
                    for (int a = 0; a < A; a++)
                    {
                        float dlogit = actor * ((a == sample.Action ? 1f : 0f) - prediction.P[a]);
                        dlogit += .015f * prediction.P[a] * (MathF.Log(MathF.Max(1e-9f, prediction.P[a])) + entropy);
                        gradient[OB + a] += dlogit;
                        for (int j = 0; j < H; j++) { gradient[OW + a * H + j] += dlogit * prediction.Hidden[j]; dh[j] += Parameters[OW + a * H + j] * dlogit; }
                    }
                    float error = prediction.Value - sample.Return;
                    float dv = .5f * error;
                    gradient[VB] += dv;
                    for (int j = 0; j < H; j++)
                    {
                        gradient[VW + j] += dv * prediction.Hidden[j];
                        dh[j] += Parameters[VW + j] * dv;
                        float dz = dh[j] * (1 - prediction.Hidden[j] * prediction.Hidden[j]);
                        gradient[HB + j] += dz;
                        for (int i = 0; i < I; i++) gradient[HW + j * I + i] += dz * sample.X[i];
                    }
                    result.Policy += -MathF.Min(ratio * sample.Advantage, clippedRatio * sample.Advantage);
                    result.Value += .5f * error * error; result.Entropy += entropy; result.Kl += sample.OldLog - log;
                    total++;
                }
                Adam(gradient, count, rate);
            }
        }
        result.Policy /= total; result.Value /= total; result.Entropy /= total; result.Kl /= total;
        return result;
    }
    void Adam(float[] gradient, int count, float rate)
    {
        float norm = 0; for (int i = 0; i < N; i++) { gradient[i] /= count; norm += gradient[i] * gradient[i]; }
        float scale = norm > .25f ? .5f / MathF.Sqrt(norm) : 1;
        Updates++;
        float correction1 = 1 - MathF.Pow(.9f, Updates), correction2 = 1 - MathF.Pow(.999f, Updates);
        for (int i = 0; i < N; i++)
        {
            float g = gradient[i] * scale;
            FirstMoment[i] = .9f * FirstMoment[i] + .1f * g;
            SecondMoment[i] = .999f * SecondMoment[i] + .001f * g * g;
            Parameters[i] -= rate * (FirstMoment[i] / correction1) / (MathF.Sqrt(SecondMoment[i] / correction2) + 1e-8f);
            if (!float.IsFinite(Parameters[i])) throw new InvalidOperationException("Training diverged; refusing to export invalid policy weights.");
        }
    }
    public PolicyFile Export() => new PolicyFile
    {
        trainingEpisodes = TrainingEpisodes,
        hiddenWeights = Parameters[HW..HB], hiddenBias = Parameters[HB..OW], outputWeights = Parameters[OW..OB], outputBias = Parameters[OB..VW]
    };
}
