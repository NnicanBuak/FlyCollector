using UnityEngine;

[DisallowMultipleComponent]
public class GameSceneController : MonoBehaviour
{
    [Header("Переход")]
    [SerializeField] private float fadeDuration = 0.6f;

    private SceneTransition transition;
    private bool transitioning;

    void Awake()
    {
        transition = new SceneTransition(fadeDuration);
    }

    /// <summary>
    /// Вызови эту функцию из своего игрового кода (смерть игрока/провал/таймер и т.п.)
    /// </summary>
    public void TriggerGameOver()
    {
        if (transitioning) return;
        transitioning = true;

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene("GameOver", transition);
        else
            Debug.LogError("[GameController] GameSceneManager.Instance == null");
    }
}