using UnityEngine;

/// <summary>
/// Hides GameObject on WebGL platform (useful for platform-specific UI)
/// </summary>
public class HideOnWebGL : MonoBehaviour
{
#if UNITY_WEBGL
    [Header("Settings")]
    [Tooltip("Destroy object instead of just disabling it")]
    [SerializeField] private bool destroyOnWebGL = false;
#endif

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Awake()
    {
#if UNITY_WEBGL
        if (showDebug)
        {
            Debug.Log($"[HideOnWebGL] Running on WebGL - hiding '{gameObject.name}'");
        }

        if (destroyOnWebGL)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
#else
        if (showDebug)
        {
            Debug.Log($"[HideOnWebGL] Not WebGL - keeping '{gameObject.name}' visible");
        }
#endif
    }
}
