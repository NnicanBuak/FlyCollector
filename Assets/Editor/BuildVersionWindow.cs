#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BuildTools
{
    /// <summary>
    /// Editor window for setting build version before building all profiles.
    /// Saves last used version to EditorPrefs.
    /// </summary>
    public class BuildVersionWindow : EditorWindow
    {
        private const string PREF_MAJOR = "BuildVersion_Major";
        private const string PREF_MINOR = "BuildVersion_Minor";
        private const string PREF_PATCH = "BuildVersion_Patch";
        private const string PREF_IS_HOTFIX = "BuildVersion_IsHotfix";
        private const string PREF_HOTFIX_NUM = "BuildVersion_HotfixNumber";

        private int _major = 1;
        private int _minor = 0;
        private int _patch = 0;
        private bool _isHotfix = false;
        private int _hotfixNumber = 1;

        [MenuItem("Build/Set Version and Build All...", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildVersionWindow>("Build Version");
            window.minSize = new Vector2(320, 500);
            window.maxSize = new Vector2(320, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadFromPrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Build Version Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Semantic Version", EditorStyles.miniBoldLabel);
                _major = EditorGUILayout.IntField("Major", Mathf.Max(0, _major));
                _minor = EditorGUILayout.IntField("Minor", Mathf.Max(0, _minor));
                _patch = EditorGUILayout.IntField("Patch", Mathf.Max(0, _patch));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");
            {
                _isHotfix = EditorGUILayout.Toggle("Is Hotfix", _isHotfix);

                using (new EditorGUI.DisabledScope(!_isHotfix))
                {
                    _hotfixNumber = EditorGUILayout.IntField("Hotfix Number", Mathf.Max(1, _hotfixNumber));
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Preview version string
            var version = GetCurrentVersion();
            EditorGUILayout.LabelField("Version Preview:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(version.ToStringWithPrefix(), EditorStyles.largeLabel);

            EditorGUILayout.Space(10);

            // Build button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Build All Profiles", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    BuildWithVersion();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(5);

            // Info text
            EditorGUILayout.HelpBox(
                "This will:\n" +
                "• Update Unity project version\n" +
                "• Build all configured profiles\n" +
                "• Archive builds to ZIP with version suffix",
                MessageType.Info
            );
        }

        private BuildVersion GetCurrentVersion()
        {
            return new BuildVersion(_major, _minor, _patch, _isHotfix, _hotfixNumber);
        }

        private void BuildWithVersion()
        {
            SaveToPrefs();

            var version = GetCurrentVersion();

            if (EditorUtility.DisplayDialog(
                "Start Build?",
                $"Build all profiles with version {version.ToStringWithPrefix()}?\n\n" +
                $"Unity bundleVersion will be set to: {version.ToStringWithoutPrefix()}\n" +
                $"Android versionCode will be set to: {version.ToVersionCode()}",
                "Build",
                "Cancel"))
            {
                Close();
                BuildAllProfiles.BuildAllWithVersion(version);
            }
        }

        private void LoadFromPrefs()
        {
            // Priority: Current project version → EditorPrefs → Default
            var currentBundleVersion = PlayerSettings.bundleVersion;

            if (BuildVersion.TryParse(currentBundleVersion, out var parsedVersion))
            {
                // Load from current project version
                _major = parsedVersion.Major;
                _minor = parsedVersion.Minor;
                _patch = parsedVersion.Patch;
                _isHotfix = parsedVersion.IsHotfix;
                _hotfixNumber = parsedVersion.HotfixNumber;

                Debug.Log($"[BuildVersionWindow] Loaded current project version: {parsedVersion.ToStringWithPrefix()}");
            }
            else
            {
                // Fallback to EditorPrefs or defaults
                _major = EditorPrefs.GetInt(PREF_MAJOR, 1);
                _minor = EditorPrefs.GetInt(PREF_MINOR, 0);
                _patch = EditorPrefs.GetInt(PREF_PATCH, 0);
                _isHotfix = EditorPrefs.GetBool(PREF_IS_HOTFIX, false);
                _hotfixNumber = EditorPrefs.GetInt(PREF_HOTFIX_NUM, 1);

                Debug.Log($"[BuildVersionWindow] Could not parse project version '{currentBundleVersion}', loaded from EditorPrefs");
            }
        }

        private void SaveToPrefs()
        {
            EditorPrefs.SetInt(PREF_MAJOR, _major);
            EditorPrefs.SetInt(PREF_MINOR, _minor);
            EditorPrefs.SetInt(PREF_PATCH, _patch);
            EditorPrefs.SetBool(PREF_IS_HOTFIX, _isHotfix);
            EditorPrefs.SetInt(PREF_HOTFIX_NUM, _hotfixNumber);
        }
    }
}
#endif
