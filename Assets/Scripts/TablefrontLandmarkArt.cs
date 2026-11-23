using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>Purpose-built cast scenery for the campaign table. All geometry is visual only.</summary>
    public static class TablefrontLandmarkArt
    {
        public sealed class Kit
        {
            internal readonly Material Ink, Ivory, Brass, Stone, Sand;
            readonly Func<Mesh, Mesh> own;
            readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            readonly Dictionary<string, RenderBatch[]> models = new Dictionary<string, RenderBatch[]>();

            public Kit(Func<Mesh, Mesh> ownMesh, Material ink, Material ivory, Material brass, Material stone, Material sand)
            { own = ownMesh; Ink = ink; Ivory = ivory; Brass = brass; Stone = stone; Sand = sand; }

            internal Mesh Cached(string name, Func<Mesh> create)
            {
                if (meshes.TryGetValue(name, out var mesh)) return mesh;
                mesh = create(); mesh.name = name; mesh = own(mesh); meshes.Add(name, mesh); return mesh;
            }
            internal Mesh Box => Cached("Landmark beveled casting", () => BeveledBox(.095f, .14f));
            internal Mesh Block => Cached("Landmark fine chamfer", () => BeveledBox(.045f, .07f));
            internal Mesh Cylinder => Cached("Landmark machined cylinder", () => Lathe(new[] { new Vector2(.46f, -.5f), new Vector2(.5f, -.43f), new Vector2(.5f, .43f), new Vector2(.46f, .5f) }, 32));
            internal Mesh Bag => Cached("Stitched canvas sandbag", Sandbag);

            internal bool InstantiateModel(string key, Transform parent)
            {
                if (!models.TryGetValue(key, out var batches)) return false;
                foreach (var batch in batches)
                    MeshPart(batch.Mesh.name, parent, batch.Mesh, Vector3.zero, Vector3.one, batch.Material);
                return true;
            }

            internal void CacheModel(string key, Transform parent)
            {
                var groups = new Dictionary<Material, List<CombineInstance>>();
                Matrix4x4 local = parent.worldToLocalMatrix;
                foreach (var filter in parent.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null || renderer.sharedMaterial == null) continue;
                    Material material = renderer.sharedMaterial;
                    if (!groups.TryGetValue(material, out var instances))
                    { instances = new List<CombineInstance>(); groups.Add(material, instances); }
                    instances.Add(new CombineInstance { mesh = filter.sharedMesh, transform = local * filter.transform.localToWorldMatrix });
                }
                var batches = new List<RenderBatch>(groups.Count);
                foreach (var group in groups)
                {
                    int vertices = 0;
                    foreach (var instance in group.Value) vertices += instance.mesh.vertexCount;
                    var mesh = new Mesh { name = "Premium " + key + " / " + group.Key.name };
                    if (vertices > 65535) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(group.Value.ToArray(), true, true, false);
                    mesh.RecalculateBounds();
                    batches.Add(new RenderBatch { Material = group.Key, Mesh = own(mesh) });
                }
                models.Add(key, batches.ToArray());
                // Deactivate immediately so deferred Play Mode destruction cannot double-render the model.
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var old = parent.GetChild(i).gameObject; old.SetActive(false);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old); else UnityEngine.Object.DestroyImmediate(old);
                }
                InstantiateModel(key, parent);
            }
        }

        sealed class RenderBatch { public Mesh Mesh; public Material Material; }

        public static void BuildHeadquarters(Kit k, Transform root, Material faction, Material signal)
        {
            string key = "HQ " + faction.GetEntityId() + "/" + signal.GetEntityId();
            var body = Group("Premium HQ art", root, Vector3.zero);
            if (k.InstantiateModel(key, body)) return;
            BuildHeadquartersGeometry(k, body, faction, signal);
            k.CacheModel(key, body);
        }

        static void BuildHeadquartersGeometry(Kit k, Transform root, Material faction, Material signal)
        {
            // Footprint deliberately matches the existing mechanical headquarters exclusion radius.
            var baseMesh = k.Cached("Cast HQ gaming plinth", () => Lathe(new[] {
                new Vector2(2.59f, .01f), new Vector2(2.77f, .07f), new Vector2(2.77f, .13f), new Vector2(2.62f, .23f) }, 64));
            MeshPart("Cast headquarters plinth", root, baseMesh, Vector3.zero, Vector3.one, k.Ink);
            Box(k, "Foundation slab", root, new Vector3(0, .28f, 0), new Vector3(3.88f, .22f, 3.02f), k.Stone);
            Box(k, "Battered command bunker", root, new Vector3(0, .89f, 0), new Vector3(3.24f, 1.11f, 2.45f), k.Stone);
            Box(k, "Enamel armor roof", root, new Vector3(0, 1.49f, 0), new Vector3(3.61f, .26f, 2.82f), faction);
            Box(k, "Dark roof recess", root, new Vector3(-.2f, 1.636f, -.08f), new Vector3(2.5f, .035f, 1.88f), k.Ink, true);
            Box(k, "Raised roof hatch", root, new Vector3(-.36f, 1.685f, -.04f), new Vector3(1.64f, .1f, 1.43f), faction);
            Box(k, "Ivory recognition bar", root, new Vector3(-.77f, 1.743f, -.04f), new Vector3(.18f, .022f, 1.17f), k.Ivory, true);
            Box(k, "Brass hatch handle", root, new Vector3(-.05f, 1.765f, -.11f), new Vector3(.22f, .035f, .07f), k.Brass, true);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(k, "Chamfered corner buttress", root, new Vector3(side * 1.54f, .89f, -.94f), new Vector3(.29f, 1.08f, .47f), k.Ink);
                Box(k, "Enamel corner cap", root, new Vector3(side * 1.54f, 1.19f, -.94f), new Vector3(.31f, .19f, .49f), faction);
                Box(k, "Bunker side inset", root, new Vector3(side * 1.625f, .94f, .16f), new Vector3(.045f, .44f, 1.13f), k.Ink, true);
                for (int slot = 0; slot < 5; slot++)
                    Box(k, "Cast cooling louvre", root, new Vector3(side * 1.66f, .94f, -.26f + slot * .21f), new Vector3(.055f, .28f, .055f), k.Stone, true);
            }

            Box(k, "Front command viewport reveal", root, new Vector3(.34f, 1.04f, -1.232f), new Vector3(1.67f, .3f, .075f), k.Ink, true);
            Box(k, "Recessed command viewport", root, new Vector3(.34f, 1.035f, -1.281f), new Vector3(1.42f, .11f, .032f), signal, true);
            for (int mullion = 0; mullion < 3; mullion++)
                Box(k, "Viewport mullion", root, new Vector3(-.21f + mullion * .55f, 1.035f, -1.308f), new Vector3(.045f, .15f, .025f), k.Ink, true);
            Box(k, "Front armor insignia", root, new Vector3(-.91f, .89f, -1.251f), new Vector3(.52f, .52f, .055f), faction, true);
            Box(k, "Insignia vertical", root, new Vector3(-.91f, .89f, -1.287f), new Vector3(.085f, .31f, .025f), k.Ivory, true);
            Box(k, "Insignia horizontal", root, new Vector3(-.91f, .89f, -1.289f), new Vector3(.29f, .08f, .025f), k.Ivory, true);

            Box(k, "Recessed entry surround", root, new Vector3(0, .8f, 1.247f), new Vector3(1.13f, .88f, .12f), k.Ink);
            Box(k, "Armored entry door", root, new Vector3(0, .79f, 1.322f), new Vector3(.82f, .7f, .04f), faction, true);
            Box(k, "Door center seam", root, new Vector3(0, .79f, 1.35f), new Vector3(.035f, .66f, .015f), k.Ink, true);
            Box(k, "Entry lintel", root, new Vector3(0, 1.263f, 1.29f), new Vector3(1.26f, .12f, .2f), k.Ivory, true);
            for (int step = 0; step < 3; step++)
                Box(k, "Cast entry step", root, new Vector3(0, .25f - step * .055f, 1.55f + step * .23f), new Vector3(1.16f, .14f - step * .025f, .3f), k.Stone);
            foreach (float x in new[] { -.32f, .32f })
                Box(k, "Brass door grip", root, new Vector3(x, .8f, 1.363f), new Vector3(.035f, .14f, .025f), k.Brass, true);

            Box(k, "Roof communications pedestal", root, new Vector3(1.02f, 1.76f, .36f), new Vector3(.52f, .3f, .52f), k.Ink);
            Rod(k, "Communications mast", root, new Vector3(1.02f, 2.23f, .36f), .055f, .82f, k.Brass);
            var dishRoot = Group("Parabolic receiver mount", root, new Vector3(1.02f, 2.63f, .36f));
            dishRoot.localRotation = Quaternion.Euler(-32, 18, 8);
            var dishMesh = k.Cached("Machined parabolic receiver", Dish);
            MeshPart("Ivory parabolic receiver", dishRoot, dishMesh, Vector3.zero, Vector3.one, k.Ivory);
            Rod(k, "Receiver feed support", dishRoot, new Vector3(0, .25f, 0), .025f, .3f, k.Brass);
            Rod(k, "Receiver feed", dishRoot, new Vector3(0, .41f, 0), .07f, .11f, k.Ink);
            Rod(k, "Auxiliary aerial", root, new Vector3(1.4f, 2.17f, -.78f), .018f, 1.0f, k.Brass);
            Rod(k, "Aerial enamel finial", root, new Vector3(1.4f, 2.7f, -.78f), .038f, .065f, signal);
            // A plaque and fasteners read as cast gaming scenery in close-ups without new labels.
            Box(k, "Command foundation plaque", root, new Vector3(-1.0f, .42f, 1.43f), new Vector3(.54f, .15f, .04f), k.Brass, true);
            foreach (float x in new[] { -1.64f, 1.64f }) foreach (float z in new[] { -1.23f, 1.23f })
                Rod(k, "Roof retaining bolt", root, new Vector3(x, 1.64f, z), .045f, .025f, k.Brass);
        }

        public static Renderer BuildPoint(Kit k, Transform root, Material neutral)
        {
            var body = Group("Premium objective art", root, Vector3.zero);
            if (!k.InstantiateModel("Objective", body))
            { BuildPointGeometry(k, body); k.CacheModel("Objective", body); }
            var flagMesh = k.Cached("Cast rippled enamel pennant", Pennant);
            return MeshPart("Enamel faction pennant", root, flagMesh, Vector3.zero, Vector3.one, neutral).GetComponent<Renderer>();
        }

        static void BuildPointGeometry(Kit k, Transform root)
        {
            var baseMesh = k.Cached("Machined objective base", () => Lathe(new[] {
                new Vector2(1.02f, .01f), new Vector2(1.17f, .065f), new Vector2(1.17f, .13f),
                new Vector2(1.03f, .21f), new Vector2(1.03f, .24f) }, 48));
            MeshPart("Objective cast plinth", root, baseMesh, Vector3.zero, Vector3.one, k.Ink);
            var trimMesh = k.Cached("Objective brass inset", () => Lathe(new[] {
                new Vector2(.96f, .23f), new Vector2(.99f, .25f), new Vector2(.99f, .29f), new Vector2(.92f, .315f) }, 48));
            MeshPart("Machined brass perimeter", root, trimMesh, Vector3.zero, Vector3.one, k.Brass);
            Rod(k, "Objective slate disk", root, new Vector3(0, .319f, 0), .84f, .035f, k.Stone);
            Rod(k, "Cast pole socket", root, new Vector3(0, .48f, 0), .19f, .29f, k.Brass);
            Rod(k, "Flagpole", root, new Vector3(0, 1.7f, 0), .035f, 2.26f, k.Brass);
            Rod(k, "Flagpole collar", root, new Vector3(0, 1.88f, 0), .06f, .09f, k.Ivory);
            Rod(k, "Flagpole finial", root, new Vector3(0, 2.91f, 0), .095f, .12f, k.Brass);
            Box(k, "Pennant ivory hoist seam", root, new Vector3(.12f, 2.43f, .045f), new Vector3(.045f, .61f, .025f), k.Ivory, true);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector3 p = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .89f + Vector3.up * .34f;
                Rod(k, "Plinth brass fastener", root, p, .034f, .018f, k.Brass);
            }
        }

        public static void BuildCover(Kit k, Transform root, float radius)
        {
            radius = Mathf.Max(.8f, radius);
            string key = "Cover " + radius.ToString("R", CultureInfo.InvariantCulture);
            var body = Group("Premium cover art", root, Vector3.zero);
            if (k.InstantiateModel(key, body)) return;
            BuildCoverGeometry(k, body, radius);
            k.CacheModel(key, body);
        }

        static void BuildCoverGeometry(Kit k, Transform root, float radius)
        {
            // Four low staggered earthworks leave obvious approaches and never add collision.
            for (int section = 0; section < 4; section++)
            {
                float angle = section * 90 + 20;
                var wall = Group("Staggered sandbag emplacement", root, Vector3.zero);
                wall.localRotation = Quaternion.Euler(0, angle, 0);
                for (int layer = 0; layer < 2; layer++)
                {
                    int count = layer == 0 ? 3 : 2;
                    for (int bag = 0; bag < count; bag++)
                    {
                        float x = (bag - (count - 1) * .5f) * .62f;
                        float z = radius * .74f - Mathf.Abs(x) * .075f;
                        var sack = MeshPart("Bulged canvas sandbag", wall, k.Bag, new Vector3(x, .15f + layer * .235f, z), new Vector3(.7f, .27f, .37f), k.Sand);
                        sack.localRotation = Quaternion.Euler(0, -x * 10, bag % 2 == 0 ? -2 : 2);
                        Box(k, "Sandbag binding seam", sack, new Vector3(0, .015f, -.455f), new Vector3(.58f, .045f, .025f), k.Sand, true);
                    }
                }
                var board = Box(k, "Protective timber footboard", wall, new Vector3(0, .027f, radius * .49f), new Vector3(1.23f, .045f, .38f), k.Ink, true);
                for (int plank = -1; plank <= 1; plank++)
                    Box(k, "Timber board seam", board, new Vector3(plank * .27f, .51f, 0), new Vector3(.017f, .018f, .86f), k.Stone, true);
            }
            for (int side = -1; side <= 1; side += 2)
                Box(k, "Rifle cover recognition inlay", root, new Vector3(side * .23f, .027f, 0), new Vector3(.07f, .025f, .62f), k.Ivory, true);
        }

        static Transform Group(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        static Transform MeshPart(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var t = Group(name, parent, position); t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return t;
        }
        static Transform Box(Kit k, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool fine = false)
            => MeshPart(name, parent, fine ? k.Block : k.Box, position, scale, material);
        static Transform Rod(Kit k, string name, Transform parent, Vector3 position, float radius, float height, Material material)
            => MeshPart(name, parent, k.Cylinder, position, new Vector3(radius * 2, height, radius * 2), material);

        sealed class Geometry
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = V.Count; var normal = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (int i = 0; i < 4; i++) N.Add(normal);
                float u = Vector3.Distance(a, d), v = Vector3.Distance(a, b);
                UV.Add(Vector2.zero); UV.Add(new Vector2(0, v)); UV.Add(new Vector2(u, v)); UV.Add(new Vector2(u, 0));
                T.Add(start); T.Add(start + 1); T.Add(start + 2); T.Add(start); T.Add(start + 2); T.Add(start + 3);
            }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int start = V.Count; V.Add(a); V.Add(b); V.Add(c); N.Add(normal); N.Add(normal); N.Add(normal);
                UV.Add(new Vector2(a.x, a.z)); UV.Add(new Vector2(b.x, b.z)); UV.Add(new Vector2(c.x, c.z));
                T.Add(start); T.Add(start + 1); T.Add(start + 2);
            }
            public Mesh Finish()
            {
                var mesh = new Mesh(); mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
            }
        }

        static Mesh BeveledBox(float bevel, float corner)
        {
            var g = new Geometry();
            var levels = new[] { -.5f, -.5f + bevel, .5f - bevel, .5f };
            var rings = new Vector3[4][];
            for (int level = 0; level < 4; level++)
            {
                float h = level == 0 || level == 3 ? .5f - bevel : .5f;
                float c = Mathf.Min(corner, h * .6f);
                rings[level] = new[] {
                    new Vector3(-h+c,levels[level],-h), new Vector3(h-c,levels[level],-h),
                    new Vector3(h,levels[level],-h+c), new Vector3(h,levels[level],h-c),
                    new Vector3(h-c,levels[level],h), new Vector3(-h+c,levels[level],h),
                    new Vector3(-h,levels[level],h-c), new Vector3(-h,levels[level],-h+c) };
            }
            for (int level = 0; level < 3; level++) for (int i = 0; i < 8; i++)
            { int j = (i + 1) % 8; g.Face(rings[level][i], rings[level + 1][i], rings[level + 1][j], rings[level][j]); }
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                g.Triangle(new Vector3(0, .5f, 0), rings[3][j], rings[3][i], Vector3.up);
                g.Triangle(new Vector3(0, -.5f, 0), rings[0][i], rings[0][j], Vector3.down);
            }
            return g.Finish();
        }

        static Mesh Lathe(Vector2[] profile, int segments)
        {
            var g = new Geometry();
            for (int level = 0; level < profile.Length - 1; level++)
            {
                float dr = profile[level + 1].x - profile[level].x, dy = profile[level + 1].y - profile[level].y;
                int start = g.V.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments; var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var normal = (radial * dy + Vector3.down * dr).normalized;
                    g.V.Add(radial * profile[level].x + Vector3.up * profile[level].y); g.N.Add(normal);
                    g.V.Add(radial * profile[level + 1].x + Vector3.up * profile[level + 1].y); g.N.Add(normal);
                    float u = (float)i / segments * Mathf.PI * (profile[level].x + profile[level + 1].x);
                    g.UV.Add(new Vector2(u, profile[level].y)); g.UV.Add(new Vector2(u, profile[level + 1].y));
                    if (i < segments)
                    { int a = start + i * 2; g.T.Add(a); g.T.Add(a + 1); g.T.Add(a + 3); g.T.Add(a); g.T.Add(a + 3); g.T.Add(a + 2); }
                }
            }
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                var first = profile[0]; var last = profile[profile.Length - 1];
                g.Triangle(Vector3.up * first.y, new Vector3(Mathf.Cos(a) * first.x, first.y, Mathf.Sin(a) * first.x), new Vector3(Mathf.Cos(b) * first.x, first.y, Mathf.Sin(b) * first.x), Vector3.down);
                g.Triangle(Vector3.up * last.y, new Vector3(Mathf.Cos(b) * last.x, last.y, Mathf.Sin(b) * last.x), new Vector3(Mathf.Cos(a) * last.x, last.y, Mathf.Sin(a) * last.x), Vector3.up);
            }
            return g.Finish();
        }

        static Mesh Sandbag()
        {
            const int longitude = 24, latitude = 12; const float exponent = .53f;
            var g = new Geometry();
            float Signed(float value) => Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
            for (int y = 0; y <= latitude; y++)
            {
                float v = (float)y / latitude, a = (v - .5f) * Mathf.PI;
                for (int x = 0; x <= longitude; x++)
                {
                    float u = (float)x / longitude, b = u * Mathf.PI * 2;
                    var p = new Vector3(Signed(Mathf.Cos(a)) * Signed(Mathf.Cos(b)), Signed(Mathf.Sin(a)), Signed(Mathf.Cos(a)) * Signed(Mathf.Sin(b))) * .5f;
                    float n = 2 / exponent;
                    var normal = new Vector3(Mathf.Sign(p.x) * Mathf.Pow(Mathf.Abs(p.x * 2), n - 1), Mathf.Sign(p.y) * Mathf.Pow(Mathf.Abs(p.y * 2), n - 1), Mathf.Sign(p.z) * Mathf.Pow(Mathf.Abs(p.z * 2), n - 1)).normalized;
                    g.V.Add(p); g.N.Add(normal); g.UV.Add(new Vector2(u, v));
                    if (y < latitude && x < longitude)
                    { int index = y * (longitude + 1) + x; g.T.Add(index); g.T.Add(index + longitude + 1); g.T.Add(index + longitude + 2); g.T.Add(index); g.T.Add(index + longitude + 2); g.T.Add(index + 1); }
                }
            }
            return g.Finish();
        }

        static Mesh Pennant()
        {
            var g = new Geometry(); const int panels = 10;
            Vector3 Point(float u, float v) => new Vector3(.08f + 1.16f * u, 2.78f - .66f * v - .065f * u, Mathf.Sin(u * Mathf.PI * 2 - .4f) * .09f * u);
            for (int i = 0; i < panels; i++)
            {
                float u = (float)i / panels, next = (float)(i + 1) / panels;
                Vector3 a = Point(u, 0), b = Point(u, 1), c = Point(next, 1), d = Point(next, 0);
                g.Face(a, b, c, d); g.Face(d - Vector3.forward * .015f, c - Vector3.forward * .015f, b - Vector3.forward * .015f, a - Vector3.forward * .015f);
            }
            return g.Finish();
        }

        static Mesh Dish()
        {
            var g = new Geometry(); const int segments = 32, rings = 6;
            Vector3 Point(int ring, int segment, float offset)
            {
                float r = .025f + .55f * ring / rings, a = segment * Mathf.PI * 2 / segments;
                return new Vector3(Mathf.Cos(a) * r, r * r * .63f + offset, Mathf.Sin(a) * r);
            }
            for (int ring = 0; ring < rings; ring++) for (int segment = 0; segment < segments; segment++)
            {
                g.Face(Point(ring, segment, 0), Point(ring, segment + 1, 0), Point(ring + 1, segment + 1, 0), Point(ring + 1, segment, 0));
                g.Face(Point(ring + 1, segment, -.035f), Point(ring + 1, segment + 1, -.035f), Point(ring, segment + 1, -.035f), Point(ring, segment, -.035f));
            }
            return g.Finish();
        }
    }
}
