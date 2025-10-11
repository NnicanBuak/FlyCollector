using UnityEngine;

/// <summary>
/// Shows GameObject ONLY on WebGL platform, hides on all others
/// </summary>
public class ShowOnlyOnWebGL : MonoBehaviour
{
#if !UNITY_WEBGL
    [Header("Settings")]
    [Tooltip("Destroy object instead of just disabling it on non-WebGL platforms")]
    [SerializeField] private bool destroyOnNonWebGL = false;
#endif

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Awake()
    {
#if UNITY_WEBGL
        if (showDebug)
        {
            Debug.Log($"[ShowOnlyOnWebGL] Running on WebGL - keeping '{gameObject.name}' visible");
        }
#else
        if (showDebug)
        {
            Debug.Log($"[ShowOnlyOnWebGL] Not WebGL - hiding '{gameObject.name}'");
        }

        if (destroyOnNonWebGL)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
#endif
    }
}
