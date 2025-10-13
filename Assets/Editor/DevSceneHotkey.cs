// Assets/Editor/DevSceneHotkeyMenu.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DevSceneHotkeyMenu
{
    // % = Ctrl/Cmd, & = Alt → Ctrl/Cmd + Alt + N/B/R
    [MenuItem("Dev/Next Scene %&n", priority = 1)]
    private static void NextScene()  => OpenByOffset(+1);

    [MenuItem("Dev/Prev Scene %&b", priority = 2)]
    private static void PrevScene()  => OpenByOffset(-1);

    [MenuItem("Dev/Reload Scene %&r", priority = 3)]
    private static void ReloadScene() => OpenByOffset(0);

    // Валидация пунктов меню — задизейблить, если нет сцен
    [MenuItem("Dev/Next Scene %&n", true)]
    [MenuItem("Dev/Prev Scene %&b", true)]
    [MenuItem("Dev/Reload Scene %&r", true)]
    private static bool ValidateMenu() => SceneManager.sceneCountInBuildSettings > 0;

    private static void OpenByOffset(int delta)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int total = SceneManager.sceneCountInBuildSettings;
        int cur   = SceneManager.GetActiveScene().buildIndex;

        // если текущая сцена не в билде — начнем с нулевой
        int target = delta == 0 ? cur : (cur < 0 ? 0 : (cur + delta + total) % total);
        if (target < 0) target = 0;

        string path = SceneUtility.GetScenePathByBuildIndex(target);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[DEV] Не найден путь сцены #{target}. Проверь Build Settings.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log($"[DEV] Editor open: #{target} → {scene.name}");
    }
}