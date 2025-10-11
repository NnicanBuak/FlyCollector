using System.Collections;
using UnityEngine;

public class PlayAnimationAction : InteractionActionBase
{
    [SerializeField]
    public Animator animator;
    
    [Header("Способ запуска")]
    [SerializeField] private string triggerName;
    [SerializeField] private string stateName;
    [SerializeField] private float minWait = 0f;
    [SerializeField] private float maxWait = 0f;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (animator)
        {
            if (!string.IsNullOrEmpty(triggerName))
                animator.SetTrigger(triggerName);

            if (!string.IsNullOrEmpty(stateName))
            {
                if (minWait > 0f) yield return new WaitForSeconds(minWait);
                float t = 0f; bool stateReached = false;

                while (t < (maxWait > 0 ? maxWait : 2f))
                {
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName(stateName)) { stateReached = true; break; }
                    t += Time.deltaTime;
                    yield return null;
                }

                if (stateReached)
                {
                    while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
                        yield return null;
                }
                else if (maxWait > 0f)
                {
                    yield return new WaitForSeconds(maxWait);
                }
            }
        }
        else if (maxWait > 0f)
        {
            yield return new WaitForSeconds(maxWait);
        }
    }
}