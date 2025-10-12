using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class GameSceneManagerBootstrap : MonoBehaviour
{
    [Tooltip("Auto-create GameSceneManager if scene starts without one.")]
    [SerializeField] private bool autoCreateOnAwake = true;

    private void Awake()
    {
        if (!autoCreateOnAwake) return;

        if (GameSceneManager.Instance == null)
        {
            var go = new GameObject("GameSceneManager");
            go.AddComponent<GameSceneManager>();
            // GameSceneManager.Awake will set Instance and DontDestroyOnLoad
        }
    }
}

