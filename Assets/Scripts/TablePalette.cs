using UnityEngine;

namespace Tablefront
{
    /// <summary>Warm miniature wargame, enamel factions on a walnut campaign table.</summary>
    public static class TablePalette
    {
        public static readonly Color Background = Hex("101B1E");
        public static readonly Color Surface = Hex("213236");
        public static readonly Color Text = Hex("F6EED8");
        public static readonly Color Muted = Hex("B2C1B9");
        public static readonly Color Gold = Hex("DDBB73");
        public static readonly Color Terrain = Hex("829071");
        public static readonly Color Ink = Hex("172326");
        public static readonly Color Walnut = Hex("3D2A23");
        public static readonly Color WoodEdge = Hex("6E4931");
        public static readonly Color Sand = Hex("B4AF8C");
        public static readonly Color Road = Hex("C0B491");
        public static readonly Color Stone = Hex("79837C");
        public static readonly Color Foliage = Hex("455F4C");
        public static readonly Color FoliageLight = Hex("6F845A");
        public static readonly Color WarmLight = Hex("FFF0D3");
        public static readonly Color SkyBounce = Hex("B3C9D6");
        public static readonly Color GroundBounce = Hex("514138");
        public static readonly Color Muzzle = Hex("FFE3A0");
        static readonly Color[] Factions = { Hex("54BAE4"), Hex("EEA05A"), Hex("BD92DF") };
        public static Color Team(int team) => team >= 0 && team < Factions.Length ? Factions[team] : Gold;
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
    }
}
