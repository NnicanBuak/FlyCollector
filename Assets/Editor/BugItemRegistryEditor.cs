#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BugData;

/// <summary>
/// Custom Inspector for BugItemRegistry with auto-populate button
/// </summary>
[CustomEditor(typeof(BugItemRegistry))]
public class BugItemRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BugItemRegistry registry = (BugItemRegistry)target;

        // Display standard inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Auto-populate button
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("Auto-populate from Bug Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Автоматически найдёт все префабы жуков (с BugAI/BugMeta), создаст Item ScriptableObjects и заполнит реестр.\n\n" +
                "1. Укажите папку с префабами жуков (Bug Prefabs Path)\n" +
                "2. Укажите папку где создавать Items (Output Items Path)\n" +
                "3. Нажмите кнопку ниже",
                MessageType.Info
            );

            if (GUILayout.Button("🐛 Create Items from Bug Prefabs", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Auto-populate from Bug Prefabs",
                    "Это:\n" +
                    "• Найдёт все префабы жуков в указанной папке\n" +
                    "• Создаст Item ScriptableObjects (или переиспользует существующие)\n" +
                    "• Очистит и заполнит реестр\n\n" +
                    "Продолжить?",
                    "Да",
                    "Отмена"))
                {
                    registry.AutoPopulateFromFolder();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }
}
#endif
