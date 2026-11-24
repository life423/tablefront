using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>Bounded, read-only planning view. Every route comes from the movement rules.</summary>
    public sealed class TablefrontOrderPreview : MonoBehaviour
    {
        sealed class Guide
        {
            public LineRenderer Path, Marker;
            public readonly List<Vector2> Geometry = new List<Vector2>(80);
            public int UnitId = -1;
            public Vector2 Origin;
            // Future legs have fixed origins until a real plan/target/bridge change.
            // UnitId is a visibility field reset by Hide, so cache identity is separate.
            public BattleSimulation CachedSimulation;
            public int CachedUnitId = -1, CachedBridgeRevision, CachedQueueCount;
            public float CachedBridgeAngle;
            public UnitKind CachedKind;
            public Vector2 CachedCurrentDestination, FutureFinalDestination;
            public readonly Vector2[] CachedQueueEndpoints = new Vector2[BattleSimulation.MaxQueuedOrders];
            public readonly List<Vector2> FutureGeometry = new List<Vector2>(BattleSimulation.MaxQueuedOrders * 16);
            public bool FutureCacheValid, FutureWaiting;
        }

        const int Capacity = BattleSimulation.UnitCap;
        const int MaximumVertices = 4096;
        static readonly Vector2[] InvalidOffsets = { new Vector2(-.65f, -.65f), new Vector2(.65f, .65f), Vector2.zero, new Vector2(-.65f, .65f), new Vector2(.65f, -.65f) };
        readonly Guide[] hover = new Guide[Capacity], accepted = new Guide[Capacity];
        readonly List<OrderFormationSlot> formation = new List<OrderFormationSlot>(Capacity);
        readonly List<Vector2> leg = new List<Vector2>(8);
        readonly Vector2[] queuedEndpointScratch = new Vector2[BattleSimulation.MaxQueuedOrders];
        readonly List<int> selectedIds = new List<int>(Capacity);
        readonly Vector3[] vertices = new Vector3[MaximumVertices];
        readonly Vector3[] markerVertices = new Vector3[17];
        readonly Material[] materials = new Material[4];
        LineRenderer invalidMarker;
        bool built;

        public string Hint { get; private set; } = "";
        public bool PreviewVisible { get; private set; }
        public bool PreviewValid { get; private set; }
        public bool PreviewWaiting { get; private set; }
        public int PreviewUnitCount { get; private set; }
        public int ActiveRouteCount { get; private set; }
        public int PoolSize => Capacity * 4 + 1;
        public int VisibleRendererCount
        {
            get
            {
                int count = invalidMarker && invalidMarker.enabled ? 1 : 0;
                for (int i = 0; i < Capacity; i++)
                {
                    if (hover[i].Path.enabled) count++;
                    if (hover[i].Marker.enabled) count++;
                    if (accepted[i].Path.enabled) count++;
                    if (accepted[i].Marker.enabled) count++;
                }
                return count;
            }
        }
        public Vector2 PreviewOriginFor(int unitId)
        {
            for (int i = 0; i < PreviewUnitCount; i++) if (hover[i].UnitId == unitId) return hover[i].Origin;
            return Vector2.positiveInfinity;
        }
        public IReadOnlyList<Vector2> PreviewRouteFor(int unitId)
        {
            for (int i = 0; i < PreviewUnitCount; i++) if (hover[i].UnitId == unitId) return hover[i].Geometry;
            return System.Array.Empty<Vector2>();
        }

        public void Build()
        {
            if (built) return;
            built = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) throw new System.InvalidOperationException("Order planning requires the project's URP Unlit shader.");
            Color[] colors = { TablePalette.Gold, Color.Lerp(TablePalette.Team(0), TablePalette.Ink, .24f), TablePalette.Team(1), TablePalette.Team(1) };
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(shader) { name = "Order planning " + i, enableInstancing = true };
                materials[i].SetColor("_BaseColor", colors[i]);
            }
            for (int i = 0; i < Capacity; i++)
            {
                hover[i] = NewGuide("Proposed order " + i, 0);
                accepted[i] = NewGuide("Selected order " + i, 1);
            }
            invalidMarker = NewLine("Unavailable destination", 3, .10f);
        }

        Guide NewGuide(string name, int material) => new Guide
        {
            Path = NewLine(name + " route", material, material == 0 ? .085f : .055f),
            Marker = NewLine(name + " formation", material, material == 0 ? .105f : .07f)
        };

        LineRenderer NewLine(string name, int material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true; line.sharedMaterial = materials[material];
            line.startWidth = line.endWidth = width; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false; line.positionCount = 0;
            return line;
        }

        public void HideHover()
        {
            if (!built) return;
            for (int i = 0; i < Capacity; i++) { hover[i].Path.enabled = hover[i].Marker.enabled = false; hover[i].UnitId = -1; }
            invalidMarker.enabled = false;
            PreviewVisible = PreviewValid = PreviewWaiting = false; PreviewUnitCount = 0; Hint = "";
        }

        public void Hide()
        {
            HideHover();
            if (!built) return;
            for (int i = 0; i < Capacity; i++) { accepted[i].Path.enabled = accepted[i].Marker.enabled = false; accepted[i].UnitId = -1; }
            ActiveRouteCount = 0;
        }

        public void Render(TablefrontGame game, bool dragging = false)
        {
            if (!built) Build();
            Hide();
            if (game.Sim == null || game.InMenu || game.Paused || game.HelpOpen || game.Spectator || game.Sim.Finished || !game.Sim.Armies[0].Alive) return;
            selectedIds.Clear();
            // Preserve the same selection enumeration that accepted formation orders use.
            foreach (int id in game.Selected)
            {
                var unit = Find(game.Sim, id);
                if (unit != null && unit.Alive && unit.Team == 0 && selectedIds.Count < Capacity) selectedIds.Add(id);
            }
            if (selectedIds.Count == 0) return;
            DrawAccepted(game);
            var mouse = Mouse.current;
            if (dragging || mouse == null || game.HUD.PointerOverUI) return;
            Vector2 screen = mouse.position.ReadValue();
            bool append = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (!game.TryPlanningTarget(screen, out Vector2 destination, out int enemyId)) return;
            DrawPreview(game, destination, enemyId, append);
        }

        void DrawAccepted(TablefrontGame game)
        {
            foreach (var unit in game.Sim.Units)
            {
                if (!unit.Alive || unit.Team != 0 || !game.Selected.Contains(unit.Id) || !unit.HasOrder || ActiveRouteCount >= Capacity) continue;
                Guide guide = accepted[ActiveRouteCount++];
                guide.UnitId = unit.Id; guide.Origin = unit.Position; guide.Geometry.Clear();
                Vector2 origin = unit.Position, destination = LiveDestination(game.Sim, unit.Destination, unit.AttackTargetId);
                bool waiting = AppendLeg(game.Sim, unit, origin, destination, guide.Geometry);
                if (!waiting)
                {
                    UpdateFutureCache(game.Sim, unit, destination, guide);
                    for (int i = 0; i < guide.FutureGeometry.Count; i++)
                        AppendPoint(guide.Geometry, guide.FutureGeometry[i]);
                    destination = guide.FutureFinalDestination;
                    waiting = guide.FutureWaiting;
                }
                bool changing = game.Sim.MissionActive && game.Sim.Bridge.Phase != BridgePhase.Stable;
                DrawRoute(game, guide, waiting || changing ? 2 : 1);
                DrawMarker(game, guide.Marker, waiting && guide.Geometry.Count > 0 ? guide.Geometry[guide.Geometry.Count - 1] : destination,
                    BattleSimulation.UnitRadius(unit.Kind) + .20f, waiting || changing ? 2 : 1, waiting);
            }
        }

        void UpdateFutureCache(BattleSimulation sim, Troop unit, Vector2 currentDestination, Guide guide)
        {
            int count = unit.QueuedOrders.Count;
            int revision = sim.MissionActive ? sim.Bridge.Revision : -1;
            float angle = sim.MissionActive ? sim.Bridge.AngleDegrees : 0;
            bool changed = !guide.FutureCacheValid || !ReferenceEquals(guide.CachedSimulation, sim) ||
                guide.CachedUnitId != unit.Id || guide.CachedKind != unit.Kind || guide.CachedBridgeRevision != revision ||
                guide.CachedBridgeAngle != angle || guide.CachedQueueCount != count || !SamePoint(guide.CachedCurrentDestination, currentDestination);
            for (int i = 0; i < count; i++)
            {
                var order = unit.QueuedOrders[i];
                queuedEndpointScratch[i] = LiveDestination(sim, order.Destination, order.AttackTargetId);
                if (!SamePoint(guide.CachedQueueEndpoints[i], queuedEndpointScratch[i])) changed = true;
            }
            if (!changed) return;
            guide.CachedSimulation = sim; guide.CachedUnitId = unit.Id; guide.CachedKind = unit.Kind;
            guide.CachedBridgeRevision = revision; guide.CachedQueueCount = count;
            guide.CachedBridgeAngle = angle;
            guide.CachedCurrentDestination = currentDestination;
            for (int i = 0; i < count; i++) guide.CachedQueueEndpoints[i] = queuedEndpointScratch[i];
            guide.FutureGeometry.Clear(); guide.FutureWaiting = false;
            guide.FutureFinalDestination = currentDestination;
            Vector2 origin = currentDestination;
            for (int i = 0; i < count; i++)
            {
                Vector2 destination = queuedEndpointScratch[i];
                guide.FutureWaiting = AppendLeg(sim, unit, origin, destination, guide.FutureGeometry);
                guide.FutureFinalDestination = destination;
                origin = destination;
                if (guide.FutureWaiting) break;
            }
            guide.FutureCacheValid = true;
        }

        static bool SamePoint(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
        static void AppendPoint(List<Vector2> geometry, Vector2 point)
        {
            if (geometry.Count == 0 || (geometry[geometry.Count - 1] - point).sqrMagnitude > .0001f) geometry.Add(point);
        }

        void DrawPreview(TablefrontGame game, Vector2 destination, int enemyId, bool append)
        {
            PreviewVisible = true;
            Troop largest = null;
            foreach (var unit in game.Sim.Units)
                if (unit.Alive && unit.Team == 0 && game.Selected.Contains(unit.Id) && (largest == null || BattleSimulation.UnitRadius(unit.Kind) > BattleSimulation.UnitRadius(largest.Kind))) largest = unit;
            if (largest == null) return;
            bool changing = game.Sim.MissionActive && game.Sim.Bridge.Phase != BridgePhase.Stable;
            int queueAvailable = 0;
            foreach (var unit in game.Sim.Units)
                if (unit.Alive && unit.Team == 0 && game.Selected.Contains(unit.Id) && (!unit.HasOrder || unit.QueuedOrders.Count < BattleSimulation.MaxQueuedOrders)) queueAvailable++;
            if (append && queueAvailable == 0)
            {
                Hint = "Plan full · 12 queued orders · release Shift to replace";
                DrawInvalidMarker(game, destination);
                return;
            }
            PreviewValid = enemyId >= 0 || game.Sim.DestinationAllowed(destination, largest.Kind) &&
                (!changing || Mathf.Abs(destination.x) >= TablefrontBridge.RiverHalfWidth + BattleSimulation.UnitRadius(largest.Kind));
            if (!PreviewValid)
            {
                Hint = changing ? "Bridge changing · choose open ground" : "Water or stone wall blocks this spot · choose open ground";
                DrawInvalidMarker(game, destination);
                return;
            }
            game.Sim.GetFormation(selectedIds, destination, formation);
            for (int i = 0; i < formation.Count && PreviewUnitCount < Capacity; i++)
            {
                var slot = formation[i]; Troop unit = Find(game.Sim, slot.UnitId);
                if (unit == null || !unit.Alive) continue;
                Guide guide = hover[PreviewUnitCount++]; guide.UnitId = unit.Id;
                guide.Origin = append ? QueueTail(game.Sim, unit) : unit.Position;
                Vector2 endpoint = enemyId >= 0 ? destination : slot.Destination;
                guide.Geometry.Clear();
                bool waiting = AppendLeg(game.Sim, unit, guide.Origin, endpoint, guide.Geometry);
                PreviewWaiting |= waiting;
                DrawRoute(game, guide, waiting || changing ? 2 : 0);
                if (enemyId < 0 || PreviewUnitCount == 1)
                    DrawMarker(game, guide.Marker, endpoint, enemyId >= 0 ? .9f : BattleSimulation.UnitRadius(unit.Kind) + .22f, waiting || changing ? 2 : 0, waiting || enemyId >= 0);
            }
            if (PreviewWaiting) Hint = "Waiting for bridge · order resumes when docked";
            else if (changing) Hint = "Bridge changing · routes replan when docked";
            else if (append && queueAvailable < selectedIds.Count) Hint = "Shift-click queues for " + queueAvailable + " of " + selectedIds.Count + " troops";
            else if (append) Hint = enemyId >= 0 ? "Shift-click to queue this attack" : "Shift-click to add this waypoint";
            else Hint = enemyId >= 0 ? "Click to attack · Shift-click queues" : game.AttackMoveArmed ? "Click to advance and engage · Shift-click queues" : "Click to move in formation · Shift-click queues";
        }

        bool AppendLeg(BattleSimulation sim, Troop unit, Vector2 origin, Vector2 destination, List<Vector2> geometry)
        {
            sim.BuildOrderRoute(unit, origin, destination, leg, out bool waiting);
            for (int i = 0; i < leg.Count; i++)
                AppendPoint(geometry, leg[i]);
            return waiting;
        }

        static Troop Find(BattleSimulation sim, int id)
        {
            foreach (var unit in sim.Units) if (unit.Id == id) return unit;
            return null;
        }
        static Vector2 LiveDestination(BattleSimulation sim, Vector2 fallback, int target)
        {
            var enemy = target >= 0 ? Find(sim, target) : null;
            return enemy != null && enemy.Alive ? enemy.Position : fallback;
        }
        static Vector2 QueueTail(BattleSimulation sim, Troop unit)
        {
            if (unit.QueuedOrders.Count > 0)
            {
                var order = unit.QueuedOrders[unit.QueuedOrders.Count - 1];
                return LiveDestination(sim, order.Destination, order.AttackTargetId);
            }
            return unit.HasOrder ? LiveDestination(sim, unit.Destination, unit.AttackTargetId) : unit.Position;
        }

        void DrawRoute(TablefrontGame game, Guide guide, int material)
        {
            int count = 0;
            for (int i = 0; i < guide.Geometry.Count; i++)
            {
                Vector2 from = i == 0 ? guide.Geometry[i] : guide.Geometry[i - 1], to = guide.Geometry[i];
                int subdivisions = i == 0 ? 1 : Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .55f));
                for (int s = i == 0 ? 0 : 1; s <= subdivisions && count < MaximumVertices; s++)
                {
                    Vector2 point = i == 0 ? to : Vector2.Lerp(from, to, s / (float)subdivisions);
                    vertices[count++] = new Vector3(point.x, game.World.SurfaceHeight(point) + .09f, point.y);
                    if (i == 0) break;
                }
            }
            guide.Path.sharedMaterial = materials[material]; guide.Path.positionCount = count;
            guide.Path.SetPositions(vertices);
            guide.Path.enabled = count > 1;
        }

        void DrawMarker(TablefrontGame game, LineRenderer marker, Vector2 point, float radius, int material, bool diamond)
        {
            int count = diamond ? 5 : 17;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / (count - 1);
                Vector2 p = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                markerVertices[i] = new Vector3(p.x, game.World.SurfaceHeight(p) + .11f, p.y);
            }
            marker.sharedMaterial = materials[material]; marker.positionCount = count;
            marker.SetPositions(markerVertices);
            marker.enabled = true;
        }

        void DrawInvalidMarker(TablefrontGame game, Vector2 point)
        {
            float floor = game.World.SurfaceHeight(point);
            float height = floor + .12f;
            if (game.Sim.MissionActive && game.Sim.Tactics != null)
                foreach (var obstacle in game.Sim.Tactics.Obstacles)
                    if (obstacle.Contains(point)) height = Mathf.Max(height, floor + obstacle.Height + .15f);
            invalidMarker.positionCount = InvalidOffsets.Length;
            for (int i = 0; i < InvalidOffsets.Length; i++)
            {
                Vector2 p = point + InvalidOffsets[i];
                invalidMarker.SetPosition(i, new Vector3(p.x, height, p.y));
            }
            invalidMarker.enabled = true;
        }

        void OnDestroy()
        {
            foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
