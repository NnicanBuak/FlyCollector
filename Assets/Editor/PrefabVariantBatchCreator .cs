// Assets/Editor/PrefabVariantBatchCreator.cs
// Делает варианты выбранного базового префаба для всех выделенных объектов,
// дублируя их меши в отдельные .asset и назначая их в варианте.

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PrefabVariantBatchCreator : EditorWindow
{
    private GameObject basePrefab;
    private DefaultAsset outputFolder;
    private string suffix = "_Var";
    private bool copyMeshFilterMeshes = true;
    private bool copySkinnedMeshes = true;
    private bool includeInactive = false;

    [MenuItem("Tools/Prefab Variants/Batch Create From Selection")]
    private static void Open() => GetWindow<PrefabVariantBatchCreator>("Batch Variant Creator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Base Prefab", EditorStyles.boldLabel);
        basePrefab = (GameObject)EditorGUILayout.ObjectField(basePrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output Folder", EditorStyles.boldLabel);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(outputFolder, typeof(DefaultAsset), false);
        suffix = EditorGUILayout.TextField("Name Suffix", suffix);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mesh Copy Options", EditorStyles.boldLabel);
        copyMeshFilterMeshes = EditorGUILayout.ToggleLeft("Duplicate Meshes in MeshFilter", copyMeshFilterMeshes);
        copySkinnedMeshes = EditorGUILayout.ToggleLeft("Duplicate Meshes in SkinnedMeshRenderer", copySkinnedMeshes);

        EditorGUILayout.Space();
        includeInactive = EditorGUILayout.ToggleLeft("Include inactive objects", includeInactive);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("Create Variants For Selected Objects"))
                CreateVariants();
        }
    }

    private bool CanRun()
    {
        if (!basePrefab || !AssetDatabase.Contains(basePrefab)) return false;
        if (!outputFolder) return false;
        return GetSelection(includeInactive).Length > 0;
    }

    private static GameObject[] GetSelection(bool includeInactive)
    {
        var objs = Selection.gameObjects;
        return includeInactive ? objs : objs.Where(o => o.activeInHierarchy).ToArray();
    }

    private void CreateVariants()
    {
        string folderPath = AssetDatabase.GetAssetPath(outputFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder", "Select a valid output folder.", "OK");
            return;
        }

        var selected = GetSelection(includeInactive);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            foreach (var src in selected)
                CreateVariantFor(src, folderPath);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private void CreateVariantFor(GameObject sourceObject, string outFolder)
    {
        string variantName = MakeSafeFileName(sourceObject.name + suffix);
        string variantPath = Path.Combine(outFolder, variantName + ".prefab").Replace("\\", "/");

        // 1) Делаем инстанс базового префаба (это важно — тогда SaveAsPrefabAsset создаст V A R I A N T)
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        inst.name = variantName;

        // 2) Дублируем и назначаем меши
        string meshesFolder = EnsureMeshesFolder(outFolder, variantName);
        CopyMeshes(sourceObject, inst, meshesFolder);

        // 3) Сохраняем как префаб-ассет -> получится Prefab Variant родителя basePrefab
        PrefabUtility.SaveAsPrefabAsset(inst, variantPath);

        // 4) Чистим сцену
        DestroyImmediate(inst);
    }

private void CopyMeshes(GameObject srcRoot, GameObject dstRoot, string meshesFolder)
{
    // ---- MeshFilter ----
    if (copyMeshFilterMeshes)
    {
        foreach (var src in srcRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!src.sharedMesh) continue;

            string relPath = GetHierarchyPath(srcRoot, src.gameObject);

            // 1) пытаемся найти точное соответствие по пути
            Transform dstTf = string.IsNullOrEmpty(relPath)
                ? dstRoot.transform
                : dstRoot.transform.Find(relPath);

            // 2) если не нашли — берём ПЕРВЫЙ MeshFilter в дочерних объектах
            var dstFilter = dstTf ? dstTf.GetComponent<MeshFilter>() : null;
            if (!dstFilter)
                dstFilter = dstRoot.GetComponentInChildren<MeshFilter>(true);

            if (!dstFilter) continue; // в префабе вообще нет MeshFilter

            // уникальная копия меша
            Mesh dup = Object.Instantiate(src.sharedMesh);
            dup.name = MakeSafeAssetName($"{dstRoot.name}_{src.sharedMesh.name}");
            SaveMeshAsset(dup, meshesFolder);
            dstFilter.sharedMesh = dup;
        }
    }

    // ---- SkinnedMeshRenderer ----
    if (copySkinnedMeshes)
    {
        foreach (var src in srcRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!src.sharedMesh) continue;

            string relPath = GetHierarchyPath(srcRoot, src.gameObject);

            Transform dstTf = string.IsNullOrEmpty(relPath)
                ? dstRoot.transform
                : dstRoot.transform.Find(relPath);

            var dstSkin = dstTf ? dstTf.GetComponent<SkinnedMeshRenderer>() : null;
            if (!dstSkin)
                dstSkin = dstRoot.GetComponentInChildren<SkinnedMeshRenderer>(true); // <-- ищем "первый" в детях

            if (!dstSkin) continue; // в префабе нет S.M.R.

            Mesh dup = Object.Instantiate(src.sharedMesh);
            dup.name = MakeSafeAssetName($"{dstRoot.name}_{src.sharedMesh.name}");
            SaveMeshAsset(dup, meshesFolder);
            dstSkin.sharedMesh = dup;
        }
    }
}


    private static string EnsureMeshesFolder(string outFolder, string variantName)
    {
        string meshesRoot = Path.Combine(outFolder, "Meshes").Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(meshesRoot))
            AssetDatabase.CreateFolder(outFolder, "Meshes");

        string variantMeshes = Path.Combine(meshesRoot, variantName).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(variantMeshes))
            AssetDatabase.CreateFolder(meshesRoot, variantName);

        return variantMeshes;
    }

    private static void SaveMeshAsset(Mesh mesh, string folder)
    {
        string path = Path.Combine(folder, mesh.name + ".asset").Replace("\\", "/");
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        // Ensure mesh stays readable for Outline script (needs to write to UV4)
        mesh.UploadMeshData(false);

        AssetDatabase.CreateAsset(mesh, path);
    }

    private static string GetHierarchyPath(GameObject root, GameObject obj)
{
    if (obj == root) return string.Empty; // корень
    string path = obj.name;
    var t = obj.transform.parent;
    while (t && t.gameObject != root)
    {
        path = t.name + "/" + path;
        t = t.parent;
    }
    return path; // путь относительно root, без имени root
}


    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static string MakeSafeAssetName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}
