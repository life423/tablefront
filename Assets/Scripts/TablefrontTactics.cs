using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tablefront
{
    [Serializable]
    public struct TroopOrder
    {
        public Vector2 Destination;
        public int AttackTargetId;
        public bool AttackMove;
    }

    public struct OrderFormationSlot
    {
        public int UnitId;
        public Vector2 Destination;
    }

    public sealed class TacticalObstacle
    {
        public Vector2 Center { get; }
        public Vector2 HalfSize { get; }
        public float AngleDegrees { get; }
        public float Height { get; }
        public TacticalObstacle(Vector2 center, Vector2 halfSize, float angle = 0, float height = 2.1f)
        { Center = center; HalfSize = halfSize; AngleDegrees = angle; Height = height; }

        public Vector2 ToLocal(Vector2 point)
        {
            Vector2 p = point - Center;
            float a = AngleDegrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(p.x * c + p.y * s, -p.x * s + p.y * c);
        }
        public Vector2 ToWorld(Vector2 point)
        {
            float a = AngleDegrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return Center + new Vector2(point.x * c - point.y * s, point.x * s + point.y * c);
        }
        public bool Contains(Vector2 point, float radius = 0)
        {
            Vector2 p = ToLocal(point);
            return Mathf.Abs(p.x) < HalfSize.x + radius && Mathf.Abs(p.y) < HalfSize.y + radius;
        }
        public bool BlocksSegment(Vector2 from, Vector2 to, float radius = 0)
        {
            Vector2 a = ToLocal(from), delta = ToLocal(to) - a;
            float lo = 0, hi = 1;
            return Clip(a.x, delta.x, HalfSize.x + radius, ref lo, ref hi) &&
                Clip(a.y, delta.y, HalfSize.y + radius, ref lo, ref hi) && hi >= 0 && lo <= 1;
        }
        static bool Clip(float a, float delta, float extent, ref float lo, ref float hi)
        {
            if (Mathf.Abs(delta) < .000001f) return Mathf.Abs(a) <= extent;
            float p = (-extent - a) / delta, q = (extent - a) / delta;
            if (p > q) { float swap = p; p = q; q = swap; }
            lo = Mathf.Max(lo, p); hi = Mathf.Min(hi, q); return lo <= hi;
        }
    }

    public sealed class DirectionalCover
    {
        public Vector2 Center { get; }
        public Vector2 Facing { get; }
        public float Radius => 2.2f;
        public float HalfAngleDegrees => 60f;
        public DirectionalCover(Vector2 center, Vector2 facing) { Center = center; Facing = facing.normalized; }
        public bool Contains(Vector2 point) => (point - Center).sqrMagnitude <= Radius * Radius;
        public bool Protects(Vector2 position, Vector2 attacker) => Contains(position) &&
            Vector2.Dot((attacker - position).normalized, Facing) >= .5f;
    }

    /// <summary>Mission geometry consumed by routing, weapons and rendered scenery alike.</summary>
    public sealed class TablefrontTactics
    {
        readonly TacticalObstacle[] obstacles = {
            new TacticalObstacle(new Vector2(-12, -4f), new Vector2(1.1f, 2.6f)),
            new TacticalObstacle(new Vector2(12, 4f), new Vector2(1.1f, 2.6f))
        };
        readonly DirectionalCover[] covers = {
            new DirectionalCover(new Vector2(-10, -7), new Vector2(.8f, -.6f)),
            new DirectionalCover(new Vector2(10, 7), new Vector2(-.8f, .6f))
        };
        public IReadOnlyList<TacticalObstacle> Obstacles => obstacles;
        public IReadOnlyList<DirectionalCover> Covers => covers;
        public bool IsWalkable(Vector2 point, float radius)
        { for (int i = 0; i < obstacles.Length; i++) if (obstacles[i].Contains(point, radius)) return false; return true; }
        public bool SegmentClear(Vector2 from, Vector2 to, float radius)
        { for (int i = 0; i < obstacles.Length; i++) if (obstacles[i].BlocksSegment(from, to, radius)) return false; return true; }
        public bool HasLineOfSight(Vector2 from, Vector2 to) => SegmentClear(from, to, 0);
        public Vector2 Project(Vector2 point, float radius)
        {
            for (int i = 0; i < obstacles.Length; i++)
            {
                var obstacle = obstacles[i];
                if (!obstacle.Contains(point, radius)) continue;
                Vector2 p = obstacle.ToLocal(point), size = obstacle.HalfSize + Vector2.one * (radius + .035f);
                if (size.x - Mathf.Abs(p.x) <= size.y - Mathf.Abs(p.y)) p.x = (p.x < 0 ? -1 : 1) * size.x;
                else p.y = (p.y < 0 ? -1 : 1) * size.y;
                point = obstacle.ToWorld(p);
            }
            return point;
        }
    }
}
