using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SplitSceneClipByObjects : EditorWindow
{
    private AnimationClip sourceClip;
    private Transform animationRoot;
    private DefaultAsset outputFolder;

    // Храним список целей как список, а не фиксированный массив
    private readonly List<Transform> targets = new List<Transform>();
    private Vector2 scroll;

    [MenuItem("Tools/Animation/Split Scene Clip By Objects")]
    static void Open() => GetWindow<SplitSceneClipByObjects>("Split Scene Clip");

    private void OnGUI()
    {
        sourceClip     = (AnimationClip)EditorGUILayout.ObjectField("Source Clip",     sourceClip,     typeof(AnimationClip), false);
        animationRoot  = (Transform)    EditorGUILayout.ObjectField("Animation Root",  animationRoot,  typeof(Transform),     true);
        outputFolder   = (DefaultAsset) EditorGUILayout.ObjectField("Output Folder",   outputFolder,   typeof(DefaultAsset),  false);

        EditorGUILayout.Space();

        // Панель управления списком
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selection"))
                AddSelection();

            if (GUILayout.Button("Clear"))
                targets.Clear();

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!CanRunWithTargets(targets)))
            {
                if (GUILayout.Button("Split"))
                    Split(targets);
            }
        }

        // Быстрый режим: сразу из выделения, без списка
        using (new EditorGUI.DisabledScope(!CanRunWithSelection()))
        {
            if (GUILayout.Button("Use Selection & Split"))
            {
                var sel = FilterSelectionUnderRoot();
                Split(sel);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Targets ({targets.Count})", EditorStyles.boldLabel);

        // Список целей со скроллом
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120));
        for (int i = 0; i < targets.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                targets[i] = (Transform)EditorGUILayout.ObjectField(targets[i], typeof(Transform), true);
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    targets.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox(
            "Нажми Add Selection, чтобы добавить текущее выделение как цели. " +
            "Кнопка Use Selection & Split возьмёт выделение и сразу разрежет клип без заполнения списка. " +
            "В список/выделение попадут только потомки Animation Root.",
            MessageType.Info);
    }

    // ---- UI helpers ----

    private void AddSelection()
    {
        if (!animationRoot)
        {
            EditorUtility.DisplayDialog("No Root", "Укажи Animation Root.", "OK");
            return;
        }

        foreach (var t in Selection.transforms)
        {
            if (!t) continue;
            if (!t.IsChildOf(animationRoot)) continue;         // только потомки корня
            if (!targets.Contains(t)) targets.Add(t);           // без дублей
        }
    }

    private bool CanRunCommon()
        => sourceClip && animationRoot && outputFolder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(outputFolder));

    private bool CanRunWithSelection()
        => CanRunCommon() && FilterSelectionUnderRoot().Count > 0;

    private bool CanRunWithTargets(List<Transform> list)
        => CanRunCommon() && list != null && list.Count > 0 && list.All(t => t);

    private List<Transform> FilterSelectionUnderRoot()
        => Selection.transforms.Where(t => t && animationRoot && t.IsChildOf(animationRoot)).Distinct().ToList();

    // ---- Core ----

    private void Split(List<Transform> list)
    {
        if (!CanRunWithTargets(list))
        {
            EditorUtility.DisplayDialog("Nothing to split", "Нет валидных целей.", "OK");
            return;
        }

        string outFolder = AssetDatabase.GetAssetPath(outputFolder);

        foreach (var t in list)
        {
            if (!t || !t.IsChildOf(animationRoot))
            {
                Debug.LogWarning($"Пропущен {t?.name}: не потомок Animation Root.");
                continue;
            }

            string relPrefix = GetRelativePath(animationRoot, t);
            string clipName  = $"{sourceClip.name}_{t.name}";
            string path      = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outFolder, clipName + ".anim").Replace("\\", "/"));

            var newClip = new AnimationClip
            {
                frameRate = sourceClip.frameRate,
                legacy    = sourceClip.legacy,
                wrapMode  = sourceClip.wrapMode
            };

            // Float-кривые
            foreach (var b in AnimationUtility.GetCurveBindings(sourceClip))
            {
                if (!PathStartsWith(b.path, relPrefix)) continue;
                var nb = b; nb.path = TrimPrefix(b.path, relPrefix);
                var curve = AnimationUtility.GetEditorCurve(sourceClip, b);
                AnimationUtility.SetEditorCurve(newClip, nb, curve);
            }

            // ObjectReference-кривые
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
            {
                if (!PathStartsWith(b.path, relPrefix)) continue;
                var nb = b; nb.path = TrimPrefix(b.path, relPrefix);
                var keys = AnimationUtility.GetObjectReferenceCurve(sourceClip, b);
                AnimationUtility.SetObjectReferenceCurve(newClip, nb, keys);
            }

            // События
            AnimationUtility.SetAnimationEvents(newClip, AnimationUtility.GetAnimationEvents(sourceClip));

            AssetDatabase.CreateAsset(newClip, path);
            Debug.Log($"Создан клип: {path} → {t.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---- path utils ----

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (child == root) return string.Empty;
        var parts = new List<string>();
        var t = child;
        while (t && t != root) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool PathStartsWith(string path, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return true;
        if (string.IsNullOrEmpty(path))   return false;
        return path == prefix || path.StartsWith(prefix + "/");
    }

    private static string TrimPrefix(string path, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return path;
        if (path == prefix) return string.Empty;
        return path.StartsWith(prefix + "/") ? path.Substring(prefix.Length + 1) : path;
    }
}
