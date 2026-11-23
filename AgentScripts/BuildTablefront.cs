using System;
using System.IO;
using System.Linq;
using Tablefront;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildTablefront
{
    public static string Prepare()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (!font) { TMP_PackageResourceImporter.ImportResources(true, false, false); return "TMP Essentials import queued. Run Prepare after import completes."; }
        foreach (string shaderName in new[] {"Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit"})
        {
            var shader = Shader.Find(shaderName);
            if (!shader) throw new Exception("Required shader missing: " + shaderName);
            string path = "Assets/Resources/" + shaderName.Split('/').Last() + "Reference.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.color = new Color(.2f, .5f, .6f);
            mat.enableInstancing = true;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(.1f, .3f, .4f));
            EditorUtility.SetDirty(mat);
        }
        // Resources material references retain the runtime shaders without including every Lit variant.
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (!pipeline) throw new Exception("URP is required");
        TablefrontGraphics.Configure();
        EditorUtility.SetDirty(pipeline);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Tablefront match director").AddComponent<TablefrontGame>();
        PlayerSettings.companyName = "Lumen Workshop"; PlayerSettings.productName = "Tablefront";
        PlayerSettings.bundleVersion = "0.5.0";
        // Avoid Windows.Gaming.Input startup stalls on this keyboard/mouse desktop game.
        PlayerSettings.windowsGamepadBackendHint = WindowsGamepadBackendHint.WindowsGamepadBackendHintXInput;
        PlayerSettings.defaultScreenWidth = 2560; PlayerSettings.defaultScreenHeight = 1440;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.resizableWindow = true;
        PlayerSettings.forceSingleInstance = true;
        PlayerSettings.runInBackground = false; PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/Tablefront.png");
        if (icon) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] {icon});
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Tablefront.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene("Assets/Scenes/Tablefront.unity", true)};
        AssetDatabase.SaveAssets();
        return "Tablefront scene saved; native display, 1.5x supersampling, 8xMSAA, 8K shadow atlas, native overlay HUD, single instance";
    }
}
