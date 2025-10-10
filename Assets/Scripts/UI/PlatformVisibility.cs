using UnityEngine;

/// <summary>
/// Controls GameObject visibility based on runtime platform
/// More flexible than compile-time directives
/// </summary>
public class PlatformVisibility : MonoBehaviour
{
    [Header("Platform Settings")]
    [Tooltip("Show on Windows Standalone")]
    [SerializeField] private bool showOnWindows = true;

    [Tooltip("Show on Linux Standalone")]
    [SerializeField] private bool showOnLinux = true;

    [Tooltip("Show on macOS Standalone")]
    [SerializeField] private bool showOnMac = true;

    [Tooltip("Show on WebGL")]
    [SerializeField] private bool showOnWebGL = true;

    [Tooltip("Show on Android")]
    [SerializeField] private bool showOnAndroid = true;

    [Tooltip("Show on iOS")]
    [SerializeField] private bool showOnIOS = true;

    [Header("Action")]
    [Tooltip("Destroy object if hidden (instead of just SetActive(false))")]
    [SerializeField] private bool destroyIfHidden = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Awake()
    {
        bool shouldShow = ShouldShowOnCurrentPlatform();

        if (showDebug)
        {
            Debug.Log($"[PlatformVisibility] Platform: {Application.platform}, Should show '{gameObject.name}': {shouldShow}");
        }

        if (!shouldShow)
        {
            if (destroyIfHidden)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    private bool ShouldShowOnCurrentPlatform()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return showOnWindows;

            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxEditor:
                return showOnLinux;

            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
                return showOnMac;

            case RuntimePlatform.WebGLPlayer:
                return showOnWebGL;

            case RuntimePlatform.Android:
                return showOnAndroid;

            case RuntimePlatform.IPhonePlayer:
                return showOnIOS;

            default:
                // By default, show on unknown platforms
                if (showDebug)
                {
                    Debug.LogWarning($"[PlatformVisibility] Unknown platform: {Application.platform}");
                }
                return true;
        }
    }
}
