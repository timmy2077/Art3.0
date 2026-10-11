using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Keep Unity's native Rect Transform inspector and add an in-place rounding command.</summary>
[CustomEditor(typeof(RectTransform)), CanEditMultipleObjects]
public sealed class UIRectRoundingTool : Editor
{
    private Editor nativeInspector;
    private System.Reflection.MethodInfo nativeSceneGUI;

    private void OnEnable()
    {
        Type nativeType = typeof(Editor).Assembly.GetType("UnityEditor.RectTransformEditor");
        if (nativeType == null) return;
        nativeInspector = CreateEditor(targets, nativeType);
        nativeSceneGUI = nativeType.GetMethod("OnSceneGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }

    private void OnDisable()
    {
        if (nativeInspector != null) DestroyImmediate(nativeInspector);
    }

    public override void OnInspectorGUI()
    {
        if (nativeInspector != null) nativeInspector.OnInspectorGUI();
        else DrawDefaultInspector();

        var rects = new List<RectTransform>();
        foreach (var selected in targets)
        {
            var rect = selected as RectTransform;
            if (rect != null && !EditorUtility.IsPersistent(rect)) rects.Add(rect);
        }
        int skipped = 0;
        foreach (var rect in rects) if (IsDriven(rect)) skipped++;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(rects.Count == 0 || skipped == rects.Count || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(new GUIContent("坐标与尺寸取整", "位置 X/Y/Z 与 Width/Height 取整，支持 Ctrl+Z 撤销。"), GUILayout.Height(26f)))
                Apply(rects.ToArray());
        }
        if (rects.Count > 0 && skipped == rects.Count)
            EditorGUILayout.LabelField("当前 Rect Transform 由布局或 Slider 控制", EditorStyles.miniLabel);
    }

    private void OnSceneGUI()
    {
        if (nativeInspector != null && nativeSceneGUI != null)
            nativeSceneGUI.Invoke(nativeInspector, null);
    }

    internal static bool IsDriven(RectTransform rect)
    {
        if (rect.drivenByObject != null) return true;
        // Detect controllers even when their UI window is inactive and the driven tracker is cleared.
        var fitter = rect.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled &&
            (fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained || fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained)) return true;
        var aspect = rect.GetComponent<AspectRatioFitter>();
        if (aspect != null && aspect.enabled && aspect.aspectMode != AspectRatioFitter.AspectMode.None) return true;
        if (rect.parent != null)
        {
            var layout = rect.parent.GetComponent<LayoutGroup>();
            if (layout != null && layout.enabled)
            {
                bool ignored = false;
                foreach (var component in rect.GetComponents<Component>())
                {
                    var ignorer = component as ILayoutIgnorer;
                    var behaviour = component as Behaviour;
                    if (ignorer != null && (behaviour == null || behaviour.enabled) && ignorer.ignoreLayout) ignored = true;
                }
                if (!ignored) return true;
            }
        }
        foreach (var slider in rect.GetComponentsInParent<Slider>(true))
            if (slider.enabled && (slider.fillRect == rect || slider.handleRect == rect)) return true;
        return false;
    }

    private static float Round(float value, int digits)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return value;
        return (float)Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }

    private static void Apply(RectTransform[] targets)
    {
        var changed = new List<RectTransform>();
        int skipped = 0;
        foreach (var rect in targets)
        {
            if (rect == null || EditorUtility.IsPersistent(rect)) continue;
            if (IsDriven(rect)) { skipped++; continue; }
            Vector3 position = rect.anchoredPosition3D;
            Vector2 size = rect.sizeDelta;
            bool positionChanged = position.x != Round(position.x, 0) || position.y != Round(position.y, 0) || position.z != Round(position.z, 0);
            bool sizeChanged = size.x != Round(size.x, 0) || size.y != Round(size.y, 0);
            if (positionChanged || sizeChanged) changed.Add(rect);
        }

        if (changed.Count > 0)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("UI 坐标与尺寸取整");
            Undo.RecordObjects(changed.ToArray(), "UI 坐标与尺寸取整");
            foreach (var rect in changed)
            {
                Vector3 p = rect.anchoredPosition3D;
                rect.anchoredPosition3D = new Vector3(Round(p.x, 0), Round(p.y, 0), Round(p.z, 0));
                Vector2 s = rect.sizeDelta;
                rect.sizeDelta = new Vector2(Round(s.x, 0), Round(s.y, 0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                if (rect.gameObject.scene.IsValid() && rect.gameObject.scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(rect.gameObject.scene);
            }
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }
        string message = $"已修改 {changed.Count} 个 UI，跳过 {skipped} 个受驱动对象。";
        Debug.Log("[UI 坐标取整] " + message);
    }
}
