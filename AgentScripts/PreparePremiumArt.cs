using System;
using UnityEditor;
using UnityEngine;

/// <summary>Retains the exact runtime Lit surface variants in the Windows player.</summary>
public static class PreparePremiumArt
{
    public static string Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before preparing build resources.");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/PremiumArt")) AssetDatabase.CreateFolder("Assets/Resources", "PremiumArt");
        Texture2D Texture(string name, Color color)
        {
            string path = "Assets/Resources/PremiumArt/" + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { name = name };
                texture.SetPixels(new[] { color, color, color, color }); texture.Apply();
                AssetDatabase.CreateAsset(texture, path);
            }
            return texture;
        }
        var normal = Texture("PremiumFlatNormal", new Color(.5f, .5f, 1, 1));
        var surface = Texture("PremiumSurfaceMask", new Color(0, 1, 0, .75f));
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("The URP Lit shader is missing.");
        foreach (bool occlusion in new[] { false, true })
        {
            string path = "Assets/Resources/PremiumArt/" + (occlusion ? "GroundReference" : "PaintReference") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.enableInstancing = true;
            material.shaderKeywords = occlusion ? new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" } : new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP" };
            material.SetTexture("_BumpMap", normal); material.SetTexture("_MetallicGlossMap", surface);
            if (occlusion) material.SetTexture("_OcclusionMap", surface);
            material.SetColor("_BaseColor", new Color(.4f, .55f, .5f)); material.SetFloat("_Smoothness", .6f);
            EditorUtility.SetDirty(material);
        }
        PlayerSettings.bundleVersion = "0.6.0";
        Tablefront.TablefrontGraphics.Configure();
        AssetDatabase.SaveAssets();
        return "Premium surface variants retained: normal+metallic/smoothness, with/without occlusion; build version 0.6.0.";
    }
}
