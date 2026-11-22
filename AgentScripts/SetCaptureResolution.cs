using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only QA helper loaded with Pipeline script-run; never copied under Assets or shipped.
/// The installed Pipeline draws Overlay UI at the GameView size and then scales its PNG.
/// Select a genuine fixed-resolution GameView before capturing, then wait for a rendered frame.
/// Internal APIs checked against UnityCsReference's 6000.6 GameView/GameViewSize sources.
/// </summary>
public static class SetCaptureResolution
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    const string Name = "Tablefront temporary QA";
    const string StateKey = "Tablefront.QA.GameViewOriginalIndex";

    static Type EditorType(string name)
    {
        var type = Type.GetType("UnityEditor." + name + ",UnityEditor");
        if (type == null) throw new InvalidOperationException("Unity 6000.6 Editor API unavailable: " + name);
        return type;
    }

    static EditorWindow View()
    {
        var type = EditorType("GameView");
        var view = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => type.IsInstanceOfType(w));
        if (view == null) throw new InvalidOperationException("Open a GameView through Pipeline capture first; this helper never opens or focuses a window.");
        return view;
    }

    static object Group()
    {
        var type = EditorType("GameViewSizes");
        var sizes = type.GetProperty("instance", Static)?.GetValue(null);
        if (sizes == null) throw new InvalidOperationException("GameViewSizes.instance missing");
        return type.GetProperty("currentGroup", Instance).GetValue(sizes);
    }

    public static string Set(int width, int height)
    {
        if (width < 320 || height < 240 || width > 4096 || height > 4096) throw new ArgumentOutOfRangeException("QA size must be320..4096 by240..4096");
        var view = View(); var type = view.GetType(); var group = Group(); var groupType = group.GetType();
        var selected = type.GetProperty("selectedSizeIndex", Instance);
        int previous = (int)selected.GetValue(view);
        if (SessionState.GetInt(StateKey, -1) < 0) SessionState.SetInt(StateKey, previous);
        var getSize = groupType.GetMethod("GetGameViewSize", Instance);
        int count = (int)groupType.GetMethod("GetTotalCount", Instance).Invoke(group, null);
        int index = -1;
        var sizeType = EditorType("GameViewSize");
        for (var i = 0; i < count; i++)
        {
            var candidate = getSize.Invoke(group, new object[] { i });
            if ((string)sizeType.GetProperty("baseText", Instance).GetValue(candidate) != Name) continue;
            sizeType.GetProperty("width", Instance).SetValue(candidate, width);
            sizeType.GetProperty("height", Instance).SetValue(candidate, height);
            index = i;
            break;
        }
        if (index < 0)
        {
            var enumType = EditorType("GameViewSizeType");
            var fixedType = Enum.Parse(enumType, "FixedResolution");
            var size = Activator.CreateInstance(sizeType, Instance, null, new object[] { fixedType, width, height, Name }, null);
            index = count;
            groupType.GetMethod("AddCustomSize", Instance).Invoke(group, new[] { size });
        }
        // Trigger the normal Editor size-selection callback even when editing our existing QA size.
        selected.SetValue(view, 0);
        type.GetMethod("SizeSelectionCallback", Instance).Invoke(view, new object[] { index, null });
        view.Repaint();
        return "Requested fixed " + width + "x" + height + "; wait for an actual rendered frame before Report() and capture. " + Report();
    }

    public static string Report()
    {
        var play = EditorType("PlayModeView");
        var target = (Vector2)play.GetMethod("GetMainPlayModeViewTargetSize", Static).Invoke(null, null);
        var view = play.GetMethod("GetMainPlayModeView", Static).Invoke(null, null);
        var texture = play.GetField("m_TargetTexture", Instance).GetValue(view) as RenderTexture;
        return "GameView target=" + Mathf.RoundToInt(target.x) + "x" + Mathf.RoundToInt(target.y)
            + "; rendered texture=" + (texture != null ? texture.width + "x" + texture.height : "none")
            + "; frame=" + Time.frameCount;
    }

    public static string Restore()
    {
        int original = SessionState.GetInt(StateKey, -1);
        if (original < 0) return "No QA GameView state to restore";
        var view = View(); var group = Group(); var groupType = group.GetType();
        view.GetType().GetMethod("SizeSelectionCallback", Instance).Invoke(view, new object[] { original, null });
        int count = (int)groupType.GetMethod("GetTotalCount", Instance).Invoke(group, null);
        var sizeType = EditorType("GameViewSize");
        for (int i = count - 1; i >= 0; i--)
        {
            var candidate = groupType.GetMethod("GetGameViewSize", Instance).Invoke(group, new object[] { i });
            if ((string)sizeType.GetProperty("baseText", Instance).GetValue(candidate) == Name)
                groupType.GetMethod("RemoveCustomSize", Instance).Invoke(group, new object[] { i });
        }
        SessionState.EraseInt(StateKey); view.Repaint();
        return "Restored previous GameView selection; temporary QA size removed. " + Report();
    }
}
