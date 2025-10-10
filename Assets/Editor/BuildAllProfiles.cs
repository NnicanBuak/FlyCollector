#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;

namespace BuildTools
{
    /// <summary>
    /// Builds all configured Build Profiles sequentially with custom output paths.
    /// Supports versioning and automatic ZIP archiving.
    /// </summary>
    public static class BuildAllProfiles
    {
        // Map profile name to output path (e.g., "Windows Dev" -> "Builds/WindowsDev/MyGame-Dev.exe")
        static readonly Dictionary<string, string> OutputByProfileName = new Dictionary<string, string>
        {
            {"Windows", "Platforms/Windows/FlyCollector-win/FlyCollector.exe"},
            {"Linux", "Platforms/Linux/FlyCollector-linux/FlyCollector.x86_64"},
            {"Web", "Platforms/Web/Build/"}
        };

        // Map profile asset path to output path (more reliable if profile names change)
        static readonly Dictionary<string, string> OutputByProfileAssetPath = new Dictionary<string, string>
        {
        };

        /// <summary>
        /// Menu command to build all Build Profiles found in the project sequentially.
        /// Output paths must be configured in OutputByProfileName or OutputByProfileAssetPath dictionaries.
        /// </summary>
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

                var options = new BuildPlayerWithProfileOptions
                {
                    buildProfile     = profile,
                    locationPathName = outPath,
                    options          = BuildOptions.None,
                };

                var start = DateTime.Now;
                var report = BuildPipeline.BuildPlayer(options);
                var took  = DateTime.Now - start;

                switch (report.summary.result)
                {
                    case BuildResult.Succeeded:
                        ok++;
                        results.Add($"✅ {profile.name} → {outPath}  [{took:mm\\:ss}]  size: {(report.summary.totalSize/1048576f):0.0} MB");
                        break;
                    case BuildResult.Cancelled:
                        cancel++;
                        results.Add($"⏹ {profile.name} → {outPath}  [{took:mm\\:ss}]  CANCELLED");
                        break;
                    default:
                        fail++;
                        results.Add($"❌ {profile.name} → {outPath}  [{took:mm\\:ss}]  ERRORS: {report.summary.totalErrors}");
                        break;
                }
            }

