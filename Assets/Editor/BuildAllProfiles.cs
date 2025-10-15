#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;

namespace BuildTools
{
    public static class BuildAllProfiles
    {
        static readonly Dictionary<string, string> OutputByProfileName = new Dictionary<string, string>
        {
            { "Windows", "Platforms/Windows/FlyCollector-win/FlyCollector.exe" },
            { "Web", "Platforms/Web/Build/" },
            { "Linux", "Platforms/Linux/FlyCollector-linux/FlyCollector.x86_64" }
        };

        static readonly Dictionary<string, string> OutputByProfileAssetPath = new Dictionary<string, string>
        {
        };

        [MenuItem("Build/Build ALL Build Profiles (sequential)")]
        public static void BuildAll()
        {
            var guids = AssetDatabase.FindAssets("t:BuildProfile");
            var profiles = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.name)
                .ToList();

            if (profiles.Count == 0)
            {
                Debug.LogError("No Build Profiles found. Open File → Build Profiles and create at least one profile.");
                return;
            }

            var results = new List<string>();
            int ok = 0, fail = 0, cancel = 0;

            foreach (var profile in profiles)
            {
                var assetPath = AssetDatabase.GetAssetPath(profile);
                if (!TryGetOutputPath(profile, assetPath, out var outPath))
                {
                    Debug.LogWarning($"[SKIP] No output path configured for profile '{profile.name}' ({assetPath}).");
                    continue;
                }

                PrepareDirectories(outPath);


                BuildProfile.SetActiveBuildProfile(profile);


                var options = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outPath,
                    options = BuildOptions.None,
                };

                var start = DateTime.Now;
                var report = BuildPipeline.BuildPlayer(options);
                var took = DateTime.Now - start;

                switch (report.summary.result)
                {
                    case BuildResult.Succeeded:
                        ok++;
                        results.Add(
                            $"✅ {profile.name} → {outPath}  [{took:mm\\:ss}]  size: {(report.summary.totalSize / 1048576f):0.0} MB");
                        break;
                    case BuildResult.Cancelled:
                        cancel++;
                        results.Add($"⏹ {profile.name} → {outPath}  [{took:mm\\:ss}]  CANCELLED");
                        break;
                    default:
                        fail++;
                        results.Add(
                            $"❌ {profile.name} → {outPath}  [{took:mm\\:ss}]  ERRORS: {report.summary.totalErrors}");
                        break;
                }
            }

