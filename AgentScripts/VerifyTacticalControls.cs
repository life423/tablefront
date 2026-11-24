using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only integration verification; virtual devices never send native input.
public static class VerifyTacticalControls
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var checks = new List<string>();
        bool reduced = game.ReducedMotion;
        bool hadMap = PlayerPrefs.HasKey("Tablefront.Battlefield");
        int oldMap = PlayerPrefs.GetInt("Tablefront.Battlefield");
        var originalKeyboard = Keyboard.current; var originalMouse = Mouse.current;
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        var keyboard = InputSystem.AddDevice<Keyboard>("Tablefront tactical planning QA");
        var mouse = InputSystem.AddDevice<Mouse>("Tablefront tactical planning pointer QA");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        game.ReducedMotion = true;

        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            checks.Add("PASS " + name);
        }
        void Tick() => game.SendMessage("Update");
        void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        void Press(params Key[] keys) { Keys(keys); Tick(); Keys(); }
        Vector2 ScreenPoint(Vector2 point, float height = 0) => game.ViewCamera.WorldToScreenPoint(new Vector3(point.x, height, point.y));
        async Task Pointer(Vector2 point, MouseButton? held = null)
        {
            var state = new MouseState { position = point };
            if (held.HasValue) state = state.WithButton(held.Value);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update(); Tick();
            await Task.Delay(25);
        }
        async Task Click(Vector2 point, MouseButton button = MouseButton.Left)
        {
            await Pointer(point); await Pointer(point, button); await Pointer(point);
        }
        void Quiet()
        {
            foreach (var army in game.Sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var unit in game.Sim.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(unit, 99999f);
        }
        async Task Fresh(bool mission = false)
        {
            game.ReturnToMenu(); game.SetBattlefield(BattlefieldId.Greenfield);
            if (mission) game.StartBridgeMission(); else game.StartBattle(false);
            Quiet(); Keys(); game.SendMessage("LateUpdate"); await Task.Delay(140);
        }
        void Select(Troop unit) { game.Selected.Clear(); game.Selected.Add(unit.Id); }
        bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .15f;
        Vector2[] AcceptedPath(int id)
        {
            var guides = (Array)typeof(TablefrontOrderPreview).GetField("accepted", Fields).GetValue(game.OrderPreview);
            foreach (object guide in guides)
            {
                var type = guide.GetType();
                if ((int)type.GetField("UnitId", Fields).GetValue(guide) == id)
                    return ((List<Vector2>)type.GetField("Geometry", Fields).GetValue(guide)).ToArray();
            }
            return Array.Empty<Vector2>();
        }
        try
        {
            await Fresh();
            var troop = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            troop.Position = troop.Destination = new Vector2(-7, -11);
            game.Selected.Clear();
            await Pointer(ScreenPoint(new Vector2(-10, -5)));
            Check(!game.OrderPreview.PreviewVisible && game.OrderPreview.VisibleRendererCount == 0, "No selection means no planning clutter");
            await Click(ScreenPoint(troop.Position, .6f));
            Check(game.Selected.Count == 1 && game.Selected.Contains(troop.Id), "A friendly click still selects one troop");

            Vector2 first = new Vector2(-11, -7), second = new Vector2(-11, 1), third = new Vector2(-5, 4);
            bool originalOrder = troop.HasOrder, originalWaiting = troop.WaitingForBridge;
            await Pointer(ScreenPoint(first));
            Check(game.OrderPreview.PreviewVisible && game.OrderPreview.PreviewValid && game.OrderPreview.PreviewUnitCount == 1, "Hover shows a valid contextual formation and route before ordering");
            Check(troop.HasOrder == originalOrder && troop.WaitingForBridge == originalWaiting && troop.QueuedOrders.Count == 0, "Planning preview never mutates the simulation order or waiting state");
            await Click(ScreenPoint(first));
            Check(troop.HasOrder && Near(troop.Destination, first) && troop.QueuedOrders.Count == 0, "A plain ground click starts a movement order without a queue");
            Keys(Key.LeftShift);
            await Pointer(ScreenPoint(second));
            Check(Near(game.OrderPreview.PreviewOriginFor(troop.Id), first), "Shift preview begins at the current order endpoint");
            await Click(ScreenPoint(second));
            Check(Near(troop.Destination, first) && troop.QueuedOrders.Count == 1 && Near(troop.QueuedOrders[0].Destination, second), "Shift left-click queues a waypoint without replacing the current order");
            await Pointer(ScreenPoint(third));
            Check(Near(game.OrderPreview.PreviewOriginFor(troop.Id), second), "Further Shift previews begin at the queue tail");
            await Click(ScreenPoint(third), MouseButton.Right);
            Check(troop.QueuedOrders.Count == 2 && Near(troop.QueuedOrders[1].Destination, third), "Shift right-click provides the same waypoint alternative");
            Keys();
            Check(game.OrderPreview.ActiveRouteCount == 1, "Accepted routes persist only for the selected live force");

            var friend = game.Sim.Units.First(u => u.Team == 0 && u.Id != troop.Id);
            friend.Position = friend.Destination = new Vector2(-2, -11);
            Keys(Key.LeftShift); await Click(ScreenPoint(friend.Position, .6f)); Keys();
            Check(game.Selected.Contains(troop.Id) && game.Selected.Contains(friend.Id) && troop.QueuedOrders.Count == 2, "Shift on a friendly remains additive selection rather than a waypoint");
            await Click(ScreenPoint(new Vector2(-8, -3)));
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.QueuedOrders.Count == 0 && u.HasOrder), "A plain destination replaces selected troops' entire prior plan");
            game.IssueOrder(new Vector2(-6, 1), -1, false, true);
            Check(game.QueuedCount == 1, "Group queue count reports one waypoint rather than one per troop");

            var selection = game.Selected.ToArray();
            game.ArmMove(); await Click(ScreenPoint(friend.Position, .6f));
            Check(!game.MoveArmed && selection.OrderBy(x => x).SequenceEqual(game.Selected.OrderBy(x => x)), "Visible Move action can target occupied ground without accidentally selecting its friendly occupant");
            Press(Key.A); Keys(Key.LeftShift); await Click(ScreenPoint(new Vector2(-6, 2))); Keys();
            Check(!game.AttackMoveArmed && troop.QueuedOrders.Count == 1 && troop.QueuedOrders[0].AttackMove, "Armed attack-move propagates Shift into the queued command");
            game.ArmMove(); Press(Key.Escape);
            Check(!game.MoveArmed && !game.Paused, "Escape cancels the pending Move action before opening Pause");
            Press(Key.S);
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => !u.HasOrder && u.QueuedOrders.Count == 0 && !u.HoldPosition), "S stops the force and clears all queued orders");
            game.IssueOrder(first); game.IssueOrder(second, -1, false, true); Press(Key.H);
            Check(game.Sim.Units.Where(u => game.Selected.Contains(u.Id)).All(u => u.HoldPosition && !u.HasOrder && u.QueuedOrders.Count == 0), "H holds without retaining a hidden movement plan");

            Select(troop); game.IssueOrder(first);
            for (int i = 0; i < BattleSimulation.MaxQueuedOrders; i++) game.IssueOrder(i % 2 == 0 ? second : third, -1, false, true);
            int fullCount = troop.QueuedOrders.Count;
            Check(fullCount == BattleSimulation.MaxQueuedOrders && !game.IssueOrder(second, -1, false, true) && troop.QueuedOrders.Count == fullCount && game.Notice.Contains("Plan full"), "A full bounded plan rejects extra orders with visible feedback and retains the plan");
            Keys(Key.LeftShift); await Pointer(ScreenPoint(second));
            Check(game.OrderPreview.PreviewVisible && !game.OrderPreview.PreviewValid && game.PlanningHint.Contains("Plan full"), "A full plan is visibly unavailable before the extra click");
            Keys(); await Click(ScreenPoint(first));
            Check(troop.QueuedOrders.Count == 0 && troop.HasOrder, "Releasing Shift makes replacement immediately available after a full plan");

            var enemy = game.Sim.Units.First(u => u.Team == 1 && u.Alive);
            enemy.Position = enemy.Destination = new Vector2(-2, -2);
            game.IssueOrder(troop.Position + new Vector2(0, 4));
            Check(troop.HasOrder && Vector2.Distance(troop.Position, troop.Destination) > 2, "Queued attack fixture has a genuinely unfinished movement order");
            Keys(Key.LeftShift); await Click(ScreenPoint(enemy.Position, .6f)); Keys();
            Check(troop.QueuedOrders.Count == 1 && troop.QueuedOrders[0].AttackTargetId == enemy.Id, "Shift clicking an enemy queues a focus attack without replacing movement");
            Vector2 finish = troop.Position + new Vector2(2, 1);
            game.IssueOrder(finish, -1, false, true); enemy.Health = 0;
            for (int i = 0; i < 120 && (troop.QueuedOrders.Count > 0 || !Near(troop.Destination, finish)); i++) game.Sim.Step(.05f);
            Check(troop.QueuedOrders.Count == 0 && Near(troop.Destination, finish), "A dead queued target is skipped and the later movement order still executes");

            await Fresh(); game.SelectAll();
            int[] reversedSelection = game.Selected.Reverse().ToArray();
            game.Selected.Clear(); game.Selected.UnionWith(reversedSelection);
            await Pointer(ScreenPoint(new Vector2(-10, -3)));
            var slots = new List<OrderFormationSlot>(); game.Sim.GetFormation(game.Selected, new Vector2(-10, -3), slots);
            Check(game.OrderPreview.PreviewUnitCount == game.Selected.Count && slots.Count == game.Selected.Count, "Formation ghosts cover every live selected miniature even after selection order changes");
            foreach (var slot in slots)
            {
                var route = game.OrderPreview.PreviewRouteFor(slot.UnitId);
                Check(route.Count > 0 && Near(route[route.Count - 1], slot.Destination), "Formation preview uses the actual assigned endpoint for unit " + slot.UnitId);
            }

            await Fresh(true);
            troop = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            troop.Position = troop.Destination = new Vector2(-12, -10); Select(troop);
            Vector2 across = new Vector2(17, -2);
            await Pointer(ScreenPoint(across));
            Check(game.TryPlanningTarget(mouse.position.ReadValue(), out Vector2 groundTarget, out int hoveredEnemy) && hoveredEnemy < 0 && Near(groundTarget, across), "Bridge route fixture points at visible empty bank ground rather than a nearby enemy");
            var expected = new List<Vector2>(); game.Sim.BuildOrderRoute(troop, troop.Position, groundTarget, expected, out bool waiting);
            Vector2[] shownRoute = game.OrderPreview.PreviewRouteFor(troop.Id).ToArray();
            bool sameRoute = !waiting && expected.Count > 2 && shownRoute.Length == expected.Count;
            if (sameRoute)
                for (int i = 0; i < expected.Count; i++)
                    if (Vector2.Distance(shownRoute[i], expected[i]) > .001f) { sameRoute = false; break; }
            if (!sameRoute)
                throw new Exception("FAIL Bridge preview uses the exact legal movement graph; waiting=" + waiting +
                    "; currentOrigin=" + troop.Position.ToString("F4") + "; previewOrigin=" + game.OrderPreview.PreviewOriginFor(troop.Id).ToString("F4") +
                    "; hit=" + groundTarget.ToString("F4") + "; shown[" + shownRoute.Length + "]=" + string.Join(" / ", shownRoute.Select(p => p.ToString("F4"))) +
                    "; expected[" + expected.Count + "]=" + string.Join(" / ", expected.Select(p => p.ToString("F4"))));
            Check(sameRoute, "Bridge preview uses the exact legal movement graph rather than a straight line through water");
            Check(game.RequestBridge(BridgeRoute.South), "Player can request the alternate bridge route through shared mission rules");
            await Pointer(ScreenPoint(across));
            Check(game.OrderPreview.PreviewValid && game.OrderPreview.PreviewWaiting && game.PlanningHint.Contains("Waiting"), "A changing crossing keeps a valid bank order and shows a distinct waiting plan");
            Check(game.OrderPreview.PreviewRouteFor(troop.Id).All(p => p.x < -TablefrontBridge.RiverHalfWidth), "Waiting path stops on the safe bank without drawing a fake water crossing");
            Check(game.IssueOrder(across) && game.Notice.Contains("waiting"), "Accepting a blocked crossing explains that the saved order waits for docking");
            await Pointer(ScreenPoint(Vector2.zero));
            Check(game.OrderPreview.PreviewVisible && !game.OrderPreview.PreviewValid && !game.IssueOrder(Vector2.zero), "Invalid river destinations show an unavailable marker and cannot issue an order");

            var unavailable = (LineRenderer)typeof(TablefrontOrderPreview).GetField("invalidMarker", Fields).GetValue(game.OrderPreview);
            Vector2 water = new Vector2(0, -12);
            await Pointer(ScreenPoint(water));
            float waterMarkerHeight = game.World.SurfaceHeight(water) + .12f;
            Check(unavailable.enabled && Enumerable.Range(0, unavailable.positionCount).All(i => Mathf.Abs(unavailable.GetPosition(i).y - waterMarkerHeight) < .001f), "Invalid water marker remains flat at the local floor rather than floating at wall height");
            var wall = game.Sim.Tactics.Obstacles[0];
            game.ArmMove(); await Pointer(ScreenPoint(wall.Center));
            Check(game.OrderPreview.PreviewVisible && !game.OrderPreview.PreviewValid && !game.IssueOrder(wall.Center) && game.Notice.Contains("stone walls"), "Wall destination keeps its unavailable preview and explicit rejected-order feedback");
            float physicalTop = game.World.SurfaceHeight(wall.Center) + wall.Height;
            float[] heights = Enumerable.Range(0, unavailable.positionCount).Select(i => unavailable.GetPosition(i).y).ToArray();
            Check(unavailable.enabled && heights.Length == 5 && heights.Average() > physicalTop && heights.Max() - heights.Min() < .001f, "Invalid wall cross is visibly above the physical wall and all vertices stay on one flat plane");

            game.TogglePause(); Tick();
            Check(game.OrderPreview.VisibleRendererCount == 0, "Pause hides every planning renderer");
            game.TogglePause(); game.ToggleHelp(); Tick();
            Check(game.OrderPreview.VisibleRendererCount == 0, "Help hides every planning renderer");
            game.ToggleHelp(); troop.Health = 0; Tick();
            Check(game.Selected.Count == 0 && game.OrderPreview.VisibleRendererCount == 0, "Dead selected troops leave no route or ghost renderers");
            game.ReturnToMenu(); Tick();
            Check(game.OrderPreview.VisibleRendererCount == 0, "Title menu hides all planning renderers");
            int poolCount = game.OrderPreview.GetComponentsInChildren<LineRenderer>(true).Length;
            for (int i = 0; i < 30; i++) game.OrderPreview.Render(game);
            Check(poolCount == game.OrderPreview.PoolSize && game.OrderPreview.GetComponentsInChildren<LineRenderer>(true).Length == poolCount, "Planning renderers use a fixed bounded pool without per-order object growth");

            await Fresh();
            troop = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout); Select(troop);
            game.IssueOrder(first); game.IssueOrder(second, -1, false, true); game.OrderPreview.Render(game);
            Vector2[] beforeQueueChange = AcceptedPath(troop.Id);
            game.IssueOrder(third, -1, false, true); game.OrderPreview.Render(game);
            Vector2[] afterQueueChange = AcceptedPath(troop.Id);
            Check(beforeQueueChange.Length > 0 && afterQueueChange.Length > 0 && Near(beforeQueueChange[beforeQueueChange.Length - 1], second) && Near(afterQueueChange[afterQueueChange.Length - 1], third), "Accepted future-route cache updates immediately when a queued waypoint is added");
            enemy = game.Sim.Units.First(u => u.Team == 1 && u.Alive);
            enemy.Position = enemy.Destination = new Vector2(17, 5);
            game.IssueOrder(first); game.IssueOrder(enemy.Position, enemy.Id, false, true); game.OrderPreview.Render(game);
            enemy.Position = enemy.Destination = new Vector2(17, 7); game.OrderPreview.Render(game);
            Vector2[] movingTargetPath = AcceptedPath(troop.Id);
            Check(movingTargetPath.Length > 0 && Vector2.Distance(movingTargetPath[movingTargetPath.Length - 1], enemy.Position) < .001f, "Accepted future-route cache tracks a moving queued attack target immediately");

            await Fresh(true);
            troop = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            troop.Position = troop.Destination = new Vector2(-12, -10); Select(troop);
            game.IssueOrder(across); game.IssueOrder(new Vector2(-17, 10), -1, false, true); game.OrderPreview.Render(game);
            Vector2[] northPlan = AcceptedPath(troop.Id);
            game.RequestBridge(BridgeRoute.South);
            game.Sim.Bridge.Step(TablefrontBridge.WarningSeconds + .01f, game.Sim.Units);
            game.Sim.Bridge.Step(TablefrontBridge.MoveSeconds + .01f, game.Sim.Units);
            game.OrderPreview.Render(game);
            Vector2[] southPlan = AcceptedPath(troop.Id);
            var rebuilt = new List<Vector2>(); var segment = new List<Vector2>();
            game.Sim.BuildOrderRoute(troop, troop.Position, troop.Destination, segment, out bool initialWait);
            rebuilt.AddRange(segment);
            game.Sim.BuildOrderRoute(troop, troop.Destination, troop.QueuedOrders[0].Destination, segment, out bool futureWait);
            for (int i = 0; i < segment.Count; i++)
                if (rebuilt.Count == 0 || (rebuilt[rebuilt.Count - 1] - segment[i]).sqrMagnitude > .0001f) rebuilt.Add(segment[i]);
            Check(game.Sim.Bridge.CurrentRoute == BridgeRoute.South && !initialWait && !futureWait && southPlan.Length == rebuilt.Count && southPlan.Where((p, i) => Vector2.Distance(p, rebuilt[i]) > .001f).Count() == 0 && !northPlan.SequenceEqual(southPlan), "Accepted future-route cache rebuilds the exact new geometry after bridge docking changes its revision");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/tactical-controls-runtime.txt", checks);
            return checks.Count + " tactical controls checks passed; virtual devices and settings restored.\n" + string.Join("\n", checks);
        }
        finally
        {
            Keys(); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            game.ReducedMotion = reduced;
            game.ReturnToMenu();
            game.SetBattlefield(hadMap ? (BattlefieldId)oldMap : BattlefieldId.Greenfield);
            if (!hadMap) PlayerPrefs.DeleteKey("Tablefront.Battlefield");
        }
    }
}