            Debug.Log($"=== Build All Profiles done ===\n" +
                      $"Success: {ok}, Failed: {fail}, Cancelled: {cancel}\n" +
                      string.Join("\n", results));
        }

        /// <summary>
        /// Resolves output path for a given build profile.
        /// Priority: asset path mapping, then profile name mapping.
        /// </summary>
        static bool TryGetOutputPath(BuildProfile profile, string assetPath, out string outPath)
        {
            if (OutputByProfileAssetPath.TryGetValue(assetPath, out outPath))
                return true;

            if (OutputByProfileName.TryGetValue(profile.name, out outPath))
                return true;

            outPath = string.Empty;
            return false;
        }

        /// <summary>
        /// Creates target directory for build output.
        /// Handles both file paths (creates parent directory) and directory paths.
        /// </summary>
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

        /// <summary>
        /// Builds all Build Profiles with specified version.
        /// Updates PlayerSettings, builds to versioned paths, and creates ZIP archives.
        /// Called by BuildVersionWindow after user inputs version.
        /// </summary>
        public static void BuildAllWithVersion(BuildVersion version)
        {
            Debug.Log($"=== Starting Build All Profiles with version {version.ToStringWithPrefix()} ===");

            // Update Unity project version settings
            UpdatePlayerSettings(version);

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

            // Check for existing archives before building
            if (!CheckExistingArchives(profiles, version))
            {
                Debug.Log("Build cancelled by user (existing archives).");
                return;
            }

            var results = new List<string>();
            int ok = 0, fail = 0, cancel = 0;

            foreach (var profile in profiles)
            {
                var assetPath = AssetDatabase.GetAssetPath(profile);
                if (!TryGetOutputPath(profile, assetPath, out var basePath))
                {
                    Debug.LogWarning($"[SKIP] No output path configured for profile '{profile.name}' ({assetPath}).");
                    continue;
                }

                // Inject version into path
                var versionedPath = InjectVersionIntoPath(basePath, version);
                PrepareDirectories(versionedPath);

                var options = new BuildPlayerWithProfileOptions
                {
                    buildProfile     = profile,
                    locationPathName = versionedPath,
                    options          = BuildOptions.None,
                };

                var start = DateTime.Now;
                var report = BuildPipeline.BuildPlayer(options);
                var took  = DateTime.Now - start;

                switch (report.summary.result)
                {
                    case BuildResult.Succeeded:
                        ok++;
                        var resultMsg = $"✅ {profile.name} → {versionedPath}  [{took:mm\\:ss}]  size: {(report.summary.totalSize/1048576f):0.0} MB";

                        // Archive successful build
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
                        results.Add($"❌ {profile.name} → {versionedPath}  [{took:mm\\:ss}]  ERRORS: {report.summary.totalErrors}");
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

        /// <summary>
        /// Updates Unity PlayerSettings with version information.
        /// Sets bundleVersion and Android versionCode.
        /// </summary>
        static void UpdatePlayerSettings(BuildVersion version)
        {
            var versionString = version.ToStringWithoutPrefix();
            PlayerSettings.bundleVersion = versionString;
            PlayerSettings.Android.bundleVersionCode = version.ToVersionCode();

            Debug.Log($"Updated PlayerSettings: bundleVersion={versionString}, Android.bundleVersionCode={version.ToVersionCode()}");
        }

        /// <summary>
        /// Injects version into build output path.
        /// Examples:
        ///   "Platforms/Windows/Game/Game.exe" -> "Platforms/Windows/Game-v1.0.0/Game.exe"
        ///   "Platforms/Web/Build/" -> "Platforms/Web/Build-v1.0.0/"
        /// </summary>
        static string InjectVersionIntoPath(string basePath, BuildVersion version)
        {
            var versionSuffix = version.ToStringWithPrefix();

            // Normalize path separators to forward slashes
            basePath = basePath.Replace("\\", "/");

            // Check if path has file extension (e.g., .exe)
            if (Path.HasExtension(basePath))
            {
                // Path contains file: "Platforms/Windows/FlyCollector-win/FlyCollector.exe"
                // Split into directory and filename
                var lastSlash = basePath.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    var directory = basePath.Substring(0, lastSlash);
                    var filename = basePath.Substring(lastSlash + 1);

                    // Append version to directory name
                    return $"{directory}-{versionSuffix}/{filename}";
                }
            }
            else
            {
                // Path is directory: "Platforms/Web/Build/" or "Platforms/Web/Build"
                var trimmed = basePath.TrimEnd('/');
                return $"{trimmed}-{versionSuffix}/";
            }

            // Fallback: just append version
            return $"{basePath}-{versionSuffix}";
        }

        /// <summary>
        /// Creates ZIP archive of build directory.
        /// Archive is placed one level above the build folder.
        /// Returns path to created archive or empty string on failure.
        /// </summary>
        static string CreateZipArchive(string buildPath, string profileName, BuildVersion version)
        {
            try
            {
                string sourceDir;

                // Determine source directory to archive
                if (Path.HasExtension(buildPath))
                {
                    // If path is a file (e.g., .exe), archive its parent directory
                    sourceDir = Path.GetDirectoryName(buildPath);
                }
                else
                {
                    // If path is already a directory
                    sourceDir = buildPath;
                }

                if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                {
                    Debug.LogWarning($"Cannot archive: directory not found at {sourceDir}");
                    return string.Empty;
                }

                // Create archive name: FlyCollector-Windows-v1.0.0.zip
                var archiveName = $"FlyCollector-{profileName}-{version.ToStringWithPrefix()}.zip";

                // Place archive one level above build directory (in Platforms/)
                var parentDir = Directory.GetParent(sourceDir)?.FullName;
                if (string.IsNullOrEmpty(parentDir))
                {
                    Debug.LogWarning($"Cannot determine parent directory for {sourceDir}");
                    return string.Empty;
                }

                var archivePath = Path.Combine(parentDir, archiveName);

                // Delete existing archive if present
                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                // Create ZIP archive
                ZipFile.CreateFromDirectory(sourceDir, archivePath, System.IO.Compression.CompressionLevel.Optimal, false);

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

        /// <summary>
        /// Checks if archives with this version already exist for any profiles.
        /// Shows confirmation dialog if conflicts found.
        /// Returns true if should proceed with build, false to cancel.
        /// </summary>
        static bool CheckExistingArchives(List<BuildProfile> profiles, BuildVersion version)
        {
            var existingArchives = new List<(string name, string path, float sizeMB)>();

            foreach (var profile in profiles)
            {
                var assetPath = AssetDatabase.GetAssetPath(profile);
                if (!TryGetOutputPath(profile, assetPath, out var basePath))
                    continue;

                // Calculate what the archive path would be
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

            // If no conflicts, proceed
            if (existingArchives.Count == 0)
                return true;

            // Show confirmation dialog
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
