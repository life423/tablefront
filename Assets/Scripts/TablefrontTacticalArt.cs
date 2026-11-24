using UnityEngine;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>Physical mission cues built from the exact shared tactical geometry. Visual only.</summary>
    public sealed class TablefrontTacticalArt
    {
        public readonly Transform Root;
        public readonly Transform[] Obstacles, Covers;
        public readonly Renderer[,] CommandCaps = new Renderer[2, TablefrontBridge.CommandsPerTeam];
        public readonly Transform[] RechargeFill = new Transform[2];
        public readonly Mesh UnitProtectionArc, UnitFlankArc;
        readonly Material ink;
        readonly Material[] signals;
        const float RechargeLength = 1.12f;

        public TablefrontTacticalArt(Transform parent, TablefrontLandmarkArt.Kit kit, TablefrontTactics tactics,
            Material wood, Material steel, Material[] factionSignals)
        {
            ink = kit.Ink; signals = factionSignals;
            Root = Group("Bridge Gambit tactical scenery", parent);
            Obstacles = new Transform[tactics.Obstacles.Count];
            Covers = new Transform[tactics.Covers.Count];
            UnitProtectionArc = kit.Cached("Directional protection front arc", () => Arc(1, .93f, -60, 60));
            UnitFlankArc = kit.Cached("Directional cover exposed rear arc", () => Arc(1, .93f, 60, 300));
            for (int i = 0; i < Obstacles.Length; i++)
            {
                var obstacle = tactics.Obstacles[i];
                var root = Obstacles[i] = Group("Solid movement and sight blocker " + i, Root);
                root.localPosition = new Vector3(obstacle.Center.x, 0, obstacle.Center.y);
                root.localRotation = Quaternion.Euler(0, -obstacle.AngleDegrees, 0);
                var body = Group("Cast supply bunker", root);
                if (obstacle.Center.x > 0) body.localRotation = Quaternion.Euler(0, 180, 0);
                // Normalized model bounds are exactly ±.5 horizontally and 0..1 vertically.
                // Scaling by the rules geometry keeps all roof ribs, doors and recognition marks inside it.
                body.localScale = new Vector3(obstacle.HalfSize.x * 2, obstacle.Height, obstacle.HalfSize.y * 2);
                const string key = "Mission solid tactical bunker";
                if (!kit.InstantiateModel(key, body))
                {
                    Part("Full footprint foundation", body, kit.Block, new Vector3(0, .045f, 0), new Vector3(1, .09f, 1), kit.Stone);
                    Part("Unbroken stone body", body, kit.Box, new Vector3(0, .49f, 0), new Vector3(.94f, .84f, .97f), kit.Stone);
                    Part("Steel cap", body, kit.Block, new Vector3(0, .94f, 0), new Vector3(1, .12f, 1), kit.Ink);
                    Part("Walnut roof inset", body, kit.Block, new Vector3(0, .998f, 0), new Vector3(.78f, .004f, .84f), wood);
                    Part("River-facing dark door", body, kit.Block, new Vector3(.475f, .40f, 0), new Vector3(.025f, .59f, .25f), kit.Ink);
                    Part("Brass lintel", body, kit.Block, new Vector3(.491f, .72f, 0), new Vector3(.016f, .045f, .31f), kit.Brass);
                    for (int end = -1; end <= 1; end += 2)
                    {
                        Part("Ivory roof recognition", body, kit.Block, new Vector3(0, .999f, end * .36f), new Vector3(.62f, .002f, .035f), kit.Ivory);
                        Part("Cast armored end rib", body, kit.Block, new Vector3(.43f, .45f, end * .43f), new Vector3(.13f, .70f, .075f), steel);
                        Part("Dark end recess", body, kit.Block, new Vector3(0, .47f, end * .488f), new Vector3(.59f, .20f, .018f), kit.Ink);
                    }
                    kit.CacheModel(key, body);
                }
            }
            for (int i = 0; i < Covers.Length; i++)
            {
                var cover = tactics.Covers[i];
                var root = Covers[i] = Group("Directional rifle cover " + i, Root);
                root.localPosition = new Vector3(cover.Center.x, .055f, cover.Center.y);
                root.localRotation = Facing(cover.Facing);
                var body = Group("Open-backed sandbag firing position", root);
                const string key = "Mission frontal rifle barricade";
                if (!kit.InstantiateModel(key, body))
                {
                    // The open back and sides are intentional: only the marked forward 120 degrees protects.
                    for (int layer = 0; layer < 2; layer++)
                        for (int bag = 0; bag < 5 - layer; bag++)
                        {
                            float z = (bag - (4 - layer) * .5f) * .61f;
                            var sack = Part("Forward canvas sandbag", body, kit.Bag, new Vector3(1.34f, .14f + layer * .23f, z),
                                new Vector3(.68f, .26f, .38f), kit.Sand);
                            sack.localRotation = Quaternion.Euler(0, 90, 0);
                        }
                    Part("Walnut firing step", body, kit.Block, new Vector3(.77f, .025f, 0), new Vector3(.44f, .05f, 2.53f), wood);
                    for (int z = -1; z <= 1; z++)
                        Part("Step seam", body, kit.Block, new Vector3(.77f, .053f, z * .68f), new Vector3(.38f, .008f, .025f), kit.Ink);
                    Part("Forward protection arc", body, kit.Cached("Rifle cover front 120 degree inlay",
                        () => Arc(cover.Radius, cover.Radius - .055f, -cover.HalfAngleDegrees, cover.HalfAngleDegrees)),
                        new Vector3(0, .012f, 0), Vector3.one, kit.Brass);
                    for (int side = -1; side <= 1; side += 2)
                        Part("Rifle position ivory inlay", body, kit.Block, new Vector3(-.25f, .016f, side * .19f), new Vector3(.65f, .025f, .065f), kit.Ivory);
                    kit.CacheModel(key, body);
                }
            }
            // Two equal three-slot capacitors sit on the ROTARY plinth, clear of its flagpole.
            var panel = Group("ROTARY physical command capacitors", Root);
            panel.localPosition = new Vector3(0, TablefrontBridgeArt.DeckFloor + .35f, 0);
            var sockets = Group("Six cast capacitor sockets", panel);
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? -.55f : .55f;
                for (int slot = 0; slot < TablefrontBridge.CommandsPerTeam; slot++)
                {
                    float z = (slot - 1) * .34f;
                    Part("Recessed command socket", sockets, kit.Cylinder, new Vector3(x, .018f, z), new Vector3(.29f, .04f, .29f), kit.Ink);
                    CommandCaps[team, slot] = Part("Available command " + team + "/" + slot, panel, kit.Cylinder,
                        new Vector3(x, .045f, z), new Vector3(.22f, .035f, .22f), signals[team]).GetComponent<Renderer>();
                }
                Part("Recharge gauge channel", sockets, kit.Block, new Vector3(x * 1.48f, .012f, 0), new Vector3(.10f, .025f, RechargeLength + .08f), kit.Ink);
                RechargeFill[team] = Part("Actual uncontested recharge " + team, panel, kit.Block,
                    new Vector3(x * 1.48f, .029f, 0), new Vector3(.07f, .016f, RechargeLength), signals[team]);
                RechargeFill[team].gameObject.SetActive(false);
            }
            kit.CacheModel("ROTARY six command capacitor sockets", sockets);
            Root.gameObject.SetActive(false);
        }

        public void Render(BattleSimulation sim)
        {
            bool active = sim != null && sim.MissionActive;
            Root.gameObject.SetActive(active);
            if (!active) return;
            for (int team = 0; team < 2; team++)
            {
                int commands = sim.Bridge.CommandsRemaining(team);
                for (int slot = 0; slot < TablefrontBridge.CommandsPerTeam; slot++)
                {
                    var cap = CommandCaps[team, slot]; bool available = slot < commands;
                    cap.sharedMaterial = available ? signals[team] : ink;
                    cap.enabled = available; // A spent command leaves a genuinely empty socket, also readable without color.
                }
                float fraction = Mathf.Clamp01(sim.BridgeRechargeSeconds(team) / BattleSimulation.BridgeRechargeRequired);
                var rotary = sim.Points[2];
                bool charging = commands < TablefrontBridge.CommandsPerTeam && fraction > .0001f && rotary.Owner == team &&
                    !rotary.Contested && (rotary.CapturingTeam < 0 || rotary.CapturingTeam == team);
                var fill = RechargeFill[team]; fill.gameObject.SetActive(charging);
                fill.localScale = new Vector3(.07f, .016f, RechargeLength * fraction);
                fill.localPosition = new Vector3((team == 0 ? -.55f : .55f) * 1.48f, .029f, -RechargeLength * (1 - fraction) * .5f);
            }
        }

        public static Quaternion Facing(Vector2 direction)
            => Quaternion.Euler(0, -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 0);

        static Mesh Arc(float outer, float inner, float fromDegrees, float toDegrees)
        {
            int segments = Mathf.CeilToInt((toDegrees - fromDegrees) / 5);
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = direction * outer; vertices[i * 2 + 1] = direction * inner;
                uv[i * 2] = new Vector2(i / (float)segments, 1); uv[i * 2 + 1] = new Vector2(i / (float)segments, 0);
                if (i == segments) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        static Transform Group(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Transform Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var t = Group(name, parent); t.localPosition = position; t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; return t;
        }
    }
}
