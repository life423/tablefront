using UnityEngine;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>One reusable rotary bridge. Rules own the pose; this assembly is visual only.</summary>
    public sealed class TablefrontBridgeArt
    {
        public const float DeckFloor = .315f;
        public readonly Transform Root, Deck, Preview;
        public readonly LineRenderer RouteOutline, RouteArrow;
        readonly Renderer[] landingSignals = new Renderer[4];
        readonly int[] landingRoutes = new int[4];
        readonly Material brass, inactive;
        readonly Material[] signals;
        TablefrontBridge previousBridge;
        float visualAngle;
        public float VisualAngle => visualAngle;

        public TablefrontBridgeArt(Transform parent, TablefrontLandmarkArt.Kit kit, Material wood,
            Material steel, Material ink, Material ivory, Material gold, Material[] factionSignals)
        {
            brass = gold; inactive = ink; signals = factionSignals;
            Root = Group("Bridge Gambit rotary crossing", parent);
            var hardware = Group("Fixed bearing and landing foundations", Root);
            Cylinder("Recessed pivot foundation", hardware, kit.Cylinder, new Vector3(0, -.04f, 0), new Vector3(3.1f, .16f, 3.1f), ink);
            Cylinder("Machined rotation bearing", hardware, kit.Cylinder, new Vector3(0, .04f, 0), new Vector3(2.6f, .12f, 2.6f), gold);
            Cylinder("Dark rotation race", hardware, kit.Cylinder, new Vector3(0, .10f, 0), new Vector3(2.25f, .08f, 2.25f), steel);
            int index = 0;
            foreach (int route in new[] { 1, -1 })
            {
                float angle = route * 53.130102f;
                var landings = Group(route > 0 ? "North crossing landings" : "South crossing landings", hardware);
                landings.localRotation = Quaternion.Euler(0, -angle, 0);
                foreach (int end in new[] { -1, 1 })
                {
                    float x = end * 10.7f;
                    Part("Cut stone landing", landings, kit.Box, new Vector3(x, .035f, 0), new Vector3(3.7f, .1f, 4.4f), kit.Stone);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Part("Brass landing guide", landings, kit.Block, new Vector3(x, .096f, side * 1.77f), new Vector3(3.3f, .026f, .08f), gold);
                        Part("Low landing bollard", landings, kit.Box, new Vector3(end * 12.22f, .25f, side * 1.72f), new Vector3(.2f, .4f, .2f), steel);
                    }
                    var signalRoot = Group("Landing route signal " + index, Root);
                    signalRoot.localRotation = Quaternion.Euler(0, -angle, 0);
                    landingSignals[index] = Part("Route indicator", signalRoot, kit.Block, new Vector3(end * 11.12f, .115f, 0), new Vector3(.21f, .035f, 2.65f), gold).GetComponent<Renderer>();
                    landingRoutes[index++] = route;
                }
            }
            // The same four bank-edge warning posts make the impassable channel readable.
            for (int bank = -1; bank <= 1; bank += 2)
                for (int end = -1; end <= 1; end += 2)
                    Part("River-edge warning block", hardware, kit.Box, new Vector3(bank * 3.62f, .12f, end * 13.4f), new Vector3(.18f, .24f, 1.4f), gold);
            kit.CacheModel("Bridge Gambit fixed bridge hardware", hardware);

            Deck = Group("Rotating bridge deck", Root);
            var casting = Group("Steel truss and walnut planks", Deck);
            Part("Steel bridge sill", casting, kit.Box, new Vector3(0, .10f, 0), new Vector3(20, .25f, 3.55f), steel);
            for (int plank = 0; plank < 39; plank++)
            {
                float x = -9.62f + plank * .506f;
                Part("Cross-grain walnut plank", casting, kit.Block, new Vector3(x, .245f, 0), new Vector3(.475f, .08f, 3.39f), wood);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Brass running edge", casting, kit.Block, new Vector3(0, .305f, side * 1.72f), new Vector3(19.8f, .07f, .11f), gold);
                Part("Low steel parapet", casting, kit.Box, new Vector3(0, .49f, side * 1.82f), new Vector3(19.9f, .40f, .10f), steel);
                for (int support = 0; support < 11; support++)
                {
                    float x = -9.5f + support * 1.9f;
                    Part("Riveted truss upright", casting, kit.Block, new Vector3(x, .52f, side * 1.84f), new Vector3(.12f, .57f, .13f), gold);
                    var diagonal = Part("Truss diagonal", casting, kit.Block, new Vector3(x + .45f, .43f, side * 1.84f), new Vector3(.92f, .07f, .09f), steel);
                    diagonal.localRotation = Quaternion.Euler(0, 0, side * 23);
                }
                Part("Ivory bridge-end recognition bar", casting, kit.Block, new Vector3(side * 9.63f, .293f, 0), new Vector3(.14f, .025f, 2.96f), ivory);
            }
            for (int stripe = -4; stripe <= 4; stripe++)
                Part("Centerline brass inlay", casting, kit.Block, new Vector3(stripe * 2.05f, .294f, 0), new Vector3(.82f, .018f, .075f), gold);
            kit.CacheModel("Bridge Gambit moving bridge deck", casting);

            Preview = Group("Announced destination outline", Root);
            RouteOutline = Line("Next route outline", Preview, gold, .105f, new[]
            {
                new Vector3(-10,.39f,-1.87f), new Vector3(10,.39f,-1.87f),
                new Vector3(10,.39f,1.87f), new Vector3(-10,.39f,1.87f), new Vector3(-10,.39f,-1.87f)
            });
            RouteArrow = Line("Next route direction", Preview, gold, .10f, new[]
            {
                new Vector3(-7,.41f,0),new Vector3(7,.41f,0),new Vector3(5.7f,.41f,.7f),
                new Vector3(7,.41f,0),new Vector3(5.7f,.41f,-.7f)
            });
            Preview.gameObject.SetActive(false);
            Root.gameObject.SetActive(false);
        }

        public void Render(TablefrontBridge bridge, bool reducedMotion)
        {
            Root.gameObject.SetActive(bridge != null);
            if (bridge == null) { previousBridge = null; return; }
            float blend = !Application.isPlaying || reducedMotion ? 1 : 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime);
            visualAngle = previousBridge != bridge ? bridge.AngleDegrees : Mathf.LerpAngle(visualAngle, bridge.AngleDegrees, blend);
            previousBridge = bridge;
            Deck.localRotation = Quaternion.Euler(0, -visualAngle, 0);
            bool changing = bridge.Phase != BridgePhase.Stable;
            Preview.gameObject.SetActive(changing);
            Preview.localRotation = Quaternion.Euler(0, bridge.TargetRoute == BridgeRoute.North ? -53.130102f : 53.130102f, 0);
            Material signal = bridge.ActorTeam >= 0 && bridge.ActorTeam < 2 ? signals[bridge.ActorTeam] : brass;
            RouteOutline.sharedMaterial = RouteArrow.sharedMaterial = signal;
            int activeRoute = bridge.CurrentRoute == BridgeRoute.North ? 1 : -1;
            int nextRoute = bridge.TargetRoute == BridgeRoute.North ? 1 : -1;
            for (int i = 0; i < landingSignals.Length; i++)
                landingSignals[i].sharedMaterial = changing && landingRoutes[i] == nextRoute ? signal : landingRoutes[i] == activeRoute ? brass : inactive;
        }

        public float FloorAt(Vector2 position)
        {
            float a = visualAngle * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float x = Vector2.Dot(position, along), z = Vector2.Dot(position, new Vector2(-along.y, along.x));
            return Mathf.Abs(x) <= 10.1f && Mathf.Abs(z) <= 1.87f ? DeckFloor : .055f;
        }

        public Vector3 PassengerPosition(Vector2 simulationPosition, float simulationAngle)
        {
            var point = Quaternion.Euler(0, simulationAngle - visualAngle, 0) * new Vector3(simulationPosition.x, 0, simulationPosition.y);
            point.y = DeckFloor; return point;
        }

        static Transform Group(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Transform Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var go = Group(name, parent); go.localPosition = position; go.localScale = scale;
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return go;
        }
        static void Cylinder(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
            => Part(name, parent, mesh, position, scale, material);
        static LineRenderer Line(string name, Transform parent, Material material, float width, Vector3[] points)
        {
            var line = Group(name, parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.positionCount = points.Length;
            line.SetPositions(points); line.startWidth = line.endWidth = width; line.numCapVertices = line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
    }
}
