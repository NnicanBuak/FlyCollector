#if UNITY_EDITOR
using System;

namespace BuildTools
{
    /// <summary>
    /// Represents semantic version with optional hotfix suffix.
    /// Supports formats: v1.0.0 or v1.0.0-hotfix1
    /// </summary>
    [Serializable]
    public struct BuildVersion
    {
        public int Major;
        public int Minor;
        public int Patch;
        public bool IsHotfix;
        public int HotfixNumber;

        public BuildVersion(int major, int minor, int patch, bool isHotfix = false, int hotfixNumber = 0)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            IsHotfix = isHotfix;
            HotfixNumber = hotfixNumber;
        }

        /// <summary>
        /// Returns version string with 'v' prefix: v1.0.0 or v1.0.0-hotfix1
        /// </summary>
        public string ToStringWithPrefix()
        {
            var baseVersion = $"v{Major}.{Minor}.{Patch}";
            return IsHotfix ? $"{baseVersion}-hotfix{HotfixNumber}" : baseVersion;
        }

        /// <summary>
        /// Returns version string without prefix (for Unity PlayerSettings.bundleVersion)
        /// </summary>
        public string ToStringWithoutPrefix()
        {
            var baseVersion = $"{Major}.{Minor}.{Patch}";
            return IsHotfix ? $"{baseVersion}-hotfix{HotfixNumber}" : baseVersion;
        }

        /// <summary>
        /// Calculates integer version code for Android builds.
        /// Formula: Major * 10000 + Minor * 100 + Patch + HotfixNumber
        /// </summary>
        public int ToVersionCode()
        {
            return Major * 10000 + Minor * 100 + Patch + (IsHotfix ? HotfixNumber : 0);
        }

        public override string ToString() => ToStringWithPrefix();

        public static BuildVersion Default => new BuildVersion(1, 0, 0);

        /// <summary>
        /// Parses version string into BuildVersion.
        /// Supports formats: "1.0.0", "1.0", "0.2", "1.0.0-hotfix1", "v1.0.0"
        /// </summary>
        /// <param name="versionString">Version string to parse</param>
        /// <param name="version">Parsed version (or Default if parsing fails)</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParse(string versionString, out BuildVersion version)
        {
            version = Default;

            if (string.IsNullOrWhiteSpace(versionString))
                return false;

            try
            {
                // Remove 'v' prefix if present
                var input = versionString.Trim();
                if (input.StartsWith("v") || input.StartsWith("V"))
                    input = input.Substring(1);

                // Check for hotfix suffix
                bool isHotfix = false;
                int hotfixNumber = 0;
                if (input.Contains("-hotfix"))
                {
                    var parts = input.Split(new[] { "-hotfix" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        input = parts[0];
                        isHotfix = true;
                        if (!int.TryParse(parts[1], out hotfixNumber))
                            hotfixNumber = 1;
                    }
                }

                // Parse version numbers (support 1.0, 1.0.0)
                var versionParts = input.Split('.');
                int major = 0, minor = 0, patch = 0;

                if (versionParts.Length >= 1)
                    int.TryParse(versionParts[0], out major);
                if (versionParts.Length >= 2)
                    int.TryParse(versionParts[1], out minor);
                if (versionParts.Length >= 3)
                    int.TryParse(versionParts[2], out patch);

                version = new BuildVersion(major, minor, patch, isHotfix, hotfixNumber);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
