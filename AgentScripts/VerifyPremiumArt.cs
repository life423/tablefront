using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only visual-resource integration checks. No native input, training or gameplay-rule changes.
public static class VerifyPremiumArt
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string ReducedKey = "Tablefront.ReducedMotion";

    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront.unity in Play Mode first.");
        var passed = new List<string>();
        bool reduced = game.ReducedMotion;
        bool hadReduced = PlayerPrefs.HasKey(ReducedKey);
        int oldReduced = PlayerPrefs.GetInt(ReducedKey, 0);
        object Field(object target, string name)
        {
            var field = target.GetType().GetField(name, Fields);
            if (field == null) throw new Exception("QA field unavailable: " + target.GetType().Name + "." + name);
            return field.GetValue(target);
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }
        bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        bool FiniteVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        void Render() { game.World.ReducedMotion = game.ReducedMotion; game.World.Render(game.Sim, game.Selected); }
        async Task Settle() { Render(); await Task.Delay(150); Render(); }
        ICollection Meshes() => (ICollection)Field(game.World, "ownedMeshes");
        ICollection Materials() => (ICollection)Field(game.World, "ownedMaterials");
        ICollection Textures() => (ICollection)Field(game.World, "ownedTextures");
        object Model(int id) => ((IDictionary)Field(game.World, "miniatures"))[id];
        Transform MiniatureRoot(int id) => ((GameObject)Field(Model(id), "Root")).transform;
        Renderer[] VisibleRenderers() => game.World.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        void Quiet()
        {
            foreach (var army in game.Sim.Armies) army.IsAI = false;
            if (!game.Paused) game.TogglePause();
        }
        Quaternion LegPose(Transform root) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hip pivot").localRotation;
        Quaternion WheelPose(Transform root) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Recon wheel pivot").localRotation;
        bool Changed(Quaternion a, Quaternion b) => Quaternion.Angle(a, b) > .01f;

        try
        {
            game.ReducedMotion = true;
            game.StartBattle(false); Quiet(); await Settle();
            Check(game.Sim.UnitCount(0) == 6 && game.Sim.Units.Count == 18, "Art replacement retains normal initial army counts");
            var visible = VisibleRenderers();
            Check(visible.Length > 0 && visible.All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader")),
                "Every visible world renderer has a supported shader rather than a missing or pink error material");
            Check(visible.All(r => r.sharedMaterials.All(m => m.name != "Default-Material")), "Visible tabletop art uses owned authored materials rather than Default-Material");
            Check(game.World.GetComponentsInChildren<Collider>(true).Length == 0, "Decorative miniatures and scenery introduce no physical colliders after deferred cleanup");

            var uniqueMeshes = new HashSet<Mesh>(visible.Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null).Select(f => f.sharedMesh));
            Check(uniqueMeshes.All(m => m.vertexCount > 0 && FiniteVector(m.bounds.center) && FiniteVector(m.bounds.extents) && m.bounds.extents.x >= 0 && m.bounds.extents.y >= 0 && m.bounds.extents.z >= 0),
                "Rendered shared meshes have nonempty geometry and finite nonnegative bounds");
            Check(uniqueMeshes.Where(m => m.isReadable).All(m => m.vertices.All(FiniteVector)), "Readable world mesh positions remain finite");
            Check(visible.All(r => FiniteVector(r.bounds.center) && FiniteVector(r.bounds.extents)), "Combined and animated renderer world bounds remain finite");

            var mapped = visible.OfType<MeshRenderer>().Where(r => r.sharedMaterial.IsKeywordEnabled("_NORMALMAP")).ToArray();
            Check(mapped.Length > 0 && mapped.All(r => r.sharedMaterial.GetTexture("_BumpMap") != null), "Normal-mapped materials actually bind their surface-detail texture");
            var missingAttributes = mapped.Where(r =>
            {
                var filter = r.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                return mesh == null || !mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || !mesh.HasVertexAttribute(VertexAttribute.Tangent);
            }).Select(r => r.name).Distinct().ToArray();
            Check(missingAttributes.Length == 0, "Normal-mapped visible geometry supplies normals, UVs and tangents" + (missingAttributes.Length == 0 ? "" : ": " + string.Join(", ", missingAttributes)));
            Check(mapped.Where(r => r.sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP")).All(r => r.sharedMaterial.GetTexture("_MetallicGlossMap") != null),
                "Enabled surface-mask variants bind their metallic/smoothness map");
            var probe = game.World.GetComponentsInChildren<ReflectionProbe>(true).FirstOrDefault(p => p.enabled);
            Check(probe != null && probe.texture != null && probe.texture.width > 0, "The studio reflection probe has produced an actual reflection texture");
            Check(probe.bounds.Contains(Vector3.zero), "The reflection-probe influence volume covers the playable table center");
            var scenery = game.World.transform.Find("Campaign table");
            int staticRenderers = scenery.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy);
            Check(staticRenderers > 0 && staticRenderers <= 64, "Static scenery submits at most 64 material-combined renderers (observed " + staticRenderers + ")");

            // All factions and classes are warmed before checking repeated player/tutorial rematches.
            game.StartTutorial(); Quiet(); await Settle();
            game.StartBattle(false); Quiet(); await Settle();
            int meshCount = Meshes().Count, materialCount = Materials().Count, textureCount = Textures().Count;
            var meshesByRound = new List<int>();
            bool noGrowth = true;
            for (int round = 0; round < 3; round++)
            {
                game.StartBattle(false); Quiet(); await Settle();
                game.StartTutorial(); Quiet(); await Settle();
                meshesByRound.Add(Meshes().Count);
                noGrowth &= Meshes().Count == meshCount && Materials().Count == materialCount && Textures().Count == textureCount;
            }
            Check(noGrowth, "Shared mesh/material/texture ownership stays bounded across three battle/tutorial restart cycles (meshes " + meshCount + "; rounds " + string.Join(",", meshesByRound) + ")");
            Check(game.World.GetComponentsInChildren<Collider>(true).Length == 0, "Restarting practice does not leave newly spawned decorative colliders");

            // Capture the beacon through ordinary movement and capture rules, then inspect the new pennant.
            game.StartTutorial(); Quiet();
            var point = game.Sim.Points[0];
            game.Selected.Clear(); foreach (var unit in game.Sim.Units.Where(u => u.Team == 0 && u.Alive)) game.Selected.Add(unit.Id);
            // Pausing the shell avoids time-dependent interference; direct simulation steps retain the real rules.
            game.Sim.OrderMove(game.Selected, point.Position);
            for (int i = 0; i < 1000 && point.Owner != 0; i++) game.Sim.Step(.05f);
            Render();
            Check(point.Owner == 0, "The premium battlefield still captures a beacon through actual occupation");
            var landmark = ((IDictionary)Field(game.World, "pointViews"))[point.Id];
            var flag = (Renderer)Field(landmark, "Flag"); var radius = (Renderer)Field(landmark, "Ring");
            Check(Vector4.Distance(flag.sharedMaterial.GetColor("_BaseColor"), TablePalette.Team(0)) < .001f,
                "The premium pennant changes to the actual capturing faction's enamel material");
            Check(Vector4.Distance(radius.sharedMaterial.GetColor("_BaseColor"), TablePalette.Team(0)) < .001f,
                "The capture-radius ring follows the same actual beacon ownership");

            var rifles = game.Sim.Units.Where(u => u.Team == 0 && u.Kind == UnitKind.Rifle).ToArray();
            var cover = game.Sim.CoverZones[0]; game.Sim.OrderMove(rifles.Select(u => u.Id), cover.Position);
            for (int i = 0; i < 1000 && !rifles.Any(u => game.Sim.IsProtected(u)); i++) game.Sim.Step(.05f);
            Render();
            Check(rifles.Any(u => game.Sim.IsProtected(u)), "Rifle movement still reaches mechanical cover through the detailed sandbag art");
            Check(game.Sim.Units.All(u => ((GameObject)Field(Model(u.Id), "Cover")).activeSelf == game.Sim.IsProtected(u)),
                "Visible cover rings match actual protection rather than visual scenery proximity");

            game.ReducedMotion = false; game.World.ReducedMotion = false;
            var rifle = rifles[0]; var scout = game.Sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            game.Sim.OrderMove(new[] { rifle.Id, scout.Id }, new Vector2(12, -12));
            var legSamples = new List<Quaternion>(); var wheelSamples = new List<Quaternion>();
            foreach (float delta in new[] { .13f, .19f, .23f })
            {
                game.Sim.Step(delta); Render();
                legSamples.Add(LegPose(MiniatureRoot(rifle.Id))); wheelSamples.Add(WheelPose(MiniatureRoot(scout.Id)));
            }
            Check(Changed(legSamples[0], legSamples[1]) && Changed(legSamples[1], legSamples[2]), "Live simulation movement produces visibly different articulated infantry poses at non-aliasing sample times");
            Check(Changed(wheelSamples[0], wheelSamples[1]) && Changed(wheelSamples[1], wheelSamples[2]), "Live recon movement turns its wheel pivots");
            game.ReducedMotion = true; game.World.ReducedMotion = true;
            Vector2 reducedStart = rifle.Position;
            game.Sim.Step(.11f); Render(); var reducedLeg = LegPose(MiniatureRoot(rifle.Id)); var reducedWheel = WheelPose(MiniatureRoot(scout.Id));
            game.Sim.Step(.27f); Render();
            Check(!Changed(reducedLeg, LegPose(MiniatureRoot(rifle.Id))) && !Changed(reducedWheel, WheelPose(MiniatureRoot(scout.Id))),
                "Reduced motion holds cosmetic limb and wheel poses stable while simulation steps continue");
            Check(Vector2.Distance(reducedStart, rifle.Position) > .01f, "Reduced motion does not freeze or change mechanical troop movement");

            game.StartTutorial(); await Settle();
            Check(game.TutorialActive && game.TutorialStep == 0 && game.Sim.PracticeMode, "Fresh tutorial still starts safely after visual resource and animation checks");
            Directory.CreateDirectory("TestResults"); File.WriteAllLines("TestResults/premium-art-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            game.ReducedMotion = reduced; game.World.ReducedMotion = reduced;
            if (hadReduced) PlayerPrefs.SetInt(ReducedKey, oldReduced); else PlayerPrefs.DeleteKey(ReducedKey);
            game.ReturnToMenu();
        }
    }
}
