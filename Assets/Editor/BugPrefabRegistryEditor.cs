#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector для BugPrefabRegistry с кнопкой автозаполнения
/// </summary>
[CustomEditor(typeof(BugPrefabRegistry))]
public class BugPrefabRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BugPrefabRegistry registry = (BugPrefabRegistry)target;

        // Отображаем стандартный инспектор
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Кнопка автозаполнения
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("Auto-populate", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Автоматически найдёт все префабы в указанной папке с заданным префиксом/суффиксом и заполнит реестр.",
                MessageType.Info
            );

            if (GUILayout.Button("🔍 Find and Populate Prefabs", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Auto-populate Prefabs",
                    "Это очистит все существующие записи и заполнит реестр найденными префабами. Продолжить?",
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
