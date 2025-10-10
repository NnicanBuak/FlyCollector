using UnityEngine;

[DisallowMultipleComponent]
public class CreditsController : MonoBehaviour
{
    [Header("Переход")]
    [SerializeField] private float fadeDuration = 0.6f;

    private SceneTransition transition;
    private bool transitioning;

    void Awake()
    {
        transition = new SceneTransition(fadeDuration);
    }

    void Update()
    {
        if (transitioning) return;


        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            GoToMenu();
            return;
        }
    }

    private void GoToMenu()
    {
        if (transitioning) return;
        transitioning = true;

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene("MainMenu", transition);
        else
            Debug.LogError("[CreditsController] GameSceneManager.Instance == null");
    }
}