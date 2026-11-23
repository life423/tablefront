using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>
    /// A single continuous campaign-board surface and handcrafted scenery. Its relief is
    /// cosmetic, below the miniature bases, and never adds a collider or changes the map.
    /// All generated resources belong to the caller; no static Unity objects are retained.
    /// </summary>
    public sealed class TablefrontTerrainArt
    {
        public sealed class Palette
        {
            public Material Ground, Soil, Stone, Bark, Grass, GrassDry, Leaves, LeavesLight;
            public Func<Mesh, Mesh> OwnMesh;
            public Func<Texture2D, Texture2D> OwnTexture;
        }

        struct Road
        {
            public Vector2 A, B;
            public float Width;
            public Road(float ax, float az, float bx, float bz, float width)
            { A = new Vector2(ax, az); B = new Vector2(bx, bz); Width = width; }
            public Road(Vector2 a, Vector2 b, float width) { A = a; B = b; Width = width; }
        }

        readonly Road[] Roads =
        {
            new Road(-10, -6, 10, -6, .92f),
            new Road(-10, -6, 0, 12, .84f),
            new Road(10, -6, 0, 12, .84f),
            new Road(0, -21, 0, -6, .74f),
            new Road(18.2f, 10.5f, 10, -6, .73f),
            new Road(-18.2f, 10.5f, -10, -6, .73f)
        };
        readonly Vector2[] Landmarks =
        {
            new Vector2(0, -21), new Vector2(18.2f, 10.5f), new Vector2(-18.2f, 10.5f),
            new Vector2(-10, -6), new Vector2(10, -6), new Vector2(0, 12)
        };
        readonly Vector2[] Groves =
        {
            new Vector2(-19, -15), new Vector2(-21, -3), new Vector2(-16, 3),
            new Vector2(20, -15), new Vector2(21, 0), new Vector2(15, 4),
            new Vector2(-11, 19), new Vector2(11, 20), new Vector2(-3, 21)
        };
        readonly Vector2[] Outcrops =
        {
            new Vector2(-15, -12), new Vector2(14, -13), new Vector2(-5, 18),
            new Vector2(17, -2), new Vector2(-20, 17), new Vector2(21, 18),
            new Vector2(-21, -21), new Vector2(20, -21)
        };

        public const float MaximumGroundHeight = .04f;
        readonly int style;

        TablefrontTerrainArt(BattlefieldDefinition battlefield)
        {
            if (battlefield == null) return;
            style = battlefield.TerrainStyle;
            var roads = new List<Road>();
            var clear = new List<Vector2>(battlefield.BasePositions);
            clear.AddRange(battlefield.PointPositions);
            for (int i = 0; i < battlefield.PointPositions.Length; i++)
            {
                Vector2 point = battlefield.PointPositions[i];
                roads.Add(new Road(point, battlefield.PointPositions[(i + 1) % battlefield.PointPositions.Length], style == 1 ? .70f : .90f));
                Vector2 radial = point.normalized, flank = new Vector2(-radial.y, radial.x);
                clear.Add(point - flank * battlefield.CoverOffset);
                clear.Add(point + flank * battlefield.CoverOffset);
            }
            foreach (Vector2 hq in battlefield.BasePositions)
            {
                Vector2 nearest = battlefield.PointPositions[0];
                foreach (Vector2 point in battlefield.PointPositions) if ((point - hq).sqrMagnitude < (nearest - hq).sqrMagnitude) nearest = point;
                roads.Add(new Road(hq, nearest, .77f));
            }
            if (style == 4)
            {
                // The mission's only water crossing is the actual movable bridge.
                // Keep all painted supply roads on the banks and lead to both dock choices.
                roads.Clear();
                roads.Add(new Road(new Vector2(-18, -12), new Vector2(-12, -8), .85f));
                roads.Add(new Road(new Vector2(18, 12), new Vector2(12, 8), .85f));
                foreach (int bank in new[] { -1, 1 })
                    foreach (int landing in new[] { -1, 1 })
                    {
                        Vector2 end = new Vector2(bank * 6, landing * 8);
                        roads.Add(new Road(new Vector2(bank * 12, bank * 8), end, .92f));
                        clear.Add(end);
                    }
            }
            Roads = roads.ToArray(); Landmarks = clear.ToArray();
            float angle = style * .31f;
            Vector2 Rotate(Vector2 p) => new Vector2(p.x * Mathf.Cos(angle) - p.y * Mathf.Sin(angle), p.x * Mathf.Sin(angle) + p.y * Mathf.Cos(angle)) * .94f;
            for (int i = 0; i < Groves.Length; i++) Groves[i] = Rotate(Groves[i]);
            for (int i = 0; i < Outcrops.Length; i++) Outcrops[i] = Rotate(Outcrops[i]);
        }

        public static void Build(Transform parent, Palette palette, BattlefieldDefinition battlefield = null)
            => new TablefrontTerrainArt(battlefield).BuildLandscape(parent, palette);

        void BuildLandscape(Transform parent, Palette palette)
        {
            if (!parent || palette == null || !palette.Ground || !palette.Stone || !palette.Bark ||
                !palette.Grass || !palette.GrassDry || !palette.Leaves || !palette.LeavesLight ||
                palette.OwnMesh == null || palette.OwnTexture == null)
                throw new ArgumentException("Terrain requires authored materials and resource owners.");

            var root = Group("Handcrafted campaign landscape", parent);
            PaintGround(palette);
            Part("Continuous flock and worn earth", root, palette.OwnMesh(GroundMesh()), Vector3.zero,
                Vector3.one, palette.Ground);

            var rocks = new Mesh[3];
            var crowns = new Mesh[3];
            var tufts = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                rocks[i] = palette.OwnMesh(RockMesh(771 + i * 67));
                crowns[i] = palette.OwnMesh(CrownMesh(113 + i * 89));
                tufts[i] = palette.OwnMesh(GrassMesh(291 + i * 41));
            }
            var branch = palette.OwnMesh(TaperedBranch());
            var random = new System.Random(71942 + style * 733);

            // Small, deliberately placed groves frame each approach instead of carpeting
            // the tactical triangle. Objective rings and all six routes stay unobscured.
            int treeIndex = 0;
            foreach (var grove in Groves)
            {
                int count = style == 2 ? 1 : treeIndex % 3 == 0 ? 3 : 2;
                for (int j = 0; j < count; j++)
                {
                    Vector2 p = grove + new Vector2(Range(random, -1.3f, 1.3f), Range(random, -1.2f, 1.2f));
                    if (!SceneryClear(p, 4.8f, 2.4f)) continue;
                    float size = Range(random, .78f, 1.12f);
                    Tree(root, p, size, treeIndex++, branch, crowns, palette, random);
                }
            }

            // Slate comes in irregular clusters with individual slanted strata. Low
            // outcrops and scattered chips never imply a new impassable wall.
            int rockIndex = 0;
            foreach (var centre in Outcrops)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = centre + new Vector2(Range(random, -1.2f, 1.2f), Range(random, -.8f, .8f));
                    if (!SceneryClear(p, 4.1f, 1.8f)) continue;
                    float size = i == 0 ? Range(random, .75f, 1.05f) : Range(random, .28f, .62f);
                    var part = Part("Sculpted slate strata", root, rocks[rockIndex++ % rocks.Length],
                        V3(p, GroundHeight(p.x, p.y) - .025f), new Vector3(size * 1.2f, size * .65f, size), palette.Stone);
                    part.localRotation = Quaternion.Euler(Range(random, -7, 7), Range(random, 0, 360), Range(random, -9, 9));
                }

            // Small planted clusters belong to landforms. Their bare surroundings stay
            // smooth at RTS distance instead of reading as random dark pepper dots.
            for (int i = 0; i < 264; i++)
            {
                int cluster = i / 11;
                Vector2 anchor;
                if (cluster < Groves.Length) anchor = Groves[cluster];
                else if (cluster < Groves.Length + Outcrops.Length) anchor = Outcrops[cluster - Groves.Length];
                else
                {
                    Road road = Roads[(cluster - Groves.Length - Outcrops.Length) % Roads.Length];
                    Vector2 axis = (road.B - road.A).normalized;
                    Vector2 across = new Vector2(-axis.y, axis.x);
                    anchor = Vector2.Lerp(road.A, road.B, .36f + (cluster % 3) * .13f) + across * (road.Width + 1.5f) * (cluster % 2 == 0 ? 1 : -1);
                }
                float angle = Range(random, 0, Mathf.PI * 2);
                float radius = Range(random, .35f, 1.85f);
                Vector2 p = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!SceneryClear(p, 3.8f, .80f)) continue;
                float size = Range(random, .85f, 1.4f);
                Material material = cluster % 4 == 0 ? palette.GrassDry : palette.Grass;
                var tuft = Part("Hand-planted grass tuft", root, tufts[i % tufts.Length],
                    V3(p, GroundHeight(p.x, p.y)), new Vector3(size, size * Range(random, .65f, 1), size), material);
                tuft.localRotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
            }

            // Sparse small stones in the dry verges break up the smooth board surface.
            for (int i = 0; i < 44; i++)
            {
                Vector2 p = new Vector2(Range(random, -23.5f, 23.5f), Range(random, -23.5f, 23.5f));
                RoadDistance(p, out float lateral, out float width);
                if (Mathf.Abs(lateral) > width + .7f || !SceneryClear(p, 3.4f, .24f)) continue;
                float size = Range(random, .055f, .13f);
                var pebble = Part("Loose verge gravel", root, rocks[i % rocks.Length],
                    V3(p, GroundHeight(p.x, p.y)), new Vector3(size * 1.4f, size * .55f, size), palette.Stone);
                pebble.localEulerAngles = new Vector3(0, Range(random, 0, 360), 0);
            }
        }

        void PaintGround(Palette palette)
        {
            const int size = 1024;
            var pixels = new Color32[size * size];
            var mask = new Color32[pixels.Length];
            var height = new float[pixels.Length];
            Color lush = Color.Lerp(TablePalette.Terrain, TablePalette.Foliage, .48f);
            Color dry = Color.Lerp(TablePalette.Terrain, TablePalette.Sand, .56f);
            Color soil = Color.Lerp(TablePalette.Road, TablePalette.Walnut, .26f);
            Color dust = Color.Lerp(TablePalette.Road, TablePalette.Stone, .18f);
            Color woodland = Color.Lerp(lush, TablePalette.Walnut, .22f) * .92f;
            if (style == 1)
            {
                lush = Color.Lerp(lush, TablePalette.Stone, .23f);
                dry = Color.Lerp(dry, TablePalette.Stone, .30f);
                soil = Color.Lerp(soil, TablePalette.Stone, .35f);
            }
            else if (style == 2)
            {
                lush = Color.Lerp(TablePalette.Sand, TablePalette.Road, .22f);
                dry = Color.Lerp(TablePalette.Sand, TablePalette.Walnut, .13f);
                soil = Color.Lerp(TablePalette.Road, TablePalette.Walnut, .37f);
                woodland = Color.Lerp(TablePalette.Sand, TablePalette.Foliage, .28f);
            }

            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float wx = x * (50f / (size - 1)) - 25;
                float wz = y * (50f / (size - 1)) - 25;
                float broad = Mathf.PerlinNoise(wx * .075f + 42.1f, wz * .075f + 57.4f);
                float patches = Mathf.PerlinNoise(wx * .43f + 71.3f, wz * .43f + 28.7f);
                float fine = Mathf.PerlinNoise(wx * 6.4f + 61, wz * 6.4f + 83);
                float fleck = Hash(x, y);
                Vector2 p = new Vector2(wx, wz);
                RoadDistance(p, out float lateral, out float width);
                float warp = (patches - .5f) * .38f;
                float distance = Mathf.Abs(lateral) + warp;
                float road = 1 - SmoothEdge(width * .78f, width + .35f, distance);
                float rut = Mathf.Exp(-Mathf.Pow((Mathf.Abs(lateral) - width * .48f) / .12f, 2));
                float centreGrass = Mathf.Exp(-Mathf.Pow(lateral / .16f, 2)) * .08f;
                float exposed = SmoothEdge(.59f, .76f, patches) * (1 - road) * .35f;
                Color grass = Color.Lerp(lush, dry, SmoothEdge(.31f, .68f, broad));
                grass *= .95f + fine * .075f + (fleck - .5f) * .025f;
                grass = Color.Lerp(grass, soil, exposed);
                float forest = 0;
                for (int g = 0; g < Groves.Length; g++)
                {
                    float squareDistance = (p - Groves[g]).sqrMagnitude;
                    forest = Mathf.Max(forest, (1 - SmoothEdge(1.2f, 13f, squareDistance)) * (.60f + patches * .40f));
                }
                grass = Color.Lerp(grass, woodland, forest * .65f);
                Color earth = Color.Lerp(soil, dust, .30f + broad * .38f);
                earth *= .97f + fine * .06f + (fleck - .5f) * .035f;
                earth = Color.Lerp(earth, TablePalette.Walnut, rut * .20f);
                earth = Color.Lerp(earth, grass, centreGrass);
                Color color = Color.Lerp(grass, earth, road);
                if (style == 1)
                {
                    // Pale slate seams are shallow flock details, not movement barriers.
                    float seam = 1 - SmoothEdge(.12f, 1.15f, Mathf.Abs(wz - Mathf.Sin(wx * .15f) * 4f - 7f));
                    color = Color.Lerp(color, TablePalette.Stone, seam * (1 - road) * .32f);
                }
                if (style == 3)
                {
                    // A painted shallow stream with dry route crossings: fully passable
                    // under the same simulation rules as the rest of the board.
                    // This bend passes through the three actual ford objectives:
                    // (-6.25,-10.825), (12.5,0), (-6.25,10.825). Normalize by
                    // its slope so its painted width stays consistent around the bend.
                    float riverX = 12.5f - .16f * wz * wz;
                    float streamDistance = Mathf.Abs(wx - riverX) / Mathf.Sqrt(1 + .1024f * wz * wz);
                    float bank = 1 - SmoothEdge(1.2f, 2.2f, streamDistance);
                    float water = (1 - SmoothEdge(.92f, 1.42f, streamDistance)) * (1 - road);
                    color = Color.Lerp(color, Color.Lerp(TablePalette.Stone, TablePalette.Sand, .45f), bank * (1 - road) * .45f);
                    color = Color.Lerp(color, Color.Lerp(TablePalette.SkyBounce, TablePalette.Ink, .48f), water * .88f);
                }
                if (style == 4)
                {
                    float bank = 1 - SmoothEdge(3.49f, 3.88f, Mathf.Abs(wx));
                    float water = 1 - SmoothEdge(3.44f, 3.56f, Mathf.Abs(wx));
                    color = Color.Lerp(color, Color.Lerp(TablePalette.Stone, TablePalette.Sand, .55f), bank * .85f);
                    Color channel = Color.Lerp(TablePalette.SkyBounce, TablePalette.Ink, .57f);
                    channel *= .94f + .035f * Mathf.Sin(wz * .72f + wx * .27f) + fine * .025f;
                    color = Color.Lerp(color, channel, water);
                }
                // Tiny palette-derived mineral flecks are deliberately sparse, avoiding
                // a high-contrast noise blanket when the camera pulls back.
                if (fleck > .995f) color = Color.Lerp(color, TablePalette.Stone, .18f);
                int index = y * size + x;
                pixels[index] = color;
                float smooth = Mathf.Lerp(.12f, .20f, road);
                if (style == 4 && Mathf.Abs(wx) < 3.5f) smooth = .48f;
                byte ao = (byte)Mathf.RoundToInt((.91f + fine * .08f) * 255);
                mask[index] = new Color32(0, ao, 0, (byte)Mathf.RoundToInt(smooth * 255));
                height[index] = (fine * .65f + fleck * .35f) * Mathf.Lerp(.026f, .010f, road) - rut * road * .014f;
            }
            var normals = new Color32[pixels.Length];
            float texel = 50f / (size - 1);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                float dx = (height[y * size + Mathf.Min(size - 1, x + 1)] - height[y * size + Mathf.Max(0, x - 1)]) / (texel * 2);
                float dy = (height[Mathf.Min(size - 1, y + 1) * size + x] - height[Mathf.Max(0, y - 1) * size + x]) / (texel * 2);
                Vector3 normal = new Vector3(-dx, -dy, 1).normalized;
                normals[index] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
            }
            var colorMap = palette.OwnTexture(Texture("Painted grass and worn supply roads", size, false, pixels));
            var normalMap = palette.OwnTexture(Texture("Fine flock and road rut normals", size, true, normals));
            var maskMap = palette.OwnTexture(Texture("Ground roughness and crevice mask", size, true, mask));
            Material material = palette.Ground;
            // White is an intentional texture multiplier: all RGB color is authored above
            // from TablePalette rather than tinting the board a second time.
            material.SetColor("_BaseColor", new Color(1, 1, 1, 1));
            material.SetTexture("_BaseMap", colorMap);
            material.SetTexture("_BumpMap", normalMap);
            material.SetTexture("_MetallicGlossMap", maskMap);
            material.SetTexture("_OcclusionMap", maskMap);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureScale("_BumpMap", Vector2.one);
            material.SetTextureScale("_MetallicGlossMap", Vector2.one);
            material.SetTextureScale("_OcclusionMap", Vector2.one);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_BumpScale", .50f);
            material.SetFloat("_OcclusionStrength", .45f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
        }

        static Texture2D Texture(string name, int size, bool linear, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            { name = name, filterMode = FilterMode.Trilinear, anisoLevel = 16, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }

        float GroundHeight(float x, float z)
        {
            if (style == 4 && Mathf.Abs(x) < 3.5f)
                return -.075f;
            float broad = Mathf.PerlinNoise(x * .35f + 41, z * .35f + 71);
            float detail = Mathf.PerlinNoise(x * 1.2f + 13, z * 1.2f + 19);
            float landmark = 1;
            Vector2 p = new Vector2(x, z);
            for (int i = 0; i < Landmarks.Length; i++)
                landmark = Mathf.Min(landmark, SmoothEdge(2.7f, 4.7f, Vector2.Distance(p, Landmarks[i])));
            float edge = Mathf.Clamp01((25 - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))) * 3);
            return .002f + (broad * .025f + detail * .010f) * landmark * edge;
        }

        Mesh GroundMesh()
        {
            const int cells = 160;
            int row = cells + 1;
            var vertices = new Vector3[row * row];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[cells * cells * 6];
            for (int z = 0; z <= cells; z++) for (int x = 0; x <= cells; x++)
            {
                int index = z * row + x;
                float wx = x * (50f / cells) - 25, wz = z * (50f / cells) - 25;
                vertices[index] = new Vector3(wx, GroundHeight(wx, wz), wz);
                uv[index] = new Vector2(x / (float)cells, z / (float)cells);
            }
            int t = 0;
            for (int z = 0; z < cells; z++) for (int x = 0; x < cells; x++)
            {
                int a = z * row + x;
                triangles[t++] = a; triangles[t++] = a + row; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = a + row; triangles[t++] = a + row + 1;
            }
            return Mesh("Sculpted 50m continuous board relief", vertices, uv, triangles);
        }

        void Tree(Transform parent, Vector2 p, float size, int index, Mesh branch, Mesh[] crowns,
            Palette palette, System.Random random)
        {
            Transform root = Group("Handcrafted broadleaf tree", parent);
            root.localPosition = V3(p, GroundHeight(p.x, p.y));
            root.localRotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
            root.localScale = Vector3.one * size;
            Branch(root, branch, new Vector3(0, 0, 0), new Vector3(.09f, 2.55f, -.07f), .20f, palette.Bark);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 2.39996f + index;
                Vector3 end = new Vector3(Mathf.Cos(angle) * .80f, 2.45f + i * .13f, Mathf.Sin(angle) * .80f);
                Branch(root, branch, new Vector3(.04f, 1.55f + i * .10f, 0), end, .095f, palette.Bark);
                var crown = Part("Layered flock leaf crown", root, crowns[(index + i) % crowns.Length],
                    end + Vector3.up * .29f, new Vector3(1.12f, .88f, 1.05f), i % 3 == 0 ? palette.LeavesLight : palette.Leaves);
                crown.localRotation = Quaternion.Euler(Range(random, -9, 9), i * 79, Range(random, -9, 9));
            }
            Part("Sunlit upper leaf crown", root, crowns[index % crowns.Length], new Vector3(.02f, 3.38f, -.03f),
                new Vector3(1.05f, .88f, .98f), palette.LeavesLight);
            // Flared roots visually seat the tiny handcrafted tree into the flock.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f + .3f;
                Branch(root, branch, new Vector3(Mathf.Cos(a) * .43f, .035f, Mathf.Sin(a) * .43f),
                    new Vector3(0, .45f, 0), .11f, palette.Bark);
            }
        }

        static void Branch(Transform parent, Mesh mesh, Vector3 a, Vector3 b, float radius, Material mat)
        {
            Vector3 direction = b - a;
            var part = Part("Tapered bark branch", parent, mesh, a, new Vector3(radius, direction.magnitude, radius), mat);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        }

        static Mesh TaperedBranch()
        {
            const int segments = 10;
            var vertices = new Vector3[(segments + 1) * 3];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 12];
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i <= segments; i++)
            {
                float angle = i * (Mathf.PI * 2 / segments);
                float radius = ring == 0 ? 1 : ring == 1 ? .73f : .37f;
                int index = ring * (segments + 1) + i;
                vertices[index] = new Vector3(Mathf.Cos(angle) * radius, ring * .5f, Mathf.Sin(angle) * radius);
                uv[index] = new Vector2(i / (float)segments, ring * .5f);
            }
            int t = 0;
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i < segments; i++)
            {
                int a = ring * (segments + 1) + i, b = a + segments + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
            return Mesh("Shared tapered bark casting", vertices, uv, triangles);
        }

        static Mesh CrownMesh(int seed)
        {
            const int rings = 8, segments = 14;
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * segments * 6];
            for (int r = 0; r <= rings; r++) for (int s = 0; s <= segments; s++)
            {
                float latitude = r * Mathf.PI / rings;
                float longitude = (s % segments) * Mathf.PI * 2 / segments;
                Vector3 normal = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                float variation = .88f + Hash((int)(normal.x * 117) + seed, (int)(normal.z * 139 + normal.y * 173)) * .18f;
                vertices[r * (segments + 1) + s] = normal * variation;
                uv[r * (segments + 1) + s] = new Vector2(s / (float)segments, r / (float)rings);
            }
            int t = 0;
            for (int r = 0; r < rings; r++) for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s, b = a + segments + 1;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
            return Mesh("Shared irregular hand-flocked crown " + seed, vertices, uv, triangles);
        }

        static Mesh RockMesh(int seed)
        {
            const int rings = 4, segments = 9;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var points = new Vector3[rings + 1, segments];
            for (int r = 0; r <= rings; r++) for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2 / segments;
                float radius = (r == 0 ? .67f : r == 1 ? 1 : r == 2 ? .92f : r == 3 ? .69f : .27f) * (.82f + Hash(seed + s, r * 13) * .28f);
                points[r, s] = new Vector3(Mathf.Cos(angle) * radius, r * .23f + Hash(seed + r, s) * .07f, Mathf.Sin(angle) * radius);
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                Face(vertices, uv, triangles, points[r, s], points[r + 1, s], points[r, next]);
                Face(vertices, uv, triangles, points[r, next], points[r + 1, s], points[r + 1, next]);
            }
            Vector3 peak = new Vector3(.04f, 1.03f, -.05f);
            for (int s = 0; s < segments; s++) Face(vertices, uv, triangles, points[rings, s], peak, points[rings, (s + 1) % segments]);
            return Mesh("Shared chiseled slate outcrop " + seed, vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        static Mesh GrassMesh(int seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 2.39996f + seed;
                float height = .16f + Hash(seed, i) * .19f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 across = new Vector3(-direction.z, 0, direction.x) * (.025f + Hash(i, seed) * .014f);
                Vector3 foot = direction * (.015f + i * .006f);
                Vector3 mid = foot + Vector3.up * height * .60f + direction * height * .17f;
                Vector3 tip = foot + Vector3.up * height + direction * height * .41f;
                Face(vertices, uv, triangles, foot - across, mid - across * .55f, foot + across);
                Face(vertices, uv, triangles, foot + across, mid - across * .55f, mid + across * .55f);
                Face(vertices, uv, triangles, mid - across * .55f, tip, mid + across * .55f);
                // Real geometry on both sides keeps the blades visible without an
                // alpha-cutout shader, transparency, or a new material variant.
                Face(vertices, uv, triangles, foot + across, mid - across * .55f, foot - across);
                Face(vertices, uv, triangles, mid + across * .55f, mid - across * .55f, foot + across);
                Face(vertices, uv, triangles, mid + across * .55f, tip, mid - across * .55f);
            }
            return Mesh("Shared curved nine-blade grass " + seed, vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        static void Face(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uv.Add(new Vector2(a.x, a.z)); uv.Add(new Vector2(b.x, b.z)); uv.Add(new Vector2(c.x, c.z));
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        static Mesh Mesh(string name, Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        void RoadDistance(Vector2 p, out float lateral, out float width)
        {
            float nearest = float.MaxValue; lateral = 99; width = .7f;
            for (int i = 0; i < Roads.Length; i++)
            {
                Road road = Roads[i]; Vector2 axis = road.B - road.A;
                float t = Mathf.Clamp01(Vector2.Dot(p - road.A, axis) / axis.sqrMagnitude);
                Vector2 delta = p - (road.A + axis * t);
                float distance = delta.magnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                float side = axis.x * delta.y - axis.y * delta.x;
                lateral = distance * (side < 0 ? -1 : 1); width = road.Width;
            }
        }

        bool SceneryClear(Vector2 p, float landmarkRadius, float roadRadius)
        {
            if (Mathf.Abs(p.x) > 24 || Mathf.Abs(p.y) > 24) return false;
            if (style == 4 && (Mathf.Abs(p.x) < 4.1f || p.sqrMagnitude < 12.2f * 12.2f)) return false;
            for (int i = 0; i < Landmarks.Length; i++) if (Vector2.Distance(p, Landmarks[i]) < landmarkRadius) return false;
            RoadDistance(p, out float lateral, out float width);
            return Mathf.Abs(lateral) > width + roadRadius;
        }
        static float Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)x * 374761393u + (uint)y * 668265263u + 1442695041u;
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0x00ffffff) / 16777215f;
            }
        }
        static float Range(System.Random random, float a, float b) => a + (b - a) * (float)random.NextDouble();
        static float SmoothEdge(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
        static Vector3 V3(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        static Transform Group(string name, Transform parent)
        { var root = new GameObject(name); root.transform.SetParent(parent, false); return root.transform; }
        static Transform Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var root = Group(name, parent); root.localPosition = position; root.localScale = scale;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return root;
        }
    }
}
