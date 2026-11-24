using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only CPU probe. No native input, player FPS claims or arbitrary pass budget.
public static class ProbePlanningCost
{
    const int Warmups = 5, Samples = 20;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Serializable]
    public sealed class CostCase
    {
        public string name;
        public int selectedTroops, maximumPendingOrdersPerTroop, totalPendingOrders, activeRouteCount, previewTroopCount, visibleRenderers;
        public int acceptedRouteVertices, proposedRouteVertices;
        public double medianCpuMilliseconds, p95CpuMilliseconds, minimumCpuMilliseconds, maximumCpuMilliseconds;
        public double medianManagedAllocatedBytes, p95ManagedAllocatedBytes;
        public long totalManagedAllocatedBytes;
        public double[] cpuMilliseconds;
        public long[] managedAllocatedBytes;
        public float[] cursorScreen, actualGroundHit;
    }
    [Serializable]
    public sealed class CostReport
    {
        public string measuredUtc, unityVersion, environment, scope, restoration;
        public int screenWidth, screenHeight, warmupCalls, measuredCallsPerCase;
        public int deviceCountBefore, deviceCountAfter, virtualDevicesCreated;
        public bool deviceCountsRestored;
        public string failure;
        public string[] cleanupErrors;
        public long allocationCounterCalibrationBytes;
        public bool managedAllocationCounterVerified;
        public CostCase[] cases;
    }

    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Planning CPU probe requires Play Mode; no devices were created.");
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        bool wasEnabled = game.enabled, reduced = game.ReducedMotion;
        bool hadMap = PlayerPrefs.HasKey("Tablefront.Battlefield");
        int oldMap = PlayerPrefs.GetInt("Tablefront.Battlefield");
        var originalKeyboard = Keyboard.current; var originalMouse = Mouse.current;
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        int devicesBefore = InputSystem.devices.Count, created = 0;
        Keyboard keyboard = null; Mouse mouse = null;
        Exception probeFailure = null;
        var cleanupErrors = new List<string>();
        var results = new List<CostCase>(2);
        void Restore(Action action, string name)
        {
            try { action(); }
            catch (Exception error) { cleanupErrors.Add(name + ": " + error.Message); }
        }

        async Task<CostCase> Measure(string name, int troopCount, int queued)
        {
            game.ReturnToMenu(); game.StartBridgeMission();
            foreach (var army in game.Sim.Armies)
            {
                army.IsAI = false;
                typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f);
            }
            game.Sim.Armies[0].Credits = 1000000;
            int recruited = 0;
            UnitKind[] kinds = { UnitKind.Scout, UnitKind.Rifle, UnitKind.Tank };
            while (game.Sim.UnitCount(0) < troopCount)
            {
                game.Sim.Armies[0].RecruitCooldown = 0;
                if (!game.Sim.Recruit(0, kinds[recruited++ % kinds.Length])) throw new Exception("Real recruitment fixture failed.");
            }
            foreach (var unit in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
            game.SelectAll();
            if (game.Selected.Count != troopCount) throw new Exception("Expected " + troopCount + " selected troops, got " + game.Selected.Count);
            if (!game.IssueOrder(new Vector2(17, -2))) throw new Exception("Initial real order was rejected.");
            for (int i = 0; i < queued; i++)
            {
                Vector2 destination = i % 2 == 0 ? new Vector2(-17, 10) : new Vector2(17, -10);
                if (!game.IssueOrder(destination, -1, false, true)) throw new Exception("Real queued-order fixture failed at " + i);
            }
            var troops = game.Sim.Units.Where(u => u.Alive && game.Selected.Contains(u.Id)).ToArray();
            if (troops.Any(u => u.QueuedOrders.Count != queued)) throw new Exception("Not all selected troops have the requested real queue.");
            game.World.Render(game.Sim, game.Selected); game.HUD.Refresh(); game.SendMessage("LateUpdate");
            await Task.Delay(160); // Let the actual Cinemachine/GameView and UI settle outside the sample.
            Vector2 screen = game.ViewCamera.WorldToScreenPoint(new Vector3(17, 0, -2));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); InputSystem.Update();
            await Task.Delay(80);
            if (game.HUD.PointerOverUI || !game.TryPlanningTarget(mouse.position.ReadValue(), out Vector2 hit, out int target) || target >= 0)
                throw new Exception("Probe pointer must resolve visible empty bank ground, not UI or an enemy.");
            for (int i = 0; i < Warmups; i++) game.OrderPreview.Render(game);
            if (!game.OrderPreview.PreviewVisible || !game.OrderPreview.PreviewValid || game.OrderPreview.PreviewUnitCount != troopCount)
                throw new Exception("Probe must measure a real valid route/formation preview for the full force.");

            var milliseconds = new double[Samples]; var allocation = new long[Samples];
            var watch = new Stopwatch();
            for (int i = 0; i < Samples; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                watch.Restart();
                game.OrderPreview.Render(game);
                watch.Stop();
                long after = GC.GetAllocatedBytesForCurrentThread();
                milliseconds[i] = watch.Elapsed.TotalMilliseconds;
                allocation[i] = after - before;
            }
            double[] sortedTimes = milliseconds.OrderBy(x => x).ToArray();
            long[] sortedAllocation = allocation.OrderBy(x => x).ToArray();
            var routes = game.OrderPreview.GetComponentsInChildren<LineRenderer>(true);
            return new CostCase
            {
                name = name, selectedTroops = troopCount,
                maximumPendingOrdersPerTroop = troops.Max(u => u.QueuedOrders.Count),
                totalPendingOrders = troops.Sum(u => u.QueuedOrders.Count),
                activeRouteCount = game.OrderPreview.ActiveRouteCount, previewTroopCount = game.OrderPreview.PreviewUnitCount,
                visibleRenderers = game.OrderPreview.VisibleRendererCount,
                acceptedRouteVertices = routes.Where(r => r.enabled && r.name.StartsWith("Selected order") && r.name.EndsWith("route")).Sum(r => r.positionCount),
                proposedRouteVertices = routes.Where(r => r.enabled && r.name.StartsWith("Proposed order") && r.name.EndsWith("route")).Sum(r => r.positionCount),
                medianCpuMilliseconds = (sortedTimes[9] + sortedTimes[10]) * .5,
                p95CpuMilliseconds = sortedTimes[18], minimumCpuMilliseconds = sortedTimes[0], maximumCpuMilliseconds = sortedTimes[19],
                medianManagedAllocatedBytes = (sortedAllocation[9] + sortedAllocation[10]) * .5,
                p95ManagedAllocatedBytes = sortedAllocation[18], totalManagedAllocatedBytes = allocation.Sum(),
                cpuMilliseconds = milliseconds, managedAllocatedBytes = allocation,
                cursorScreen = new[] { mouse.position.ReadValue().x, mouse.position.ReadValue().y }, actualGroundHit = new[] { hit.x, hit.y }
            };
        }

        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>("Tablefront planning CPU probe keyboard"); created++;
            mouse = InputSystem.AddDevice<Mouse>("Tablefront planning CPU probe pointer"); created++;
            InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            game.enabled = false; // Stop automatic simulation ticks while manual planning is measured.
            game.ReducedMotion = true;
            results.Add(await Measure("Six troops with one ordinary movement order and live hover preview", 6, 0));
            results.Add(await Measure("Twenty-six selected troops, twelve pending crossing waypoints each, and live hover preview", 26, BattleSimulation.MaxQueuedOrders));
        }
        catch (Exception error) { probeFailure = error; }
        finally
        {
            Restore(() => { if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); }, "Remove virtual mouse");
            Restore(() => { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); }, "Remove virtual keyboard");
            Restore(() => { if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent(); }, "Restore current keyboard");
            Restore(() => { if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent(); }, "Restore current mouse");
            Restore(() => InputSystem.settings.backgroundBehavior = background, "Restore background routing");
            Restore(() => InputSystem.settings.editorInputBehaviorInPlayMode = routing, "Restore Editor routing");
            Restore(() => game.ReducedMotion = reduced, "Restore reduced motion");
            Restore(() => game.ReturnToMenu(), "Return Editor game to title");
            Restore(() => game.SetBattlefield(hadMap ? (BattlefieldId)oldMap : BattlefieldId.Greenfield), "Restore selected map");
            Restore(() => { if (!hadMap) PlayerPrefs.DeleteKey("Tablefront.Battlefield"); }, "Restore map preference presence");
            Restore(() => game.enabled = wasEnabled, "Restore Game.enabled");
        }
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        var calibration = new byte[1024]; GC.KeepAlive(calibration);
        long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        var report = new CostReport
        {
            measuredUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            environment = "Agent-owned Unity Editor Play Mode; game Update disabled during synchronous samples; no native game input",
            scope = "OrderPreview.Render main-thread CPU and managed allocations only. Includes shared legal routing, formation and LineRenderer update calls. Excludes GPU work, frame presentation, other gameplay, native FPS and unmanaged allocation measurement. A false managedAllocationCounterVerified means reported allocation zeros are not allocation evidence. Median is the middle pair; p95 is nearest rank over twenty samples. No arbitrary pass/fail time budget.",
            restoration = "Virtual devices removed; original input devices/routing, map preference, reduced-motion setting and Game.enabled restored; Editor game returned to title.",
            screenWidth = Screen.width, screenHeight = Screen.height, warmupCalls = Warmups, measuredCallsPerCase = Samples,
            deviceCountBefore = devicesBefore, deviceCountAfter = InputSystem.devices.Count, virtualDevicesCreated = created,
            deviceCountsRestored = devicesBefore == InputSystem.devices.Count && cleanupErrors.Count == 0,
            failure = probeFailure?.ToString(), cleanupErrors = cleanupErrors.ToArray(),
            allocationCounterCalibrationBytes = calibrationBytes, managedAllocationCounterVerified = calibrationBytes >= 1024, cases = results.ToArray()
        };
        Directory.CreateDirectory("TestResults");
        File.WriteAllText("TestResults/maneuver-preview-cost.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        if (probeFailure != null || cleanupErrors.Count > 0)
            throw new Exception("Planning CPU probe or cleanup failed; devices before/after " + devicesBefore + "/" + InputSystem.devices.Count + ". See TestResults/maneuver-preview-cost.json. " + string.Join("; ", cleanupErrors), probeFailure);
        return "Editor CPU only: " + string.Join("; ", results.Select(c => c.selectedTroops + " troops / " + c.maximumPendingOrdersPerTroop + " pending: median " + c.medianCpuMilliseconds.ToString("F3") + " ms, p95 " + c.p95CpuMilliseconds.ToString("F3") + " ms, median managed allocation " + c.medianManagedAllocatedBytes.ToString("F0") + " bytes")) + ". Not native FPS. Result: TestResults/maneuver-preview-cost.json";
    }
}
