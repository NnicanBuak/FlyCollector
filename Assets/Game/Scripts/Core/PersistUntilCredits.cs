using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the GameObject alive across scene loads until the credits scene is reached.
/// Once the credits scene loads, the object is moved into that scene so it can be
/// unloaded normally, and this helper component removes itself.
/// </summary>
public class PersistUntilCredits : MonoBehaviour
{
    [SerializeField] private string creditsSceneName = "Credits";

    private bool subscribed;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribed = true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, creditsSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        // Move back into the credits scene so the object is no longer marked as persistent.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        subscribed = false;

        SceneManager.MoveGameObjectToScene(gameObject, scene);
        Destroy(this); // Component no longer needed.
    }

    private void OnDestroy()
    {
        if (subscribed)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            subscribed = false;
        }
    }
}
