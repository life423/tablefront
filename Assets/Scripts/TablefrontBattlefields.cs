using System;
using UnityEngine;

namespace Tablefront
{
    public enum BattlefieldId { Greenfield, Ridgeway, DustBasin, RiverCrossing, BridgeGambit }

    /// <summary>Shared rules/art layout. Cosmetic terrain never changes these passable routes.</summary>
    public sealed class BattlefieldDefinition
    {
        public readonly BattlefieldId Id;
        public readonly string Name, Description;
        public readonly Vector2[] BasePositions, PointPositions;
        public readonly string[] PointNames;
        public readonly float CoverRadius, CoverOffset;
        public readonly int TerrainStyle;

        public BattlefieldDefinition(BattlefieldId id, string name, string description, Vector2[] bases,
            Vector2[] points, string[] names, float coverOffset, float coverRadius, int terrainStyle)
        {
            Id = id; Name = name; Description = description;
            BasePositions = bases; PointPositions = points; PointNames = names;
            CoverOffset = coverOffset; CoverRadius = coverRadius; TerrainStyle = terrainStyle;
        }
    }

    public static class TablefrontBattlefields
    {
        // A separately entered experiment, deliberately outside the four-map skirmish roster.
        public static readonly BattlefieldDefinition BridgeMission = new BattlefieldDefinition(
            BattlefieldId.BridgeGambit, "Bridge Gambit",
            "Swing the crossing, flank the tank line, and hold the enemy depot for 45 seconds.",
            new[] { new Vector2(-18, -12), new Vector2(18, 12), new Vector2(0, 23) },
            new[] { new Vector2(-12, -8), new Vector2(12, 8), Vector2.zero },
            new[] { "COBALT DEPOT", "EMBER DEPOT", "ROTARY" }, 4f, 2.2f, 4);
        // Each added layout has rotationally equal approach distances for the three seats.
        // The original layout remains byte-for-byte equivalent for tutorial/training replay.
        public static readonly BattlefieldDefinition[] All =
        {
            new BattlefieldDefinition(BattlefieldId.Greenfield, "Greenfield",
                "Open meadow. Balanced routes and six rifle-cover positions.",
                new[] { new Vector2(0, -21), new Vector2(18.2f, 10.5f), new Vector2(-18.2f, 10.5f) },
                new[] { new Vector2(-10, -6), new Vector2(10, -6), new Vector2(0, 12) },
                new[] { "WEST", "EAST", "NORTH" }, 4f, 2.2f, 0),
            new BattlefieldDefinition(BattlefieldId.Ridgeway, "Ridgeway",
                "Spread-out ridge objectives. Split your army and protect long reinforcement routes.",
                Triangle(21f, -90f), Triangle(16f, -150f),
                new[] { "RIDGE A", "RIDGE B", "SUMMIT" }, 4.8f, 2f, 1),
            new BattlefieldDefinition(BattlefieldId.DustBasin, "Dust Basin",
                "Close central objectives. Infantry and artillery compete for a compact battle line.",
                Triangle(21f, -90f), Triangle(7.5f, -150f),
                new[] { "DUNE", "OASIS", "RUINS" }, 3.4f, 2.1f, 2),
            new BattlefieldDefinition(BattlefieldId.RiverCrossing, "River Crossing",
                "Three shallow, passable fords. Flank between crossings instead of piling into one fight.",
                Triangle(21f, -60f), Triangle(12.5f, -120f),
                new[] { "FORD A", "FORD B", "FORD C" }, 4.2f, 2.2f, 3)
        };

        public static BattlefieldDefinition Get(BattlefieldId id)
        {
            if (id == BattlefieldId.BridgeGambit) return BridgeMission;
            int index = (int)id;
            return index >= 0 && index < All.Length ? All[index] : All[0];
        }

        static Vector2[] Triangle(float radius, float firstDegrees)
        {
            var positions = new Vector2[3];
            for (int i = 0; i < positions.Length; i++)
            {
                // Points are ordered west/east/north around their triangle; preserve indices
                // rather than sorting by screen direction so the policy vocabulary stays fixed.
                float angle = (firstDegrees + i * 120f) * Mathf.Deg2Rad;
                positions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return positions;
        }
    }
}
