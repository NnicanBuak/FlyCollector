using UnityEngine;

public static class InteractionFreeze
{
    private static int counter;
    public static bool IsLocked => counter > 0;

    public static void Push()
    {
        counter++;

    }

    public static void Pop()
    {
        counter = Mathf.Max(0, counter - 1);

    }

    public static void Clear() => counter = 0;
}