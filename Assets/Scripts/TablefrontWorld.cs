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
            public GameObject Root, Selection, Flash, Cover, Damage;
            public Transform Hull, HealthRoot, HealthFill, Barrel;
            public float Recoil, PreviousHealth, DamageLife;
            public UnitKind Kind;
            public Vector3 Previous;
            public TablefrontMiniatureArt.Rig Art;
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
        readonly Dictionary<BattlefieldId, Transform> battlefieldRoots = new Dictionary<BattlefieldId, Transform>();
        BattlefieldId activeBattlefield = (BattlefieldId)(-1);
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
        Material ink, ceramic, walnut, edge, brass, terrain, sand, stone, foliage, foliageLight, neutral, muzzle, steel, grassDry;
        TablefrontMiniatureArt miniatureArt;
        TablefrontLandmarkArt.Kit landmarkArt;
        ReflectionProbe studioProbe;
        VolumeProfile studioProfile;
        Mesh ringMesh, selectionMesh, rockMesh;
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
        public int HoveredUnitId { get; set; } = -1;

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
            miniatureArt = new TablefrontMiniatureArt(mesh => OwnMesh(mesh));
            landmarkArt = new TablefrontLandmarkArt.Kit(OwnMesh, ink, ceramic, brass, stone, sand);
            ringMesh = OwnMesh(MakeRing(1f, .89f, 64));
            selectionMesh = OwnMesh(MakeRing(1f, .82f, 64));
            rockMesh = OwnMesh(MakeRock());
            Table();
            Lighting();
            EffectPool();
            AudioSetup();
            CombineScenery();
            if (studioProbe != null) studioProbe.RenderProbe();
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
            stone = Mat("Slate scenery", TablePalette.Stone, .16f);
            foliage = Mat("Deep pine", TablePalette.Foliage, .12f);
            foliageLight = Mat("Sunlit pine", TablePalette.FoliageLight, .12f);
            steel = Mat("Weathered steel details", new Color(.32f, .37f, .37f), .45f, .7f);
            grassDry = Mat("Dry grass tips", TablePalette.Sand * .85f, .1f);
            neutral = Mat("Neutral brass", TablePalette.Gold, .24f);
            muzzle = Mat("Warm shot flash", TablePalette.Muzzle, 0, 0, true);
            for (var i = 0; i < 3; i++)
            {
                faction[i] = Mat("Faction enamel " + i, TablePalette.Team(i), .38f, .05f);
                factionBright[i] = Mat("Faction signal " + i, TablePalette.Team(i), 0, 0, true);
            }
            var surfaces = new TablefrontSurfaceArt(texture => ownedTextures.Add(texture));
            surfaces.Apply(ink, TablefrontSurfaceArt.Finish.Rubber);
            surfaces.Apply(ceramic, TablefrontSurfaceArt.Finish.Enamel);
            surfaces.Apply(walnut, TablefrontSurfaceArt.Finish.Walnut);
            surfaces.Apply(edge, TablefrontSurfaceArt.Finish.Walnut);
            surfaces.Apply(brass, TablefrontSurfaceArt.Finish.Brass);
            surfaces.Apply(steel, TablefrontSurfaceArt.Finish.Brass);
            surfaces.Apply(stone, TablefrontSurfaceArt.Finish.Stone);
            surfaces.Apply(sand, TablefrontSurfaceArt.Finish.Stone);
            for (int i = 0; i < faction.Length; i++) surfaces.Apply(faction[i], TablefrontSurfaceArt.Finish.Enamel);
            walnut.SetTextureScale("_BaseMap", new Vector2(3, 1));
            walnut.SetTextureScale("_BumpMap", new Vector2(3, 1));
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

        void Table()
        {
            Box("Studio floor", scenery, new Vector3(0, -3.2f, 0), new Vector3(180, .3f, 180), ink);
            Box("Walnut plinth", scenery, new Vector3(0, -1.45f, 0), new Vector3(54, 2.1f, 54), walnut);
            Box("Raised bevel", scenery, new Vector3(0, -.35f, 0), new Vector3(53.5f, .25f, 53.5f), edge);
            Box("Brass inner lip", scenery, new Vector3(0, -.18f, 0), new Vector3(51.4f, .13f, 51.4f), brass);
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
            RenderSettings.ambientSkyColor = TablePalette.SkyBounce * .76f;
            RenderSettings.ambientEquatorColor = TablePalette.Muted * .48f;
            RenderSettings.ambientGroundColor = TablePalette.GroundBounce * .65f;
            RenderSettings.fog = false;
            var key = new GameObject("Warm studio key").AddComponent<Light>(); key.transform.SetParent(transform, false);
            key.type = LightType.Directional; key.color = TablePalette.WarmLight; key.intensity = 1.8f;
            key.transform.rotation = Quaternion.Euler(42, -38, 0); key.shadows = LightShadows.Soft; key.shadowStrength = .72f;
            key.shadowBias = .025f; key.shadowNormalBias = .16f;
            var fill = new GameObject("Cool studio fill").AddComponent<Light>(); fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional; fill.color = TablePalette.SkyBounce; fill.intensity = .34f;
            fill.transform.rotation = Quaternion.Euler(35, 135, 0); fill.shadows = LightShadows.None;
            if (view != null)
            {
                view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = TablePalette.Background;
                view.allowHDR = true; view.allowMSAA = true; view.allowDynamicResolution = false;
                var data = view.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
                data.volumeLayerMask = 1; data.antialiasing = AntialiasingMode.None;
                data.stopNaN = true;
                view.cullingMask &= ~(1 << 30); // Studio reflection cards are invisible to the playing camera.
            }
            var urp = UniversalRenderPipeline.asset;
            if (urp != null) TablefrontGraphics.Configure();
            var volume = new GameObject("Tabletop grading").AddComponent<Volume>(); volume.transform.SetParent(transform, false); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); studioProfile = profile; profile.name = "Tablefront warm studio profile"; volume.sharedProfile = profile;
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true); bloom.intensity.value = .28f; bloom.threshold.value = 1.1f; bloom.scatter.value = .55f;
            var vignette = profile.Add<Vignette>(true); vignette.intensity.value = .18f; vignette.smoothness.value = .65f;
            var color = profile.Add<ColorAdjustments>(true); color.saturation.value = 3; color.contrast.value = 9; color.postExposure.value = .05f;
            StudioReflections();
        }

        void StudioReflections()
        {
            // An actual captured studio environment gives enamel and brass broad highlights.
            var cardMaterial = Mat("Warm studio reflection card", TablePalette.WarmLight * 2.6f, 0, 0, true);
            var coolCardMaterial = Mat("Cool studio reflection card", TablePalette.SkyBounce * 1.8f, 0, 0, true);
            var cards = Group("Reflection-only softboxes", transform);
            var warmCard = Box("Large warm softbox", cards, new Vector3(-12, 14, -5), new Vector3(15, .08f, 8), cardMaterial);
            warmCard.localRotation = Quaternion.Euler(0, 0, -22); warmCard.gameObject.layer = 30;
            var coolCard = Box("Cool edge softbox", cards, new Vector3(16, 12, 5), new Vector3(7, .08f, 16), coolCardMaterial);
            coolCard.localRotation = Quaternion.Euler(0, 0, 24); coolCard.gameObject.layer = 30;
            foreach (var renderer in cards.GetComponentsInChildren<Renderer>())
            { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
            var probeObject = new GameObject("Tablefront studio reflection probe"); probeObject.transform.SetParent(transform, false);
            probeObject.transform.position = new Vector3(0, 5, 0);
            studioProbe = probeObject.AddComponent<ReflectionProbe>();
            studioProbe.mode = ReflectionProbeMode.Realtime; studioProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            studioProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            studioProbe.resolution = 256; studioProbe.hdr = true; studioProbe.intensity = .85f;
            studioProbe.boxProjection = true; studioProbe.size = new Vector3(72, 32, 72); studioProbe.center = new Vector3(0, 2, 0);
            studioProbe.blendDistance = 3; studioProbe.nearClipPlane = .3f; studioProbe.farClipPlane = 100;
            studioProbe.clearFlags = ReflectionProbeClearFlags.SolidColor; studioProbe.backgroundColor = TablePalette.SkyBounce * .35f;
            studioProbe.cullingMask = ~0;
        }

        public void Render(BattleSimulation sim, ICollection<int> selected)
        {
            if (!built || sim == null) return;
            SetBattlefield(sim);
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
                model.Art.Tick(sim.Elapsed, Mathf.Clamp01(Mathf.Sqrt(movement) * 5), model.Recoil, ReducedMotion);
                bool isSelected = selected != null && selected.Contains(u.Id);
                bool isHovered = u.Id == HoveredUnitId;
                if (u.Health < model.PreviousHealth - .01f) model.DamageLife = .19f;
                model.PreviousHealth = u.Health;
                model.Selection.SetActive(isSelected || isHovered);
                model.Selection.transform.localScale = Vector3.one * (BaseRadius(u.Kind) + (isSelected ? .25f : .14f));
                model.Damage.SetActive(model.DamageLife > 0 && !ReducedMotion);
                model.Cover.SetActive(sim.IsProtected(u));
                model.Flash.SetActive(u.ShotFlash > .01f);
                model.HealthRoot.gameObject.SetActive(u.Health < u.MaxHealth - .1f || isSelected || isHovered);
                model.HealthRoot.rotation = view.transform.rotation;
                float healthScale = Mathf.Clamp(Vector3.Distance(view.transform.position, model.Root.transform.position) / 47f, 1, 1.65f);
                model.HealthRoot.localScale = Vector3.one * healthScale;
                float fraction = Mathf.Clamp01(u.Health / Mathf.Max(1, u.MaxHealth));
                model.HealthFill.localScale = new Vector3(.88f * fraction, .075f, .02f);
                model.HealthFill.localPosition = new Vector3(-.44f * (1 - fraction), 0, -.012f);
            }
            removed.Clear(); foreach (var pair in miniatures) if (!present.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { ShowWreck(miniatures[id]); Destroy(miniatures[id].Root); miniatures.Remove(id); }
            foreach (var point in sim.Points)
            {
                if (!pointViews.TryGetValue(point.Id, out var lm)) { lm = CreatePoint(point); pointViews.Add(point.Id, lm); }
                lm.Root.transform.position = V3(point.Position, .025f);
                var mat = point.Owner >= 0 ? faction[point.Owner] : neutral;
                lm.Flag.sharedMaterial = mat; lm.Ring.sharedMaterial = point.Owner >= 0 ? factionBright[point.Owner] : neutral;
                lm.Progress.gameObject.SetActive(point.CapturingTeam >= 0 && point.CaptureProgress > .01f);
                lm.Progress.localScale = Vector3.one * Mathf.Lerp(1.2f, 1.36f, point.CaptureProgress);
                if (point.CapturingTeam >= 0) lm.Progress.GetComponent<Renderer>().sharedMaterial = factionBright[point.CapturingTeam];
            }
            foreach (var army in sim.Armies)
            {
                if (!headquarters.TryGetValue(army.Id, out var lm)) { lm = CreateHeadquarters(army); headquarters.Add(army.Id, lm); }
                lm.Root.transform.position = V3(army.BasePosition, .02f);
                lm.Root.SetActive(army.Alive);
                if (lm.Health != null) lm.Health.localScale = new Vector3(Mathf.Clamp(army.BaseHealth / BattleSimulation.BaseMaxHealth, .01f, 1f) * 3.1f, .09f, .08f);
            }
            RenderOrders(sim, selected);
            foreach (var text in worldLabels) if (text != null) text.transform.rotation = view.transform.rotation;
            RenderTutorialTarget();
        }

        /// <summary>Each authored landscape is generated once, then reused on future map switches.</summary>
        public void SetBattlefield(BattleSimulation sim)
        {
            if (!built || sim == null || sim.Battlefield == null || activeBattlefield == sim.Battlefield.Id) return;
            foreach (var pair in battlefieldRoots) pair.Value.gameObject.SetActive(false);
            if (!battlefieldRoots.TryGetValue(sim.Battlefield.Id, out var root))
            {
                root = Group(sim.Battlefield.Name + " landscape", transform);
                var groundMaterial = Mat(sim.Battlefield.Name + " flock", TablePalette.Terrain, .1f);
                TablefrontTerrainArt.Build(root, new TablefrontTerrainArt.Palette
                {
                    Ground = groundMaterial, Soil = sand, Stone = stone, Bark = walnut,
                    Grass = foliage, GrassDry = grassDry, Leaves = foliage, LeavesLight = foliageLight,
                    OwnMesh = OwnMesh, OwnTexture = texture => { ownedTextures.Add(texture); return texture; }
                }, sim.Battlefield);
                CombineStatic(root);
                battlefieldRoots.Add(sim.Battlefield.Id, root);
            }
            activeBattlefield = sim.Battlefield.Id;
            root.gameObject.SetActive(true);
            if (studioProbe != null) studioProbe.RenderProbe();
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
                MeshPart("Cover boundary", root, ringMesh, new Vector3(0, .018f, 0), Vector3.one * zone.Radius, brass);
                TablefrontLandmarkArt.BuildCover(landmarkArt, root, zone.Radius);
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
            bool tank = u.Kind == UnitKind.Tank || u.Kind == UnitKind.Artillery;
            float radius = BaseRadius(u.Kind);
            Part("Dark beveled gaming base", root, PrimitiveType.Cylinder, new Vector3(0, .07f, 0), new Vector3(radius * 2, .07f, radius * 2), ink);
            Part("Enamel base top", root, PrimitiveType.Cylinder, new Vector3(0, .145f, 0), new Vector3(radius * 1.83f, .016f, radius * 1.83f), faction[u.Team]);
            var art = miniatureArt.Build(u.Kind, hull, faction[u.Team], ink, ceramic, steel, brass, factionBright[u.Team], muzzle, u.Id);
            GameObject flash = art.Flash;
            ClassMark(root, u.Kind, radius);
            var selected = MeshPart("Selected unit ring", root, selectionMesh, new Vector3(0, .022f, 0), Vector3.one * (radius + .25f), factionBright[u.Team]).gameObject;
            selected.SetActive(false);
            var damage = MeshPart("Brief incoming hit marker", root, selectionMesh, new Vector3(0, .16f, 0), Vector3.one * (radius + .09f), muzzle).gameObject;
            damage.SetActive(false);
            var cover = MeshPart("Protected rifle ring", root, ringMesh, new Vector3(0, .028f, 0), Vector3.one * (radius + .06f), brass).gameObject;
            cover.SetActive(false);
            var health = Group("Billboard health", root); health.localPosition = new Vector3(0, u.Kind == UnitKind.Artillery ? 2.2f : tank ? 1.68f : 1.75f, 0);
            Box("Health backing", health, Vector3.zero, new Vector3(.98f, .13f, .035f), ink);
            var fill = Box("Health", health, new Vector3(0, 0, -.012f), new Vector3(.88f, .075f, .02f), factionBright[u.Team]);
            return new Miniature { Root = root.gameObject, Hull = hull, Selection = selected, Cover = cover, Flash = flash, Damage = damage,
                HealthRoot = health, HealthFill = fill, Kind = u.Kind, Barrel = art.Barrel, Art = art, PreviousHealth = u.Health };
        }

        static float BaseRadius(UnitKind kind) => kind == UnitKind.Tank ? .97f : kind == UnitKind.Artillery ? 1.08f : kind == UnitKind.Gunner ? .82f : .72f;

        void ClassMark(Transform parent, UnitKind kind, float radius)
        {
            // Cast ivory symbols belong to the physical base, rather than adding more
            // floating labels. Different shapes remain readable without faction color.
            var mark = Group(kind + " cast class insignia", parent);
            mark.localPosition = new Vector3(0, .167f, -radius * .77f);
            switch (kind)
            {
                case UnitKind.Scout:
                    foreach (float side in new[] { -1f, 1f })
                        Box("Recon chevron", mark, new Vector3(side * .10f, 0, 0), new Vector3(.25f, .025f, .06f), ceramic).localEulerAngles = new Vector3(0, side * 35, 0);
                    break;
                case UnitKind.Rifle:
                    Box("Rifle horizontal bar", mark, Vector3.zero, new Vector3(.35f, .025f, .075f), ceramic);
                    break;
                case UnitKind.Gunner:
                    foreach (float z in new[] { -.065f, .065f }) Box("Support double bar", mark, new Vector3(0, 0, z), new Vector3(.39f, .025f, .06f), ceramic);
                    break;
                case UnitKind.Tank:
                    Box("Armor diamond", mark, Vector3.zero, new Vector3(.23f, .025f, .23f), ceramic).localEulerAngles = new Vector3(0, 45, 0);
                    break;
                case UnitKind.Artillery:
                    Part("Artillery roundel", mark, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.28f, .018f, .28f), ceramic);
                    Part("Artillery roundel center", mark, PrimitiveType.Cylinder, new Vector3(0, .02f, 0), new Vector3(.12f, .018f, .12f), ink);
                    break;
            }
        }

        Landmark CreatePoint(ControlPoint p)
        {
            var root = Group("Control point " + p.Name, landmarks); root.position = V3(p.Position, .025f);
            var ring = MeshPart("Control radius", root, ringMesh, Vector3.zero, Vector3.one * BattleSimulation.CaptureRadius, neutral).GetComponent<Renderer>();
            var flag = TablefrontLandmarkArt.BuildPoint(landmarkArt, root, neutral);
            var progress = MeshPart("Capture progress ring", root, ringMesh, new Vector3(0, .36f, 0), Vector3.one * 1.3f, neutral); progress.gameObject.SetActive(false);
            var label = Label(p.Name.ToUpperInvariant(), root, new Vector3(0, 3.7f, 0), 7.2f, TablePalette.Text);
            return new Landmark { Root = root.gameObject, Ring = ring, Flag = flag, Progress = progress, Label = label };
        }

        Landmark CreateHeadquarters(Army a)
        {
            var root = Group(a.Name + " command post", landmarks); root.position = V3(a.BasePosition, .02f);
            Part("HQ base ring", root, PrimitiveType.Cylinder, new Vector3(0, .09f, 0), new Vector3(5.7f, .09f, 5.7f), ink);
            MeshPart("Faction headquarters rim", root, ringMesh, new Vector3(0, .198f, 0), Vector3.one * 2.73f, faction[a.Id]);
            TablefrontLandmarkArt.BuildHeadquarters(landmarkArt, root, faction[a.Id], factionBright[a.Id]);
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
                bool artillery = e.WeaponKind == UnitKind.Artillery;
                bool heavy = e.Heavy || e.WeaponKind == UnitKind.Tank || artillery;
                effect.MaxLife = heavy ? .48f : .22f; effect.Life = effect.MaxLife; effect.Heavy = heavy || e.Killed;
                effect.Trace.enabled = true; effect.Trace.sharedMaterial = e.Heavy ? muzzle : factionBright[Mathf.Clamp(e.Team, 0, 2)];
                effect.Trace.startWidth = heavy ? .16f : e.WeaponKind == UnitKind.Gunner ? .095f : e.WeaponKind == UnitKind.Scout ? .045f : .065f; effect.Trace.endWidth = heavy ? .09f : .035f;
                Vector3 origin = V3(e.From, heavy ? 1 : .85f);
                if (miniatures.TryGetValue(e.ShooterId, out var model))
                {
                    model.Recoil = heavy ? 1f : .18f;
                    if (model.Flash != null) origin = model.Flash.transform.position;
                }
                Vector3 destination = V3(e.To, .75f);
                effect.Trace.positionCount = artillery ? 9 : 2;
                if (artillery)
                    for (int p = 0; p < 9; p++)
                    {
                        float t = p / 8f;
                        effect.Trace.SetPosition(p, Vector3.Lerp(origin, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.2f);
                    }
                else { effect.Trace.SetPosition(0, origin); effect.Trace.SetPosition(1, destination); }
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
            wreck.Root.localScale = Vector3.one * (model.Kind == UnitKind.Tank || model.Kind == UnitKind.Artillery ? 1.2f : .8f);
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
            foreach (var model in miniatures.Values)
            {
                model.Recoil = Mathf.MoveTowards(model.Recoil, 0, dt * 5.5f);
                model.DamageLife = Mathf.Max(0, model.DamageLife - dt);
            }
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
            HoveredUnitId = -1;
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
            CombineStatic(scenery);
        }

        void CombineStatic(Transform root)
        {
            // One mesh per shared material replaces hundreds of tiny static draw submissions.
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var r = f.GetComponent<MeshRenderer>(); if (r == null || f.sharedMesh == null) continue;
                if (!groups.TryGetValue(r.sharedMaterial, out var list)) { list = new List<CombineInstance>(); groups.Add(r.sharedMaterial, list); }
                list.Add(new CombineInstance { mesh = f.sharedMesh, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix });
                r.enabled = false;
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Combined table " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); ownedMeshes.Add(mesh);
                MeshPart(mesh.name, root, mesh, Vector3.zero, Vector3.one, pair.Key);
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
            var mesh = new Mesh { name = "Flat miniature annulus", vertices = verts, uv = uv, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh MakeRock()
        {
            var mesh = new Mesh { name = "Hand-cut low polygon slate" };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var ring = new[] { new Vector3(-.7f, 0, -.5f), new Vector3(.45f, 0, -.65f), new Vector3(.75f, 0, .3f), new Vector3(-.35f, 0, .65f) };
            var top = new Vector3(-.12f, .85f, .05f);
            for (var i = 0; i < 4; i++) { int n = vertices.Count; vertices.Add(ring[i]); vertices.Add(top); vertices.Add(ring[(i + 1) % 4]); triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); }
            var uv = new List<Vector2>(); foreach (var vertex in vertices) uv.Add(new Vector2(vertex.x, vertex.z));
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        void OnDestroy()
        {
            if (studioProfile != null) Destroy(studioProfile);
            foreach (var m in ownedMaterials) if (m != null) Destroy(m);
            foreach (var m in ownedMeshes) if (m != null) Destroy(m);
            foreach (var t in ownedTextures) if (t != null) Destroy(t);
            if (scoutShot != null) Destroy(scoutShot); if (lightShot != null) Destroy(lightShot); if (heavyShot != null) Destroy(heavyShot); if (deathSound != null) Destroy(deathSound);
            if (recruitCue != null) Destroy(recruitCue); if (captureCue != null) Destroy(captureCue); if (lostCue != null) Destroy(lostCue);
            if (ambience != null && ambience.clip != null) Destroy(ambience.clip);
        }
    }
}
