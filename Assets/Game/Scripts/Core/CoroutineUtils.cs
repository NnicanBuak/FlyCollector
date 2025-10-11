using System.Collections;
using UnityEngine;

public static class CoroutineUtils
{
    public static IEnumerator WaitZeroAndCollect()
    {

        yield return null;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.InputSystem.Update();
#endif

        yield return null;
    }


    public static IEnumerator WaitZeroAndCollect(this MonoBehaviour _)
    {
        yield return WaitZeroAndCollect();
    }
}
