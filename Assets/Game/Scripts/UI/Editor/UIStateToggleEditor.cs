using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIStateToggle))]
public class UIStateToggleEditor : Editor
{
    SerializedProperty stateNamesProp;
    SerializedProperty stateObjectsProp;
    SerializedProperty showFailProp;
    SerializedProperty showMismatchProp;
    SerializedProperty showWinProp;

    private void OnEnable()
    {
        stateNamesProp = serializedObject.FindProperty("stateNames");
        stateObjectsProp = serializedObject.FindProperty("stateObjects");
        showFailProp = serializedObject.FindProperty("showFail");
        showMismatchProp = serializedObject.FindProperty("showMismatch");
        showWinProp = serializedObject.FindProperty("showWin");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(stateNamesProp, true);
        EditorGUILayout.PropertyField(stateObjectsProp, true);

        var toggle = (UIStateToggle)target;
        bool changed = false;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Active States", EditorStyles.boldLabel);

        changed |= DrawStateToggle(toggle.SupportsFail(), "Fail", showFailProp);
        changed |= DrawStateToggle(toggle.SupportsMismatch(), "Mismatch", showMismatchProp);
        changed |= DrawStateToggle(toggle.SupportsWin(), "Win", showWinProp);

        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            toggle.ApplyStateVisibility();
            EditorUtility.SetDirty(toggle);
        }
    }

    private bool DrawStateToggle(bool supported, string label, SerializedProperty prop)
    {
        if (!supported)
            return false;

        EditorGUI.BeginChangeCheck();
        bool newValue = EditorGUILayout.Toggle(label, prop.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            prop.boolValue = newValue;
            return true;
        }
        return false;
    }
}
