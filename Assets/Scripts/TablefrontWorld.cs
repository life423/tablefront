using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tablefront
{
    /// <summary>Procedural miniature art, shared materials, and a fixed-size combat-effect pool.</summary>
    public sealed class TablefrontWorld : MonoBehaviour
    {
        sealed class Miniature
        {
            public GameObject Root, Selection, Flash, Cover;
            public Transform Hull, HealthRoot, HealthFill, Barrel;
            public float Recoil;
            public UnitKind Kind;
            public Vector3 Previous;
        }
        sealed class Landmark
        {
            public GameObject Root;
            public Renderer Flag, Ring;
            public Transform Progress, Health;
            public TextMeshPro Label;
        }
        sealed class Effect
        {
            public LineRenderer Trace;
            public Transform Impact, Chip;
            public float Life, MaxLife;
            public bool Heavy;
        }
        sealed class OrderGuide
        {
            public LineRenderer Line;
            public Transform Marker;
            public Vector2 Destination, Origin;
            public int Count;
        }
        sealed class Wreck
        {
            public Transform Root;
            public float Life;
        }

        Camera view;
        Transform scenery, pieces, landmarks, effectsRoot;
        readonly Dictionary<int, Miniature> miniatures = new Dictionary<int, Miniature>();
        readonly Dictionary<int, Landmark> pointViews = new Dictionary<int, Landmark>();
        readonly Dictionary<int, Landmark> headquarters = new Dictionary<int, Landmark>();
        readonly List<int> removed = new List<int>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        readonly List<TextMeshPro> worldLabels = new List<TextMeshPro>();
        readonly HashSet<int> present = new HashSet<int>();
        readonly Material[] faction = new Material[3];
        readonly Material[] factionBright = new Material[3];
        readonly Effect[] effects = new Effect[128];
        readonly OrderGuide[] orderGuides = new OrderGuide[8];
        readonly Wreck[] wrecks = new Wreck[16];
        readonly AudioSource[] voices = new AudioSource[8];
        int effectCursor, wreckCursor;
        Material ink, ceramic, walnut, edge, brass, terrain, sand, road, stone, foliage, foliageLight, neutral, muzzle;
        Mesh ringMesh, coneMesh, rockMesh;
        Transform orderMarker;
        float orderLife;
        Transform tutorialTargetRoot, tutorialTargetRing, tutorialTargetBacking, tutorialTargetPlate;
        TextMeshPro tutorialTargetLabel;
        LineRenderer tutorialTargetPointer;
        Material tutorialGold;
        Vector2 tutorialPosition;
        float tutorialRadius = 1;
        bool tutorialTargetVisible;
        string tutorialLabelText = "";
        AudioSource cueAudio, ambience;
        AudioClip scoutShot, lightShot, heavyShot, deathSound, recruitCue, captureCue, lostCue;
        float lightCooldown, heavyCooldown;
        Transform coverRoot;
        BattleSimulation coverSimulation;
        bool built;
        public bool ReducedMotion { get; set; }

        public void Build(Camera camera)
        {
            if (built) return;
            built = true;
            view = camera;
            scenery = Group("Campaign table", transform);
            pieces = Group("Miniatures", transform);
            landmarks = Group("Objectives and HQs", transform);
            effectsRoot = Group("Pooled battlefield effects", transform);
            Materials();
            ringMesh = OwnMesh(MakeRing(1f, .89f, 64));
            coneMesh = OwnMesh(MakeCone(9));
            rockMesh = OwnMesh(MakeRock());
            Table();
            Lighting();
            EffectPool();
            AudioSetup();
            CombineScenery();
        }

        void Materials()
        {
            ink = Mat("Rubber and cast iron", TablePalette.Ink, .22f);
            ceramic = Mat("Painted ivory details", TablePalette.Text, .38f);
            walnut = Mat("Walnut table", TablePalette.Walnut, .32f);
            edge = Mat("Polished walnut edges", TablePalette.WoodEdge, .45f);
            brass = Mat("Aged brass inlay", TablePalette.Gold, .5f, .65f);
            terrain = Mat("Sage flock terrain", TablePalette.Terrain, .1f);
            sand = Mat("Dry meadow", TablePalette.Sand, .1f);
            road = Mat("Sandstone routes", TablePalette.Road, .12f);
            stone = Mat("Slate scenery", TablePalette.Stone, .16f);
            foliage = Mat("Deep pine", TablePalette.Foliage, .12f);
            foliageLight = Mat("Sunlit pine", TablePalette.FoliageLight, .12f);
            neutral = Mat("Neutral brass", TablePalette.Gold, .24f);
            muzzle = Mat("Warm shot flash", TablePalette.Muzzle, 0, 0, true);
            for (var i = 0; i < 3; i++)
            {
                faction[i] = Mat("Faction enamel " + i, TablePalette.Team(i), .38f, .05f);
                factionBright[i] = Mat("Faction signal " + i, TablePalette.Team(i), 0, 0, true);
            }
            var flock = Texture("Flocked linen", 512, false);
            terrain.SetTexture("_BaseMap", flock);
            terrain.SetTextureScale("_BaseMap", new Vector2(14, 14));
            sand.SetTexture("_BaseMap", flock);
            sand.SetTextureScale("_BaseMap", new Vector2(4, 4));
            walnut.SetTexture("_BaseMap", Texture("Fine walnut grain", 1024, true));
            walnut.SetTextureScale("_BaseMap", new Vector2(4, 1));
        }

        Material Mat(string name, Color color, float smooth = .15f, float metallic = 0, bool unlit = false)
        {
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Tablefront requires URP shaders: " + name);
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            if (!unlit) { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); }
            ownedMaterials.Add(m);
            return m;
        }

        Texture2D Texture(string name, int size, bool grain)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Trilinear, anisoLevel = 16, wrapMode = TextureWrapMode.Repeat };
            var data = new Color[size * size];
            for (var z = 0; z < size; z++) for (var x = 0; x < size; x++)
            {
                // Preserve the authored scale while adding real texture samples, rather than more noise.
                float sampleScale = 128f / size;
                float n = Mathf.PerlinNoise(x * sampleScale * (grain ? .035f : .39f), z * sampleScale * (grain ? .48f : .39f));
                float v = .88f + n * .17f;
                data[z * size + x] = new Color(v, v, v, 1);
            }
            t.SetPixels(data); t.Apply(); ownedTextures.Add(t); return t;
        }

        void Table()
        {
            Box("Studio floor", scenery, new Vector3(0, -3.2f, 0), new Vector3(180, .3f, 180), ink);
            Box("Walnut plinth", scenery, new Vector3(0, -1.45f, 0), new Vector3(54, 2.1f, 54), walnut);
            Box("Raised bevel", scenery, new Vector3(0, -.35f, 0), new Vector3(53.5f, .25f, 53.5f), edge);
            Box("Brass inner lip", scenery, new Vector3(0, -.18f, 0), new Vector3(51.4f, .13f, 51.4f), brass);
            Box("Recessed terrain", scenery, new Vector3(0, -.09f, 0), new Vector3(50, .18f, 50), terrain);
            // Low-rise terrain reads as handcrafted flock, never blocks the simulation.
            var random = new System.Random(7102);
            for (var i = 0; i < 26; i++)
            {
                float x = (float)random.NextDouble() * 44 - 22;
                float z = (float)random.NextDouble() * 44 - 22;
                if (NearLandmark(x, z, 5)) continue;
                var patch = Part("Meadow patch", scenery, PrimitiveType.Cylinder, new Vector3(x, .006f, z), new Vector3(3.4f + (float)random.NextDouble() * 2.5f, .006f, 2.5f + (float)random.NextDouble() * 2), sand);
                patch.localEulerAngles = new Vector3(0, (float)random.NextDouble() * 180, 0);
            }
            var west = new Vector3(-10, .026f, -6);
            var east = new Vector3(10, .026f, -6);
            var north = new Vector3(0, .026f, 12);
            Route(west, east, 1.35f);
            Route(west, north, 1.35f); Route(east, north, 1.35f);
            Route(new Vector3(0, .026f, -21), new Vector3(0, .026f, -6), 1.15f);
            Route(new Vector3(18.2f, .026f, 10.5f), east, 1.15f);
            Route(new Vector3(-18.2f, .026f, 10.5f), west, 1.15f);
            // Dotted route markings are sparse, legible at gameplay distance.
            for (var i = -4; i <= 4; i++) Box("Road inset", scenery, new Vector3(i * 2, .047f, -6), new Vector3(.35f, .012f, .08f), brass);
            for (var i = 0; i < 40; i++)
            {
                float x = (float)random.NextDouble() * 45 - 22.5f;
                float z = (float)random.NextDouble() * 45 - 22.5f;
                if (NearLandmark(x, z, 4.8f) || NearRoad(x, z, 1.7f)) continue;
                if (i % 4 == 0) Rock(new Vector3(x, .02f, z), .6f + (float)random.NextDouble() * .7f);
                else Tree(new Vector3(x, .01f, z), .65f + (float)random.NextDouble() * .45f);
            }
            // Fine ruler ticks and brass hardware emphasize that this is a physical table.
            for (var i = -24; i <= 24; i += 2)
            {
                float length = i % 10 == 0 ? .5f : .23f;
                Box("North ruler", scenery, new Vector3(i, -.018f, 25.8f), new Vector3(.035f, .04f, length), brass);
                Box("South ruler", scenery, new Vector3(i, -.018f, -25.8f), new Vector3(.035f, .04f, length), brass);
                Box("East ruler", scenery, new Vector3(25.8f, -.018f, i), new Vector3(length, .04f, .035f), brass);
                Box("West ruler", scenery, new Vector3(-25.8f, -.018f, i), new Vector3(length, .04f, .035f), brass);
            }
            foreach (var x in new[] { -25.8f, 25.8f }) foreach (var z in new[] { -25.8f, 25.8f })
            {
                Part("Corner brass screw", scenery, PrimitiveType.Cylinder, new Vector3(x, -.01f, z), new Vector3(.24f, .028f, .24f), brass);
                Box("Screw slot", scenery, new Vector3(x, .02f, z), new Vector3(.13f, .01f, .035f), ink);
            }
            // A small set of ruin pieces belongs at the edges of the battlefield.
            Ruin(new Vector3(-19, .02f, -12), 20); Ruin(new Vector3(20, .02f, -13), -25);
            Ruin(new Vector3(7, .02f, 21), -10);
            // Edge identification stays out of the tactical space.
            var plaque = Box("Campaign name plaque", scenery, new Vector3(0, -.17f, -26.1f), new Vector3(8, .1f, .85f), ink);
            for (var i = 0; i < 3; i++) Box("Faction enamel inlay", scenery, new Vector3(-1.1f + i * 1.1f, -.09f, -26.1f), new Vector3(.65f, .035f, .25f), faction[i]);
        }

        static bool NearLandmark(float x, float z, float radius)
        {
            Vector2 p = new Vector2(x, z);
            return Vector2.Distance(p, new Vector2(0, -21)) < radius || Vector2.Distance(p, new Vector2(18.2f, 10.5f)) < radius || Vector2.Distance(p, new Vector2(-18.2f, 10.5f)) < radius || Vector2.Distance(p, new Vector2(-10, -6)) < radius || Vector2.Distance(p, new Vector2(10, -6)) < radius || Vector2.Distance(p, new Vector2(0, 12)) < radius;
        }

        static bool NearRoad(float x, float z, float width)
        {
            var p = new Vector2(x, z);
            return SegmentDistance(p, new Vector2(-10, -6), new Vector2(10, -6)) < width || SegmentDistance(p, new Vector2(-10, -6), new Vector2(0, 12)) < width || SegmentDistance(p, new Vector2(10, -6), new Vector2(0, 12)) < width;
        }
        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b) => Vector2.Distance(p, a + (b - a) * Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude));

        void Route(Vector3 a, Vector3 b, float width)
        {
            var path = Box("Worn supply route", scenery, (a + b) * .5f, new Vector3(width, .022f, Vector3.Distance(a, b)), road);
            path.rotation = Quaternion.LookRotation(b - a);
        }
        void Tree(Vector3 position, float size)
        {
            var t = Group("Handcrafted pine", scenery); t.localPosition = position; t.localScale = Vector3.one * size;
            Part("Soil island", t, PrimitiveType.Cylinder, new Vector3(0, .035f, 0), new Vector3(1.6f, .035f, 1.4f), sand);
            Part("Trunk", t, PrimitiveType.Cylinder, new Vector3(0, .65f, 0), new Vector3(.18f, .65f, .18f), walnut);
            MeshPart("Low pine crown", t, coneMesh, new Vector3(0, .55f, 0), new Vector3(1.55f, 1.7f, 1.55f), foliage);
            MeshPart("Top pine crown", t, coneMesh, new Vector3(0, 1.4f, 0), new Vector3(1.05f, 1.55f, 1.05f), foliageLight);
        }
        void Rock(Vector3 position, float size)
        {
            var r = MeshPart("Faceted slate outcrop", scenery, rockMesh, position, new Vector3(size, size * .7f, size * 1.1f), stone);
            r.localEulerAngles = new Vector3(0, position.x * 15, 0);
        }
        void Ruin(Vector3 p, float angle)
        {
            var root = Group("Weathered ruin", scenery); root.localPosition = p; root.localEulerAngles = new Vector3(0, angle, 0);
            Box("Foundation", root, new Vector3(0, .1f, 0), new Vector3(3.2f, .18f, 2.5f), sand);
            Box("Low wall", root, new Vector3(-.9f, .48f, 0), new Vector3(.4f, .9f, 2.1f), stone);
            Box("Broken wall", root, new Vector3(0, .68f, .85f), new Vector3(2.2f, 1.3f, .4f), stone);
            Box("Capstone", root, new Vector3(-.4f, 1.36f, .85f), new Vector3(1.5f, .14f, .55f), ceramic);
            Rock(p + new Vector3(1, 0, -.8f), .4f);
        }

        void Lighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = TablePalette.SkyBounce * .63f;
            RenderSettings.ambientEquatorColor = TablePalette.Muted * .45f;
            RenderSettings.ambientGroundColor = TablePalette.GroundBounce * .6f;
            RenderSettings.fog = false;
            var key = new GameObject("Warm studio key").AddComponent<Light>(); key.transform.SetParent(transform, false);
            key.type = LightType.Directional; key.color = TablePalette.WarmLight; key.intensity = 1.3f;
            key.transform.rotation = Quaternion.Euler(46, -38, 0); key.shadows = LightShadows.Soft; key.shadowStrength = .78f;
            key.shadowBias = .06f; key.shadowNormalBias = .25f;
            var fill = new GameObject("Cool studio fill").AddComponent<Light>(); fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional; fill.color = TablePalette.SkyBounce; fill.intensity = .24f;
            fill.transform.rotation = Quaternion.Euler(35, 135, 0); fill.shadows = LightShadows.None;
            if (view != null)
            {
                view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = TablePalette.Background;
                view.allowHDR = true; view.allowMSAA = true; view.allowDynamicResolution = false;
                var data = view.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
                data.volumeLayerMask = 1; data.antialiasing = AntialiasingMode.None;
                data.stopNaN = true;
            }
            var urp = UniversalRenderPipeline.asset;
            if (urp != null) TablefrontGraphics.Configure();
            var volume = new GameObject("Tabletop grading").AddComponent<Volume>(); volume.transform.SetParent(transform, false); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Tablefront warm studio profile"; volume.sharedProfile = profile;
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true); bloom.intensity.value = .28f; bloom.threshold.value = 1.1f; bloom.scatter.value = .55f;
            var vignette = profile.Add<Vignette>(true); vignette.intensity.value = .18f; vignette.smoothness.value = .65f;
            var color = profile.Add<ColorAdjustments>(true); color.saturation.value = 7; color.contrast.value = 5; color.postExposure.value = .12f;
        }

        public void Render(BattleSimulation sim, ICollection<int> selected)
        {
            if (!built || sim == null) return;
            if (coverSimulation != sim) BuildCover(sim);
            present.Clear();
            float blend = Application.isPlaying && !ReducedMotion ? 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime) : 1;
            foreach (var u in sim.Units)
            {
                if (!u.Alive) continue;
                present.Add(u.Id);
                if (!miniatures.TryGetValue(u.Id, out var model)) { model = CreateMiniature(u); miniatures.Add(u.Id, model); }
                var goal = V3(u.Position, .055f);
                var before = model.Root.transform.position;
                model.Root.transform.position = Vector3.Lerp(before, goal, blend);
                model.Root.transform.rotation = Quaternion.Slerp(model.Root.transform.rotation, Quaternion.Euler(0, u.Heading, 0), blend);
                float movement = (before - goal).sqrMagnitude;
                if (model.Hull != null)
                {
                    float gait = !ReducedMotion && movement > .0008f ? Mathf.Sin(sim.Elapsed * (u.Kind == UnitKind.Rifle ? 15 : 11) + u.Id) : 0;
                    model.Hull.localPosition = new Vector3(0, gait * (u.Kind == UnitKind.Tank ? .018f : .035f), 0);
                    model.Hull.localRotation = Quaternion.Euler(!ReducedMotion ? -model.Recoil * 8 : 0, 0, gait * (u.Kind == UnitKind.Scout ? 1.2f : .45f));
                }
                if (model.Barrel != null) model.Barrel.localPosition = new Vector3(0, 0, ReducedMotion ? 0 : -.21f * model.Recoil);
                model.Selection.SetActive(selected != null && selected.Contains(u.Id));
                model.Cover.SetActive(sim.IsProtected(u));
                model.Flash.SetActive(u.ShotFlash > .01f);
                model.HealthRoot.gameObject.SetActive(u.Health < u.MaxHealth - .1f || (selected != null && selected.Contains(u.Id)));
                model.HealthRoot.rotation = view.transform.rotation;
                float fraction = Mathf.Clamp01(u.Health / Mathf.Max(1, u.MaxHealth));
                model.HealthFill.localScale = new Vector3(.88f * fraction, .075f, .02f);
                model.HealthFill.localPosition = new Vector3(-.44f * (1 - fraction), 0, -.012f);
            }
            removed.Clear(); foreach (var pair in miniatures) if (!present.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { ShowWreck(miniatures[id]); Destroy(miniatures[id].Root); miniatures.Remove(id); }
            foreach (var point in sim.Points)
            {
                if (!pointViews.TryGetValue(point.Id, out var lm)) { lm = CreatePoint(point); pointViews.Add(point.Id, lm); }
                var mat = point.Owner >= 0 ? faction[point.Owner] : neutral;
                lm.Flag.sharedMaterial = mat; lm.Ring.sharedMaterial = point.Owner >= 0 ? factionBright[point.Owner] : neutral;
                lm.Progress.gameObject.SetActive(point.CapturingTeam >= 0 && point.CaptureProgress > .01f);
                lm.Progress.localScale = Vector3.one * Mathf.Lerp(.55f, 1f, point.CaptureProgress);
                if (point.CapturingTeam >= 0) lm.Progress.GetComponent<Renderer>().sharedMaterial = factionBright[point.CapturingTeam];
            }
            foreach (var army in sim.Armies)
            {
                if (!headquarters.TryGetValue(army.Id, out var lm)) { lm = CreateHeadquarters(army); headquarters.Add(army.Id, lm); }
                lm.Root.SetActive(army.Alive);
                if (lm.Health != null) lm.Health.localScale = new Vector3(Mathf.Clamp(army.BaseHealth / BattleSimulation.BaseMaxHealth, .01f, 1f) * 3.1f, .09f, .08f);
            }
            RenderOrders(sim, selected);
            foreach (var text in worldLabels) if (text != null) text.transform.rotation = view.transform.rotation;
            RenderTutorialTarget();
        }

        /// <summary>One reusable, noninteractive world guide for the current tutorial task.</summary>
        public void SetTutorialTarget(Vector2 position, float radius, string label, bool visible)
        {
            bool finite = !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsInfinity(position.x) && !float.IsInfinity(position.y)
                && !float.IsNaN(radius) && !float.IsInfinity(radius);
            tutorialTargetVisible = visible && finite;
            if (tutorialTargetVisible)
            {
                tutorialPosition = position;
                tutorialRadius = Mathf.Clamp(radius, .8f, 6f);
                tutorialLabelText = string.IsNullOrWhiteSpace(label) ? "HERE" : label.Trim().ToUpperInvariant();
                if (tutorialLabelText.Length > 24) tutorialLabelText = tutorialLabelText.Substring(0, 24);
            }
            if (built) RenderTutorialTarget();
        }

        void BuildTutorialTarget()
        {
            tutorialGold = Mat("Tutorial target gold", TablePalette.Gold, 0, 0, true);
            tutorialTargetRoot = Group("Tutorial target guide", effectsRoot);
            // Thin concentric outlines keep the gold legible without filling or obscuring the target.
            tutorialTargetBacking = MeshPart("Tutorial target dark outline", tutorialTargetRoot, ringMesh, new Vector3(0, .045f, 0), Vector3.one, ink);
            tutorialTargetRing = MeshPart("Tutorial target gold ring", tutorialTargetRoot, ringMesh, new Vector3(0, .055f, 0), Vector3.one, tutorialGold);
            var labelObject = new GameObject("Tutorial target label");
            labelObject.transform.SetParent(tutorialTargetRoot, false);
            tutorialTargetLabel = labelObject.AddComponent<TextMeshPro>();
            tutorialTargetLabel.text = "HERE";
            tutorialTargetLabel.fontSize = 8.4f;
            tutorialTargetLabel.fontStyle = FontStyles.Bold;
            tutorialTargetLabel.characterSpacing = 1.6f;
            tutorialTargetLabel.color = TablePalette.Gold;
            tutorialTargetLabel.richText = false;
            tutorialTargetLabel.alignment = TextAlignmentOptions.Center;
            tutorialTargetLabel.textWrappingMode = TextWrappingModes.NoWrap;
            tutorialTargetLabel.rectTransform.sizeDelta = new Vector2(18, 2.2f);
            tutorialTargetLabel.outlineColor = TablePalette.Ink;
            tutorialTargetLabel.outlineWidth = .16f;
            tutorialTargetLabel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            tutorialTargetLabel.GetComponent<Renderer>().receiveShadows = false;
            tutorialTargetPlate = Box("Tutorial target matte label", labelObject.transform, new Vector3(0, 0, .065f), new Vector3(3, .7f, .035f), ink);
            tutorialTargetPlate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            tutorialTargetPlate.GetComponent<Renderer>().receiveShadows = false;
            var pointerObject = new GameObject("Tutorial target pointer");
            pointerObject.transform.SetParent(tutorialTargetRoot, false);
            tutorialTargetPointer = pointerObject.AddComponent<LineRenderer>();
            tutorialTargetPointer.sharedMaterial = tutorialGold;
            tutorialTargetPointer.useWorldSpace = true;
            tutorialTargetPointer.positionCount = 5;
            tutorialTargetPointer.startWidth = tutorialTargetPointer.endWidth = .085f;
            tutorialTargetPointer.numCapVertices = 2;
            tutorialTargetPointer.numCornerVertices = 2;
            tutorialTargetPointer.shadowCastingMode = ShadowCastingMode.Off;
            tutorialTargetPointer.receiveShadows = false;
            tutorialTargetRoot.gameObject.SetActive(false);
        }

        void RenderTutorialTarget()
        {
            if (tutorialTargetRoot == null) return;
            bool visible = tutorialTargetVisible && view != null;
            tutorialTargetRoot.gameObject.SetActive(visible);
            if (!visible) return;
            tutorialTargetRoot.position = V3(tutorialPosition);
            float pulse = ReducedMotion ? 1 : 1 + .025f * Mathf.Sin(Time.unscaledTime * 3);
            tutorialTargetRing.localScale = Vector3.one * (tutorialRadius * pulse);
            tutorialTargetBacking.localScale = Vector3.one * (tutorialRadius * pulse * 1.1f);
            if (tutorialTargetLabel.text != tutorialLabelText)
            {
                tutorialTargetLabel.text = tutorialLabelText;
                tutorialTargetLabel.ForceMeshUpdate(true);
                var bounds = tutorialTargetLabel.textBounds;
                tutorialTargetPlate.localPosition = new Vector3(bounds.center.x, bounds.center.y, .065f);
                tutorialTargetPlate.localScale = new Vector3(Mathf.Max(1.4f, bounds.size.x + .5f), Mathf.Max(.6f, bounds.size.y + .26f), .035f);
            }
            Vector3 right = view.transform.right;
            // The coach sits on the right. Keep the placard beside the target, away from unit faces.
            float side = view.WorldToViewportPoint(V3(tutorialPosition)).x < .25f ? 1 : -1;
            Vector3 outward = right * side;
            float halfPlate = tutorialTargetPlate.localScale.x * .5f;
            Vector3 labelPosition = V3(tutorialPosition, 1.8f) + outward * (tutorialRadius + halfPlate + .8f);
            tutorialTargetLabel.transform.SetPositionAndRotation(labelPosition, view.transform.rotation);
            Vector3 start = labelPosition - outward * halfPlate;
            Vector3 end = V3(tutorialPosition, .12f) + outward * tutorialRadius;
            Vector3 direction = (end - start).normalized;
            Vector3 wing = view.transform.up * .25f;
            tutorialTargetPointer.SetPosition(0, start);
            tutorialTargetPointer.SetPosition(1, end);
            tutorialTargetPointer.SetPosition(2, end - direction * .5f + wing);
            tutorialTargetPointer.SetPosition(3, end);
            tutorialTargetPointer.SetPosition(4, end - direction * .5f - wing);
        }

        void BuildCover(BattleSimulation sim)
        {
            if (coverRoot != null) Destroy(coverRoot.gameObject);
            coverSimulation = sim;
            coverRoot = Group("Playable rifle cover", landmarks);
            for (int i = 0; i < sim.CoverZones.Count; i++)
            {
                var zone = sim.CoverZones[i];
                var root = Group("Low cover position " + (i + 1), coverRoot);
                root.localPosition = V3(zone.Position, .015f);
                Part("Worn defensive ground", root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(zone.Radius * 2, .006f, zone.Radius * 2), sand);
                MeshPart("Cover boundary", root, ringMesh, new Vector3(0, .018f, 0), Vector3.one * zone.Radius, brass);
                // These low, permeable earthworks show protection; they are not movement obstacles.
                for (int segment = 0; segment < 6; segment++)
                {
                    float angle = segment * 60f + 15f;
                    var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    var bag = Box("Low sandbag section", root, direction * zone.Radius * .83f + Vector3.up * .13f,
                        new Vector3(zone.Radius * .57f, .24f, .23f), stone);
                    bag.localRotation = Quaternion.Euler(0, angle, 0);
                }
                // A pair of short braces distinguishes playable cover from decorative meadow patches.
                Box("Cover marking left", root, new Vector3(-.25f, .027f, 0), new Vector3(.08f, .015f, .7f), ceramic);
                Box("Cover marking right", root, new Vector3(.25f, .027f, 0), new Vector3(.08f, .015f, .7f), ceramic);
            }
        }

        void RenderOrders(BattleSimulation sim, ICollection<int> selected)
        {
            int count = 0;
            foreach (var guide in orderGuides) guide.Count = 0;
            if (selected != null)
            {
                foreach (var unit in sim.Units)
                {
                    if (!unit.Alive || !unit.HasOrder || !selected.Contains(unit.Id)) continue;
                    Vector2 destination = unit.Destination;
                    if (unit.AttackTargetId >= 0)
                    {
                        foreach (var target in sim.Units) if (target.Id == unit.AttackTargetId && target.Alive) { destination = target.Position; break; }
                    }
                    if ((destination - unit.Position).sqrMagnitude < 1f) continue;
                    int index = -1;
                    for (int j = 0; j < count; j++)
                        if ((orderGuides[j].Destination / orderGuides[j].Count - destination).sqrMagnitude < 12.25f) { index = j; break; }
                    if (index < 0)
                    {
                        if (count == orderGuides.Length) continue;
                        index = count++; orderGuides[index].Destination = orderGuides[index].Origin = Vector2.zero;
                    }
                    var guide = orderGuides[index];
                    guide.Destination += destination; guide.Origin += unit.Position; guide.Count++;
                }
            }
            for (int j = 0; j < orderGuides.Length; j++)
            {
                var guide = orderGuides[j]; bool active = j < count;
                guide.Line.enabled = active; guide.Marker.gameObject.SetActive(active);
                if (!active) continue;
                Vector2 destination = guide.Destination / guide.Count;
                guide.Line.SetPosition(0, V3(guide.Origin / guide.Count, .17f));
                guide.Line.SetPosition(1, V3(destination, .17f));
                guide.Marker.position = V3(destination, .08f);
            }
        }

        Miniature CreateMiniature(Troop u)
        {
            var root = Group(u.Kind + " miniature " + u.Id, pieces);
            root.position = V3(u.Position, .055f); root.rotation = Quaternion.Euler(0, u.Heading, 0);
            var hull = Group("Animated model", root);
            bool tank = u.Kind == UnitKind.Tank;
            float radius = tank ? .92f : .66f;
            Part("Dark beveled gaming base", root, PrimitiveType.Cylinder, new Vector3(0, .07f, 0), new Vector3(radius * 2, .07f, radius * 2), ink);
            Part("Enamel base top", root, PrimitiveType.Cylinder, new Vector3(0, .145f, 0), new Vector3(radius * 1.83f, .016f, radius * 1.83f), faction[u.Team]);
            GameObject flash;
            if (tank) flash = Tank(hull, u.Team);
            else if (u.Kind == UnitKind.Scout) flash = Scout(hull, u.Team);
            else flash = Infantry(hull, u.Team);
            var selected = MeshPart("Selected unit ring", root, ringMesh, new Vector3(0, .022f, 0), Vector3.one * (radius + .2f), factionBright[u.Team]).gameObject;
            selected.SetActive(false);
            var cover = MeshPart("Protected rifle ring", root, ringMesh, new Vector3(0, .028f, 0), Vector3.one * (radius + .06f), brass).gameObject;
            cover.SetActive(false);
            var health = Group("Billboard health", root); health.localPosition = new Vector3(0, tank ? 1.68f : 1.75f, 0);
            Box("Health backing", health, Vector3.zero, new Vector3(.98f, .13f, .035f), ink);
            var fill = Box("Health", health, new Vector3(0, 0, -.012f), new Vector3(.88f, .075f, .02f), factionBright[u.Team]);
            return new Miniature { Root = root.gameObject, Hull = hull, Selection = selected, Cover = cover, Flash = flash, HealthRoot = health, HealthFill = fill, Kind = u.Kind, Barrel = tank ? hull.Find("Recoiling cannon") : null };
        }

        GameObject Tank(Transform root, int team)
        {
            foreach (float x in new[] { -.62f, .62f })
            {
                Box("Continuous track", root, new Vector3(x, .38f, 0), new Vector3(.36f, .42f, 1.7f), ink);
                for (var z = -3; z <= 3; z++) Box("Track tread", root, new Vector3(x, .603f, z * .23f), new Vector3(.38f, .045f, .09f), stone);
            }
            var body = Box("Sloped tank hull", root, new Vector3(0, .53f, -.05f), new Vector3(1.14f, .48f, 1.55f), faction[team]);
            body.localRotation = Quaternion.Euler(-5, 0, 0);
            Box("Ivory recognition stripe", root, new Vector3(-.32f, .785f, -.03f), new Vector3(.14f, .035f, 1.13f), ceramic);
            Part("Low turret", root, PrimitiveType.Cylinder, new Vector3(0, .93f, .02f), new Vector3(.91f, .2f, .91f), faction[team]);
            Box("Turret cap", root, new Vector3(0, 1.13f, -.12f), new Vector3(.68f, .12f, .55f), faction[team]);
            var cannon = Group("Recoiling cannon", root);
            Box("Long cannon", cannon, new Vector3(0, 1.01f, .91f), new Vector3(.15f, .15f, 1.28f), ink);
            Box("Muzzle collar", cannon, new Vector3(0, 1.01f, 1.52f), new Vector3(.24f, .24f, .22f), stone);
            Box("Optics", root, new Vector3(.27f, 1.12f, .27f), new Vector3(.2f, .1f, .09f), factionBright[team]);
            Box("Antenna", root, new Vector3(-.3f, 1.42f, -.35f), new Vector3(.035f, .65f, .035f), ink);
            var flash = Part("Cannon flash", cannon, PrimitiveType.Sphere, new Vector3(0, 1.01f, 1.72f), new Vector3(.36f, .36f, .7f), muzzle); flash.gameObject.SetActive(false); return flash.gameObject;
        }

        GameObject Scout(Transform root, int team)
        {
            foreach (float x in new[] { -.51f, .51f }) foreach (float z in new[] { -.48f, .47f })
            {
                var wheel = Part("Recon wheel", root, PrimitiveType.Cylinder, new Vector3(x, .36f, z), new Vector3(.43f, .14f, .43f), ink);
                wheel.localEulerAngles = new Vector3(0, 0, 90);
            }
            Box("Recon chassis", root, new Vector3(0, .49f, 0), new Vector3(.9f, .3f, 1.24f), faction[team]);
            var hood = Box("Angled nose", root, new Vector3(0, .65f, .43f), new Vector3(.82f, .16f, .48f), faction[team]); hood.localEulerAngles = new Vector3(9, 0, 0);
            Box("Crew visor", root, new Vector3(0, .83f, -.1f), new Vector3(.74f, .34f, .48f), ink);
            Box("Roof panel", root, new Vector3(0, 1.02f, -.16f), new Vector3(.82f, .08f, .61f), ceramic);
            Box("Light turret", root, new Vector3(.12f, 1.11f, -.03f), new Vector3(.28f, .14f, .29f), faction[team]);
            Box("Light barrel", root, new Vector3(.12f, 1.1f, .36f), new Vector3(.075f, .075f, .65f), ink);
            foreach (float x in new[] { -.28f, .28f }) Box("Recon headlamp", root, new Vector3(x, .62f, .645f), new Vector3(.13f, .1f, .04f), ceramic);
            var flash = Part("Recon muzzle flash", root, PrimitiveType.Sphere, new Vector3(.12f, 1.1f, .73f), Vector3.one * .23f, muzzle); flash.gameObject.SetActive(false); return flash.gameObject;
        }

        GameObject Infantry(Transform root, int team)
        {
            GameObject flash = null;
            for (var i = 0; i < 2; i++)
            {
                var soldier = Group("Rifle trooper", root); soldier.localPosition = new Vector3(i == 0 ? -.27f : .29f, .16f, i == 0 ? .16f : -.22f);
                foreach (float x in new[] { -.115f, .115f })
                {
                    var leg = Box("Boot and leg", soldier, new Vector3(x, .23f, -.015f), new Vector3(.16f, .4f, .19f), ink); leg.localEulerAngles = new Vector3(x > 0 ? -9 : 9, 0, x > 0 ? -4 : 4);
                }
                Box("Uniform torso", soldier, new Vector3(0, .58f, 0), new Vector3(.4f, .42f, .26f), faction[team]);
                Box("Ceramic chest plate", soldier, new Vector3(0, .6f, .155f), new Vector3(.29f, .25f, .08f), ceramic);
                Part("Helmet", soldier, PrimitiveType.Sphere, new Vector3(0, .93f, .015f), new Vector3(.36f, .29f, .35f), faction[team]);
                Box("Dark visor", soldier, new Vector3(0, .91f, .18f), new Vector3(.27f, .075f, .035f), ink);
                var arm = Box("Supporting arm", soldier, new Vector3(-.22f, .61f, .17f), new Vector3(.13f, .31f, .15f), faction[team]); arm.localEulerAngles = new Vector3(-52, 0, -10);
                Box("Rifle stock", soldier, new Vector3(.2f, .64f, .24f), new Vector3(.13f, .14f, .49f), ink);
                Box("Rifle barrel", soldier, new Vector3(.2f, .67f, .58f), new Vector3(.065f, .065f, .34f), ink);
                Box("Backpack", soldier, new Vector3(0, .58f, -.23f), new Vector3(.3f, .32f, .17f), stone);
                if (i == 0) { var f = Part("Rifle muzzle flash", soldier, PrimitiveType.Sphere, new Vector3(.2f, .67f, .82f), Vector3.one * .22f, muzzle); flash = f.gameObject; flash.SetActive(false); }
            }
            return flash;
        }

        Landmark CreatePoint(ControlPoint p)
        {
            var root = Group("Control point " + p.Name, landmarks); root.position = V3(p.Position, .025f);
            var ring = MeshPart("Control radius", root, ringMesh, Vector3.zero, Vector3.one * BattleSimulation.CaptureRadius, neutral).GetComponent<Renderer>();
            Part("Objective plinth", root, PrimitiveType.Cylinder, new Vector3(0, .09f, 0), new Vector3(2.2f, .09f, 2.2f), ink);
            Part("Stone inset", root, PrimitiveType.Cylinder, new Vector3(0, .195f, 0), new Vector3(1.9f, .035f, 1.9f), stone);
            Part("Flagpole socket", root, PrimitiveType.Cylinder, new Vector3(0, .31f, 0), new Vector3(.42f, .11f, .42f), brass);
            Part("Flagpole", root, PrimitiveType.Cylinder, new Vector3(0, 1.55f, 0), new Vector3(.075f, 1.35f, .075f), brass);
            Part("Finial", root, PrimitiveType.Sphere, new Vector3(0, 2.93f, 0), Vector3.one * .2f, brass);
            var flag = Box("Enamel pennant", root, new Vector3(.6f, 2.45f, 0), new Vector3(1.12f, .66f, .045f), neutral).GetComponent<Renderer>();
            Box("Flag seam", root, new Vector3(.13f, 2.45f, .029f), new Vector3(.04f, .6f, .025f), ceramic);
            var progress = MeshPart("Capture progress ring", root, ringMesh, new Vector3(0, .035f, 0), Vector3.one * 1.1f, neutral); progress.gameObject.SetActive(false);
            var label = Label(p.Name.ToUpperInvariant(), root, new Vector3(0, 3.7f, 0), 7.2f, TablePalette.Text);
            return new Landmark { Root = root.gameObject, Ring = ring, Flag = flag, Progress = progress, Label = label };
        }

        Landmark CreateHeadquarters(Army a)
        {
            var root = Group(a.Name + " command post", landmarks); root.position = V3(a.BasePosition, .02f);
            Part("HQ base ring", root, PrimitiveType.Cylinder, new Vector3(0, .09f, 0), new Vector3(5.7f, .09f, 5.7f), ink);
            MeshPart("Faction headquarters rim", root, ringMesh, new Vector3(0, .198f, 0), Vector3.one * 2.73f, faction[a.Id]);
            Box("Command bunker", root, new Vector3(0, .7f, 0), new Vector3(3.25f, 1.05f, 2.4f), stone);
            Box("Enamel roof", root, new Vector3(0, 1.28f, 0), new Vector3(3.5f, .2f, 2.7f), faction[a.Id]);
            Box("Roof bevel inset", root, new Vector3(0, 1.42f, -.03f), new Vector3(2.9f, .1f, 2.15f), ink);
            Box("HQ insignia stripe", root, new Vector3(-.8f, 1.49f, 0), new Vector3(.35f, .035f, 1.7f), ceramic);
            foreach (float x in new[] { -1f, 1f }) Box("Reinforced support", root, new Vector3(x, .73f, -1.22f), new Vector3(.3f, 1.15f, .22f), ink);
            Box("Command window", root, new Vector3(0, .94f, -1.22f), new Vector3(1.35f, .26f, .08f), factionBright[a.Id]);
            Box("Bunker entry", root, new Vector3(0, .45f, 1.24f), new Vector3(.8f, .64f, .08f), ink);
            Box("Entry frame", root, new Vector3(0, .8f, 1.27f), new Vector3(1.02f, .12f, .14f), ceramic);
            var mast = Part("Radio mast", root, PrimitiveType.Cylinder, new Vector3(1.03f, 2.2f, .53f), new Vector3(.075f, .78f, .075f), brass);
            var dish = Part("Radio dish", root, PrimitiveType.Sphere, new Vector3(1.03f, 3, .53f), new Vector3(.75f, .14f, .75f), ceramic); dish.localEulerAngles = new Vector3(25, 0, 12);
            Box("HQ health backing", root, new Vector3(0, .21f, -2.14f), new Vector3(3.35f, .1f, .19f), ink);
            var health = Box("HQ health", root, new Vector3(0, .275f, -2.14f), new Vector3(3.1f, .09f, .08f), factionBright[a.Id]);
            var label = Label(a.Name.ToUpperInvariant(), root, new Vector3(0, 3.85f, 0), 6.6f, TablePalette.Team(a.Id));
            return new Landmark { Root = root.gameObject, Health = health, Label = label };
        }

        TextMeshPro Label(string text, Transform parent, Vector3 position, float fontSize, Color color)
        {
            var go = new GameObject(text + " label"); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var tmp = go.AddComponent<TextMeshPro>(); tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 3; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center; tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(13, 2); tmp.outlineWidth = .24f; tmp.outlineColor = TablePalette.Ink;
            tmp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // A slim physical nameplate keeps the small strategic names legible over any flock color.
            // Size it to the actual glyphs rather than the wide TMP layout rectangle.
            tmp.ForceMeshUpdate();
            var bounds = tmp.textBounds;
            var backing = Box("Matte miniature nameplate", go.transform,
                new Vector3(bounds.center.x, bounds.center.y, .055f),
                new Vector3(Mathf.Max(.7f, bounds.size.x + .28f), Mathf.Max(.35f, bounds.size.y + .18f), .025f), ink);
            backing.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            worldLabels.Add(tmp); return tmp;
        }

        void EffectPool()
        {
            for (var i = 0; i < effects.Length; i++)
            {
                var go = new GameObject("Pooled trace " + i); go.transform.SetParent(effectsRoot, false);
                var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = muzzle; line.positionCount = 2; line.useWorldSpace = true;
                line.numCapVertices = 2; line.startWidth = .07f; line.endWidth = .035f;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
                var impact = MeshPart("Pooled impact " + i, effectsRoot, ringMesh, Vector3.zero, Vector3.one, muzzle); impact.gameObject.SetActive(false);
                var chip = MeshPart("Pooled impact chip " + i, effectsRoot, rockMesh, Vector3.zero, Vector3.one, stone); chip.gameObject.SetActive(false);
                effects[i] = new Effect { Trace = line, Impact = impact, Chip = chip };
            }
            orderMarker = MeshPart("Move command marker", effectsRoot, ringMesh, Vector3.zero, Vector3.one, factionBright[0]); orderMarker.gameObject.SetActive(false);
            BuildTutorialTarget();
            for (int i = 0; i < orderGuides.Length; i++)
            {
                var go = new GameObject("Selected force order " + (i + 1)); go.transform.SetParent(effectsRoot, false);
                var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = factionBright[0];
                line.positionCount = 2; line.startWidth = .045f; line.endWidth = .045f; line.numCapVertices = 2;
                line.useWorldSpace = true; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
                var marker = MeshPart("Persistent destination " + (i + 1), effectsRoot, ringMesh, Vector3.zero, Vector3.one * .52f, factionBright[0]);
                marker.gameObject.SetActive(false); orderGuides[i] = new OrderGuide { Line = line, Marker = marker };
            }
            for (int i = 0; i < wrecks.Length; i++)
            {
                var root = Group("Pooled fallen miniature " + i, effectsRoot);
                Part("Scuffed base", root, PrimitiveType.Cylinder, new Vector3(0, .04f, 0), new Vector3(1.3f, .035f, 1.3f), ink);
                Box("Fallen casting", root, new Vector3(.05f, .17f, 0), new Vector3(.75f, .2f, .95f), stone);
                var fragment = Box("Bent recognition stripe", root, new Vector3(-.2f, .28f, .05f), new Vector3(.1f, .025f, .52f), ceramic);
                fragment.localRotation = Quaternion.Euler(0, 23, 0); root.gameObject.SetActive(false);
                wrecks[i] = new Wreck { Root = root };
            }
        }

        public void PlayEvents(IReadOnlyList<CombatEvent> events)
        {
            if (!built || events == null) return;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i]; var effect = effects[effectCursor++ % effects.Length];
                bool heavy = e.Heavy || e.WeaponKind == UnitKind.Tank;
                effect.MaxLife = heavy ? .48f : .22f; effect.Life = effect.MaxLife; effect.Heavy = heavy || e.Killed;
                effect.Trace.enabled = true; effect.Trace.sharedMaterial = e.Heavy ? muzzle : factionBright[Mathf.Clamp(e.Team, 0, 2)];
                effect.Trace.startWidth = heavy ? .13f : e.WeaponKind == UnitKind.Scout ? .045f : .065f; effect.Trace.endWidth = heavy ? .08f : .025f;
                Vector3 origin = V3(e.From, heavy ? 1 : .85f);
                if (miniatures.TryGetValue(e.ShooterId, out var model))
                {
                    model.Recoil = heavy ? 1f : .18f;
                    if (model.Flash != null) origin = model.Flash.transform.position;
                }
                effect.Trace.SetPosition(0, origin); effect.Trace.SetPosition(1, V3(e.To, .75f));
                effect.Impact.position = V3(e.To, .08f); effect.Impact.localScale = Vector3.one * .15f;
                effect.Impact.GetComponent<Renderer>().sharedMaterial = e.Killed || e.Heavy ? muzzle : factionBright[Mathf.Clamp(e.Team, 0, 2)];
                effect.Impact.gameObject.SetActive(true);
                effect.Chip.position = V3(e.To, .13f); effect.Chip.localScale = new Vector3(.19f, .14f, .19f);
                effect.Chip.gameObject.SetActive(heavy || e.Killed);
                if ((heavy ? heavyCooldown : lightCooldown) <= 0)
                {
                    PlayWorldSound(e.Killed ? deathSound : heavy ? heavyShot : e.WeaponKind == UnitKind.Scout ? scoutShot : lightShot,
                        e.To, e.Killed ? .28f : heavy ? .30f : .15f, .96f + (i % 5) * .018f);
                    if (heavy) heavyCooldown = .095f; else lightCooldown = .075f;
                }
            }
        }

        void ShowWreck(Miniature model)
        {
            var wreck = wrecks[wreckCursor++ % wrecks.Length];
            wreck.Root.position = model.Root.transform.position;
            wreck.Root.rotation = model.Root.transform.rotation;
            wreck.Root.localScale = Vector3.one * (model.Kind == UnitKind.Tank ? 1.2f : .8f);
            wreck.Life = 1.8f; wreck.Root.gameObject.SetActive(true);
        }

        void PlayWorldSound(AudioClip clip, Vector2 position, float gain, float pitch)
        {
            if (clip == null) return;
            // Eight fixed voices cap concurrent sounds. View-relative stereo stays audible from a high RTS camera.
            foreach (var voice in voices)
            {
                if (voice == null || voice.isPlaying) continue;
                Vector3 viewport = view != null ? view.WorldToViewportPoint(V3(position)) : new Vector3(.5f, .5f, 1);
                voice.panStereo = Mathf.Clamp((viewport.x - .5f) * 1.3f, -.75f, .75f);
                voice.volume = gain * (viewport.z > 0 && viewport.x > -.15f && viewport.x < 1.15f && viewport.y > -.15f && viewport.y < 1.15f ? 1 : .4f);
                voice.pitch = pitch; voice.clip = clip; voice.Play(); return;
            }
        }

        public void PlayRecruitCue() { if (cueAudio != null && recruitCue != null) { cueAudio.clip = recruitCue; cueAudio.Play(); } }
        public void PlayCaptureCue(bool friendly) { if (cueAudio != null) { cueAudio.clip = friendly ? captureCue : lostCue; if (cueAudio.clip != null) cueAudio.Play(); } }

        public void ShowOrder(Vector2 location)
        {
            if (orderMarker == null) return;
            orderMarker.position = V3(location, .065f); orderLife = .7f; orderMarker.localScale = Vector3.one * 1.1f; orderMarker.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            lightCooldown -= dt; heavyCooldown -= dt;
            foreach (var model in miniatures.Values) model.Recoil = Mathf.MoveTowards(model.Recoil, 0, dt * 5.5f);
            for (var i = 0; i < effects.Length; i++)
            {
                var e = effects[i]; if (e == null || e.Life <= 0) continue;
                e.Life -= dt; float p = Mathf.Clamp01(e.Life / e.MaxLife);
                e.Trace.enabled = e.Life > e.MaxLife * .7f;
                float radius = ReducedMotion ? (e.Heavy ? .55f : .22f) * p : Mathf.Sin(p * Mathf.PI) * (e.Heavy ? 1.05f : .4f);
                e.Impact.localScale = Vector3.one * Mathf.Max(.001f, radius);
                e.Chip.localScale = Vector3.one * (e.Heavy ? .32f : .15f) * p;
                if (!ReducedMotion) e.Chip.position += Vector3.up * dt * .45f * p;
                if (e.Life <= 0) { e.Trace.enabled = false; e.Impact.gameObject.SetActive(false); e.Chip.gameObject.SetActive(false); }
            }
            foreach (var wreck in wrecks)
            {
                if (wreck == null || wreck.Life <= 0) continue;
                wreck.Life -= dt;
                if (wreck.Life < .45f) wreck.Root.localScale *= Mathf.Max(0, 1 - dt * 9);
                if (wreck.Life <= 0) wreck.Root.gameObject.SetActive(false);
            }
            if (orderLife > 0)
            {
                orderLife -= dt; orderMarker.localScale = Vector3.one * (ReducedMotion ? .85f : Mathf.Lerp(.65f, 1.1f, orderLife / .7f));
                if (orderLife <= 0) orderMarker.gameObject.SetActive(false);
            }
        }

        public void ClearBattle()
        {
            foreach (var m in miniatures.Values) if (m.Root != null) Destroy(m.Root);
            foreach (var m in pointViews.Values) if (m.Root != null) Destroy(m.Root);
            foreach (var m in headquarters.Values) if (m.Root != null) Destroy(m.Root);
            miniatures.Clear(); pointViews.Clear(); headquarters.Clear(); worldLabels.Clear();
            foreach (var e in effects) if (e != null) { e.Life = 0; e.Trace.enabled = false; e.Impact.gameObject.SetActive(false); e.Chip.gameObject.SetActive(false); }
            foreach (var guide in orderGuides) if (guide != null) { guide.Line.enabled = false; guide.Marker.gameObject.SetActive(false); guide.Count = 0; }
            foreach (var wreck in wrecks) if (wreck != null) { wreck.Life = 0; wreck.Root.gameObject.SetActive(false); }
            foreach (var voice in voices) if (voice != null) voice.Stop();
            if (cueAudio != null) cueAudio.Stop();
            if (coverRoot != null) Destroy(coverRoot.gameObject);
            coverRoot = null; coverSimulation = null;
            lightCooldown = heavyCooldown = 0;
            orderLife = 0; if (orderMarker != null) orderMarker.gameObject.SetActive(false);
            tutorialTargetVisible = false;
            if (tutorialTargetRoot != null) tutorialTargetRoot.gameObject.SetActive(false);
        }

        void AudioSetup()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var source = new GameObject("Battlefield audio voice " + (i + 1)).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false); source.playOnAwake = false; source.spatialBlend = 0;
                source.priority = 150; source.dopplerLevel = 0; voices[i] = source;
            }
            cueAudio = gameObject.AddComponent<AudioSource>(); cueAudio.playOnAwake = false; cueAudio.spatialBlend = 0; cueAudio.volume = .25f;
            scoutShot = Synth("Recon chatter", .065f, 210, .58f);
            lightShot = Synth("Dry rifle burst", .11f, 145, .75f);
            heavyShot = Synth("Miniature cannon", .38f, 58, .52f);
            deathSound = Synth("Metallic impact", .32f, 82, .65f);
            recruitCue = Chime("Reinforcements ready", new[] { 440f, 660f }, .075f);
            captureCue = Chime("Beacon secured", new[] { 523f, 659f, 784f }, .1f);
            lostCue = Chime("Beacon lost", new[] { 440f, 330f }, .12f);
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.spatialBlend = 0; ambience.volume = .035f;
            const int rate = 22050; var samples = new float[rate * 6]; var random = new System.Random(52); float brown = 0;
            for (var i = 0; i < samples.Length; i++) { brown = Mathf.Clamp((brown + ((float)random.NextDouble() * 2 - 1) * .025f) * .995f, -.2f, .2f); float fade = Mathf.Min(1, Mathf.Min(i / 110f, (samples.Length - 1 - i) / 110f)); samples[i] = (brown + Mathf.Sin(i * .0006f) * .018f) * fade; }
            var clip = AudioClip.Create("Quiet war room ambience", samples.Length, 1, rate, false); clip.SetData(samples, 0); ambience.clip = clip; ambience.Play();
        }
        static AudioClip Synth(string name, float length, float frequency, float noise)
        {
            const int rate = 22050; int count = Mathf.CeilToInt(length * rate); var data = new float[count]; var random = new System.Random(name.GetHashCode());
            for (var i = 0; i < count; i++)
            {
                float t = i / (float)rate, p = i / (float)count;
                float signal = Mathf.Sin(t * frequency * (1 - p * .45f) * Mathf.PI * 2) * (1 - noise) + ((float)random.NextDouble() * 2 - 1) * noise;
                float fade = Mathf.Min(1, Mathf.Min(i / 110f, (count - 1 - i) / 110f));
                data[i] = signal * Mathf.Exp(-p * 7) * fade * .7f;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        static AudioClip Chime(string name, float[] notes, float duration)
        {
            const int rate = 22050;
            int noteLength = Mathf.CeilToInt(duration * rate), count = noteLength * notes.Length;
            var data = new float[count];
            for (int note = 0; note < notes.Length; note++)
                for (int i = 0; i < noteLength; i++)
                {
                    float t = i / (float)rate, progress = i / (float)noteLength;
                    float edgeFade = Mathf.Min(1, Mathf.Min(i / 110f, (noteLength - 1 - i) / 110f));
                    data[note * noteLength + i] = Mathf.Sin(2 * Mathf.PI * notes[note] * t) * Mathf.Exp(-progress * 3.8f) * edgeFade * .55f;
                }
            var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        void CombineScenery()
        {
            // One mesh per shared material replaces hundreds of tiny static draw submissions.
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var filters = scenery.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var r = f.GetComponent<MeshRenderer>(); if (r == null || f.sharedMesh == null) continue;
                if (!groups.TryGetValue(r.sharedMaterial, out var list)) { list = new List<CombineInstance>(); groups.Add(r.sharedMaterial, list); }
                list.Add(new CombineInstance { mesh = f.sharedMesh, transform = scenery.worldToLocalMatrix * f.transform.localToWorldMatrix });
                r.enabled = false;
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Combined table " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); ownedMeshes.Add(mesh);
                MeshPart(mesh.name, scenery, mesh, Vector3.zero, Vector3.one, pair.Key);
            }
        }

        static Transform Group(string name, Transform parent) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform; }
        static Vector3 V3(Vector2 p, float y = 0) => new Vector3(p.x, y, p.y);
        Transform Box(string name, Transform parent, Vector3 p, Vector3 scale, Material mat) => Part(name, parent, PrimitiveType.Cube, p, scale, mat);
        Transform Part(string name, Transform parent, PrimitiveType type, Vector3 p, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = scale;
            var collider = g.GetComponent<Collider>(); if (collider != null) { collider.enabled = false; if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
            g.GetComponent<Renderer>().sharedMaterial = mat; return g.transform;
        }
        Transform MeshPart(string name, Transform parent, Mesh mesh, Vector3 p, Vector3 scale, Material mat)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat; return g.transform;
        }
        Mesh OwnMesh(Mesh mesh) { ownedMeshes.Add(mesh); return mesh; }
        static Mesh MakeRing(float outer, float inner, int segments)
        {
            var verts = new Vector3[segments * 2]; var uv = new Vector2[verts.Length]; var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                verts[i * 2] = d * outer; verts[i * 2 + 1] = d * inner;
                uv[i * 2] = new Vector2(i / (float)segments, 1); uv[i * 2 + 1] = new Vector2(i / (float)segments, 0);
                int n = (i + 1) % segments; int j = i * 6;
                triangles[j] = i * 2; triangles[j + 1] = n * 2; triangles[j + 2] = i * 2 + 1;
                triangles[j + 3] = i * 2 + 1; triangles[j + 4] = n * 2; triangles[j + 5] = n * 2 + 1;
            }
            var mesh = new Mesh { name = "Flat miniature annulus", vertices = verts, uv = uv, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh MakeCone(int sides)
        {
            var verts = new Vector3[sides * 6]; var triangles = new int[verts.Length];
            for (var i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var pa = new Vector3(Mathf.Sin(a) * .5f, 0, Mathf.Cos(a) * .5f); var pb = new Vector3(Mathf.Sin(b) * .5f, 0, Mathf.Cos(b) * .5f);
                int j = i * 6; verts[j] = pa; verts[j + 1] = pb; verts[j + 2] = new Vector3(0, 1, 0);
                verts[j + 3] = pb; verts[j + 4] = pa; verts[j + 5] = Vector3.zero;
                for (var k = 0; k < 6; k++) triangles[j + k] = j + k;
            }
            var mesh = new Mesh { name = "Faceted pine crown", vertices = verts, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh MakeRock()
        {
            var mesh = new Mesh { name = "Hand-cut low polygon slate" };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var ring = new[] { new Vector3(-.7f, 0, -.5f), new Vector3(.45f, 0, -.65f), new Vector3(.75f, 0, .3f), new Vector3(-.35f, 0, .65f) };
            var top = new Vector3(-.12f, .85f, .05f);
            for (var i = 0; i < 4; i++) { int n = vertices.Count; vertices.Add(ring[i]); vertices.Add(top); vertices.Add(ring[(i + 1) % 4]); triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        void OnDestroy()
        {
            foreach (var m in ownedMaterials) if (m != null) Destroy(m);
            foreach (var m in ownedMeshes) if (m != null) Destroy(m);
            foreach (var t in ownedTextures) if (t != null) Destroy(t);
            if (scoutShot != null) Destroy(scoutShot); if (lightShot != null) Destroy(lightShot); if (heavyShot != null) Destroy(heavyShot); if (deathSound != null) Destroy(deathSound);
            if (recruitCue != null) Destroy(recruitCue); if (captureCue != null) Destroy(captureCue); if (lostCue != null) Destroy(lostCue);
            if (ambience != null && ambience.clip != null) Destroy(ambience.clip);
        }
    }
}
