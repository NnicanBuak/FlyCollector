using System.Collections;
using UnityEngine;

public class LoadSceneAction : InteractionActionBase
{
    [Header("Scene")]
    [SerializeField] private string sceneName = "GameOver";

    [Header("Timing")]
    [SerializeField] private float delay = 0f;

    [Header("Transition")]
    [SerializeField] private bool useFadeTransition = true;
    [SerializeField] private float fadeTime = 1f;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var gsm = GameSceneManager.Instance;
        if (gsm == null)
        {
            Debug.LogError("[LoadSceneAction] GameSceneManager.Instance not found!");
            yield break;
        }

        SceneTransition transition = useFadeTransition ? new SceneTransition(fadeTime) : null;
        gsm.LoadScene(sceneName, transition);



        if (gsm != null)
        {
            if (gsm.WaitUntilLoaded() != null)
                yield return gsm.WaitUntilLoaded();
            else
                while (gsm.IsLoading) yield return null;
        }
    }
}
