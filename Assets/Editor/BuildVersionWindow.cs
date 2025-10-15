#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Profile;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BuildTools
{
    public class BuildVersionWindow : EditorWindow
    {
        private const string PREF_MAJOR = "BuildVersion_Major";
        private const string PREF_MINOR = "BuildVersion_Minor";
        private const string PREF_PATCH = "BuildVersion_Patch";
        private const string PREF_IS_HOTFIX = "BuildVersion_IsHotfix";
        private const string PREF_HOTFIX_NUM = "BuildVersion_HotfixNumber";

        private int _major = 1;
        private int _minor;
        private int _patch;
        private bool _isHotfix;
        private int _hotfixNumber = 1;

        [MenuItem("Build/Set Version and Build All...", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildVersionWindow>("Build Version");
            window.minSize = new Vector2(320, 500);
            window.maxSize = new Vector2(320, 500);
            window.Show();
        }

        private void OnEnable() => LoadFromPrefs();

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

            var version = GetCurrentVersion();
            EditorGUILayout.LabelField("Version Preview:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(version.ToStringWithPrefix(), EditorStyles.largeLabel);

            EditorGUILayout.Space(10);

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


            var guids = AssetDatabase.FindAssets("t:BuildProfile");
            var profiles = new List<BuildProfile>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (p != null) profiles.Add(p);
            }

            if (profiles.Count == 0)
            {
                EditorUtility.DisplayDialog("No Build Profiles", "Couldn't find any BuildProfile assets.", "OK");
                return;
            }


            bool proceed = EditorUtility.DisplayDialog(
                "Start Build?",
                $"Build all profiles with version {version.ToStringWithPrefix()}?\n\n" +
                $"Unity bundleVersion will be set to: {version.ToStringWithoutPrefix()}\n" +
                $"Android versionCode will be set to: {version.ToVersionCode()}",
                "Build",
                "Cancel");

            if (!proceed) return;


            BuildProfile.SetActiveBuildProfile(profiles[0]);

            Close();


            BuildAllProfiles.BuildAllWithVersion(version);
        }

        private void LoadFromPrefs()
        {
            string diskBundle = null;

            try
            {
                var appData = Application.dataPath;
                var projectDirInfo = Directory.GetParent(appData);
                if (projectDirInfo == null)
                {
                    Debug.LogWarning("[BuildVersionWindow] Could not determine project directory.");
                }
                else
                {
                    var projectSettingsPath = Path.Combine(projectDirInfo.FullName, "ProjectSettings", "ProjectSettings.asset");
                    if (File.Exists(projectSettingsPath))
                    {
                        var fileText = File.ReadAllText(projectSettingsPath);
                        var match = Regex.Match(fileText, @"(?m)^\s*bundleVersion:\s*(.+)$");
                        if (match.Success)
                            diskBundle = match.Groups[1].Value.Trim();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BuildVersionWindow] Could not read ProjectSettings.asset: {ex.Message}");
            }


            var currentBundleVersion = PlayerSettings.bundleVersion;


            if (!string.IsNullOrEmpty(diskBundle) && BuildVersion.TryParse(diskBundle, out var diskParsed))
            {
                _major = diskParsed.Major;
                _minor = diskParsed.Minor;
                _patch = diskParsed.Patch;
                _isHotfix = diskParsed.IsHotfix;
                _hotfixNumber = diskParsed.HotfixNumber;


                try
                {
                    var diskWithoutPrefix = diskParsed.ToStringWithoutPrefix();
                    if (PlayerSettings.bundleVersion != diskWithoutPrefix)
                    {
                        PlayerSettings.bundleVersion = diskWithoutPrefix;
                        PlayerSettings.Android.bundleVersionCode = diskParsed.ToVersionCode();
                        Debug.Log(
                            $"[BuildVersionWindow] Synchronized PlayerSettings.bundleVersion to {diskWithoutPrefix}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BuildVersionWindow] Could not synchronize PlayerSettings: {ex.Message}");
                }


                if (BuildVersion.TryParse(currentBundleVersion, out var memParsed))
                {
                    if (memParsed.ToStringWithoutPrefix() != diskParsed.ToStringWithoutPrefix())
                        Debug.Log(
                            $"[BuildVersionWindow] ProjectSettings.asset bundleVersion ({diskParsed.ToStringWithoutPrefix()}) differs from PlayerSettings.bundleVersion ({memParsed.ToStringWithoutPrefix()}). Using disk value.");
                }
                else
                {
                    Debug.Log(
                        $"[BuildVersionWindow] Loaded version from ProjectSettings.asset: {diskParsed.ToStringWithPrefix()}");
                }

                return;
            }


            if (BuildVersion.TryParse(currentBundleVersion, out var parsedVersion))
            {
                _major = parsedVersion.Major;
                _minor = parsedVersion.Minor;
                _patch = parsedVersion.Patch;
                _isHotfix = parsedVersion.IsHotfix;
                _hotfixNumber = parsedVersion.HotfixNumber;

                Debug.Log($"[BuildVersionWindow] Loaded current project version: {parsedVersion.ToStringWithPrefix()}");
            }
            else
            {
                _major = EditorPrefs.GetInt(PREF_MAJOR, 1);
                _minor = EditorPrefs.GetInt(PREF_MINOR, 0);
                _patch = EditorPrefs.GetInt(PREF_PATCH, 0);
                _isHotfix = EditorPrefs.GetBool(PREF_IS_HOTFIX, false);
                _hotfixNumber = EditorPrefs.GetInt(PREF_HOTFIX_NUM, 1);

                Debug.Log(
                    $"[BuildVersionWindow] Could not parse project version '{currentBundleVersion}', loaded from EditorPrefs");
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