using UnityEngine;
using UnityEngine.UI;

public class MainMenuSceneController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private SceneTransition transition;

    void Awake()
    {
        transition = new SceneTransition(0.6f);
    }

    void Start()
    {
        playButton.onClick.AddListener(() =>
        {
            GameSceneManager.Instance.LoadScene("Game", transition);
        });

        quitButton.onClick.AddListener(() => GameSceneManager.Instance.QuitGame());
    }
}