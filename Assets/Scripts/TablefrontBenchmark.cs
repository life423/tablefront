using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Tablefront
{
    /// <summary>Explicit local QA only: no telemetry and no changes to ordinary launches.</summary>
    public sealed class TablefrontBenchmark : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        bool preferenceCaptured, hadMap;
        int savedMap;
        [Serializable]
        sealed class Report
        {
            public string version, measuredAtUtc, gpu, cpu, battlefield, scenario;
            public int width, height, samples, maximumUnits, msaaSamples, shadowAtlas;
            public float renderScale, measuredSeconds, simulationSeconds, averageFps, medianFrameMs, p95FrameMs, p99FrameMs;
            public bool vSyncEnabled, interruptedByPause;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_EDITOR
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tablefront-benchmark") >= 0)
            {
                IsRunning = true;
                new GameObject("Opt-in local performance sample").AddComponent<TablefrontBenchmark>();
            }
#endif
        }

        IEnumerator Start()
        {
            yield return null;
            var game = FindAnyObjectByType<TablefrontGame>();
            if (!game) yield break;
            hadMap = PlayerPrefs.HasKey("Tablefront.Battlefield");
            savedMap = PlayerPrefs.GetInt("Tablefront.Battlefield", 0);
            preferenceCaptured = true;
            Application.runInBackground = true;
            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.DustBasin); game.StartBattle(true);
            // All sides pay the same costs; only this QA scenario supplies an opening budget.
            foreach (var army in game.Sim.Armies)
            {
                army.Credits = 600; army.RecruitCooldown = 0;
                game.Sim.Recruit(army.Id, UnitKind.Gunner); army.RecruitCooldown = 0;
                game.Sim.Recruit(army.Id, UnitKind.Artillery);
            }
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 8) yield return null;
            var durations = new List<float>(6000);
            int maximumUnits = 0;
            bool interrupted = false;
            float simulationStart = game.Sim.Elapsed;
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 30)
            {
                if (Time.unscaledDeltaTime > 0) durations.Add(Time.unscaledDeltaTime * 1000);
                maximumUnits = Mathf.Max(maximumUnits, game.Sim.Units.Count(u => u.Alive));
                interrupted |= game.Paused || game.HelpOpen;
                yield return null;
            }
            float measured = Time.realtimeSinceStartup - start;
            if (durations.Count == 0) yield break;
            durations.Sort();
            float Percentile(float fraction) => durations[Mathf.Clamp(Mathf.CeilToInt(durations.Count * fraction) - 1, 0, durations.Count - 1)];
            var report = new Report
            {
                version = Application.version, measuredAtUtc = DateTime.UtcNow.ToString("o"),
                gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                battlefield = game.Sim.Battlefield.Name,
                scenario = "30-second Dust Basin spectator sample after 8-second warmup; opening Gunners and Artillery; native display and normal VSync. Not a maximum-army stress test.",
                width = Screen.width, height = Screen.height, samples = durations.Count,
                maximumUnits = maximumUnits, msaaSamples = TablefrontGraphics.MsaaSamples,
                shadowAtlas = 8192, renderScale = TablefrontGraphics.RenderScale,
                measuredSeconds = measured, simulationSeconds = game.Sim.Elapsed - simulationStart,
                averageFps = 1000 / durations.Average(), interruptedByPause = interrupted,
                medianFrameMs = Percentile(.5f), p95FrameMs = Percentile(.95f), p99FrameMs = Percentile(.99f),
                vSyncEnabled = QualitySettings.vSyncCount > 0
            };
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "tablefront-benchmark.json"), JsonUtility.ToJson(report, true));
            RestoreMapPreference();
            Application.Quit();
        }
        void RestoreMapPreference()
        {
            if (!preferenceCaptured) return;
            if (hadMap) PlayerPrefs.SetInt("Tablefront.Battlefield", savedMap); else PlayerPrefs.DeleteKey("Tablefront.Battlefield");
            PlayerPrefs.Save(); preferenceCaptured = false;
        }
        void OnDestroy() { RestoreMapPreference(); IsRunning = false; }
    }
}
