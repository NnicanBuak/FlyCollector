using System.Collections;
using UnityEngine;

public class SetGameOutcomeAction : InteractionActionBase
{
    [SerializeField] private Game.GameOutcome outcome = Game.GameOutcome.Timeout;
    [SerializeField] private string key = "gameOutcome";

    public override IEnumerator Execute(InteractionContext ctx)
    {
        var gsm = GameSceneManager.Instance;
        if (gsm != null)
        {
            gsm.SetPersistentData(key, outcome);
        }
        else
        {
            Debug.LogWarning("[SetGameOutcomeAction] GameSceneManager not found");
        }
        yield break;
    }
}
