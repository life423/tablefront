using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;

// Root runs this in the Editor. No native input, paid action, artificial bridge phase or fake hit events.
public static class VerifyTacticalArt
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(object owner, string name) => owner.GetType().GetField(name, Fields).GetValue(owner);
    static void Timer(Troop troop, float seconds) => typeof(Troop).GetField("WeaponTimer", Fields).SetValue(troop, seconds);
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront in Play Mode.");
        var world = game.World; bool enabled = game.enabled, reduced = world.ReducedMotion;
        int hover = world.HoveredUnitId; var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); checks.Add("PASS " + name); }
        BattleSimulation Setup(BattlefieldId id = BattlefieldId.BridgeGambit)
        {
            var sim = new BattleSimulation(false, 7423, id) { UseExternalAI = true };
            foreach (var army in sim.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var troop in sim.Units) Timer(troop, 99999);
            world.ClearBattle(); world.Render(sim, new HashSet<int>()); return sim;
        }
        object Model(int id) => ((IDictionary)Field(world, "miniatures"))[id];
        int Count(string name) => ((ICollection)Field(world, name)).Count;
        try
        {
            game.enabled = false; world.ReducedMotion = true;
            var sim = Setup(); var art = (TablefrontTacticalArt)Field(world, "tacticalArt");
            var signals = (Material[])Field(world, "factionBright");
            Check(art != null && art.Root.gameObject.activeInHierarchy, "Mission displays its cached tactical scenery");
            Check(art.Root.GetComponentsInChildren<Collider>(true).Length == 0, "Tactical art adds no hidden physics or pick colliders");
            Check(art.Obstacles.Length == 2 && art.Covers.Length == 2, "Both banks have one equal solid obstacle and one directional cover area");
            for (int i = 0; i < art.Obstacles.Length; i++)
            {
                var logic = sim.Tactics.Obstacles[i]; var root = art.Obstacles[i];
                Check(Vector3.Distance(root.position, new Vector3(logic.Center.x, 0, logic.Center.y)) < .001f &&
                    Mathf.Abs(Mathf.DeltaAngle(-root.eulerAngles.y, logic.AngleDegrees)) < .001f, "Blocker " + i + " uses the exact shared center and angle");
                var renderers = root.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.size.x - logic.HalfSize.x * 2) < .01f && Mathf.Abs(bounds.size.z - logic.HalfSize.y * 2) < .01f &&
                    Mathf.Abs(bounds.min.y) < .01f && Mathf.Abs(bounds.max.y - logic.Height) < .01f, "Blocker " + i + " visible bounds match its collision and sight silhouette");
                var cover = sim.Tactics.Covers[i];
                Check(Vector3.Distance(art.Covers[i].position, new Vector3(cover.Center.x, .055f, cover.Center.y)) < .001f &&
                    Vector3.Distance(art.Covers[i].TransformDirection(Vector3.right), new Vector3(cover.Facing.x, 0, cover.Facing.y)) < .001f,
                    "Cover " + i + " front arc and sandbags face the actual protected direction");
            }
            Check(art.Root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial != null && r.sharedMaterial.shader.isSupported),
                "Physical tactical cues use supported shared palette materials");
            Check(art.Root.GetComponentsInChildren<Renderer>().Length < 36, "Tactical scenery is bounded to fewer than 36 material batches and indicators");
            Check(art.UnitProtectionArc.bounds.min.x > .45f && art.UnitFlankArc.bounds.min.x < -.99f,
                "Protection uses a forward partial arc; exposed hit feedback uses an unmistakably different rear arc");
            Check(!art.RechargeFill[0].gameObject.activeSelf && !art.RechargeFill[1].gameObject.activeSelf,
                "Full command capacitors show no misleading recharge progress");
            world.SetBridgePlanning(true); world.Render(sim, new HashSet<int>());
            var bridgeArt = (TablefrontBridgeArt)Field(world, "bridgeArt");
            Check(bridgeArt.Preview.gameObject.activeSelf && sim.Bridge.Phase == BridgePhase.Stable && sim.Bridge.CommandsRemaining(0) == 3 &&
                Vector3.Distance(bridgeArt.Preview.TransformDirection(Vector3.right), new Vector3(.6f, 0, -.8f)) < .001f,
                "Stable bridge planning previews the alternative landing without spending a command");
            world.SetBridgePlanning(false); world.Render(sim, new HashSet<int>());
            Check(!bridgeArt.Preview.gameObject.activeSelf, "Leaving the contextual bridge action removes the planning ghost");

            for (int side = 0; side < 2; side++)
            {
                sim = Setup(); sim.Units.Clear();
                var defender = new Troop { Id = 501, Team = 1, Kind = UnitKind.Rifle, Position = new Vector2(10, 7), Health = 115, MaxHealth = 115, HoldPosition = true };
                var attacker = new Troop { Id = 500, Team = 0, Kind = UnitKind.Rifle, Position = side == 0 ? new Vector2(6, 8) : new Vector2(14, 6.8f), Health = 115, MaxHealth = 115, HoldPosition = true, AttackTargetId = defender.Id };
                Timer(defender, 99999); Timer(attacker, 0); sim.Units.Add(attacker); sim.Units.Add(defender);
                world.ClearBattle(); world.HoveredUnitId = defender.Id; world.Render(sim, new HashSet<int> { attacker.Id });
                var model = Model(defender.Id); var cover = (GameObject)Field(model, "Cover");
                Check(cover.activeSelf && cover.GetComponent<MeshFilter>().sharedMesh == art.UnitProtectionArc,
                    (side == 0 ? "Frontal" : "Rear") + " fixture exposes the enemy cover direction only while inspected");
                sim.Step(.05f);
                var hit = sim.Events.FirstOrDefault(e => e.TargetId == defender.Id);
                Check(hit.ShooterId == attacker.Id && hit.ProtectedHit == (side == 0) && hit.FlankedHit == (side == 1),
                    "Actual " + (side == 0 ? "frontal" : "rear") + " shot supplies exact target and cover classification");
                world.PlayEvents(sim.Events); world.ReducedMotion = false; world.Render(sim, new HashSet<int> { attacker.Id });
                Check(cover.GetComponent<MeshFilter>().sharedMesh == (side == 0 ? art.UnitProtectionArc : art.UnitFlankArc),
                    "Actual " + (side == 0 ? "protected" : "flanked") + " hit displays its corresponding directional feedback");
                world.ReducedMotion = true; world.HoveredUnitId = -1; world.Render(sim, new HashSet<int>());
                Check(!cover.activeSelf, "Reduced motion suppresses the unselected transient cover-hit cue " + side);
                world.Render(sim, new HashSet<int> { defender.Id });
                Check(cover.activeSelf && cover.GetComponent<MeshFilter>().sharedMesh == art.UnitProtectionArc,
                    "Reduced motion retains the selected unit's meaningful protection direction " + side);
            }

            sim = Setup(); var scout = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            var center = new Vector2(-1.2f, -1.6f); sim.OrderMove(new[] { scout.Id }, center, false);
            for (int i = 0; i < 1200 && Vector2.Distance(scout.Position, center) > .3f; i++) sim.Step(.05f);
            Check(Vector2.Distance(scout.Position, center) <= .3f && sim.Bridge.OnBridge(scout.Position), "Normal mission navigation reaches ROTARY for physical recharge proof");
            Check(sim.TryRequestBridge(0, BridgeRoute.South), "Actual bridge request spends a real command for indicator verification");
            world.Render(sim, new HashSet<int> { scout.Id });
            Check(art.CommandCaps[0, 0].sharedMaterial == signals[0] && art.CommandCaps[0, 1].sharedMaterial == signals[0] &&
                !art.CommandCaps[0, 2].enabled && art.CommandCaps[0, 2].sharedMaterial != signals[0] && art.CommandCaps[1, 2].enabled && art.CommandCaps[1, 2].sharedMaterial == signals[1],
                "The physical capacitor shows two blue commands while all three orange commands remain");
            for (int i = 0; i < 1000 && sim.BridgeRechargeSeconds(0) < 9.5f; i++) sim.Step(.05f);
            world.Render(sim, new HashSet<int> { scout.Id });
            float fraction = sim.BridgeRechargeSeconds(0) / BattleSimulation.BridgeRechargeRequired;
            Check(sim.Points[2].Owner == 0 && fraction >= .47f && fraction < .6f && art.RechargeFill[0].gameObject.activeSelf &&
                Mathf.Abs(art.RechargeFill[0].localScale.z / 1.12f - fraction) < .001f && !art.RechargeFill[1].gameObject.activeSelf,
                "Actual uncontested capture displays only the owning army's exact partial recharge");
            for (int i = 0; i < 500 && sim.Bridge.CommandsRemaining(0) < 3; i++) sim.Step(.05f);
            world.Render(sim, new HashSet<int>());
            Check(sim.Bridge.CommandsRemaining(0) == 3 && art.CommandCaps[0, 2].sharedMaterial == signals[0] && !art.RechargeFill[0].gameObject.activeSelf,
                "A genuine completed recharge restores the third cap and removes its progress bar");

            Setup(BattlefieldId.Greenfield); Check(!art.Root.gameObject.activeSelf, "Ordinary skirmishes hide all mission-only tactical art");
            Setup(); await Task.Delay(100);
            int meshes = Count("ownedMeshes"), materials = Count("ownedMaterials"), textures = Count("ownedTextures");
            for (int i = 0; i < 3; i++) { Setup(BattlefieldId.Greenfield); Setup(); }
            Check(meshes == Count("ownedMeshes") && materials == Count("ownedMaterials") && textures == Count("ownedTextures"),
                "Mission and skirmish rematches reuse all tactical meshes, shared materials and textures");
            Check(ReferenceEquals(art, Field(world, "tacticalArt")), "Rematches keep one tactical-art instance");
            Directory.CreateDirectory("TestResults"); File.WriteAllLines("TestResults/tactical-art-runtime.txt", checks);
            return checks.Count + " checks passed\n" + string.Join("\n", checks);
        }
        finally { world.HoveredUnitId = hover; world.ReducedMotion = reduced; game.enabled = enabled; game.ReturnToMenu(); }
    }
}
