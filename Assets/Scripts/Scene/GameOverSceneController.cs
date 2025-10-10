using UnityEngine;

[DisallowMultipleComponent]
public class GameOverSceneController : MonoBehaviour
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
        
        // TODO: заменить старый InpitSystem
        // if (Input.anyKeyDown)
        // {
        //     GoNext();
        //     return;
        // }


        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            GoNext();
            return;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            GoNext();
            return;
        }
    }

    private void GoNext()
    {
        if (transitioning) return;
        transitioning = true;

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene("Credits", transition);
        else
            Debug.LogError("[GameOverController] GameSceneManager.Instance == null");
    }
}