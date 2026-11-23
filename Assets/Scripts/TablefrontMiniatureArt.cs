using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tablefront
{
    /// <summary>Shared, UV-mapped miniature geometry. The caller owns every mesh through the supplied callback.</summary>
    public sealed class TablefrontMiniatureArt
    {
        readonly Action<Mesh> ownMesh;
        readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public TablefrontMiniatureArt(Action<Mesh> ownMesh) { this.ownMesh = ownMesh ?? throw new ArgumentNullException(nameof(ownMesh)); }

        public sealed class Rig
        {
            public Transform Barrel;
            public GameObject Flash;
            internal Transform[] Wheels = Array.Empty<Transform>();
            internal Soldier[] Soldiers = Array.Empty<Soldier>();
            internal float WheelAngle;
            internal int Seed;

            /// <summary>Presentation only. The simulation remains authoritative for position, aiming and firing.</summary>
            public void Tick(float time, float speed01, float recoil, bool reducedMotion)
            {
                speed01 = Mathf.Clamp01(speed01);
                if (Barrel) Barrel.localPosition = new Vector3(0, 0, reducedMotion ? 0 : -.19f * recoil);
                if (!reducedMotion) WheelAngle = (WheelAngle + speed01 * Mathf.Min(Time.unscaledDeltaTime, .05f) * 370) % 360;
                var wheelRotation = Quaternion.Euler(reducedMotion ? 0 : WheelAngle, 0, 0);
                for (int i = 0; i < Wheels.Length; i++) Wheels[i].localRotation = wheelRotation;
                for (int i = 0; i < Soldiers.Length; i++)
                {
                    var soldier = Soldiers[i];
                    float phase = time * 12.5f + Seed * .73f + i * 2.4f;
                    float stride = reducedMotion ? 0 : Mathf.Sin(phase) * 27 * speed01;
                    soldier.LeftLeg.localRotation = Quaternion.Euler(stride - 4, 0, -2);
                    soldier.RightLeg.localRotation = Quaternion.Euler(-stride + 4, 0, 2);
                    soldier.LeftKnee.localRotation = Quaternion.Euler(5 + Mathf.Max(0, -stride) * .65f, 0, 0);
                    soldier.RightKnee.localRotation = Quaternion.Euler(5 + Mathf.Max(0, stride) * .65f, 0, 0);
                    float breath = reducedMotion ? 0 : Mathf.Sin(time * 2.3f + i * 1.7f + Seed) * .006f;
                    float bob = reducedMotion ? 0 : Mathf.Abs(Mathf.Sin(phase)) * .018f * speed01;
                    soldier.Torso.localPosition = new Vector3(0, .58f + breath + bob, 0);
                    soldier.Torso.localRotation = Quaternion.Euler(reducedMotion ? 0 : -recoil * 4 - speed01 * 3, 0,
                        reducedMotion ? 0 : Mathf.Sin(phase * .5f) * 1.5f * speed01);
                }
            }
        }

        internal sealed class Soldier
        {
            public Transform Torso, LeftLeg, RightLeg, LeftKnee, RightKnee;
        }

        struct Materials
        {
            public Material Paint, Dark, Ivory, Steel, Brass, Signal, Flash;
        }

        public Rig Build(UnitKind kind, Transform hull, Material paint, Material ink, Material ivory,
            Material steel, Material brass, Material signal, Material flash, int unitId)
        {
            var materials = new Materials { Paint = paint, Dark = ink, Ivory = ivory, Steel = steel, Brass = brass, Signal = signal, Flash = flash };
            var rig = new Rig { Seed = unitId };
            if (kind == UnitKind.Tank) Tank(hull, materials, rig);
            else if (kind == UnitKind.Scout) Scout(hull, materials, rig);
            else if (kind == UnitKind.Artillery) Artillery(hull, materials, rig);
            else if (kind == UnitKind.Gunner) Gunner(hull, materials, rig);
            else Infantry(hull, materials, rig);
            return rig;
        }

        void Gunner(Transform parent, Materials m, Rig rig)
        {
            // The broad, crew-served weapon and ammunition box make this team read
            // differently from the two slim, independently armed rifle miniatures.
            var crew = Group("Heavy weapon crew", parent);
            crew.localPosition = new Vector3(-.12f, 0, -.27f);
            Infantry(crew, m, rig);
            if (rig.Flash != null) UnityEngine.Object.Destroy(rig.Flash);
            var gun = Group("Crew-served heavy machine gun", parent);
            Cylinder("Tripod pintle", gun, new Vector3(.12f, .66f, .27f), .065f, .72f, m.Steel);
            foreach (float side in new[] { -1f, 1f })
                Link("Deployed support leg", gun, new Vector3(.12f, .64f, .27f), new Vector3(.12f + side * .47f, .19f, .65f), .065f, .065f, m.Dark);
            Link("Rear support leg", gun, new Vector3(.12f, .64f, .27f), new Vector3(.12f, .19f, -.36f), .065f, .065f, m.Dark);
            Box("Heavy receiver", gun, new Vector3(.12f, .92f, .38f), new Vector3(.27f, .23f, .61f), .035f, m.Steel);
            Box("Ivory gun shield", gun, new Vector3(.12f, .92f, .61f), new Vector3(.64f, .40f, .07f), .045f, m.Paint);
            Box("Shield recognition band", gun, new Vector3(.12f, 1.07f, .655f), new Vector3(.43f, .055f, .018f), .005f, m.Ivory);
            var barrel = Cylinder("Long finned heavy barrel", gun, new Vector3(.12f, .94f, .97f), .052f, .78f, m.Dark);
            barrel.localEulerAngles = new Vector3(90, 0, 0);
            for (int i = 0; i < 5; i++)
            {
                var fin = Cylinder("Cooling collar", gun, new Vector3(.12f, .94f, .73f + i * .085f), .072f, .035f, m.Steel);
                fin.localEulerAngles = new Vector3(90, 0, 0);
            }
            Box("Feed ammunition box", gun, new Vector3(-.23f, .82f, .33f), new Vector3(.31f, .34f, .34f), .035f, m.Brass);
            for (int i = 0; i < 5; i++) Box("Brass linked ammunition", gun, new Vector3(-.12f - i * .048f, .98f, .37f), new Vector3(.031f, .055f, .15f), .009f, m.Brass);
            rig.Flash = Ellipsoid("Heavy gun burst flash", gun, new Vector3(.12f, .94f, 1.43f), new Vector3(.20f, .20f, .35f), m.Flash).gameObject;
            rig.Flash.SetActive(false);
            Combine(gun, "gunner-weapon", false);
        }

        void Artillery(Transform parent, Materials m, Rig rig)
        {
            var body = Group("Low artillery carriage", parent);
            Box("Armored gun carriage", body, new Vector3(0, .49f, -.05f), new Vector3(1.30f, .39f, 1.75f), .15f, m.Paint);
            Box("Open fighting deck", body, new Vector3(0, .72f, -.18f), new Vector3(.98f, .10f, 1.22f), .04f, m.Dark);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Protected artillery track", body, new Vector3(side * .70f, .37f, -.12f), new Vector3(.34f, .43f, 1.95f), .17f, m.Dark);
                Box("Long track fender", body, new Vector3(side * .70f, .62f, -.12f), new Vector3(.40f, .065f, 1.94f), .025f, m.Paint);
                Box("Trail stabilizer", body, new Vector3(side * .48f, .31f, -1.06f), new Vector3(.16f, .14f, .57f), .025f, m.Steel);
                Box("Ivory artillery identity stripe", body, new Vector3(side * .70f, .665f, -.25f), new Vector3(.09f, .02f, 1.1f), .004f, m.Ivory);
                Box("Upright gun mounting cheek", body, new Vector3(side * .37f, 1.02f, .07f), new Vector3(.17f, .61f, .54f), .055f, m.Paint);
            }
            Cylinder("Exposed turntable", body, new Vector3(0, .81f, .03f), .43f, .16f, m.Brass);
            var wheels = new List<Transform>();
            foreach (float side in new[] { -1f, 1f }) for (int i = 0; i < 5; i++)
            {
                var wheel = Group("Artillery road wheel", parent); wheel.localPosition = new Vector3(side * .872f, .33f, -.82f + i * .35f);
                var tire = Cylinder("Road wheel rubber", wheel, Vector3.zero, .17f, .045f, m.Dark); tire.localEulerAngles = new Vector3(0, 0, 90);
                var hub = Cylinder("Recessed artillery hub", wheel, new Vector3(side * .03f, 0, 0), .11f, .018f, m.Steel); hub.localEulerAngles = new Vector3(0, 0, 90);
                Box("Hub radial", wheel, new Vector3(side * .045f, 0, 0), new Vector3(.014f, .14f, .024f), .006f, m.Brass);
                Combine(wheel, "artillery-wheel-" + side, true); wheels.Add(wheel);
            }
            rig.Wheels = wheels.ToArray();
            rig.Barrel = Group("Recoiling elevated artillery", parent);
            var elevated = Group("Elevated gun axis", rig.Barrel); elevated.localPosition = new Vector3(0, 1.14f, .03f); elevated.localRotation = Quaternion.Euler(-20, 0, 0);
            Box("Breech block", elevated, new Vector3(0, 0, -.24f), new Vector3(.51f, .40f, .72f), .075f, m.Paint);
            var barrelTube = Cylinder("Long artillery tube", elevated, new Vector3(0, 0, .87f), .085f, 1.90f, m.Steel); barrelTube.localEulerAngles = new Vector3(90, 0, 0);
            var sleeve = Cylinder("Artillery recoil sleeve", elevated, new Vector3(0, 0, .28f), .145f, .73f, m.Paint); sleeve.localEulerAngles = new Vector3(90, 0, 0);
            Box("Double baffle muzzle brake", elevated, new Vector3(0, 0, 1.86f), new Vector3(.31f, .25f, .30f), .035f, m.Dark);
            Box("Muzzle brass edge", elevated, new Vector3(0, .115f, 1.86f), new Vector3(.29f, .028f, .29f), .005f, m.Brass);
            foreach (float side in new[] { -1f, 1f })
                Link("Hydraulic elevation strut", rig.Barrel, new Vector3(side * .28f, .80f, -.34f), new Vector3(side * .24f, 1.27f, .37f), .075f, .075f, m.Steel);
            rig.Flash = Ellipsoid("Artillery discharge", elevated, new Vector3(0, 0, 2.10f), new Vector3(.40f, .40f, .64f), m.Flash).gameObject;
            rig.Flash.SetActive(false);
            Combine(body, "artillery-carriage", true);
            Combine(elevated, "artillery-elevated-gun", false);
        }

        void Tank(Transform parent, Materials m, Rig rig)
        {
            var body = Group("Cast armor and fittings", parent);
            Add("Sloped cast hull", body, Hull("tank hull", new[] {
                Ring(.32f, .53f, .79f, .12f, -.04f), Ring(.55f, .61f, .83f, .14f, -.04f),
                Ring(.80f, .48f, .57f, .12f, -.15f), Ring(.84f, .45f, .54f, .10f, -.15f) }), Vector3.zero, Vector3.one, m.Paint);
            Box("Upper deck seam", body, new Vector3(0, .851f, -.15f), new Vector3(.80f, .035f, .90f), .014f, m.Dark);
            Box("Upper deck plate", body, new Vector3(0, .873f, -.15f), new Vector3(.76f, .04f, .86f), .018f, m.Paint);
            Box("Recognition enamel", body, new Vector3(-.31f, .900f, -.18f), new Vector3(.10f, .012f, .82f), .004f, m.Ivory);
            Box("Glacis recognition stripe", body, new Vector3(-.31f, .693f, .615f), new Vector3(.10f, .018f, .26f), .006f, m.Ivory).localEulerAngles = new Vector3(49, 0, 0);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * .66f;
                Box("Protected continuous track", body, new Vector3(x, .40f, -.02f), new Vector3(.35f, .48f, 1.78f), .19f, m.Dark);
                Box("Rolled mudguard", body, new Vector3(x, .676f, -.015f), new Vector3(.43f, .075f, 1.78f), .033f, m.Paint);
                Box("Mudguard ivory edge", body, new Vector3(side * .844f, .69f, -.05f), new Vector3(.035f, .025f, 1.52f), .01f, m.Ivory);
                for (int i = 0; i < 9; i++)
                    Box("Track shoe", body, new Vector3(x, .65f, -.73f + i * .183f), new Vector3(.36f, .024f, .079f), .009f, m.Steel);
                for (int i = 0; i < 3; i++)
                {
                    Box("Side skirt armor", body, new Vector3(side * .863f, .57f, -.54f + i * .51f), new Vector3(.045f, .22f, .45f), .025f, m.Paint);
                    Box("Skirt fastener", body, new Vector3(side * .894f, .63f, -.54f + i * .51f), new Vector3(.026f, .045f, .05f), .008f, m.Brass);
                }
            }
            var wheelList = new List<Transform>(12);
            foreach (float side in new[] { -1f, 1f }) for (int i = 0; i < 6; i++)
            {
                var wheel = Group("Road wheel pivot", parent); wheel.localPosition = new Vector3(side * .855f, .35f, -.66f + i * .263f);
                var tire = Cylinder("Rubber road wheel", wheel, Vector3.zero, .174f, .06f, m.Dark); tire.localEulerAngles = new Vector3(0, 0, 90);
                var hub = Cylinder("Pressed wheel hub", wheel, new Vector3(side * .035f, 0, 0), .123f, .019f, m.Steel); hub.localEulerAngles = new Vector3(0, 0, 90);
                var pin = Cylinder("Brass wheel pin", wheel, new Vector3(side * .049f, 0, 0), .043f, .016f, m.Brass); pin.localEulerAngles = new Vector3(0, 0, 90);
                Box("Hub spoke", wheel, new Vector3(side * .051f, 0, 0), new Vector3(.014f, .17f, .027f), .008f, m.Dark);
                Combine(wheel, "tank-wheel-" + side, true);
                wheelList.Add(wheel);
            }
            rig.Wheels = wheelList.ToArray();
            Cylinder("Turret ring", body, new Vector3(0, .91f, -.05f), .47f, .06f, m.Dark);
            Add("Cast turret", body, Hull("tank turret", new[] { Ring(.94f, .46f, .48f, .17f, -.045f),
                Ring(1.16f, .39f, .40f, .15f, -.10f), Ring(1.21f, .30f, .31f, .12f, -.12f) }), Vector3.zero, Vector3.one, m.Paint);
            Cylinder("Commander hatch gasket", body, new Vector3(-.12f, 1.223f, -.20f), .173f, .017f, m.Dark);
            Cylinder("Commander hatch", body, new Vector3(-.12f, 1.242f, -.20f), .145f, .024f, m.Ivory);
            Box("Hatch handle", body, new Vector3(-.12f, 1.268f, -.20f), new Vector3(.125f, .027f, .025f), .009f, m.Brass);
            Box("Periscope surround", body, new Vector3(.23f, 1.177f, .18f), new Vector3(.18f, .10f, .17f), .022f, m.Dark);
            Box("Periscope glass", body, new Vector3(.23f, 1.195f, .271f), new Vector3(.12f, .045f, .015f), .005f, m.Signal);
            for (int i = 0; i < 5; i++) Box("Engine cooling louvre", body, new Vector3(0, .894f, -.40f - i * .055f), new Vector3(.34f, .018f, .022f), .005f, m.Dark);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Headlamp housing", body, new Vector3(side * .43f, .62f, .749f), new Vector3(.14f, .12f, .105f), .035f, m.Dark);
                Box("Headlamp glass", body, new Vector3(side * .43f, .626f, .809f), new Vector3(.095f, .07f, .018f), .018f, m.Ivory);
                var tow = Cylinder("Tow eye", body, new Vector3(side * .35f, .427f, .803f), .052f, .06f, m.Brass); tow.localEulerAngles = new Vector3(90, 0, 0);
            }
            Box("Antenna foot", body, new Vector3(-.29f, 1.16f, -.39f), new Vector3(.075f, .10f, .075f), .02f, m.Dark);
            Cylinder("Flexible radio aerial", body, new Vector3(-.29f, 1.43f, -.39f), .011f, .48f, m.Dark);
            rig.Barrel = Group("Recoiling cannon", parent);
            Box("Gun mantlet", rig.Barrel, new Vector3(0, 1.055f, .39f), new Vector3(.36f, .22f, .27f), .075f, m.Paint);
            var barrel = Cylinder("Turned steel cannon", rig.Barrel, new Vector3(0, 1.055f, .98f), .067f, 1.03f, m.Dark); barrel.localEulerAngles = new Vector3(90, 0, 0);
            var sleeve = Cylinder("Gun sleeve", rig.Barrel, new Vector3(0, 1.055f, .70f), .092f, .40f, m.Paint); sleeve.localEulerAngles = new Vector3(90, 0, 0);
            Box("Muzzle brake", rig.Barrel, new Vector3(0, 1.055f, 1.523f), new Vector3(.22f, .17f, .25f), .035f, m.Steel);
            Box("Muzzle bore", rig.Barrel, new Vector3(0, 1.055f, 1.653f), new Vector3(.112f, .075f, .012f), .014f, m.Dark);
            rig.Flash = Ellipsoid("Cannon flash", rig.Barrel, new Vector3(0, 1.055f, 1.80f), new Vector3(.28f, .28f, .57f), m.Flash).gameObject;
            rig.Flash.SetActive(false);
            Combine(body, "tank-body", true);
            Combine(rig.Barrel, "tank-cannon", false);
        }

        void Scout(Transform parent, Materials m, Rig rig)
        {
            var body = Group("Recon armor and fittings", parent);
            Box("Chassis frame", body, new Vector3(0, .35f, -.02f), new Vector3(.77f, .14f, 1.38f), .06f, m.Dark);
            Add("Sculpted recon hull", body, Hull("scout hull", new[] { Ring(.43f, .43f, .67f, .09f, 0),
                Ring(.64f, .48f, .69f, .12f, 0), Ring(.78f, .37f, .49f, .11f, -.13f) }), Vector3.zero, Vector3.one, m.Paint);
            Box("Sloped hood", body, new Vector3(0, .77f, .39f), new Vector3(.73f, .13f, .46f), .055f, m.Paint).localEulerAngles = new Vector3(12, 0, 0);
            Box("Ivory hood stripe", body, new Vector3(-.22f, .843f, .38f), new Vector3(.09f, .018f, .39f), .005f, m.Ivory).localEulerAngles = new Vector3(12, 0, 0);
            Add("Armored crew cabin", body, Hull("scout cabin", new[] { Ring(.73f, .36f, .33f, .06f, -.21f),
                Ring(1.055f, .29f, .25f, .055f, -.25f), Ring(1.10f, .28f, .24f, .055f, -.25f) }), Vector3.zero, Vector3.one, m.Paint);
            Box("Dark armored windshield", body, new Vector3(0, .934f, .034f), new Vector3(.57f, .18f, .025f), .026f, m.Dark).localEulerAngles = new Vector3(11, 0, 0);
            Box("Windshield glint", body, new Vector3(-.13f, .987f, .059f), new Vector3(.19f, .028f, .008f), .006f, m.Steel).localEulerAngles = new Vector3(11, 0, 0);
            Box("Windshield divider", body, new Vector3(0, .935f, .060f), new Vector3(.028f, .18f, .025f), .008f, m.Paint);
            Box("Ivory roof plate", body, new Vector3(0, 1.12f, -.25f), new Vector3(.62f, .065f, .56f), .028f, m.Ivory);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Side armored window", body, new Vector3(side * .328f, .945f, -.27f), new Vector3(.022f, .145f, .28f), .025f, m.Dark);
                Box("Side access handle", body, new Vector3(side * .418f, .72f, -.28f), new Vector3(.035f, .025f, .12f), .008f, m.Brass);
                foreach (float z in new[] { -.48f, .45f })
                    Box("Flared wheel arch", body, new Vector3(side * .49f, .65f, z), new Vector3(.25f, .075f, .55f), .033f, m.Paint);
                Box("Headlamp housing", body, new Vector3(side * .29f, .655f, .665f), new Vector3(.16f, .13f, .08f), .03f, m.Dark);
                Box("Round lamp glass", body, new Vector3(side * .29f, .66f, .713f), new Vector3(.105f, .08f, .02f), .025f, m.Ivory);
            }
            Box("Front brush guard", body, new Vector3(0, .455f, .745f), new Vector3(.88f, .09f, .11f), .025f, m.Dark);
            Box("Rear stowage rack", body, new Vector3(0, .81f, -.60f), new Vector3(.54f, .14f, .17f), .04f, m.Steel);
            Cylinder("Radio aerial", body, new Vector3(-.27f, 1.38f, -.44f), .011f, .46f, m.Dark);
            var wheels = new List<Transform>(4);
            foreach (float side in new[] { -1f, 1f }) foreach (float z in new[] { -.48f, .45f })
            {
                var wheel = Group("Recon wheel pivot", parent); wheel.localPosition = new Vector3(side * .52f, .365f, z);
                var tire = Cylinder("Molded rubber tire", wheel, Vector3.zero, .235f, .20f, m.Dark); tire.localEulerAngles = new Vector3(0, 0, 90);
                var rim = Cylinder("Painted wheel rim", wheel, new Vector3(side * .105f, 0, 0), .16f, .027f, m.Paint); rim.localEulerAngles = new Vector3(0, 0, 90);
                var hub = Cylinder("Wheel center cap", wheel, new Vector3(side * .125f, 0, 0), .065f, .035f, m.Brass); hub.localEulerAngles = new Vector3(0, 0, 90);
                for (int spoke = 0; spoke < 5; spoke++)
                {
                    float a = spoke * Mathf.PI * .4f;
                    var vent = Ellipsoid("Rim inset", wheel, new Vector3(side * .125f, Mathf.Sin(a) * .108f, Mathf.Cos(a) * .108f), new Vector3(.012f, .047f, .047f), m.Dark);
                }
                Combine(wheel, "scout-wheel-" + side, true); wheels.Add(wheel);
            }
            rig.Wheels = wheels.ToArray();
            rig.Barrel = Group("Recoiling recon gun", parent);
            Cylinder("Recon turret", rig.Barrel, new Vector3(.08f, 1.19f, -.18f), .18f, .12f, m.Paint);
            var gun = Cylinder("Light gun barrel", rig.Barrel, new Vector3(.08f, 1.205f, .23f), .032f, .63f, m.Dark); gun.localEulerAngles = new Vector3(90, 0, 0);
            Box("Gun optics", rig.Barrel, new Vector3(.16f, 1.23f, -.03f), new Vector3(.07f, .06f, .10f), .015f, m.Signal);
            rig.Flash = Ellipsoid("Recon muzzle flash", rig.Barrel, new Vector3(.08f, 1.205f, .61f), new Vector3(.18f, .18f, .30f), m.Flash).gameObject;
            rig.Flash.SetActive(false); Combine(body, "scout-body", true); Combine(rig.Barrel, "scout-gun", false);
        }

        void Infantry(Transform parent, Materials m, Rig rig)
        {
            rig.Soldiers = new Soldier[2];
            for (int i = 0; i < 2; i++)
            {
                var soldierRoot = Group("Painted rifle miniature", parent);
                soldierRoot.localPosition = new Vector3(i == 0 ? -.27f : .29f, .16f, i == 0 ? .17f : -.23f);
                var s = new Soldier(); rig.Soldiers[i] = s;
                s.Torso = Group("Articulated torso", soldierRoot); s.Torso.localPosition = new Vector3(0, .58f, 0);
                Add("Tailored armor vest", s.Torso, Hull("infantry torso", new[] { Ring(-.18f, .145f, .115f, .035f, 0),
                    Ring(.10f, .205f, .14f, .05f, 0), Ring(.19f, .16f, .125f, .045f, 0) }), Vector3.zero, Vector3.one, m.Paint);
                Box("Ivory breastplate", s.Torso, new Vector3(0, .035f, .143f), new Vector3(.285f, .24f, .065f), .035f, m.Ivory);
                Box("Chest enamel inset", s.Torso, new Vector3(0, .043f, .180f), new Vector3(.16f, .13f, .012f), .013f, m.Paint);
                Box("Utility belt", s.Torso, new Vector3(0, -.14f, .01f), new Vector3(.32f, .055f, .25f), .014f, m.Dark);
                Box("Belt buckle", s.Torso, new Vector3(-.05f, -.14f, .144f), new Vector3(.055f, .055f, .025f), .008f, m.Brass);
                Box("Ammo pouch", s.Torso, new Vector3(-.14f, -.07f, .15f), new Vector3(.08f, .105f, .055f), .015f, m.Steel);
                Ellipsoid("Helmet underliner", s.Torso, new Vector3(0, .285f, .02f), new Vector3(.265f, .21f, .25f), m.Dark);
                Ellipsoid("Smooth cast helmet", s.Torso, new Vector3(0, .353f, .005f), new Vector3(.365f, .275f, .345f), m.Paint);
                Box("Helmet brow rim", s.Torso, new Vector3(0, .322f, .156f), new Vector3(.29f, .039f, .09f), .015f, m.Paint);
                Box("Dark protective visor", s.Torso, new Vector3(0, .285f, .161f), new Vector3(.25f, .073f, .039f), .018f, m.Dark);
                Box("Visor reflection", s.Torso, new Vector3(-.065f, .306f, .184f), new Vector3(.085f, .012f, .008f), .003f, m.Steel);
                Box("Helmet recognition band", s.Torso, new Vector3(-.075f, .477f, .01f), new Vector3(.055f, .014f, .14f), .005f, m.Ivory);
                foreach (float side in new[] { -1f, 1f })
                {
                    Box("Rounded shoulder armor", s.Torso, new Vector3(side * .223f, .10f, 0), new Vector3(.165f, .17f, .21f), .06f, m.Paint);
                    Box("Shoulder ivory marking", s.Torso, new Vector3(side * .253f, .166f, .005f), new Vector3(.08f, .026f, .12f), .012f, m.Ivory);
                }
                Link("Left upper arm", s.Torso, new Vector3(-.22f, .07f, .015f), new Vector3(-.22f, -.08f, .17f), .115f, .11f, m.Paint);
                Link("Left supporting forearm", s.Torso, new Vector3(-.22f, -.08f, .17f), new Vector3(.09f, -.015f, .39f), .10f, .10f, m.Paint);
                Link("Right upper arm", s.Torso, new Vector3(.22f, .07f, .01f), new Vector3(.27f, -.10f, .19f), .115f, .11f, m.Paint);
                Link("Right aiming forearm", s.Torso, new Vector3(.27f, -.10f, .19f), new Vector3(.15f, .01f, .30f), .10f, .10f, m.Paint);
                Ellipsoid("Left glove", s.Torso, new Vector3(.07f, -.005f, .39f), new Vector3(.11f, .10f, .12f), m.Dark);
                Ellipsoid("Right glove", s.Torso, new Vector3(.145f, -.005f, .30f), new Vector3(.10f, .10f, .11f), m.Dark);
                Box("Rifle receiver", s.Torso, new Vector3(.14f, .035f, .37f), new Vector3(.11f, .13f, .43f), .018f, m.Dark);
                Box("Rifle forward rail", s.Torso, new Vector3(.14f, .082f, .45f), new Vector3(.055f, .025f, .29f), .007f, m.Steel);
                Box("Rifle stock", s.Torso, new Vector3(.14f, .025f, .12f), new Vector3(.095f, .12f, .20f), .025f, m.Dark);
                Box("Rifle magazine", s.Torso, new Vector3(.14f, -.075f, .32f), new Vector3(.075f, .16f, .09f), .013f, m.Steel).localEulerAngles = new Vector3(12, 0, 0);
                var barrel = Cylinder("Rifle barrel", s.Torso, new Vector3(.14f, .052f, .665f), .026f, .27f, m.Dark); barrel.localEulerAngles = new Vector3(90, 0, 0);
                Box("Rifle sights", s.Torso, new Vector3(.14f, .123f, .36f), new Vector3(.05f, .055f, .085f), .008f, m.Signal);
                Box("Field backpack", s.Torso, new Vector3(0, -.005f, -.205f), new Vector3(.28f, .34f, .17f), .045f, m.Steel);
                foreach (float x in new[] { -.085f, .085f }) Box("Backpack straps", s.Torso, new Vector3(x, .005f, -.300f), new Vector3(.032f, .27f, .018f), .006f, m.Dark);
                if (i == 0)
                {
                    rig.Flash = Ellipsoid("Rifle muzzle flash", s.Torso, new Vector3(.14f, .052f, .85f), new Vector3(.16f, .16f, .28f), m.Flash).gameObject;
                    rig.Flash.SetActive(false);
                }
                for (int leg = 0; leg < 2; leg++)
                {
                    float side = leg == 0 ? -1 : 1;
                    var thigh = Group("Hip pivot", soldierRoot); thigh.localPosition = new Vector3(side * .105f, .46f, -.015f);
                    Box("Sculpted thigh", thigh, new Vector3(0, -.10f, 0), new Vector3(.145f, .215f, .16f), .038f, m.Paint);
                    var knee = Group("Knee pivot", thigh); knee.localPosition = new Vector3(0, -.195f, 0);
                    Box("Kneepad", knee, new Vector3(0, -.015f, .07f), new Vector3(.13f, .12f, .065f), .027f, m.Ivory);
                    Box("Lower leg", knee, new Vector3(0, -.10f, -.005f), new Vector3(.115f, .18f, .13f), .029f, m.Paint);
                    Box("Cast leather boot", knee, new Vector3(0, -.18f, .035f), new Vector3(.15f, .115f, .25f), .035f, m.Dark);
                    if (leg == 0) { s.LeftLeg = thigh; s.LeftKnee = knee; }
                    else { s.RightLeg = thigh; s.RightKnee = knee; }
                }
                Combine(s.Torso, "rifle-torso", false);
            }
        }

        Transform Link(string name, Transform parent, Vector3 a, Vector3 b, float width, float depth, Material material)
        {
            var link = Box(name, parent, (a + b) * .5f, new Vector3(width, Vector3.Distance(a, b), depth), Mathf.Min(width, depth) * .28f, material);
            link.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return link;
        }
        Transform Box(string name, Transform parent, Vector3 position, Vector3 size, float bevel, Material material)
            => Add(name, parent, BevelBox(size, bevel), position, Vector3.one, material);
        Transform Cylinder(string name, Transform parent, Vector3 position, float radius, float height, Material material)
            => Add(name, parent, CylinderMesh(), position, new Vector3(radius * 2, height, radius * 2), material);
        Transform Ellipsoid(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => Add(name, parent, SphereMesh(), position, size, material);
        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        static Transform Add(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var t = Group(name, parent); t.localPosition = position; t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; return t;
        }
        Mesh Cache(string key, Func<Mesh> create)
        {
            if (meshes.TryGetValue(key, out var result)) return result;
            result = create(); result.name = "Tablefront miniature " + key; meshes.Add(key, result); ownMesh(result); return result;
        }
        static string Key(float value) => Mathf.RoundToInt(value * 10000).ToString();

        Mesh BevelBox(Vector3 size, float radius)
        {
            radius = Mathf.Clamp(radius, .001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .49f);
            string key = "bevel-" + Key(size.x) + "-" + Key(size.y) + "-" + Key(size.z) + "-" + Key(radius);
            return Cache(key, () =>
            {
                var b = new MeshBuilder(); Vector3 half = size * .5f, core = half - Vector3.one * radius;
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                Vector3[] us = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
                Vector3[] vs = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
                for (int face = 0; face < 6; face++)
                {
                    var n = normals[face]; var u = us[face]; var v = vs[face];
                    float hN = Vector3.Dot(Abs(n), half), hU = Vector3.Dot(Abs(u), half), hV = Vector3.Dot(Abs(v), half);
                    float[] xs = { -hU, -hU + radius * .293f, -hU + radius, hU - radius, hU - radius * .293f, hU };
                    float[] ys = { -hV, -hV + radius * .293f, -hV + radius, hV - radius, hV - radius * .293f, hV };
                    int start = b.Vertices.Count;
                    for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++)
                    {
                        Vector3 raw = n * hN + u * xs[x] + v * ys[y];
                        Vector3 close = new Vector3(Mathf.Clamp(raw.x, -core.x, core.x), Mathf.Clamp(raw.y, -core.y, core.y), Mathf.Clamp(raw.z, -core.z, core.z));
                        Vector3 normal = (raw - close).normalized;
                        b.Vertex(close + normal * radius, normal, new Vector2(xs[x] / (hU * 2) + .5f, ys[y] / (hV * 2) + .5f));
                    }
                    for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                    { int i = start + y * 6 + x; b.Triangle(i, i + 1, i + 7); b.Triangle(i, i + 7, i + 6); }
                }
                return b.Finish();
            });
        }
        static Vector3 Abs(Vector3 p) => new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));

        static Vector3[] Ring(float height, float halfWidth, float halfLength, float cut, float zOffset)
        {
            return new[] {
                new Vector3(-halfWidth + cut,height,-halfLength + zOffset), new Vector3(halfWidth-cut,height,-halfLength+zOffset),
                new Vector3(halfWidth,height,-halfLength+cut+zOffset),new Vector3(halfWidth,height,halfLength-cut+zOffset),
                new Vector3(halfWidth-cut,height,halfLength+zOffset),new Vector3(-halfWidth+cut,height,halfLength+zOffset),
                new Vector3(-halfWidth,height,halfLength-cut+zOffset),new Vector3(-halfWidth,height,-halfLength+cut+zOffset)};
        }
        Mesh Hull(string key, Vector3[][] rings)
        {
            return Cache(key, () =>
            {
                var b = new MeshBuilder();
                for (int level = 0; level < rings.Length - 1; level++) for (int side = 0; side < 8; side++)
                {
                    int next = (side + 1) % 8;
                    b.Quad(rings[level][side], rings[level + 1][side], rings[level + 1][next], rings[level][next]);
                }
                Vector3 top = Vector3.zero, bottom = Vector3.zero;
                foreach (var p in rings[rings.Length - 1]) top += p / 8;
                foreach (var p in rings[0]) bottom += p / 8;
                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    b.Face(top, rings[rings.Length - 1][next], rings[rings.Length - 1][i]);
                    b.Face(bottom, rings[0][i], rings[0][next]);
                }
                return b.Finish();
            });
        }
        Mesh CylinderMesh()
        {
            return Cache("turned-cylinder", () =>
            {
                var b = new MeshBuilder(); const int segments = 24;
                // Rim bevel catches a narrow highlight rather than ending in a knife edge.
                float[] ys = { -.5f, -.465f, .465f, .5f }; float[] radii = { .445f, .5f, .5f, .445f };
                for (int row = 0; row < 4; row++) for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments; var outwards = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var normal = row == 0 ? (outwards + Vector3.down).normalized : row == 3 ? (outwards + Vector3.up).normalized : outwards;
                    b.Vertex(outwards * radii[row] + Vector3.up * ys[row], normal, new Vector2((float)i / segments, row / 3f));
                }
                for (int row = 0; row < 3; row++) for (int i = 0; i < segments; i++)
                { int n = row * (segments + 1) + i; b.Triangle(n, n + segments + 1, n + segments + 2); b.Triangle(n, n + segments + 2, n + 1); }
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments, c = (i + 1) * Mathf.PI * 2 / segments;
                    var p = new Vector3(Mathf.Cos(a) * .445f, 0, Mathf.Sin(a) * .445f);
                    var q = new Vector3(Mathf.Cos(c) * .445f, 0, Mathf.Sin(c) * .445f);
                    b.Face(Vector3.up * .5f, q + Vector3.up * .5f, p + Vector3.up * .5f);
                    b.Face(Vector3.down * .5f, p + Vector3.down * .5f, q + Vector3.down * .5f);
                }
                return b.Finish();
            });
        }
        Mesh SphereMesh()
        {
            return Cache("cast-ellipsoid", () =>
            {
                var b = new MeshBuilder(); const int rows = 12, columns = 20;
                for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
                {
                    float latitude = y * Mathf.PI / rows, longitude = x * Mathf.PI * 2 / columns;
                    var n = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    b.Vertex(n * .5f, n, new Vector2((float)x / columns, 1 - (float)y / rows));
                }
                for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
                { int n = y * (columns + 1) + x; b.Triangle(n, n + 1, n + columns + 2); b.Triangle(n, n + columns + 2, n + columns + 1); }
                return b.Finish();
            });
        }

        // Only immutable fittings are merged. Animated wheel, limb and gun transforms stay independent.
        void Combine(Transform root, string kind, bool includeInactive)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.gameObject.activeSelf || !filter.sharedMesh) continue;
                var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var list)) { list = new List<CombineInstance>(); groups.Add(material, list); }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            foreach (var pair in groups)
            {
                string key = "combined-" + kind + "-" + pair.Key.GetEntityId();
                Mesh combined = Cache(key, () => { var mesh = new Mesh(); mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds(); return mesh; });
                Add("Combined " + pair.Key.name, root, combined, Vector3.zero, Vector3.one, pair.Key);
            }
            foreach (var filter in filters)
            {
                if (!filter.gameObject.activeSelf) continue;
                filter.gameObject.SetActive(false); UnityEngine.Object.Destroy(filter.gameObject);
            }
        }

        sealed class MeshBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> triangles = new List<int>();
            public void Vertex(Vector3 v, Vector3 normal, Vector2 texture) { Vertices.Add(v); normals.Add(normal); uv.Add(texture); }
            public void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = Vertices.Count; var normal = Vector3.Cross(b - a, c - a).normalized;
                Vertex(a, normal, Vector2.zero); Vertex(b, normal, Vector2.up); Vertex(c, normal, Vector2.one); Vertex(d, normal, Vector2.right);
                Triangle(start, start + 1, start + 2); Triangle(start, start + 2, start + 3);
            }
            public void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int start = Vertices.Count; var normal = Vector3.Cross(b - a, c - a).normalized;
                Vertex(a, normal, new Vector2(a.x, a.z)); Vertex(b, normal, new Vector2(b.x, b.z)); Vertex(c, normal, new Vector2(c.x, c.z)); Triangle(start, start + 1, start + 2);
            }
            public Mesh Finish()
            {
                var mesh = new Mesh(); mesh.SetVertices(Vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
            }
        }
    }
}