            Debug.Log($"=== Build All Profiles done ===\n" +
                      $"Success: {ok}, Failed: {fail}, Cancelled: {cancel}\n" +
                      string.Join("\n", results));
        }

        static bool TryGetOutputPath(BuildProfile profile, string assetPath, out string outPath)
        {
            if (OutputByProfileAssetPath.TryGetValue(assetPath, out outPath))
                return true;

            if (OutputByProfileName.TryGetValue(profile.name, out outPath))
                return true;

            outPath = string.Empty;
            return false;
        }

        static void PrepareDirectories(string path)
        {
            var full = Path.GetFullPath(path);
            string dir;

            if (Directory.Exists(full))
            {
                dir = full;
            }
            else if (Path.HasExtension(full))
            {
                dir = Path.GetDirectoryName(full) ?? Path.GetPathRoot(full) ?? string.Empty;
            }
            else
            {
                dir = full;
            }

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static void BuildAllWithVersion(BuildVersion version)
        {
            Debug.Log($"=== Starting Build All Profiles with version {version.ToStringWithPrefix()} ===");


            UpdatePlayerSettings(version);
            UpdateGameAnalyticsSettings(version);

            var guids = AssetDatabase.FindAssets("t:BuildProfile");
            if (guids.Length == 0)
            {
                Debug.LogError("No Build Profiles found. Open File → Build Profiles and create at least one profile.");
                return;
            }

            var results = new List<string>();
            int ok = 0, fail = 0, cancel = 0;

            foreach (var guid in guids)
            {

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(assetPath);
                if (profile == null)
                {
                    Debug.LogWarning($"[SKIP] Build Profile at {assetPath} could not be loaded.");
                    continue;
                }

                if (!TryGetOutputPath(profile, assetPath, out var basePath))
                {
                    Debug.LogWarning($"[SKIP] No output path configured for profile '{profile.name}' ({assetPath}).");
                    continue;
                }


                var versionedPath = InjectVersionIntoPath(basePath, version);
                PrepareDirectories(versionedPath);


                BuildProfile.SetActiveBuildProfile(profile);

                var options = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = versionedPath,
                    options = BuildOptions.None,
                };


                ClearEditorConsole();

                var start = DateTime.Now;
                var report = BuildPipeline.BuildPlayer(options);
                var took = DateTime.Now - start;

                switch (report.summary.result)
                {
                    case BuildResult.Succeeded:
                        ok++;
                        var resultMsg =
                            $"✅ {profile.name} → {versionedPath}  [{took:mm\\:ss}]  size: {(report.summary.totalSize / 1048576f):0.0} MB";
                        var archivePath = CreateZipArchive(versionedPath, profile.name, version);
                        if (!string.IsNullOrEmpty(archivePath))
                            resultMsg += $"\n   📦 Archived to: {archivePath}";
                        results.Add(resultMsg);
                        break;

                    case BuildResult.Cancelled:
                        cancel++;
                        results.Add($"⏹ {profile.name} → {versionedPath}  [{took:mm\\:ss}]  CANCELLED");
                        break;

                    default:
                        fail++;

                        var errors = GetConsoleErrors();
                        var errorText = errors != null && errors.Count > 0
                            ? string.Join("\n---\n", errors.Take(20))
                            : $"Build failed with {report.summary.totalErrors} errors (no console messages found).";

                        results.Add(
                            $"❌ {profile.name} → {versionedPath}  [{took:mm\\:ss}]  ERRORS: {report.summary.totalErrors}\n{errorText}");
                        break;
                }
            }

            Debug.Log($"=== Build All Profiles done (version {version.ToStringWithPrefix()}) ===\n" +
                      $"Success: {ok}, Failed: {fail}, Cancelled: {cancel}\n" +
                      string.Join("\n", results));

            if (ok > 0)
            {
                EditorUtility.DisplayDialog(
                    "Build Complete",
                    $"Successfully built {ok} profile(s) with version {version.ToStringWithPrefix()}.\n\n" +
                    $"Check Console for detailed results.",
                    "OK"
                );
                
            }
        }

        static void ClearEditorConsole()
        {
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null)
                    return;

                var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                clearMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not clear editor console: {ex.Message}");
            }
        }

        static void UpdatePlayerSettings(BuildVersion version)
        {
            var versionString = version.ToStringWithoutPrefix();
            PlayerSettings.bundleVersion = versionString;
            PlayerSettings.Android.bundleVersionCode = version.ToVersionCode();

            Debug.Log(
                $"Updated PlayerSettings: bundleVersion={versionString}, Android.bundleVersionCode={version.ToVersionCode()}");
        }


        static void UpdateGameAnalyticsSettings(BuildVersion version)
        {
            try
            {
                var versionString = version.ToStringWithoutPrefix();
                var settingsPath = Path.Combine("Assets", "Game", "Resources", "GameAnalytics", "Settings.asset");
                if (!File.Exists(settingsPath))
                {
                    Debug.LogWarning($"GameAnalytics settings file not found at {settingsPath}");
                    return;
                }

                var text = File.ReadAllText(settingsPath);


                var pattern = new Regex(@"(?m)^(?<indent>\s*)Build:\s*\r?\n(?<list>(?:\s*-\s*.*\r?\n)*)");
                var m = pattern.Match(text);
                if (!m.Success)
                {
                    Debug.LogWarning("GameAnalytics.Settings.asset: no 'Build:' section found to update.");
                    return;
                }

                var indent = m.Groups["indent"].Value;
                var listText = m.Groups["list"].Value;


                var count = 0;
                if (!string.IsNullOrEmpty(listText))
                {

                    count = Regex.Matches(listText, @"(?m)^\s*-\s*.*$").Count;
                }


                if (count == 0)
                    count = 1;


                // Построим новую секцию с нужной версией
                var newList = string.Empty;
                for (int i = 0; i < count; i++)
                {
                    // используем тот же indent, что и у 'Build:' плюс два пробела для YAML списка
                    var entryIndent = indent + "  ";
                    newList += entryIndent + "- " + versionString + "\r\n";
                }

                var replacement = indent + "Build:\r\n" + newList;
                var newText = pattern.Replace(text, replacement, 1);

                if (newText != text)
                {
                    File.WriteAllText(settingsPath, newText);
                    AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"Updated GameAnalytics.Settings.asset with Build={versionString}");
                }
                else
                {
                    Debug.Log($"GameAnalytics.Settings.asset did not require update (Build unchanged).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update GameAnalytics Settings.asset: {ex.Message}");
            }
        }

        static string InjectVersionIntoPath(string basePath, BuildVersion version)
        {
            var versionSuffix = version.ToStringWithPrefix();


            basePath = basePath.Replace("\\", "/");


            if (Path.HasExtension(basePath))
            {
                var lastSlash = basePath.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    var directory = basePath.Substring(0, lastSlash);
                    var filename = basePath.Substring(lastSlash + 1);


                    return $"{directory}-{versionSuffix}/{filename}";
                }
            }
            else
            {
                var trimmed = basePath.TrimEnd('/');
                return $"{trimmed}-{versionSuffix}/";
            }


            return $"{basePath}-{versionSuffix}";
        }

        static string CreateZipArchive(string buildPath, string profileName, BuildVersion version)
        {
            try
            {
                string sourceDir;


                if (Path.HasExtension(buildPath))
                {
                    sourceDir = Path.GetDirectoryName(buildPath);
                }
                else
                {
                    sourceDir = buildPath;
                }

                if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                {
                    Debug.LogWarning($"Cannot archive: directory not found at {sourceDir}");
                    return string.Empty;
                }


                var archiveName = $"FlyCollector-{profileName}-{version.ToStringWithPrefix()}.zip";


                var parentDir = Directory.GetParent(sourceDir)?.FullName;
                if (string.IsNullOrEmpty(parentDir))
                {
                    Debug.LogWarning($"Cannot determine parent directory for {sourceDir}");
                    return string.Empty;
                }

                var archivePath = Path.Combine(parentDir, archiveName);


                if (File.Exists(archivePath))
                    File.Delete(archivePath);


                var fullSourceDir = Path.GetFullPath(sourceDir);
                var allFiles = Directory.EnumerateFiles(fullSourceDir, "*", SearchOption.AllDirectories).ToList();
                Debug.Log($"[Archive] Found {allFiles.Count} files under {fullSourceDir} (before filtering).");


                var files = allFiles
                    .Where(f => !f.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                        .Any(seg => string.Equals(seg, "DontShip", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                Debug.Log($"[Archive] {files.Count} files remain after filtering DontShip.");


                if (files.Count == 0)
                {
                    var subdirs = Directory.GetDirectories(fullSourceDir);
                    foreach (var sd in subdirs)
                    {
                        var nested = Directory.EnumerateFiles(sd, "*", SearchOption.AllDirectories).ToList();
                        if (nested.Count > 0)
                        {
                            Debug.Log($"[Archive] Using nested directory {sd} as source (found {nested.Count} files).\n");
                            fullSourceDir = sd;
                            files = nested
                                .Where(f => !f.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                                    .Any(seg => string.Equals(seg, "DontShip", StringComparison.OrdinalIgnoreCase)))
                                .ToList();
                            break;
                        }
                    }
                }

                int added = 0;
                using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    foreach (var file in files)
                    {
                        var entryName = Path.GetRelativePath(fullSourceDir, file).Replace('\\', '/');
                        zip.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
                        added++;
                    }
                }

                if (added == 0)
                {
                    Debug.LogWarning($"Archive at {archivePath} created but no files were added (source={fullSourceDir}). Deleting empty archive.");
                    try
                    {
                        if (File.Exists(archivePath))
                            File.Delete(archivePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not delete empty archive {archivePath}: {ex.Message}");
                    }
                    return string.Empty;
                }

                var archiveSize = new FileInfo(archivePath).Length / 1048576f;
                Debug.Log($"Created archive: {archivePath} ({archiveSize:0.0} MB)");

                return archivePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create archive for {buildPath}: {ex.Message}");
                return string.Empty;
            }
        }


        static List<string> GetConsoleErrors()
        {
            try
            {
                var result = new List<string>();
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");

                if (logEntriesType == null || logEntryType == null)
                    return result;

                var getCount = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var getEntry = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (getCount == null || getEntry == null)
                    return result;

                int count = (int)getCount.Invoke(null, null);
                var entry = Activator.CreateInstance(logEntryType);
                var conditionField = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var stackTraceField = logEntryType.GetField("stackTrace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < count; i++)
                {
                    var args = new[] { (object)i, entry };
                    getEntry.Invoke(null, args);
                    var mode = modeField?.GetValue(entry);

                    int modeInt = mode is int mi ? mi : -1;
                    if (modeInt == 2 || modeInt == 3 || modeInt == 4)
                    {
                        var cond = conditionField?.GetValue(entry) as string;
                        var stack = stackTraceField?.GetValue(entry) as string;
                        if (!string.IsNullOrEmpty(cond))
                            result.Add(cond + "\n" + stack);
                    }
                }

                return result;
            }
            catch
            {
                return new List<string>();
            }
        }

        static bool CheckExistingArchives(List<BuildProfile> profiles, BuildVersion version)
        {
            var existingArchives = new List<(string name, string path, float sizeMB)>();

            foreach (var profile in profiles)
            {
                var assetPath = AssetDatabase.GetAssetPath(profile);
                if (!TryGetOutputPath(profile, assetPath, out var basePath))
                    continue;


                var versionedPath = InjectVersionIntoPath(basePath, version);

                string sourceDir;
                if (Path.HasExtension(versionedPath))
                    sourceDir = Path.GetDirectoryName(versionedPath);
                else
                    sourceDir = versionedPath;

                if (string.IsNullOrEmpty(sourceDir))
                    continue;

                var archiveName = $"FlyCollector-{profile.name}-{version.ToStringWithPrefix()}.zip";
                var parentDir = Directory.GetParent(Path.GetFullPath(sourceDir))?.FullName;

                if (string.IsNullOrEmpty(parentDir))
                    continue;

                var archivePath = Path.Combine(parentDir, archiveName);

                if (File.Exists(archivePath))
                {
                    var sizeMB = new FileInfo(archivePath).Length / 1048576f;
                    existingArchives.Add((archiveName, archivePath, sizeMB));
                }
            }


            if (existingArchives.Count == 0)
                return true;


            var message = $"The following archives already exist:\n\n";
            foreach (var (name, path, size) in existingArchives)
            {
                message += $"• {name} ({size:0.0} MB)\n";
            }

            message += "\nOverwrite existing archives?";

            return EditorUtility.DisplayDialog(
                "Archives Already Exist",
                message,
                "Overwrite",
                "Cancel"
            );
        }
    }
}
#endif
