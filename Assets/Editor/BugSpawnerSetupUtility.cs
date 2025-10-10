#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using Bug;

public static class BugSpawnerSetupUtility
{
    [MenuItem("Tools/Bugs/Assign Registry to Spawner Manager")]
    public static void AssignRegistryToManager()
    {

        var guids = AssetDatabase.FindAssets("t:BugPrefabRegistry");

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Registry Not Found",
                "Не найден ни один BugPrefabRegistry asset в проекте.\n\n" +
                "Создайте его: ПКМ в Project → Create → Bugs → Bug Prefab Registry",
                "OK"
            );
            return;
        }

        BugPrefabRegistry registry;

        if (guids.Length == 1)
        {

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            registry = AssetDatabase.LoadAssetAtPath<BugPrefabRegistry>(path);
        }
        else
        {

            var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();
            var names = paths.Select(p => System.IO.Path.GetFileName(p)).ToArray();

            var choice = EditorUtility.DisplayDialogComplex(
                "Multiple Registries Found",
                $"Найдено {guids.Length} реестров. Выберите который назначить:",
                names[0],
                names.Length > 1 ? names[1] : "Cancel",
                names.Length > 2 ? names[2] : ""
            );

            if (choice >= paths.Length) return;

            registry = AssetDatabase.LoadAssetAtPath<BugPrefabRegistry>(paths[choice]);
        }

        if (registry == null)
        {
            Debug.LogError("[BugSpawnerSetupUtility] Не удалось загрузить BugPrefabRegistry!");
            return;
        }

        var manager = Object.FindFirstObjectByType<BugSpawnerManager>();

        if (manager == null)
        {
            EditorUtility.DisplayDialog(
                "Manager Not Found",
                "Не найден BugSpawnerManager на текущей сцене.\n\n" +
                "Добавьте BugSpawnerManager на сцену перед назначением реестра.",
                "OK"
            );
            return;
        }


        var so = new SerializedObject(manager);
        var registryProp = so.FindProperty("prefabRegistry");

        if (registryProp != null)
        {
            registryProp.objectReferenceValue = registry;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);

            EditorUtility.DisplayDialog(
                "Success",
                $"BugPrefabRegistry '{registry.name}' назначен BugSpawnerManager.",
                "OK"
            );

            Debug.Log($"[BugSpawnerSetupUtility] Назначен {registry.name} → BugSpawnerManager");
        }
        else
        {
            Debug.LogError("[BugSpawnerSetupUtility] Не найдено поле 'prefabRegistry' в BugSpawnerManager!");
        }
    }
}
#endif
