using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;

// Editor-only rendering regression checks; never ships in Assets and sends no native input.
public static class VerifyExpandedArt
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new InvalidOperationException("Open Tablefront in Play mode.");
        var world = game.World;
        bool gameEnabled = game.enabled, reduced = world.ReducedMotion;
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            passed.Add("PASS " + name);
        }
        ICollection Owned(string name) => (ICollection)Field(world, name);
        IDictionary Models() => (IDictionary)Field(world, "miniatures");
        GameObject ModelObject(int id, string name) => (GameObject)Field(Models()[id], name);
        Transform ModelTransform(int id, string name) => (Transform)Field(Models()[id], name);
        BattleSimulation Setup(BattlefieldId id)
        {
            var sim = new BattleSimulation(false, 592, id) { UseExternalAI = true };
            foreach (var army in sim.Armies)
            {
                army.IsAI = false;
                army.Credits = 999; army.RecruitCooldown = 0; sim.Recruit(army.Id, UnitKind.Gunner);
                army.Credits = 999; army.RecruitCooldown = 0; sim.Recruit(army.Id, UnitKind.Artillery);
            }
            world.ClearBattle(); world.SetBattlefield(sim); world.Render(sim, new HashSet<int>());
            return sim;
        }

        try
        {
            game.enabled = false; world.ReducedMotion = false;
            BattleSimulation current = null;
            var landscapeMeshes = new HashSet<Mesh>();
            foreach (var map in TablefrontBattlefields.All)
            {
                current = Setup(map.Id);
                await Task.Delay(160);
                var roots = (IDictionary)Field(world, "battlefieldRoots");
                int activeCount = 0;
                foreach (DictionaryEntry pair in roots) if (((Transform)pair.Value).gameObject.activeInHierarchy) activeCount++;
                Check(activeCount == 1 && ((Transform)roots[map.Id]).gameObject.activeInHierarchy,
                    map.Name + " displays exactly its own cached landscape");
                var ground = ((Transform)roots[map.Id]).GetComponentsInChildren<MeshFilter>(true)
                    .First(f => f.name == "Continuous flock and worn earth");
                Check(ground.sharedMesh.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z) &&
                    v.y >= 0 && v.y <= TablefrontTerrainArt.MaximumGroundHeight),
                    map.Name + " keeps the continuous terrain cosmetic and below miniature bases");
                landscapeMeshes.Add(ground.sharedMesh);
                var points = (IDictionary)Field(world, "pointViews");
                Check(current.Points.All(p => Vector3.Distance(((GameObject)Field(points[p.Id], "Root")).transform.position,
                    new Vector3(p.Position.x, .025f, p.Position.y)) < .001f), map.Name + " point art matches actual objective positions");
                var hqs = (IDictionary)Field(world, "headquarters");
                Check(current.Armies.All(a => Vector3.Distance(((GameObject)Field(hqs[a.Id], "Root")).transform.position,
                    new Vector3(a.BasePosition.x, .02f, a.BasePosition.y)) < .001f), map.Name + " HQ art follows the actual deployment layout");
            }
            Check(landscapeMeshes.Count == 4, "Four layouts own distinct generated ground meshes");
            int meshes = Owned("ownedMeshes").Count, materials = Owned("ownedMaterials").Count, textures = Owned("ownedTextures").Count;
            for (int round = 0; round < 2; round++) foreach (var map in TablefrontBattlefields.All)
            { current = Setup(map.Id); await Task.Delay(30); }
            Check(meshes == Owned("ownedMeshes").Count && materials == Owned("ownedMaterials").Count && textures == Owned("ownedTextures").Count,
                "Two further cycles through all four maps and five classes allocate no new owned meshes, materials or textures");
            Check(world.GetComponentsInChildren<Collider>(true).Length == 0, "Landscape and miniature art add no decorative gameplay colliders");

            foreach (UnitKind kind in BattleSimulation.UnitKinds)
            {
                var troop = current.Units.First(u => u.Team == 0 && u.Kind == kind);
                var root = ModelObject(troop.Id, "Root");
                Check(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == kind + " cast class insignia"),
                    kind + " has its own physical base class mark");
            }
            var gunner = current.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Gunner);
            var artillery = current.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Artillery);
            Check(ModelObject(gunner.Id, "Root").GetComponentsInChildren<Transform>(true).Any(t => t.name == "Crew-served heavy machine gun"),
                "Gunner uses the broad crew-served weapon model");
            Check(ModelObject(artillery.Id, "Root").GetComponentsInChildren<Transform>(true).Any(t => t.name == "Elevated gun axis"),
                "Artillery uses an elevated long gun rather than the tank or rifle silhouette");

            var selected = new HashSet<int> { gunner.Id };
            world.Render(current, selected);
            Check(ModelObject(gunner.Id, "Selection").activeSelf && ModelTransform(gunner.Id, "HealthRoot").gameObject.activeSelf,
                "Selected troops have a strong ring and visible health");
            Check(!ModelTransform(artillery.Id, "HealthRoot").gameObject.activeSelf, "Healthy unselected troops do not add idle health clutter");
            world.HoveredUnitId = artillery.Id; world.Render(current, selected);
            Check(ModelObject(artillery.Id, "Selection").activeSelf && ModelTransform(artillery.Id, "HealthRoot").gameObject.activeSelf,
                "Hover reveals the targeted miniature and its health");
            world.HoveredUnitId = -1;
            gunner.Health -= 10; world.Render(current, selected);
            Check(ModelObject(gunner.Id, "Damage").activeSelf, "Actual health loss briefly marks the damaged base");
            await Task.Delay(300); world.Render(current, selected);
            Check(!ModelObject(gunner.Id, "Damage").activeSelf, "Incoming-hit marker expires instead of becoming a permanent overlay");
            world.ReducedMotion = true; gunner.Health -= 5; world.Render(current, selected);
            Check(!ModelObject(gunner.Id, "Damage").activeSelf && ModelTransform(gunner.Id, "HealthRoot").gameObject.activeSelf,
                "Reduced motion suppresses the hit flash while preserving health feedback");

            int effects = ((ICollection)Field(world, "effects")).Count;
            var shell = new CombatEvent { From = artillery.Position, To = artillery.Position + Vector2.up * 10,
                Team = 0, ShooterId = artillery.Id, WeaponKind = UnitKind.Artillery, Heavy = true };
            world.PlayEvents(new[] { shell });
            Check(world.GetComponentsInChildren<LineRenderer>(true).Any(l => l.enabled && l.positionCount == 9 && l.GetPosition(4).y > l.GetPosition(8).y + 1),
                "Artillery events use a visible curved shell trace");
            for (int i = 0; i < 12; i++) world.PlayEvents(new[] { shell });
            Check(effects == ((ICollection)Field(world, "effects")).Count && effects == 128,
                "Repeated new-unit shots retain the fixed 128-slot effect pool");
            var visible = world.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy);
            Check(visible.All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader")),
                "All active expanded-unit and map materials use supported shaders");

            Directory.CreateDirectory("TestResults");
            File.WriteAllLines("TestResults/expanded-art-runtime.txt", passed);
            return passed.Count + " checks passed\n" + string.Join("\n", passed);
        }
        finally
        {
            world.ReducedMotion = reduced;
            game.enabled = gameEnabled;
            game.ReturnToMenu();
        }
    }
}
