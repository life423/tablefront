using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tablefront
{
    public enum BridgeRoute { North, South }
    public enum BridgePhase { Stable, Warning, Moving }

    /// <summary>
    /// Shared deterministic bridge geometry, timing and graph routing. The river is a real
    /// movement boundary; scenery and controller type never decide whether a troop may cross.
    /// </summary>
    public sealed class TablefrontBridge
    {
        public const float RiverHalfWidth = 3.5f;
        public const float HalfLength = 10f;
        public const float HalfWidth = 1.8f;
        public const float NorthAngle = 53.130102f;
        public const float SouthAngle = -53.130102f;
        public const float WarningSeconds = 3f;
        public const float MoveSeconds = 2f;
        public const float CooldownSeconds = 20f;
        public const float DockingGraceSeconds = 8f;
        public const int CommandsPerTeam = 3;

        public BridgeRoute CurrentRoute { get; private set; } = BridgeRoute.North;
        public BridgeRoute TargetRoute { get; private set; } = BridgeRoute.North;
        public BridgePhase Phase { get; private set; } = BridgePhase.Stable;
        public float AngleDegrees { get; private set; } = NorthAngle;
        public int ActorTeam { get; private set; } = -1;
        public int LastActor => ActorTeam;
        public int Revision { get; private set; }
        public float TimeToChange => Phase == BridgePhase.Warning ? Mathf.Max(0, WarningSeconds - phaseTime)
            : Phase == BridgePhase.Moving ? Mathf.Max(0, MoveSeconds - phaseTime) : 0;

        readonly int[] commands = { CommandsPerTeam, CommandsPerTeam };
        readonly float[] cooldowns = new float[2];
        readonly HashSet<int> warnedRiders = new HashSet<int>();
        readonly Dictionary<int, Vector2> riding = new Dictionary<int, Vector2>();
        readonly Vector2[] nodes = new Vector2[12];
        readonly float[] distances = new float[12];
        readonly int[] previous = new int[12];
        readonly bool[] visited = new bool[12];
        readonly int[] reversePath = new int[12];
        readonly Vector2[] segmentIntervals = new Vector2[3];
        readonly List<Vector2> movementPath = new List<Vector2>(12);
        readonly TablefrontTactics tactics;
        float phaseTime, movementStartAngle, dockingGrace;

        public TablefrontBridge(TablefrontTactics tactics = null) { this.tactics = tactics; }

        public int CommandsRemaining(int team) => team >= 0 && team < 2 ? commands[team] : 0;
        public float CooldownRemaining(int team) => team >= 0 && team < 2 ? Mathf.Max(cooldowns[team], dockingGrace) : 0;
        public bool IsRiding(int id) => Phase == BridgePhase.Moving && riding.ContainsKey(id);
        public static float RouteAngle(BridgeRoute route) => route == BridgeRoute.North ? NorthAngle : SouthAngle;
        internal void RestoreCommand(int team) { if (team >= 0 && team < 2) commands[team] = Mathf.Min(CommandsPerTeam, commands[team] + 1); }

        public bool TryRequest(int team, BridgeRoute route, IList<Troop> units)
        {
            if (team < 0 || team > 1 || (route != BridgeRoute.North && route != BridgeRoute.South) ||
                Phase != BridgePhase.Stable || route == CurrentRoute || commands[team] <= 0 || CooldownRemaining(team) > .0001f)
                return false;
            commands[team]--; cooldowns[team] = CooldownSeconds;
            ActorTeam = team; TargetRoute = route; phaseTime = 0; Phase = BridgePhase.Warning;
            warnedRiders.Clear(); riding.Clear();
            for (int i = 0; i < units.Count; i++)
                if (units[i].Alive && OnBridge(units[i].Position, BattleSimulation.UnitRadius(units[i].Kind))) warnedRiders.Add(units[i].Id);
            Revision++;
            return true;
        }

        public void Step(float dt, IList<Troop> units)
        {
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0, cooldowns[i] - dt);
            dockingGrace = Mathf.Max(0, dockingGrace - dt);
            if (Phase == BridgePhase.Stable) return;
            phaseTime += dt;
            if (Phase == BridgePhase.Warning)
            {
                if (phaseTime + .0001f < WarningSeconds) return;
                phaseTime = 0; movementStartAngle = AngleDegrees; Phase = BridgePhase.Moving;
                for (int i = 0; i < units.Count; i++)
                {
                    Troop unit = units[i];
                    if (unit.Alive && warnedRiders.Contains(unit.Id) && OnBridge(unit.Position, BattleSimulation.UnitRadius(unit.Kind)))
                    {
                        float radius = BattleSimulation.UnitRadius(unit.Kind);
                        Vector2 local = ToLocal(unit.Position, movementStartAngle);
                        // Membership admits a sub-millimetre numerical tolerance. Keep a
                        // captured edge passenger inside that tolerance throughout rotation.
                        local.x = Mathf.Clamp(local.x, -HalfLength + radius + .0001f, HalfLength - radius - .0001f);
                        local.y = Mathf.Clamp(local.y, -HalfWidth + radius + .0001f, HalfWidth - radius - .0001f);
                        riding[unit.Id] = local;
                    }
                }
                Revision++;
                return;
            }
            float oldAngle = AngleDegrees;
            float t = Mathf.Clamp01(phaseTime / MoveSeconds);
            AngleDegrees = Mathf.Lerp(movementStartAngle, RouteAngle(TargetRoute), t * t * (3 - 2 * t));
            for (int i = 0; i < units.Count; i++)
            {
                Troop unit = units[i];
                if (!unit.Alive || !riding.TryGetValue(unit.Id, out Vector2 local)) continue;
                unit.Position = ToWorld(local, AngleDegrees);
                unit.Heading -= AngleDegrees - oldAngle;
                unit.WaitingForBridge = false;
            }
            if (t < 1) return;
            CurrentRoute = TargetRoute; AngleDegrees = RouteAngle(CurrentRoute); Phase = BridgePhase.Stable;
            phaseTime = 0; dockingGrace = DockingGraceSeconds;
            warnedRiders.Clear(); riding.Clear(); Revision++;
        }

        public bool OnBridge(Vector2 position, float radius = 0)
        {
            Vector2 local = ToLocal(position, AngleDegrees);
            return Mathf.Abs(local.x) <= HalfLength - radius + .0005f && Mathf.Abs(local.y) <= HalfWidth - radius + .0005f;
        }

        public bool IsWalkable(Vector2 position, float radius = 0, bool includeBridge = true)
        {
            if (!Finite(position) || Mathf.Abs(position.x) > 24.001f || Mathf.Abs(position.y) > 24.001f) return false;
            return (Mathf.Abs(position.x) >= RiverHalfWidth + radius - .0005f || includeBridge && OnBridge(position, radius)) &&
                (tactics == null || tactics.IsWalkable(position, radius));
        }

        /// <summary>Orders into water snap to the closest legal bank or docked bridge, never across the river.</summary>
        public Vector2 ProjectDestination(Vector2 position, float radius)
        {
            position = new Vector2(Mathf.Clamp(position.x, -24, 24), Mathf.Clamp(position.y, -24, 24));
            if (tactics != null) position = tactics.Project(position, radius);
            if (IsWalkable(position, radius)) return position;
            Vector2 bank = new Vector2((position.x < 0 ? -1 : 1) * (RiverHalfWidth + radius + .002f), position.y);
            Vector2 local = ToLocal(position, AngleDegrees);
            local.x = Mathf.Clamp(local.x, -HalfLength + radius + .002f, HalfLength - radius - .002f);
            local.y = Mathf.Clamp(local.y, -HalfWidth + radius + .002f, HalfWidth - radius - .002f);
            Vector2 deck = ToWorld(local, AngleDegrees);
            return (deck - position).sqrMagnitude < (bank - position).sqrMagnitude ? deck : bank;
        }

        public Vector2 Portal(int side, float radius, BridgeRoute route)
        {
            float angle = RouteAngle(route) * Mathf.Deg2Rad;
            float x = (side < 0 ? -1 : 1) * (RiverHalfWidth + radius + .35f);
            return new Vector2(x, x * Mathf.Tan(angle));
        }

        public float RouteCost(Vector2 origin, Vector2 destination, float radius, BridgeRoute route)
        {
            int from = origin.x < 0 ? -1 : 1, to = destination.x < 0 ? -1 : 1;
            if (from == to) return Vector2.Distance(origin, destination);
            Vector2 a = Portal(from, radius, route), b = Portal(to, radius, route);
            return Vector2.Distance(origin, a) + Vector2.Distance(a, b) + Vector2.Distance(b, destination);
        }

        public Vector2 NextWaypoint(Troop unit, Vector2 desired, float radius, out bool finalDestination)
        {
            BuildRoute(unit, unit.Position, desired, radius, movementPath, out bool waiting);
            unit.WaitingForBridge = waiting;
            if (movementPath.Count < 2) { finalDestination = !waiting; return unit.Position; }
            finalDestination = movementPath.Count == 2 && !waiting;
            return movementPath[1];
        }

        /// <summary>Pure route query, including a legal safe approach when the crossing is closed.</summary>
        public void BuildRoute(Troop unit, Vector2 origin, Vector2 desired, float radius, List<Vector2> output, out bool waiting)
        {
            output.Clear(); output.Add(origin); waiting = false;
            bool deckAllowed = Phase == BridgePhase.Stable || Phase == BridgePhase.Warning && unit != null && warnedRiders.Contains(unit.Id);
            desired = ProjectDestination(desired, radius);
            if (FindRoute(origin, desired, radius, deckAllowed, output)) return;
            waiting = true;
            if (Mathf.Abs(origin.x) < RiverHalfWidth + radius) return;
            int side = origin.x < 0 ? -1 : 1;
            Vector2 wait = Portal(side, radius, CurrentRoute); wait.x += side * .8f;
            FindRoute(origin, wait, radius, deckAllowed, output);
        }

        bool FindRoute(Vector2 origin, Vector2 desired, float radius, bool deckAllowed, List<Vector2> output)
        {
            if (SegmentWalkable(origin, desired, radius, deckAllowed))
            { if ((desired - origin).sqrMagnitude > .000001f) output.Add(desired); return true; }
            // Exact visibility graph through landing overlaps and expanded obstacle corners.
            nodes[0] = origin; nodes[1] = desired;
            nodes[2] = Portal(-1, radius, CurrentRoute); nodes[3] = Portal(1, radius, CurrentRoute);
            int nodeCount = 4;
            if (tactics != null)
                for (int obstacleIndex = 0; obstacleIndex < tactics.Obstacles.Count; obstacleIndex++)
                {
                    var obstacle = tactics.Obstacles[obstacleIndex];
                    Vector2 extent = obstacle.HalfSize + Vector2.one * (radius + .035f);
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2) nodes[nodeCount++] = obstacle.ToWorld(new Vector2(extent.x * x, extent.y * y));
                }
            for (int i = 0; i < nodeCount; i++) { distances[i] = float.PositiveInfinity; previous[i] = -1; visited[i] = false; }
            distances[0] = 0;
            for (int pass = 0; pass < nodeCount; pass++)
            {
                int current = -1; float best = float.PositiveInfinity;
                for (int i = 0; i < nodeCount; i++) if (!visited[i] && distances[i] < best) { best = distances[i]; current = i; }
                if (current < 0 || current == 1) break;
                visited[current] = true;
                for (int next = 0; next < nodeCount; next++)
                {
                    if (visited[next] || next == current || !SegmentWalkable(nodes[current], nodes[next], radius, deckAllowed)) continue;
                    float cost = distances[current] + Vector2.Distance(nodes[current], nodes[next]);
                    if (cost < distances[next]) { distances[next] = cost; previous[next] = current; }
                }
            }
            if (previous[1] >= 0)
            {
                int length = 0, at = 1;
                while (at > 0 && length < nodeCount) { reversePath[length++] = at; at = previous[at]; }
                for (int i = length - 1; i >= 0; i--)
                {
                    int node = reversePath[i];
                    if ((nodes[node] - origin).sqrMagnitude <= .000001f && node != 1) continue;
                    output.Add(nodes[node]);
                }
                return true;
            }
            return false;
        }

        public Vector2 ConstrainMovement(Troop unit, Vector2 proposed, float radius)
        {
            bool deckAllowed = Phase == BridgePhase.Stable || Phase == BridgePhase.Warning && warnedRiders.Contains(unit.Id);
            Vector2 from = unit.Position;
            if (SegmentWalkable(from, proposed, radius, deckAllowed)) return proposed;
            Vector2 best = from;
            float bestDistance = (from - proposed).sqrMagnitude;
            Vector2 x = new Vector2(proposed.x, from.y), y = new Vector2(from.x, proposed.y);
            if (SegmentWalkable(from, x, radius, deckAllowed)) { best = x; bestDistance = (x - proposed).sqrMagnitude; }
            if (SegmentWalkable(from, y, radius, deckAllowed) && (y - proposed).sqrMagnitude < bestDistance)
            { best = y; bestDistance = (y - proposed).sqrMagnitude; }
            Vector2 projected = ProjectDestination(proposed, radius);
            if (SegmentWalkable(from, projected, radius, deckAllowed) && (projected - proposed).sqrMagnitude < bestDistance) best = projected;
            return best;
        }

        bool SegmentWalkable(Vector2 from, Vector2 to, float radius, bool includeBridge)
        {
            if (!IsWalkable(from, radius, includeBridge) || !IsWalkable(to, radius, includeBridge)) return false;
            if (tactics != null && !tactics.SegmentClear(from, to, radius)) return false;
            Vector2 delta = to - from;
            int count = 0;
            float lo = 0, hi = 1, bankEdge = RiverHalfWidth + radius - .0005f;
            if (ClipAxis(from.x, delta.x, float.NegativeInfinity, -bankEdge, ref lo, ref hi)) segmentIntervals[count++] = new Vector2(lo, hi);
            lo = 0; hi = 1;
            if (ClipAxis(from.x, delta.x, bankEdge, float.PositiveInfinity, ref lo, ref hi)) segmentIntervals[count++] = new Vector2(lo, hi);
            if (includeBridge)
            {
                Vector2 local = ToLocal(from, AngleDegrees), localDelta = ToLocal(delta, AngleDegrees);
                lo = 0; hi = 1;
                float length = HalfLength - radius + .0005f, width = HalfWidth - radius + .0005f;
                if (ClipAxis(local.x, localDelta.x, -length, length, ref lo, ref hi) &&
                    ClipAxis(local.y, localDelta.y, -width, width, ref lo, ref hi)) segmentIntervals[count++] = new Vector2(lo, hi);
            }
            // The segment must be covered continuously by the union. This catches even
            // tiny illegal corner cuts that fixed-distance sampling could step over.
            for (int i = 0; i < count - 1; i++)
                for (int j = i + 1; j < count; j++)
                    if (segmentIntervals[j].x < segmentIntervals[i].x)
                    { Vector2 swap = segmentIntervals[i]; segmentIntervals[i] = segmentIntervals[j]; segmentIntervals[j] = swap; }
            float covered = 0;
            for (int i = 0; i < count; i++)
            {
                if (segmentIntervals[i].x > covered + .00001f) return false;
                covered = Mathf.Max(covered, segmentIntervals[i].y);
            }
            return covered >= .99999f;
        }

        static bool ClipAxis(float origin, float delta, float minimum, float maximum, ref float lo, ref float hi)
        {
            if (Mathf.Abs(delta) < .000001f) return origin >= minimum && origin <= maximum;
            float a = (minimum - origin) / delta, b = (maximum - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            lo = Mathf.Max(lo, a); hi = Mathf.Min(hi, b);
            return lo <= hi;
        }

        static Vector2 ToLocal(Vector2 position, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad, c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(position.x * c + position.y * s, -position.x * s + position.y * c);
        }
        static Vector2 ToWorld(Vector2 local, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad, c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }
        static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    }
}
