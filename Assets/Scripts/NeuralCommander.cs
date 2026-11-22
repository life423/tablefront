using System;
using UnityEngine;

namespace Tablefront
{
    /// <summary>A trained two-layer neural policy. Strategic decisions contain no scripted fallback.</summary>
    public static class NeuralCommander
    {
        [Serializable]
        public sealed class Policy
        {
            public int observationCount, hiddenCount, actionCount, trainingEpisodes;
            public float[] hiddenWeights, hiddenBias, outputWeights, outputBias;
        }

        static Policy policy;
        static bool attempted;
        static float[] hidden, logits;
        public static bool Available { get { EnsureLoaded(); return policy != null; } }
        public static int TrainingEpisodes { get { EnsureLoaded(); return policy == null ? 0 : policy.trainingEpisodes; } }

        public static void SetPolicy(Policy trainedPolicy)
        {
            attempted = true;
            policy = Valid(trainedPolicy) ? trainedPolicy : null;
            hidden = policy == null ? null : new float[policy.hiddenCount];
            logits = policy == null ? null : new float[policy.actionCount];
        }

        static void EnsureLoaded()
        {
            if (attempted) return;
            attempted = true;
            TextAsset asset = Resources.Load<TextAsset>("commander-policy");
            if (asset != null)
            {
                try { SetPolicy(JsonUtility.FromJson<Policy>(asset.text)); }
                catch (Exception) { policy = null; }
            }
        }

        static bool Valid(Policy p)
        {
            return p != null && p.observationCount == BattleSimulation.ObservationCount && p.actionCount == BattleSimulation.ActionCount &&
                p.hiddenCount > 0 && p.hiddenCount <= 256 && p.hiddenWeights != null && p.hiddenWeights.Length == p.hiddenCount * p.observationCount &&
                p.hiddenBias != null && p.hiddenBias.Length == p.hiddenCount && p.outputWeights != null && p.outputWeights.Length == p.actionCount * p.hiddenCount &&
                p.outputBias != null && p.outputBias.Length == p.actionCount && Finite(p.hiddenWeights) && Finite(p.hiddenBias) && Finite(p.outputWeights) && Finite(p.outputBias);
        }

        static bool Finite(float[] values)
        {
            for (int i = 0; i < values.Length; i++) if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            return true;
        }

        public static float[] PredictProbabilities(float[] observation)
        {
            EnsureLoaded();
            if (policy == null || observation == null || observation.Length != policy.observationCount || !Finite(observation)) return null;
            for (int h = 0; h < policy.hiddenCount; h++)
            {
                float sum = policy.hiddenBias[h];
                int offset = h * policy.observationCount;
                for (int i = 0; i < policy.observationCount; i++) sum += policy.hiddenWeights[offset + i] * observation[i];
                hidden[h] = (float)Math.Tanh(sum);
            }
            float maximum = float.NegativeInfinity;
            for (int a = 0; a < policy.actionCount; a++)
            {
                float sum = policy.outputBias[a];
                int offset = a * policy.hiddenCount;
                for (int h = 0; h < policy.hiddenCount; h++) sum += policy.outputWeights[offset + h] * hidden[h];
                logits[a] = sum;
                if (sum > maximum) maximum = sum;
            }
            double total = 0d;
            for (int a = 0; a < policy.actionCount; a++) total += Math.Exp(logits[a] - maximum);
            var probabilities = new float[policy.actionCount];
            for (int a = 0; a < policy.actionCount; a++)
            {
                probabilities[a] = (float)(Math.Exp(logits[a] - maximum) / total);
            }
            return probabilities;
        }

        public static int Decide(float[] observation, System.Random random)
        {
            float[] probabilities = PredictProbabilities(observation);
            if (probabilities == null) return -1;
            // Match the stochastic policy optimized during PPO training. The simulation supplies a
            // separate seeded RNG per commander, so identical seeds/actions remain reproducible.
            double sample = random == null ? .5d : random.NextDouble();
            for (int a = 0; a < probabilities.Length; a++)
            {
                sample -= probabilities[a];
                if (sample <= 0d) return a;
            }
            return probabilities.Length - 1;
        }
    }
}
