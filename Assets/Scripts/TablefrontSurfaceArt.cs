using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tablefront
{
    /// <summary>Shared, deterministic miniature surface maps; generated once per world.</summary>
    public sealed class TablefrontSurfaceArt
    {
        public enum Finish { Enamel, Rubber, Walnut, Brass, Stone }
        sealed class Maps { public Texture2D Color, Normal, Mask; }
        readonly Dictionary<Finish, Maps> cache = new Dictionary<Finish, Maps>();
        readonly Action<Texture2D> own;
        public TablefrontSurfaceArt(Action<Texture2D> ownTexture) { own = ownTexture; }

        public void Apply(Material material, Finish finish)
        {
            if (!cache.TryGetValue(finish, out var maps)) { maps = Make(finish); cache.Add(finish, maps); }
            material.SetTexture("_BaseMap", maps.Color);
            material.SetTexture("_BumpMap", maps.Normal);
            material.SetTexture("_MetallicGlossMap", maps.Mask);
            material.SetFloat("_BumpScale", finish == Finish.Stone ? .65f : finish == Finish.Rubber ? .38f : .24f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", finish == Finish.Enamel ? .68f : finish == Finish.Brass ? .72f : finish == Finish.Walnut ? .48f : .3f);
        }

        Maps Make(Finish finish)
        {
            int size = finish == Finish.Walnut ? 1024 : 256;
            var heights = new float[size * size];
            var color = new Color[size * size]; var normal = new Color[size * size]; var mask = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float grain = Mathf.PerlinNoise(u * 22 + 41, v * 22 + 17);
                float fine = Hash(x, y);
                float height, shade, gloss, metal = 0;
                if (finish == Finish.Walnut)
                {
                    float bend = Mathf.PerlinNoise(u * 2.2f + 12, v * 1.7f) * .65f;
                    float rings = Mathf.Sin((u * 38 + bend + Mathf.Sin(v * 5) * .16f) * Mathf.PI * 2);
                    float fibres = Mathf.Sin((u * 320 + bend * 4) * Mathf.PI * 2);
                    height = rings * .32f + fibres * .045f + fine * .025f;
                    shade = .77f + rings * .11f + grain * .11f + fibres * .025f;
                    gloss = .67f + grain * .22f;
                }
                else if (finish == Finish.Brass)
                {
                    float brushing = Mathf.PerlinNoise(u * 180 + 3, v * 5 + 1);
                    height = brushing * .045f + fine * .015f;
                    shade = .85f + grain * .13f;
                    gloss = .67f + brushing * .25f; metal = 1;
                }
                else if (finish == Finish.Stone)
                {
                    float strata = Mathf.PerlinNoise(u * 7 + 61, v * 12 + 28);
                    height = grain * .45f + strata * .5f + fine * .09f;
                    shade = .77f + strata * .16f + grain * .10f;
                    gloss = .18f + grain * .2f;
                }
                else if (finish == Finish.Rubber)
                {
                    float grooves = Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 22)), 12);
                    height = fine * .06f - grooves * .2f;
                    shade = .91f + grain * .08f - grooves * .08f;
                    gloss = .36f + grain * .13f;
                }
                else
                {
                    // Small orange-peel variations affect specular response, not noisy paint color.
                    height = grain * .025f + fine * .012f;
                    shade = .97f + grain * .03f;
                    gloss = .73f + grain * .22f;
                }
                int i = y * size + x;
                heights[i] = height; color[i] = new Color(shade, shade, shade, 1);
                mask[i] = new Color(metal, 1, 0, gloss);
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                var n = new Vector3(-dx * 2, -dy * 2, 1).normalized;
                normal[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            string name = "Tablefront " + finish;
            return new Maps { Color = Texture(name + " color", size, color, false), Normal = Texture(name + " normal", size, normal, true), Mask = Texture(name + " surface", size, mask, true) };
        }

        Texture2D Texture(string name, int size, Color[] pixels, bool linear)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            texture.SetPixels(pixels); texture.Apply(true, true); own(texture); return texture;
        }
        static float Hash(int x, int y)
        {
            unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + 71237); h = (h ^ (h >> 13)) * 1274126177; return (h & 65535) / 65535f; }
        }
    }
}
