using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Tablefront;
using UnityEngine;

// Editor-only verification. Real routing/requests drive every bridge pose; no native input.
public static class VerifyBridgeArt
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(object owner, string name) => owner.GetType().GetField(name, Fields).GetValue(owner);
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        if (!game) throw new Exception("Open Tablefront in Play Mode.");
        var world = game.World;
        bool enabled = game.enabled, reduced = world.ReducedMotion;
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); checks.Add("PASS " + name); }
        BattleSimulation Setup(BattlefieldId id)
        {
            var battle = new BattleSimulation(false, 423, id) { UseExternalAI = true };
            foreach (var army in battle.Armies) { army.IsAI = false; typeof(Army).GetField("TurretTimer", Fields).SetValue(army, 99999f); }
            foreach (var troop in battle.Units) typeof(Troop).GetField("WeaponTimer", Fields).SetValue(troop, 99999f);
            world.ClearBattle(); world.SetBattlefield(battle); world.Render(battle, new HashSet<int>()); return battle;
        }
        GameObject Model(int id) => (GameObject)Field(((IDictionary)Field(world, "miniatures"))[id], "Root");
        int Count(string name) => ((ICollection)Field(world, name)).Count;
        try
        {
            game.enabled = false; world.ReducedMotion = true;
            var sim = Setup(BattlefieldId.BridgeGambit);
            var art = (TablefrontBridgeArt)Field(world, "bridgeArt");
            Check(art != null && art.Root.gameObject.activeInHierarchy, "Bridge Gambit displays its physical crossing");
            Check(art.Root.GetComponentsInChildren<Collider>(true).Length == 0, "Bridge artwork adds no conflicting gameplay colliders");
            Check(Vector3.Distance(art.Deck.TransformDirection(Vector3.right), new Vector3(.6f, 0, .8f)) < .001f,
                "The physical North deck matches the rules' SW-to-NE crossing");
            Check(!art.Preview.gameObject.activeSelf, "A stable crossing does not leave a warning outline on the board");
            var hqs = (IDictionary)Field(world, "headquarters");
            Check(!hqs.Contains(2) || !((GameObject)Field(hqs[2], "Root")).activeInHierarchy,
                "The unused third headquarters is absent from the two-army mission");
            Check(art.Root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial != null && r.sharedMaterial.shader.isSupported),
                "Bridge, bank guides and preview use supported shared materials");
            Check(art.Root.GetComponentsInChildren<MeshRenderer>().Length < 24,
                "Bridge planks and trusses are combined into bounded material batches");

            var rider = sim.Units.First(u => u.Team == 0 && u.Kind == UnitKind.Scout);
            Vector2 target = new Vector2(-1.8f, -2.4f);
            sim.OrderMove(new[] { rider.Id }, target, false);
            for (int i = 0; i < 1200 && Vector2.Distance(rider.Position, target) > .35f; i++) sim.Step(.05f);
            Check(rider.Alive && Vector2.Distance(rider.Position, target) <= .35f && sim.Bridge.OnBridge(rider.Position),
                "The rider reaches the bridge through normal mission navigation");
            world.Render(sim, new HashSet<int> { rider.Id });
            Check(Mathf.Abs(Model(rider.Id).transform.position.y - TablefrontBridgeArt.DeckFloor) < .001f,
                "A troop crossing the river stands on the bridge deck rather than under it");
            float health = rider.Health;
            Check(sim.TryRequestBridge(0, BridgeRoute.South), "A real player request starts the announced rotation");
            world.Render(sim, new HashSet<int> { rider.Id });
            Check(art.Preview.gameObject.activeSelf && art.RouteOutline.positionCount == 5 && art.RouteArrow.positionCount == 5,
                "The warning shows the destination footprint and direction");
            Check(Vector3.Distance(art.Preview.TransformDirection(Vector3.right), new Vector3(.6f, 0, -.8f)) < .001f,
                "The preview points to the actual South landing alignment");
            var faction = (Material[])Field(world, "factionBright");
            Check(art.RouteOutline.sharedMaterial == faction[0], "The announced player route uses the same blue signal as the army");
            Check(((AudioSource)Field(world, "bridgeAudio")).clip == (AudioClip)Field(world, "bridgeWarningCue"),
                "The Warning transition selects the dedicated quiet warning bell");
            sim.Step(2); sim.Step(.9f); world.Render(sim, new HashSet<int> { rider.Id });
            Check(sim.Bridge.Phase == BridgePhase.Warning && art.Preview.gameObject.activeSelf,
                "The destination remains visible throughout the three-second warning");
            sim.Step(.7f); world.Render(sim, new HashSet<int> { rider.Id });
            Check(sim.Bridge.Phase == BridgePhase.Moving && sim.Bridge.IsRiding(rider.Id), "Normal simulation carries the actual bridge passenger");
            Check(Mathf.Abs(Mathf.DeltaAngle(-art.Deck.localEulerAngles.y, sim.Bridge.AngleDegrees)) < .01f,
                "Reduced motion preserves the exact gameplay bridge pose");
            var riderModel = Model(rider.Id).transform;
            Vector3 deckLocal = art.Deck.InverseTransformPoint(riderModel.position);
            Check(Mathf.Abs(deckLocal.z) <= TablefrontBridge.HalfWidth && Mathf.Abs(riderModel.position.y - TablefrontBridgeArt.DeckFloor) < .001f,
                "The moving passenger stays on the physical planks");
            Check(((AudioSource)Field(world, "bridgeAudio")).clip == (AudioClip)Field(world, "bridgeMotorCue"),
                "The Moving transition selects the two-second mechanism sound");
            world.ReducedMotion = false; world.Render(sim, new HashSet<int> { rider.Id });
            Check(Mathf.Abs(Model(rider.Id).transform.position.y - TablefrontBridgeArt.DeckFloor) < .001f,
                "Normal visual smoothing does not let a moving passenger dip into the river");
            world.ReducedMotion = true;
            for (int i = 0; i < 45 && sim.Bridge.Phase != BridgePhase.Stable; i++) sim.Step(.05f);
            world.Render(sim, new HashSet<int> { rider.Id });
            Check(sim.Bridge.Phase == BridgePhase.Stable && sim.Bridge.CurrentRoute == BridgeRoute.South && !art.Preview.gameObject.activeSelf,
                "Docking clears the preview and displays the completed South route");
            Check(Vector3.Distance(art.Deck.TransformDirection(Vector3.right), new Vector3(.6f, 0, -.8f)) < .001f,
                "The docked physical deck matches the new walkable alignment");
            Check(rider.Alive && Mathf.Abs(rider.Health - health) < .001f, "The visible bridge ride causes no cosmetic fall or damage");
            Check(((AudioSource)Field(world, "bridgeAudio")).clip == (AudioClip)Field(world, "bridgeDockCue"),
                "Docking selects one quiet latch cue");

            Setup(BattlefieldId.Greenfield);
            Check(!art.Root.gameObject.activeSelf, "Ordinary skirmishes hide the mission bridge completely");
            Setup(BattlefieldId.BridgeGambit); await Task.Delay(100);
            int meshes = Count("ownedMeshes"), materials = Count("ownedMaterials"), textures = Count("ownedTextures");
            for (int cycle = 0; cycle < 3; cycle++) { Setup(BattlefieldId.Greenfield); Setup(BattlefieldId.BridgeGambit); }
            Check(meshes == Count("ownedMeshes") && materials == Count("ownedMaterials") && textures == Count("ownedTextures"),
                "Repeated mission and skirmish restarts reuse the bridge and terrain resources");
            sim = Setup(BattlefieldId.BridgeGambit); sim.TryRequestBridge(1, BridgeRoute.South); world.Render(sim, new HashSet<int>());
            Check(art.RouteOutline.sharedMaterial == faction[1], "Enemy requests visibly use the same orange signal as its army");
            Directory.CreateDirectory("TestResults"); File.WriteAllLines("TestResults/bridge-art-runtime.txt", checks);
            return checks.Count + " checks passed\n" + string.Join("\n", checks);
        }
        finally { world.ReducedMotion = reduced; game.enabled = enabled; game.ReturnToMenu(); }
    }
}
