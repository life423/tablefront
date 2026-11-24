using System;
using System.IO;
using System.Linq;
using Tablefront;
using UnityEngine;
using UnityEngine.Rendering;

// Read-only baseline probe for the reviewed frame. Not a substitute for visual inspection.
public static class ProbeManeuverVisual
{
    public static string Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<TablefrontGame>();
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>()
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var defaults = renderers.Where(r => r.sharedMaterials.Any(m => !m || !m.shader || m.name.Contains("Default-Material")))
            .Select(r => r.name).Distinct().ToArray();
        var whitePanels = game.HUD.GetComponentsInChildren<UnityEngine.UI.Image>()
            .Where(i => i.color == Color.white && !i.GetComponent<UnityEngine.UI.Button>()).Select(i => i.name).ToArray();
        var whiteParticles = UnityEngine.Object.FindObjectsByType<ParticleSystem>()
            .Where(p => p.main.startColor.mode == ParticleSystemGradientMode.Color && p.main.startColor.color == Color.white).Select(p => p.name).ToArray();
        string report = "pipeline=" + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in") +
            "; activeRenderers=" + renderers.Length + "; missingOrDefaultMaterials=" + string.Join(",", defaults) +
            "; untintedNonButtonPanels=" + string.Join(",", whitePanels) + "; defaultWhiteParticles=" + string.Join(",", whiteParticles) +
            "; planningPool=" + game.OrderPreview.PoolSize + "; planningVisible=" + game.OrderPreview.VisibleRendererCount;
        File.WriteAllText("TestResults/maneuver-visual-baseline.txt", report + Environment.NewLine);
        return report;
    }
}
